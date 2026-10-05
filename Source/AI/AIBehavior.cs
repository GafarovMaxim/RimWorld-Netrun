using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>What each personality eats and what its service does.</summary>
public static class AIBehavior
{
    private static AISettingsDef Settings => NetrunDefOf.NR_AI;

    /// <summary>Item and amount one feeding takes; null for personalities fed differently (Ghost, Trickster).</summary>
    public static (ThingDef thing, int count)? Food(AIPersonality personality) => personality switch
    {
        AIPersonality.DataHunter => (NetrunDefOf.USH_Cyberdata, 3),
        AIPersonality.Muse => (NetrunDefOf.NR_BraindanceChip, 1),
        AIPersonality.Predator => (ThingDefOf.ComponentSpacer, 1),
        AIPersonality.Warden => (ThingDefOf.ComponentIndustrial, 2),
        AIPersonality.Oracle => (NetrunDefOf.USH_AdvancedCyberdata, 1),
        AIPersonality.Forge => (ThingDefOf.ComponentIndustrial, 3),
        AIPersonality.Siren => (ThingDefOf.Silver, 100),
        AIPersonality.Echo => (NetrunDefOf.NR_BraindanceChip, 1),
        _ => null,
    };

    public static string FoodText(AIPersonality personality)
    {
        if (Food(personality) is { } food)
            return "NR_AIFoodItem".Translate(food.count, food.thing.label);
        return ("NR_AIFoodSpecial_" + personality).Translate();
    }

    public static string ServiceLabel(AIPersonality personality) => ("NR_AIService_" + personality).Translate();
    public static string ServiceDescription(AIPersonality personality) => ("NR_AIServiceDesc_" + personality).Translate();

    public static AcceptanceReport CanUseService(NetAI ai)
    {
        if (!ai.Powered)
            return "NR_AIAsleep".Translate();
        if (ai.Isolated)
            return "NR_AIIsolatedNoService".Translate();
        if (ai.Trust < Settings.minTrustForService)
            return "NR_AIRefuses".Translate(ai.Name);
        if (!ai.ServiceReady)
            return "NR_AICooldown".Translate(ai.ServiceTicksLeft.ToStringTicksToPeriod());
        return true;
    }

    public static AcceptanceReport CanFeed(NetAI ai, Map map)
    {
        if (!ai.InServer)
            return "NR_AIAsleep".Translate();
        if (Food(ai.Personality) is not { } food)
            return ("NR_AIFoodSpecial_" + ai.Personality).Translate();
        if (ColonyGoods.Count(map, food.thing) < food.count)
            return "NR_AINoFood".Translate(food.count, food.thing.label);
        return true;
    }

    public static void Feed(NetAI ai, Map map)
    {
        if (!CanFeed(ai, map).Accepted || Food(ai.Personality) is not { } food)
            return;

        ColonyGoods.TryTake(map, food.thing, food.count);
        ai.AddHunger(-Settings.feedHungerRelief);
        ai.AddTrust(Settings.feedTrust);
        Messages.Message("NR_AIFed".Translate(ai.Name), MessageTypeDefOf.PositiveEvent, historical: false);
    }

    public static void UseService(NetAI ai, Map map)
    {
        if (!CanUseService(ai).Accepted)
            return;

        ai.ServiceReadyTick = Find.TickManager.TicksGame + Mathf.RoundToInt(Settings.serviceCooldownDays * GenDate.TicksPerDay);

        // A Trickster answering a request for attention.
        if (ai.Request == AIRequestKind.Attention)
            AIManager.Get()?.CompleteRequest(ai);
        if (ai.Personality == AIPersonality.Trickster)
            ai.AddHunger(-Settings.feedHungerRelief);

        Perform(ai, ai.Personality, map);
    }

