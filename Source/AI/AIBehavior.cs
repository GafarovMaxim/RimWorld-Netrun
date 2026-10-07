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
        AIPersonality.Doc => (ThingDefOf.MedicineIndustrial, 1),
        AIPersonality.Archivist => (NetrunDefOf.USH_Cyberdata, 2),
        AIPersonality.Echo => (NetrunDefOf.NR_BraindanceChip, 1),
        _ => null,
    };

    /// <summary>What a Seed can be fed, and what each kind of food grows it into. Silence grows a Ghost.</summary>
    public static List<(ThingDef thing, int count, AIPersonality grows)> SeedFoods =>
    [
        (NetrunDefOf.USH_Cyberdata, 2, AIPersonality.DataHunter),
        (NetrunDefOf.NR_BraindanceChip, 1, AIPersonality.Muse),
        (ThingDefOf.ComponentSpacer, 1, AIPersonality.Predator),
    ];

    public static string FoodText(AIPersonality personality)
    {
        if (Food(personality) is { } food)
            return "NR_AIFoodItem".Translate(food.count, food.thing.label);
        return ("NR_AIFoodSpecial_" + personality).Translate();
    }

    public static string ServiceLabel(AIPersonality personality) => ("NR_AIService_" + personality).Translate();
    public static string ServiceDescription(AIPersonality personality) => ("NR_AIServiceDesc_" + personality).Translate()
        + "\n\n" + "NR_AIServiceCost".Translate(Settings.serviceTrustCost.ToString("0"), Settings.serviceHungerCost.ToString("0"));

    public static AcceptanceReport CanUseService(NetAI ai)
    {
        // A host gets the passenger hediff and a pod gets the coprocessor bonus; services need the server's uplink.
        if (!ai.InMainServer)
            return "NR_AIServiceOnlyInServer".Translate();
        if (!ai.Powered)
            return "NR_AIAsleep".Translate();
        if (ai.Isolated)
            return "NR_AIIsolatedNoService".Translate();
        if (ai.Trust < Settings.minTrustForService)
            return "NR_AIRefuses".Translate(ai.Name);
        if (!ai.ServiceReady)
            return "NR_AICooldown".Translate(ai.ServiceTicksLeft.ToStringTicksToPeriod());
        if (AITraitUtility.ServiceRefusal(ai, ai.Server?.Map) is { } refusal)
            return refusal;
        return true;
    }

    public static AcceptanceReport CanFeed(NetAI ai, Map map)
    {
        if (!ai.Housed)
            return "NR_AIAsleep".Translate();
        // Either-or: a host gets the passenger hediff, but feeding needs the AI back in a server.
        if (ai.InHost)
            return "NR_AIHostNoFeed".Translate();
        if (ai.Personality == AIPersonality.Seed)
            return SeedFoods.Any(f => ColonyGoods.Count(map, f.thing) >= f.count) ? true : "NR_AISeedNoFood".Translate();
        if (Food(ai.Personality) is not { } food)
            return ("NR_AIFoodSpecial_" + ai.Personality).Translate();
        if (ColonyGoods.Count(map, food.thing) < food.count)
            return "NR_AINoFood".Translate(food.count, food.thing.label);
        return true;
    }

    public static void Feed(NetAI ai, Map map)
    {
        if (ai.Personality == AIPersonality.Seed)
        {
            OpenSeedMenu(ai, map);
            return;
        }

        if (!CanFeed(ai, map).Accepted || Food(ai.Personality) is not { } food)
            return;

        if (AIDelivery.NeedsCarrying(ai))
        {
            AIDelivery.OpenCarrierMenu(ai, map, AIDeliveryKind.Feed, food.thing, food.count, ai.Personality);
            return;
        }

        ColonyGoods.TryTake(map, food.thing, food.count);
        FedDelivered(ai, ai.Personality);
    }

    /// <summary>The food reached the AI (carried to its server, or taken from storage by its host).</summary>
    public static void FedDelivered(NetAI ai, AIPersonality seedGrows)
    {
        ai.AddHunger(-Settings.feedHungerRelief);
        ai.AddTrust(Settings.feedTrust * AITraitUtility.FeedTrustFactor(ai));
        Messages.Message("NR_AIFed".Translate(ai.Name), MessageTypeDefOf.PositiveEvent, historical: false);

        if (ai.Personality == AIPersonality.Seed)
            AIManager.Get()?.GrowSeed(ai, seedGrows);
    }

    /// <summary>A Seed is fed by choice: what it eats decides what it grows into.</summary>
    private static void OpenSeedMenu(NetAI ai, Map map)
    {
        var options = new List<FloatMenuOption>();
        foreach (var food in SeedFoods)
        {
            string label = "NR_AISeedFeedOption".Translate(food.count, food.thing.label, food.grows.Label());
            if (ColonyGoods.Count(map, food.thing) < food.count)
            {
                options.Add(new FloatMenuOption(label + " (" + "NR_AINoFood".Translate(food.count, food.thing.label) + ")", null));
                continue;
            }

            var captured = food;
            options.Add(new FloatMenuOption(label, () =>
            {
                if (AIDelivery.NeedsCarrying(ai))
                {
                    AIDelivery.OpenCarrierMenu(ai, map, AIDeliveryKind.Feed, captured.thing, captured.count, captured.grows);
                    return;
                }
                if (ColonyGoods.TryTake(map, captured.thing, captured.count))
                    FedDelivered(ai, captured.grows);
            }));
        }
        Find.WindowStack.Add(new FloatMenu(options));
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
        else
            ai.AddHunger(Settings.serviceHungerCost);
        ai.AddTrust(-Settings.serviceTrustCost);

        float power = AITraitUtility.ServicePower(ai, map);
        if (ai.Has(AITrait.Glitchy))
        {
            if (Rand.Chance(Settings.glitchyFailChance))
            {
                Messages.Message("NR_AIGlitchFail".Translate(ai.Name), MessageTypeDefOf.NegativeEvent, historical: true);
                return;
            }
            if (Rand.Chance(Settings.glitchyCritChance))
            {
                power *= 2f;
                Messages.Message("NR_AIGlitchCrit".Translate(ai.Name), MessageTypeDefOf.PositiveEvent, historical: false);
            }
        }
        Perform(ai, ai.Personality, map, power);
    }

    /// <summary>A free use of the AI's service, without cooldown: used by random events.</summary>
    public static void PerformOnItsOwn(NetAI ai, Map map)
    {
        if (map != null)
            Perform(ai, ai.Personality, map);
    }

    /// <param name="power">Service strength from the AI's traits: scales every number the service gives.</param>
    private static void Perform(NetAI ai, AIPersonality personality, Map map, float power = 1f)
    {
        switch (personality)
        {
            case AIPersonality.DataHunter:
            {
                Thing data = ThingMaker.MakeThing(NetrunDefOf.USH_Cyberdata);
                data.stackCount = Mathf.Max(1, Mathf.RoundToInt(Settings.dataHunterData.RandomInRange * power));
                Drop(map, ai, data);
                HeatTracker.Get()?.AddHeat(Settings.dataHunterHeat, "NR_LogAIService".Translate(ai.Name), HeatSource.AI);
                Report("NR_AIDidDataHunter", ai, data.stackCount);
                break;
            }
            case AIPersonality.Ghost:
                HeatTracker.Get()?.ReduceHeat(Settings.ghostHeatScrub * power, "NR_LogAIService".Translate(ai.Name), HeatSource.AI);
                Report("NR_AIDidGhost", ai, (Settings.ghostHeatScrub * power).ToString("0"));
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
                foreach (NetAI other in AIManager.Get().AIs.Where(a => a != ai && a.Housed))
                    other.AddTrust(Settings.wardenTrust * power);
                Report("NR_AIDidWarden", ai, (Settings.wardenTrust * power).ToString("0"));
                break;
            }
            case AIPersonality.Oracle:
            {
                foreach (Fixer fixer in FixerManager.Get()?.Fixers ?? [])
                    fixer.TraitRevealed = true;
                foreach (Contract contract in ContractManager.Get()?.Active ?? [])
                    contract.DeadlineTick += Mathf.RoundToInt(Settings.oracleDeadlineDays * power * GenDate.TicksPerDay);
                Report("NR_AIDidOracle", ai, (Settings.oracleDeadlineDays * power).ToString("0.#"));
                break;
            }
            case AIPersonality.Forge:
            {
                ResearchProjectDef project = Find.ResearchManager.GetProject();
                if (project != null)
                {
                    Find.ResearchManager.AddProgress(project, Settings.forgeResearch * power, null);
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
                    fixer.GainReputation(Settings.sirenReputation * power);
                Report("NR_AIDidSiren", ai, (Settings.sirenReputation * power).ToString("0"));
                break;
            }
            case AIPersonality.Trickster:
                if (Rand.Chance(Settings.tricksterBadChance))
                {
                    HeatTracker.Get()?.AddHeat(Settings.tricksterHeat, "NR_LogAIService".Translate(ai.Name), HeatSource.AI);
                    Report("NR_AIDidTricksterBad", ai, Settings.tricksterHeat.ToString("0"));
                }
                else
                {
                    Perform(ai, AIPersonalityUtility.Borrowable.RandomElement(), map, power);
                }
                break;
            case AIPersonality.Doc:
            {
                int healed = 0;
                HediffDef[] iceHarm = [USH_HE.USH_DefOf.USH_CyberspaceNausea, USH_HE.USH_DefOf.USH_CyberspaceLockOut, USH_HE.USH_DefOf.USH_CyberspaceComa];
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    foreach (Hediff hediff in pawn.health.hediffSet.hediffs.Where(h => iceHarm.Contains(h.def)).ToList())
                    {
                        pawn.health.RemoveHediff(hediff);
                        healed++;
                    }
                    Hediff netsick = pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick);
                    if (netsick != null)
                        netsick.Severity -= Settings.docNetsickRelief * power;
                }
                Report("NR_AIDidDoc", ai, healed);
                break;
            }
            case AIPersonality.Archivist:
            {
                Pawn pawn = map.mapPawns.FreeColonistsSpawned.Where(p => p.skills != null).RandomElementWithFallback();
                SkillRecord skill = pawn?.skills.skills.Where(s => !s.TotallyDisabled).MaxByWithFallback(s => s.Level);
                if (skill != null)
                {
                    skill.Learn(Settings.archivistXp * power, direct: true);
                    Report("NR_AIDidArchivist", ai, $"{pawn.LabelShort}, {skill.def.label}");
                }
                else
                {
                    Report("NR_AIDidMuseNoOne", ai, "");
                }
                break;
            }
            case AIPersonality.Seed:
                HeatTracker.Get()?.ReduceHeat(Settings.seedHeatScrub * power, "NR_LogAIService".Translate(ai.Name), HeatSource.AI);
                Report("NR_AIDidSeed", ai, (Settings.seedHeatScrub * power).ToString("0"));
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
        Thing anchor = ai.Anchor;
        IntVec3 cell = anchor?.Spawned == true ? anchor.Position : DropCellFinder.TradeDropSpot(map);
        GenPlace.TryPlaceThing(thing, cell, anchor?.Map ?? map, ThingPlaceMode.Near);
    }

    private static void Report(string key, NetAI ai, NamedArgument arg)
        => Messages.Message(key.Translate(ai.Name, arg), MessageTypeDefOf.PositiveEvent, historical: true);
}
