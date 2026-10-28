using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

public enum FixerSpecialty
{
    Broker,
    Cleaner,
    Fence,
    /// <summary>An imperial client (Royalty): pays less silver, but adds royal favor.</summary>
    Imperial,
}

// New traits are appended so old saves keep their values.
public enum FixerTrait
{
    Greedy,
    Reliable,
    Rat,
    Paranoid,
    Gambler,
    Loyal,
    Veteran,
    Collector,
    Hothead,
}

public class Fixer : IExposable
{
    private const float MaxReputation = 100f;

    public int Id;
    public string Name;
    public FixerSpecialty Specialty;
    public FixerTrait Trait;
    public float Reputation;
    public int CallCost;
    public int ChannelOpenUntilTick;
    public int SilentUntilTick;
    public bool TraitRevealed;
    /// <summary>Free trace scrubs owed by this fixer (a cyberspace find).</summary>
    public int FreeScrubs;
    /// <summary>When the owed free scrubs burn out (-1: never; only saves from before T1.10, the migration sets it).</summary>
    public int FreeScrubUntilTick = -1;

    /// <summary>Free scrubs still usable: they all burn out together, freeScrubDays after the last one was granted.</summary>
    public int FreeScrubsLeft => FreeScrubUntilTick >= 0 && Find.TickManager.TicksGame >= FreeScrubUntilTick ? 0 : FreeScrubs;

    public void GrantFreeScrub()
    {
        FreeScrubs = FreeScrubsLeft + 1;
        FreeScrubUntilTick = Find.TickManager.TicksGame + (int)(NetrunDefOf.NR_Cyberspace.freeScrubDays * GenDate.TicksPerDay);
    }

    public string FreeScrubTip => "NR_FixerFreeScrubTip".Translate(NetrunDefOf.NR_Cyberspace.freeScrubHeat.ToString("0"),
        FreeScrubUntilTick < 0 ? "-" : (FreeScrubUntilTick - Find.TickManager.TicksGame).ToStringTicksToPeriod());
    public List<ThingDef> Stock = [];
    /// <summary>Held by the player: stays through the next rotations.</summary>
    public bool Held;
    public int JoinedTick;
    /// <summary>A Siren AI talked this fixer round: 20% off everything for good.</summary>
    public bool SirenDiscount;
    /// <summary>When this Cleaner last did an instant scrub (the cooldown counts from it).</summary>
    public int LastScrubTick = -999999;

    private static FixerSettingsDef Settings => NetrunDefOf.NR_Fixers;

    /// <summary>-1 unknown (an old save: decided by reputation on load), 0 not pinned, 1 pinned.</summary>
    private int _pinned = -1;

    /// <summary>Pinned: enough reputation and a free place (at most maxPinned at once).</summary>
    public bool Pinned => _pinned == 1;
    /// <summary>Reputation is there, but all pinned places are taken.</summary>
    public bool WaitsForPin => _pinned != 1 && Reputation >= Settings.pinnedReputation;
    public bool TraitKnown => TraitRevealed || Reputation >= Settings.traitRevealReputation;
    public bool HasShop => Specialty == FixerSpecialty.Fence;
    public bool IsCleaner => Specialty == FixerSpecialty.Cleaner;
    public bool IsNew => Find.TickManager.TicksGame - JoinedTick < Settings.newcomerDays * GenDate.TicksPerDay;

    public bool IsSilent => Find.TickManager.TicksGame < SilentUntilTick;
    public bool ChannelOpen => Find.TickManager.TicksGame < ChannelOpenUntilTick;
    public int ChannelTicksLeft => Mathf.Max(0, ChannelOpenUntilTick - Find.TickManager.TicksGame);

    /// <summary>Multiplier applied to everything the fixer charges.</summary>
    public float PriceFactor
    {
        get
        {
            float discount = Reputation / MaxReputation * Settings.maxReputationDiscount;
            if (Specialty == FixerSpecialty.Cleaner)
                discount += Settings.cleanerDiscount;
            if (Trait == FixerTrait.Loyal && Pinned)
                discount += Settings.loyalPinnedDiscount;

            float factor = 1f - Mathf.Min(discount, Settings.maxDiscount);
            if (Trait == FixerTrait.Greedy)
                factor *= Settings.greedyPriceFactor;
            else if (Trait == FixerTrait.Rat)
                factor *= Settings.ratPriceFactor;
            // A watched "night market" recording: the colony knows how to haggle for a few days.
            if (UniqueBraindanceManager.Get()?.MarketDiscountActive == true)
                factor *= 1f - NetrunDefOf.NR_UniqueBraindance.marketDiscount;
            if (SirenDiscount)
                factor *= 0.8f;

            return factor;
        }
    }

