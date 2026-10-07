using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

public enum ExternalNodeType
{
    Market,
    Archive,
    Corp,
    Abandoned,
    DarkExchange,
    /// <summary>The boss node: a corporate core that shows up every few weeks for a few days.</summary>
    CorpCore,
}

/// <summary>Permanent programs found in cyberspace: active in every dive, free, no slot.</summary>
public enum DiveDaemon
{
    Blade,
    Aegis,
    Wraith,
    Hound,
    Lens,
}

/// <summary>Numbers for the Gateway and dives (docs/CYBERSPACE.md), in Defs/Cyberspace.xml.</summary>
public class CyberspaceSettingsDef : Def
{
    public IntRange nodeCount = new(4, 6);
    public int hiddenNodes = 1;
    public float refreshDays = 5f;
    public int searchCost = 5;
    public int programCost = 1;
    public int programSlots = 2;
    /// <summary>An exec data program: +1 action in each of this many first turns.</summary>
    public int execExtraActionTurns = 3;
    /// <summary>Pawn side of dives: dive traits, atrophy, stories, backstories, DLC links.</summary>
    public PawnNetSettings pawnNet = new();
    /// <summary>Entry cost of any dive; the node adds one per two defense levels.</summary>
    public int diveBaseCost = 1;
    /// <summary>Discounts on the dive cost: an AI in the pod's coprocessor, an AI riding in the netrunner.</summary>
    public int coprocessorDiscount = 1;
    public int passengerDiscount = 1;
    public int maxTurns = 8;

    public float hackBase = 0.30f;
    public float hackPerSkill = 0.035f;
    public float hackPerStrength = 0.065f;
    public float hackBonusCap = 0.30f;
    public int alertThreshold = 3;
    public float alertPatrolChance = 0.15f;
    public int alertFreeDefense = 1;
    public float alertPatrolKeepShare = 0.5f;
    public float swordBonus = 0.3f;

    public float tracePerAction = 1f;
    public float stealthTraceFactor = 0.5f;
    public float iceAttackChance = 0.6f;
    public float decoyChance = 0.5f;
    public float retreatLootLoss = 0.5f;
    public float netsickPerDive = 0.03f;
    public float netsickPerIceHit = 0.1f;
    /// <summary>A failed dive (not a retreat) leaves a few small burns from the neural feedback.</summary>
    public IntRange failBurns = new(2, 3);

    // Branches (docs/DIVE_BRANCHING.md): haul multipliers of the hard and easy branch, their trace, the hidden passage.
    public float branchHardReward = 1.6f;
    public float branchEasyReward = 0.8f;
    public float branchHardTrace = 4f;
    public float branchEasyTrace = 1f;
    public float branchPassageBonus = 0.1f;
    public int branchExtraTurns = 3;
    public FloatRange failBurnDamage = new(1.5f, 3.5f);
    public float successXp = 400f;
    /// <summary>Chance per Black ICE hit that the runner's cyberlink burns out for good.</summary>
    public float blackIceCyberlinkLoss = 0.03f;

    // Living cyberspace: a headline every few days; data glut, quiet net and the corp lockdown after the core falls.
    public FloatRange newsIntervalDays = new(2f, 4f);
    public float newsKeepDays = 3f;
    public float newsGlutDays = 2f;
    public float newsGlutLoot = 1.3f;
    public float newsQuietDays = 1f;
    public float newsQuietTrace = 0.7f;
    public float newsLockdownDays = 7f;

    // What a dive leaves on the runner (DiveEffects).
    public float effectMemoryChance = 0.25f;
    public float effectMemoryXp = 1500f;
    public int effectMigraineTurns = 9;
    public float effectPhantomNetsick = 0.6f;
    public float effectPhantomChance = 0.3f;
    public float effectBurnedLinkChance = 0.25f;
    public float effectFlowHackBonus = 0.05f;
    public float effectBurnedHackPenalty = 0.1f;
    /// <summary>Waking up in a cyberpod (Hacking Expansion's own is 5000 = 2 hours).</summary>
    public int podWakeUpTicks = 2500;
    public float rewardMultLow = 1.5f;
    public float rewardMultMid = 2f;
    public float rewardMultBlackIce = 3f;
    public float specialItemChance = 0.1f;
    public float specialItemChanceBlackIce = 0.25f;

