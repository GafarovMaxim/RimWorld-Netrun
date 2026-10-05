using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using USH_HE;
using Verse;

namespace Netrun;

public enum DiveElementKind
{
    Lobby,
    Gate,
    Ice,
    Files,
    Memory,
    Exit,
}

public enum DiveProgram
{
    Sword,
    Shield,
    Decoy,
    Stealth,
    Killer,
}

/// <summary>One element of a node's architecture (Data Fortress style): a box on the dive map.</summary>
public class DiveElement : IExposable
{
    public DiveElementKind Kind;
    public int Strength;
    public bool Hidden;
    /// <summary>A small hidden cache rather than the node's main files.</summary>
    public bool Cache;
    public bool Cleared;
    public bool Looted;

    /// <summary>Gates and ICE block the way on until they are hacked.</summary>
    public bool Blocks => (Kind == DiveElementKind.Gate || Kind == DiveElementKind.Ice) && !Cleared;

    public void ExposeData()
    {
        Scribe_Values.Look(ref Kind, "kind");
        Scribe_Values.Look(ref Strength, "strength");
        Scribe_Values.Look(ref Hidden, "hidden");
        Scribe_Values.Look(ref Cache, "cache");
        Scribe_Values.Look(ref Cleared, "cleared");
        Scribe_Values.Look(ref Looted, "looted");
    }
}

/// <summary>
/// A dive by turns (docs/CYBERSPACE.md, mockup 3-dive): the netrunner moves along the node's elements,
/// spending network actions; when they run out the node answers (Black ICE attacks, the trace grows).
/// The game is paused while the terminal is open, so a dive takes no game time.
/// </summary>
public class DiveSession : IExposable
{
    private static CyberspaceSettingsDef Settings => NetrunDefOf.NR_Cyberspace;

    public ExternalNode Node;
    public Pawn Runner;
    public List<DiveElement> Elements = [];
    public int Position;
    public int Turn = 1;
    public int ActionsLeft;
    public float Trace;
    public List<DiveProgram> Programs = [];
    public List<DiveDaemon> Daemons = [];
    public int ShieldCharges;
    public int DecoyCharges;
    public int KillerCharges;
    public int LootCyberdata;
    public int LootAdvanced;
    public int LootSilver;
    public List<ThingDef> LootItems = [];
    public List<string> Log = [];
    public bool Finished;
    /// <summary>Cyberdata paid to start the dive; the main files pay it back with a multiplier.</summary>
    public int CostPaid;

    // Cyberspace events.
    public bool HunterActive;
    public bool HunterSpawned;
    public bool HoneypotRevealed;
    public bool RivalTook;
    public bool Storm;

    public int ActionsPerTurn
    {
        get
        {
            float speed = Runner?.GetStatValue(StatDefOf.HackingSpeed) ?? 1f;
            return speed < 0.8f ? 1 : speed < 1.3f ? 2 : 3;
        }
    }

    public DiveElement Current => Elements[Position];
    public DiveElement Next => Position + 1 < Elements.Count ? Elements[Position + 1] : null;

    public static DiveSession Start(ExternalNode node, Pawn runner, List<DiveProgram> programs, int costPaid, List<DiveDaemon> daemons)
    {
        var session = new DiveSession { Node = node, Runner = runner, Programs = programs.ToList(), CostPaid = costPaid, Daemons = daemons.ToList() };
        session.ShieldCharges = programs.Count(p => p == DiveProgram.Shield);
        session.DecoyCharges = programs.Count(p => p == DiveProgram.Decoy);
        session.KillerCharges = programs.Count(p => p == DiveProgram.Killer);
        // Daemons: Aegis and Hound add a charge each dive.
        if (session.Daemons.Contains(DiveDaemon.Aegis))
            session.ShieldCharges++;
        if (session.Daemons.Contains(DiveDaemon.Hound))
            session.KillerCharges++;
        session.Storm = GatewayManager.Get()?.StormActive == true;
        session.Build();
        session.ActionsLeft = session.ActionsPerTurn;
        session.AddLog("NR_DiveLogStart".Translate(runner.LabelShort, node.Name));
        return session;
    }

