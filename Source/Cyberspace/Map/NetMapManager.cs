using System.Collections.Generic;
using System.Linq;
using RimWorld;
using USH_HE;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>Numbers for the map, sessions and travel (Defs/CyberMap.xml).</summary>
public class CyberMapSettingsDef : Def
{
    public int maxRunners = 3;
    public float branchFillShare = 0.7f;
    public IntRange refillDays = new(2, 4);
    public int visibleHops = 2;
    public int visibleDays = 10;
    public int jumpCost = 1;
    public int tollCost = 1;
    public float emergencyNetsick = 0.05f;
    public float emergencyLockoutChance = 0.1f;
    public float sessionGainPerDay = 0.24f;
    public float remotePortSpeed = 1.33f;
    public float naturalSpeed = 1.18f;
    public float patrolHitChance = 0.3f;
    /// <summary>A runner standing on a hub pings the branches within this many hops every hour; a ping now costs cyberdata.</summary>
    public int pingHops = 2;
    public int pingCost = 3;
    public float patrolNetsick = 0.02f;
    public IntRange eventIntervalDays = new(1, 2);
    public int permanentChangeDays = 15;
    /// <summary>Odyssey: orbital backbone nodes on the map, their cyberdata factor.</summary>
    public IntRange orbitNodes = new(2, 3);
    public float orbitLoot = 1.5f;
}

/// <summary>
/// The cyberspace map in play (docs/CYBERSPACE_V2.md): generated once, branches on its node slots, runners walking it
/// in game time, a day-by-day life of outages, jams, new tenants and the like.
/// Travel checks run every 250 ticks only while someone walks; timers and events once an hour and once a day.
/// </summary>
public partial class NetMapManager : GameComponent, INetrunCleanable
{
    public CyberMap Map;
    /// <summary>A node marked for the player (a scenario's target: the former owner's fortress), always visible.</summary>
    public int MarkedNodeId = -1;
    /// <summary>Hubs pinned by the player (right click, Watch), at most six.</summary>
    public List<int> Watch = [];

    public void ToggleWatch(int hubId)
    {
        if (!Watch.Remove(hubId) && Watch.Count < 6)
            Watch.Add(hubId);
    }
    public List<NetRunner> Runners = [];
    /// <summary>Slots waiting to fill again after a branch was burned out: node id and tick.</summary>
    private List<int> _refillNode = [];
    private List<int> _refillTick = [];
    private int _nextEventTick = -1;
    private int _lastPermanentTick = -1;

    public NetMapManager(Game game)
    {
    }

    public static NetMapManager Get() => Current.Game?.GetComponent<NetMapManager>();

    public static CyberMapSettingsDef Settings => NetrunDefOf.NR_CyberMap;

    public bool Ready => Map != null && Map.Nodes.Count > 0;

    private static GatewayManager Gateway => GatewayManager.Get();

    // ---- Generation ----

    /// <summary>Builds the map for this world and hangs branches on it; existing branches move onto the map.</summary>
    public void Generate(NetSize size)
    {
        int seed = Find.World?.info?.Seed.GetHashCode() ?? Rand.Int;
        Map = CyberMapGenerator.Generate(size, seed);
        MarkedNodeId = -1;
        Runners.Clear();
        _refillNode.Clear();
        _refillTick.Clear();
        GatewayManager gateway = Gateway;
        if (gateway == null)
            return;

        // Old branches (from before the map) settle in the Outskirts and the transit; the rest is dropped.
        List<ExternalNode> old = gateway.AllBranches.ToList();
        gateway.ClearBranches(keepSession: false);
        foreach (ExternalNode node in old)
        {
            bool keep = gateway.Session?.Node?.Id == node.Id || node.IsBoss || node.CounterHack || node.WildAI;
            if (keep)
                Place(node, node.IsBoss ? null : new[] { District.Outskirts, District.Transit, District.Market });
        }

        foreach (MapNode n in Map.Nodes)
        {
            int fill = n.District == District.OldNet ? n.Slots : Mathf.RoundToInt(n.Slots * Settings.branchFillShare + Rand.Value * 0.5f);
            for (int i = BranchesAt(n.Id).Count(); i < Mathf.Min(fill, n.Slots); i++)
                Attach(ExternalNode.ForDistrict(gateway.NextBranchId(), n.District), n);
        }
        foreach (MapNode n in Map.Nodes)
            n.LastVisitTick = -1;
        // A large map has a fourth corporate district: the factions and their districts follow the map.
        NetFactions.Get()?.Ensure();
        CorpManager.Get()?.Sync();
        Map.Home.LastVisitTick = Find.TickManager.TicksGame;
        EnsureOrbit();
        UpdateVisibility();
        _nextEventTick = Find.TickManager.TicksGame + Settings.eventIntervalDays.RandomInRange * GenDate.TicksPerDay;
        Log.Message($"[Netrun] cyberspace map: {Map.Size}, {Map.Layout}, {Map.Nodes.Count} nodes, {Map.Links.Count} links.");
    }

