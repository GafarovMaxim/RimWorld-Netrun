using System.Collections.Generic;
using System.Linq;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// The Gateway: the external nodes on offer (refreshed every few days), the programs picked for the
/// next dive and the dive in progress, if any.
/// </summary>
public partial class GatewayManager : GameComponent, INetrunCleanable
{
    private static CyberspaceSettingsDef Settings => NetrunDefOf.NR_Cyberspace;

    private List<ExternalNode> _nodes = [];
    private int _nextRefreshTick = -1;
    private int _nextId = 1;
    public List<DiveProgram> SelectedPrograms = [];
    /// <summary>The one daemon taken on dives (-1 for none); kept between dives.</summary>
    public int SelectedDaemon = -1;

    /// <summary>The daemon to take, if it is still unlocked.</summary>
    public List<DiveDaemon> DaemonsForDive => SelectedDaemon >= 0 && Daemons.Contains((DiveDaemon)SelectedDaemon) ? [(DiveDaemon)SelectedDaemon] : [];
    /// <summary>Permanent programs found in cyberspace.</summary>
    public List<DiveDaemon> Daemons = [];
    /// <summary>Favor of a hacker group, spent on calling allies.</summary>
    public int GroupFavor;
    private int _alliesReadyTick;
    private int _nextBossTick = -1;
    private int _stormUntilTick = -1;

    /// <summary>A data storm: gates and ICE are stronger, rewards bigger.</summary>
    public bool StormActive => Find.TickManager.TicksGame < _stormUntilTick;
    public int StormTicksLeft => System.Math.Max(0, _stormUntilTick - Find.TickManager.TicksGame);

    public void StartStorm()
    {
        _stormUntilTick = Find.TickManager.TicksGame + (int)(Settings.stormDays.RandomInRange * GenDate.TicksPerDay);
        Find.LetterStack.ReceiveLetter("NR_StormLabel".Translate(), "NR_Storm".Translate(StormTicksLeft.ToStringTicksToPeriod()), LetterDefOf.NeutralEvent);
    }
    public DiveSession Session;
    /// <summary>The summary of the last finished dive (dev: show it again).</summary>
    public DiveReport LastReport;
    /// <summary>The last few dive summaries, newest last.</summary>
    public List<DiveReport> Reports = [];
    private const int MaxReports = 10;

    public GatewayManager(Game game)
    {
    }

    public int Cleanup(CleanupReason reason)
    {
        int n = Reports.RemoveAll(r => r == null) + _nodes.RemoveAll(x => x == null) + Headlines.RemoveAll(h => h == null);
        if (Reports.Count > MaxReports)
        {
            n += Reports.Count - MaxReports;
            Reports.RemoveRange(0, Reports.Count - MaxReports);
        }
        if (Headlines.Count > 12)
        {
            n += Headlines.Count - 12;
            Headlines.RemoveRange(0, Headlines.Count - 12);
        }
        return n;
    }

    public static GatewayManager Get() => Current.Game?.GetComponent<GatewayManager>();

    /// <summary>Every branch (dive target) as stored, without refreshing.</summary>
    public IReadOnlyList<ExternalNode> AllBranches => _nodes;

    public int NextBranchId() => _nextId++;

    public void AddBranchRaw(ExternalNode node) => _nodes.Add(node);

    public void ClearBranches(bool keepSession) => _nodes.RemoveAll(n => !keepSession || Session?.Node?.Id != n.Id);

    private static bool MapReady => NetMapManager.Get() is { Ready: true };

    /// <summary>
    /// Adds a branch: on the map, on a free slot in one of the preferred districts; before the map exists, into the
    /// old ring of up to seven nodes.
    /// </summary>
    public void AddBranch(ExternalNode node, params District[] prefer)
    {
        if (MapReady && NetMapManager.Get().Place(node, prefer.Length > 0 ? prefer : null))
            return;
        if (_nodes.Count >= MaxNodes)
            _nodes.RemoveAt(0);
        _nodes.Add(node);
    }

