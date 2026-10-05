using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>Added to the vanilla comms console by an XML patch.</summary>
public class CompProperties_NetTerminal : CompProperties
{
    public CompProperties_NetTerminal() => compClass = typeof(CompNetTerminal);
}

public class CompNetTerminal : ThingComp
{
    private CompPowerTrader _power;

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

        bool powered = _power == null || _power.PowerOn;
        yield return NetTerminalUtility.MakeGizmo(parent.Map, powered);

        if (!AIAwakening.Fragments(parent.Map).Any())
            yield break;

        var awaken = new Command_Action
        {
            defaultLabel = "NR_CommandAwakenAI".Translate(),
            defaultDesc = "NR_CommandAwakenAIDesc".Translate(),
            icon = NetrunDefOf.NR_AIFragment.uiIcon,
            action = () => AIAwakening.OpenMenu(parent.Map),
        };
        AcceptanceReport report = AIAwakening.CanAwaken(parent.Map);
        if (!report.Accepted)
            awaken.Disable(report.Reason);
        yield return awaken;
    }
}
