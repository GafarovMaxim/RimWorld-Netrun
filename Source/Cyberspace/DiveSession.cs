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
    /// <summary>HE black ICE breaker: breaks one gate without a roll, but that action leaves double trace.</summary>
    IceBreaker,
    /// <summary>HE exec data: +1 action in each of the first turns.</summary>
    Exec,
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

    // The dive is a graph (docs/DIVE_BRANCHING.md): where this step leads, which branch it is on, where it is drawn.
    /// <summary>Indices of the steps this one leads to: one on a plain path, two at a fork (hard first, easy second).</summary>
    public List<int> Links = [];
    /// <summary>0 on the trunk, 1 on a hard branch, 2 on an easy branch.</summary>
    public int Branch;
    /// <summary>For branch steps: the index of the fork they come from.</summary>
    public int ForkAt = -1;
    /// <summary>The easy branch's hidden passage: downloading it adds to the haul multiplier instead of giving a cache.</summary>
    public bool Passage;
    public int Col;
    /// <summary>Lane: -1 top (hard), 0 middle, 1 bottom (easy), with a little offset so no two dives look alike.</summary>
    public float Row;

    /// <summary>Gates and ICE block the way on until they are hacked.</summary>
    public bool Blocks => (Kind == DiveElementKind.Gate || Kind == DiveElementKind.Ice) && !Cleared;

    public void ExposeData()
    {
        Scribe_Collections.Look(ref Links, "links", LookMode.Value);
        Scribe_Values.Look(ref Branch, "branch");
        Scribe_Values.Look(ref ForkAt, "forkAt", -1);
        Scribe_Values.Look(ref Passage, "passage");
        Scribe_Values.Look(ref Col, "col");
        Scribe_Values.Look(ref Row, "row");
        Links ??= [];
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
    public int BreakerCharges;
    /// <summary>Turns (from the first) that get an extra action from an exec data.</summary>
    public int ExecTurns;
    public int LootCyberdata;
    public int LootAdvanced;
    public int LootSilver;
    public List<ThingDef> LootItems = [];
    public List<string> Log = [];
    public bool Finished;
    /// <summary>The steps walked so far, in order (the lit path on the map).</summary>
    public List<int> Visited = [];
    /// <summary>Forks decided: the fork's index and the chosen branch (1 hard, 2 easy).</summary>
    public List<int> ForkIndex = [];
    public List<int> ForkChoice = [];
    /// <summary>Forks whose branches have been scanned: their numbers are shown.</summary>
    public List<int> ForkScanned = [];
    /// <summary>Added to the path multiplier by downloaded hidden passages.</summary>
    public float PassageBonus;
    /// <summary>Cyberdata paid to start the dive; the main files pay it back with a multiplier.</summary>
    public int CostPaid;

    // Cyberspace events.
    public bool HunterActive;
    public bool HunterSpawned;
    public bool HoneypotRevealed;
    public bool RivalTook;
    public bool Storm;

    // For the dive summary.
    public float HeatAtStart;
    public float NetsickAtStart;
    public bool SpecialFound;
    public bool Retreated;
    /// <summary>ICE hits that got through (not stopped by a shield or a decoy).</summary>
    public int IceHits;
    /// <summary>Node alert: failed gate hacks; at the threshold an ICE patrol wakes up (docs/DIVE_BALANCE.md).</summary>
    public int NodeAlert;
    private bool _cacheFound;

    /// <summary>Nodes this weak have no alert: training grounds of the Outskirts.</summary>
    public bool AlertApplies => Node != null && Node.Defense > Settings.alertFreeDefense;

    /// <summary>Wraith (stealth daemon) lets one more failure slip by.</summary>
    /// <summary>Alarms before the patrol wakes; the difficulty's ICE strength makes patrols quicker or slower.</summary>
    /// <summary>The share of the haul a retreat costs, scaled by the difficulty.</summary>
    private float RetreatLoss => Mathf.Clamp01(Settings.retreatLootLoss * NetrunMod.Settings.RetreatLoss);

    public int AlertThreshold => Mathf.Max(1, Mathf.RoundToInt(Settings.alertThreshold / Mathf.Max(0.25f, NetrunMod.Settings.IceStrength)))
                                 + (Daemons.Contains(DiveDaemon.Wraith) ? 1 : 0);

    public bool PatrolAwake => AlertApplies && NodeAlert >= AlertThreshold;
    public DiveReport Report;

    public int ActionsPerTurn
    {
        get
        {
            float speed = Runner?.GetStatValue(StatDefOf.HackingSpeed) ?? 1f;
            // An overloaded node is slow to react: one more action a turn.
            return (speed < 0.8f ? 1 : speed < 1.3f ? 2 : 3) + (Node?.Overloaded == true ? 1 : 0);
        }
    }

    public DiveElement Current => Elements[Position];

    /// <summary>
    /// Extra actions in a turn: an exec data in the first turns, and HE's optimization module in the first turn only
    /// (the module already speeds up hacking; in a dive it only helps the opening move).
    /// </summary>
    public int ExtraActions(int turn)
    {
        int extra = turn <= ExecTurns ? 1 : 0;
        if (turn == 1)
            extra += DiveTraits.FirstTurnActions(Runner) + (Precept_NetArchon.Holds(Runner) ? 1 : 0);
        if (turn == 1 && DefDatabase<HediffDef>.GetNamedSilentFail("USH_InstalledOptimizationModule") is { } module
            && Runner?.health?.hediffSet?.HasHediff(module) == true)
            extra++;
        return extra;
    }

    /// <summary>The step ahead: the only link, or the chosen branch at a fork; null at an undecided fork or the end.</summary>
    public DiveElement Next => NextIndex(Position) is var i and >= 0 ? Elements[i] : null;

    public int NextIndex(int index)
    {
        List<int> links = Elements[index].Links;
        if (links.Count == 1)
            return links[0];
        if (links.Count > 1)
        {
            int at = ForkIndex.IndexOf(index);
            return at >= 0 ? links[ForkChoice[at] - 1] : -1;
        }
        return -1;
    }

    /// <summary>The step <paramref name="steps"/> ahead along the chosen path, or null.</summary>
    public DiveElement Ahead(int steps)
    {
        int i = Position;
        for (int s = 0; s < steps && i >= 0; s++)
            i = NextIndex(i);
        return i >= 0 ? Elements[i] : null;
    }

    public bool IsFork(int index) => Elements[index].Links.Count > 1;

    /// <summary>Standing at a fork with no branch chosen yet.</summary>
    public bool AtUndecidedFork => IsFork(Position) && !ForkIndex.Contains(Position);

    public int ChoiceAt(int fork) => ForkIndex.IndexOf(fork) is var at and >= 0 ? ForkChoice[at] : 0;

    /// <summary>Number of forks for this node: small none, medium one, large and the corporate core two.</summary>
    public static int ForksFor(ExternalNode node) => node.IsBoss || node.Ancient ? 2 : node.Defense <= 2 ? 0 : node.Defense <= 4 ? 1 : 2;

    public int Forks => Elements.Count(e => e.Links.Count > 1);

    /// <summary>Turns: the setting, plus a couple per fork (branched nodes are longer).</summary>
    public int MaxTurns => Settings.maxTurns + Forks * Settings.branchExtraTurns;

    /// <summary>The haul multiplier of the chosen path: the mean of the branches taken plus hidden passages.</summary>
    public float PathMultiplier
    {
        get
        {
            if (ForkChoice.Count == 0)
                return 1f;
            float mean = (float)ForkChoice.Average(c => c == 1 ? Settings.branchHardReward : Settings.branchEasyReward);
            return mean + PassageBonus;
        }
    }

    public static DiveSession Start(ExternalNode node, Pawn runner, List<DiveProgram> programs, int costPaid, List<DiveDaemon> daemons)
    {
        var session = new DiveSession { Node = node, Runner = runner, Programs = programs.ToList(), CostPaid = costPaid, Daemons = daemons.ToList() };
        NetrunSound.Play("Dive_Start");
        session.ShieldCharges = programs.Count(p => p == DiveProgram.Shield);
        session.DecoyCharges = programs.Count(p => p == DiveProgram.Decoy);
        session.KillerCharges = programs.Count(p => p == DiveProgram.Killer);
        // Daemons: Aegis and Hound add a charge each dive.
        if (session.Daemons.Contains(DiveDaemon.Aegis))
            session.ShieldCharges++;
        if (session.Daemons.Contains(DiveDaemon.Hound))
            session.KillerCharges++;
        session.BreakerCharges = programs.Count(p => p == DiveProgram.IceBreaker);
        session.ExecTurns = programs.Contains(DiveProgram.Exec) ? Settings.execExtraActionTurns : 0;
        session.Storm = GatewayManager.Get()?.StormActive == true || NetMapManager.Get()?.NodeOf(node)?.Storm == true;
        session.HeatAtStart = HeatTracker.Get()?.Heat ?? 0f;
        session.NetsickAtStart = DiveReport.NetsickOf(runner);
        session.Build();
        // A watched corporate confession: this dive shows every hidden step, the forks' numbers and any trap.
        if (UniqueBraindanceManager.Get() is { RevealNextDive: true } ub)
        {
            ub.RevealNextDive = false;
            foreach (DiveElement e in session.Elements)
                e.Hidden = false;
            for (int i = 0; i < session.Elements.Count; i++)
                if (session.IsFork(i))
                    session.ForkScanned.Add(i);
            session.HoneypotRevealed = node.Honeypot;
            session.AddLog("NR_DiveLogConfession".Translate().Colorize(NetrunUI.Warn));
        }
        session.ActionsLeft = UnityEngine.Mathf.Max(1, session.ActionsPerTurn + session.ExtraActions(1));
        session.AddLog("NR_DiveLogStart".Translate(runner.LabelShort, node.Name));
        return session;
    }

    private int Add(DiveElement e, int col, float row)
    {
        e.Col = col;
        e.Row = row + Rand.Range(-0.18f, 0.18f);
        Elements.Add(e);
        return Elements.Count - 1;
    }

    private static void Link(DiveElement from, int to) => from.Links.Add(to);

    private int GateStrength(int delta) => Mathf.Max(1, Node.Defense + delta + Rand.RangeInclusive(0, 2) + (Storm ? 1 : 0) - (Node.OpenDoor ? 1 : 0));

    /// <summary>
    /// Medium and large nodes (docs/DIVE_BRANCHING.md): the trunk splits into a hard branch (a stronger gate, a cache,
    /// Black ICE) and an easy one (a weaker gate, a hidden passage), the branches meet at a junction cache; after the last fork come the
    /// main files, a memory unit and the exit.
    /// </summary>
    private void BuildBranched(int forks)
    {
        int col = 0;
        int last = Add(new DiveElement { Kind = DiveElementKind.Lobby, Cleared = true }, col++, 0f);
        int trunkGates = Node.IsBoss ? 2 : 1;
        for (int i = 0; i < trunkGates; i++)
        {
            int g = Add(new DiveElement { Kind = DiveElementKind.Gate, Strength = GateStrength(0) }, col++, 0f);
            Link(Elements[last], g);
            last = g;
        }
        for (int f = 0; f < forks; f++)
        {
            int fork = last;
            int c0 = col;
            // Hard: a stronger gate, a visible cache, Black ICE.
            int h1 = Add(new DiveElement { Kind = DiveElementKind.Gate, Strength = GateStrength(1), Branch = 1, ForkAt = fork }, c0, -1f);
            int h2 = Add(new DiveElement { Kind = DiveElementKind.Files, Cache = true, Cleared = true, Branch = 1, ForkAt = fork }, c0 + 1, -1f);
            // The second fork's Black ICE is one weaker: two full-strength ICE on one path made the hard way a losing bet
            // (tools/dive_branch_check.py).
            int iceStrength = Mathf.Max(1, (Node.BlackIce ? Node.IceStrength : Node.Defense + 1) + (Storm ? 1 : 0) - (f > 0 ? 1 : 0));
            int h3 = Add(new DiveElement { Kind = DiveElementKind.Ice, Strength = iceStrength, Branch = 1, ForkAt = fork }, c0 + 2, -1f);
            // Easy: a weaker gate and a hidden passage.
            int e1 = Add(new DiveElement { Kind = DiveElementKind.Gate, Strength = GateStrength(-1), Branch = 2, ForkAt = fork }, c0, 1f);
            int e2 = Add(new DiveElement { Kind = DiveElementKind.Files, Cache = true, Passage = true, Cleared = true,
                Hidden = !Daemons.Contains(DiveDaemon.Lens), Branch = 2, ForkAt = fork }, c0 + 2, 1f);
            Link(Elements[fork], h1);
            Link(Elements[fork], e1);
            Link(Elements[h1], h2);
            Link(Elements[h2], h3);
            Link(Elements[e1], e2);
            col = c0 + 3;
            // The branches meet: at a small junction cache (the next fork), or at the main files after the last fork.
            int merge = f < forks - 1
                ? Add(new DiveElement { Kind = DiveElementKind.Files, Cache = true, Cleared = true }, col++, 0f)
                : Add(new DiveElement { Kind = DiveElementKind.Files, Cleared = true }, col++, 0f);
            Link(Elements[h3], merge);
            Link(Elements[e2], merge);
            last = merge;
        }
        if (Node.Type is ExternalNodeType.Abandoned or ExternalNodeType.Corp or ExternalNodeType.DarkExchange or ExternalNodeType.CorpCore)
        {
            int m = Add(new DiveElement { Kind = DiveElementKind.Memory, Cleared = true }, col++, 0f);
            Link(Elements[last], m);
            last = m;
        }
        int exit = Add(new DiveElement { Kind = DiveElementKind.Exit, Cleared = true }, col, 0f);
        Link(Elements[last], exit);
    }

    /// <summary>Old saves and small nodes: a single path; the steps zigzag a little so the map is not a staircase.</summary>
    private void Linearize()
    {
        for (int i = 0; i < Elements.Count; i++)
        {
            DiveElement e = Elements[i];
            e.Links = i + 1 < Elements.Count ? [i + 1] : [];
            e.Col = i;
            e.Row = (i % 2 == 0 ? -0.3f : 0.3f) + Rand.Range(-0.15f, 0.15f);
        }
    }

    /// <summary>Lobby, 1-3 code gates, maybe a hidden cache, Black ICE if the node has it, files, a memory unit, the exit.</summary>
    private void Build()
    {
        int forks = ForksFor(Node);
        if (forks > 0)
        {
            BuildBranched(forks);
            Visited.Add(0);
            return;
        }
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
        Linearize();
        Visited.Add(0);
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
        if (AtUndecidedFork)
            return "NR_DiveChoosePath".Translate();
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
        Position = NextIndex(Position);
        Visited.Add(Position);
        NetrunSound.Play("Dive_Move");
        AddLog("NR_DiveLogMove".Translate(ElementLabel(Current)));
        Spend(Settings.tracePerAction);
    }

    /// <summary>The step the runner came from (-1 at the start).</summary>
    private int PreviousIndex => Visited.Count >= 2 && Visited[Visited.Count - 1] == Position ? Visited[Visited.Count - 2] : -1;

    /// <summary>A step back is allowed, but never back out of a branch already chosen at a fork.</summary>
    public AcceptanceReport CanBack()
    {
        int prev = PreviousIndex;
        if (prev < 0)
            return "NR_DiveBackStart".Translate();
        if (ForkIndex.Contains(prev))
            return "NR_DiveBackFork".Translate();
        return true;
    }

    /// <summary>One step back (to pick up files left behind); it takes the rest of the turn.</summary>
    public void Back()
    {
        if (!CanBack().Accepted)
            return;
        Visited.RemoveAt(Visited.Count - 1);
        Position = Visited[Visited.Count - 1];
        NetrunSound.Play("Dive_Back");
        AddLog("NR_DiveLogBack".Translate(ElementLabel(Current)));
        ActionsLeft = 1;
        Spend(Settings.tracePerAction);
    }

    public AcceptanceReport CanScan()
        => Next is { Hidden: true } || (AtUndecidedFork && !ForkScanned.Contains(Position)) || (Node.Honeypot && !HoneypotRevealed)
            ? true : "NR_DiveNothingToScan".Translate();

    public void Scan()
    {
        NetrunSound.Play("UI_Scan");
        // Interference: a scan takes two actions.
        if (Node.Interference && ActionsLeft > 1)
            ActionsLeft--;
        // A forgotten cache turns up on the first scan.
        if (Node.ForgottenCache && !_cacheFound)
        {
            _cacheFound = true;
            int found = Rand.RangeInclusive(3, 6);
            LootCyberdata += found;
            AddLog("NR_DiveLogForgottenCache".Translate(found).Colorize(NetrunUI.Ok));
        }
        if (AtUndecidedFork && !ForkScanned.Contains(Position))
        {
            ForkScanned.Add(Position);
            AddLog("NR_DiveLogForkScan".Translate());
        }
        else if (Next is { Hidden: true })
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
        // The difficulty's ICE strength scales ICE (not gates).
        float strength = target.Kind == DiveElementKind.Ice ? target.Strength * NetrunMod.Settings.IceStrength * NetrunMod.Settings.NewcomerEase : target.Strength;
        float chance = Settings.hackBase + skill * Settings.hackPerSkill - strength * Settings.hackPerStrength;
        float bonus = 0f;
        if (Programs.Contains(DiveProgram.Sword) || Daemons.Contains(DiveDaemon.Blade))
            bonus += Settings.swordBonus;
        if (DefDatabase<HediffDef>.GetNamedSilentFail("NR_UBH_ZeroRun") is { } zero && Runner?.health.hediffSet.HasHediff(zero) == true)
            bonus += NetrunDefOf.NR_UniqueBraindance.zeroRunHackBonus;
        bonus += DiveEffects.HackBonus(Runner) + NetrunTraits.HackBonus(Runner);
        // Bonuses stack up to a cap, so gear never makes the hardest nodes trivial; penalties are not capped.
        chance += Mathf.Min(bonus, Settings.hackBonusCap);
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
        if (target.Kind == DiveElementKind.Gate && BreakerCharges > 0)
        {
            BreakerCharges--;
            target.Cleared = true;
            AddLog("NR_DiveLogBreaker".Translate(ElementLabel(target)));
            Spend(Settings.tracePerAction * 2f);
            return;
        }
        if (KillerCharges > 0)
        {
            KillerCharges--;
            target.Cleared = true;
            AddLog("NR_DiveLogKiller".Translate(ElementLabel(target)));
        }
        else if (Rand.Chance(HackChance(target)))
        {
            target.Cleared = true;
            NetrunSound.Play("UI_Hack");
            AddLog("NR_DiveLogHackOk".Translate(ElementLabel(target)));
        }
        else
        {
            AddLog("NR_DiveLogHackFail".Translate(ElementLabel(target)).Colorize(NetrunUI.Bad));
            // ICE answers a failed hack with its own strike sound; a gate just slips.
            if (target.Kind == DiveElementKind.Ice)
                IceAttack();
            else
            {
                NetrunSound.Play("Dive_HackFail");
                if (target.Kind == DiveElementKind.Gate)
                    RaiseAlert();
            }
        }
        Spend(Settings.tracePerAction * (sword ? 2f : 1f));
    }

    /// <summary>A failed gate raises the node alert; the patrol wakes at the threshold.</summary>
    /// <summary>Achievement counters for a finished dive (docs/TECH_DETAILS.md, section 3.5).</summary>
    private void DiveSignals(bool success)
    {
        NetrunCounters counters = NetrunCounters.Get();
        if (counters == null)
            return;
        // The first dive ever, right in the Red Zone.
        if (counters.Value("Dives") == 0 && HeatAtStart >= 95f)
            NetrunSignals.Increment("RookieRedZone");
        NetrunSignals.Increment("Dives");
        int streak = success && !Retreated ? counters.Value("CleanStreakNow") + 1 : 0;
        counters.Set("CleanStreakNow", streak);
        NetrunSignals.Max("CleanStreak", streak);
        if (success && Node.BlackIce)
            NetrunSignals.Increment("BlackICEPassed");
        if (success && Node.IsBoss)
            NetrunSignals.Increment("CorpCores");
    }

    private void RaiseAlert()
    {
        if (!AlertApplies || PatrolAwake)
            return;
        NodeAlert++;
        if (PatrolAwake)
            AddLog("NR_DiveLogPatrolAwake".Translate().Colorize(NetrunUI.Bad));
        else
            AddLog("NR_DiveLogAlert".Translate(NodeAlert, AlertThreshold).Colorize(NetrunUI.Warn));
    }

    /// <summary>
    /// The ICE patrol strikes: a shield (or Aegis) takes it, otherwise the runner is thrown out with half the haul
    /// and HE nausea. No coma and no lockout: only Black ICE does that.
    /// </summary>
    private void PatrolStrike()
    {
        if (ShieldCharges > 0)
        {
            ShieldCharges--;
            AddLog("NR_DiveLogPatrolShield".Translate());
            return;
        }
        NetrunSound.Play("Dive_ICE");
        AddHediff("USH_CyberspaceNausea");
        AddLog("NR_DiveLogPatrolStrike".Translate(Runner?.LabelShort ?? "?").Colorize(NetrunUI.Bad));
        Retreated = true;
        End(success: false, keepShare: Settings.alertPatrolKeepShare);
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

    // ---- Forks ----

    /// <summary>What a branch holds, for the choice cards: its haul multiplier, Black ICE strength (0 if none), trace.</summary>
    public (float reward, int ice, float trace) BranchInfo(int fork, int branch)
    {
        int first = Elements[fork].Links[branch - 1];
        int ice = 0;
        for (int i = first; i >= 0 && Elements[i].Branch == branch; i = Elements[i].Links.Count > 0 ? Elements[i].Links[0] : -1)
            if (Elements[i].Kind == DiveElementKind.Ice)
                ice = Elements[i].Strength;
        return branch == 1
            ? (Settings.branchHardReward, ice, Settings.branchHardTrace)
            : (Settings.branchEasyReward, ice, Settings.branchEasyTrace);
    }

    /// <summary>Takes a branch at the fork here. The choice is final and costs no action; the branch adds its trace.</summary>
    public void Choose(int branch)
    {
        if (!AtUndecidedFork)
            return;
        ForkIndex.Add(Position);
        ForkChoice.Add(branch);
        bool stealth = Programs.Contains(DiveProgram.Stealth) || Daemons.Contains(DiveDaemon.Wraith);
        Trace += (branch == 1 ? Settings.branchHardTrace : Settings.branchEasyTrace) * (stealth ? Settings.stealthTraceFactor : 1f) * DiveTraits.TraceFactor(Runner);
        AddLog((branch == 1 ? "NR_DiveLogPathHard" : "NR_DiveLogPathEasy").Translate().Colorize(branch == 1 ? NetrunUI.Bad : NetrunUI.Ok));
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
        NetrunSound.Play("UI_Download");
        if (Current.Kind == DiveElementKind.Memory && Node.WildAI)
            AskWildAI();
        else if (Current.Kind == DiveElementKind.Memory)
            RollMemory();
        else if (Node.Honeypot && !Current.Cache)
            SpringHoneypot();
        else if (Current.Passage)
        {
            PassageBonus += Settings.branchPassageBonus;
            AddLog("NR_DiveLogPassage".Translate(Settings.branchPassageBonus.ToString("0.0#")).Colorize(NetrunUI.Ok));
        }
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
        float mult = Forks > 0 ? Node.BranchBaseMultiplier * PathMultiplier : RewardMultiplier;
        LootCyberdata += Mathf.Max(2, Mathf.RoundToInt(basis * mult * Rand.Range(0.85f, 1.15f)));
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
        if (Node.IsBoss || Node.Ancient)
        {
            RollBossReward();
            if (Node.Ancient && Rand.Chance(AIBalance.FragmentChance(0.5f)))
                LootItems.Add(NetrunDefOf.NR_AIFragment);
            return;
        }
        // Campus archives always carry some research data (docs/CYBERSPACE_V2.md, section 9.2).
        if (Node.District == District.Campus && Node.Type == ExternalNodeType.Archive)
            FindResearch(0.5f);
        float chance = Node.BlackIce ? Settings.specialItemChanceBlackIce : Settings.specialItemChance;
        if (Node.District == District.Dump)
            chance *= 1.5f;
        if (!Rand.Chance(chance))
            return;

        District d = Node.District;
        var finds = new List<(System.Action find, float weight)>
        {
            (() => LootItems.Add(NetrunDefOf.NR_AIFragment), Settings.findFragment * NetrunMod.Settings.AIAvailability * (d == District.Dump ? 2f : 1f)),
            (() => FindResearch(), Settings.findResearch * (d == District.Campus ? 3f : 1f)),
            (FindFixerService, Settings.findFixerService),
            (FindGroupFavor, Settings.findGroupFavor),
            (() => { if (!(Rand.Chance(NetrunDefOf.NR_UniqueBraindance.diveFindChance) && FindUnique())) FindCelebrityChip(); }, Settings.findCelebrityChip * (d == District.Entertainment ? 3f : 1f)),
            (() => FindDaemon(), Settings.findDaemon),
            (() => LootItems.Add(NetrunDefOf.NR_AICoreModule), Settings.findCoreModule),
        };
        finds.RandomElementByWeight(f => f.weight).find();
        SpecialFound = true;
        AddLog("NR_DiveLogSpecial".Translate().Colorize(NetrunUI.Ok));
    }

    /// <summary>The corporate core: a daemon (or a celebrity chip once all are found), maybe a core module.</summary>
    private void RollBossReward()
    {
        // A daemon or a unique recording (whichever is left), otherwise a celebrity chip.
        bool unique = Rand.Chance(NetrunDefOf.NR_UniqueBraindance.bossChance) && FindUnique();
        if (!unique && !FindDaemon() && !FindUnique())
            FindCelebrityChip();
        if (Rand.Chance(Settings.bossCoreModuleChance * (Node.Ancient ? 1.5f : 1f)))
            LootItems.Add(NetrunDefOf.NR_AICoreModule);
        SpecialFound = true;
        AddLog("NR_DiveLogBoss".Translate().Colorize(NetrunUI.Ok));
    }

    private void FindResearch(float share = 1f)
    {
        ResearchProjectDef project = Find.ResearchManager.GetProject();
        if (project == null)
        {
            LootAdvanced += Mathf.Max(1, Mathf.RoundToInt(2 * share));
            return;
        }
        Find.ResearchManager.AddProgress(project, Settings.researchReward * share, null);
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
        // The favor comes from a crew: relations with it warm up too.
        NetFactions.AffectCrew(NetFactions.BestCrew() ?? NetFactions.Crews.RandomElementWithFallback(), 3, "NR_CrewGoodwillFind");
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

    /// <summary>A unique braindance recording not found yet joins the haul; false when all are out in the world.</summary>
    private bool FindUnique()
    {
        ThingDef def = UniqueBraindanceManager.Get()?.RandomUnfound();
        if (def == null || LootItems.Contains(def))
            return false;
        LootItems.Add(def);
        AddLog("NR_UB_Found".Translate(def.LabelCap).Colorize(NetrunUI.Warn));
        return true;
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
        if (Rand.Chance(AIBalance.FragmentChance(Node.Type == ExternalNodeType.Abandoned ? Settings.memoryFragmentAbandoned : Settings.memoryFragmentOther)))
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
        Trace += trace * (stealth ? Settings.stealthTraceFactor : 1f) * DiveTraits.TraceFactor(Runner) * (Node.Mirrored ? 2f : 1f);
        ActionsLeft--;
        if (ActionsLeft <= 0 && !Finished)
            NodeTurn();
    }

    private void NodeTurn()
    {
        // An overloaded node may fall over: the dive ends, but what was taken is kept.
        if (Node.Overloaded && Rand.Chance(Settings.overloadCrashChance))
        {
            AddLog("NR_DiveLogOverloadCrash".Translate().Colorize(NetrunUI.Warn));
            End(success: false, keepShare: 1f);
            return;
        }
        bool iceNext = Next is { Kind: DiveElementKind.Ice, Cleared: false };
        bool iceNear = iceNext || Ahead(2) is { Kind: DiveElementKind.Ice, Cleared: false };
        if (iceNext && Rand.Chance(Settings.iceAttackChance))
            IceAttack();
        if (!Finished && HunterActive && Rand.Chance(Settings.hunterAttackChance))
        {
            AddLog("NR_DiveLogHunterStrikes".Translate().Colorize(NetrunUI.Bad));
            IceAttack();
        }
        if (!Finished && PatrolAwake && Rand.Chance(Settings.alertPatrolChance))
            PatrolStrike();
        if (!Finished)
            NodeEvents();

        if (Finished)
            return;

        Turn++;
        ActionsLeft = Mathf.Max(1, ActionsPerTurn + ExtraActions(Turn));
        if (Turn > MaxTurns)
        {
            AddLog("NR_DiveLogTimeout".Translate().Colorize(NetrunUI.Bad));
            Runner?.health.AddHediff(HediffDefOf.Anesthetic);
            End(success: false, keepShare: 1f - RetreatLoss);
        }
        else if (iceNear)
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
            && Rand.Chance(Settings.hunterChance * NetrunMod.Settings.CyberEventFrequency))
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
        FirstHints.Show("Ice", "NR_Help_Gateway_Ice");
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
        IceHits++;
        TryBurnCyberlink();
        NetrunSound.Play("Dive_ICE");
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
            NetsickManager.WakeFromPod(Runner);
        }
        else
        {
            AddHediff("USH_CyberspaceComa");
            NetrunRecords.Add(Runner, NetrunRecords.NR_Record_NetComas);
            AddLog("NR_DiveLogComa".Translate(Runner.LabelShort).Colorize(NetrunUI.Bad));
            End(success: false, keepShare: 0f);
            // Out of the pod, so the coma runs its course (a Vitality pod would hold it frozen).
            NetsickManager.WakeFromPod(Runner);
        }
    }

    /// <summary>Black ICE can fry the cyberlink itself: a small chance per hit that the implant is gone.</summary>
    private void TryBurnCyberlink()
    {
        if (!Node.BlackIce || Runner == null || Runner.Dead || !Rand.Chance(Settings.blackIceCyberlinkLoss))
            return;
        Hediff link = Runner.health.hediffSet.hediffs.FirstOrDefault(h => h.def.defName is "USH_InstalledCyberlink" or "USH_InstalledAdvancedCyberlink");
        if (link == null)
            return;
        Runner.health.RemoveHediff(link);
        NetrunRecords.Add(Runner, NetrunRecords.NR_Record_LinkBurns);
        AddLog("NR_DiveLogLinkLost".Translate(Runner.LabelShort, link.LabelBase).Colorize(NetrunUI.Bad));
        Find.LetterStack.ReceiveLetter("NR_LinkLostLabel".Translate(), "NR_LinkLost".Translate(Runner.LabelShort, link.LabelBase), LetterDefOf.NegativeEvent, Runner);
    }

    /// <summary>Neural feedback from a failed dive: a few small burns on outer body parts.</summary>
    private void AddFailBurns()
    {
        if (Runner == null || Runner.Dead)
            return;
        List<BodyPartRecord> parts = Runner.health.hediffSet.GetNotMissingParts()
            .Where(p => p.depth == BodyPartDepth.Outside && p.coverageAbs > 0f).ToList();
        HediffDef burnDef = DefDatabase<HediffDef>.GetNamedSilentFail("Burn");
        if (parts.Count == 0 || burnDef == null)
            return;
        int count = Settings.failBurns.RandomInRange;
        for (int i = 0; i < count; i++)
        {
            BodyPartRecord part = parts.RandomElement();
            var burn = (Hediff_Injury)HediffMaker.MakeHediff(burnDef, Runner, part);
            burn.Severity = Settings.failBurnDamage.RandomInRange;
            Runner.health.AddHediff(burn, part);
        }
        AddLog("NR_DiveLogBurns".Translate(Runner.LabelShort, count).Colorize(NetrunUI.Bad));
    }

    private void AddHediff(string defName)
    {
        if (defName == "USH_CyberspaceNausea" && DiveTraits.NauseaImmune(Runner))
            return;
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
        netsick.Severity += amount * CyberpodUtility.NetsickFactor(coprocessor) * NetrunTraits.GainFactor(Runner);
    }

    /// <summary>Dev: end the dive at once, as a clean exit (with the special roll) or a failure.</summary>
    public void DevFinish(bool success)
    {
        if (Finished)
            return;
        if (success)
        {
            RollFiles();
            RollSpecial();
            End(success: true, keepShare: 1f);
        }
        else
        {
            End(success: false, keepShare: 0f);
        }
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
        bool safe = Current.Kind == DiveElementKind.Lobby || (AtUndecidedFork && ForkIndex.Count == 0);
        if (!safe)
            AddNetsick(Settings.netsickPerIceHit * 0.5f);
        AddLog("NR_DiveLogRetreat".Translate());
        Retreated = true;
        End(success: false, keepShare: safe ? 1f : 1f - RetreatLoss, sound: "Dive_Exit");
    }

    /// <summary>Delivers what was kept next to the pod, adds Heat and finishes the dive.</summary>
    /// <param name="sound">The pack sound for a retreat (Exit); otherwise the victory or failure sound.</param>
    private void End(bool success, float keepShare, string sound = null)
    {
        Finished = true;
        NetrunSound.Play(sound ?? (success ? "Dive_Success" : "Dive_Fail"));
        Map map = Runner?.MapHeld;
        CyberpodUtility.InCyberpod(Runner, out NetAI coprocessor);

        if (map != null)
        {
            IntVec3 spot = Runner.PositionHeld;
            float mirror = Node.Mirrored ? 2f : 1f;
            Spawn(map, spot, NetrunDefOf.USH_Cyberdata, Mathf.RoundToInt(LootCyberdata * keepShare * mirror * (GatewayManager.Get()?.LootFactor ?? 1f)
                * (Node.District == District.Orbit ? NetMapManager.Settings.orbitLoot : 1f)));
            Spawn(map, spot, NetrunDefOf.USH_AdvancedCyberdata, Mathf.RoundToInt(LootAdvanced * keepShare * mirror));
            Spawn(map, spot, ThingDefOf.Silver, Mathf.RoundToInt(LootSilver * keepShare * mirror * (Node.Auction ? 1.5f : 1f)));
            if (keepShare >= 1f)
            {
                foreach (ThingDef item in LootItems)
                    Spawn(map, spot, item, 1);
            }
        }

        if (HunterActive && map != null)
        {
            HeatTracker.Get()?.AddHeat(Settings.hunterEscapeHeat, "NR_HunterFollowedLog".Translate(), HeatSource.Dive);
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(NetrunDefOf.NR_NetAttack_Breakdown.category, map);
            parms.forced = true;
            Find.Storyteller.incidentQueue.Add(new QueuedIncident(new FiringIncident(NetrunDefOf.NR_NetAttack_Breakdown, null, parms),
                Find.TickManager.TicksGame + Rand.RangeInclusive(6, 12) * GenDate.TicksPerHour));
            Messages.Message("NR_HunterFollowed".Translate(), MessageTypeDefOf.ThreatSmall, historical: true);
        }

        float heat = Trace * Node.HeatPerTrace * CyberpodUtility.HeatFactor(coprocessor) * (GatewayManager.Get()?.TraceFactor ?? 1f);
        if (heat > 0f)
            HeatTracker.Get()?.AddHeat(heat, "NR_DiveHeatReason".Translate(Runner?.LabelShort ?? "?", Node.Name), HeatSource.Dive);
        AddNetsick(Settings.netsickPerDive);
        if (!success && !Retreated)
            AddFailBurns();

        int kept = Mathf.RoundToInt(LootCyberdata * keepShare) + Mathf.RoundToInt(LootAdvanced * keepShare) * NetrunDefOf.NR_Fixers.advancedCyberdataWorth;
        EconomyTracker.Get()?.Note(gained: kept, diveWon: success);

        if (success)
        {
            Runner?.skills?.Learn(SkillDefOf.Intellectual, Settings.successXp, direct: true);
            ContractManager contracts = ContractManager.Get();
            for (int i = 0; i < (Node.Contract ? 2 : 1); i++)
                contracts?.NotifyHackCompleted();
            GatewayManager.Get()?.RemoveNode(Node);
            if (Node.CounterHack)
                MapEventsManager.Get()?.CounterHackWon();
            if (Node.IsBoss)
            {
                GatewayManager.Get()?.CorpLockdown();
                NetMapManager.Get()?.OpenWall();
            }
        }

        NetrunRecords.Add(Runner, NetrunRecords.NR_Record_Dives);
        if (success)
        {
            NetrunRecords.Add(Runner, NetrunRecords.NR_Record_CleanExits);
            if (Node.BlackIce)
                NetrunRecords.Add(Runner, NetrunRecords.NR_Record_BlackICEPassed);
            if (Node.IsBoss)
            {
                NetrunRecords.Add(Runner, NetrunRecords.NR_Record_CorpCores);
                if (DefDatabase<TaleDef>.GetNamedSilentFail("NR_Tale_CorpCore") is { } tale && Runner != null)
                    TaleRecorder.RecordTale(tale, Runner);
            }
        }
        PawnNetState.Get()?.NotifyDive(Runner, IceHits);
        CorpManager.Get()?.NotifyDive(Node, success);
        NetRanking.Get()?.NotifyDive(this, success);
        if (Node.Rival)
            CrewSim.Get()?.NotifyRace(this, success && !RivalTook);
        if (success && Node.InterceptPacketId >= 0)
            IntrusionManager.Get()?.Intercepted(Node.InterceptPacketId, Runner);

        Report = BuildReport(success, keepShare, HunterActive && map != null);
        DiveEffects.Apply(this, success, Report);
        ContractManager.Get()?.NotifyDive(Runner, success);
        NetCulture.RecordDive(Runner);
        DiveTraits.Check(Runner);
        NetIdeo.AfterDive(Runner, Node.BlackIce, success);
        AIQuests.NotifyDive(this, success);
        DiveSignals(success);

        Messages.Message(success
                ? "NR_DiveDone".Translate(Runner?.LabelShort ?? "?", Node.Name, LootText(), heat.ToString("0"))
                : "NR_DiveFailed".Translate(Runner?.LabelShort ?? "?", Node.Name, heat.ToString("0")),
            success ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent, historical: true);
    }

    private DiveReport BuildReport(bool success, float keepShare, bool hunterFollowed)
    {
        int Kept(int n) => Mathf.RoundToInt(n * keepShare);
        var report = new DiveReport
        {
            NodeName = Node.Name,
            EndTick = Find.TickManager.TicksGame,
            RunnerName = Runner?.LabelShort ?? "?",
            Success = success,
            Retreated = Retreated,
            Turns = Turn,
            CostPaid = CostPaid,
            Cyberdata = Kept(LootCyberdata),
            Advanced = Kept(LootAdvanced),
            Silver = Kept(LootSilver),
            Items = keepShare >= 1f ? LootItems.ToList() : [],
            LostCyberdata = LootCyberdata - Kept(LootCyberdata),
            LostAdvanced = LootAdvanced - Kept(LootAdvanced),
            LostSilver = LootSilver - Kept(LootSilver),
            LostItems = keepShare >= 1f ? 0 : LootItems.Count,
            HeatBefore = HeatAtStart,
            HeatAfter = HeatTracker.Get()?.Heat ?? 0f,
            NetsickBefore = NetsickAtStart,
            NetsickAfter = DiveReport.NetsickOf(Runner),
            Xp = success ? Settings.successXp : 0f,
            Special = SpecialFound,
            HunterFollowed = hunterFollowed,
        };
        if (HunterSpawned)
            report.Events.Add("Event_HunterICE|" + "NR_ReportEventHunter".Translate());
        if (RivalTook)
            report.Events.Add("Event_Rival|" + "NR_ReportEventRival".Translate());
        if (HoneypotRevealed)
            report.Events.Add("Event_Honeypot|" + "NR_ReportEventHoneypot".Translate());
        if (Storm)
            report.Events.Add("Event_DataStorm|" + "NR_ReportEventStorm".Translate());
        if (Node.IsBoss)
            report.Events.Add("Node_CorpCore|" + "NR_ReportEventBoss".Translate());
        if (Node.CounterHack && success)
            report.Events.Add("Node_Counterhack|" + "NR_ReportEventCounterHack".Translate());
        if (ForkChoice.Count > 0)
        {
            string path = string.Join(", ", ForkChoice.Select(c => (c == 1 ? "NR_DivePathHard" : "NR_DivePathEasy").Translate().ToString()));
            report.Events.Add((ForkChoice.Contains(1) ? "Path_Hard" : "Path_Easy") + "|" + "NR_ReportEventPath".Translate(path));
        }
        return report;
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
        Scribe_Values.Look(ref BreakerCharges, "breaker");
        Scribe_Values.Look(ref ExecTurns, "execTurns");
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
        Scribe_Values.Look(ref HeatAtStart, "heatAtStart");
        Scribe_Values.Look(ref NetsickAtStart, "netsickAtStart");
        Scribe_Values.Look(ref SpecialFound, "specialFound");
        Scribe_Values.Look(ref Retreated, "retreated");
        Scribe_Values.Look(ref IceHits, "iceHits");
        Scribe_Values.Look(ref NodeAlert, "nodeAlert");
        Scribe_Values.Look(ref _cacheFound, "cacheFound");
        Scribe_Deep.Look(ref Report, "report");
        Scribe_Collections.Look(ref Visited, "visited", LookMode.Value);
        Scribe_Collections.Look(ref ForkIndex, "forkIndex", LookMode.Value);
        Scribe_Collections.Look(ref ForkChoice, "forkChoice", LookMode.Value);
        Scribe_Collections.Look(ref ForkScanned, "forkScanned", LookMode.Value);
        Scribe_Values.Look(ref PassageBonus, "passageBonus");
        Visited ??= [];
        ForkIndex ??= [];
        ForkChoice ??= [];
        ForkScanned ??= [];
        Elements ??= [];
        if (Scribe.mode == LoadSaveMode.PostLoadInit && Elements.Count > 0 && Elements.All(e => e.Links.Count == 0))
        {
            // A dive saved before branching: the old staircase becomes a single path.
            Linearize();
            for (int i = 0; i <= Position; i++)
                Visited.Add(i);
        }
        Programs ??= [];
        Daemons ??= [];
        LootItems ??= [];
        Log ??= [];
    }
}