    // ---- Branches ----

    public IEnumerable<ExternalNode> BranchesAt(int nodeId)
        => Gateway?.AllBranches.Where(b => b.MapNodeId == nodeId) ?? Enumerable.Empty<ExternalNode>();

    public MapNode NodeOf(ExternalNode branch) => Map?.Node(branch.MapNodeId);

    public int FreeSlots(MapNode n) => n.Slots - BranchesAt(n.Id).Count() - _refillNode.Count(id => id == n.Id);

    private void Attach(ExternalNode branch, MapNode n)
    {
        branch.MapNodeId = n.Id;
        if (!branch.CounterHack && !branch.IsBoss)
            branch.District = n.District;
        Gateway?.AddBranchRaw(branch);
    }

    /// <summary>
    /// Hangs a branch on a node with a free slot, in one of the preferred districts if possible (the boss goes to a
    /// fortress and needs no free slot). Returns false if the map has no room.
    /// </summary>
    public bool Place(ExternalNode branch, District[] prefer, int near = -1)
    {
        if (!Ready)
            return false;
        if (branch.IsBoss)
        {
            MapNode fortress = Map.Nodes.Where(n => n.Fortress).RandomElementWithFallback();
            if (fortress != null)
            {
                Attach(branch, fortress);
                branch.District = District.Corp;
                return true;
            }
        }
        IEnumerable<MapNode> pool = Map.Nodes.Where(n => n.District is not (District.Home or District.OldNet or District.Orbit) && FreeSlots(n) > 0);
        List<MapNode> preferred = prefer == null ? [] : pool.Where(n => prefer.Contains(n.District)).ToList();
        List<MapNode> choices = preferred.Count > 0 ? preferred : pool.ToList();
        if (choices.Count == 0)
        {
            // No free slot anywhere: open one more slot on a preferred node.
            choices = Map.Nodes.Where(n => prefer?.Contains(n.District) ?? n.District == District.Outskirts).ToList();
            if (choices.Count == 0)
                return false;
            MapNode grow = choices.RandomElement();
            grow.Slots++;
            choices = [grow];
        }
        MapNode target = near >= 0 ? choices.MinBy(n => Map.Hops(near, n.Id)) : choices.RandomElement();
        Attach(branch, target);
        return true;
    }

    /// <summary>A branch was burned out (a clean dive): the slot fills again in a few days.</summary>
    public void NotifyBurned(ExternalNode branch)
    {
        if (!Ready || branch.MapNodeId < 0 || branch.IsBoss || branch.CounterHack)
            return;
        _refillNode.Add(branch.MapNodeId);
        _refillTick.Add(Find.TickManager.TicksGame + Settings.refillDays.RandomInRange * GenDate.TicksPerDay);
    }

    /// <summary>Branches near places runners have been lately are known; the rest show as grey dots.</summary>
    public void UpdateVisibility()
    {
        if (!Ready || Gateway == null)
            return;
        int now = Find.TickManager.TicksGame;
        var recent = Map.Nodes.Where(n => n.LastVisitTick >= 0 && now - n.LastVisitTick <= Settings.visibleDays * GenDate.TicksPerDay).Select(n => n.Id).ToList();
        recent.AddRange(Runners.Select(r => r.NodeId));
        var near = new HashSet<int>();
        foreach (int id in recent.Distinct())
            foreach (MapNode n in Map.Nodes)
                if (!near.Contains(n.Id) && Map.Hops(id, n.Id, Settings.visibleHops) <= Settings.visibleHops)
                    near.Add(n.Id);
        if (MarkedNodeId >= 0)
            near.Add(MarkedNodeId);
        foreach (ExternalNode b in Gateway.AllBranches)
        {
            if (b.MapNodeId < 0 || b.IsBoss)
                continue;
            if (near.Contains(b.MapNodeId))
                b.Hidden = false;
        }
    }

