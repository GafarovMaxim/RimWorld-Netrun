using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Netrun;

/// <summary>A colonist walks to the comms console and opens the network terminal there.</summary>
public class JobDriver_UseNetTerminal : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
        => pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOn(() => job.targetA.Thing.TryGetComp<CompPowerTrader>()?.PowerOn == false);

        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

        Toil open = ToilMaker.MakeToil("OpenNetTerminal");
        open.initAction = () =>
        {
            if (pawn.Faction == Faction.OfPlayer)
                NetTerminalUtility.OpenTerminal(pawn.Map);
        };
        open.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return open;
    }
}
