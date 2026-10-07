using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// A hackable target shows the trace a hack would leave, before anyone starts it. Hooks the hackable comp's own
/// inspect text when it has one, otherwise the thing's inspect string.
/// </summary>
[HarmonyPatch]
public static class Patch_CompHackable_Inspect
{
    private static MethodBase Own => AccessTools.DeclaredMethod(typeof(CompHackable), nameof(ThingComp.CompInspectStringExtra));

    static MethodBase TargetMethod() => Own ?? AccessTools.DeclaredMethod(typeof(ThingWithComps), nameof(ThingWithComps.GetInspectString));

    static void Postfix(object __instance, ref string __result)
    {
        try
        {
            CompHackable comp = __instance as CompHackable ?? (__instance as ThingWithComps)?.GetComp<CompHackable>();
            if (comp == null || comp.IsHacked || HeatTracker.Get() == null)
                return;
            float heat = HackHeatUtility.BaseHeat(comp) * NetrunMod.Settings.HeatGainMultiplier;
            string line = "NR_HackHeatPreview".Translate(Mathf.CeilToInt(heat));
            __result = __result.NullOrEmpty() ? line : __result + "\n" + line;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Hack Heat preview failed: " + e.Message, 0x4E52_4850);
        }
    }
}
