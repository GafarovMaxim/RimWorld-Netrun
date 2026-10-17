using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Netrun;

/// <summary>
/// The data courier lies low in the room he was given and stays out of the colony's fights (Maxim, 2026-10-17);
/// when his time is up he walks off the map. LordJob_VisitColony let him wander outside and join every fight.
/// </summary>
public class LordJob_CourierHide : LordJob
{
    private IntVec3 _spot;
    private int _ticks;

    public LordJob_CourierHide()
    {
    }

    public LordJob_CourierHide(IntVec3 spot, int ticks)
    {
        _spot = spot;
        _ticks = ticks;
    }

    public override StateGraph CreateGraph()
    {
        var graph = new StateGraph();
        var hide = new LordToil_CourierHide(_spot);
        graph.AddToil(hide);
        var leave = new LordToil_ExitMap(LocomotionUrgency.Jog, canDig: false, interruptCurrentJob: true);
        graph.AddToil(leave);
        var done = new Transition(hide, leave);
        done.AddTrigger(new Trigger_TicksPassed(_ticks));
        graph.AddTransition(done);
        return graph;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref _spot, "spot");
        Scribe_Values.Look(ref _ticks, "ticks");
    }
}

public class LordToil_CourierHide : LordToil
{
    private readonly IntVec3 _spot;

    public LordToil_CourierHide(IntVec3 spot) => _spot = spot;

    public override void UpdateAllDuties()
    {
        foreach (Pawn pawn in lord.ownedPawns)
            pawn.mindState.duty = new PawnDuty(NetrunDefOf.NR_CourierHide, _spot);
    }
}

/// <summary>Walks or stands inside the room of the duty spot; outdoors (no room was found) only near the spot.</summary>
public class JobGiver_CourierHide : ThinkNode_JobGiver
{
    protected override Job TryGiveJob(Pawn pawn)
    {
        IntVec3 spot = pawn.mindState.duty?.focus.Cell ?? IntVec3.Invalid;
        if (!spot.IsValid)
            return null;
        Map map = pawn.Map;
        Room room = spot.GetRoom(map);
        bool indoors = room != null && !room.PsychologicallyOutdoors;
        if (Fight(pawn, indoors ? room : null) is { } fight)
            return fight;
        bool there = indoors ? pawn.GetRoom() == room : pawn.Position.InHorDistOf(spot, 6f);
        if (there && Rand.Chance(0.5f))
        {
            Job wait = JobMaker.MakeJob(JobDefOf.Wait_Wander);
            wait.expiryInterval = Rand.Range(200, 500);
            return wait;
        }
        IntVec3 dest = RCellFinder.RandomWanderDestFor(pawn, spot, indoors ? 12f : 6f,
            (p, c, _) => !indoors || c.GetRoom(p.Map) == room, Danger.Deadly);
        if (!dest.IsValid || dest == pawn.Position)
            dest = spot;
        if (dest == pawn.Position)
        {
            Job wait = JobMaker.MakeJob(JobDefOf.Wait_Wander);
            wait.expiryInterval = 300;
            return wait;
        }
        Job go = JobMaker.MakeJob(JobDefOf.GotoWander, dest);
        go.locomotionUrgency = there ? LocomotionUrgency.Walk : LocomotionUrgency.Jog;
        return go;
    }

    private const float CloseRange = 5f;

    /// <summary>He fights only an enemy who got into his room or right up to him (Maxim, 2026-10-17); never chases outside.</summary>
    private static Job Fight(Pawn pawn, Room room)
    {
        if (pawn.WorkTagIsDisabled(WorkTags.Violent))
            return null;
        var target = AttackTargetFinder.BestAttackTarget(pawn, TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
            t => (room != null && t.GetRoom() == room) || t.Position.InHorDistOf(pawn.Position, CloseRange), 0f, 40f)?.Thing;
        if (target == null)
            return null;
        Verb verb = pawn.TryGetAttackVerb(target);
        if (verb == null)
            return null;
        Job job = JobMaker.MakeJob(verb.IsMeleeAttack ? JobDefOf.AttackMelee : JobDefOf.AttackStatic, target);
        // Short jobs: the target is checked again soon, so he stops once it leaves the room.
        job.expiryInterval = 120;
        job.checkOverrideOnExpire = true;
        job.endIfCantShootTargetFromCurPos = true;
        return job;
    }
}
