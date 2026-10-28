using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// A pawn in a powered cyberpod gets no hypothermia and no heatstroke: the vanilla givers skip the pawn
/// (the hediff already on it fades as it does in comfort). An unpowered pod protects nothing.
/// </summary>
[HarmonyPatch]
public static class Patch_Pod_Climate
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(HediffGiver_Hypothermia), nameof(HediffGiver_Hypothermia.OnIntervalPassed));
        yield return AccessTools.Method(typeof(HediffGiver_Heat), nameof(HediffGiver_Heat.OnIntervalPassed));
    }

    static bool Prefix(HediffGiver __instance, Pawn pawn)
    {
        try
        {
            if (!CyberpodUtility.InPoweredPod(pawn))
                return true;
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(__instance.hediff);
            if (existing != null)
                existing.Severity -= Mathf.Clamp(existing.Severity * 0.027f, 0.0015f, 0.015f);
            return false;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] pod climate: " + e.Message, 0x4E52_5043);
            return true;
        }
    }
}
