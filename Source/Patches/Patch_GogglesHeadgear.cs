using HarmonyLib;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// Braindance goggles go over the eyes, so they fit under open helmets and hats (simple, advanced, caps) but not under
/// headgear that closes the whole head (marine, cataphract, phoenix, recon, gas masks: bodyPartGroups FullHead).
/// Two goggles-like eye covers still exclude each other. Called often by outfit logic: the def checks come first.
/// </summary>
[HarmonyPatch(typeof(ApparelUtility), nameof(ApparelUtility.CanWearTogether))]
public static class Patch_GogglesHeadgear
{
    public static void Postfix(ThingDef A, ThingDef B, ref bool __result)
    {
        ThingDef goggles = NetrunDefOf.NR_BraindanceGoggles;
        if (goggles == null || A != goggles && B != goggles || A == B)
            return;
        try
        {
            ThingDef other = A == goggles ? B : A;
            if (other?.apparel == null)
                return;
            if (other.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.FullHead))
                __result = false;
            // Some mods put open helmets on the EyeCover layer too: only another item over the eyes really clashes.
            else if (!other.apparel.layers.Contains(ApparelLayerDefOf.EyeCover)
                     || !other.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Eyes))
                __result = true;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Patch_GogglesHeadgear: " + e.Message, 0x4E52_4748);
        }
    }
}
