using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// A dying host drops its AI passenger as a fragment; a colonist dying with a cyberlink
/// may leave an Echo behind.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
public static class Patch_Pawn_Kill
{
    static void Prefix(Pawn __instance, out bool __state)
    {
        try
        {
            // Read before death: hediffs and faction can change while dying.
            // A body released by the Transition leaves no Echo: the mind went into the net whole.
            __state = !TransitionManager.IsReleasing(__instance) && __instance.Faction == Faction.OfPlayer && __instance.RaceProps.Humanlike && __instance.IsHacker();
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Patch_Pawn_Kill: " + e.Message, 0x4E525409);
            __state = false;
        }
    }

    static void Postfix(Pawn __instance, bool __state, DamageInfo? dinfo)
    {
        try
        {
            AIManager manager = AIManager.Get();
            if (manager == null || !__instance.Dead)
                return;

            NetAI passenger = manager.AIs.FirstOrDefault(a => a.Host == __instance);
            if (passenger != null)
                manager.EjectFromHost(passenger, "NR_AIHostDied");

            if (__state && Rand.Chance(NetrunDefOf.NR_AI.echoChance))
                manager.CreateEcho(__instance, dinfo?.Instigator as Pawn);
        }
        catch (Exception ex)
        {
            Log.ErrorOnce("[Netrun] Failed to handle a dying host: " + ex, 0x4E520004);
        }
    }
}