    /// <summary>Shows the hidden branches within some hops of a node (honeypot back-traces, the future runner ping).</summary>
    public int RevealAround(int nodeId, int hops)
    {
        // One breadth-first pass collects the hubs in range, then one pass over the branches (not a search per branch).
        var near = new HashSet<int> { nodeId };
        var frontier = new List<int> { nodeId };
        for (int h = 0; h < hops && frontier.Count > 0; h++)
        {
            var next = new List<int>();
            foreach (int u in frontier)
                foreach (MapLink l in Map.LinksOf(u))
                    if (l.Known && near.Add(l.Other(u)))
                        next.Add(l.Other(u));
            frontier = next;
        }
        int count = 0;
        foreach (ExternalNode b in Gateway?.AllBranches ?? Enumerable.Empty<ExternalNode>())
        {
            if (b.Hidden && b.MapNodeId >= 0 && near.Contains(b.MapNodeId))
            {
                b.Hidden = false;
                count++;
            }
        }
        return count;
    }

    // ---- Runners and sessions ----

    public NetRunner RunnerOf(Pawn pawn) => Runners.FirstOrDefault(r => r.Pawn == pawn);

    public NetRunner RunnerAt(int nodeId) => Runners.FirstOrDefault(r => r.NodeId == nodeId && !r.Moving && !r.Busy);

    /// <summary>Pawns in the colony's powered pods who could connect now.</summary>
    public static IEnumerable<(Pawn pawn, Building_Cyberpod pod)> PodPawns(Map map)
    {
        foreach (Building_Cyberpod pod in CyberpodUtility.PlayerPods(map))
            if (pod.ContainedThing is Pawn p && p.IsHacker() && pod.GetComp<CompPowerTrader>()?.PowerOn != false)
                yield return (p, pod);
    }

    public AcceptanceReport CanConnect(Pawn pawn)
    {
        if (!Ready)
            return "NR_MapNotReady".Translate();
        if (NetworkIsolation.Active)
            return NetworkIsolation.Check();
        if (RunnerOf(pawn) != null)
            return "NR_MapAlreadyConnected".Translate(pawn.LabelShort);
        if (Runners.Count >= Settings.maxRunners)
            return "NR_MapMaxRunners".Translate(Settings.maxRunners);
        return true;
    }

    /// <summary>Connects a pawn lying in a pod: at Home, or at an exit point for a little cyberdata.</summary>
    public NetRunner Connect(Pawn pawn, int startNode, global::Verse.Map colonyMap)
    {
        if (!CanConnect(pawn).Accepted)
            return null;
        if (startNode != Map.HomeId)
        {
            if (!ColonyGoods.TryTake(colonyMap, NetrunDefOf.USH_Cyberdata, Settings.jumpCost))
            {
                Messages.Message("NR_AINoFood".Translate(Settings.jumpCost, NetrunDefOf.USH_Cyberdata.label), MessageTypeDefOf.RejectInput, historical: false);
                return null;
            }
        }
        var runner = new NetRunner { Pawn = pawn, NodeId = startNode, SessionStartTick = Find.TickManager.TicksGame };
        Runners.Add(runner);
        Map.Node(startNode).LastVisitTick = Find.TickManager.TicksGame;
        NetworkGate.Notify();
        UpdateVisibility();
        Messages.Message("NR_MapConnected".Translate(pawn.LabelShort, NodeLabel(Map.Node(startNode))), MessageTypeDefOf.NeutralEvent, historical: false);
        return runner;
    }

    public AcceptanceReport CanTravel(NetRunner runner, int target)
    {
        if (runner == null)
            return "NR_MapNoRunner".Translate();
        if (runner.Busy)
            return "NR_CrewRunnerBusy".Translate();
        if (Gateway?.Session is { Finished: false } s && s.Runner == runner.Pawn)
            return "NR_IsolationDiveActive".Translate();
        if (target == runner.NodeId && !runner.Moving)
            return "NR_MapAlreadyHere".Translate();
        List<int> route = Map.Route(runner.Moving ? runner.NextNodeId : runner.NodeId, target, out _);
        if (route.Count == 0 && target != runner.NextNodeId)
            return "NR_MapNoRoute".Translate();
        return true;
    }

