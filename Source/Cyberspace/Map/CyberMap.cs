using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Netrun;

public enum District
{
    Home,
    Outskirts,
    Transit,
    Corp,
    Market,
    Ruins,
    Campus,
    Entertainment,
    Dump,
    Dark,
    OldNet,
    /// <summary>Odyssey: orbital backbone nodes, open while a colony gravship is in space (appended for old saves).</summary>
    Orbit,
}

public enum LinkKind
{
    Normal,
    Fast,
    Dark,
    Wall,
    Backdoor,
}

public enum NetSize
{
    Small,
    Normal,
    Large,
}

public enum NetLayout
{
    Ring,
    Spine,
    Archipelago,
    Metro,
}

/// <summary>A backbone node of the cyberspace map (docs/CYBERSPACE_V2.md, section 4). Positions never change.</summary>
public class MapNode : IExposable
{
    public int Id;
    public District District;
    /// <summary>Which corporation district (0..3) for District.Corp; -1 otherwise.</summary>
    public int CorpSlot = -1;
    public float X;
    public float Y;
    /// <summary>Branch slots on this node (dive targets).</summary>
    public int Slots;
    public bool Exit;
    public bool Fortress;
    /// <summary>Faction loadID of the corporation that owns the node, -1 neutral (stage 9 moves it).</summary>
    public int OwnerFaction = -1;

    // Timed states (ticks until; -1 none): offline, storm, ICE patrol, instant transit, corporate quarantine.
    public int OfflineUntil = -1;
    public int StormUntil = -1;
    public int PatrolUntil = -1;
    public int TransitUntil = -1;
    public int QuarantineUntil = -1;
    /// <summary>A rival crew's trap: strikes a passing runner like ICE unless the Lens daemon is along.</summary>
    public int RivalTrapUntil = -1;
    /// <summary>Last tick a runner stood here (branches near it are visible for some days).</summary>
    public int LastVisitTick = -1;

    public Vector2 Pos => new(X, Y);

    private static int Now => Find.TickManager.TicksGame;
    public bool Offline => Now < OfflineUntil || Now < QuarantineUntil || (District == District.Orbit && !NetMapManager.OrbitOpen);
    public bool Storm => Now < StormUntil;
    public bool Patrol => Now < PatrolUntil;
    public bool Transit => Now < TransitUntil;
    public bool RivalTrap => Now < RivalTrapUntil;
    public bool Quarantine => Now < QuarantineUntil;

    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id");
        Scribe_Values.Look(ref District, "district");
        Scribe_Values.Look(ref CorpSlot, "corpSlot", -1);
        Scribe_Values.Look(ref X, "x");
        Scribe_Values.Look(ref Y, "y");
        Scribe_Values.Look(ref Slots, "slots");
        Scribe_Values.Look(ref Exit, "exit");
        Scribe_Values.Look(ref Fortress, "fortress");
        Scribe_Values.Look(ref OwnerFaction, "owner", -1);
        Scribe_Values.Look(ref OfflineUntil, "offlineUntil", -1);
        Scribe_Values.Look(ref StormUntil, "stormUntil", -1);
        Scribe_Values.Look(ref PatrolUntil, "patrolUntil", -1);
        Scribe_Values.Look(ref TransitUntil, "transitUntil", -1);
        Scribe_Values.Look(ref QuarantineUntil, "quarantineUntil", -1);
        Scribe_Values.Look(ref RivalTrapUntil, "rivalTrapUntil", -1);
        Scribe_Values.Look(ref LastVisitTick, "lastVisit", -1);
    }
}

public class MapLink : IExposable
{
    public int A;
    public int B;
    public LinkKind Kind;
    /// <summary>Dark links stay hidden until found; the wall stays shut until opened.</summary>
    public bool Known = true;
    public int JamUntil = -1;
    public int BrokenUntil = -1;
    public int TollUntil = -1;
    /// <summary>Backdoors close on their own (-1: permanent link).</summary>
    public int ExpiresTick = -1;

    private static int Now => Find.TickManager.TicksGame;
    public bool Jammed => Now < JamUntil;
    public bool Broken => Now < BrokenUntil;
    public bool Toll => Now < TollUntil;

    public int Other(int id) => id == A ? B : A;
    public bool Has(int id) => A == id || B == id;

    public void ExposeData()
    {
        Scribe_Values.Look(ref A, "a");
        Scribe_Values.Look(ref B, "b");
        Scribe_Values.Look(ref Kind, "kind");
        Scribe_Values.Look(ref Known, "known", true);
        Scribe_Values.Look(ref JamUntil, "jamUntil", -1);
        Scribe_Values.Look(ref BrokenUntil, "brokenUntil", -1);
        Scribe_Values.Look(ref TollUntil, "tollUntil", -1);
        Scribe_Values.Look(ref ExpiresTick, "expires", -1);
    }
}

/// <summary>The cyberspace map: backbone nodes, links, districts. Generated once per game from the world seed.</summary>
public class CyberMap : IExposable
{
    public List<MapNode> Nodes = [];
    public List<MapLink> Links = [];
    public NetSize Size;
    public NetLayout Layout;
    public int HomeId;
    public int DarkHubId = -1;
    /// <summary>The wall to the Old Net opens after the first corporate core falls.</summary>
    public bool WallOpen;
    public int NextNodeId;

    private Dictionary<int, MapNode> _byId;
    private Dictionary<int, List<MapLink>> _adjacency;