    // Special finds on a clean exit, by weight.
    public float findFragment = 30f;
    /// <summary>An AI fragment in a node's memory unit (abandoned servers more often).</summary>
    public float memoryFragmentAbandoned = 0.15f;
    public float memoryFragmentOther = 0.05f;
    public float findResearch = 20f;
    public float findFixerService = 15f;
    public float findGroupFavor = 15f;
    public float findCelebrityChip = 12f;
    public float findDaemon = 5f;
    public float findCoreModule = 3f;
    public float researchReward = 600f;
    public float fixerServiceReputation = 8f;
    public float freeScrubHeat = 15f;
    public int groupFavorForAllies = 3;
    public float alliesCooldownDays = 15f;

    // The boss node.
    public float bossIntervalDays = 20f;
    public float bossLifetimeDays = 3f;
    public int bossIceStrength = 8;
    public float bossRewardMult = 6f;
    public float bossCoreModuleChance = 0.5f;
    /// <summary>Old Net systems behind the wall.</summary>
    public int ancientIceStrength = 10;

    // Cyberspace events (docs/PHASE5_CYBEREVENTS.md).
    public float hunterChance = 0.35f;
    public int hunterFromTurn = 3;
    public int hunterStrength = 6;
    public float hunterAttackChance = 0.4f;
    public float hunterEscapeHeat = 10f;
    public float rivalChance = 0.15f;
    public int rivalTurns = 5;
    public float honeypotChance = 0.12f;
    public float honeypotTrace = 8f;
    public float honeypotRewardShare = 0.5f;
    public float honeypotShownReward = 1.5f;
    public float stormChance = 0.2f;
    public FloatRange stormDays = new(1f, 2f);
    public float stormRewardMult = 1.5f;
    public float wildAIChance = 0.25f;
    public float wildAITameTrace = 5f;
    public int wildAIEraseAdvanced = 3;
    public int wildAISellSilver = 500;
    public float wildAISellHeat = 10f;
}

/// <summary>A node outside the home cluster: something to dive into from a cyberpod.</summary>
public class ExternalNode : IExposable
{
    public int Id;
    public int Number;
    public ExternalNodeType Type;
    public int Defense;
    public bool BlackIce;
    public int IceStrength;
    public bool Hidden;
    public bool Contract;
    /// <summary>Tick the node leaves the Gateway on its own (boss node only, -1 never).</summary>
    public int ExpireTick = -1;
    /// <summary>Another netrunner is after the same files.</summary>
    public bool Rival;
    /// <summary>A trap that looks rich (hidden from the player until scanned in a dive).</summary>
    public bool Honeypot;
    /// <summary>A wild AI lives in this node's memory unit.</summary>
    public bool WildAI;
    /// <summary>The counter-hack against hackers holding the colony's turrets.</summary>
    public bool CounterHack;
    /// <summary>The backbone node this branch hangs on (docs/CYBERSPACE_V2.md); -1 before the map exists.</summary>
    public int MapNodeId = -1;
    /// <summary>The district it lies in: drives loot (Campus research, Entertainment chips, Dump fragments).</summary>
    public District District = District.Outskirts;
    /// <summary>An ancient system of the Old Net, behind the wall: corporate-core rewards, Black ICE 10+.</summary>
    public bool Ancient;
    /// <summary>Owners left: weaker defense, less loot, a chance of a wild AI and forgotten caches.</summary>
    public bool Deserted;
    /// <summary>Unreachable for a while (a crash, an outage), -1 none.</summary>
    public int OfflineUntil = -1;
    /// <summary>+1 defense from its corporation's Alert 40+ (taken back when the Alert drops).</summary>
    public bool AlertHardened;
    /// <summary>The intrusion packet this branch intercepts (a short dive on the packet's path), -1 none.</summary>
    public int InterceptPacketId = -1;

    public bool IsBoss => Type == ExternalNodeType.CorpCore;
    public bool Offline => Find.TickManager.TicksGame < OfflineUntil;

