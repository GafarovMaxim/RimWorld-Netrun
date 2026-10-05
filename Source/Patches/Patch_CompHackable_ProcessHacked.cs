using System;
using System.Linq;
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
            AIManager.Get()?.NotifyHack();
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

    /// <summary>
    /// Extra cyberdata: from a cyberpod (more with a Netrun coprocessor) and from a hosted Data Hunter.
    /// </summary>
    private static void TryGiveCyberpodBonusData(Pawn hacker)
    {
        float chance = 0f;
        Thing anchor = hacker;

        if (hacker.TryGetHoldingCyberpod(out Building_Cyberpod pod) && pod.Spawned)
        {
            anchor = pod;
            chance += NetrunDefOf.NR_Heat.cyberpodBonusDataChance;
            if (CyberpodUtility.HasNetrunCoprocessor(pod))
                chance += NetrunDefOf.NR_Heat.coprocessorBonusDataChance;
        }

        if (AIManager.Get()?.AIs.Any(a => a.Host == hacker && a.Personality == AIPersonality.DataHunter && a.Active) == true)
            chance += NetrunDefOf.NR_AI.hostedDataHunterBonusChance;

        if (!anchor.Spawned || !Rand.Chance(chance))
            return;

        IntVec3 cell = anchor is Building building && building.def.hasInteractionCell ? building.InteractionCell : anchor.Position;
        Thing data = ThingMaker.MakeThing(NetrunDefOf.USH_Cyberdata);
        GenPlace.TryPlaceThing(data, cell, anchor.Map, ThingPlaceMode.Near);
        Messages.Message("NR_CyberpodBonusData".Translate(hacker.LabelShort), new LookTargets(data), MessageTypeDefOf.PositiveEvent, historical: false);
    }
}
