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

public enum ServerTemp { Normal, Cooled, Overheated, Asleep }

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

    // Saved, so heat sleep holds through a load until the room cools to the wake mark (hysteresis).
    private ServerTemp _temp;
    private ServerTemp? _forced;

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
    public bool Powered => (_power == null || _power.PowerOn) && _temp != ServerTemp.Asleep;
    public ServerTemp Temp => _temp;
    /// <summary>Asleep from heat while the power is on: the AI only sleeps, it is never lost for this.</summary>
    public bool HeatAsleep => _temp == ServerTemp.Asleep && (_power == null || _power.PowerOn);
    public float CoolFactor => _temp == ServerTemp.Cooled ? 1f - Settings.serverCoolBonus : 1f;

    /// <summary>Hourly, from the AI manager's tick: reads the room temperature. A forced dev state wins.</summary>
    public void RefreshTemp()
    {
        if (IsCoprocessor || !parent.Spawned)
            return;
        float t = parent.AmbientTemperature;
        // Falls asleep above serverSleepTemp, wakes only at serverWakeTemp or below.
        _temp = _forced ?? (t > Settings.serverSleepTemp || (_temp == ServerTemp.Asleep && t > Settings.serverWakeTemp) ? ServerTemp.Asleep
            : t > Settings.serverHotTemp ? ServerTemp.Overheated
            : t <= Settings.serverCoolTemp ? ServerTemp.Cooled : ServerTemp.Normal);
    }

    /// <summary>Dev: pin a state (null returns to the real room reading).</summary>
    public void DevForce(ServerTemp? state)
    {
        _forced = state;
        RefreshTemp();
    }

    public string TempLabel => ("NR_ServerTemp_" + _temp).Translate();
    public string TempTip => ("NR_ServerTempTip_" + _temp).Translate(parent.AmbientTemperature.ToStringTemperature("F0"),
        Settings.serverCoolTemp.ToStringTemperature("F0"), Settings.serverHotTemp.ToStringTemperature("F0"), Settings.serverSleepTemp.ToStringTemperature("F0"),
        Settings.serverCoolBonus.ToStringPercent("F0"), Settings.serverWakeTemp.ToStringTemperature("F0"));

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
        if (parent.Faction != Faction.OfPlayer)
            yield break;
        if (IsCoprocessor)
        {
            // C8: an AI riding in this coprocessor moves to another free one straight from the building.
            foreach (NetAI ai in AIManager.Get()?.InServer((Building)parent) ?? [])
            {
                NetAI moving = ai;
                var move = new Command_Action
                {
                    defaultLabel = "NR_AIToOtherCoprocessor".Translate() + ": " + ai.Name,
                    defaultDesc = "NR_AIToOtherCoprocessorTip".Translate(),
                    icon = NetrunTint.Of(NetrunTex.AIToCoprocessor),
                    action = () => AIHosting.OpenCoprocessorMenu(moving, parent.Map),
                };
                AcceptanceReport can = AIHosting.CanMoveBetweenCoprocessors(ai, parent.Map);
                if (!can.Accepted)
                    move.Disable(can.Reason);
                yield return move;
            }
            yield break;
        }
        yield return new Command_Action
        {
            defaultLabel = "NR_AIServerGizmo".Translate(),
            defaultDesc = "NR_AIServerGizmoDesc".Translate(),
            icon = NetrunTex.HomeNode("Server", NetrunTex.TabIcon(3)) ?? NetrunTex.AIUse,
            action = () => Find.WindowStack.Add(new Dialog_AIServer(parent.Map)),
        };
    }

    public override string CompInspectStringExtra()
    {
        var lines = new List<string> { "NR_ServerSlots".Translate(Used, Slots) };
        foreach (NetAI ai in AIManager.Get()?.InServer((Building)parent) ?? [])
            lines.Add($"  {ai.Name} ({ai.Personality.Label()})");
        if (!IsCoprocessor)
            lines.Add(TempLabel + " (" + parent.AmbientTemperature.ToStringTemperature("F0") + ")");
        if (!Powered && Used > 0)
            lines.Add(HeatAsleep ? "NR_ServerAsleepHot".Translate(Settings.serverWakeTemp.ToStringTemperature("F0")) : "NR_ServerAsleep".Translate());
        return string.Join("\n", lines);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref _installedModules, "installedModules");
        Scribe_Values.Look(ref _temp, "roomTemp", ServerTemp.Normal);
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
