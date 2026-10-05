using Verse;

namespace Netrun;

/// <summary>AI balance numbers (Defs/AI.xml).</summary>
public class AISettingsDef : Def
{
    // Server slots: base, maximum with core modules.
    public int baseSlots = 1;
    public int maxSlots = 3;

    // Awakening a fragment at the comms console.
    public int awakenWorkTicks = 5000;
    public float awakenDetectChancePerHeat = 0.006f;
    public float awakenDetectHeat = 10f;

    // Drops on completed hacks, by Hacking Expansion hackset of the target.
    public float fragmentChanceCoreCommandICE = 0.1f;
    public float fragmentChanceBlackICE = 0.25f;
    public float moduleChanceBlackICE = 0.05f;
    public float moduleChanceBlackBox = 0.1f;

    // Trust and hunger (0-100).
    public float startTrust = 50f;
    public float hungerPerDay = 10f;
    public float starvingHunger = 70f;
    public float starvingTrustPerDay = 2f;
    public float isolatedTrustPerDay = 3f;
    public float isolatedHungerFactor = 2f;
    public float feedHungerRelief = 50f;
    public float feedTrust = 5f;
    public float ghostHungerPerHack = 15f;
    public float ghostCalmPerDay = 15f;

    // Services.
    public float minTrustForService = 20f;
    public float serviceCooldownDays = 3f;
    public IntRange dataHunterData = new(2, 4);
    public float dataHunterHeat = 2f;
    public float ghostHeatScrub = 10f;
    public float wardenTrust = 10f;
    public int oracleDeadlineDays = 1;
    public float forgeResearch = 600f;
    public float sirenReputation = 10f;
    public float tricksterBadChance = 0.35f;
    public float tricksterHeat = 10f;

    // Requests.
    public IntRange requestIntervalDays = new(6, 10);
    public int requestDays = 3;
    public int requestFeedMultiplier = 3;
    public float requestTrust = 15f;
}
