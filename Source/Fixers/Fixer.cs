using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

public enum FixerSpecialty
{
    Broker,
    Cleaner,
    Fence,
}

public enum FixerTrait
{
    Greedy,
    Reliable,
    Rat,
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
    public List<ThingDef> Stock = [];
    /// <summary>Held by the player: stays through the next rotations.</summary>
    public bool Held;
    public int JoinedTick;

    private static FixerSettingsDef Settings => NetrunDefOf.NR_Fixers;

    public bool Pinned => Reputation >= Settings.pinnedReputation;
    public bool TraitKnown => TraitRevealed || Reputation >= Settings.traitRevealReputation;
    public bool HasShop => Specialty == FixerSpecialty.Fence;
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

            float factor = 1f - Mathf.Min(discount, Settings.maxDiscount);
            if (Trait == FixerTrait.Greedy)
                factor *= Settings.greedyPriceFactor;
            // A watched "night market" recording: the colony knows how to haggle for a few days.
            if (UniqueBraindanceManager.Get()?.MarketDiscountActive == true)
                factor *= 1f - NetrunDefOf.NR_UniqueBraindance.marketDiscount;

            return factor;
        }
    }

    /// <summary>Calls never get the Cleaner discount.</summary>
    public int EffectiveCallCost => Mathf.CeilToInt(CallCost * (Trait == FixerTrait.Greedy ? Settings.greedyPriceFactor : 1f));

    public float HeatPerCyberdata => Settings.heatPerCyberdata * ScrubLevelFactor * NetrunMod.Settings.ScrubMultiplier / PriceFactor;
    public float HeatPerAdvancedCyberdata => Settings.heatPerAdvancedCyberdata * ScrubLevelFactor * NetrunMod.Settings.ScrubMultiplier / PriceFactor;

    /// <summary>The scrub gets less effective as the colony's trace grows.</summary>
    private static float ScrubLevelFactor => LevelFactor(HeatTracker.Get()?.Level ?? HeatLevel.Shadow);

    private static float LevelFactor(HeatLevel level)
    {
        List<float> factors = Settings.scrubFactorByLevel;
        return factors.NullOrEmpty() ? 1f : factors[Mathf.Clamp((int)level, 0, factors.Count - 1)];
    }

    /// <summary>Heat one cyberdata (or one advanced) removes when the Heat stands at <paramref name="heat"/>.</summary>
    public float HeatPerUnitAt(float heat, bool advanced)
        => (advanced ? Settings.heatPerAdvancedCyberdata : Settings.heatPerCyberdata) * LevelFactor(HeatLevelUtility.LevelOf(heat))
           * NetrunMod.Settings.ScrubMultiplier / PriceFactor;

    public int PriceOf(ThingDef def) => Mathf.CeilToInt(def.BaseMarketValue * Settings.shopMarkup * PriceFactor);

    public void GainReputation(float amount)
    {
        bool wasPinned = Pinned;
        Reputation = Mathf.Clamp(Reputation + amount, 0f, MaxReputation);

        if (!wasPinned && Pinned)
            Messages.Message("NR_FixerPinned".Translate(Name), MessageTypeDefOf.PositiveEvent, historical: false);
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
        Scribe_Values.Look(ref CallCost, "callCost");
        Scribe_Values.Look(ref ChannelOpenUntilTick, "channelOpenUntilTick");
        Scribe_Values.Look(ref SilentUntilTick, "silentUntilTick");
        Scribe_Values.Look(ref TraitRevealed, "traitRevealed");
        Scribe_Collections.Look(ref Stock, "stock", LookMode.Def);
        Scribe_Values.Look(ref FreeScrubs, "freeScrubs");
        Scribe_Values.Look(ref Held, "held");
        Scribe_Values.Look(ref JoinedTick, "joinedTick", -999999);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            Stock ??= [];
            Stock.RemoveAll(d => d == null);
        }
    }
}
