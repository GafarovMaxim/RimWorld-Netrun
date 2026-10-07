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
    public int maxPinned = 2;
    /// <summary>Silver paid per held fixer at every rotation; without it the hold lapses.</summary>
    public int holdCost = 100;
    /// <summary>After breaking ties a fixer does not come back for this many days.</summary>
    /// <summary>Breaking ties during a running contract: cyberdata the fixer takes, or Heat if there is none.</summary>
    public int breakContractCyberdata = 6;
    public float breakContractHeat = 6f;
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

    // Royalty: imperial clients.
    public float imperialChance = 0.2f;
    public float imperialSilverFactor = 0.6f;
    public float imperialSilverPerFavor = 150f;
    public int imperialMaxFavor = 6;

    // New contracts (docs/CONCEPTS_7.md, part 5).
    public IntRange surveillanceHours = new(12, 30);
    public int surveillanceRewardPerHour = 25;
    public float layLowMinHeat = 25f;
    public IntRange layLowDrop = new(15, 30);
    public int layLowRewardPerHeat = 18;
    public int rookieMaxSkill = 6;
    public int rookieDives = 2;
    public int rookieReward = 450;
    public int aiOrderReward = 650;
    public IntRange cleanLabChips = new(1, 2);
    public int cleanLabRewardPerChip = 300;

    // Traits: each one helps and hurts.
    /// <summary>How often each trait rolls, by specialty (Broker, Cleaner, Fence); the order follows FixerTrait.</summary>
    public List<float> traitWeightsBroker = [2f, 2f, 1f, 2f, 2f, 2f, 1f, 0f, 2f];
    public List<float> traitWeightsCleaner = [1f, 2f, 1f, 2f, 1f, 2f, 3f, 0f, 1f];
    public List<float> traitWeightsFence = [2f, 1f, 2f, 1f, 1f, 1f, 1f, 3f, 1f];
    public int greedyExtraStock = 2;
    public float reliableRewardFactor = 0.9f;
    public float ratPriceFactor = 0.8f;
    public float paranoidRaidFactor = 0.75f;
    public FloatRange gamblerRewardFactor = new(0.5f, 1.8f);
    public float loyalReputationFactor = 2f;
    public float loyalPinnedDiscount = 0.1f;
    public float veteranScrubFactor = 1.2f;
    public float veteranCallFactor = 1.3f;
    public int collectorStockPenalty = 2;
    public float hotheadDurationFactor = 0.6f;
    public float hotheadRewardFactor = 1.4f;
    public float hotheadFailHeat = 10f;
    public float maxReputationDiscount = 0.5f;
    public float cleanerDiscount = 0.25f;
    public float maxDiscount = 0.75f;

    // Heat scrubbing
    public float heatPerCyberdata = 5f;
    /// <summary>Cyberdata to rotate the fixer pool now instead of waiting.</summary>
    public int rotateNowCost = 8;
    /// <summary>Black market goods cost this much more than the fixer's own stock.</summary>
    public float blackMarketMarkup = 1.18f;
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