    /// <summary>Sets the runner off along the shortest route (from the next node, if already walking).</summary>
    public void Travel(NetRunner runner, int target)
    {
        if (!CanTravel(runner, target).Accepted)
            return;
        if (runner.Moving)
        {
            int next = runner.NextNodeId;
            List<int> rest = next == target ? [] : Map.Route(next, target, out _);
            runner.Path = [next, .. rest];
        }
        else
        {
            runner.Path = Map.Route(runner.NodeId, target, out _);
            runner.Progress = 0f;
        }
    }

    public void Stop(NetRunner runner)
    {
        if (runner.Moving && runner.Path.Count > 1)
            runner.Path.RemoveRange(1, runner.Path.Count - 1);
    }

    /// <summary>Game hours left on the runner's route (before its speed).</summary>
    public float HoursLeft(NetRunner runner)
    {
        if (!runner.Moving)
            return 0f;
        float total = 0f;
        int from = runner.NodeId;
        foreach (int to in runner.Path)
        {
            MapLink l = Map.LinkBetween(from, to);
            total += l == null ? 1f : Map.Hours(l, from);
            from = to;
        }
        return Mathf.Max(0f, total - runner.Progress) / Speed(runner.Pawn);
    }

    /// <summary>The runner's own speed: HE's remote access port and a natural netrunner walk faster.</summary>
    public static float Speed(Pawn pawn)
    {
        float speed = 1f;
        if (pawn?.health?.hediffSet == null)
            return speed;
        if (DefDatabase<HediffDef>.GetNamedSilentFail("USH_InstalledRemoteAccessPort") is { } port && pawn.health.hediffSet.HasHediff(port))
            speed *= Settings.remotePortSpeed;
        if (DefDatabase<TraitDef>.GetNamedSilentFail("NR_NaturalNetrunner") is { } natural && pawn.story?.traits?.HasTrait(natural) == true)
            speed *= Settings.naturalSpeed;
        return speed;
    }

    public bool AtExit(NetRunner runner) => !runner.Moving && Map.Node(runner.NodeId) is { Exit: true };

    /// <summary>The exit point (Home counts) the runner reaches soonest; -1 when none can be reached.</summary>
    public int NearestExit(NetRunner runner)
    {
        int from = runner.Moving ? runner.NextNodeId : runner.NodeId;
        int best = -1;
        float bestHours = float.MaxValue;
        foreach (MapNode n in Map.Nodes)
        {
            if (!n.Exit && n.Id != Map.HomeId)
                continue;
            if (n.Id == from)
                return n.Id;
            List<int> route = Map.Route(from, n.Id, out float hours);
            if (route.Count > 0 && hours < bestHours)
            {
                bestHours = hours;
                best = n.Id;
            }
        }
        return best;
    }

    /// <summary>One action: walk to the nearest exit and leave the net there.</summary>
    public void GoAndExit(NetRunner runner)
    {
        int exit = NearestExit(runner);
        if (exit < 0)
            return;
        runner.ExitOnArrival = true;
        if (AtExit(runner) || runner.NodeId == exit && !runner.Moving)
            Disconnect(runner, emergency: false);
        else
            Travel(runner, exit);
    }

    /// <summary>Netsick, nausea and a chance of lockout for a runner torn out of the net; Aegis or a Warden AI halve it.</summary>
    public static void EmergencyJolt(Pawn pawn)
    {
        if (pawn == null || pawn.Dead)
            return;
        bool soft = Gateway?.DaemonsForDive.Contains(DiveDaemon.Aegis) == true
                    || AIManager.Get()?.AIs.Any(a => a.Personality == AIPersonality.Warden && a.Active) == true;
        NetsickManager.AddNetsickFor(pawn, Settings.emergencyNetsick * (soft ? 0.5f : 1f));
        if (DefDatabase<HediffDef>.GetNamedSilentFail("USH_CyberspaceNausea") is { } nausea && !DiveTraits.NauseaImmune(pawn))
            pawn.health.AddHediff(nausea, pawn.health.hediffSet.GetBrain());
        if (Rand.Chance(Settings.emergencyLockoutChance * (soft ? 0.5f : 1f)) && DefDatabase<HediffDef>.GetNamedSilentFail("USH_CyberspaceLockOut") is { } lockout)
            pawn.health.AddHediff(lockout, pawn.health.hediffSet.GetBrain());
    }