    private static void Perform(NetAI ai, AIPersonality personality, Map map)
    {
        switch (personality)
        {
            case AIPersonality.DataHunter:
            {
                Thing data = ThingMaker.MakeThing(NetrunDefOf.USH_Cyberdata);
                data.stackCount = Settings.dataHunterData.RandomInRange;
                Drop(map, ai, data);
                HeatTracker.Get()?.AddHeat(Settings.dataHunterHeat, "NR_LogAIService".Translate(ai.Name));
                Report("NR_AIDidDataHunter", ai, data.stackCount);
                break;
            }
            case AIPersonality.Ghost:
                HeatTracker.Get()?.ReduceHeat(Settings.ghostHeatScrub, "NR_LogAIService".Translate(ai.Name));
                Report("NR_AIDidGhost", ai, Settings.ghostHeatScrub.ToString("0"));
                break;
            case AIPersonality.Muse:
            {
                Pawn pawn = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed && p.mindState?.inspirationHandler?.Inspired == false).RandomElementWithFallback();
                InspirationDef inspiration = pawn == null ? null
                    : DefDatabase<InspirationDef>.AllDefs.Where(d => d.Worker.InspirationCanOccur(pawn)).RandomElementWithFallback();
                if (inspiration != null && pawn.mindState.inspirationHandler.TryStartInspiration(inspiration, "NR_AIInspired".Translate(ai.Name), sendLetter: true))
                    Report("NR_AIDidMuse", ai, pawn.LabelShort);
                else
                    Report("NR_AIDidMuseNoOne", ai, "");
                break;
            }
            case AIPersonality.Predator:
            {
                var hackers = map.mapPawns.FreeColonistsSpawned.Where(p => USH_HE.CyberUtils.IsHacker(p)).ToList();
                foreach (Pawn hacker in hackers)
                    hacker.health.AddHediff(NetrunDefOf.NR_PredatorBoost);
                Report(hackers.Count > 0 ? "NR_AIDidPredator" : "NR_AIDidPredatorNoOne", ai, hackers.Count);
                break;
            }
            case AIPersonality.Warden:
            {
                foreach (NetAI other in AIManager.Get().AIs.Where(a => a != ai && a.InServer))
                    other.AddTrust(Settings.wardenTrust);
                Report("NR_AIDidWarden", ai, Settings.wardenTrust.ToString("0"));
                break;
            }
            case AIPersonality.Oracle:
            {
                foreach (Fixer fixer in FixerManager.Get()?.Fixers ?? [])
                    fixer.TraitRevealed = true;
                foreach (Contract contract in ContractManager.Get()?.Active ?? [])
                    contract.DeadlineTick += Settings.oracleDeadlineDays * GenDate.TicksPerDay;
                Report("NR_AIDidOracle", ai, Settings.oracleDeadlineDays);
                break;
            }
            case AIPersonality.Forge:
            {
                ResearchProjectDef project = Find.ResearchManager.GetProject();
                if (project != null)
                {
                    Find.ResearchManager.AddProgress(project, Settings.forgeResearch, null);
                    Report("NR_AIDidForge", ai, project.LabelCap);
                }
                else
                {
                    Report("NR_AIDidForgeNoProject", ai, "");
                }
                break;
            }
            case AIPersonality.Siren:
            {
                foreach (Fixer fixer in FixerManager.Get()?.Fixers ?? [])
                    fixer.GainReputation(Settings.sirenReputation);
                Report("NR_AIDidSiren", ai, Settings.sirenReputation.ToString("0"));
                break;
            }
            case AIPersonality.Trickster:
                if (Rand.Chance(Settings.tricksterBadChance))
                {
                    HeatTracker.Get()?.AddHeat(Settings.tricksterHeat, "NR_LogAIService".Translate(ai.Name));
                    Report("NR_AIDidTricksterBad", ai, Settings.tricksterHeat.ToString("0"));
                }
                else
                {
                    AIPersonality borrowed = (AIPersonality)Rand.Range(0, (int)AIPersonality.Trickster);
                    Perform(ai, borrowed, map);
                }
                break;
            case AIPersonality.Echo:
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                    pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(NetrunDefOf.NR_EchoPresence);
                Report("NR_AIDidEcho", ai, "");
                break;
        }
    }

    private static void Drop(Map map, NetAI ai, Thing thing)
    {
        IntVec3 cell = ai.Server?.Spawned == true ? ai.Server.Position : DropCellFinder.TradeDropSpot(map);
        GenPlace.TryPlaceThing(thing, cell, ai.Server?.Map ?? map, ThingPlaceMode.Near);
    }

    private static void Report(string key, NetAI ai, NamedArgument arg)
        => Messages.Message(key.Translate(ai.Name, arg), MessageTypeDefOf.PositiveEvent, historical: true);
}
