using System.Collections.Generic;
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

        yield return NetTerminalUtility.MakeGizmo(parent.Map, _power == null || _power.PowerOn);
    }
}
