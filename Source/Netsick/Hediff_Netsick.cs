using RimWorld;
using Verse;

namespace Netrun;

/// <summary>Cyberspace dependency. Grows while connected, fades offline, hurts when cut off.</summary>
public class Hediff_Netsick : HediffWithComps
{
    public int LastConnectedTick;
    public int EmotionalDiveUntilTick = -1;

    private static NetsickSettingsDef Settings => NetrunDefOf.NR_NetsickSettings;

    public bool Withdrawing
        => Severity >= Settings.withdrawalMinSeverity
           && Find.TickManager.TicksGame - LastConnectedTick > Settings.withdrawalAfterDays * GenDate.TicksPerDay;

    public override string LabelInBrackets => Withdrawing ? "NR_NetsickWithdrawalTag".Translate() : base.LabelInBrackets;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref LastConnectedTick, "lastConnectedTick");
        Scribe_Values.Look(ref EmotionalDiveUntilTick, "emotionalDiveUntilTick", -1);
    }
}