    /// <summary>The pawn is out in the net right now: walking the map as a runner, or inside a dive.</summary>
    public static bool InRun(Pawn pawn)
        => pawn != null && (Get()?.RunnerOf(pawn) != null || GatewayManager.Get()?.Session is { Finished: false } s && s.Runner == pawn);

    /// <summary>A clean exit at an exit point or Home: the pod wakes the runner, nothing else.</summary>
    public void Disconnect(NetRunner runner, bool emergency)
    {
        if (runner == null)
            return;
        if (Gateway?.Session is { Finished: false } s && s.Runner == runner.Pawn)
            return;
        Runners.Remove(runner);
        Pawn pawn = runner.Pawn;
        if (pawn == null || pawn.Dead)
            return;
        if (emergency)
        {
            EmergencyJolt(pawn);
            Messages.Message("NR_MapEmergency".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NegativeEvent);
        }
        else
        {
            Messages.Message("NR_MapDisconnected".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NeutralEvent, historical: false);
        }
        NetsickManager.WakeFromPod(pawn);
    }

    /// <summary>The corporation playing in a corp district (by its slot), if any (stage 9 keeps this in step with crashes).</summary>
    public Faction CorpOfSlot(int slot) => slot < 0 ? null : CorpManager.Get()?.CorpOfSlot(slot);

    /// <summary>The district a node's new branches belong to: a corporation's captured node grows corporate vaults.</summary>
    public static District BranchDistrict(MapNode n) => n.OwnerFaction >= 0 && n.District is not (District.OldNet or District.Dark) ? District.Corp : n.District;

    /// <summary>Dev: every walking runner jumps to the end of its route.</summary>
    public void DevArriveAll()
    {
        foreach (NetRunner r in Runners.Where(r => r.Moving))
        {
            r.NodeId = r.Path[r.Path.Count - 1];
            r.Path.Clear();
            r.Progress = 0f;
            Map.Node(r.NodeId).LastVisitTick = Find.TickManager.TicksGame;
        }
        UpdateVisibility();
    }

    /// <summary>The first corporate core taken opens the wall to the Old Net.</summary>
    public void OpenWall()
    {
        if (!Ready || Map.WallOpen)
            return;
        Map.WallOpen = true;
        Map.Dirty();
        Gateway?.AddHeadline("NR_MapNewsWall".Translate(), 1);
        Find.LetterStack.ReceiveLetter("NR_MapWallLabel".Translate(), "NR_MapWall".Translate(), LetterDefOf.PositiveEvent);
    }

    public bool InSession(Pawn pawn) => pawn != null && Runners.Any(r => r.Pawn == pawn);

    public override void GameComponentTick()
    {
        if (!Ready)
            return;
        int now = Find.TickManager.TicksGame;
        if (Runners.Count > 0 && now % 250 == 0)
            TickRunners(250);
        if (now % GenDate.TicksPerHour == 91)
            HourlyTick(now);
    }

    /// <summary>Reveals the branches within pingHops of the runner's hub; the terminal shows a wave.</summary>
    public int Ping(NetRunner runner)
    {
        if (runner == null)
            return 0;
        GatewayView.NotePing(runner.NodeId);
        return RevealAround(runner.NodeId, Settings.pingHops);
    }

