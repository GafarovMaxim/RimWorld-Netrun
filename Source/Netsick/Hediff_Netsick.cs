using RimWorld;
using Verse;

namespace Netrun;

/// <summary>Cyberspace dependency. Grows while connected, fades offline, hurts when cut off.</summary>
public class Hediff_Netsick : HediffWithComps
{
    public int LastConnectedTick;
    public int EmotionalDiveUntilTick = -1;
    /// <summary>A craving sent the pawn to a pod; the emotional dive starts as soon as they lie down (even if the break itself ended on the way).</summary>
    public int CravingSinceTick = -1;

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
        Scribe_Values.Look(ref CravingSinceTick, "cravingSinceTick", -1);
    }
}
