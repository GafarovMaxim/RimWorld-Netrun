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
    public float serviceTrustCost = 7f;
    public float serviceHungerCost = 10f;
    public IntRange dataHunterData = new(2, 4);
    public float dataHunterHeat = 2f;
    public float ghostHeatScrub = 10f;
    public float wardenTrust = 10f;
    public int oracleDeadlineDays = 1;
    public float forgeResearch = 600f;
    public float sirenReputation = 10f;
    public float tricksterBadChance = 0.35f;
    public float tricksterHeat = 10f;
    public float docNetsickRelief = 0.2f;
    public float archivistXp = 4000f;
    public float seedHeatScrub = 3f;

    // Seed: feedings (or quiet days for Ghost) needed before it settles into a personality.
    public int seedGrowthToEvolve = 5;

    // Requests.
    public IntRange requestIntervalDays = new(6, 10);
    public int requestDays = 3;
    public int requestFeedMultiplier = 3;
    public float requestTrust = 15f;

    // Power loss: the AI sleeps at once, is lost for good after this many days.
    public float lostAfterUnpoweredDays = 5f;

    // Escape and betrayal.
    public float escapeTrust = 20f;
    public float escapeHunger = 70f;
    public int escapeWarningHours = 6;
    public float escapeHeat = 10f;
    public float betrayalTrust = 10f;
    public float betrayalHeat = 25f;

    // Erasing an AI gives some of it back as cyberdata.
    public int eraseAdvancedCyberdata = 2;

    // "Fell asleep" notice waits this long, so power flickers stay quiet.
    public int sleepNoticeHours = 2;

    // Random AI events: how often, and the trust needed for a good one (below the low mark they cause trouble).
    public IntRange eventIntervalDays = new(4, 8);
    public float eventGoodTrust = 50f;
    public float eventBadTrust = 30f;
    public float eventBadHeat = 6f;
    public float colonyEventGapHours = 36f;
    public float eventAnswerDays = 2f;

    // Echo: chance that a colonist dying with a cyberlink leaves a digital imprint.
    public float echoChance = 0.35f;

    // Hosted Data Hunter: extra cyberdata chance per hack by its host.
    public float hostedDataHunterBonusChance = 0.25f;
}