    /// <summary>Lobby, 1-3 code gates, maybe a hidden cache, Black ICE if the node has it, files, a memory unit, the exit.</summary>
    private void Build()
    {
        Elements.Add(new DiveElement { Kind = DiveElementKind.Lobby, Cleared = true });
        int gates = Node.IsBoss ? 4 : 1 + Node.Defense / 2;
        for (int i = 0; i < gates; i++)
            Elements.Add(new DiveElement { Kind = DiveElementKind.Gate, Strength = Node.Defense + Rand.RangeInclusive(0, 2) + (Storm ? 1 : 0) });
        if (Rand.Chance(0.6f))
            Elements.Add(new DiveElement { Kind = DiveElementKind.Files, Hidden = !Daemons.Contains(DiveDaemon.Lens), Cache = true, Cleared = true });
        if (Node.BlackIce)
            Elements.Add(new DiveElement { Kind = DiveElementKind.Ice, Strength = Node.IceStrength + (Storm ? 1 : 0) });
        Elements.Add(new DiveElement { Kind = DiveElementKind.Files, Cleared = true });
        if (Node.Type is ExternalNodeType.Abandoned or ExternalNodeType.Corp or ExternalNodeType.DarkExchange or ExternalNodeType.CorpCore)
            Elements.Add(new DiveElement { Kind = DiveElementKind.Memory, Cleared = true });
        Elements.Add(new DiveElement { Kind = DiveElementKind.Exit, Cleared = true });
    }

    public void AddLog(string line)
    {
        Log.Add(line);
        if (Log.Count > 8)
            Log.RemoveAt(0);
    }

    // ---- Actions ----

    public AcceptanceReport CanMove()
    {
        if (Next == null)
            return "NR_DiveNoFurther".Translate();
        if (Next.Hidden)
            return "NR_DiveHiddenAhead".Translate();
        if (Next.Blocks)
            return "NR_DiveBlocked".Translate();
        return true;
    }

    public void Move()
    {
        Position++;
        AddLog("NR_DiveLogMove".Translate(ElementLabel(Current)));
        Spend(Settings.tracePerAction);
    }

    public AcceptanceReport CanScan()
        => Next is { Hidden: true } || (Node.Honeypot && !HoneypotRevealed) ? true : "NR_DiveNothingToScan".Translate();

    public void Scan()
    {
        if (Next is { Hidden: true })
        {
            Next.Hidden = false;
            AddLog("NR_DiveLogScan".Translate(ElementLabel(Next)));
        }
        else
        {
            // A deep scan: the only way to see a honeypot for what it is.
            HoneypotRevealed = true;
            AddLog("NR_DiveLogHoneypotFound".Translate().Colorize(NetrunUI.Warn));
        }
        Spend(Settings.tracePerAction);
    }

    public AcceptanceReport CanHack()
        => Next is { Blocks: true, Hidden: false } || HunterActive ? true : "NR_DiveNothingToHack".Translate();

    /// <summary>What a hack goes after: the gate or ICE ahead first, otherwise the hunter.</summary>
    public bool HackTargetsHunter => HunterActive && Next is not { Blocks: true, Hidden: false };

    public float CurrentHackChance => HackTargetsHunter
        ? HackChance(new DiveElement { Kind = DiveElementKind.Ice, Strength = Settings.hunterStrength })
        : Next == null ? 0f : HackChance(Next);

    public float HackChance(DiveElement target)
    {
        int skill = Runner?.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
        float chance = Settings.hackBase + skill * Settings.hackPerSkill - target.Strength * Settings.hackPerStrength;
        if (Programs.Contains(DiveProgram.Sword) || Daemons.Contains(DiveDaemon.Blade))
            chance += Settings.swordBonus;
        return Mathf.Clamp(chance, 0.05f, 0.95f);
    }

    public void Hack()
    {
        if (HackTargetsHunter)
        {
            HackHunter();
            return;
        }
        DiveElement target = Next;
        bool sword = Programs.Contains(DiveProgram.Sword);
        if (KillerCharges > 0)
        {
            KillerCharges--;
            target.Cleared = true;
            AddLog("NR_DiveLogKiller".Translate(ElementLabel(target)));
        }
        else if (Rand.Chance(HackChance(target)))
        {
            target.Cleared = true;
            AddLog("NR_DiveLogHackOk".Translate(ElementLabel(target)));
        }
        else
        {
            AddLog("NR_DiveLogHackFail".Translate(ElementLabel(target)).Colorize(NetrunUI.Bad));
            if (target.Kind == DiveElementKind.Ice)
                IceAttack();
        }
        Spend(Settings.tracePerAction * (sword ? 2f : 1f));
    }

