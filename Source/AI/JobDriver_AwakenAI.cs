using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Netrun;

/// <summary>
/// A colonist picks up an AI fragment, carries it to the comms console and coaxes it awake there,
/// then moves it into the AI server. A: console, B: fragment, C: server.
/// </summary>
public class JobDriver_AwakenAI : JobDriver
{
    private const TargetIndex ConsoleInd = TargetIndex.A;
    private const TargetIndex FragmentInd = TargetIndex.B;
    private const TargetIndex ServerInd = TargetIndex.C;

    private Building Server => job.GetTarget(ServerInd).Thing as Building;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
        => pawn.Reserve(job.GetTarget(ConsoleInd), job, errorOnFailed: errorOnFailed)
           && pawn.Reserve(job.GetTarget(FragmentInd), job, 1, 1, null, errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(ConsoleInd);
        this.FailOnDestroyedOrNull(FragmentInd);
        this.FailOn(() => Server == null || Server.Destroyed || Server.GetComp<CompAIServer>()?.HasFreeSlot != true);

        yield return Toils_Goto.GotoThing(FragmentInd, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(FragmentInd);
        yield return Toils_Haul.StartCarryThing(FragmentInd);
        yield return Toils_Goto.GotoThing(ConsoleInd, PathEndMode.InteractionCell);

        float speed = Mathf.Max(0.25f, pawn.GetStatValue(StatDefOf.HackingSpeed));
        int ticks = Mathf.RoundToInt(NetrunDefOf.NR_AI.awakenWorkTicks / speed);

        Toil work = Toils_General.Wait(ticks, ConsoleInd);
        work.FailOnCannotTouch(ConsoleInd, PathEndMode.InteractionCell);
        work.FailOn(() => pawn.carryTracker.CarriedThing?.def != NetrunDefOf.NR_AIFragment);
        work.WithProgressBarToilDelay(ConsoleInd);
        work.WithEffect(EffecterDefOf.Hacking, ConsoleInd);
        work.tickAction = () => pawn.skills?.Learn(SkillDefOf.Intellectual, 0.05f);
        work.activeSkill = () => SkillDefOf.Intellectual;
        yield return work;

        Toil finish = ToilMaker.MakeToil("AwakenAI");
        finish.initAction = () =>
        {
            Thing carried = pawn.carryTracker.CarriedThing;
            if (carried != null)
                AIManager.Get()?.Awaken(carried, Server, pawn);
        };
        finish.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return finish;
    }
}
