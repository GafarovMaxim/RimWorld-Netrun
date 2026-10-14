using HarmonyLib;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// Hacking Expansion's black box keeps its owner and compromised factions only in memory: after a load they are null,
/// and hacking the box throws in OnHacked (goodwill on a null faction), leaving the hacker stuck in the job.
/// We save both factions under our own keys and, for boxes from older saves, pick plausible ones before the hack resolves.
/// </summary>
public static class Patch_BlackBox_Factions
{
    private static readonly AccessTools.FieldRef<CompBlackBox, Faction> Owner =
        AccessTools.FieldRefAccess<CompBlackBox, Faction>("_ownerFaction");
    private static readonly AccessTools.FieldRef<CompBlackBox, Faction> Compromised =
        AccessTools.FieldRefAccess<CompBlackBox, Faction>("_compromisedFaction");

    [HarmonyPatch(typeof(CompBlackBox), nameof(CompBlackBox.PostExposeData))]
    public static class Save
    {
        public static void Postfix(CompBlackBox __instance)
        {
            try
            {
                Scribe_References.Look(ref Owner(__instance), "nr_ownerFaction");
                Scribe_References.Look(ref Compromised(__instance), "nr_compromisedFaction");
            }
            catch (System.Exception e)
            {
                Log.ErrorOnce("[Netrun] Black box factions: " + e.Message, 0x4E52_4242);
            }
        }
    }

    [HarmonyPatch(typeof(CompBlackBox), "OnHacked")]
    public static class Hacked
    {
        public static void Prefix(CompBlackBox __instance)
        {
            try
            {
                FactionManager fm = Find.FactionManager;
                ref Faction compromised = ref Compromised(__instance);
                ref Faction owner = ref Owner(__instance);
                if (compromised == null || compromised.defeated)
                    compromised = fm.RandomNonHostileFaction(allowNonHumanlike: false) ?? Faction.OfAncients;
                if (owner == null || owner.defeated)
                    owner = fm.RandomEnemyFaction(allowNonHumanlike: false) ?? Faction.OfAncientsHostile;
            }
            catch (System.Exception e)
            {
                Log.ErrorOnce("[Netrun] Black box factions: " + e.Message, 0x4E52_4243);
            }
        }
    }
}