    private void HackHunter()
    {
        var hunter = new DiveElement { Kind = DiveElementKind.Ice, Strength = Settings.hunterStrength };
        if (KillerCharges > 0 || Rand.Chance(HackChance(hunter)))
        {
            if (KillerCharges > 0)
                KillerCharges--;
            HunterActive = false;
            AddLog("NR_DiveLogHunterDown".Translate().Colorize(NetrunUI.Ok));
        }
        else
        {
            AddLog("NR_DiveLogHunterHolds".Translate().Colorize(NetrunUI.Bad));
            IceAttack();
        }
        Spend(Settings.tracePerAction);
    }

    public AcceptanceReport CanDownload()
    {
        if (Current.Kind is not (DiveElementKind.Files or DiveElementKind.Memory))
            return "NR_DiveNothingToDownload".Translate();
        return Current.Looted ? "NR_DiveAlreadyLooted".Translate() : true;
    }

    public void Download()
    {
        Current.Looted = true;
        if (Current.Kind == DiveElementKind.Memory && Node.WildAI)
            AskWildAI();
        else if (Current.Kind == DiveElementKind.Memory)
            RollMemory();
        else if (Node.Honeypot && !Current.Cache)
            SpringHoneypot();
        else if (Current.Cache)
            LootCyberdata += Rand.RangeInclusive(1, 2);
        else
            RollFiles();
        AddLog("NR_DiveLogDownload".Translate(LootText()));
        Spend(Settings.tracePerAction);
    }

    /// <summary>Main files pay the dive back: cost (at least the node's entry) times 1.5 / 2 / 3, plus the type's extras.</summary>
    public float RewardMultiplier => Node.RewardMultiplier;

    private void RollFiles()
    {
        int basis = Mathf.Max(CostPaid, Settings.diveBaseCost + Node.Defense / 2);
        LootCyberdata += Mathf.Max(2, Mathf.RoundToInt(basis * RewardMultiplier * Rand.Range(0.85f, 1.15f)));
        if (Node.Type is ExternalNodeType.Corp or ExternalNodeType.CorpCore)
            LootAdvanced += Rand.RangeInclusive(1, 2);
        else if (Node.Type == ExternalNodeType.DarkExchange)
            LootSilver += Rand.RangeInclusive(200, 400);
    }

    /// <summary>
    /// On a clean exit: the boss always pays out; other nodes sometimes leave a special find
    /// (docs/PHASE5_REWARDS.md): a fragment, research, a fixer's service, the hacker group's favor,
    /// a celebrity braindance, a daemon or a core module.
    /// </summary>
    private void RollSpecial()
    {
        if (Node.IsBoss)
        {
            RollBossReward();
            return;
        }
        if (!Rand.Chance(Node.BlackIce ? Settings.specialItemChanceBlackIce : Settings.specialItemChance))
            return;

        var finds = new List<(System.Action find, float weight)>
        {
            (() => LootItems.Add(NetrunDefOf.NR_AIFragment), Settings.findFragment),
            (FindResearch, Settings.findResearch),
            (FindFixerService, Settings.findFixerService),
            (FindGroupFavor, Settings.findGroupFavor),
            (FindCelebrityChip, Settings.findCelebrityChip),
            (() => FindDaemon(), Settings.findDaemon),
            (() => LootItems.Add(NetrunDefOf.NR_AICoreModule), Settings.findCoreModule),
        };
        finds.RandomElementByWeight(f => f.weight).find();
        AddLog("NR_DiveLogSpecial".Translate().Colorize(NetrunUI.Ok));
    }

    /// <summary>The corporate core: a daemon (or a celebrity chip once all are found), maybe a core module.</summary>
    private void RollBossReward()
    {
        if (!FindDaemon())
            FindCelebrityChip();
        if (Rand.Chance(Settings.bossCoreModuleChance))
            LootItems.Add(NetrunDefOf.NR_AICoreModule);
        AddLog("NR_DiveLogBoss".Translate().Colorize(NetrunUI.Ok));
    }