    public string Name => CounterHack ? "NR_ExtNodeName_CounterHack".Translate() : ("NR_ExtNodeName_" + Type).Translate(Number.ToString("00"));
    public string TypeLabel => ("NR_ExtNodeType_" + Type).Translate();

    public Color Color => Type switch
    {
        ExternalNodeType.Market => NetrunUI.Accent,
        ExternalNodeType.Archive => NetrunUI.Ok,
        ExternalNodeType.Corp => NetrunUI.Bad,
        ExternalNodeType.Abandoned => NetrunUI.Purple,
        ExternalNodeType.CorpCore => new Color(0.85f, 0.15f, 0.25f),
        _ => NetrunUI.Warn,
    };

    /// <summary>Rough value of the haul, for the circle size and the panel.</summary>
    public float RewardScore => Type switch
    {
        ExternalNodeType.CorpCore => 1.4f,
        ExternalNodeType.Corp => 1f,
        ExternalNodeType.DarkExchange => 0.8f,
        ExternalNodeType.Abandoned => 0.7f,
        ExternalNodeType.Market => 0.6f,
        _ => 0.45f,
    } + Defense * 0.05f;

    public float HeatPerTrace => Type switch
    {
        ExternalNodeType.CorpCore => 2f,
        ExternalNodeType.Corp => 1.5f,
        ExternalNodeType.DarkExchange => 1.2f,
        _ => 1f,
    };

    public Texture2D Icon => (CounterHack ? NetrunTex.CyberIcon("Node_Counterhack") : null)
        ?? (Type == ExternalNodeType.CorpCore ? NetrunTex.CyberIcon("Node_CorpCore") : null)
        ?? NetrunTex.CyberIcon("Node_" + Type switch
    {
        ExternalNodeType.Corp or ExternalNodeType.CorpCore => "CorpVault",
        ExternalNodeType.Abandoned => "AbandonedServer",
        _ => Type.ToString(),
    });

    /// <summary>0 low (white ICE, light gates), 1 medium (strong gates), 2 high (Black ICE).</summary>
    public int Risk => BlackIce ? 2 : Defense >= 3 ? 1 : 0;

    public Texture2D RiskIcon => NetrunTex.CyberIcon(Risk switch { 2 => "Dive_Lightning_Red", 1 => "Dive_Lightning_Yellow", _ => "Dive_Lightning_Grey" });

    /// <summary>Icons of what the node pays out, for the reward row.</summary>
    public System.Collections.Generic.IEnumerable<Texture2D> RewardIcons
    {
        get
        {
            if (Type == ExternalNodeType.DarkExchange)
                yield return ThingDefOf.Silver.uiIcon;
            yield return NetrunDefOf.USH_Cyberdata.uiIcon;
            if (Type is ExternalNodeType.Corp or ExternalNodeType.CorpCore)
                yield return NetrunDefOf.USH_AdvancedCyberdata.uiIcon;
            if (IsBoss)
                yield return NetrunDefOf.NR_AICoreModule.uiIcon;
            if (Type == ExternalNodeType.Abandoned)
                yield return NetrunDefOf.NR_AIFragment.uiIcon;
            if (Type is ExternalNodeType.Corp or ExternalNodeType.DarkExchange or ExternalNodeType.CorpCore)
                yield return NetrunTex.CyberIcon("Reward_Valuables");
        }
    }

    /// <summary>How many times the dive cost the main files pay back.</summary>
    public float RewardMultiplier => BaseRewardMultiplier
        * (GatewayManager.Get()?.StormActive == true ? NetrunDefOf.NR_Cyberspace.stormRewardMult : 1f)
        * (Honeypot ? NetrunDefOf.NR_Cyberspace.honeypotShownReward : 1f);

    private float BaseRewardMultiplier => (IsBoss || Ancient) ? NetrunDefOf.NR_Cyberspace.bossRewardMult
        : BlackIce ? NetrunDefOf.NR_Cyberspace.rewardMultBlackIce
        : Defense <= 2 ? NetrunDefOf.NR_Cyberspace.rewardMultLow : NetrunDefOf.NR_Cyberspace.rewardMultMid;

