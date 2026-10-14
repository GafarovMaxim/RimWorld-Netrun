using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Netrun;

/// <summary>
/// The Transition's techprints come only out of the corporate labs. A zero techprintCommonality is not enough: when ours
/// is the only techprint left, the weighted pick falls back to it and orbital traders sell it. We drop it from what
/// techprint stock generators and techprint reward makers produce.
/// </summary>
[HarmonyPatch]
public static class Patch_TransitionTechprintStock
{
    private static bool Prepare() => TargetMethods().Any();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        if (AccessTools.TypeByName("RimWorld.StockGenerator_Techprints") is { } stock
            && AccessTools.Method(stock, "GenerateThings") is { } generate && generate.DeclaringType == stock)
            yield return generate;
    }

    private static ThingDef Ours => TransitionDefOf.NR_Res_Transition?.Techprint;

    public static void Postfix(ref IEnumerable<Thing> __result)
    {
        try
        {
            ThingDef ours = Ours;
            if (ours != null && __result != null)
                __result = __result.Where(t => t?.def != ours).ToList();
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Techprint stock: " + e.Message, 0x4E52_5452);
        }
    }

    [HarmonyPatch]
    public static class Rewards
    {
        private static bool Prepare() => TargetMethods().Any();

        private static IEnumerable<MethodBase> TargetMethods()
        {
            if (AccessTools.TypeByName("RimWorld.ThingSetMaker_Techprints") is { } maker
                && AccessTools.Method(maker, "Generate", [typeof(RimWorld.ThingSetMakerParams), typeof(List<Thing>)]) is { } generate
                && generate.DeclaringType == maker)
                yield return generate;
        }

        public static void Postfix(List<Thing> outThings)
        {
            try
            {
                ThingDef ours = Ours;
                if (ours != null)
                    outThings?.RemoveAll(t => t?.def == ours);
            }
            catch (System.Exception e)
            {
                Log.ErrorOnce("[Netrun] Techprint rewards: " + e.Message, 0x4E52_5453);
            }
        }
    }
}
