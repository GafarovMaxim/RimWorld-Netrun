using HarmonyLib;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// Heat makes vanilla enemy raids on a player home bigger (x1.1 Tracked, x1.2 Target, x1.25 Red Zone; T1.3). A prefix on the
/// execute method, not on the storyteller's threat points: those feed every threat and quest. Our own mercenary raid (a Heat
/// consequence already) and raids on other maps are left alone.
/// </summary>
[HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryExecuteWorker")]
public static class Patch_RaidEnemy_HeatPoints
{
    static void Prefix(IncidentWorker_RaidEnemy __instance, IncidentParms parms)
    {
        try
        {
            if (parms == null || parms.target is not Map { IsPlayerHome: true } || __instance is IncidentWorker_MercRaid
                || parms.faction?.def == NetrunDefOf.NR_Mercenaries)
                return;
            float factor = HeatTracker.Get()?.RaidPointsFactor ?? 1f;
            if (factor > 1f)
                parms.points *= factor;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Patch_RaidEnemy_HeatPoints: " + e.Message, 0x4E525413);
        }
    }
}
