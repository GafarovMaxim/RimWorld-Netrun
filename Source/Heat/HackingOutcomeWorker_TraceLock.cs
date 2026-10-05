using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>Hacking Expansion ICE outcome: the intrusion is traced and the colony's Heat jumps.</summary>
public class HackingOutcomeWorker_TraceLock(HackingOutcomeDef def) : HackingOutcomeWorker(def)
{
    public override void ApplyOutcome(Pawn hacker, Thing caster)
    {
        if (!CanApplyOnPawn(hacker).Accepted)
            return;

        base.ApplyOutcome(hacker, caster);

        if (hacker.Faction != Faction.OfPlayer)
            return;

        string text = "NR_LogTraceLock".Translate(caster.LabelCap);
        HeatTracker.Get()?.AddHeat(NetrunDefOf.NR_Heat.traceLockHeat, text);
        Messages.Message(text, new LookTargets(hacker), MessageTypeDefOf.NegativeEvent, historical: true);
    }
}
