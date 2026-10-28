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

    /// <summary>From the Studio: pick a colonist who carries the recording to a network console and edits it.</summary>
    public static void OpenEditorMenu(Thing raw, ThingDef chip)
    {
        Map map = raw.MapHeld;
        Building console = map == null ? null : AIAwakening.Console(map);
        var options = new List<FloatMenuOption>();
        if (console != null)
        {
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed && !p.WorkTagIsDisabled(WorkTags.Intellectual)))
            {
                Pawn editor = pawn;
                int skill = pawn.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
                options.Add(new FloatMenuOption($"{pawn.LabelShort} ({SkillDefOf.Intellectual.LabelCap}: {skill})", () => Order(editor, raw, console, chip)));
            }
        }
        if (options.Count == 0)
            options.Add(new FloatMenuOption(console == null ? "NR_AwakenNoConsole".Translate() : "NR_AwakenNoWorker".Translate(), null));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    /// <summary>Raw recordings grouped by author, event and mood: one chip per group in a batch edit.</summary>
    public static int BatchGroups(List<Thing> raws)
    {
        var keys = new HashSet<(string, string, BraindanceMood)>();
        foreach (Thing raw in raws)
            if (raw.TryGetComp<CompBraindanceRecord>() is { HasRecord: true } rec)
                keys.Add((rec.Author, rec.Event, rec.Mood));
        return keys.Count;
    }

    /// <summary>A batch edit takes longer with more distinct recordings, but far less than one by one (half a day at most).</summary>
    public static int BatchWorkTicks(int groups) => Mathf.Min(GenDate.TicksPerDay / 2, Mathf.CeilToInt(EditWorkTicks * Mathf.Sqrt(Mathf.Max(1, groups))));

    /// <summary>A batch edit glitches now and then (Maxim, 2026-10-15: 5%), and then a share of the chips is lost or worse.</summary>
    public const float BatchGlitchChance = 0.05f, BatchGlitchShare = 0.15f;

    /// <summary>
    /// From the Studio: "edit all" in one click. The best editor who can reach them gathers every raw stack, carries them
    /// to the network console and edits them there (Maxim, 2026-10-28: chips no longer teleport). The first time a
    /// warning about the glitch comes first.
    /// </summary>
    public static void StartBatch(Map map)
    {
        if (map == null)
            return;
        HelpProgress progress = HelpProgress.Get();
        if (progress != null && !progress.HintSeen("BDBatchWarn"))
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "NR_BDBatchWarn".Translate((BatchGlitchChance * 100f).ToString("0"), (BatchGlitchShare * 100f).ToString("0")),
                () =>
                {
                    progress.MarkHint("BDBatchWarn");
                    OrderBatch(map);
                }));
            return;
        }
        OrderBatch(map);
    }

    private static void OrderBatch(Map map)
    {
        Building console = AIAwakening.Console(map);
        if (console == null)
        {
            Messages.Message("NR_AwakenNoConsole".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }
        List<Thing> raws = RawRecordings(map).ToList();
        Pawn editor = map.mapPawns.FreeColonistsSpawned
            .Where(p => !p.Downed && !p.Drafted && !p.WorkTagIsDisabled(WorkTags.Intellectual) && p.CanReserveAndReach(console, PathEndMode.InteractionCell, Danger.Some))
            .MaxByWithFallback(p => p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0);
        List<Thing> reachable = editor == null ? [] : raws.Where(t => editor.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some)).ToList();
        if (reachable.Count == 0)
        {
            Messages.Message("NR_AwakenNoWorker".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }
        Job job = JobMaker.MakeJob(NetrunDefOf.NR_EditBraindanceBatch, console);
        foreach (Thing raw in reachable)
            job.AddQueuedTarget(TargetIndex.B, raw);
        editor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        Messages.Message("NR_BDBatchOrdered".Translate(editor.LabelShort, reachable.Sum(t => t.stackCount)), editor, MessageTypeDefOf.NeutralEvent, historical: false);
    }

    /// <summary>The batch job at the console: the raw stacks the editor carries are edited.</summary>
    public static void FinishBatch(Pawn editor)
        => FinishBatch(editor.Map, editor, editor.inventory.innerContainer.Where(t => t.def == NetrunDefOf.NR_BraindanceRaw).ToList());

    /// <summary>
    /// Edits every raw recording on the map at once: one chip per author, event and mood, from the strongest take;
    /// the duplicates are wiped (Maxim, 2026-10-14: editing thousands one by one was impossible). A glitch loses
    /// some chips and dulls others.
    /// </summary>
    public static void FinishBatch(Map map, Pawn editor, List<Thing> raws)
    {
        if (map == null || raws.Count == 0)
            return;
        // Every recording of every stack (2026-10-23: a stack holds different recordings; only its top one was edited).
        var best = new Dictionary<(string, string, BraindanceMood), BraindanceRecordData>();
        int total = 0;
        foreach (Thing raw in raws)
        {
            if (raw.TryGetComp<CompBraindanceRecord>() is not { HasRecord: true } rec)
                continue;
            foreach (BraindanceRecordData r in rec.All)
            {
                total++;
                var key = (r.Author, r.Event, r.Mood);
                if (!best.TryGetValue(key, out BraindanceRecordData kept) || kept.Strength < r.Strength)
                    best[key] = r;
            }
        }
        if (best.Count == 0)
            return;
        int skill = editor?.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
        IntVec3 cell = editor?.Spawned == true && editor.Map == map ? editor.Position
            : AIAwakening.Console(map)?.InteractionCell ?? raws[0].PositionHeld;
        if (best.Count == 0)
            return;

        // The glitch: a share of the chips (at least one) is hit, half of them lost, half dulled.
        var hit = new HashSet<BraindanceRecordData>();
        if (Rand.Chance(BatchGlitchChance))
            foreach (BraindanceRecordData raw in best.Values.InRandomOrder().Take(Mathf.Max(1, Mathf.RoundToInt(best.Count * BatchGlitchShare))))
                hit.Add(raw);
        int lost = 0, dulled = 0, made = 0;
        foreach (BraindanceRecordData source in best.Values)
        {
            bool glitched = hit.Contains(source);
            if (glitched && Rand.Bool)
            {
                lost++;
                continue;
            }
            ThingDef chipDef = ChipsFor(source.Mood).First();
            Thing chip = ThingMaker.MakeThing(chipDef);
            if (chip.TryGetComp<CompBraindanceRecord>() is { } target)
            {
                target.CopyFrom(source);
                target.Strength = Mathf.Clamp01(source.Strength + skill * 0.015f);
                if (glitched)
                {
                    target.Strength *= 0.6f;
                    dulled++;
                }
                ContractManager.Get()?.NotifyChipEdited(target.Mood);
                AIQuests.NotifyEdited(target.Mood, target.QualityTier);
                if (target.QualityTier >= 2)
                    NetrunSignals.Increment("MasterChip");
            }
            GenPlace.TryPlaceThing(chip, cell, map, ThingPlaceMode.Near);
            made++;
        }
        foreach (Thing raw in raws)
            if (!raw.Destroyed)
                raw.Destroy();
        string who = editor?.LabelShort ?? (string)"NR_BDBatchConsole".Translate();
        if (hit.Count > 0)
            Find.LetterStack.ReceiveLetter("NR_BDBatchGlitchLabel".Translate(), "NR_BDBatchGlitch".Translate(who, total, made, lost, dulled),
                LetterDefOf.NegativeEvent, new LookTargets(cell, map));
        else
            Messages.Message("NR_BDBatchDone".Translate(who, total, made), new LookTargets(cell, map), MessageTypeDefOf.PositiveEvent, historical: false);
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
            ContractManager.Get()?.NotifyChipEdited(target.Mood);
            AIQuests.NotifyEdited(target.Mood, target.QualityTier);
            if (target.QualityTier >= 2)
                NetrunSignals.Increment("MasterChip");
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

/// <summary>
/// A: comms console, queue B: the raw stacks. The editor picks every stack up, carries them all to the console and edits
/// them there in one go, one chip per kind. Interrupted, the stacks are dropped where the editor stands.
/// </summary>
public class JobDriver_EditBraindanceBatch : JobDriver
{
    private const TargetIndex ConsoleInd = TargetIndex.A;
    private const TargetIndex RawInd = TargetIndex.B;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if (!pawn.Reserve(job.GetTarget(ConsoleInd), job, errorOnFailed: errorOnFailed))
            return false;
        pawn.ReserveAsManyAsPossible(job.GetTargetQueue(RawInd), job);
        return true;
    }

    private bool Gone() => job.GetTarget(RawInd).Thing is not { Spawned: true } t || t.IsForbidden(pawn);

    private List<Thing> Carried() => pawn.inventory.innerContainer.Where(t => t.def == NetrunDefOf.NR_BraindanceRaw).ToList();

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(ConsoleInd);
        this.FailOn(() => job.GetTarget(ConsoleInd).Thing.TryGetComp<CompNetTerminal>()?.Powered != true);
        AddFinishAction(condition =>
        {
            if (condition == JobCondition.Succeeded || !pawn.Spawned)
                return;
            foreach (Thing raw in Carried())
                pawn.inventory.innerContainer.TryDrop(raw, pawn.Position, pawn.Map, ThingPlaceMode.Near, out _);
        });

        // Gather: every queued stack still lying around goes into the editor's pockets; one gone is skipped.
        Toil toConsole = Toils_Goto.GotoThing(ConsoleInd, PathEndMode.InteractionCell);
        Toil next = Toils_JobTransforms.ExtractNextTargetFromQueue(RawInd);
        yield return next;
        yield return Toils_Jump.JumpIf(toConsole, () => Gone() && job.GetTargetQueue(RawInd).Count == 0);
        yield return Toils_Jump.JumpIf(next, Gone);
        yield return Toils_Goto.GotoThing(RawInd, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(RawInd);
        // TakeToInventory refuses a job without a count (logs "Invalid count: -1"): the whole stack.
        Toil count = ToilMaker.MakeToil("SetBatchCount");
        count.initAction = () => job.count = job.GetTarget(RawInd).Thing?.stackCount ?? 1;
        count.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return count;
        yield return Toils_Haul.TakeToInventory(RawInd, () => job.GetTarget(RawInd).Thing?.stackCount ?? 0);
        yield return Toils_Jump.JumpIfHaveTargetInQueue(RawInd, next);

        yield return toConsole;

        Toil work = Toils_General.Wait(BraindanceEditing.EditWorkTicks, ConsoleInd);
        work.initAction = () => work.actor.jobs.curDriver.ticksLeftThisToil = BraindanceEditing.BatchWorkTicks(BraindanceEditing.BatchGroups(Carried()));
        work.FailOnCannotTouch(ConsoleInd, PathEndMode.InteractionCell);
        work.WithProgressBarToilDelay(ConsoleInd);
        work.WithEffect(EffecterDefOf.Hacking, ConsoleInd);
        work.tickAction = () => pawn.skills?.Learn(SkillDefOf.Intellectual, 0.05f);
        work.activeSkill = () => SkillDefOf.Intellectual;
        yield return work;

        Toil finish = ToilMaker.MakeToil("EditBraindanceBatch");
        finish.initAction = () => BraindanceEditing.FinishBatch(pawn);
        finish.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return finish;
    }
}
