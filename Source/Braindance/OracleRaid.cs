using HarmonyLib;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// The Oracle's vision (a unique recording): the next raid on a player home is foreseen. It does not hit now; it is put
/// back by a day and a letter tells the colony when and where it comes from. Only the first raid after watching.
/// </summary>
[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
public static class Patch_IncidentWorker_OracleRaid
{
    static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        try
        {
            UniqueBraindanceManager manager = UniqueBraindanceManager.Get();
            if (manager is not { OracleWatching: true } || __instance is not IncidentWorker_RaidEnemy || parms?.target is not Map { IsPlayerHome: true } map)
                return true;
            manager.OracleWatching = false;
            int delay = (int)(NetrunDefOf.NR_UniqueBraindance.oracleWarningDays * GenDate.TicksPerDay);
            Find.Storyteller.incidentQueue.Add(new QueuedIncident(new FiringIncident(__instance.def, null, parms), Find.TickManager.TicksGame + delay));
            string who = parms.faction?.Name ?? (string)"NR_UB_OracleUnknown".Translate();
            Find.LetterStack.ReceiveLetter("NR_UB_OracleLabel".Translate(), "NR_UB_OracleText".Translate(who, delay.ToStringTicksToPeriod()),
                LetterDefOf.ThreatSmall, new LookTargets(map.Center, map));
            __result = true;
            return false;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Patch_IncidentWorker_OracleRaid: " + e.Message, 0x4E525406);
            return true;
        }
    }
}