    private void FindResearch()
    {
        ResearchProjectDef project = Find.ResearchManager.GetProject();
        if (project == null)
        {
            LootAdvanced += 2;
            return;
        }
        Find.ResearchManager.AddProgress(project, Settings.researchReward, null);
        Messages.Message("NR_FindResearch".Translate(project.LabelCap), MessageTypeDefOf.PositiveEvent, historical: true);
    }

    private void FindFixerService()
    {
        Fixer fixer = FixerManager.Get()?.Fixers.RandomElementWithFallback();
        if (fixer == null)
        {
            LootAdvanced += 1;
            return;
        }
        fixer.GainReputation(Settings.fixerServiceReputation);
        fixer.FreeScrubs++;
        Messages.Message("NR_FindFixerService".Translate(fixer.Name, Settings.freeScrubHeat.ToString("0")), MessageTypeDefOf.PositiveEvent, historical: true);
    }

    private static void FindGroupFavor()
    {
        GatewayManager gateway = GatewayManager.Get();
        if (gateway == null)
            return;
        gateway.GroupFavor++;
        Messages.Message("NR_FindGroupFavor".Translate(gateway.GroupFavor, NetrunDefOf.NR_Cyberspace.groupFavorForAllies),
            MessageTypeDefOf.PositiveEvent, historical: true);
    }

    /// <summary>A masterful recording of someone famous, delivered with the rest of the haul.</summary>
    private void FindCelebrityChip()
    {
        bool black = Rand.Chance(0.3f);
        Thing chip = ThingMaker.MakeThing(black ? NetrunDefOf.NR_BlackBraindanceChip : NetrunDefOf.NR_BraindanceChip);
        CompBraindanceRecord rec = chip.TryGetComp<CompBraindanceRecord>();
        if (rec != null)
        {
            rec.Author = ("NR_Celebrity_" + Rand.RangeInclusive(0, 7)).Translate();
            rec.Event = (black ? "NR_CelebrityEventDark" : "NR_CelebrityEvent").Translate();
            rec.Mood = black ? BraindanceMood.Dark : BraindanceMood.Bright;
            rec.Strength = 1f;
        }
        if (Runner?.MapHeld is { } map)
            GenPlace.TryPlaceThing(chip, Runner.PositionHeld, map, ThingPlaceMode.Near);
        Messages.Message("NR_FindCelebrity".Translate(chip.LabelCap), MessageTypeDefOf.PositiveEvent, historical: true);
    }

    /// <summary>Unlocks a daemon the colony does not have yet; false when all are already found.</summary>
    private bool FindDaemon()
    {
        GatewayManager gateway = GatewayManager.Get();
        var missing = System.Enum.GetValues(typeof(DiveDaemon)).Cast<DiveDaemon>().Where(d => gateway?.Daemons.Contains(d) == false).ToList();
        if (gateway == null || missing.Count == 0)
            return false;
        DiveDaemon daemon = missing.RandomElement();
        gateway.AddDaemon(daemon);
        Find.LetterStack.ReceiveLetter("NR_FindDaemonLabel".Translate(("NR_Daemon_" + daemon).Translate()),
            "NR_FindDaemon".Translate(("NR_Daemon_" + daemon).Translate(), ("NR_DaemonDesc_" + daemon).Translate()), LetterDefOf.PositiveEvent);
        return true;
    }

    /// <summary>The bait was real only in part: half the haul, a burst of trace, and the hunter comes at once.</summary>
    private void SpringHoneypot()
    {
        RollFiles();
        LootCyberdata = Mathf.RoundToInt(LootCyberdata * Settings.honeypotRewardShare);
        Trace += Settings.honeypotTrace;
        AddLog("NR_DiveLogHoneypot".Translate().Colorize(NetrunUI.Bad));
        if (!HunterActive)
            SpawnHunter();
    }

