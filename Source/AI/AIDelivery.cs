using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Netrun;

public enum AIDeliveryKind
{
    Feed,
    Request,
}

/// <summary>
/// Food and requested items are carried to the AI's server or coprocessor by a colonist.
/// AIs riding in a colonist eat from storage directly: their host is right there.
/// </summary>
public static class AIDelivery
{
    private struct Pending
    {
        public int AIId;
        public AIDeliveryKind Kind;
        public AIPersonality SeedGrows;
    }

    // Keyed by job load ID. Not saved: a delivery interrupted by a reload simply fails and can be ordered again.
    private static readonly Dictionary<int, Pending> PendingJobs = [];

    public static bool NeedsCarrying(NetAI ai) => ai.InServer && ai.Server.Spawned;

    /// <summary>Choose who carries it; the largest suitable stack in the colony is taken.</summary>
    public static void OpenCarrierMenu(NetAI ai, Map map, AIDeliveryKind kind, ThingDef thing, int count, AIPersonality seedGrows)
    {
        Thing stack = map.listerThings.ThingsOfDef(thing)
            .Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.stackCount >= count)
            .OrderByDescending(t => t.stackCount)
            .FirstOrDefault();

        if (stack == null)
        {
            Messages.Message("NR_AINeedOneStack".Translate(count, thing.label), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var options = new List<FloatMenuOption>();
        foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed))
        {
            if (!pawn.CanReserveAndReach(stack, PathEndMode.ClosestTouch, Danger.Some)
                || !pawn.CanReach(ai.Server, PathEndMode.Touch, Danger.Some))
                continue;

            Pawn carrier = pawn;
            options.Add(new FloatMenuOption(pawn.LabelShort, () => Order(carrier, ai, kind, stack, count, seedGrows)));
        }

        // D7 (Maxim 2026-10-09): the colony's hauling mechanoids are in the same list; the player picks, nothing is automatic.
        if (ModsConfig.BiotechActive)
            foreach (Pawn mech in map.mapPawns.SpawnedColonyMechs)
            {
                if (mech.Downed || mech.RaceProps.mechEnabledWorkTypes?.Contains(WorkTypeDefOf.Hauling) != true
                    || !mech.CanReserveAndReach(stack, PathEndMode.ClosestTouch, Danger.Some) || !mech.CanReach(ai.Server, PathEndMode.Touch, Danger.Some))
                    continue;
                string label = "NR_AICarrierMech".Translate(mech.LabelShort);
                if (!MechanitorUtility.InMechanitorCommandRange(mech, stack))
                {
                    options.Add(new FloatMenuOption(label + ": " + "NR_AICarrierMechRange".Translate(), null));
                    continue;
                }
                Pawn carrier = mech;
                options.Add(new FloatMenuOption(label, () => Order(carrier, ai, kind, stack, count, seedGrows)));
            }

        if (options.Count == 0)
            options.Add(new FloatMenuOption("NR_AINoCarrier".Translate(), null));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void Order(Pawn pawn, NetAI ai, AIDeliveryKind kind, Thing stack, int count, AIPersonality seedGrows, bool queued = false)
    {
        Job job = JobMaker.MakeJob(NetrunDefOf.NR_DeliverToAI, ai.Server, stack);
        job.count = count;
        PendingJobs[job.loadID] = new Pending { AIId = ai.Id, Kind = kind, SeedGrows = seedGrows };
        // Auto-feed waits for the hauler's current job to end; a player's order starts at once.
        if (queued)
            pawn.jobs.jobQueue.EnqueueLast(job, JobTag.Misc);
        else
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
    }

    /// <summary>
    /// Auto-feed: the nearest free colonist who hauls carries the food to the server after their current job. False when
    /// nobody can (no stack big enough, no free hauler).
    /// </summary>
    public static bool OrderAuto(NetAI ai, Map map, ThingDef thing, int count)
    {
        Thing stack = map.listerThings.ThingsOfDef(thing)
            .Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.stackCount >= count)
            .OrderByDescending(t => t.stackCount).FirstOrDefault();
        if (stack == null)
            return false;
        Pawn hauler = map.mapPawns.FreeColonistsSpawned
            .Where(p => !p.Downed && !p.Drafted && !p.InMentalState && p.workSettings?.WorkIsActive(WorkTypeDefOf.Hauling) == true
                        && p.CanReserveAndReach(stack, PathEndMode.ClosestTouch, Danger.Some) && p.CanReach(ai.Server, PathEndMode.Touch, Danger.Some))
            .MinBy(p => p.Position.DistanceToSquared(stack.Position));
        if (hauler == null)
            return false;
        Order(hauler, ai, AIDeliveryKind.Feed, stack, count, ai.Personality, queued: true);
        return true;
    }

    /// <summary>Called by the job when the items reach the server.</summary>
    public static void Complete(Job job, Thing carried)
    {
        if (!PendingJobs.TryGetValue(job.loadID, out Pending pending))
            return;
        PendingJobs.Remove(job.loadID);

        AIManager manager = AIManager.Get();
        NetAI ai = manager?.ById(pending.AIId);
        if (ai == null || carried == null || carried.stackCount < job.count)
            return;

        carried.SplitOff(job.count).Destroy();

        if (pending.Kind == AIDeliveryKind.Request)
            manager.FulfilDelivered(ai);
        else
            AIBehavior.FedDelivered(ai, pending.SeedGrows);
    }
}

/// <summary>Carry a stack to an AI host building. A: server or coprocessor, B: the stack.</summary>
public class JobDriver_DeliverToAI : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
        => pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, job.count, null, errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(TargetIndex.A);
        this.FailOnDestroyedOrNull(TargetIndex.B);

        yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
        yield return Toils_Haul.StartCarryThing(TargetIndex.B, subtractNumTakenFromJobCount: false);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

        Toil hand = ToilMaker.MakeToil("HandToAI");
        hand.initAction = () => AIDelivery.Complete(job, pawn.carryTracker.CarriedThing);
        hand.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return hand;
    }
}
