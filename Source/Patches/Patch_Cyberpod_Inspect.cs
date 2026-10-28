using HarmonyLib;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>The pod's inspect panel tells how long its netrunner can stay before Netsick turns strong.</summary>
[HarmonyPatch(typeof(Building_Cyberpod), nameof(Building_Cyberpod.GetInspectString))]
public static class Patch_Cyberpod_Inspect
{
    public static void Postfix(Thing __instance, ref string __result)
    {
        try
        {
            if (__instance is not Building_Cyberpod pod || pod.Faction != Faction.OfPlayer || pod.ContainedThing is not Pawn pawn || pawn.Dead)
                return;
            float mass = CyberpodUtility.ApparelMass(pawn);
            CyberpodUtility.LightGear(mass, out float hack, out float cut);
            string line = NetsickManager.SafeTimeText(pawn, pod) + "\n"
                + (hack > 0f ? "NR_PodGearLight".Translate(mass.ToString("0.0"), hack.ToStringPercent("0"), cut.ToStringPercent("0"))
                             : "NR_PodGearHeavy".Translate(mass.ToString("0.0")));
            __result = __result.NullOrEmpty() ? line : __result.TrimEnd() + "\n" + line;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] pod inspect: " + e.Message, 0x4E52_5049);
        }
    }
}