    /// <summary>A wild AI in the memory unit: tame it, erase it or sell it.</summary>
    private void AskWildAI()
    {
        var node = new DiaNode("NR_WildAI".Translate());
        node.options.Add(new DiaOption("NR_WildAITame".Translate()) { action = () =>
        {
            LootItems.Add(NetrunDefOf.NR_AIFragment);
            Trace += Settings.wildAITameTrace;
            AddLog("NR_DiveLogWildTamed".Translate().Colorize(NetrunUI.Ok));
        }, resolveTree = true });
        node.options.Add(new DiaOption("NR_WildAIErase".Translate(Settings.wildAIEraseAdvanced)) { action = () =>
        {
            LootAdvanced += Settings.wildAIEraseAdvanced;
            AddLog("NR_DiveLogWildErased".Translate());
        }, resolveTree = true });
        node.options.Add(new DiaOption("NR_WildAISell".Translate(Settings.wildAISellSilver)) { action = () =>
        {
            LootSilver += Settings.wildAISellSilver;
            HeatTracker.Get()?.AddHeat(Settings.wildAISellHeat, "NR_WildAISoldLog".Translate());
            AddLog("NR_DiveLogWildSold".Translate());
        }, resolveTree = true });
        Find.WindowStack.Add(new Dialog_NodeTree(node, delayInteractivity: true, title: "NR_WildAITitle".Translate()));
    }

    private void RollMemory()
    {
        if (Rand.Chance(Node.Type == ExternalNodeType.Abandoned ? 0.25f : 0.1f))
            LootItems.Add(NetrunDefOf.NR_AIFragment);
        else if (Rand.Chance(0.05f))
            LootItems.Add(NetrunDefOf.NR_AICoreModule);
        else
            LootAdvanced += Rand.RangeInclusive(1, 2);
    }

    public string LootText()
    {
        var parts = new List<string>();
        if (LootCyberdata > 0)
            parts.Add($"{LootCyberdata} {NetrunDefOf.USH_Cyberdata.label}");
        if (LootAdvanced > 0)
            parts.Add($"{LootAdvanced} {NetrunDefOf.USH_AdvancedCyberdata.label}");
        if (LootSilver > 0)
            parts.Add($"{LootSilver} {ThingDefOf.Silver.label}");
        parts.AddRange(LootItems.Select(d => d.label));
        return parts.Count == 0 ? "-" : parts.ToCommaList();
    }

    /// <summary>Each action costs one network action and raises the trace; when actions run out the node takes its turn.</summary>
    private void Spend(float trace)
    {
        bool stealth = Programs.Contains(DiveProgram.Stealth) || Daemons.Contains(DiveDaemon.Wraith);
        Trace += trace * (stealth ? Settings.stealthTraceFactor : 1f);
        ActionsLeft--;
        if (ActionsLeft <= 0 && !Finished)
            NodeTurn();
    }

    private void NodeTurn()
    {
        int ice = Elements.FindIndex(e => e.Kind == DiveElementKind.Ice && !e.Cleared);
        if (ice >= 0 && Position >= ice - 1 && Rand.Chance(Settings.iceAttackChance))
            IceAttack();
        if (!Finished && HunterActive && Rand.Chance(Settings.hunterAttackChance))
        {
            AddLog("NR_DiveLogHunterStrikes".Translate().Colorize(NetrunUI.Bad));
            IceAttack();
        }
        if (!Finished)
            NodeEvents();

        if (Finished)
            return;

        Turn++;
        ActionsLeft = ActionsPerTurn;
        if (Turn > Settings.maxTurns)
        {
            AddLog("NR_DiveLogTimeout".Translate().Colorize(NetrunUI.Bad));
            Runner?.health.AddHediff(HediffDefOf.Anesthetic);
            End(success: false, keepShare: 1f - Settings.retreatLootLoss);
        }
        else if (ice >= 0 && Position >= ice - 2)
        {
            AddLog("NR_DiveLogIceWatching".Translate().Colorize(NetrunUI.Bad));
        }
    }

    /// <summary>Per node turn: the rival's countdown and the ICE hunter appearing at high Heat.</summary>
    private void NodeEvents()
    {
        if (Node.Rival && !RivalTook)
        {
            int left = Settings.rivalTurns - Turn;
            if (left <= 0)
            {
                RivalTook = true;
                foreach (DiveElement files in Elements.Where(e => e.Kind == DiveElementKind.Files && !e.Cache))
                    files.Looted = true;
                AddLog("NR_DiveLogRivalTook".Translate().Colorize(NetrunUI.Bad));
            }
            else
            {
                AddLog("NR_DiveLogRival".Translate(left).Colorize(NetrunUI.Warn));
            }
        }

        if (!HunterSpawned && Turn >= Settings.hunterFromTurn && (HeatTracker.Get()?.Level ?? HeatLevel.Shadow) >= HeatLevel.Target
            && Rand.Chance(Settings.hunterChance))
            SpawnHunter();
    }

