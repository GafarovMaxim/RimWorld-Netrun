using HarmonyLib;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// What Netrun backstories add beyond skills (docs/CONCEPTS_10.md, section 5): a chance at "natural netrunner",
/// Netsick for the runaway braindance tester, and fixers who remember a former fixer.
/// </summary>
public static class NetrunBackstories
{
    public static bool Has(Pawn pawn, string defName)
        => pawn?.story != null && (pawn.story.Childhood?.defName == defName || pawn.story.Adulthood?.defName == defName);

    /// <summary>Reputation a new fixer starts with: a former fixer in the colony opens doors.</summary>
    public static float FixerStartReputation()
    {
        foreach (Pawn p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
            if (Has(p, "NR_Bs_FormerFixer"))
                return NetrunDefOf.NR_Cyberspace.pawnNet.formerFixerReputation;
        return 0f;
    }
}

[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), typeof(PawnGenerationRequest))]
public static class Patch_PawnGenerator_Backstories
{
    public static void Postfix(Pawn __result)
    {
        try
        {
            Pawn pawn = __result;
            if (pawn?.story == null || !pawn.RaceProps.Humanlike)
                return;
            PawnNetSettings s = NetrunDefOf.NR_Cyberspace.pawnNet;

            if ((NetrunBackstories.Has(pawn, "NR_Bs_BraindanceSubject") || NetrunBackstories.Has(pawn, "NR_Bs_StreetNetrunner"))
                && DefDatabase<TraitDef>.GetNamedSilentFail("NR_NaturalNetrunner") is { } natural
                && !pawn.story.traits.HasTrait(natural) && Rand.Chance(s.naturalNetrunnerBackstoryChance))
                pawn.story.traits.GainTrait(new Trait(natural));

            if (NetrunBackstories.Has(pawn, "NR_Bs_BraindanceTester") && pawn.health != null
                && pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick) == null)
            {
                var netsick = (Hediff_Netsick)HediffMaker.MakeHediff(NetrunDefOf.NR_Netsick, pawn);
                netsick.Severity = s.burnedTesterNetsick;
                netsick.LastConnectedTick = Find.TickManager?.TicksGame ?? 0;
                pawn.health.AddHediff(netsick);
            }
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] backstory extras: " + e.Message, 0x4E520032);
        }
    }
}