    public MapNode Node(int id)
    {
        if (_byId == null || _byId.Count != Nodes.Count)
            _byId = Nodes.ToDictionary(n => n.Id);
        return _byId.TryGetValue(id, out MapNode n) ? n : null;
    }

    public MapNode Home => Node(HomeId);

    public void Dirty()
    {
        _byId = null;
        _adjacency = null;
    }

    public List<MapLink> LinksOf(int id)
    {
        if (_adjacency == null)
        {
            _adjacency = new Dictionary<int, List<MapLink>>();
            foreach (MapLink l in Links)
            {
                if (!_adjacency.TryGetValue(l.A, out List<MapLink> a))
                    _adjacency[l.A] = a = [];
                if (!_adjacency.TryGetValue(l.B, out List<MapLink> b))
                    _adjacency[l.B] = b = [];
                a.Add(l);
                b.Add(l);
            }
        }
        return _adjacency.TryGetValue(id, out List<MapLink> list) ? list : [];
    }

    public MapLink LinkBetween(int a, int b) => LinksOf(a).FirstOrDefault(l => l.Other(a) == b);

    /// <summary>Can a runner use this link now.</summary>
    public bool Passable(MapLink l)
    {
        if (!l.Known || l.Broken)
            return false;
        if (l.Kind == LinkKind.Wall && !WallOpen)
            return false;
        if (l.ExpiresTick >= 0 && Find.TickManager.TicksGame >= l.ExpiresTick)
            return false;
        return true;
    }

    /// <summary>Game hours to cross a link (before the runner's own speed).</summary>
    public float Hours(MapLink l, int from)
    {
        float hours = l.Kind == LinkKind.Fast ? 0.5f : 1f;
        if (l.Jammed)
            hours *= 2f;
        if (Node(from)?.Transit == true || Node(l.Other(from))?.Transit == true)
            hours = 0.05f;
        return hours;
    }

    /// <summary>Shortest route in hours (Dijkstra over ~45 nodes, only on demand). Empty if unreachable.</summary>
    public List<int> Route(int from, int to, out float hours)
    {
        hours = 0f;
        if (from == to)
            return [];
        var dist = new Dictionary<int, float> { [from] = 0f };
        var prev = new Dictionary<int, int>();
        var open = new List<int> { from };
        var done = new HashSet<int>();
        while (open.Count > 0)
        {
            int u = open.MinBy(i => dist[i]);
            open.Remove(u);
            if (u == to)
                break;
            if (!done.Add(u))
                continue;
            MapNode un = Node(u);
            if (u != from && un != null && un.Offline)
                continue;
            foreach (MapLink l in LinksOf(u))
            {
                if (!Passable(l))
                    continue;
                int v = l.Other(u);
                if (done.Contains(v))
                    continue;
                float nd = dist[u] + Hours(l, u);
                if (!dist.TryGetValue(v, out float old) || nd < old)
                {
                    dist[v] = nd;
                    prev[v] = u;
                    if (!open.Contains(v))
                        open.Add(v);
                }
            }
        }
        if (!dist.ContainsKey(to))
            return [];
        hours = dist[to];
        var path = new List<int>();
        for (int c = to; c != from; c = prev[c])
            path.Add(c);
        path.Reverse();
        return path;
    }

    /// <summary>Hops between two nodes over passable links (breadth-first).</summary>
    public int Hops(int a, int b, int max = 99)
    {
        if (a == b)
            return 0;
        var seen = new Dictionary<int, int> { [a] = 0 };
        var q = new Queue<int>();
        q.Enqueue(a);
        while (q.Count > 0)
        {
            int u = q.Dequeue();
            if (seen[u] >= max)
                continue;
            foreach (MapLink l in LinksOf(u))
            {
                if (!Passable(l))
                    continue;
                int v = l.Other(u);
                if (seen.ContainsKey(v))
                    continue;
                seen[v] = seen[u] + 1;
                if (v == b)
                    return seen[v];
                q.Enqueue(v);
            }
        }
        return 99;
    }

    /// <summary>Would every node still reach Home over permanent links without this one (the last path never breaks).</summary>
    public bool SafeToRemove(MapLink link)
    {
        var seen = new HashSet<int> { HomeId };
        var stack = new Stack<int>();
        stack.Push(HomeId);
        while (stack.Count > 0)
        {
            int u = stack.Pop();
            foreach (MapLink l in LinksOf(u))
            {
                if (l == link || l.Kind is LinkKind.Dark or LinkKind.Wall or LinkKind.Backdoor)
                    continue;
                int v = l.Other(u);
                if (seen.Add(v))
                    stack.Push(v);
            }
        }
        return Nodes.Where(n => n.District is not (District.Dark or District.OldNet or District.Orbit)).All(n => seen.Contains(n.Id));
    }

    public void ExposeData()
    {
        Scribe_Collections.Look(ref Nodes, "nodes", LookMode.Deep);
        Scribe_Collections.Look(ref Links, "links", LookMode.Deep);
        Scribe_Values.Look(ref Size, "size");
        Scribe_Values.Look(ref Layout, "layout");
        Scribe_Values.Look(ref HomeId, "homeId");
        Scribe_Values.Look(ref DarkHubId, "darkHubId", -1);
        Scribe_Values.Look(ref WallOpen, "wallOpen");
        Scribe_Values.Look(ref NextNodeId, "nextNodeId");
        Nodes ??= [];
        Links ??= [];
        Dirty();
    }
}