    public IReadOnlyList<ExternalNode> Nodes
    {
        get
        {
            int now = Find.TickManager.TicksGame;
            // With the map, branches live on their nodes and refill on their own; the old refresh is for the old ring.
            if (MapReady)
                MarkContractBranch(now);
            else if (_nextRefreshTick < 0 || now >= _nextRefreshTick)
                Refresh();
            // The boss leaves when its time is up (unless someone is inside), and comes back every few weeks.
            _nodes.RemoveAll(n => n.ExpireTick >= 0 && now >= n.ExpireTick && (Session == null || n.Id != Session.Node.Id));
            if (_nextBossTick < 0)
                _nextBossTick = now + (int)(Settings.bossIntervalDays * GenDate.TicksPerDay);
            else if (now >= _nextBossTick)
                SpawnBoss();
            return _nodes;
        }
    }

    private void Refresh()
    {
        _nextRefreshTick = Find.TickManager.TicksGame + (int)(Settings.refreshDays * GenDate.TicksPerDay);
        // The node being dived into and a live boss node stay.
        _nodes.RemoveAll(n => (Session == null || n.Id != Session.Node.Id) && !n.IsBoss);
        int count = Settings.nodeCount.RandomInRange;
        for (int i = 0; i < count; i++)
        {
            ExternalNode node = ExternalNode.Generate(_nextId++);
            // A corp lockdown after the core fell: new corp nodes come with stronger ICE too.
            if (LockdownActive && node.Type == ExternalNodeType.Corp)
                node.Defense++;
            _nodes.Add(node);
        }
        foreach (ExternalNode node in _nodes.InRandomOrder().Take(Settings.hiddenNodes))
            node.Hidden = true;
        if (!StormActive && Rand.Chance(Settings.stormChance * NetrunMod.Settings.CyberEventFrequency))
            StartStorm();
        if (ContractManager.Get()?.Active.Any(c => c.Type is ContractType.SystemTap or ContractType.QuietRun) == true
            && _nodes.Where(n => !n.Hidden).TryRandomElement(out ExternalNode marked))
            marked.Contract = true;
    }

    public void RemoveNode(ExternalNode node)
    {
        NetMapManager.Get()?.NotifyBurned(node);
        _nodes.RemoveAll(n => n.Id == node.Id);
    }

    private int _contractCheckTick = -1;

    /// <summary>With the map: a running tap or quiet-run contract marks one known branch as its target.</summary>
    private void MarkContractBranch(int now)
    {
        if (now - _contractCheckTick < GenDate.TicksPerHour)
            return;
        _contractCheckTick = now;
        bool wanted = ContractManager.Get()?.Active.Any(c => c.Type is ContractType.SystemTap or ContractType.QuietRun) == true;
        if (!wanted)
        {
            foreach (ExternalNode n in _nodes)
                n.Contract = false;
            return;
        }
        if (!_nodes.Any(n => n.Contract) && _nodes.Where(n => !n.Hidden && !n.IsBoss && !n.Ancient).TryRandomElement(out ExternalNode marked))
            marked.Contract = true;
    }

    /// <summary>The corporate core appears now (and the next one is scheduled).</summary>
    public ExternalNode SpawnBoss()
    {
        _nextBossTick = Find.TickManager.TicksGame + (int)(Settings.bossIntervalDays * GenDate.TicksPerDay);
        _nodes.RemoveAll(n => n.IsBoss && (Session == null || n.Id != Session.Node.Id));
        ExternalNode boss = ExternalNode.GenerateBoss(_nextId++);
        AddBranch(boss, District.Corp);
        Find.LetterStack.ReceiveLetter("NR_BossNodeLabel".Translate(), "NR_BossNode".Translate(boss.Name, Settings.bossLifetimeDays.ToString("0")),
            LetterDefOf.PositiveEvent);
        return boss;
    }

