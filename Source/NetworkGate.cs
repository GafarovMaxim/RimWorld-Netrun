using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>The four onboarding steps of the Core (docs/CORE_V3.md), in order.</summary>
public enum OnboardingStep
{
    Console,
    Cyberpod,
    Cyberlink,
    FirstHack,
}

/// <summary>
/// Newcomer protection (docs/NEWCOMER.md): Netrun's threats wait until the colony has "touched the net".
/// Opens for good once any of these happen: Heat above zero, a cyberpod / AI server / server rack /
/// coprocessor / data server built, a colonist with a cyberlink, or a dive. Saved with the game.
/// Also keeps the Core's onboarding (docs/CORE_V3.md): the checklist until its steps are done, then the
/// nodes open one by one.
/// </summary>
public class NetworkGate : GameComponent
{
    private const int CheckInterval = 2000;

    /// <summary>Incidents that do not depend on Heat or contacts and may come at any time.</summary>
    private static readonly HashSet<string> AlwaysAllowed = ["NR_BlackMarket", "NR_DistressSignal"];

    private static readonly string[] NetBuildings = ["USH_Cyberpod", "USH_DataServer", "NR_AIServer", "NR_AIServerRack", "NR_NetrunCoprocessor"];

    private bool _opened;
    private bool _onboardingDone;
    private HashSet<string> _unlocked = [];

    public NetworkGate(Game game) { }

    public static NetworkGate Get() => GameComp<NetworkGate>.Get();

    public bool Opened => _opened;
    public bool OnboardingDone => _onboardingDone;

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

    /// <summary>Dev: close the gate again to test the protection (and show the onboarding again).</summary>
    public void Close()
    {
        _opened = false;
        _onboardingDone = false;
        _unlocked.Clear();
    }

    /// <summary>Called right away for an obvious signal (a dive starting, Heat added).</summary>
    public static void Notify() => Get()?.Open();

    /// <summary>A Core node becomes available (kept for good).</summary>
    public static void Unlock(string node) => Get()?._unlocked.Add(node);

    public bool IsUnlocked(string node) => _onboardingDone && _unlocked.Contains(node);

    public void SkipOnboarding()
    {
        _onboardingDone = true;
        _unlocked.UnionWith(["Club", "Board", "Archive", "Server", "Studio", "Gateway"]);
    }

    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % CheckInterval != 0)
            return;
        Refresh(Find.AnyPlayerHomeMap);
    }

    /// <summary>Re-checks the gate, the onboarding and the node unlocks (cheap; the terminal calls it about once a second).</summary>
    public void Refresh(Map map)
    {
        if (!_opened && Touched())
            Open();
        if (map == null)
            return;

        if (!_onboardingDone && System.Enum.GetValues(typeof(OnboardingStep)).Cast<OnboardingStep>().All(s => StepDone(s, map)))
        {
            _onboardingDone = true;
            Open();
            Messages.Message("NR_OnboardingDone".Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
        }
        if (!_onboardingDone)
            return;

        HeatTracker heat = HeatTracker.Get();
        bool everHeat = heat != null && (heat.Heat > 0f || heat.Log.Count > 0);
        if (everHeat)
        {
            _unlocked.Add("Club");
            _unlocked.Add("Archive");
        }
        _unlocked.Add("Gateway");
        if (FixerManager.Get()?.Fixers.Any(f => f.Reputation > 0f || f.ChannelOpen) == true || ContractManager.Get()?.Active.Any() == true)
            _unlocked.Add("Board");
        if (AIManager.Get()?.AIs.Count > 0 || map.listerBuildings.ColonistsHaveBuilding(NetrunDefOf.NR_AIServer) || AIAwakening.Fragments(map).Any())
            _unlocked.Add("Server");
        if (ColonyGoods.Count(map, NetrunDefOf.NR_BraindanceGoggles) > 0
            || map.mapPawns.FreeColonists.Any(p => p.apparel?.WornApparel.Any(a => a.def == NetrunDefOf.NR_BraindanceGoggles) == true)
            || ColonyGoods.Count(map, NetrunDefOf.NR_BraindanceRaw) > 0 || ColonyGoods.Count(map, NetrunDefOf.NR_BraindanceChip) > 0)
            _unlocked.Add("Studio");
    }

    public static bool StepDone(OnboardingStep step, Map map)
    {
        HeatTracker heat = HeatTracker.Get();
        return step switch
        {
            OnboardingStep.Console => map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.CommsConsole).Any(b => b.TryGetComp<CompNetTerminal>() != null),
            OnboardingStep.Cyberpod => DefDatabase<ThingDef>.GetNamedSilentFail("USH_Cyberpod") is { } pod && map.listerBuildings.ColonistsHaveBuilding(pod),
            OnboardingStep.Cyberlink => map.mapPawns.FreeColonists.Any(p => p.IsHacker()),
            OnboardingStep.FirstHack => heat != null && (heat.Heat > 0f || heat.Log.Count > 0),
            _ => false,
        };
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
        Scribe_Values.Look(ref _onboardingDone, "onboardingDone");
        Scribe_Collections.Look(ref _unlocked, "unlocked", LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            _unlocked ??= [];
            // A save from before the onboarding that already has trace history: skip the checklist.
            if (!_onboardingDone && HeatTracker.Get()?.Log.Count > 0)
                SkipOnboarding();
        }
    }
}

/// <summary>Which settings group a Netrun incident belongs to.</summary>
public static class EventGroups
{
    private static readonly HashSet<string> MapEvents = ["NR_DataCourier", "NR_BlackMarket", "NR_DistressSignal", "NR_MapHack"];

    public static bool IsScheduled(IncidentDef def) => MapEvents.Contains(def.defName);

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
    public static void Postfix(IncidentWorker __instance, ref float __result)
    {
        // Map events run on their own schedule (MapEventsManager); the storyteller does not pick them.
        if (__instance.def != null && EventGroups.IsScheduled(__instance.def))
            __result = 0f;
        else
            __result *= EventGroups.Frequency(__instance.def);
    }
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
