using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Netrun;

/// <summary>Added to the vanilla comms console by an XML patch.</summary>
public class CompProperties_NetTerminal : CompProperties
{
    public CompProperties_NetTerminal() => compClass = typeof(CompNetTerminal);
}

/// <summary>
/// The comms console as a network terminal: colonists walk up to it to open the terminal or to
/// awaken an AI fragment they carry there. No gizmos: everything goes through the colonist.
/// </summary>
public class CompNetTerminal : ThingComp
{
    private CompPowerTrader _power;

    public bool Powered => _power == null || _power.PowerOn;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        _power = parent.GetComp<CompPowerTrader>();
    }

    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        foreach (FloatMenuOption option in base.CompFloatMenuOptions(selPawn))
            yield return option;

        if (parent.Faction != Faction.OfPlayer)
            yield break;

        string terminal = "NR_FloatOpenTerminal".Translate();
        if (!Powered)
        {
            yield return new FloatMenuOption(terminal + " (" + "NR_TerminalNoPower".Translate() + ")", null);
            yield break;
        }
        if (!selPawn.CanReach(parent, PathEndMode.InteractionCell, Danger.Some))
        {
            yield return new FloatMenuOption(terminal + " (" + "NoPath".Translate() + ")", null);
            yield break;
        }

        yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(terminal, () =>
            selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(NetrunDefOf.NR_UseNetTerminal, parent), JobTag.Misc)), selPawn, parent);

        foreach (FloatMenuOption option in AIAwakening.FloatOptions(selPawn, (Building)parent))
            yield return option;
    }
}