    /// <summary>A light node to take the turrets back; gone after the given hours.</summary>
    public void AddCounterHackNode(float hours)
    {
        RemoveCounterHackNodes();
        ExternalNode node = ExternalNode.Generate(_nextId++);
        node.Type = ExternalNodeType.Market;
        node.Defense = 2;
        node.BlackIce = false;
        node.IceStrength = 0;
        node.Hidden = node.Rival = node.Honeypot = node.WildAI = false;
        node.CounterHack = true;
        node.ExpireTick = Find.TickManager.TicksGame + (int)(hours * GenDate.TicksPerHour);
        AddBranch(node, District.Outskirts);
    }

    public void RemoveCounterHackNodes() => _nodes.RemoveAll(n => n.CounterHack && (Session == null || n.Id != Session.Node.Id));

    /// <summary>Dev: one node of each event kind (rival, honeypot, wild AI).</summary>
    public void SpawnEventNodes()
    {
        ExternalNode rival = ExternalNode.Generate(_nextId++);
        rival.Rival = true;
        rival.Honeypot = false;
        ExternalNode honeypot = ExternalNode.Generate(_nextId++);
        honeypot.Honeypot = true;
        honeypot.Rival = false;
        ExternalNode wild = ExternalNode.Generate(_nextId++);
        wild.Type = ExternalNodeType.Abandoned;
        wild.WildAI = true;
        foreach (ExternalNode node in new[] { rival, honeypot, wild })
        {
            node.Hidden = false;
            AddBranch(node, District.Outskirts, District.Market, District.Ruins);
        }
    }

    public bool AddDaemon(DiveDaemon daemon)
    {
        if (Daemons.Contains(daemon))
            return false;
        Daemons.Add(daemon);
        return true;
    }

    /// <summary>Dev: allies ready again.</summary>
    public void DevResetAllies() => _alliesReadyTick = 0;

    /// <summary>Dev: a fresh node of the given type (visible, not an event node).</summary>
    public void DevAddNode(ExternalNodeType type)
    {
        if (type == ExternalNodeType.CorpCore)
        {
            SpawnBoss();
            return;
        }
        ExternalNode node = ExternalNode.Generate(_nextId++);
        node.Type = type;
        node.Hidden = node.Rival = node.Honeypot = node.WildAI = node.CounterHack = false;
        AddBranch(node, type switch
        {
            ExternalNodeType.Corp => District.Corp,
            ExternalNodeType.Abandoned => District.Ruins,
            ExternalNodeType.DarkExchange => District.Market,
            ExternalNodeType.Archive => District.Campus,
            _ => District.Outskirts,
        });
    }

    public bool AlliesReady => Find.TickManager.TicksGame >= _alliesReadyTick;

    public AcceptanceReport CanCallAllies()
    {
        if (NetworkIsolation.Active)
            return NetworkIsolation.Check();
        if (GroupFavor < Settings.groupFavorForAllies)
            return "NR_AlliesNeedFavor".Translate(GroupFavor, Settings.groupFavorForAllies);
        if (!AlliesReady)
            return "NR_AlliesCooldown".Translate((_alliesReadyTick - Find.TickManager.TicksGame).ToStringTicksToPeriod());
        return true;
    }