    /// <summary>Calls never get the Cleaner discount.</summary>
    public int EffectiveCallCost => Mathf.CeilToInt(CallCost * Trait switch
    {
        FixerTrait.Greedy => Settings.greedyPriceFactor,
        FixerTrait.Rat => Settings.ratPriceFactor,
        FixerTrait.Veteran => Settings.veteranCallFactor,
        _ => 1f,
    });

    /// <summary>A veteran knows which logs to burn: every cyberdata clears more.</summary>
    private float TraitScrubFactor => Trait == FixerTrait.Veteran ? Settings.veteranScrubFactor : 1f;

    /// <summary>Why the fixer will not pick up a call now (null when he will), beyond being silent.</summary>
    public string CallRefusal
    {
        get
        {
            if (Trait == FixerTrait.Paranoid && (HeatTracker.Get()?.Level ?? HeatLevel.Shadow) >= HeatLevel.Tracked)
                return "NR_FixerRefuseParanoid".Translate(Name, HeatLevel.Tracked.Label());
            if (Specialty == FixerSpecialty.Imperial && !ImperialClients.EmpireFriendly)
                return "NR_FixerRefuseImperial".Translate(Name);
            if (Trait == FixerTrait.Loyal && FixerManager.Get()?.Fixers.FirstOrDefault(f => f != this && f.Pinned && f.Specialty == Specialty) is { } rival)
                return "NR_FixerRefuseLoyal".Translate(Name, rival.Name);
            return null;
        }
    }

    public float HeatPerCyberdata => HeatPerUnitAt(HeatTracker.Get()?.Heat ?? 0f, false);
    public float HeatPerAdvancedCyberdata => HeatPerUnitAt(HeatTracker.Get()?.Heat ?? 0f, true);

    public int ScrubCooldownTicksLeft => Mathf.Max(0, LastScrubTick + Settings.cleanerScrubCooldownDays * GenDate.TicksPerDay - Find.TickManager.TicksGame);

    /// <summary>An instant scrub: only a Cleaner does it, once per cooldown.</summary>
    public AcceptanceReport CanScrubNow
        => !IsCleaner ? "NR_ScrubCleanerOnly".Translate()
            : ScrubCooldownTicksLeft > 0 ? "NR_ScrubCooldown".Translate(Name, ScrubCooldownTicksLeft.ToStringTicksToPeriod())
            : (AcceptanceReport)true;

    /// <summary>Most Heat one instant scrub takes off at the current level.</summary>
    public static float ScrubCap => (HeatTracker.Get()?.Level ?? HeatLevel.Shadow) >= HeatLevel.RedZone ? Settings.cleanerScrubMaxHeatRedZone : Settings.cleanerScrubMaxHeat;

    /// <summary>Cyberdata for one slow cooling deal: double in the Red Zone, except at a Cleaner.</summary>
    public int CoolingCost => Mathf.CeilToInt(Settings.coolingCost
        * (!IsCleaner && (HeatTracker.Get()?.Level ?? HeatLevel.Shadow) >= HeatLevel.RedZone ? Settings.redZoneCoolingPriceFactor : 1f));

    private static float LevelFactor(HeatLevel level)
    {
        List<float> factors = Settings.scrubFactorByLevel;
        return factors.NullOrEmpty() ? 1f : factors[Mathf.Clamp((int)level, 0, factors.Count - 1)];
    }

    /// <summary>
    /// Heat one cyberdata (or one advanced) removes when the Heat stands at <paramref name="heat"/>. Reputation and
    /// discounts do not change it (T1.2); the Red Zone doubles the price.
    /// </summary>
    public float HeatPerUnitAt(float heat, bool advanced)
    {
        HeatLevel level = HeatLevelUtility.LevelOf(heat);
        return (advanced ? Settings.heatPerAdvancedCyberdata : Settings.heatPerCyberdata) * LevelFactor(level)
               * TraitScrubFactor * NetrunMod.Settings.ScrubMultiplier / (level >= HeatLevel.RedZone ? Settings.redZoneScrubPriceFactor : 1f);
    }

