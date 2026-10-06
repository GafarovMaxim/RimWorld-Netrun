using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// Newcomer protection (docs/NEWCOMER.md): Netrun's threats wait until the colony has "touched the net".
/// Opens for good once any of these happen: Heat above zero, a cyberpod / AI server / server rack /
/// coprocessor / data server built, a colonist with a cyberlink, or a dive. Saved with the game.
/// </summary>
public class NetworkGate : GameComponent
{
    private const int CheckInterval = 2000;

    /// <summary>Incidents that do not depend on Heat or contacts and may come at any time.</summary>
    private static readonly HashSet<string> AlwaysAllowed = ["NR_BlackMarket", "NR_DistressSignal"];

    private static readonly string[] NetBuildings = ["USH_Cyberpod", "USH_DataServer", "NR_AIServer", "NR_AIServerRack", "NR_NetrunCoprocessor"];

    private bool _opened;

    public NetworkGate(Game game) { }

    public static NetworkGate Get() => Current.Game?.GetComponent<NetworkGate>();

    public bool Opened => _opened;

    /// <summary>True when Netrun's threats may come: the colony is online, or the protection is off in the settings.</summary>
    public static bool IsOpen => !NetrunMod.Settings.NewcomerProtection || Get()?._opened != false;

    public static bool Allows(IncidentDef def)
        => def == null || !def.defName.StartsWith("NR_") || AlwaysAllowed.Contains(def.defName) || IsOpen;

    public void Open()
    {
        if (_opened)
            return;
        _opened = true;
        Log.Message("[Netrun] The colony is online: network events are enabled.");
    }

    /// <summary>Dev: close the gate again to test the protection.</summary>
    public void Close() => _opened = false;

    /// <summary>Called right away for an obvious signal (a dive starting, Heat added).</summary>
    public static void Notify() => Get()?.Open();

    public override void GameComponentTick()
    {
        if (_opened || Find.TickManager.TicksGame % CheckInterval != 0)
            return;
        if (Touched())
            Open();
    }

    private static bool Touched()
    {
        if ((HeatTracker.Get()?.Heat ?? 0f) > 0f || GatewayManager.Get()?.LastReport != null)
            return true;
        foreach (Map map in Find.Maps.Where(m => m.IsPlayerHome))
        {
            foreach (string defName in NetBuildings)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def != null && map.listerBuildings.ColonistsHaveBuilding(def))
                    return true;
            }
            if (map.mapPawns.FreeColonists.Any(p => p.IsHacker()))
                return true;
        }
        return false;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref _opened, "opened");
    }
}

/// <summary>Which settings group a Netrun incident belongs to.</summary>
public static class EventGroups
{
    private static readonly HashSet<string> MapEvents = ["NR_DataCourier", "NR_BlackMarket", "NR_DistressSignal", "NR_MapHack"];

    /// <summary>The frequency setting for this incident: net attacks (attacks, mercenaries, corporate hunt) or map events; 1 for others.</summary>
    public static float Frequency(IncidentDef def)
    {
        if (def == null || !def.defName.StartsWith("NR_"))
            return 1f;
        if (MapEvents.Contains(def.defName))
            return NetrunMod.Settings.MapEventFrequency;
        if (def.defName.StartsWith("NR_NetAttack_") || def.defName == "NR_MercRaid" || def.defName == "NR_CorpHunt")
            return NetrunMod.Settings.NetAttackFrequency;
        return 1f;
    }
}

[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.BaseChanceThisGame), MethodType.Getter)]
public static class Patch_IncidentWorker_BaseChance
{
    public static void Postfix(IncidentWorker __instance, ref float __result) => __result *= EventGroups.Frequency(__instance.def);
}

/// <summary>Netrun incidents stay quiet until the colony is online, or when their group is off (dev "Execute incident" still works).</summary>
[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.CanFireNow))]
public static class Patch_IncidentWorker_CanFireNow
{
    public static void Postfix(IncidentWorker __instance, ref bool __result)
    {
        if (__result && (!NetworkGate.Allows(__instance.def) || EventGroups.Frequency(__instance.def) <= 0f))
            __result = false;
    }
}
