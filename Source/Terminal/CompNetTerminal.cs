using System.Collections.Generic;
using RimWorld;
using UnityEngine;
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
/// edit braindance recordings. Awakening an AI fragment is a gizmo on the console (fragment, then colonist).
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

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            yield return gizmo;

        if (parent.Faction != Faction.OfPlayer)
            yield break;

        var awaken = new Command_Action
        {
            defaultLabel = "NR_CommandAwakenAI".Translate(),
            defaultDesc = "NR_CommandAwakenAIDesc".Translate(),
            icon = NetrunTex.AIAwaken,
            action = () => AIAwakening.OpenMenu(parent.Map),
        };
        AcceptanceReport report = AIAwakening.CanAwaken(parent.Map);
        if (!report.Accepted)
            awaken.Disable(report.Reason);
        yield return awaken;
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
            yield return new FloatMenuOption(terminal + " (" + "NR_TerminalNoPower".Translate() + ")", null, NetrunTex.NetTerminal, Color.white);
            yield break;
        }
        if (!selPawn.CanReach(parent, PathEndMode.InteractionCell, Danger.Some))
        {
            yield return new FloatMenuOption(terminal + " (" + "NoPath".Translate() + ")", null, NetrunTex.NetTerminal, Color.white);
            yield break;
        }

        yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(terminal, () =>
            selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(NetrunDefOf.NR_UseNetTerminal, parent), JobTag.Misc), NetrunTex.NetTerminal, Color.white), selPawn, parent);

        foreach (FloatMenuOption option in BraindanceEditing.FloatOptions(selPawn, (Building)parent))
            yield return option;
    }
}
