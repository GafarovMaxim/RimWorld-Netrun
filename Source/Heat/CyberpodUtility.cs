using System.Collections.Generic;
using System.Linq;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// The Netrun coprocessor is an AI host for a cyberpod: the AI inside gives the pod a bonus
/// that depends on its personality. An empty coprocessor does nothing.
/// </summary>
public static class CyberpodUtility
{
    private static HeatSettingsDef Settings => NetrunDefOf.NR_Heat;

    /// <summary>The colony's cyberpods from Hacking Expansion's own registry (no scan of all buildings).</summary>
    public static IEnumerable<Building_Cyberpod> PlayerPods(Map map)
        => map?.GetComponent<MapComponent_CyberpodManager>()?.Pods.Where(p => p.Spawned && p.Faction == Faction.OfPlayer)
           ?? Enumerable.Empty<Building_Cyberpod>();

    /// <summary>The awake AI in a powered coprocessor linked to this pod, if any.</summary>
    public static NetAI CoprocessorAI(Building_Cyberpod pod)
    {
        CompAffectedByFacilities facilities = pod?.GetComp<CompAffectedByFacilities>();
        if (facilities == null)
            return null;

        Thing coprocessor = facilities.LinkedFacilitiesListForReading
            .FirstOrDefault(f => f.def == NetrunDefOf.NR_NetrunCoprocessor && facilities.IsFacilityActive(f));
        if (coprocessor == null)
            return null;

        return AIManager.Get()?.AIs.FirstOrDefault(a => a.Server == coprocessor && a.Active);
    }

    /// <summary>True when the hacker is in a cyberpod; <paramref name="ai"/> is the pod's coprocessor AI, if any.</summary>
    public static bool InCyberpod(Pawn hacker, out NetAI ai)
    {
        ai = null;
        if (!hacker.TryGetHoldingCyberpod(out Building_Cyberpod pod))
            return false;

        ai = CoprocessorAI(pod);
        return true;
    }

    public static float HeatFactor(NetAI ai) => ai == null ? 1f
        : ai.Personality == AIPersonality.Ghost ? Settings.coprocessorGhostHeatFactor
        : Settings.coprocessorHeatFactor;

    public static float BonusDataChance(NetAI ai) => ai == null ? 0f
        : ai.Personality == AIPersonality.DataHunter ? Settings.coprocessorDataHunterBonusDataChance
        : Settings.coprocessorBonusDataChance;

    public static float NetsickFactor(NetAI ai) => ai == null ? 1f
        : ai.Personality is AIPersonality.Doc or AIPersonality.Warden ? Settings.coprocessorDocNetsickFactor
        : Settings.coprocessorNetsickFactor;
}
