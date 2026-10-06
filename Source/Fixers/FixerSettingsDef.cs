using System.Collections.Generic;
using Verse;

namespace Netrun;

/// <summary>All fixer balance numbers in one place (Defs/Fixers.xml).</summary>
public class FixerSettingsDef : Def
{
    // Pool
    public int poolSize = 4;
    public IntRange rotationDays = new(7, 10);
    /// <summary>Unpinned fixers the player can hold through a rotation.</summary>
    public int maxHeld = 2;
    /// <summary>Silver paid per held fixer at every rotation; without it the hold lapses.</summary>
    public int holdCost = 100;
    /// <summary>After breaking ties a fixer does not come back for this many days.</summary>
    public int breakBanDays = 30;
    /// <summary>A fixer is marked "new" for this many days after joining the pool.</summary>
    public float newcomerDays = 2f;
    /// <summary>The pool keeps at least this many different specialties after a rotation.</summary>
    public int minSpecialties = 2;
    public float pinnedReputation = 60f;
    public float traitRevealReputation = 25f;

    // Contacting a fixer
    public IntRange callCost = new(50, 150);
    public int channelHours = 24;

    // Prices and discounts
    public float greedyPriceFactor = 1.25f;
    public float maxReputationDiscount = 0.5f;
    public float cleanerDiscount = 0.25f;
    public float maxDiscount = 0.75f;

    // Heat scrubbing
    public float heatPerCyberdata = 5f;
    public float heatPerAdvancedCyberdata = 15f;
    /// <summary>Scrub efficiency by Heat level (index = HeatLevel): the louder the colony, the dearer the scrub.</summary>
    public List<float> scrubFactorByLevel = [0.6f, 0.6f, 0.4f, 0.3f, 0.2f];

    // Rat trait
    public float ratLeakChance = 0.1f;
    public float ratLeakHeat = 10f;

    // Reputation gains
    public float reputationPerCall = 2f;
    public float reputationPerScrub = 5f;
    public float reputationPerPurchase = 5f;

    // Shop
    public float shopMarkup = 1.3f;
    public int shopStockSize = 4;
    public List<ThingDef> shopItems = [];
    public List<ThingDef> pinnedShopItems = [];

    // Contracts
    public IntRange offersPerFixer = new(1, 2);
    public IntRange contractDays = new(3, 7);
    public float contractReputation = 10f;
    public float failReputationLoss = 10f;
    public int failSilentDays = 3;
    public float ratFailLeakChance = 0.5f;

    public IntRange dataDeliveryGoal = new(4, 10);
    public int dataDeliveryRewardPerItem = 60;
    public int advancedCyberdataWorth = 3;
    public float brokerDataBonus = 1.5f;

    public IntRange systemTapGoal = new(2, 5);
    public int systemTapRewardPerHack = 120;

    public IntRange quietRunGoal = new(2, 4);
    public int quietRunRewardPerHack = 180;
}
