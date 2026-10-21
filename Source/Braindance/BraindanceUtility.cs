using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using USH_HE;
using Verse;
using Verse.AI;

namespace Netrun;

/// <summary>
/// A braindance chip is played, not swallowed: the pawn needs braindance goggles or a cyberlink.
/// Every vanilla route to an ingest job for a chip is gated here.
/// </summary>
public static class BraindanceUtility
{
    public static bool IsChip(ThingDef def) => def != null && def.HasModExtension<BraindanceChipExtension>();

    public static bool HasGoggles(Pawn pawn)
        => pawn.apparel?.WornApparel?.Any(a => a.def == NetrunDefOf.NR_BraindanceGoggles) == true;

    public static AcceptanceReport CanWatch(Pawn pawn)
    {
        if (pawn == null)
            return false;
        if (HasGoggles(pawn) || CyberUtils.IsHacker(pawn))
            return true;
        return "NR_BDNeedsDevice".Translate();
    }

    /// <summary>True when the pawn is blocked from playing this thing (a chip without goggles or a cyberlink).</summary>
    public static bool Blocked(Pawn pawn, Thing thing)
    {
        try
        {
            return thing != null && IsChip(thing.def) && !CanWatch(pawn).Accepted;
        }
        catch (Exception ex)
        {
            Log.ErrorOnce("[Netrun] Braindance check failed: " + ex, 0x4E520030);
            return false;
        }
    }

    public static bool IsOwn(Pawn viewer, BraindanceRecordData r)
        => r.AuthorId.NullOrEmpty() ? r.Author == viewer.LabelShort : r.AuthorId == viewer.ThingID;

    /// <summary>The viewer's relative (alive or dead) who recorded it; older recordings match by name.</summary>
    public static Pawn Relative(Pawn viewer, BraindanceRecordData r)
    {
        if (viewer.relations == null || r.Author.NullOrEmpty())
            return null;
        foreach (Pawn other in viewer.relations.RelatedPawns)
            if (other != viewer && (r.AuthorId.NullOrEmpty() ? other.LabelShort == r.Author : other.ThingID == r.AuthorId))
                return other;
        return null;
    }

    /// <summary>"own" or the relation ("brother"), for the record lists; null for strangers.</summary>
    public static string RelationTag(Pawn viewer, BraindanceRecordData r)
    {
        if (IsOwn(viewer, r))
            return "NR_BDOwn".Translate();
        Pawn other = Relative(viewer, r);
        return other == null ? null : viewer.GetMostImportantRelation(other)?.GetGenderSpecificLabel(other);
    }

    /// <summary>
    /// Watching one's own memory or a loved one's leaves a memory of its own (Maxim, 2026-10-22): stronger if they are dead.
    /// Relatives the viewer dislikes (rivals, exes) give nothing.
    /// </summary>
    public static void RelativeMemory(Pawn viewer, BraindanceRecordData r)
    {
        if (viewer?.needs?.mood == null || r.Author.NullOrEmpty())
            return;
        int stage;
        if (IsOwn(viewer, r))
            stage = 0;
        else if (Relative(viewer, r) is { } other && viewer.GetMostImportantRelation(other)?.opinionOffset > 0)
            stage = other.Dead ? 2 : 1;
        else
            return;
        viewer.needs.mood.thoughts.memories.TryGainMemory((Thought_Memory)ThoughtMaker.MakeThought(NetrunDefOf.NR_BraindanceRelativeMemory, stage));
    }

    /// <summary>The pawn goes and watches this recording of the stack: it is put on top, so the chip taken plays it.</summary>
    public static void OrderWatch(Pawn pawn, Thing chip, int index)
    {
        if (chip.Destroyed || !chip.Spawned)
            return;
        chip.TryGetComp<CompBraindanceRecord>()?.PutOnTop(index);
        Job job = JobMaker.MakeJob(JobDefOf.Ingest, chip);
        job.count = 1;
        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
    }

    /// <summary>Why this pawn cannot watch this chip now, or true.</summary>
    public static AcceptanceReport CanWatchChip(Pawn pawn, Thing chip)
    {
        AcceptanceReport device = CanWatch(pawn);
        if (!device.Accepted)
            return device;
        if (chip.IsForbidden(pawn) || !pawn.CanReach(chip, PathEndMode.ClosestTouch, Danger.Deadly))
            return "NoPath".Translate();
        return true;
    }

