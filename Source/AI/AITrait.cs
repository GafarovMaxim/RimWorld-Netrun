using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>What makes one AI unlike another of its personality. Each trait helps and hurts; the order is saved.</summary>
public enum AITrait
{
    Curious,
    Loyal,
    Paranoid,
    Vain,
    Glitchy,
    Hoarder,
    Pacifist,
    NightOwl,
    Rebel,
    Nostalgic,
}

public static class AITraitUtility
{
    private static AISettingsDef Settings => NetrunDefOf.NR_AI;

    public static string Label(this AITrait trait) => ("NR_AITrait_" + trait).Translate();
    public static string Description(this AITrait trait) => ("NR_AITraitDesc_" + trait).Translate();

    /// <summary>0-2 traits on waking. A pacifist predator makes no sense, so it never rolls.</summary>
    public static List<AITrait> Roll(AIPersonality personality)
    {
        int count = Rand.Value < Settings.traitNoneChance ? 0 : Rand.Value < Settings.traitTwoChance ? 2 : 1;
        var pool = ((AITrait[])System.Enum.GetValues(typeof(AITrait)))
            .Where(t => !(t == AITrait.Pacifist && personality == AIPersonality.Predator)).ToList();
        return pool.InRandomOrder().Take(count).ToList();
    }

    /// <summary>The trait a Seed picks up from what it was fed most.</summary>
    public static AITrait FromSeedPath(AIPersonality grown) => grown switch
    {
        AIPersonality.DataHunter => AITrait.Curious,
        AIPersonality.Muse => AITrait.Nostalgic,
        AIPersonality.Predator => AITrait.Rebel,
        _ => AITrait.Paranoid,
    };

    /// <summary>Trust needed to see the first and the second trait.</summary>
    public static float RevealTrust(int index) => index == 0 ? Settings.traitRevealTrustFirst : Settings.traitRevealTrustSecond;

    public static bool Has(this NetAI ai, AITrait trait) => ai.Traits.Contains(trait);

    public static float HungerFactor(NetAI ai)
        => (ai.Has(AITrait.Loyal) ? Settings.loyalHungerFactor : 1f) * (ai.Has(AITrait.Hoarder) ? Settings.hoarderHungerFactor : 1f);

    public static float FeedTrustFactor(NetAI ai) => ai.Has(AITrait.Curious) ? Settings.curiousTrustFactor : 1f;
    public static float RequestIntervalFactor(NetAI ai) => ai.Has(AITrait.Curious) ? Settings.curiousRequestFactor : 1f;
    public static float FailTrustFactor(NetAI ai) => ai.Has(AITrait.Vain) ? Settings.vainFailFactor : 1f;
    public static float EscapeTrust(NetAI ai) => Settings.escapeTrust + (ai.Has(AITrait.Rebel) ? Settings.rebelEscapeTrust : 0f);

    public static bool HasChips(Map map)
        => map != null && (ColonyGoods.Count(map, NetrunDefOf.NR_BraindanceChip) > 0 || ColonyGoods.Count(map, NetrunDefOf.NR_BlackBraindanceChip) > 0);

    public static bool IsNight(Map map)
    {
        if (map == null)
            return false;
        int hour = GenLocalDate.HourOfDay(map);
        return hour >= 22 || hour < 6;
    }

    /// <summary>How strong the service is right now: every trait that touches it, multiplied.</summary>
    public static float ServicePower(NetAI ai, Map map)
    {
        float power = 1f;
        foreach (AITrait trait in ai.Traits)
        {
            power *= trait switch
            {
                AITrait.Loyal => Settings.loyalServiceFactor,
                AITrait.Vain => ai.Trust >= Settings.vainTrust ? Settings.vainServiceFactor : 1f,
                AITrait.Hoarder => ai.Hunger <= Settings.hoarderHunger ? Settings.hoarderServiceFactor : 1f,
                AITrait.Pacifist => Settings.pacifistServiceFactor,
                AITrait.NightOwl => IsNight(map) ? Settings.nightOwlNightFactor : Settings.nightOwlDayFactor,
                AITrait.Rebel => Settings.rebelServiceFactor,
                AITrait.Nostalgic => HasChips(map) ? Settings.nostalgicServiceFactor : 1f,
                _ => 1f,
            };
        }
        return power;
    }

    /// <summary>Why this AI will not serve now (null when it will).</summary>
    public static string ServiceRefusal(NetAI ai, Map map)
    {
        if (ai.Has(AITrait.Paranoid) && (HeatTracker.Get()?.Level ?? HeatLevel.Shadow) >= HeatLevel.Target)
            return "NR_AIRefuseParanoid".Translate(ai.Name, HeatLevel.Target.Label());
        if (ai.Has(AITrait.Pacifist) && map != null && GenHostility.AnyHostileActiveThreatToPlayer(map))
            return "NR_AIRefusePacifist".Translate(ai.Name);
        return null;
    }
}
