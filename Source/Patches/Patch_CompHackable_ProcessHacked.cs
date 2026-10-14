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
            HeatTracker heat = HeatTracker.Get();
            float before = heat?.Heat ?? 0f;
            heat?.AddHeat(HackHeatUtility.HeatForHack(__instance, hacker), reason, HeatSource.Hack);
            ShowHeatGain(__instance.parent, (heat?.Heat ?? 0f) - before);
            ContractManager.Get()?.NotifyHackCompleted();
            AIManager.Get()?.NotifyHack();
            NetsickManager.Get()?.NotifyHack(hacker);
            TryGiveCyberpodBonusData(hacker);
            TryDropAIFinds(__instance, hacker);
            TryStarterCrate(__instance, hacker);
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

        if (Rand.Chance(AIBalance.FragmentChance(fragmentChance)))
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
    /// The newcomer's starting stock (Maxim, 2026-10-14: at least 25 cyberdata to start with): Hacking Expansion's
    /// encrypted crate by the ancient cyberdeck gives only 3-6, so the first such crate the colony opens on a home map
    /// in its first days adds more. Once per game; the numbers are in Defs/MapEvents.xml.
    /// </summary>
    private static void TryStarterCrate(CompHackable comp, Pawn hacker)
    {
        Thing crate = comp.parent;
        MapEventsSettingsDef settings = NetrunDefOf.NR_MapEvents;
        NetworkGate gate = NetworkGate.Get();
        if (gate == null || gate.StarterCrateGiven || crate.def.defName != "USH_EncryptedCrate" || !crate.Spawned || !crate.Map.IsPlayerHome
            || Find.TickManager.TicksGame > settings.starterCrateDays * GenDate.TicksPerDay || settings.starterCrateCyberdata <= 0)
            return;
        gate.StarterCrateGiven = true;
        Thing data = ThingMaker.MakeThing(NetrunDefOf.USH_Cyberdata);
        data.stackCount = settings.starterCrateCyberdata;
        GenPlace.TryPlaceThing(data, crate.Position, crate.Map, ThingPlaceMode.Near);
        Messages.Message("NR_StarterCrate".Translate(hacker.LabelShort, settings.starterCrateCyberdata), new LookTargets(data), MessageTypeDefOf.PositiveEvent);
    }

    /// <summary>
    /// Extra cyberdata: from a cyberpod (more with an AI in a Netrun coprocessor) and from a hosted Data Hunter.
    /// </summary>
    /// <summary>A floating "+N Heat" over the hacked object, so the cost of a hack is seen where it happens.</summary>
    private static void ShowHeatGain(Thing target, float gain)
    {
        if (gain < 0.05f || target?.Spawned != true)
            return;
        MoteMaker.ThrowText(target.DrawPos + new UnityEngine.Vector3(0f, 0f, 0.6f), target.Map,
            "NR_HeatFloat".Translate(gain.ToString("0.#")), new UnityEngine.Color(1f, 0.55f, 0.25f), 3.5f);
    }

    private static void TryGiveCyberpodBonusData(Pawn hacker)
    {
        float chance = 0f;
        Thing anchor = hacker;

        if (hacker.TryGetHoldingCyberpod(out Building_Cyberpod pod) && pod.Spawned)
        {
            anchor = pod;
            chance += NetrunDefOf.NR_Heat.cyberpodBonusDataChance;
            chance += CyberpodUtility.BonusDataChance(CyberpodUtility.CoprocessorAI(pod));
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