    /// <summary>How many of the thing one purchase brings: a pack of cyberdata, one of anything else.</summary>
    public static int ShopCount(ThingDef def) => def == NetrunDefOf.USH_Cyberdata && Settings.cyberdataBundle > 0 ? Settings.cyberdataBundle : 1;

    public int PriceOf(ThingDef def) => Mathf.CeilToInt(def.BaseMarketValue * ShopCount(def) * Settings.shopMarkup * PriceFactor
                                                       * (Stock.Contains(def) ? 1f : Settings.blackMarketMarkup));

    /// <summary>What this fence pays for an AI fragment: from the minimum up to the maximum as reputation grows.</summary>
    public int ShardBuyPrice => Mathf.RoundToInt(Mathf.Lerp(Settings.shardBuyMin, Settings.shardBuyMax, Reputation / MaxReputation));

    public void GainReputation(float amount)
    {
        bool wasPinned = Pinned;
        if (amount > 0f && Trait == FixerTrait.Loyal)
            amount *= Settings.loyalReputationFactor;
        Reputation = Mathf.Clamp(Reputation + amount, 0f, MaxReputation);
        UpdatePin();

        if (!wasPinned && Pinned)
            Messages.Message("NR_FixerPinned".Translate(Name), MessageTypeDefOf.PositiveEvent, historical: false);
    }

    /// <summary>Pinned once reputation reaches the mark and a place is free; unpinned when it falls below.</summary>
    public void UpdatePin()
    {
        if (Reputation < Settings.pinnedReputation)
            _pinned = 0;
        else if (_pinned != 1)
        {
            int taken = FixerManager.Get()?.Fixers.Count(f => f != this && f.Pinned) ?? 0;
            _pinned = taken < Settings.maxPinned ? 1 : 0;
        }
    }

    /// <summary>Called after every deal. A rat sometimes sells the colony out.</summary>
    public void AfterDeal()
    {
        if (Trait != FixerTrait.Rat || !Rand.Chance(Settings.ratLeakChance))
            return;

        TraitRevealed = true;
        string text = "NR_FixerRatLeak".Translate(Name);
        HeatTracker.Get()?.AddHeat(Settings.ratLeakHeat, text, HeatSource.Event);
        Messages.Message(text, MessageTypeDefOf.NegativeEvent, historical: true);
    }

    public string SpecialtyLabel => ("NR_FixerSpecialty_" + Specialty).Translate();
    public string SpecialtyDescription => ("NR_FixerSpecialtyDesc_" + Specialty).Translate();
    public string TraitLabel => TraitKnown ? ("NR_FixerTrait_" + Trait).Translate() : "NR_FixerTraitUnknown".Translate();
    public string TraitDescription => TraitKnown ? ("NR_FixerTraitDesc_" + Trait).Translate() : "NR_FixerTraitUnknownDesc".Translate(Settings.traitRevealReputation.ToString("0"));

    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id");
        Scribe_Values.Look(ref Name, "name");
        Scribe_Values.Look(ref Specialty, "specialty");
        Scribe_Values.Look(ref Trait, "trait");
        Scribe_Values.Look(ref Reputation, "reputation");
        Scribe_Values.Look(ref SirenDiscount, "sirenDiscount");
        Scribe_Values.Look(ref CallCost, "callCost");
        Scribe_Values.Look(ref ChannelOpenUntilTick, "channelOpenUntilTick");
        Scribe_Values.Look(ref SilentUntilTick, "silentUntilTick");
        Scribe_Values.Look(ref TraitRevealed, "traitRevealed");
        Scribe_Collections.Look(ref Stock, "stock", LookMode.Def);
        Scribe_Values.Look(ref FreeScrubs, "freeScrubs");
        Scribe_Values.Look(ref FreeScrubUntilTick, "freeScrubUntilTick", -1);
        Scribe_Values.Look(ref Held, "held");
        Scribe_Values.Look(ref _pinned, "pinned", -1);
        Scribe_Values.Look(ref JoinedTick, "joinedTick", -999999);
        Scribe_Values.Look(ref LastScrubTick, "lastScrubTick", -999999);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (_pinned < 0)
                _pinned = Reputation >= Settings.pinnedReputation ? 1 : 0;
            // Fixers from older saves came without a call price and picked up for free.
            if (CallCost <= 0)
                CallCost = Settings.callCost.RandomInRange;
            Stock ??= [];
            Stock.RemoveAll(d => d == null);
        }
    }
}
