using System;
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
