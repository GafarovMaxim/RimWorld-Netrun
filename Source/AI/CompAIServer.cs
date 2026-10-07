using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

public class CompProperties_AIServer : CompProperties
{
    /// <summary>Slots before racks. The main server gets more from linked racks; a coprocessor never does.</summary>
    public int baseSlots = 1;

    /// <summary>True for the cyberpod coprocessor: its AI boosts the pod instead of offering services.</summary>
    public bool coprocessor;

    public CompProperties_AIServer() => compClass = typeof(CompAIServer);
}

/// <summary>
/// Hosts awakened AIs. The main AI server starts with one slot and gains one per linked server rack;
/// a netrun coprocessor holds a single AI that works for its cyberpod.
/// </summary>
public class CompAIServer : ThingComp
{
    // Kept from 0.3.0 saves, when core modules were installed straight into the server.
    private int _installedModules;
    private CompPowerTrader _power;
    private CompAffectedByFacilities _facilities;

    private static AISettingsDef Settings => NetrunDefOf.NR_AI;
    public CompProperties_AIServer Props => (CompProperties_AIServer)props;

    public bool IsCoprocessor => Props.coprocessor;

    public int ActiveRacks => _facilities?.LinkedFacilitiesListForReading
        .Count(f => f.def == NetrunDefOf.NR_AIServerRack && _facilities.IsFacilityActive(f)) ?? 0;

    public int Slots => IsCoprocessor
        ? Props.baseSlots
        : System.Math.Min(Settings.maxSlots, Props.baseSlots + ActiveRacks + _installedModules);

    public int Used => AIManager.Get()?.InServer((Building)parent).Count() ?? 0;
    public bool HasFreeSlot => Used < Slots;
    public bool Powered => _power == null || _power.PowerOn;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        _power = parent.GetComp<CompPowerTrader>();
        _facilities = parent.GetComp<CompAffectedByFacilities>();
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
        AIManager.Get()?.OnServerDestroyed((Building)parent, previousMap, parent.Position);
    }

    /// <summary>The main server opens its AI room straight from the building (only the AIs, not the whole terminal).</summary>
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo g in base.CompGetGizmosExtra())
            yield return g;
        if (IsCoprocessor || parent.Faction != Faction.OfPlayer)
            yield break;
        yield return new Command_Action
        {
            defaultLabel = "NR_AIServerGizmo".Translate(),
            defaultDesc = "NR_AIServerGizmoDesc".Translate(),
            icon = NetrunTex.AIUse,
            action = () => Find.WindowStack.Add(new Dialog_AIServer(parent.Map)),
        };
    }

    public override string CompInspectStringExtra()
    {
        var lines = new List<string> { "NR_ServerSlots".Translate(Used, Slots) };
        foreach (NetAI ai in AIManager.Get()?.InServer((Building)parent) ?? [])
            lines.Add($"  {ai.Name} ({ai.Personality.Label()})");
        if (!Powered && Used > 0)
            lines.Add("NR_ServerAsleep".Translate());
        return string.Join("\n", lines);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref _installedModules, "installedModules");
    }
}

/// <summary>Only one AI server per colony map (blueprints and frames count too).</summary>
public class PlaceWorker_OnlyOneAIServer : PlaceWorker
{
    public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
    {
        ThingDef def = NetrunDefOf.NR_AIServer;
        bool exists = map.listerBuildings.AllBuildingsColonistOfDef(def).Any()
            || map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).Any(t => t.def.entityDefToBuild == def && t != thingToIgnore)
            || map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame).Any(t => t.def.entityDefToBuild == def && t != thingToIgnore);
        return exists ? "NR_OnlyOneAIServer".Translate() : AcceptanceReport.WasAccepted;
    }
}
