using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>Colony-wide digital trace. Rises from hacks, cools down over time.</summary>
public class HeatTracker : GameComponent
{
    public const float MaxHeat = 100f;
    private const int TickInterval = 250;

    private float _heat;

    public HeatTracker(Game game) { }

    public static HeatTracker Get() => Current.Game?.GetComponent<HeatTracker>();

    public float Heat => _heat;
    public HeatLevel Level => HeatLevelUtility.LevelOf(_heat);

    /// <summary>Adds heat after the settings multiplier is applied.</summary>
    public void AddHeat(float amount)
    {
        if (amount <= 0f)
            return;

        HeatLevel before = Level;
        _heat = Mathf.Min(MaxHeat, _heat + amount * NetrunMod.Settings.HeatGainMultiplier);

        if (Level > before)
            NotifyLevelUp();
    }

    public void ReduceHeat(float amount)
    {
        if (amount <= 0f)
            return;

        _heat = Mathf.Max(0f, _heat - amount);
    }

    public override void GameComponentTick()
    {
        if (_heat <= 0f || Find.TickManager.TicksGame % TickInterval != 0)
            return;

        float decay = NetrunMod.Settings.HeatDecayPerDay * TickInterval / GenDate.TicksPerDay;
        ReduceHeat(decay);
    }

    private void NotifyLevelUp()
    {
        MessageTypeDef type = Level >= HeatLevel.Target
            ? MessageTypeDefOf.ThreatSmall
            : MessageTypeDefOf.CautionInput;

        Messages.Message("NR_HeatLevelUp".Translate(Level.Label()), type, historical: true);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref _heat, "heat", 0f);
    }
}
