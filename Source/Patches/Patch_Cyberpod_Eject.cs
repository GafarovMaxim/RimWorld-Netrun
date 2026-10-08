using HarmonyLib;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// A pod opened while its pawn is still out in the net (a runner on the map or inside a dive) tears the pawn out:
/// the emergency jolt (Netsick, nausea, maybe a lockout), the runner leaves the map and the dive is lost.
/// </summary>
[HarmonyPatch(typeof(Building_Cyberpod), nameof(Building_Cyberpod.EjectContents))]
public static class Patch_Cyberpod_Eject
{
    static void Prefix(Building_Cyberpod __instance)
    {
        try
        {
            if (__instance.ContainedThing is not Pawn pawn || pawn.Faction != Faction.OfPlayer || !NetMapManager.InRun(pawn))
                return;
            NetMapManager.EmergencyJolt(pawn);
            NetMapManager net = NetMapManager.Get();
            if (net?.RunnerOf(pawn) is { } runner)
                net.Runners.Remove(runner);
            Find.LetterStack.ReceiveLetter("NR_PulledOutLabel".Translate(pawn.LabelShort), "NR_PulledOut".Translate(pawn.LabelShort),
                LetterDefOf.NegativeEvent, pawn);
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] pulled-out check failed: " + e.Message, 0x4E520051);
        }
    }
}
