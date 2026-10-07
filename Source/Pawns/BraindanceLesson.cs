using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Netrun;

/// <summary>
/// Biotech: a child watches an ordinary braindance chip from storage as a lesson, like reading a book
/// (docs/CONCEPTS_10.md, section 9). The chip is not used up.
/// </summary>
public class LearningGiver_Braindance : LearningGiver
{
    private static ThingDef Chip => DefDatabase<ThingDef>.GetNamedSilentFail("NR_BraindanceChip");

    private static Thing FindChip(Pawn pawn)
    {
        if (Chip == null || pawn.Map == null)
            return null;
        return GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, pawn.Map.listerThings.ThingsOfDef(Chip),
            PathEndMode.Touch, TraverseParms.For(pawn), 9999f,
            t => !t.IsForbidden(pawn) && pawn.CanReserve(t));
    }

    public override bool CanDo(Pawn pawn) => base.CanDo(pawn) && FindChip(pawn) != null;

    public override Job TryGiveJob(Pawn pawn)
    {
        Thing chip = FindChip(pawn);
        return chip == null ? null : JobMaker.MakeJob(def.jobDef, chip);
    }
}

public class JobDriver_BraindanceLesson : JobDriver
{
    private const TargetIndex ChipInd = TargetIndex.A;
    private const int MaxTicks = 5000;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
        => pawn.Reserve(job.GetTarget(ChipInd), job, errorOnFailed: errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedNullOrForbidden(ChipInd);
        yield return Toils_Goto.GotoThing(ChipInd, PathEndMode.Touch);

        // Half the lessons teach the mind, half the eye: chosen per lesson.
        SkillDef skill = Rand.Bool ? SkillDefOf.Intellectual : SkillDefOf.Artistic;
        Toil watch = ToilMaker.MakeToil("BraindanceLesson");
        watch.defaultCompleteMode = ToilCompleteMode.Delay;
        watch.defaultDuration = MaxTicks;
        watch.handlingFacing = true;
        watch.tickAction = () =>
        {
            pawn.rotationTracker.FaceTarget(job.GetTarget(ChipInd));
            float xp = NetrunDefOf.NR_Learning_Braindance?.xpPerTick ?? 0.1f;
            pawn.skills?.Learn(skill, xp * LearningUtility.LearningRateFactor(pawn));
            if (LearningUtility.LearningTickCheckEnd(pawn, 1))
                ReadyForNextToil();
        };
        watch.FailOnCannotTouch(ChipInd, PathEndMode.Touch);
        watch.activeSkill = () => skill;
        yield return watch;
    }
}