    private void SpawnHunter()
    {
        HunterSpawned = true;
        HunterActive = true;
        AddLog("NR_DiveLogHunter".Translate().Colorize(NetrunUI.Bad));
    }

    /// <summary>Black ICE strikes: a shield absorbs it, a decoy may draw it off, otherwise a Hacking Expansion outcome.</summary>
    private void IceAttack()
    {
        if (ShieldCharges > 0)
        {
            ShieldCharges--;
            AddLog("NR_DiveLogShield".Translate());
            return;
        }
        if (DecoyCharges > 0 && Rand.Chance(Settings.decoyChance))
        {
            DecoyCharges--;
            AddLog("NR_DiveLogDecoy".Translate());
            return;
        }

        AddNetsick(Settings.netsickPerIceHit);
        float roll = Rand.Value;
        if (roll < 0.6f)
        {
            AddHediff("USH_CyberspaceNausea");
            AddLog("NR_DiveLogNausea".Translate(Runner.LabelShort).Colorize(NetrunUI.Bad));
        }
        else if (roll < 0.9f)
        {
            AddHediff("USH_CyberspaceLockOut");
            AddLog("NR_DiveLogLockOut".Translate(Runner.LabelShort).Colorize(NetrunUI.Bad));
            End(success: false, keepShare: 0f);
        }
        else
        {
            AddHediff("USH_CyberspaceComa");
            AddLog("NR_DiveLogComa".Translate(Runner.LabelShort).Colorize(NetrunUI.Bad));
            End(success: false, keepShare: 0f);
        }
    }

    private void AddHediff(string defName)
    {
        HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
        if (def != null && Runner != null && !Runner.Dead)
            Runner.health.AddHediff(def, Runner.health.hediffSet.GetBrain());
    }

