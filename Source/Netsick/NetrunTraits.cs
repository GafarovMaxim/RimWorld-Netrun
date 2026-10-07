using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// Pawn traits in the net (docs/CONCEPTS_7.md, part 2): a neurotic mind gets hooked faster, a hard worker shakes it
/// off sooner offline (a lazy one slower), and a natural netrunner barely gets hooked and hacks better.
/// </summary>
public static class NetrunTraits
{
    private static TraitDef Natural => DefDatabase<TraitDef>.GetNamedSilentFail("NR_NaturalNetrunner");

    private static int Degree(Pawn pawn, string defName)
        => pawn?.story?.traits != null && DefDatabase<TraitDef>.GetNamedSilentFail(defName) is { } def && pawn.story.traits.HasTrait(def)
            ? pawn.story.traits.DegreeOfTrait(def)
            : 0;

    /// <summary>How fast Netsick grows for this pawn.</summary>
    public static float GainFactor(Pawn pawn)
    {
        float factor = 1f;
        if (Natural is { } natural && pawn?.story?.traits?.HasTrait(natural) == true)
            factor *= 0.6f;
        int neurotic = Degree(pawn, "Neurotic");
        if (neurotic > 0)
            factor *= 1f + 0.15f * neurotic;
        // Biotech gene "neural resilience" (docs/CONCEPTS_10.md, section 9).
        if (ModsConfig.BiotechActive && pawn?.genes != null && DefDatabase<GeneDef>.GetNamedSilentFail("NR_Gene_NeuralResilience") is { } gene
            && pawn.genes.HasActiveGene(gene))
            factor *= 0.6f;
        return factor;
    }

    /// <summary>How fast Netsick fades offline for this pawn.</summary>
    public static float DecayFactor(Pawn pawn)
    {
        int industrious = Degree(pawn, "Industriousness");
        return (1f + 0.15f * industrious) * DiveTraits.NetsickDecayFactor(pawn);
    }

    /// <summary>Hack chance in dives.</summary>
    public static float HackBonus(Pawn pawn)
        => (Natural is { } natural && pawn?.story?.traits?.HasTrait(natural) == true ? 0.05f : 0f) + DiveTraits.HackBonus(pawn);
}
