using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Netrun;

/// <summary>Raw recordings are edited into chips at the comms console.</summary>
public static class BraindanceEditing
{
    public const int EditWorkTicks = 2500;

    /// <summary>Which chip a recording becomes: a bright memory makes an ordinary chip, a dark one or a kill a black chip.</summary>
    public static IEnumerable<ThingDef> ChipsFor(BraindanceMood mood)
    {
        switch (mood)
        {
            case BraindanceMood.Bright:
                yield return NetrunDefOf.NR_BraindanceChip;
                break;
            default:
                yield return NetrunDefOf.NR_BlackBraindanceChip;
                break;
        }
    }

    public static IEnumerable<Thing> RawRecordings(Map map)
        => map.listerThings.ThingsOfDef(NetrunDefOf.NR_BraindanceRaw).Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer));

    /// <summary>Right-click options on the comms console: carry a raw recording here and edit it into a chip.</summary>
    public static IEnumerable<FloatMenuOption> FloatOptions(Pawn pawn, Building console)
    {
        List<Thing> raws = RawRecordings(pawn.Map).ToList();
        if (raws.Count == 0)
            yield break;

        if (pawn.WorkTagIsDisabled(WorkTags.Intellectual))
        {
            yield return new FloatMenuOption("NR_BDEditCommand".Translate() + " (" + "NR_AwakenNoWorker".Translate() + ")", null);
            yield break;
        }

        foreach (Thing raw in raws)
        {
            if (!pawn.CanReserveAndReach(raw, PathEndMode.ClosestTouch, Danger.Some))
                continue;

            CompBraindanceRecord rec = raw.TryGetComp<CompBraindanceRecord>();
            if (rec == null)
                continue;

            foreach (ThingDef chip in ChipsFor(rec.Mood))
            {
                Thing capturedRaw = raw;
                ThingDef capturedChip = chip;
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(
                    "NR_BDEditOption".Translate(rec.Author, rec.Event, chip.label),
                    () => Order(pawn, capturedRaw, console, capturedChip), NetrunTex.BDRecord, Color.white), pawn, console);
            }
        }
    }

    private static void Order(Pawn pawn, Thing raw, Building console, ThingDef chip)
    {
        Job job = JobMaker.MakeJob(NetrunDefOf.NR_EditBraindance, console, raw);
        job.count = 1;
        job.thingDefToCarry = chip;
        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
    }

    /// <summary>The chip keeps the recording; a skilled editor makes it a little better.</summary>
    public static void Finish(Pawn editor, Thing raw, ThingDef chipDef)
    {
        CompBraindanceRecord source = raw.TryGetComp<CompBraindanceRecord>();
        Thing chip = ThingMaker.MakeThing(chipDef);
        CompBraindanceRecord target = chip.TryGetComp<CompBraindanceRecord>();
        if (source != null && target != null)
        {
            target.CopyFrom(source);
            int skill = editor.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
            target.Strength = Mathf.Clamp01(source.Strength + skill * 0.015f);
        }

        IntVec3 cell = editor.Position;
        Map map = editor.Map;
        raw.Destroy();
        GenPlace.TryPlaceThing(chip, cell, map, ThingPlaceMode.Near);
        Messages.Message("NR_BDEdited".Translate(editor.LabelShort, chip.LabelCap), chip, MessageTypeDefOf.PositiveEvent, historical: false);
    }
}

/// <summary>A: comms console, B: raw recording. The chip type rides on job.thingDefToCarry.</summary>
public class JobDriver_EditBraindance : JobDriver
{
    private const TargetIndex ConsoleInd = TargetIndex.A;
    private const TargetIndex RawInd = TargetIndex.B;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
        => pawn.Reserve(job.GetTarget(ConsoleInd), job, errorOnFailed: errorOnFailed)
           && pawn.Reserve(job.GetTarget(RawInd), job, 1, 1, null, errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(ConsoleInd);
        this.FailOnDestroyedOrNull(RawInd);
        this.FailOn(() => job.thingDefToCarry == null || job.GetTarget(ConsoleInd).Thing.TryGetComp<CompNetTerminal>()?.Powered != true);

        yield return Toils_Goto.GotoThing(RawInd, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(RawInd);
        yield return Toils_Haul.StartCarryThing(RawInd);
        yield return Toils_Goto.GotoThing(ConsoleInd, PathEndMode.InteractionCell);

        Toil work = Toils_General.Wait(BraindanceEditing.EditWorkTicks, ConsoleInd);
        work.FailOnCannotTouch(ConsoleInd, PathEndMode.InteractionCell);
        work.FailOn(() => pawn.carryTracker.CarriedThing?.def != NetrunDefOf.NR_BraindanceRaw);
        work.WithProgressBarToilDelay(ConsoleInd);
        work.WithEffect(EffecterDefOf.Hacking, ConsoleInd);
        work.tickAction = () => pawn.skills?.Learn(SkillDefOf.Intellectual, 0.05f);
        work.activeSkill = () => SkillDefOf.Intellectual;
        yield return work;

        Toil finish = ToilMaker.MakeToil("EditBraindance");
        finish.initAction = () =>
        {
            Thing carried = pawn.carryTracker.CarriedThing;
            if (carried == null)
                return;
            pawn.carryTracker.innerContainer.Remove(carried);
            BraindanceEditing.Finish(pawn, carried, job.thingDefToCarry);
        };
        finish.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return finish;
    }
}
