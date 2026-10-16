using HarmonyLib;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// A cyberpod that loses power throws out a pawn who is busy in the net: on a run, in a dive, or hacking a target through
/// HE (Maxim, 2026-10-17). No death: the emergency jolt (Netsick, nausea, a lockout chance) and a chance of a net coma.
/// A pawn only resting in the pod stays, and the Transition has its own reserve (TransitionManager.TickPower).
/// Checked every 250 ticks from TransitionManager.
/// </summary>
public static class CyberpodPowerCut
{
    public const float ComaChance = 0.15f;

    private static readonly AccessTools.FieldRef<Building_Cyberpod, CompHackable> CurrentlyHacking =
        AccessTools.FieldRefAccess<Building_Cyberpod, CompHackable>("_currentlyHacking");

    public static void Check()
    {
        TransitionManager transition = TransitionManager.Get();
        foreach (Map map in Find.Maps)
        {
            foreach (Building_Cyberpod pod in map.listerBuildings.AllBuildingsColonistOfClass<Building_Cyberpod>())
            {
                if (pod.ContainedThing is not Pawn pawn || pod.GetComp<CompPowerTrader>()?.PowerOn != false)
                    continue;
                if (transition != null && transition.Uploading(pawn))
                    continue;
                bool inRun = NetMapManager.InRun(pawn);
                if (!inRun && !Hacking(pod))
                    continue;
                Throw(pod, pawn, inRun);
                // The lister may change under the loop: the next pod waits for the next check.
                return;
            }
        }
    }

    private static void Throw(Building_Cyberpod pod, Pawn pawn, bool inRun)
    {
        try
        {
            if (GatewayManager.Get()?.Session is { Finished: false } session && session.Runner == pawn)
                session.PowerCut();
            // In a run the eject patch applies the jolt and sends the "pulled out" letter; a hacker gets the jolt here.
            pod.EjectContents();
            if (!inRun)
                NetMapManager.EmergencyJolt(pawn);
            if (!pawn.Dead && Rand.Chance(ComaChance) && DefDatabase<HediffDef>.GetNamedSilentFail("USH_CyberspaceComa") is { } coma)
                pawn.health.AddHediff(coma, pawn.health.hediffSet.GetBrain());
            Messages.Message("NR_PodPowerEject".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NegativeEvent);
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] power-cut eject failed: " + e.Message, 0x4E520052);
        }
    }

    private static bool Hacking(Building_Cyberpod pod)
    {
        try
        {
            return CurrentlyHacking(pod) != null;
        }
        catch
        {
            return false;
        }
    }
}