    /// <summary>
    /// Branched dives: the base without the Black ICE premium (the hard branch carries the ICE and its own multiplier),
    /// times storm and honeypot as usual.
    /// </summary>
    public float BranchBaseMultiplier => ((IsBoss || Ancient) ? NetrunDefOf.NR_Cyberspace.bossRewardMult
            : Defense <= 2 ? NetrunDefOf.NR_Cyberspace.rewardMultLow : NetrunDefOf.NR_Cyberspace.rewardMultMid)
        * (GatewayManager.Get()?.StormActive == true ? NetrunDefOf.NR_Cyberspace.stormRewardMult : 1f)
        * (Honeypot ? NetrunDefOf.NR_Cyberspace.honeypotShownReward : 1f);

    public string RewardText => ("NR_ExtNodeReward_" + Type).Translate();

    /// <summary>What a clean dive can bring, item by item, with an amount or a chance (for the node panel).</summary>
    public System.Collections.Generic.List<(ThingDef def, string amount)> RewardLines(int diveCost)
    {
        var s = NetrunDefOf.NR_Cyberspace;
        int basis = Mathf.Max(diveCost, s.diveBaseCost + Defense / 2);
        var lines = new System.Collections.Generic.List<(ThingDef, string)>();
        if (DiveSession.ForksFor(this) > 0)
        {
            float b = basis * BranchBaseMultiplier;
            lines.Add((NetrunDefOf.USH_Cyberdata, $"{Mathf.Max(2, Mathf.RoundToInt(b * s.branchEasyReward * 0.85f))}–{Mathf.RoundToInt(b * s.branchHardReward * 1.15f)}"));
        }
        else
        {
            int data = Mathf.Max(2, Mathf.RoundToInt(basis * RewardMultiplier));
            lines.Add((NetrunDefOf.USH_Cyberdata, $"{Mathf.Max(2, Mathf.RoundToInt(data * 0.85f))}–{Mathf.RoundToInt(data * 1.15f)}"));
        }
        if (Type is ExternalNodeType.Corp or ExternalNodeType.CorpCore)
            lines.Add((NetrunDefOf.USH_AdvancedCyberdata, "1–2"));
        if (Type == ExternalNodeType.DarkExchange)
            lines.Add((ThingDefOf.Silver, "200–400"));
        if (IsBoss)
            lines.Add((NetrunDefOf.NR_AICoreModule, s.bossCoreModuleChance.ToStringPercent()));
        if (Type is ExternalNodeType.Abandoned or ExternalNodeType.Corp or ExternalNodeType.DarkExchange or ExternalNodeType.CorpCore)
            lines.Add((NetrunDefOf.NR_AIFragment, AIBalance.FragmentChance(Type == ExternalNodeType.Abandoned ? NetrunDefOf.NR_Cyberspace.memoryFragmentAbandoned : NetrunDefOf.NR_Cyberspace.memoryFragmentOther).ToStringPercent()));
        return lines;
    }

    /// <summary>The corporate core: top defense, strong Black ICE, gone after a few days.</summary>
    public static ExternalNode GenerateBoss(int id) => new()
    {
        Id = id,
        Number = Rand.RangeInclusive(1, 99),
        Type = ExternalNodeType.CorpCore,
        Defense = 5,
        BlackIce = true,
        IceStrength = NetrunDefOf.NR_Cyberspace.bossIceStrength,
        ExpireTick = Find.TickManager.TicksGame + (int)(NetrunDefOf.NR_Cyberspace.bossLifetimeDays * GenDate.TicksPerDay),
    };