    private void AddNetsick(float amount)
    {
        if (Runner == null || Runner.Dead)
            return;
        CyberpodUtility.InCyberpod(Runner, out NetAI coprocessor);
        Hediff netsick = Runner.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick)
                         ?? Runner.health.AddHediff(NetrunDefOf.NR_Netsick);
        netsick.Severity += amount * CyberpodUtility.NetsickFactor(coprocessor);
    }

    public AcceptanceReport CanExit() => Current.Kind == DiveElementKind.Exit ? true : "NR_DiveExitFar".Translate();

    public void Exit()
    {
        AddLog("NR_DiveLogExit".Translate());
        RollSpecial();
        End(success: true, keepShare: 1f);
    }

    /// <summary>Leaving early: from the lobby it is free, deeper in it costs part of the haul and some Netsick.</summary>
    public void Retreat()
    {
        bool safe = Current.Kind == DiveElementKind.Lobby;
        if (!safe)
            AddNetsick(Settings.netsickPerIceHit * 0.5f);
        AddLog("NR_DiveLogRetreat".Translate());
        End(success: false, keepShare: safe ? 1f : 1f - Settings.retreatLootLoss);
    }

    /// <summary>Delivers what was kept next to the pod, adds Heat and finishes the dive.</summary>
    private void End(bool success, float keepShare)
    {
        Finished = true;
        Map map = Runner?.MapHeld;
        CyberpodUtility.InCyberpod(Runner, out NetAI coprocessor);

        if (map != null)
        {
            IntVec3 spot = Runner.PositionHeld;
            Spawn(map, spot, NetrunDefOf.USH_Cyberdata, Mathf.RoundToInt(LootCyberdata * keepShare));
            Spawn(map, spot, NetrunDefOf.USH_AdvancedCyberdata, Mathf.RoundToInt(LootAdvanced * keepShare));
            Spawn(map, spot, ThingDefOf.Silver, Mathf.RoundToInt(LootSilver * keepShare));
            if (keepShare >= 1f)
            {
                foreach (ThingDef item in LootItems)
                    Spawn(map, spot, item, 1);
            }
        }

        if (HunterActive && map != null)
        {
            HeatTracker.Get()?.AddHeat(Settings.hunterEscapeHeat, "NR_HunterFollowedLog".Translate());
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(NetrunDefOf.NR_NetAttack_Breakdown.category, map);
            parms.forced = true;
            Find.Storyteller.incidentQueue.Add(new QueuedIncident(new FiringIncident(NetrunDefOf.NR_NetAttack_Breakdown, null, parms),
                Find.TickManager.TicksGame + Rand.RangeInclusive(6, 12) * GenDate.TicksPerHour));
            Messages.Message("NR_HunterFollowed".Translate(), MessageTypeDefOf.ThreatSmall, historical: true);
        }

        float heat = Trace * Node.HeatPerTrace * CyberpodUtility.HeatFactor(coprocessor);
        if (heat > 0f)
            HeatTracker.Get()?.AddHeat(heat, "NR_DiveHeatReason".Translate(Runner?.LabelShort ?? "?", Node.Name));
        AddNetsick(Settings.netsickPerDive);

        int kept = Mathf.RoundToInt(LootCyberdata * keepShare) + Mathf.RoundToInt(LootAdvanced * keepShare) * NetrunDefOf.NR_Fixers.advancedCyberdataWorth;
        EconomyTracker.Get()?.Note(gained: kept, diveWon: success);

        if (success)
        {
            Runner?.skills?.Learn(SkillDefOf.Intellectual, Settings.successXp, direct: true);
            ContractManager contracts = ContractManager.Get();
            for (int i = 0; i < (Node.Contract ? 2 : 1); i++)
                contracts?.NotifyHackCompleted();
            GatewayManager.Get()?.RemoveNode(Node);
        }

        Messages.Message(success
                ? "NR_DiveDone".Translate(Runner?.LabelShort ?? "?", Node.Name, LootText(), heat.ToString("0"))
                : "NR_DiveFailed".Translate(Runner?.LabelShort ?? "?", Node.Name, heat.ToString("0")),
            success ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent, historical: true);
    }

    private static void Spawn(Map map, IntVec3 spot, ThingDef def, int count)
    {
        while (count > 0)
        {
            Thing stack = ThingMaker.MakeThing(def);
            stack.stackCount = Mathf.Min(count, def.stackLimit);
            count -= stack.stackCount;
            GenPlace.TryPlaceThing(stack, spot, map, ThingPlaceMode.Near);
        }
    }

    public static string ElementLabel(DiveElement element)
        => element.Hidden ? "NR_DiveElement_Hidden".Translate() : ("NR_DiveElement_" + element.Kind).Translate();

    public void ExposeData()
    {
        Scribe_Deep.Look(ref Node, "node");
        Scribe_References.Look(ref Runner, "runner");
        Scribe_Collections.Look(ref Elements, "elements", LookMode.Deep);
        Scribe_Values.Look(ref Position, "position");
        Scribe_Values.Look(ref Turn, "turn", 1);
        Scribe_Values.Look(ref ActionsLeft, "actionsLeft");
        Scribe_Values.Look(ref Trace, "trace");
        Scribe_Collections.Look(ref Programs, "programs", LookMode.Value);
        Scribe_Collections.Look(ref Daemons, "daemons", LookMode.Value);
        Scribe_Values.Look(ref ShieldCharges, "shield");
        Scribe_Values.Look(ref DecoyCharges, "decoy");
        Scribe_Values.Look(ref KillerCharges, "killer");
        Scribe_Values.Look(ref LootCyberdata, "lootCyberdata");
        Scribe_Values.Look(ref LootAdvanced, "lootAdvanced");
        Scribe_Values.Look(ref LootSilver, "lootSilver");
        Scribe_Collections.Look(ref LootItems, "lootItems", LookMode.Def);
        Scribe_Collections.Look(ref Log, "log", LookMode.Value);
        Scribe_Values.Look(ref Finished, "finished");
        Scribe_Values.Look(ref CostPaid, "costPaid");
        Scribe_Values.Look(ref HunterActive, "hunterActive");
        Scribe_Values.Look(ref HunterSpawned, "hunterSpawned");
        Scribe_Values.Look(ref HoneypotRevealed, "honeypotRevealed");
        Scribe_Values.Look(ref RivalTook, "rivalTook");
        Scribe_Values.Look(ref Storm, "storm");
        Elements ??= [];
        Programs ??= [];
        Daemons ??= [];
        LootItems ??= [];
        Log ??= [];
    }
}
