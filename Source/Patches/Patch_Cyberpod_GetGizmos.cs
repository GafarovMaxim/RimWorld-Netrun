using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>Adds the network terminal button to an occupied Hacking Expansion cyberpod.</summary>
[HarmonyPatch(typeof(Building_Cyberpod), nameof(Building_Cyberpod.GetGizmos))]
public static class Patch_Cyberpod_GetGizmos
{
    static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Building_Cyberpod __instance)
    {
        foreach (Gizmo gizmo in gizmos)
            yield return gizmo;

        Gizmo terminal = null;
        try
        {
            if (__instance.Faction == Faction.OfPlayer && __instance.ContainedThing is Pawn)
            {
                CompPowerTrader power = __instance.GetComp<CompPowerTrader>();
                terminal = NetTerminalUtility.MakeGizmo(__instance.Map, power == null || power.PowerOn);
            }
        }
        catch (Exception ex)
        {
            Log.ErrorOnce("[Netrun] Failed to add the terminal button to the cyberpod: " + ex, 0x4E520002);
        }

        if (terminal != null)
            yield return terminal;
    }
}
