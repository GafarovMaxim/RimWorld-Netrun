using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// What a dive leaves on the netrunner beyond Netsick and burns (docs/CONCEPTS_7.md, part 4): a flow after a clean run,
/// a skill remembered from the net, the rush of surviving ICE; a migraine after a long dive, phantom signals at a
/// strong dependency, a burned link after Black ICE. Each lands in the dive report.
/// </summary>
public static class DiveEffects
{
    private static CyberspaceSettingsDef Settings => NetrunDefOf.NR_Cyberspace;

    public static void Apply(DiveSession dive, bool success, DiveReport report)
    {
        Pawn pawn = dive.Runner;
        if (pawn == null || pawn.Dead)
            return;

        if (success && dive.IceHits == 0 && Give(pawn, "NR_DiveFlow"))
            report.Events.Add("Dive_Lightning_Yellow|" + "NR_DiveEffectFlow".Translate(pawn.LabelShort));

        if (success && Rand.Chance(Settings.effectMemoryChance)
            && pawn.skills?.skills.Where(s => !s.TotallyDisabled).RandomElementWithFallback() is { } skill)
        {
            skill.Learn(Settings.effectMemoryXp, direct: true);
            report.Events.Add("Icon_Xp|" + "NR_DiveEffectMemory".Translate(pawn.LabelShort, skill.def.label, Settings.effectMemoryXp.ToString("0")));
        }

        if (success && dive.IceHits > 0 && DefDatabase<ThoughtDef>.GetNamedSilentFail("NR_NetAdrenaline") is { } rush)
        {
            pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(rush);
            report.Events.Add("Dive_ICE|" + "NR_DiveEffectAdrenaline".Translate(pawn.LabelShort));
        }

        if (dive.Turn >= Settings.effectMigraineTurns && Give(pawn, "NR_DiveMigraine"))
            report.Events.Add("Event_Cyberspace_NeuralNoise|" + "NR_DiveEffectMigraine".Translate(pawn.LabelShort));

        if (DiveReport.NetsickOf(pawn) >= Settings.effectPhantomNetsick && Rand.Chance(Settings.effectPhantomChance) && Give(pawn, "NR_PhantomSignals"))
            report.Events.Add("Dive_BLACKICE|" + "NR_DiveEffectPhantom".Translate(pawn.LabelShort));

        if (!success && dive.Node.BlackIce && dive.IceHits > 0 && Rand.Chance(Settings.effectBurnedLinkChance) && Give(pawn, "NR_BurnedLink"))
            report.Events.Add("Dive_Lightning_Red|" + "NR_DiveEffectBurned".Translate(pawn.LabelShort));
    }

    /// <summary>Hack chance from the runner's state: flow helps, a burned link hurts.</summary>
    public static float HackBonus(Pawn pawn)
    {
        if (pawn?.health?.hediffSet == null)
            return 0f;
        float bonus = 0f;
        if (Has(pawn, "NR_DiveFlow"))
            bonus += Settings.effectFlowHackBonus;
        if (Has(pawn, "NR_BurnedLink"))
            bonus -= Settings.effectBurnedHackPenalty;
        return bonus;
    }

    private static bool Has(Pawn pawn, string defName)
        => DefDatabase<HediffDef>.GetNamedSilentFail(defName) is { } def && pawn.health.hediffSet.HasHediff(def);

    /// <summary>Adds the hediff (refreshing it if already there); false when the def is missing.</summary>
    private static bool Give(Pawn pawn, string defName)
    {
        if (DefDatabase<HediffDef>.GetNamedSilentFail(defName) is not { } def)
            return false;
        if (pawn.health.hediffSet.GetFirstHediffOfDef(def) is { } old)
            pawn.health.RemoveHediff(old);
        pawn.health.AddHediff(def);
        return true;
    }
}
