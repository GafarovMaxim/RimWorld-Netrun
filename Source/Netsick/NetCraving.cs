using System.Linq;
using RimWorld;
using USH_HE;
using Verse;
using Verse.AI;

namespace Netrun;

/// <summary>Withdrawal mood: only while the dependency is strong and the pawn has been offline for a while.</summary>
public class ThoughtWorker_NetsickWithdrawal : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p)
    {
        if (p.health?.hediffSet?.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick) is not Hediff_Netsick netsick || !netsick.Withdrawing)
            return ThoughtState.Inactive;

        int stage = System.Math.Min(def.stages.Count - 1, System.Math.Max(0, netsick.CurStageIndex - 1));
        return ThoughtState.ActiveAtStage(stage);
    }
}

public static class NetCravingUtility
{
    /// <summary>A powered, empty cyberpod the pawn can reach.</summary>
    public static Building_Cyberpod FreePod(Pawn pawn)
    {
        var pods = pawn.Map?.GetComponent<MapComponent_CyberpodManager>()?.Pods;
        return pods?.FirstOrDefault(pod => pod.Spawned
            && !pod.HasAnyContents
            && pod.GetComp<CompPowerTrader>()?.PowerOn != false
            && pawn.CanReach(pod, PathEndMode.InteractionCell, Danger.Some));
    }
}

/// <summary>Only hackers with a free cyberpod in reach can break this way.</summary>
public class MentalStateWorker_NetCraving : MentalStateWorker
{
    public override bool StateCanOccur(Pawn pawn)
        => base.StateCanOccur(pawn) && pawn.Spawned && pawn.IsHacker() && !NeurostabilizerUtility.Stabilized(pawn)
           && NetCravingUtility.FreePod(pawn) != null;
}

/// <summary>"Net craving": the pawn heads for a cyberpod and dives on emotion.</summary>
public class MentalState_NetCraving : MentalState
{
    public override void MentalStateTick(int delta)
    {
        base.MentalStateTick(delta);

        if (pawn.TryGetHoldingCyberpod(out _))
        {
            StartEmotionalDive();
            return;
        }

        if (!pawn.IsHashIntervalTick(60, delta) || pawn.CurJobDef == USH_DefOf.USH_EnterCyberpod)
            return;

        Building_Cyberpod pod = NetCravingUtility.FreePod(pawn);
        if (pod == null)
        {
            RecoverFromState();
            return;
        }

        if (pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick) is Hediff_Netsick netsick && netsick.CravingSinceTick < 0)
            netsick.CravingSinceTick = Find.TickManager.TicksGame;
        pawn.jobs.StartJob(JobMaker.MakeJob(USH_DefOf.USH_EnterCyberpod, pod), JobCondition.InterruptForced);
    }

    private void StartEmotionalDive()
    {
        if (pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick) is Hediff_Netsick netsick)
            NetsickManager.BeginEmotionalDive(pawn, netsick);
        RecoverFromState();
    }
}
