using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>Colony-wide digital trace. Rises from hacks, cools down over time.</summary>
public class HeatTracker : GameComponent
{
    public const float MaxHeat = 100f;
    private const int TickInterval = 250;
    private const int MaxLogEntries = 30;
    /// <summary>Heat is sampled once an hour for the 7-day graph on the Heat screen.</summary>
    public const int HistoryInterval = GenDate.TicksPerHour;
    public const int HistorySize = 7 * 24;

    private float _heat;
    private List<HeatLogEntry> _log = [];
    private List<float> _history = [];

    public HeatTracker(Game game) { }

    public static HeatTracker Get() => Current.Game?.GetComponent<HeatTracker>();

    public float Heat => _heat;
    public HeatLevel Level => HeatLevelUtility.LevelOf(_heat);

    /// <summary>Newest entries last.</summary>
    public IReadOnlyList<HeatLogEntry> Log => _log;

    /// <summary>Hourly Heat samples, oldest first, up to 7 days.</summary>
    public IReadOnlyList<float> History => _history;

    /// <summary>Adds heat after the settings multiplier is applied.</summary>
    public void AddHeat(float amount, string reason)
    {
        if (amount <= 0f)
            return;

        HeatLevel before = Level;
        float old = _heat;
        _heat = Mathf.Min(MaxHeat, _heat + amount * NetrunMod.Settings.HeatGainMultiplier);
        AddLog(_heat - old, reason);

        if (Level > before)
            NotifyLevelUp();
    }

    public void ReduceHeat(float amount, string reason)
    {
        float old = _heat;
        ReduceHeatSilently(amount);

        if (old != _heat)
            AddLog(_heat - old, reason);
    }

    private void ReduceHeatSilently(float amount)
    {
        if (amount <= 0f)
            return;

        _heat = Mathf.Max(0f, _heat - amount);
    }

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

        float decay = NetrunMod.Settings.HeatDecayPerDay * TickInterval / GenDate.TicksPerDay;
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
    }

    private void AddLog(float amount, string text)
    {
        _log.Add(new HeatLogEntry(Find.TickManager.TicksGame, amount, text));

        if (_log.Count > MaxLogEntries)
            _log.RemoveRange(0, _log.Count - MaxLogEntries);
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
