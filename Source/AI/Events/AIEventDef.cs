using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>Where the AI lives: server (S), coprocessor (K) or host pawn (P) in docs/AI_EVENTS.md.</summary>
public enum AIEventPlace
{
    Server,
    Coprocessor,
    Host,
}

/// <summary>
/// One AI event from docs/AI_EVENTS.md. The letter text is the description with {AI}, {PAWN} and {HACKER};
/// without options the effects apply at once, otherwise the player picks and silence picks ignoreOption.
/// </summary>
public class AIEventDef : Def
{
    /// <summary>Empty: a general event any personality can get.</summary>
    public List<AIPersonality> personalities = [];
    /// <summary>Empty: the main server only.</summary>
    public List<AIEventPlace> places = [];

    public float minTrust;
    public float maxTrust = 100f;
    public float minHunger;
    public float maxHunger = 100f;
    public int minSeedGrowth;
    public int maxSeedGrowth = 99;
    public float weight = 1f;

    public bool needsHacker;
    public bool needsPrisoner;
    public bool needsContract;
    public bool needsArt;

    /// <summary>Applied when the event fires, before any choice.</summary>
    public List<AIEventEffect> effects = [];
    public List<AIEventOption> options = [];
    /// <summary>Which option silence (timeout or dismissal) counts as; -1 means the last one.</summary>
    public int ignoreOption = -1;

    public bool General => personalities.NullOrEmpty();

    public bool PlaceAllowed(NetAI ai)
    {
        AIEventPlace place = ai.InHost ? AIEventPlace.Host : ai.InCoprocessor ? AIEventPlace.Coprocessor : AIEventPlace.Server;
        return places.NullOrEmpty() ? place == AIEventPlace.Server : places.Contains(place);
    }

    public bool CanFire(NetAI ai, Map map)
    {
        if (!General && !personalities.Contains(ai.Personality))
            return false;
        if (!PlaceAllowed(ai))
            return false;
        if (ai.Trust < minTrust || ai.Trust > maxTrust || ai.Hunger < minHunger || ai.Hunger > maxHunger)
            return false;
        if (ai.Personality == AIPersonality.Seed && (ai.SeedGrowth < minSeedGrowth || ai.SeedGrowth > maxSeedGrowth))
            return false;
        if (needsHacker && AIEventContext.BestHacker(map) == null)
            return false;
        if (needsPrisoner && !map.mapPawns.PrisonersOfColonySpawned.Any())
            return false;
        if (needsContract && ContractManager.Get()?.Active.Any() != true)
            return false;
        if (needsArt && AIEffect_SellArt.FindArt(map) == null)
            return false;
        return map.mapPawns.FreeColonistsSpawned.Any();
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
            yield return error;
        if (description.NullOrEmpty())
            yield return "AI event has no description (letter text)";
        if (options.Count > 3)
            yield return "AI event has more than 3 options";
    }
}

public class AIEventOption
{
    [MustTranslate] public string label;
    /// <summary>Shown as a message after the choice; same placeholders as the letter.</summary>
    [MustTranslate] public string result;
    public List<AIEventEffect> effects = [];
    /// <summary>Paid from colony storage; the option is greyed out without it.</summary>
    public List<ThingDefCountClass> cost = [];
    public bool needsHacker;
    /// <summary>Feeds the AI its usual food from storage (one portion).</summary>
    public bool needsFood;

    public AcceptanceReport Available(AIEventContext context)
    {
        if (needsHacker && context.Hacker == null)
            return "NR_AIEventNeedsHacker".Translate();
        if (needsFood && AIBehavior.Food(context.AI.Personality) is { } food && ColonyGoods.Count(context.Map, food.thing) < food.count)
            return "NR_AINoFood".Translate(food.count, food.thing.label);
        foreach (ThingDefCountClass c in cost)
        {
            if (ColonyGoods.Count(context.Map, c.thingDef) < c.count)
                return "NR_AINoFood".Translate(c.count, c.thingDef.label);
        }
        return true;
    }
}
