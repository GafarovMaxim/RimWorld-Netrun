using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// The neurostabilizer eases Netsick at once (scaled by tolerance like any drug effect) and postpones the withdrawal
/// by <see cref="postponeHours"/> (a dose sets the end to max(current, now + hours), never adds). Doses in a row are
/// counted by a short-lived hediff (24 h after the last dose, severity = count): <see cref="streakDoses"/> in a row
/// give a resistance hediff, and while it lasts a dose does not postpone anything (its other effects stay).
/// </summary>
public class IngestionOutcomeDoer_ReduceNetsick : IngestionOutcomeDoer
{
    public float amount = 0.15f;
    public float postponeHours = 36f;
    public int streakDoses = 3;
    public HediffDef streakHediff;
    public HediffDef resistanceHediff;

    protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
    {
        bool resistant = resistanceHediff != null && pawn.health.hediffSet.HasHediff(resistanceHediff);
        if (!resistant)
            CountDose(pawn);
        if (pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick) is not { } netsick)
            return;
        float effect = amount;
        // Tolerance dulls it, like any drug.
        if (ingested.def.GetCompProperties<CompProperties_Drug>()?.chemical?.toleranceHediff is { } toleranceDef
            && pawn.health.hediffSet.GetFirstHediffOfDef(toleranceDef) is { } tolerance)
            effect *= UnityEngine.Mathf.Clamp01(1f - tolerance.Severity);
        netsick.Severity -= effect;
        if (resistant)
            return;
        // Shift the "last online" mark: the withdrawal clock runs out no sooner than postponeHours from now (a max, so doses never add up).
        int earliest = Find.TickManager.TicksGame
            + (int)(postponeHours / 24f * GenDate.TicksPerDay - NetrunDefOf.NR_NetsickSettings.withdrawalAfterDays * GenDate.TicksPerDay);
        if (netsick is Hediff_Netsick ns && ns.LastConnectedTick < earliest)
            ns.LastConnectedTick = earliest;
    }

    /// <summary>Counts this dose in the streak; the third one in a row starts the resistance and resets the count.</summary>
    private void CountDose(Pawn pawn)
    {
        if (streakHediff == null || resistanceHediff == null)
            return;
        Hediff streak = pawn.health.hediffSet.GetFirstHediffOfDef(streakHediff);
        if (streak == null)
        {
            streak = HediffMaker.MakeHediff(streakHediff, pawn);
            streak.Severity = 1f;
            pawn.health.AddHediff(streak);
        }
        else
        {
            streak.Severity += 1f;
            if (streak.TryGetComp<HediffComp_Disappears>() is { } timer)
                timer.ticksToDisappear = timer.Props.disappearsAfterTicks.max;
        }
        if (streak.Severity >= streakDoses)
        {
            pawn.health.RemoveHediff(streak);
            pawn.health.AddHediff(resistanceHediff);
        }
    }
}

public static class NeurostabilizerUtility
{
    /// <summary>While stabilized, Netsick neither hurts as withdrawal nor sends the pawn to a pod. Resistance cancels it.</summary>
    public static bool Stabilized(Pawn pawn)
    {
        var set = pawn?.health?.hediffSet;
        return set != null && NetrunDefOf.NR_NeurostabilizerHigh != null && set.HasHediff(NetrunDefOf.NR_NeurostabilizerHigh)
            && (NetrunDefOf.NR_StabilizerResistance == null || !set.HasHediff(NetrunDefOf.NR_StabilizerResistance));
    }
}