    public AcceptanceReport CanPingNow(NetRunner runner, Map map)
        => runner == null ? (AcceptanceReport)"NR_MapNoRunner".Translate()
            : runner.Moving ? (AcceptanceReport)"NR_MapPingMoving".Translate()
            : ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata) < Settings.pingCost ? (AcceptanceReport)"NR_NotEnoughCyberdata".Translate()
            : true;

    public void PingNow(NetRunner runner, Map map)
    {
        if (!CanPingNow(runner, map).Accepted || !ColonyGoods.TryTake(map, NetrunDefOf.USH_Cyberdata, Settings.pingCost))
            return;
        int found = Ping(runner);
        Messages.Message("NR_MapPinged".Translate(runner.Pawn?.LabelShort ?? "?", found), MessageTypeDefOf.NeutralEvent, historical: false);
    }

    // Threats already announced (runner and hub), so the game pauses once per threat, not every check.
    private static readonly HashSet<(int pawn, int hub)> Warned = [];

    /// <summary>A patrol or an intrusion on a runner's route or at its target: pause once and say where (a setting).</summary>
    private void CheckThreats()
    {
        if (!NetrunMod.Settings.MapAutopause)
            return;
        IntrusionManager intrusions = IntrusionManager.Get();
        foreach (NetRunner r in Runners)
        {
            if (!r.Moving || r.Pawn == null)
                continue;
            foreach (int hub in r.Path)
            {
                MapNode n = Map.Node(hub);
                bool threat = n != null && n.Patrol || intrusions?.At(hub) != null;
                if (!threat || !Warned.Add((r.Pawn.thingIDNumber, hub)))
                    continue;
                if (Find.TickManager.CurTimeSpeed != TimeSpeed.Paused)
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                Find.LetterStack.ReceiveLetter("NR_MapThreatLabel".Translate(r.Pawn.LabelShort),
                    "NR_MapThreat".Translate(r.Pawn.LabelShort, NodeLabel(n)), LetterDefOf.ThreatSmall);
                break;
            }
        }
    }

    private void TickRunners(int ticks)
    {
        CheckThreats();
        foreach (NetRunner runner in Runners.ToList())
        {
            // Out of the pod (woken, ejected, dead): the session simply ends.
            if (runner.Pawn == null || runner.Pawn.Dead || !runner.Pawn.TryGetHoldingCyberpod(out _))
            {
                Runners.Remove(runner);
                continue;
            }
            if (runner.BusyCrew >= 0 && !runner.Busy)
                CrewSim.Get()?.FinishHire(runner);
            if (!runner.Moving)
                continue;
            if (Gateway?.Session is { Finished: false } s && s.Runner == runner.Pawn)
                continue;
            runner.Progress += ticks / (float)GenDate.TicksPerHour * Speed(runner.Pawn);
            MapLink link = Map.LinkBetween(runner.NodeId, runner.NextNodeId);
            if (link == null || !Map.Passable(link) || Map.Node(runner.NextNodeId)?.Offline == true)
            {
                // The way broke under the runner's feet: find another, or stop.
                int target = runner.Path[runner.Path.Count - 1];
                runner.Path = Map.Route(runner.NodeId, target, out _);
                runner.Progress = 0f;
                if (!runner.Moving)
                    Messages.Message("NR_MapRouteLost".Translate(runner.Pawn.LabelShort), MessageTypeDefOf.NegativeEvent, historical: false);
                continue;
            }
            if (runner.Progress < Map.Hours(link, runner.NodeId))
                continue;
            if (link.Toll && !PayToll(runner))
                continue;
            runner.Progress = 0f;
            runner.NodeId = runner.NextNodeId;
            runner.Path.RemoveAt(0);
            Arrive(runner, link);
            if (runner.ExitOnArrival && !runner.Moving)
            {
                runner.ExitOnArrival = false;
                if (Map.Node(runner.NodeId) is { Exit: true } || runner.NodeId == Map.HomeId)
                {
                    Disconnect(runner, emergency: false);
                    continue;
                }
            }
        }
    }

    private bool PayToll(NetRunner runner)
    {
        global::Verse.Map colony = runner.Pawn.MapHeld;
        if (ColonyGoods.TryTake(colony, NetrunDefOf.USH_Cyberdata, Settings.tollCost))
            return true;
        runner.Path.Clear();
        runner.Progress = 0f;
        Messages.Message("NR_MapTollUnpaid".Translate(runner.Pawn.LabelShort), MessageTypeDefOf.RejectInput, historical: false);
        return false;
    }

    private void Arrive(NetRunner runner, MapLink link)
    {
        MapNode node = Map.Node(runner.NodeId);
        node.LastVisitTick = Find.TickManager.TicksGame;
        float trace = HopTrace(node, link);
        if (trace > 0f)
            HeatTracker.Get()?.AddHeat(trace, "NR_MapHopTrace".Translate(NodeLabel(node)), HeatSource.Dive);
        if (node.RivalTrap && Gateway?.DaemonsForDive.Contains(DiveDaemon.Lens) != true)
        {
            NetsickManager.AddNetsickFor(runner.Pawn, Settings.patrolNetsick * 1.5f);
            if (DefDatabase<HediffDef>.GetNamedSilentFail("USH_CyberspaceNausea") is { } nausea && !DiveTraits.NauseaImmune(runner.Pawn))
                runner.Pawn.health.AddHediff(nausea, runner.Pawn.health.hediffSet.GetBrain());
            node.RivalTrapUntil = -1;
            Messages.Message("NR_MapRivalTrapHit".Translate(runner.Pawn.LabelShort, NodeLabel(node)), runner.Pawn, MessageTypeDefOf.NegativeEvent);
        }
        if (node.Patrol && Rand.Chance(Settings.patrolHitChance))
        {
            NetsickManager.AddNetsickFor(runner.Pawn, Settings.patrolNetsick);
            Messages.Message("NR_MapPatrolHit".Translate(runner.Pawn.LabelShort, NodeLabel(node)), runner.Pawn, MessageTypeDefOf.NegativeEvent);
        }
        UpdateVisibility();
        if (!runner.Moving)
        {
            NetrunSound.Play("UI_Hack");
            Messages.Message("NR_MapArrived".Translate(runner.Pawn.LabelShort, NodeLabel(node)), runner.Pawn, MessageTypeDefOf.PositiveEvent, historical: false);
        }
    }

    /// <summary>Trace for one hop: none on neutral ground; corporations add theirs (stage 9). Dark links leave none.</summary>
    public static float HopTrace(MapNode node, MapLink link) => link?.Kind == LinkKind.Dark ? 0f : CorpTrace(node);

    /// <summary>Filled by the corporations' logic: trace on an alert corporation's ground.</summary>
    public static System.Func<MapNode, float> CorpTraceHook;

    private static float CorpTrace(MapNode node) => CorpTraceHook?.Invoke(node) ?? 0f;

    private void HourlyTick(int now)
    {
        EnsureOrbit();
        // The runners' ping: whoever stands still scans around (this replaces the paid Discover).
        foreach (NetRunner r in Runners)
            if (!r.Moving)
                Ping(r);
        for (int i = _refillTick.Count - 1; i >= 0; i--)
        {
            if (now < _refillTick[i])
                continue;
            MapNode n = Map.Node(_refillNode[i]);
            _refillNode.RemoveAt(i);
            _refillTick.RemoveAt(i);
            if (n != null && FreeSlots(n) > 0 && Gateway != null)
            {
                ExternalNode branch = ExternalNode.ForDistrict(Gateway.NextBranchId(), BranchDistrict(n));
                branch.Hidden = true;
                Attach(branch, n);
            }
        }
        // Expired backdoors go away.
        if (Map.Links.RemoveAll(l => l.ExpiresTick >= 0 && now >= l.ExpiresTick) > 0)
            Map.Dirty();
        if (_nextEventTick < 0)
            _nextEventTick = now + Settings.eventIntervalDays.RandomInRange * GenDate.TicksPerDay;
        else if (now >= _nextEventTick)
        {
            _nextEventTick = now + Settings.eventIntervalDays.RandomInRange * GenDate.TicksPerDay;
            RunEvent();
            UpdateVisibility();
        }
    }

    public static string NodeLabel(MapNode n)
        => n == null ? "?" : n.District == District.Home ? (string)"NR_District_Home".Translate() : "NR_MapNodeLabel".Translate(("NR_District_" + n.District).Translate(), n.Id);

    public int Cleanup(CleanupReason reason)
    {
        int n = Runners.RemoveAll(r => r.Pawn == null || r.Pawn.Dead || (reason != CleanupReason.Loaded && !r.Pawn.TryGetHoldingCyberpod(out _)));
        if (Map != null)
        {
            n += Map.Links.RemoveAll(l => l.ExpiresTick >= 0 && Find.TickManager.TicksGame >= l.ExpiresTick);
            n += Watch.RemoveAll(id => Map.Node(id) == null);
            Map.Dirty();
        }
        return n;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Deep.Look(ref Map, "map");
        ExposeOrbit();
        Scribe_Values.Look(ref MarkedNodeId, "markedNode", -1);
        Scribe_Collections.Look(ref Watch, "watch", LookMode.Value);
        Scribe_Collections.Look(ref Runners, "runners", LookMode.Deep);
        Scribe_Collections.Look(ref _refillNode, "refillNode", LookMode.Value);
        Scribe_Collections.Look(ref _refillTick, "refillTick", LookMode.Value);
        Scribe_Values.Look(ref _nextEventTick, "nextEventTick", -1);
        Scribe_Values.Look(ref _lastPermanentTick, "lastPermanentTick", -1);
        Runners ??= [];
        Watch ??= [];
        _refillNode ??= [];
        _refillTick ??= [];
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            Runners.RemoveAll(r => r.Pawn == null);
    }
}
