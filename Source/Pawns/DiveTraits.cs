using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// Permanent dive traits earned from pawn records (docs/CONCEPTS_10.md, section 14). Checked when a dive ends.
/// One per pawn, except "burnt", which can sit next to another.
/// </summary>
public static class DiveTraits
{
    public static TraitDef Veteran => DefDatabase<TraitDef>.GetNamedSilentFail("NR_NetVeteran");
    public static TraitDef GhostHand => DefDatabase<TraitDef>.GetNamedSilentFail("NR_GhostHand");
    public static TraitDef Burnt => DefDatabase<TraitDef>.GetNamedSilentFail("NR_Burnt");
    public static TraitDef Alien => DefDatabase<TraitDef>.GetNamedSilentFail("NR_AlienInFlesh");

    private static PawnNetSettings S => NetrunDefOf.NR_Cyberspace.pawnNet;

    public static bool Has(Pawn pawn, TraitDef def) => def != null && pawn?.story?.traits?.HasTrait(def) == true;

    private static bool HasMainDiveTrait(Pawn pawn) => Has(pawn, Veteran) || Has(pawn, GhostHand) || Has(pawn, Alien);

    private static int Record(Pawn pawn, RecordDef def) => def == null || pawn?.records == null ? 0 : pawn.records.GetAsInt(def);

    public static void Check(Pawn pawn)
    {
        if (pawn?.story?.traits == null || pawn.Dead)
            return;

        if (!Has(pawn, Burnt) && Record(pawn, NetrunRecords.NR_Record_NetComas) + Record(pawn, NetrunRecords.NR_Record_LinkBurns) >= S.burntCount)
            Give(pawn, Burnt);

        if (HasMainDiveTrait(pawn))
            return;
        // The hardest first, so a pawn that qualifies for several gets the rarest.
        if (Record(pawn, NetrunRecords.NR_Record_Dives) >= S.alienDives)
            Give(pawn, Alien);
        else if ((PawnNetState.Get()?.CleanStreak(pawn) ?? 0) >= S.ghostHandStreak)
            Give(pawn, GhostHand);
        else if (Record(pawn, NetrunRecords.NR_Record_CleanExits) >= S.veteranCleanExits)
            Give(pawn, Veteran);
    }

    private static void Give(Pawn pawn, TraitDef def)
    {
        if (def == null)
            return;
        pawn.story.traits.GainTrait(new Trait(def, 0, forced: true));
        Find.LetterStack.ReceiveLetter("NR_DiveTraitLabel".Translate(pawn.LabelShort, def.degreeDatas[0].label),
            "NR_DiveTrait".Translate(pawn.LabelShort, def.degreeDatas[0].label, def.DataAtDegree(0).description.Formatted(pawn.Named("PAWN")).Resolve()),
            LetterDefOf.PositiveEvent, pawn);
    }

    // ---- Effects ----

    public static float HackBonus(Pawn pawn) => Has(pawn, Veteran) ? S.veteranHackBonus : 0f;

    public static float NetsickDecayFactor(Pawn pawn) => Has(pawn, Veteran) ? S.veteranDecayFactor : 1f;

    public static float TraceFactor(Pawn pawn) => Has(pawn, GhostHand) ? S.ghostHandTraceFactor : 1f;

    /// <summary>Burnt: one action less in the first turn.</summary>
    public static int FirstTurnActions(Pawn pawn) => Has(pawn, Burnt) ? -1 : 0;

    public static bool NauseaImmune(Pawn pawn) => Has(pawn, Burnt);

    public static bool InPod(Pawn pawn) => pawn != null && pawn.TryGetHoldingCyberpod(out _);
}

public class ThoughtWorker_AlienInPod : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p) => DiveTraits.InPod(p);
}

public class ThoughtWorker_AlienOffline : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p)
    {
        if (DiveTraits.InPod(p))
            return false;
        int last = PawnNetState.Get()?.LastDiveTick(p) ?? -1;
        int days = NetrunDefOf.NR_Cyberspace.pawnNet.alienOfflineDays;
        return last < 0 || Find.TickManager.TicksGame - last > days * GenDate.TicksPerDay;
    }
}

/// <summary>A netrunner who has been past Black ICE tells about it now and then.</summary>
public class InteractionWorker_DiveStory : InteractionWorker
{
    public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
    {
        if (initiator?.records == null || NetrunRecords.NR_Record_BlackICEPassed == null)
            return 0f;
        return initiator.records.GetAsInt(NetrunRecords.NR_Record_BlackICEPassed) > 0 ? NetrunDefOf.NR_Cyberspace.pawnNet.storyWeight : 0f;
    }
}
