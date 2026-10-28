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
    /// <summary>Light clothing (worn apparel mass, kg): at 0 kg and up to the second limit; hack bonus and Netsick cut of the session.</summary>
    public float lightGearMass0 = 0.0001f;
    public float lightGearMass1 = 1.5f;
    public float lightGearHack0 = 0.04f;
    public float lightGearHack1 = 0.02f;
    public float lightGearNetsickCut0 = 0.10f;
    public float lightGearNetsickCut1 = 0.05f;
    /// <summary>Noise and alarm states (BALANCE_PLAN 3.3, 3.11): thresholds, what raises noise, what each state changes.</summary>
    public int noiseMax = 6;
    public int noiseSuspicion = 2;
    public int noiseAlarm = 4;
    public int noiseStealthShift = 1;
    public int noiseGateFail = 1;
    public int noiseIceWhiteFail = 2;
    public int noiseIceFail = 1;
    public int noisePatrolSeen = 2;
    public int noiseSword = 1;
    public int noiseIceBreaker = 2;
    public int noiseKiller = 1;
    public int noiseScanSuspicion = 1;
    public float iceAttackChanceQuiet = 0.3f;
    public float suspicionGatePenalty = 0.05f;
    public float alarmPatrolChance = 0.15f;
    public float alarmTraceFactor = 1.5f;
    public int alarmFilesMelt = 1;
    public float alarmCorpAlert = 5f;
    public float ghostTraceFactor = 0.5f;
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
    // Main files by node tier (docs/BALANCE_PLAN.md, section 2): fixed ranges, no longer tied to the dive cost.
    public IntRange rewardLow = new(2, 3);
    public IntRange rewardMid = new(3, 5);
    public IntRange rewardMidHard = new(4, 6);
    public IntRange rewardBlackIce = new(5, 8);
    public IntRange rewardBoss = new(10, 14);
    public IntRange cacheReward = new(1, 1);
    public IntRange forgottenCacheReward = new(2, 4);
    /// <summary>Corp nodes: advanced cyberdata only on the hard path; the core always.</summary>
    public IntRange advancedCorpHard = new(1, 1);
    public IntRange advancedBoss = new(2, 3);
    /// <summary>Black ICE nodes that are not corp vaults: 1 advanced cyberdata with the files (docs/BALANCE_PLAN.md, section 2).</summary>
    public IntRange advancedBlackIce = new(1, 1);
    /// <summary>The memory unit pays advanced cyberdata unless it holds an AI fragment or a core module.</summary>
    public IntRange memoryAdvanced = new(1, 2);
    /// <summary>All reward multipliers on one dive (storm, guarded hub, lure, mirror, news glut, Orbit, hidden passages) together stop here; the corporate core is exempt.</summary>
    public float rewardMultCap = 2f;
    public IntRange exchangeSilver = new(120, 250);
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
    public float freeScrubHeat = 10f;
    public float freeScrubDays = 15f;
    public int groupFavorForAllies = 3;
    public float alliesCooldownDays = 15f;

    // The boss node.
    public float bossIntervalDays = 20f;
    public float bossLifetimeDays = 3f;
    public int bossIceStrength = 8;
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
    /// <summary>An overloaded node falls over with this chance every node turn.</summary>
    public float overloadCrashChance = 0.15f;
    public float honeypotShownReward = 1.5f;
    public float stormChance = 0.2f;
    public FloatRange stormDays = new(1f, 2f);
    public float stormRewardMult = 1.5f;
    public float wildAIChance = 0.25f;
    public float wildAITameTrace = 5f;
    public int wildAIEraseAdvanced = 3;
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
    /// <summary>A crew's race challenge the player accepted: the crew's faction loadID, -1 for none.</summary>
    public int ChallengeCrew = -1;
    /// <summary>A trap that looks rich (hidden from the player until scanned in a dive).</summary>
    public bool Honeypot;
    /// <summary>A runner's deep scan or the sprung trap showed the honeypot: its hub plate marks it.</summary>
    public bool HoneypotKnown;
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
    // Node effects from the news (docs/CONCEPTS_10.md, section 13.1), ticks until; -1 none.
    public int OverloadUntil = -1;
    public int MirrorUntil = -1;
    public int InterferenceUntil = -1;
    public int AuctionUntil = -1;
    public int OpenDoorUntil = -1;
    /// <summary>A forgotten cache: an extra stash, found by a scan.</summary>
    public bool ForgottenCache;

    public bool Overloaded => Find.TickManager.TicksGame < OverloadUntil;
    public bool Mirrored => Find.TickManager.TicksGame < MirrorUntil;
    public bool Interference => Find.TickManager.TicksGame < InterferenceUntil;
    public bool Auction => Find.TickManager.TicksGame < AuctionUntil;
    public bool OpenDoor => Find.TickManager.TicksGame < OpenDoorUntil;

    /// <summary>Short labels of the effects on this node, for the panel and the map.</summary>
    public System.Collections.Generic.IEnumerable<string> EffectLabels
    {
        get
        {
            if (Overloaded)
                yield return "NR_NodeEffect_Overload".Translate();
            if (Mirrored)
                yield return "NR_NodeEffect_Mirror".Translate();
            if (Interference)
                yield return "NR_NodeEffect_Interference".Translate();
            if (Auction)
                yield return "NR_NodeEffect_Auction".Translate();
            if (OpenDoor)
                yield return "NR_NodeEffect_OpenDoor".Translate();
            if (ForgottenCache)
                yield return "NR_NodeEffect_Cache".Translate();
        }
    }

    /// <summary>+1 defense from its corporation's Alert 40+ (taken back when the Alert drops).</summary>
    public bool AlertHardened;
    /// <summary>The intrusion packet this branch intercepts (a short dive on the packet's path), -1 none.</summary>
    public int InterceptPacketId = -1;
    /// <summary>D2: this branch is the guard gateway of a protected hub (that hub's id), -1 none.</summary>
    public int GuardFor = -1;

    public bool IsBoss => Type == ExternalNodeType.CorpCore;

    /// <summary>D2: the branch hangs on a protected hub (its loot is richer).</summary>
    public bool InGuardedHub => MapNodeId >= 0 && NetrunMod.Settings.ProtectedHubs && NetMapManager.Get()?.Map?.Node(MapNodeId)?.Guarded == true;
    public bool Offline => Find.TickManager.TicksGame < OfflineUntil;

    /// <summary>The training simulation (never on the map or in the node list): no pod, no Heat, no harm, no haul.</summary>
    public bool Training;
    /// <summary>The ending (docs/CONCEPT.md, section 19): a corporate black lab with a code name; may hold a Transition blueprint.</summary>
    public bool Lab;
    public string LabName;

    /// <summary>The top tier asks for one advanced cyberdata at the door: the corporate core, the Old Net and the labs.</summary>
    public bool NeedsAdvanced => IsBoss || Ancient || Lab;

    public string Name => Training ? "NR_TrainingNodeName".Translate() : Lab ? "NR_LabNodeName".Translate(LabName ?? "?") : CounterHack ? "NR_ExtNodeName_CounterHack".Translate() : ("NR_ExtNodeName_" + Type).Translate(Number.ToString("00"));
    public string TypeLabel => ("NR_ExtNodeType_" + Type).Translate();

    /// <summary>A lab glows violet so it never passes for a plain corporate vault (Maxim, 2026-10-14).</summary>
    public Color Color => Lab ? new Color(0.8f, 0.4f, 1f) : Type switch
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

    public Texture2D Icon => (Lab ? NetrunTex.CyberIcon("Node_CorpLab") : null)
        ?? (Ancient ? NetrunTex.CyberIcon("Node_OldNet") : null)
        ?? (CounterHack ? NetrunTex.CyberIcon("Node_Counterhack") : null)
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

    /// <summary>
    /// Everything that scales cyberdata on a dive: storm, guarded hub, honeypot lure, mirror, news glut, Orbit and the hidden
    /// passages, multiplied and capped at rewardMultCap (the corporate core is exempt). The roll and the forecast share it.
    /// </summary>
    public float StackMult(float passage = 0f)
    {
        CyberspaceSettingsDef s = NetrunDefOf.NR_Cyberspace;
        float m = (GatewayManager.Get()?.StormActive == true ? s.stormRewardMult : 1f)
            * (InGuardedHub ? NetMapManager.GuardedRewardMult : 1f)
            * (Honeypot ? s.honeypotShownReward : 1f)
            * (Mirrored ? 2f : 1f)
            * (GatewayManager.Get()?.LootFactor ?? 1f)
            * (District == District.Orbit ? NetMapManager.Settings.orbitLoot : 1f)
            * (1f + passage);
        return IsBoss ? m : Mathf.Min(m, s.rewardMultCap);
    }

    /// <summary>The mirror (and the auction, for silver) on advanced cyberdata and silver, under the same cap.</summary>
    public float ExtraMult(bool silver)
    {
        float m = (Mirrored ? 2f : 1f) * (silver && Auction ? 1.5f : 1f);
        return IsBoss ? m : Mathf.Min(m, NetrunDefOf.NR_Cyberspace.rewardMultCap);
    }

    public float RewardFactor => StackMult();

    /// <summary>The node has a memory unit at the end of the dive.</summary>
    public bool HasMemory => Type is ExternalNodeType.Abandoned or ExternalNodeType.Corp or ExternalNodeType.DarkExchange or ExternalNodeType.CorpCore;

    /// <summary>The tier's main-files range (before the factor); only medium nodes differ by path.</summary>
    public IntRange BaseRewardRange(bool hard)
    {
        var s = NetrunDefOf.NR_Cyberspace;
        return (IsBoss || Ancient) ? s.rewardBoss : BlackIce ? s.rewardBlackIce : Defense <= 2 ? s.rewardLow : hard ? s.rewardMidHard : s.rewardMid;
    }

    /// <summary>What the main files can pay on a path, with the factor applied; the forecast and the roll share it.</summary>
    public IntRange ShownRewardRange(bool hard)
    {
        IntRange r = BaseRewardRange(hard);
        float f = RewardFactor;
        return new IntRange(Mathf.RoundToInt(r.min * f), Mathf.RoundToInt(r.max * f));
    }

    public static string RangeText(IntRange r) => r.min == r.max ? r.min.ToString() : $"{r.min}–{r.max}";

    /// <summary>The best case average, for the map's loot lens.</summary>
    public float ExpectedHaul => ShownRewardRange(DiveSession.ForksFor(this) > 0).Average;

    public string RewardText => ("NR_ExtNodeReward_" + Type).Translate();

    /// <summary>Advanced cyberdata from the files themselves: (always, extra on the hard path).</summary>
    public (int min, int max) FilesAdvanced(bool hard)
    {
        CyberspaceSettingsDef s = NetrunDefOf.NR_Cyberspace;
        if (IsBoss)
            return (s.advancedBoss.min, s.advancedBoss.max);
        if (Type == ExternalNodeType.Corp)
            return hard ? (s.advancedCorpHard.min, s.advancedCorpHard.max) : (0, 0);
        return BlackIce ? (s.advancedBlackIce.min, s.advancedBlackIce.max) : (0, 0);
    }

    /// <summary>
    /// Totals a clean dive can bring over every route through the forks: min is the quietest, max the richest (caches, the forgotten
    /// cache, hidden passages, every bonus and the cap included). The roll is checked against it by the dev action.
    /// </summary>
    public (int dataMin, int dataMax, int advMin, int advMax, int silverMin, int silverMax) ForecastTotals()
    {
        CyberspaceSettingsDef s = NetrunDefOf.NR_Cyberspace;
        int forks = DiveSession.ForksFor(this);
        int dataMin = int.MaxValue, dataMax = 0, advMin = int.MaxValue, advMax = 0;
        // Each route: how many forks take the hard branch (the first n; the order does not change the sums).
        for (int hardForks = 0; hardForks <= forks; hardForks++)
        {
            bool hard = hardForks > 0;
            int easyForks = forks - hardForks;
            // Caches: the hard branch has one, a junction between two forks has one, a node without forks may hold one.
            int caches = hardForks + Mathf.Max(0, forks - 1) + (forks == 0 ? 1 : 0);
            float mult = StackMult(easyForks * s.branchPassageBonus);
            IntRange main = BaseRewardRange(hard);
            int lo = Mathf.RoundToInt(main.min * StackMult());
            int hi = Mathf.RoundToInt((main.max + caches * s.cacheReward.max + (ForgottenCache ? s.forgottenCacheReward.max : 0)) * mult);
            dataMin = Mathf.Min(dataMin, lo);
            dataMax = Mathf.Max(dataMax, hi);
            (int fmin, int fmax) = FilesAdvanced(hard);
            advMin = Mathf.Min(advMin, fmin);
            advMax = Mathf.Max(advMax, fmax);
        }
        if (HasMemory)
            advMax += s.memoryAdvanced.max;
        float extra = ExtraMult(false);
        int silver = Type == ExternalNodeType.DarkExchange ? 1 : 0;
        float sx = ExtraMult(true);
        return (dataMin, dataMax, Mathf.RoundToInt(advMin * extra), Mathf.RoundToInt(advMax * extra),
            silver * Mathf.RoundToInt(s.exchangeSilver.min * sx), silver * Mathf.RoundToInt(s.exchangeSilver.max * sx));
    }

    /// <summary>Chance of a special find on a clean exit (the core and the Old Net always leave one).</summary>
    public float SpecialFindChance
    {
        get
        {
            if (IsBoss || Ancient)
                return 1f;
            float chance = BlackIce ? NetrunDefOf.NR_Cyberspace.specialItemChanceBlackIce : NetrunDefOf.NR_Cyberspace.specialItemChance;
            return Mathf.Min(1f, District == District.Dump ? chance * 1.5f : chance);
        }
    }

    /// <summary>What a clean dive can bring, line by line: icon, name, amount or chance, tooltip (for the node panel).</summary>
    public System.Collections.Generic.List<(Texture2D icon, string label, string amount, string tip)> RewardLines()
    {
        CyberspaceSettingsDef s = NetrunDefOf.NR_Cyberspace;
        var lines = new System.Collections.Generic.List<(Texture2D, string, string, string)>();
        var t = ForecastTotals();
        bool forked = DiveSession.ForksFor(this) > 0;

        // Cyberdata: main files + caches + the forgotten cache, with every bonus, under the cap.
        string dataTip = "NR_RewardTipData".Translate(RangeText(ShownRewardRange(false)) + (forked ? " / " + RangeText(ShownRewardRange(true)) : ""),
            s.cacheReward.max.ToString(), ForgottenCache ? "NR_RewardTipForgotten".Translate(s.forgottenCacheReward.min.ToString(), s.forgottenCacheReward.max.ToString()).ToString() : "NR_RewardTipNoForgotten".Translate().ToString(),
            s.rewardMultCap.ToString("0.#")).ToString();
        lines.Add((NetrunDefOf.USH_Cyberdata.uiIcon, NetrunDefOf.USH_Cyberdata.LabelCap, RangeText(new IntRange(t.dataMin, t.dataMax)), dataTip));

        // Advanced cyberdata, and when it comes.
        if (t.advMax > 0)
        {
            var when = new System.Collections.Generic.List<string>();
            if (IsBoss)
                when.Add("NR_RewardTipAdvBoss".Translate(RangeText(s.advancedBoss)));
            else if (Type == ExternalNodeType.Corp)
                when.Add("NR_RewardTipAdvCorp".Translate(RangeText(s.advancedCorpHard)));
            else if (BlackIce)
                when.Add("NR_RewardTipAdvBlackIce".Translate(RangeText(s.advancedBlackIce)));
            if (HasMemory)
                when.Add("NR_RewardTipAdvMemory".Translate(RangeText(s.memoryAdvanced)));
            lines.Add((NetrunDefOf.USH_AdvancedCyberdata.uiIcon, NetrunDefOf.USH_AdvancedCyberdata.LabelCap, RangeText(new IntRange(t.advMin, t.advMax)), string.Join("; ", when)));
        }
        if (Type == ExternalNodeType.DarkExchange)
            lines.Add((ThingDefOf.Silver.uiIcon, ThingDefOf.Silver.LabelCap, RangeText(new IntRange(t.silverMin, t.silverMax)), "NR_RewardTipSilver".Translate()));
        if (IsBoss)
            lines.Add((NetrunDefOf.NR_AICoreModule.uiIcon, NetrunDefOf.NR_AICoreModule.LabelCap, s.bossCoreModuleChance.ToStringPercent(), null));
        if (Type is ExternalNodeType.Abandoned or ExternalNodeType.Corp or ExternalNodeType.DarkExchange or ExternalNodeType.CorpCore)
            lines.Add((NetrunDefOf.NR_AIFragment.uiIcon, NetrunDefOf.NR_AIFragment.LabelCap,
                AIBalance.FragmentChance(Ancient ? 0.5f : Type == ExternalNodeType.Abandoned ? s.memoryFragmentAbandoned : s.memoryFragmentOther).ToStringPercent(), null));
        // The special find (fragment, research, service, favor, chip, daemon, module): one roll on a clean exit.
        lines.Add((NetrunTex.CyberIcon("Reward_Valuables"), "NR_RewardSpecial".Translate(), SpecialFindChance.ToStringPercent(), "NR_RewardTipSpecial".Translate()));
        return lines;
    }

    /// <summary>A corporate black lab (the ending): Black ICE, two forks, gone when its coordinates go stale.</summary>
    public static ExternalNode GenerateLab(int id, string codeName, float lifetimeDays) => new()
    {
        Id = id,
        Number = Rand.RangeInclusive(1, 99),
        Type = ExternalNodeType.Corp,
        District = District.Corp,
        Defense = 5,
        BlackIce = true,
        IceStrength = 7,
        Lab = true,
        LabName = codeName,
        ExpireTick = Find.TickManager.TicksGame + (int)(lifetimeDays * GenDate.TicksPerDay),
    };

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
            District.Orbit => (Rand.Bool ? ExternalNodeType.Corp : ExternalNodeType.Archive, 4, 5),
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
        Scribe_Values.Look(ref ChallengeCrew, "challengeCrew", -1);
        Scribe_Values.Look(ref Honeypot, "honeypot");
        Scribe_Values.Look(ref HoneypotKnown, "honeypotKnown");
        Scribe_Values.Look(ref WildAI, "wildAI");
        Scribe_Values.Look(ref CounterHack, "counterHack");
        Scribe_Values.Look(ref MapNodeId, "mapNode", -1);
        Scribe_Values.Look(ref District, "district", District.Outskirts);
        Scribe_Values.Look(ref Ancient, "ancient");
        Scribe_Values.Look(ref Deserted, "deserted");
        Scribe_Values.Look(ref OfflineUntil, "offlineUntil", -1);
        Scribe_Values.Look(ref AlertHardened, "alertHardened");
        Scribe_Values.Look(ref OverloadUntil, "overloadUntil", -1);
        Scribe_Values.Look(ref MirrorUntil, "mirrorUntil", -1);
        Scribe_Values.Look(ref InterferenceUntil, "interferenceUntil", -1);
        Scribe_Values.Look(ref AuctionUntil, "auctionUntil", -1);
        Scribe_Values.Look(ref OpenDoorUntil, "openDoorUntil", -1);
        Scribe_Values.Look(ref ForgottenCache, "forgottenCache");
        Scribe_Values.Look(ref Training, "training");
        Scribe_Values.Look(ref Lab, "lab");
        Scribe_Values.Look(ref LabName, "labName");
        Scribe_Values.Look(ref InterceptPacketId, "interceptPacket", -1);
        Scribe_Values.Look(ref GuardFor, "guardFor", -1);
    }
}