    /// <summary>The hacker group sends help: a friendly party from an allied faction, or data when there is none.</summary>
    public void CallAllies(Map map)
    {
        if (!CanCallAllies().Accepted)
            return;
        GroupFavor -= Settings.groupFavorForAllies;
        _alliesReadyTick = Find.TickManager.TicksGame + (int)(Settings.alliesCooldownDays * GenDate.TicksPerDay);

        // The friendliest runner crew answers first (docs/CONCEPTS_10.md, section 6); otherwise any allied faction.
        if (NetFactions.BestCrew() is { } crew && NetrunDefOf.NR_CrewHelp != null)
        {
            IncidentParms help = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            help.faction = crew;
            help.forced = true;
            help.raidStrategy = RaidStrategyDefOf.ImmediateAttackFriendly;
            if (NetrunDefOf.NR_CrewHelp.Worker.TryExecute(help))
                return;
        }
        Faction ally = Find.FactionManager.AllFactionsVisible
            .Where(f => !f.IsPlayer && f.def.humanlikeFaction && f.PlayerRelationKind == FactionRelationKind.Ally && !f.defeated)
            .RandomElementWithFallback();
        if (ally != null)
        {
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.faction = ally;
            parms.forced = true;
            if (IncidentDefOf.RaidFriendly.Worker.TryExecute(parms))
                return;
        }

        Thing data = ThingMaker.MakeThing(NetrunDefOf.USH_AdvancedCyberdata);
        data.stackCount = 3;
        DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, [data], forbid: false);
        Messages.Message("NR_AlliesNoFaction".Translate(), MessageTypeDefOf.PositiveEvent, historical: true);
    }

    /// <summary>Ticks until the next boss node, or -1 before the first count starts.</summary>
    public int TicksUntilBoss => _nextBossTick < 0 ? -1 : System.Math.Max(0, _nextBossTick - Find.TickManager.TicksGame);
    public ExternalNode Boss => _nodes.FirstOrDefault(n => n.IsBoss);

    public int TicksUntilRefresh => System.Math.Max(0, _nextRefreshTick - Find.TickManager.TicksGame);

    // ---- The netrunner and the pod ----

    /// <summary>A colonist with a cyberlink lying in a powered cyberpod on this map: the only way in.</summary>
    public static Pawn Runner(Map map, out Building_Cyberpod pod)
    {
        pod = null;
        foreach (Building_Cyberpod each in CyberpodUtility.PlayerPods(map))
        {
            if (each.ContainedThing is Pawn pawn && pawn.IsHacker() && each.GetComp<CompPowerTrader>()?.PowerOn != false)
            {
                pod = each;
                return pawn;
            }
        }
        return null;
    }

    /// <summary>
    /// The runner for a dive into this branch: with the map, the one standing at its node (docs/CYBERSPACE_V2.md);
    /// before the map exists, anyone in a pod.
    /// </summary>
    public static Pawn Runner(Map map, ExternalNode node, out Building_Cyberpod pod)
    {
        NetMapManager net = NetMapManager.Get();
        if (net is { Ready: true } && node is { MapNodeId: >= 0 })
        {
            pod = null;
            Pawn pawn = net.RunnerAt(node.MapNodeId)?.Pawn;
            if (pawn == null || !pawn.TryGetHoldingCyberpod(out pod))
                return null;
            return pawn;
        }
        return Runner(map, out pod);
    }

    public static int ProgramSlots(Building_Cyberpod pod)
        => Settings.programSlots + (pod != null && CyberpodUtility.CoprocessorAI(pod) != null ? 1 : 0);

    public AcceptanceReport CanDive(Map map, ExternalNode node)
    {
        if (NetworkIsolation.Active)
            return NetworkIsolation.Check();
        if (node == null)
            return "NR_GatewayPickNode".Translate();
        if (node.Hidden)
            return "NR_GatewayNodeHidden".Translate();
        if (node.Offline || NetMapManager.Get()?.NodeOf(node)?.Offline == true)
            return "NR_MapBranchOffline".Translate();
        if (Runner(map, node, out Building_Cyberpod pod) == null)
            return NetMapManager.Get() is { Ready: true } && node.MapNodeId >= 0 ? "NR_MapNeedRunnerHere".Translate() : "NR_GatewayNoPod".Translate();
        int cost = DiveCost(map, node, out _);
        if (ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata) + PodFuelCyberdata(pod) < cost)
            return "NR_AINoFood".Translate(cost, NetrunDefOf.USH_Cyberdata.label);
        foreach (DiveProgram program in SelectedPrograms.Where(HePrograms.IsHeItem))
            if (HePrograms.Available(map, program) <= 0)
                return "NR_HeProgramNoItem".Translate(HePrograms.IconDef(program)?.label ?? "?");
        return true;
    }

    /// <summary>The pod's hack points, counted in whole cyberdata (HE refuels 1 cyberdata = 40 hack points).</summary>
    public static int PodFuelCyberdata(Building_Cyberpod pod)
    {
        CompRefuelable fuel = pod?.GetComp<CompRefuelable>();
        if (fuel == null || fuel.Props.FuelMultiplierCurrentDifficulty <= 0f)
            return 0;
        return UnityEngine.Mathf.FloorToInt(fuel.Fuel / fuel.Props.FuelMultiplierCurrentDifficulty);
    }

    /// <summary>A dive is paid from the pod's hack points first, the rest in cyberdata (docs/CONCEPTS_10.md, section 8).</summary>
    private static void Pay(Map map, Building_Cyberpod pod, int cost)
    {
        int fromPod = System.Math.Min(cost, PodFuelCyberdata(pod));
        if (fromPod > 0)
        {
            CompRefuelable fuel = pod.GetComp<CompRefuelable>();
            fuel.ConsumeFuel(fromPod * fuel.Props.FuelMultiplierCurrentDifficulty);
        }
        ColonyGoods.TryTake(map, NetrunDefOf.USH_Cyberdata, cost - fromPod);
    }

    public void Dive(Map map, ExternalNode node)
    {
        NetworkGate.Notify();
        Pawn runner = Runner(map, node, out Building_Cyberpod pod);
        if (runner == null)
            return;
        int cost = DiveCost(map, node, out _);
        Pay(map, pod, cost);
        // HE items are burnt at the start; one that went missing since the pick just drops out.
        SelectedPrograms.RemoveAll(p => HePrograms.IsHeItem(p) && !HePrograms.TryConsume(map, p));
        Session = DiveSession.Start(node, runner, SelectedPrograms, cost, DaemonsForDive);
        EconomyTracker.Get()?.Note(spent: cost, dive: true);
        SelectedPrograms = [];
    }

    /// <summary>
    /// Cyberdata a dive costs: the node's entry (base + one per two defense levels) plus the programs,
    /// minus discounts for an AI in the pod's coprocessor and an AI riding in the netrunner. Never below zero.
    /// </summary>
    public int DiveCost(Map map, ExternalNode node, out string breakdown)
    {
        int entry = node == null ? 0 : Settings.diveBaseCost + node.Defense / 2;
        int paidPrograms = SelectedPrograms.Count(p => !HePrograms.IsHeItem(p));
        int programs = paidPrograms * Settings.programCost;
        Pawn runner = Runner(map, node, out Building_Cyberpod pod);
        int coprocessor = pod != null && CyberpodUtility.CoprocessorAI(pod) != null ? Settings.coprocessorDiscount : 0;
        int passenger = runner != null && AIManager.Get()?.AIs.Any(a => a.Host == runner) == true ? Settings.passengerDiscount : 0;
        int total = System.Math.Max(0, entry + programs - coprocessor - passenger);

        var lines = new List<string> { "NR_DiveCostEntry".Translate(entry), "NR_DiveCostPrograms".Translate(paidPrograms, programs) };
        if (coprocessor > 0)
            lines.Add("NR_DiveCostCoprocessor".Translate(coprocessor));
        if (passenger > 0)
            lines.Add("NR_DiveCostPassenger".Translate(passenger));
        lines.Add("NR_DiveCostTotal".Translate(total));
        int podFuel = PodFuelCyberdata(pod);
        if (podFuel > 0 && total > 0)
            lines.Add("NR_DiveCostPodFuel".Translate(System.Math.Min(total, podFuel)));
        breakdown = lines.ToLineList();
        return total;
    }

    /// <summary>The dive window was closed after the end: forget it.</summary>
    public void ClearFinished()
    {
        if (Session is not { Finished: true })
            return;
        if (Session.Report != null)
        {
            LastReport = Session.Report;
            Reports.Add(LastReport);
            if (Reports.Count > MaxReports)
                Reports.RemoveAt(0);
            Dialog_DiveReport.Show(LastReport);
        }
        Session = null;
    }

    /// <summary>A dive whose netrunner left the pod (or died) cannot go on.</summary>
    public void ValidateSession()
    {
        if (Session == null || Session.Finished)
            return;
        if (Session.Runner == null || Session.Runner.Dead || !Session.Runner.TryGetHoldingCyberpod(out _))
        {
            Messages.Message("NR_DiveAborted".Translate(), MessageTypeDefOf.NegativeEvent, historical: false);
            Session = null;
        }
    }

    /// <summary>The Gateway map has room for this many circles.</summary>
    public const int MaxNodes = 7;

    /// <summary>"Discover new node": reveals a hidden node, or finds a new one when nothing is hidden.</summary>
    public AcceptanceReport CanSearch(Map map)
    {
        if (NetworkIsolation.Active)
            return NetworkIsolation.Check();
        if (!MapReady && !_nodes.Any(n => n.Hidden) && _nodes.Count >= MaxNodes)
            return "NR_GatewayFull".Translate();
        return ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata) >= Settings.searchCost
            ? true
            : "NR_AINoFood".Translate(Settings.searchCost, NetrunDefOf.USH_Cyberdata.label);
    }

    /// <summary>Uncovers one hidden node for free (the honeypot's back-trace); null when nothing is hidden.</summary>
    public ExternalNode RevealHidden()
    {
        ExternalNode found = _nodes.FirstOrDefault(n => n.Hidden);
        if (found != null)
            found.Hidden = false;
        return found;
    }

    public ExternalNode Search(Map map)
    {
        if (!CanSearch(map).Accepted)
            return null;
        ColonyGoods.TryTake(map, NetrunDefOf.USH_Cyberdata, Settings.searchCost);
        EconomyTracker.Get()?.Note(spent: Settings.searchCost);
        if (MapReady)
        {
            // With the map: a scan around the runners (or Home) shows what hides nearby; if nothing does, a new branch turns up.
            NetMapManager net = NetMapManager.Get();
            int around = net.Runners.FirstOrDefault()?.NodeId ?? net.Map.HomeId;
            ExternalNode before = _nodes.FirstOrDefault(n => n.Hidden && net.Map.Hops(around, n.MapNodeId, 3) <= 3);
            if (net.RevealAround(around, 3) > 0)
                return before;
            ExternalNode fresh = ExternalNode.Generate(_nextId++);
            fresh.Hidden = false;
            net.Place(fresh, null, around);
            return fresh;
        }
        ExternalNode found = _nodes.FirstOrDefault(n => n.Hidden);
        if (found == null)
        {
            found = ExternalNode.Generate(_nextId++);
            _nodes.Add(found);
        }
        found.Hidden = false;
        return found;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref _nodes, "nodes", LookMode.Deep);
        Scribe_Values.Look(ref _nextRefreshTick, "nextRefreshTick", -1);
        Scribe_Values.Look(ref _nextId, "nextId", 1);
        Scribe_Collections.Look(ref SelectedPrograms, "selectedPrograms", LookMode.Value);
        Scribe_Values.Look(ref SelectedDaemon, "selectedDaemon", -1);
        Scribe_Deep.Look(ref Session, "session");
        Scribe_Collections.Look(ref Daemons, "daemons", LookMode.Value);
        Scribe_Values.Look(ref GroupFavor, "groupFavor");
        Scribe_Deep.Look(ref LastReport, "lastReport");
        Scribe_Collections.Look(ref Reports, "reports", LookMode.Deep);
        Reports ??= [];
        Scribe_Values.Look(ref _alliesReadyTick, "alliesReadyTick");
        Scribe_Values.Look(ref _nextBossTick, "nextBossTick", -1);
        Scribe_Values.Look(ref _stormUntilTick, "stormUntilTick", -1);
        ExposeHeadlines();
        Daemons ??= [];
        _nodes ??= [];
        SelectedPrograms ??= [];
    }
}
