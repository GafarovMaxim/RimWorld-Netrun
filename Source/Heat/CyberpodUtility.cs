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

    /// <summary>True when the pawn lies in a cyberpod that has power: it keeps the climate off the pawn.</summary>
    public static bool InPoweredPod(Pawn pawn)
        => pawn != null && pawn.TryGetHoldingCyberpod(out Building_Cyberpod pod) && pod.GetComp<CompPowerTrader>()?.PowerOn == true;

    /// <summary>Total mass (kg) of the apparel the pawn wears.</summary>
    public static float ApparelMass(Pawn pawn)
    {
        float mass = 0f;
        List<Apparel> worn = pawn?.apparel?.WornApparel;
        if (worn != null)
            for (int i = 0; i < worn.Count; i++)
                mass += worn[i].GetStatValue(StatDefOf.Mass);
        return mass;
    }

    /// <summary>Light clothing by worn mass: the hacking bonus and the Netsick cut (0 when the gear is heavier).</summary>
    public static void LightGear(float mass, out float hackBonus, out float netsickCut)
    {
        CyberspaceSettingsDef s = NetrunDefOf.NR_Cyberspace;
        if (mass <= s.lightGearMass0)
        {
            hackBonus = s.lightGearHack0;
            netsickCut = s.lightGearNetsickCut0;
        }
        else if (mass <= s.lightGearMass1)
        {
            hackBonus = s.lightGearHack1;
            netsickCut = s.lightGearNetsickCut1;
        }
        else
            hackBonus = netsickCut = 0f;
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
