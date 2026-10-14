using HarmonyLib;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// The cyberpod and the AI server can be packed up (scenarios hand them over as items), but not while in use:
/// a pod with someone inside or a server still hosting AIs stays where it is.
/// </summary>
[HarmonyPatch(typeof(Designator_Uninstall), nameof(Designator_Uninstall.CanDesignateThing))]
public static class Patch_Uninstall_Busy
{
    static void Postfix(Thing t, ref AcceptanceReport __result)
    {
        if (!__result.Accepted)
            return;
        try
        {
            if (t is Building_Cyberpod pod && pod.ContainedThing != null)
                __result = "NR_UninstallBusyPod".Translate();
            else if (t.TryGetComp<CompAIServer>() is { Used: > 0 })
                __result = "NR_UninstallBusyServer".Translate();
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] uninstall check failed: " + e.Message, 0x4E520071);
        }
    }
}
