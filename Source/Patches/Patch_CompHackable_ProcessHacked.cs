using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// Vanilla hook that fires once when a hack completes, for pawns and for Hacking Expansion cyberpods alike
/// (inside a pod, <paramref name="hacker"/> is the pawn in the pod).
/// </summary>
[HarmonyPatch(typeof(CompHackable), "ProcessHacked")]
public static class Patch_CompHackable_ProcessHacked
{
    static void Postfix(CompHackable __instance, Pawn hacker)
    {
        try
        {
            if (hacker == null || hacker.Faction != Faction.OfPlayer)
                return;

            HeatTracker.Get()?.AddHeat(HackHeatUtility.HeatForHack(__instance, hacker));
        }
        catch (Exception ex)
        {
            Log.ErrorOnce("[Netrun] Failed to register hack heat: " + ex, 0x4E520001);
        }
    }
}
