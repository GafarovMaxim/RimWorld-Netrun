using HarmonyLib;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// The "pod needs fuel" setting off: a cyberpod always counts as fuelled, burns nothing, asks for no refuelling and hides
/// its fuel readout. Other refuelables are untouched.
/// </summary>
public static class PodFuelOff
{
    public static bool Applies(CompRefuelable comp) => !NetrunMod.Settings.PodFuel && comp?.parent is Building_Cyberpod;
}

[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.HasFuel), MethodType.Getter)]
public static class Patch_PodFuel_HasFuel
{
    public static void Postfix(CompRefuelable __instance, ref bool __result)
    {
        if (!__result && PodFuelOff.Applies(__instance))
            __result = true;
    }
}

[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.ConsumeFuel))]
public static class Patch_PodFuel_Consume
{
    public static bool Prefix(CompRefuelable __instance) => !PodFuelOff.Applies(__instance);
}

[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.ShouldAutoRefuelNow), MethodType.Getter)]
public static class Patch_PodFuel_AutoRefuel
{
    public static void Postfix(CompRefuelable __instance, ref bool __result)
    {
        if (__result && PodFuelOff.Applies(__instance))
            __result = false;
    }
}

[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.ShouldAutoRefuelNowIgnoringFuelPct), MethodType.Getter)]
public static class Patch_PodFuel_AutoRefuelIgnoringPct
{
    public static void Postfix(CompRefuelable __instance, ref bool __result)
    {
        if (__result && PodFuelOff.Applies(__instance))
            __result = false;
    }
}

[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.CompInspectStringExtra))]
public static class Patch_PodFuel_Inspect
{
    public static void Postfix(CompRefuelable __instance, ref string __result)
    {
        if (PodFuelOff.Applies(__instance))
            __result = null;
    }
}
