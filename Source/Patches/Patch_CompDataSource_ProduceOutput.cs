using System;
using HarmonyLib;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>Every item ripped with Hacking Expansion's data ripping leaves extra trace.</summary>
[HarmonyPatch(typeof(CompDataSource), "ProduceOutput")]
public static class Patch_CompDataSource_ProduceOutput
{
    static void Postfix(CompDataSource __instance, ThingDef thingDef, Pawn hacker)
    {
        try
        {
            if (hacker == null || hacker.Faction != Faction.OfPlayer)
                return;

            string reason = "NR_LogRip".Translate(hacker.LabelShort, thingDef.label, __instance.parent.LabelCap);
            HeatTracker.Get()?.AddHeat(NetrunDefOf.NR_Heat.heatPerRippedItem, reason, HeatSource.Hack);
        }
        catch (Exception ex)
        {
            Log.ErrorOnce("[Netrun] Failed to register ripping heat: " + ex, 0x4E520003);
        }
    }
}
