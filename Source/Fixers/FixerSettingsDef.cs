using System.Collections.Generic;
using Verse;

namespace Netrun;

/// <summary>All fixer balance numbers in one place (Defs/Fixers.xml).</summary>
public class FixerSettingsDef : Def
{
    // Pool
    public int poolSize = 3;
    public IntRange rotationDays = new(7, 10);
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
}
