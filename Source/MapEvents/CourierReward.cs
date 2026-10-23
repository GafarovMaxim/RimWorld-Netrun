using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>One payment the courier's fixer offers: a few stacks worth about the promised sum.</summary>
public class CourierRewardOption : IExposable
{
    public List<ThingDefCountClass> Items = [];

    public float Value => Items.Sum(i => i.thingDef.BaseMarketValue * i.count);

    /// <summary>
    /// One line in the letter (its options do not wrap; Maxim, 2026-10-23: three exec chips ran off the window): kinds that
    /// share a name before brackets are said once, "Exec data: short circuit x1, zapcode x1".
    /// </summary>
    public string Label
    {
        get
        {
            var parts = Items.Select(i => System.Text.RegularExpressions.Regex.Match(i.thingDef.LabelCap.ToString(), @"^(.+?) \((.+)\)$")).ToList();
            if (Items.Count > 1 && parts.All(m => m.Success) && parts.Select(m => m.Groups[1].Value).Distinct().Count() == 1)
                return parts[0].Groups[1].Value + ": " + Items.Select((item, k) => parts[k].Groups[2].Value + " x" + item.count).ToCommaList();
            return Items.Select(i => i.thingDef.LabelCap + " x" + i.count).ToCommaList();
        }
    }

    public void ExposeData()
    {
        Scribe_Collections.Look(ref Items, "items", LookMode.Deep);
        Items ??= [];
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            Items.RemoveAll(i => i?.thingDef == null);
    }
}

/// <summary>
/// The courier's pay grows with the colony's threat points, like the hunters do, and the player picks how it comes:
/// silver always, plus two of gold, a prosthetic (any mod's), exec chips or cyberdata (Maxim, 2026-10-21).
/// </summary>
public static class CourierReward
{
    private static MapEventsSettingsDef Settings => NetrunDefOf.NR_MapEvents;

    public static int Value(Map map)
    {
        float points = map != null ? StorytellerUtility.DefaultThreatPointsNow(map) : 0f;
        float value = Mathf.Clamp(points * Settings.courierRewardPerPoint, Settings.courierSilver, Settings.courierRewardMax);
        return Mathf.RoundToInt(value / 10f) * 10;
    }

    private static List<ThingDef> _prosthetics;
    private static List<ThingDef> _execs;

    /// <summary>Bionic and prosthetic parts from every mod: tech body parts, not natural organs or archotech.</summary>
    private static List<ThingDef> Prosthetics => _prosthetics ??= DefDatabase<ThingDef>.AllDefs
        .Where(d => d.isTechHediff && d.techLevel < TechLevel.Archotech && d.tradeability != Tradeability.None && d.BaseMarketValue > 0f
                    && d.thingCategories?.Any(c => c.defName == "BodyPartsNatural") != true
                    && d.thingCategories?.Any(c => c.defName.StartsWith("BodyParts") || c.parent?.defName == "BodyParts") == true)
        .ToList();

    /// <summary>Hacking Expansion's exec chips and any mod's: items implanted into the exec data case.</summary>
    private static List<ThingDef> Execs => _execs ??= DefDatabase<ThingDef>.AllDefs
        .Where(d => d.BaseMarketValue > 0f && d.comps?.Any(c => c is CompProperties_Usable u && u.userMustHaveHediff?.defName == "USH_InstalledExecDataCase") == true)
        .ToList();

    /// <summary>Silver first, then two other kinds that can be paid at this value.</summary>
    public static List<CourierRewardOption> Roll(int value)
    {
        var options = new List<CourierRewardOption> { Of(ThingDefOf.Silver, value) };
        var others = new List<System.Func<CourierRewardOption>>
        {
            () => Of(ThingDefOf.Gold, value),
            () => Prosthetic(value),
            () => ExecChips(value),
            () => Of(NetrunDefOf.USH_AdvancedCyberdata, value),
        };
        foreach (System.Func<CourierRewardOption> make in others.InRandomOrder())
        {
            if (options.Count >= 3)
                break;
            if (make() is { Items.Count: > 0 } option)
                options.Add(option);
        }
        return options;
    }

    private static CourierRewardOption Of(ThingDef def, float value)
    {
        var option = new CourierRewardOption();
        if (def != null && def.BaseMarketValue > 0f)
            option.Items.Add(new ThingDefCountClass(def, Mathf.Max(1, Mathf.RoundToInt(value / def.BaseMarketValue))));
        return option;
    }

    /// <summary>One part close to the value, the rest in silver.</summary>
    private static CourierRewardOption Prosthetic(int value)
    {
        var option = new CourierRewardOption();
        List<ThingDef> fit = Prosthetics.Where(d => d.BaseMarketValue >= value * 0.5f && d.BaseMarketValue <= value * 1.25f).ToList();
        ThingDef part = fit.Count > 0 ? fit.RandomElement()
            : Prosthetics.Where(d => d.BaseMarketValue >= value * 0.25f && d.BaseMarketValue <= value).MaxByWithFallback(d => d.BaseMarketValue);
        if (part == null)
            return option;
        option.Items.Add(new ThingDefCountClass(part, 1));
        int rest = Mathf.RoundToInt((value - part.BaseMarketValue) / 10f) * 10;
        if (rest > 0)
            option.Items.Add(new ThingDefCountClass(ThingDefOf.Silver, rest));
        return option;
    }

    /// <summary>Up to three kinds of exec chips sharing the value.</summary>
    private static CourierRewardOption ExecChips(int value)
    {
        var option = new CourierRewardOption();
        List<ThingDef> kinds = Execs.Where(d => d.BaseMarketValue <= value).InRandomOrder().Take(3).ToList();
        foreach (ThingDef def in kinds)
            option.Items.Add(new ThingDefCountClass(def, Mathf.Max(1, Mathf.RoundToInt(value / (float)kinds.Count / def.BaseMarketValue))));
        return option;
    }

    public static void Drop(CourierRewardOption option, Map map)
    {
        var things = new List<Thing>();
        foreach (ThingDefCountClass item in option.Items)
        {
            for (int left = item.count; left > 0;)
            {
                Thing thing = ThingMaker.MakeThing(item.thingDef, GenStuff.DefaultStuffFor(item.thingDef));
                thing.stackCount = Mathf.Min(left, item.thingDef.stackLimit);
                thing.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good, ArtGenerationContext.Outsider);
                left -= thing.stackCount;
                things.Add(thing);
            }
        }
        DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, things, forbid: false);
    }
}

/// <summary>The job is done: the player picks one of the fixer's payments. It stays until picked.</summary>
public class ChoiceLetter_CourierReward : ChoiceLetter
{
    public Map Map;
    public List<CourierRewardOption> Options = [];

    public override bool CanDismissWithRightClick => false;

    public override IEnumerable<DiaOption> Choices
    {
        get
        {
            if (ArchivedOnly)
            {
                yield return Option_Close;
                yield break;
            }
            foreach (CourierRewardOption option in Options)
            {
                yield return new DiaOption("NR_CourierRewardOption".Translate(option.Label, option.Value.ToString("0")))
                {
                    action = () =>
                    {
                        Map map = Map != null && Find.Maps.Contains(Map) ? Map : Find.AnyPlayerHomeMap;
                        if (map != null)
                            CourierReward.Drop(option, map);
                        Find.LetterStack.RemoveLetter(this);
                    },
                    resolveTree = true,
                };
            }
            yield return Option_Postpone;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref Map, "map");
        Scribe_Collections.Look(ref Options, "options", LookMode.Deep);
        Options ??= [];
    }
}
