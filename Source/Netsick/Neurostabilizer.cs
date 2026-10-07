using RimWorld;
using Verse;

namespace Netrun;

/// <summary>The neurostabilizer eases Netsick at once (scaled by tolerance like any drug effect).</summary>
public class IngestionOutcomeDoer_ReduceNetsick : IngestionOutcomeDoer
{
    public float amount = 0.15f;

    protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
    {
        if (pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick) is not { } netsick)
            return;
        float effect = amount;
        // Tolerance dulls it, like any drug.
        if (ingested.def.GetCompProperties<CompProperties_Drug>()?.chemical?.toleranceHediff is { } toleranceDef
            && pawn.health.hediffSet.GetFirstHediffOfDef(toleranceDef) is { } tolerance)
            effect *= UnityEngine.Mathf.Clamp01(1f - tolerance.Severity);
        netsick.Severity -= effect;
    }
}

public static class NeurostabilizerUtility
{
    /// <summary>While stabilized, Netsick neither hurts as withdrawal nor sends the pawn to a pod.</summary>
    public static bool Stabilized(Pawn pawn)
        => NetrunDefOf.NR_NeurostabilizerHigh != null && pawn?.health?.hediffSet?.HasHediff(NetrunDefOf.NR_NeurostabilizerHigh) == true;
}