    /// <summary>A branch for a backbone node of the given district (docs/CYBERSPACE_V2.md, section 9.2).</summary>
    public static ExternalNode ForDistrict(int id, District district)
    {
        ExternalNode node = Generate(id);
        node.District = district;
        (ExternalNodeType type, int lo, int hi) = district switch
        {
            District.Outskirts => (Rand.Bool ? ExternalNodeType.Market : ExternalNodeType.Archive, 1, 2),
            District.Transit => (ExternalNodeType.Market, 2, 3),
            District.Corp => (ExternalNodeType.Corp, 3, 5),
            District.Market => (Rand.Chance(0.35f) ? ExternalNodeType.DarkExchange : ExternalNodeType.Market, 2, 3),
            District.Ruins => (ExternalNodeType.Abandoned, 2, 4),
            District.Campus => (ExternalNodeType.Archive, 2, 3),
            District.Entertainment => (Rand.Bool ? ExternalNodeType.Market : ExternalNodeType.Archive, 2, 3),
            District.Dump => (Rand.Bool ? ExternalNodeType.Abandoned : ExternalNodeType.Archive, 1, 3),
            District.Dark => (ExternalNodeType.DarkExchange, 3, 4),
            District.OldNet => (ExternalNodeType.Corp, 5, 5),
            _ => (node.Type, node.Defense, node.Defense),
        };
        node.Type = type;
        node.Defense = Rand.RangeInclusive(lo, hi);
        node.BlackIce = type == ExternalNodeType.Corp || (node.Defense >= 4 && Rand.Chance(0.5f));
        node.IceStrength = node.BlackIce ? node.Defense + Rand.RangeInclusive(1, 2) : 0;
        CyberspaceSettingsDef s = NetrunDefOf.NR_Cyberspace;
        node.WildAI = type == ExternalNodeType.Abandoned && Rand.Chance(s.wildAIChance * NetrunMod.Settings.CyberEventFrequency);
        // The dump is full of junk and traps.
        if (district == District.Dump)
            node.Honeypot = Rand.Chance(s.honeypotChance * 2f * NetrunMod.Settings.CyberEventFrequency);
        if (district == District.OldNet)
        {
            node.Ancient = true;
            node.BlackIce = true;
            node.IceStrength = s.ancientIceStrength;
            node.Rival = node.Honeypot = false;
        }
        return node;
    }

    public static ExternalNode Generate(int id)
    {
        var type = (ExternalNodeType)Rand.Range(0, 5);
        int defense = Rand.RangeInclusive(1, 5);
        bool blackIce = type == ExternalNodeType.Corp || (defense >= 4 && Rand.Chance(0.5f));
        CyberspaceSettingsDef s = NetrunDefOf.NR_Cyberspace;
        return new ExternalNode
        {
            Id = id,
            Number = Rand.RangeInclusive(1, 99),
            Type = type,
            Defense = defense,
            BlackIce = blackIce,
            IceStrength = blackIce ? defense + Rand.RangeInclusive(1, 2) : 0,
            Rival = Rand.Chance(s.rivalChance * NetrunMod.Settings.CyberEventFrequency),
            Honeypot = Rand.Chance(s.honeypotChance * NetrunMod.Settings.CyberEventFrequency),
            WildAI = type == ExternalNodeType.Abandoned && Rand.Chance(s.wildAIChance * NetrunMod.Settings.CyberEventFrequency),
        };
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id");
        Scribe_Values.Look(ref Number, "number");
        Scribe_Values.Look(ref Type, "type");
        Scribe_Values.Look(ref Defense, "defense");
        Scribe_Values.Look(ref BlackIce, "blackIce");
        Scribe_Values.Look(ref IceStrength, "iceStrength");
        Scribe_Values.Look(ref Hidden, "hidden");
        Scribe_Values.Look(ref Contract, "contract");
        Scribe_Values.Look(ref ExpireTick, "expireTick", -1);
        Scribe_Values.Look(ref Rival, "rival");
        Scribe_Values.Look(ref Honeypot, "honeypot");
        Scribe_Values.Look(ref WildAI, "wildAI");
        Scribe_Values.Look(ref CounterHack, "counterHack");
        Scribe_Values.Look(ref MapNodeId, "mapNode", -1);
        Scribe_Values.Look(ref District, "district", District.Outskirts);
        Scribe_Values.Look(ref Ancient, "ancient");
        Scribe_Values.Look(ref Deserted, "deserted");
        Scribe_Values.Look(ref OfflineUntil, "offlineUntil", -1);
        Scribe_Values.Look(ref AlertHardened, "alertHardened");
        Scribe_Values.Look(ref InterceptPacketId, "interceptPacket", -1);
    }
}
