using System;
using HarmonyLib;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// Vanilla hook that fires once when a hack completes, for pawns and for Hacking Expansion cyberpods alike
/// (inside a pod, <paramref name="hacker"/> is the pawn in the pod).
/// </summary>
[HarmonyPatch(typeof(CompHackable), "ProcessHacked")]
public static class Patch_CompHackable_ProcessHacked
{
    static void Postfix(CompHackable __instance, Pawn hacker)
    {
        try
        {
            if (hacker == null || hacker.Faction != Faction.OfPlayer)
                return;

            string reason = "NR_LogHack".Translate(hacker.LabelShort, __instance.parent.LabelCap);
            HeatTracker.Get()?.AddHeat(HackHeatUtility.HeatForHack(__instance, hacker), reason);
            ContractManager.Get()?.NotifyHackCompleted();
            TryGiveCyberpodBonusData(hacker);
            TryDropAIFinds(__instance, hacker);
        }
        catch (Exception ex)
        {
            Log.ErrorOnce("[Netrun] Failed to register hack heat: " + ex, 0x4E520001);
        }
    }

    /// <summary>Strong ICE sometimes hides a sleeping AI fragment or a server core module.</summary>
    private static void TryDropAIFinds(CompHackable comp, Pawn hacker)
    {
        AISettingsDef settings = NetrunDefOf.NR_AI;
        Thing target = comp.parent;
        if (!target.Spawned)
            return;

        HacksetDef hackset = target.TryGetComp<CompDataSourceProtected>()?.HacksetDef;
        bool blackIce = hackset == USH_DefOf.USH_BlackICE;
        bool coreCommand = hackset?.defName == "USH_CoreCommandICE";
        bool blackBox = target.def.defName == "USH_BlackBox";

        float fragmentChance = blackIce ? settings.fragmentChanceBlackICE : coreCommand ? settings.fragmentChanceCoreCommandICE : 0f;
        float moduleChance = blackIce ? settings.moduleChanceBlackICE : blackBox ? settings.moduleChanceBlackBox : 0f;

        if (Rand.Chance(fragmentChance))
            DropFind(NetrunDefOf.NR_AIFragment, target, hacker);
        if (Rand.Chance(moduleChance))
            DropFind(NetrunDefOf.NR_AICoreModule, target, hacker);
    }

    private static void DropFind(ThingDef def, Thing target, Pawn hacker)
    {
        Thing thing = ThingMaker.MakeThing(def);
        GenPlace.TryPlaceThing(thing, target.Position, target.Map, ThingPlaceMode.Near);
        Find.LetterStack.ReceiveLetter(
            "NR_FindLabel".Translate(def.LabelCap),
            "NR_Find".Translate(hacker.LabelShort, target.LabelCap, def.label),
            LetterDefOf.PositiveEvent, new LookTargets(thing));
    }

    /// <summary>A hack finished from a cyberpod sometimes yields an extra cyberdata next to the pod.</summary>
    private static void TryGiveCyberpodBonusData(Pawn hacker)
    {
        if (!hacker.TryGetHoldingCyberpod(out Building_Cyberpod pod) || !pod.Spawned)
            return;

        if (!Rand.Chance(NetrunDefOf.NR_Heat.cyberpodBonusDataChance))
            return;

        Thing data = ThingMaker.MakeThing(NetrunDefOf.USH_Cyberdata);
        GenPlace.TryPlaceThing(data, pod.InteractionCell, pod.Map, ThingPlaceMode.Near);
        Messages.Message("NR_CyberpodBonusData".Translate(hacker.LabelShort), new LookTargets(data), MessageTypeDefOf.PositiveEvent, historical: false);
    }
}