    /// <summary>The stack's recordings for a menu, the viewer's own and relatives' first.</summary>
    public static List<FloatMenuOption> WatchOptions(Pawn pawn, Thing chip)
    {
        var options = new List<FloatMenuOption>();
        if (chip.TryGetComp<CompBraindanceRecord>() is not { HasRecord: true } rec)
            return options;
        List<BraindanceRecordData> all = rec.All;
        foreach (int i in Enumerable.Range(0, all.Count).OrderBy(i => RelationTag(pawn, all[i]) == null).ThenBy(i => all[i].Author))
        {
            int index = i;
            options.Add(new FloatMenuOption(CompBraindanceRecord.Line(all[i], pawn), () => OrderWatch(pawn, chip, index)));
        }
        return options;
    }
}

/// <summary>Right-click a stack of recorded chips: "Watch..." lists every recording in it (Maxim, 2026-10-22).</summary>
public class FloatMenuOptionProvider_WatchBraindance : FloatMenuOptionProvider
{
    protected override bool Drafted => false;
    protected override bool Undrafted => true;
    protected override bool Multiselect => false;

    protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
    {
        try
        {
            if (!BraindanceUtility.IsChip(clickedThing.def) || clickedThing.TryGetComp<CompBraindanceRecord>() is not { HasRecord: true })
                return null;
            Pawn pawn = context.FirstSelectedPawn;
            AcceptanceReport can = BraindanceUtility.CanWatchChip(pawn, clickedThing);
            if (!can.Accepted)
                return new FloatMenuOption("NR_BDWatchMenu".Translate() + ": " + can.Reason, null);
            return new FloatMenuOption("NR_BDWatchMenu".Translate(), () => Find.WindowStack.Add(new FloatMenu(BraindanceUtility.WatchOptions(pawn, clickedThing))));
        }
        catch (Exception e)
        {
            Log.ErrorOnce("[Netrun] FloatMenuOptionProvider_WatchBraindance: " + e.Message, 0x4E525406);
            return null;
        }
    }
}

/// <summary>Marks a ThingDef as a braindance chip.</summary>
public class BraindanceChipExtension : DefModExtension
{
}

[HarmonyPatch(typeof(JoyGiver_Ingest), "CanIngestForJoy")]
public static class Patch_JoyGiver_Ingest_CanIngestForJoy
{
    static void Postfix(Pawn pawn, Thing t, ref bool __result)
    {
        try
        {
            if (__result && BraindanceUtility.Blocked(pawn, t))
                __result = false;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Patch_JoyGiver_Ingest_CanIngestForJoy: " + e.Message, 0x4E525401);
        }
    }
}

[HarmonyPatch(typeof(JobGiver_SatisfyChemicalNeed), "DrugValidator")]
public static class Patch_JobGiver_SatisfyChemicalNeed_DrugValidator
{
    static void Postfix(Pawn pawn, Thing drug, ref bool __result)
    {
        try
        {
            if (__result && BraindanceUtility.Blocked(pawn, drug))
                __result = false;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Patch_JobGiver_SatisfyChemicalNeed_DrugValidator: " + e.Message, 0x4E525402);
        }
    }
}

[HarmonyPatch(typeof(JobGiver_BingeDrug), "BestIngestTarget")]
public static class Patch_JobGiver_BingeDrug_BestIngestTarget
{
    static void Postfix(Pawn pawn, ref Thing __result)
    {
        try
        {
            if (BraindanceUtility.Blocked(pawn, __result))
                __result = null;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Patch_JobGiver_BingeDrug_BestIngestTarget: " + e.Message, 0x4E525403);
        }
    }
}

/// <summary>Drug policy, chemical dependency and drug joy all end here.</summary>
[HarmonyPatch(typeof(DrugAIUtility), nameof(DrugAIUtility.IngestAndTakeToInventoryJob))]
public static class Patch_DrugAIUtility_IngestAndTakeToInventoryJob
{
    static bool Prefix(Thing drug, Pawn pawn, ref Job __result)
    {
        try
        {
            if (!BraindanceUtility.Blocked(pawn, drug))
                return true;
            __result = null;
            return false;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Patch_DrugAIUtility_IngestAndTakeToInventoryJob: " + e.Message, 0x4E525404);
            return true;
        }
    }
}

/// <summary>The right-click "Play" option says why it is unavailable.</summary>
[HarmonyPatch(typeof(FloatMenuOptionProvider_Ingest), "GetSingleOptionFor")]
public static class Patch_FloatMenuOptionProvider_Ingest
{
    static void Postfix(Thing clickedThing, FloatMenuContext context, ref FloatMenuOption __result)
    {
        try
        {
            if (__result == null || clickedThing == null || !BraindanceUtility.IsChip(clickedThing.def))
                return;

            AcceptanceReport report = BraindanceUtility.CanWatch(context.FirstSelectedPawn);
            if (!report.Accepted)
                __result = new FloatMenuOption(__result.Label + ": " + report.Reason, null);
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Patch_FloatMenuOptionProvider_Ingest: " + e.Message, 0x4E525405);
        }
    }
}
