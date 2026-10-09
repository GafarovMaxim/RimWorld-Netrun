using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>Colony-wide digital trace. Rises from hacks, cools down over time.</summary>
public class HeatTracker : GameComponent, INetrunCleanable
{
    public const float MaxHeat = 100f;
    private const int TickInterval = 250;
    private const int MaxLogEntries = 200;
    /// <summary>Heat is sampled once an hour for the 7-day graph on the Heat screen.</summary>
    public const int HistoryInterval = GenDate.TicksPerHour;
    public const int HistorySize = 7 * 24;

    private float _heat;
    private List<HeatLogEntry> _log = [];
    private List<float> _history = [];

    public HeatTracker(Game game) { }

    public static HeatTracker Get() => GameComp<HeatTracker>.Get();

    public float Heat => _heat;
    public HeatLevel Level => HeatLevelUtility.LevelOf(_heat);

    /// <summary>Newest entries last.</summary>
    public IReadOnlyList<HeatLogEntry> Log => _log;

    /// <summary>Passive cooling per day right now: the mod setting, halved (by Defs) at Target and above.</summary>
    public float DecayPerDay => NetrunMod.Settings.HeatDecayPerDay
        * (Level >= HeatLevel.Target ? NetrunDefOf.NR_Heat.highHeatDecayFactor : 1f);

    /// <summary>Hourly Heat samples, oldest first, up to 7 days.</summary>
    public IReadOnlyList<float> History => _history;

    /// <summary>Adds heat after the settings multiplier is applied.</summary>
    public void AddHeat(float amount, string reason, HeatSource source = HeatSource.Other)
    {
        if (amount <= 0f)
            return;
        NetworkGate.Notify();

        HeatLevel before = Level;
        float old = _heat;
        _heat = Mathf.Min(MaxHeat, _heat + amount * NetrunMod.Settings.HeatGainMultiplier * NetrunMod.Settings.NewcomerEase);
        AddLog(_heat - old, reason, source);
        EconomyTracker.Get()?.Note(heatUp: _heat - old);

        if (Level > before)
        {
            NotifyLevelUp();
            NetrunSound.Play(Level >= HeatLevel.Target ? "UI_Alert" : "UI_HeatUp");
        }
    }

    /// <summary>Dev: put Heat at an exact value (no multipliers), logged.</summary>
    public void DevSetHeat(float value, string reason)
    {
        float target = Mathf.Clamp(value, 0f, MaxHeat);
        if (target > _heat)
        {
            float multiplier = NetrunMod.Settings.HeatGainMultiplier * NetrunMod.Settings.NewcomerEase;
            AddHeat((target - _heat) / (multiplier > 0f ? multiplier : 1f), reason);
        }
        else if (target < _heat)
        {
            _ignoreNoiseFloor = true;
            ReduceHeat(_heat - target, reason);
            _ignoreNoiseFloor = false;
        }
    }

    /// <summary>Days for Heat to cool to zero with no new trace (slower part above Target).</summary>
    public float DaysToZero()
    {
        float low = NetrunMod.Settings.HeatDecayPerDay;
        if (low <= 0f)
            return float.PositiveInfinity;
        float high = low * NetrunDefOf.NR_Heat.highHeatDecayFactor;
        float target = HeatLevel.Target.MinHeat();
        return _heat <= target ? _heat / low : (_heat - target) / Mathf.Max(0.01f, high) + target / low;
    }

    /// <summary>Days to cool below the current level's threshold (0 at Shadow).</summary>
    public float DaysToLowerLevel()
    {
        if (Level == HeatLevel.Shadow)
            return 0f;
        float min = Level.MinHeat();
        float rate = Level >= HeatLevel.Target ? NetrunMod.Settings.HeatDecayPerDay * NetrunDefOf.NR_Heat.highHeatDecayFactor : NetrunMod.Settings.HeatDecayPerDay;
        return rate <= 0f ? float.PositiveInfinity : (_heat - min + 0.5f) / rate;
    }

    public void ReduceHeat(float amount, string reason, HeatSource source = HeatSource.Other)
    {
        float old = _heat;
        HeatLevel before = Level;
        ReduceHeatSilently(amount);
        if (Level < before)
            NetrunSound.Play("UI_HeatDown");

        if (old != _heat)
        {
            AddLog(_heat - old, reason, source);
            EconomyTracker.Get()?.Note(heatDown: old - _heat);
        }
    }

    private void ReduceHeatSilently(float amount)
    {
        if (amount <= 0f)
            return;

        // Network noise (awake AIs) keeps a floor under the trace: cooling and scrubbing stop there.
        float floor = _ignoreNoiseFloor ? 0f : AIBalance.NoiseFloor;
        if (_heat <= floor)
            return;
        _heat = Mathf.Max(floor, Mathf.Max(0f, _heat - amount));
    }

    private bool _ignoreNoiseFloor;

    /// <summary>The noise floor right now (0 with no or one awake AI).</summary>
    public float NoiseFloor => AIBalance.NoiseFloor;

    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % HistoryInterval == 0)
        {
            _history.Add(_heat);
            if (_history.Count > HistorySize)
                _history.RemoveRange(0, _history.Count - HistorySize);
        }

        if (_heat <= 0f || Find.TickManager.TicksGame % TickInterval != 0)
            return;

        float decay = DecayPerDay * TickInterval / GenDate.TicksPerDay;
        ReduceHeatSilently(decay);
    }

    private void NotifyLevelUp()
    {
        MessageTypeDef type = Level >= HeatLevel.Target
            ? MessageTypeDefOf.ThreatSmall
            : MessageTypeDefOf.CautionInput;

        string text = "NR_HeatLevelUp".Translate(Level.Label());
        Messages.Message(text, type, historical: true);
        AddLog(0f, text);
        _log[_log.Count - 1].Level = (int)Level;
    }

    /// <summary>A journal line with no Heat change (a raid, an attack, an event).</summary>
    public void Note(string text, HeatSource source) => AddLog(0f, text, source);

    private void AddLog(float amount, string text, HeatSource source = HeatSource.Other)
    {
        _log.Add(new HeatLogEntry(Find.TickManager.TicksGame, amount, text) { Source = source, After = _heat });

        if (_log.Count > MaxLogEntries)
            _log.RemoveRange(0, _log.Count - MaxLogEntries);
    }

    public int Cleanup(CleanupReason reason)
    {
        int n = _log.RemoveAll(e => e == null);
        if (_log.Count > MaxLogEntries)
        {
            n += _log.Count - MaxLogEntries;
            _log.RemoveRange(0, _log.Count - MaxLogEntries);
        }
        if (_history.Count > HistorySize)
        {
            n += _history.Count - HistorySize;
            _history.RemoveRange(0, _history.Count - HistorySize);
        }
        return n;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref _heat, "heat", 0f);
        Scribe_Collections.Look(ref _log, "log", LookMode.Deep);
        Scribe_Collections.Look(ref _history, "history", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            _log ??= [];
            _history ??= [];
        }
    }
}
