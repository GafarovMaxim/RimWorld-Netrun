using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Builds the cyberspace map (docs/CYBERSPACE_V2.md, sections 4 and 9). A port of tools/gen_cyber_maps.py:
/// districts placed by layout, Gabriel graph inside the net, cross-district links by layout, fast links through the
/// transit hub, Home on the left edge, the Old Net behind the wall, a hidden darknet hub, fortresses and exits.
/// Coordinates: x 0..1, y 0..0.66.
/// </summary>
public static class CyberMapGenerator
{
    private const string Outskirts = "outskirts", Hub = "hub", Market = "market", Ruins = "ruins", Campus = "campus",
        Fun = "fun", Dump = "dump", OldNet = "oldnet";
    private static readonly string[] CorpKeys = ["corpA", "corpB", "corpC", "corpD"];

    private static readonly Dictionary<string, (int lo, int hi)> SlotRange = new()
    {
        [Outskirts] = (1, 2), [Hub] = (0, 1), ["corpA"] = (2, 2), ["corpB"] = (2, 2), ["corpC"] = (2, 2), ["corpD"] = (2, 2),
        [Market] = (2, 3), [Ruins] = (1, 2), [Campus] = (2, 2), [Fun] = (2, 2), [Dump] = (1, 2), [OldNet] = (1, 1),
    };

    private static Dictionary<string, int> Counts(NetSize size) => size switch
    {
        NetSize.Small => new() { [Outskirts] = 3, [Hub] = 3, ["corpA"] = 3, ["corpB"] = 3, ["corpC"] = 3, [Market] = 3, [Ruins] = 2, [Fun] = 2, [Dump] = 2, [OldNet] = 2 },
        NetSize.Normal => new() { [Outskirts] = 4, [Hub] = 4, ["corpA"] = 3, ["corpB"] = 3, ["corpC"] = 3, [Market] = 4, [Ruins] = 4, [Campus] = 3, [Fun] = 2, [Dump] = 2, [OldNet] = 3 },
        _ => new() { [Outskirts] = 5, [Hub] = 5, ["corpA"] = 3, ["corpB"] = 3, ["corpC"] = 3, ["corpD"] = 3, [Market] = 5, [Ruins] = 4, [Campus] = 3, [Fun] = 3, [Dump] = 4, [OldNet] = 3 },
    };

    private class LayoutData
    {
        public Dictionary<string, Vector2> Centers;
        public int Cross;
        public string Express;
    }

    private static LayoutData Data(NetLayout layout) => layout switch
    {
        NetLayout.Spine => new LayoutData
        {
            Cross = 1, Express = "chain",
            Centers = C((Outskirts, 0.07f, 0.34f), (Hub, 0.50f, 0.34f), ("corpA", 0.32f, 0.10f), ("corpB", 0.58f, 0.10f), ("corpC", 0.66f, 0.58f),
                ("corpD", 0.84f, 0.10f), (Market, 0.20f, 0.56f), (Ruins, 0.42f, 0.58f), (Campus, 0.18f, 0.12f), (Fun, 0.86f, 0.58f), (Dump, 0.82f, 0.34f), (OldNet, 0.96f, 0.34f)),
        },
        NetLayout.Archipelago => new LayoutData
        {
            Cross = 0, Express = "star",
            Centers = C((Outskirts, 0.08f, 0.34f), (Hub, 0.46f, 0.33f), ("corpA", 0.30f, 0.08f), ("corpB", 0.64f, 0.08f), ("corpC", 0.68f, 0.58f),
                ("corpD", 0.32f, 0.60f), (Market, 0.22f, 0.34f), (Ruins, 0.50f, 0.62f), (Campus, 0.48f, 0.06f), (Fun, 0.70f, 0.33f), (Dump, 0.84f, 0.18f), (OldNet, 0.96f, 0.46f)),
        },
        NetLayout.Metro => new LayoutData
        {
            Cross = 3, Express = "ring",
            Centers = C((Outskirts, 0.08f, 0.34f), (Hub, 0.50f, 0.34f), ("corpA", 0.34f, 0.12f), ("corpB", 0.66f, 0.12f), ("corpC", 0.66f, 0.56f),
                ("corpD", 0.34f, 0.56f), (Market, 0.22f, 0.20f), (Ruins, 0.22f, 0.50f), (Campus, 0.50f, 0.08f), (Fun, 0.50f, 0.60f), (Dump, 0.80f, 0.34f), (OldNet, 0.95f, 0.34f)),
        },
        _ => new LayoutData
        {
            Cross = 2, Express = "star",
            Centers = C((Outskirts, 0.08f, 0.36f), (Hub, 0.46f, 0.34f), ("corpA", 0.42f, 0.08f), ("corpB", 0.72f, 0.16f), ("corpC", 0.70f, 0.54f),
                ("corpD", 0.30f, 0.47f), (Market, 0.22f, 0.16f), (Ruins, 0.18f, 0.60f), (Campus, 0.62f, 0.34f), (Fun, 0.46f, 0.60f), (Dump, 0.80f, 0.36f), (OldNet, 0.95f, 0.30f)),
        },
    };

    private static Dictionary<string, Vector2> C(params (string k, float x, float y)[] items) => items.ToDictionary(i => i.k, i => new Vector2(i.x, i.y));

    /// <summary>The layout for this world: Ring 40%, the other three 20% each (CYBERSPACE_V2, section 10).</summary>
    public static NetLayout LayoutFor(int seed)
    {
        int picked = NetrunMod.Settings.MapLayout;
        if (picked >= 0 && picked <= (int)NetLayout.Metro)
            return (NetLayout)picked;
        Rand.PushState(seed ^ 0x4E52);
        float r = Rand.Value;
        Rand.PopState();
        return r < 0.4f ? NetLayout.Ring : r < 0.6f ? NetLayout.Spine : r < 0.8f ? NetLayout.Archipelago : NetLayout.Metro;
    }

    private static District DistrictOf(string key) => key switch
    {
        Outskirts => District.Outskirts,
        Hub => District.Transit,
        Market => District.Market,
        Ruins => District.Ruins,
        Campus => District.Campus,
        Fun => District.Entertainment,
        Dump => District.Dump,
        OldNet => District.OldNet,
        _ => District.Corp,
    };

    public static CyberMap Generate(NetSize size, int seed)
    {
        NetLayout layout = LayoutFor(seed);
        Rand.PushState(seed);
        try
        {
            return Build(size, layout);
        }
        finally
        {
            Rand.PopState();
        }
    }

    private static CyberMap Build(NetSize size, NetLayout layout)
    {
        var map = new CyberMap { Size = size, Layout = layout };
        LayoutData L = Data(layout);
        Dictionary<string, int> counts = Counts(size);
        // The player's hub count (a multiplier of the size); corporations keep their own count.
        float scale = NetrunMod.Settings.MapHubScale;
        if (!Mathf.Approximately(scale, 1f))
            foreach (string k in counts.Keys.ToList())
                if (!k.StartsWith("corp"))
                    counts[k] = Mathf.Max(1, Mathf.RoundToInt(counts[k] * scale));
        var keyOf = new Dictionary<int, string>();
        var nodes = map.Nodes;

        MapNode Add(string key, float x, float y, int slots)
        {
            var n = new MapNode
            {
                Id = map.NextNodeId++,
                District = DistrictOf(key),
                CorpSlot = System.Array.IndexOf(CorpKeys, key),
                X = x,
                Y = y,
                Slots = slots,
            };
            nodes.Add(n);
            keyOf[n.Id] = key;
            return n;
        }

        foreach ((string key, int n) in counts)
        {
            if (n == 0)
                continue;
            (int lo, int hi) = SlotRange[key];
            if (key == Hub && L.Express == "chain")
            {
                for (int k = 0; k < n; k++)
                    Add(key, 0.22f + k * (0.62f / Mathf.Max(1, n - 1)) + Rand.Range(-0.02f, 0.02f), L.Centers[Hub].y + Rand.Range(-0.04f, 0.04f), Rand.RangeInclusive(lo, hi));
                continue;
            }
            Vector2 c = L.Centers[key];
            float r = 0.06f + 0.012f * n;
            int placed = 0, tries = 0;
            // Room between hubs for their name plates and markers (Maxim, 2026-10-14: the map was cramped).
            float minDist = 0.068f;
            while (placed < n && tries < 4000)
            {
                tries++;
                if (tries % 1000 == 0)
                    minDist *= 0.85f;
                float a = Rand.Range(0f, Mathf.PI * 2f), rr = r * Mathf.Sqrt(Rand.Value);
                float x = c.x + Mathf.Cos(a) * rr, y = c.y + Mathf.Sin(a) * rr * 0.9f;
                if (!(x > 0.03f && x < 0.985f && y > 0.03f && y < 0.64f))
                    continue;
                if (nodes.Any(o => Vector2.Distance(new Vector2(x, y), o.Pos) < minDist))
                    continue;
                Add(key, x, y, Rand.RangeInclusive(lo, hi));
                placed++;
            }
        }

        List<int> Ids(string key) => nodes.Where(n => keyOf[n.Id] == key).Select(n => n.Id).ToList();
        List<int> main = nodes.Where(n => n.District != District.OldNet).Select(n => n.Id).ToList();
        float Dist(int a, int b) => Vector2.Distance(map.Node(a).Pos, map.Node(b).Pos);
        // A link is only added where it crosses no other link and passes no other hub (unless forced: the few links the
        // net cannot do without, when no clear pair is left).
        bool Link(int a, int b, LinkKind kind, bool force = false)
        {
            if (a == b)
                return false;
            MapLink existing = map.Links.FirstOrDefault(l => l.Has(a) && l.Has(b));
            if (existing != null)
            {
                if (kind == LinkKind.Fast)
                    existing.Kind = LinkKind.Fast;
                return true;
            }
            if (!force && !map.LinkClear(a, b))
                return false;
            map.Links.Add(new MapLink { A = a, B = b, Kind = kind, Known = kind != LinkKind.Dark });
            map.Dirty();
            return true;
        }
        // The shortest clear pair of a list, or the shortest one when none is clear.
        void LinkBest(List<(float d, int a, int b)> pairs, LinkKind kind)
        {
            if (pairs.Count == 0)
                return;
            foreach ((float _, int a, int b) in pairs)
                if (Link(a, b, kind))
                    return;
            Link(pairs[0].a, pairs[0].b, kind, force: true);
        }

        // Gabriel graph inside the main net.
        var cand = new List<(float d, int i, int j)>();
        foreach (int i in main)
            foreach (int j in main)
            {
                if (i >= j)
                    continue;
                Vector2 mid = (map.Node(i).Pos + map.Node(j).Pos) / 2f;
                float r2 = (map.Node(i).Pos - map.Node(j).Pos).sqrMagnitude / 4f;
                if (main.All(k => k == i || k == j || (map.Node(k).Pos - mid).sqrMagnitude >= r2))
                    cand.Add((Dist(i, j), i, j));
            }
        var crossPairs = new Dictionary<string, int>();
        foreach ((float d, int i, int j) in cand.OrderBy(c => c.d))
        {
            string ki = keyOf[i], kj = keyOf[j];
            if (ki == kj)
            {
                // Only short neighbours inside a district: longer pairs made triangles and webs (Maxim, 2026-10-14: still tangled).
                if (d < 0.17f)
                    Link(i, j, LinkKind.Normal);
                continue;
            }
            if (ki == Hub || kj == Hub)
                continue;
            string pair = string.CompareOrdinal(ki, kj) < 0 ? ki + "|" + kj : kj + "|" + ki;
            crossPairs.TryGetValue(pair, out int used);
            if (used < L.Cross && d < 0.20f)
            {
                crossPairs[pair] = used + 1;
                Link(i, j, LinkKind.Normal);
            }
        }

        // Fast links: the transit hub.
        List<int> hub = Ids(Hub);
        if (L.Express == "chain")
            hub = hub.OrderBy(i => map.Node(i).X).ToList();
        else
        {
            Vector2 hc = hub.Aggregate(Vector2.zero, (s, i) => s + map.Node(i).Pos) / Mathf.Max(1, hub.Count);
            hub = hub.OrderBy(i => Mathf.Atan2(map.Node(i).Y - hc.y, map.Node(i).X - hc.x)).ToList();
        }
        for (int k = 0; k + 1 < hub.Count; k++)
            Link(hub[k], hub[k + 1], LinkKind.Fast, force: true);
        // The ring closes only where the closing link crosses nothing (the chain already connects the transit).
        if (L.Express != "chain" && hub.Count > 2)
            Link(hub[hub.Count - 1], hub[0], LinkKind.Fast);
        foreach (string key in counts.Keys)
        {
            if (key is Hub or OldNet)
                continue;
            List<int> ids = Ids(key);
            if (ids.Count == 0 || hub.Count == 0)
                continue;
            var pairs = (from i in ids from h in hub select (d: Dist(i, h), a: i, b: h)).OrderBy(p => p.d).ToList();
            LinkBest(pairs, LinkKind.Fast);
            if (L.Express == "ring" && ids.Count > 2)
                foreach ((float _, int a, int b) in pairs)
                    if (map.LinkBetween(a, b) == null && Link(a, b, LinkKind.Fast))
                        break;
        }

        // Each district stays connected inside (a minimum spanning tree as a safety net).
        foreach (string key in counts.Keys)
        {
            List<int> ids = Ids(key);
            if (ids.Count < 2)
                continue;
            var inside = new HashSet<int> { ids[0] };
            while (inside.Count < ids.Count)
            {
                var options = (from a in inside from b in ids where !inside.Contains(b) select (d: Dist(a, b), a, b)).OrderBy(p => p.d).ToList();
                // The shortest clear link that grows the district's tree; the shortest one if none is clear.
                var best = options.FirstOrDefault(o => map.LinkClear(o.a, o.b));
                if (best.a == best.b)
                    best = options[0];
                Link(best.a, best.b, LinkKind.Normal, force: true);
                inside.Add(best.b);
            }
        }

        // Degree cap 3 for normal links (transit hubs may keep more), as long as the main net stays connected.
        foreach (int i in main)
        {
            int cap = keyOf[i] == Hub ? 5 : 3;
            foreach (MapLink l in map.LinksOf(i).Where(l => l.Kind == LinkKind.Normal).OrderByDescending(l => Dist(l.A, l.B)).ToList())
            {
                if (map.LinksOf(i).Count <= cap)
                    break;
                map.Links.Remove(l);
                map.Dirty();
                if (!Connected(map, main))
                {
                    map.Links.Add(l);
                    map.Dirty();
                }
            }
        }

        // Home on the left edge, linked to the two nearest outskirts nodes.
        MapNode home = Add(Outskirts, 0.02f, L.Centers[Outskirts].y, 0);
        home.District = District.Home;
        home.Exit = true;
        map.HomeId = home.Id;
        foreach (int j in Ids(Outskirts).Where(j => j != home.Id).OrderBy(j => Dist(home.Id, j)).Take(2))
            Link(home.Id, j, LinkKind.Normal);

        // The Old Net behind the wall: a chain, one wall link from the nearest main node outside the transit.
        List<int> old = Ids(OldNet).OrderBy(i => map.Node(i).Y).ToList();
        for (int k = 0; k + 1 < old.Count; k++)
            Link(old[k], old[k + 1], LinkKind.Normal);
        if (old.Count > 0)
        {
            var gate = (from i in old from j in main where keyOf[j] != Hub select (d: Dist(i, j), i, j)).OrderBy(p => p.d).First();
            Link(gate.i, gate.j, LinkKind.Wall);
        }

        // The darknet: one hidden hub and dark links to the ruins, the dump and the market, plus one shortcut.
        Vector2 dp = Enumerable.Range(0, 200).Select(_ => new Vector2(Rand.Range(0.30f, 0.80f), Rand.Range(0.10f, 0.58f)))
            .MaxBy(p => nodes.Min(n => Vector2.Distance(p, n.Pos)));
        MapNode dark = Add(Market, dp.x, dp.y, 2);
        dark.District = District.Dark;
        map.DarkHubId = dark.Id;
        foreach (string key in new[] { Ruins, Dump, Market })
        {
            List<int> ids = Ids(key);
            if (ids.Count > 0)
                Link(dark.Id, ids.MinBy(j => Dist(dark.Id, j)), LinkKind.Dark);
        }
        List<int> far = main.OrderByDescending(i => Dist(dark.Id, i)).Take(6).ToList();
        if (far.Count >= 2)
        {
            List<int> pick = far.InRandomOrder().Take(2).ToList();
            Link(pick[0], pick[1], LinkKind.Dark);
        }

        // One fortress per corporation: its node farthest from Home.
        foreach (string key in CorpKeys)
        {
            List<int> ids = Ids(key);
            if (ids.Count > 0)
                map.Node(ids.MaxBy(i => Dist(home.Id, i))).Fortress = true;
        }

        // Exits: about 20% of the main net, at least 2 hops apart, never a fortress.
        int want = Mathf.Max(3, Mathf.RoundToInt(main.Count * 0.2f));
        foreach (int i in main.InRandomOrder())
        {
            if (nodes.Count(n => n.Exit && n.District != District.Home) >= want)
                break;
            MapNode n = map.Node(i);
            if (n.Fortress)
                continue;
            if (n.District == District.Transit || nodes.Where(o => o.Exit).All(o => map.Hops(i, o.Id, 2) >= 2))
                n.Exit = true;
        }
        map.Dirty();
        return map;
    }

    private static bool Connected(CyberMap map, List<int> ids)
    {
        if (ids.Count == 0)
            return true;
        var set = new HashSet<int>(ids);
        var seen = new HashSet<int> { ids[0] };
        var stack = new Stack<int>();
        stack.Push(ids[0]);
        while (stack.Count > 0)
        {
            int u = stack.Pop();
            foreach (MapLink l in map.LinksOf(u))
            {
                if (l.Kind is not (LinkKind.Normal or LinkKind.Fast))
                    continue;
                int v = l.Other(u);
                if (set.Contains(v) && seen.Add(v))
                    stack.Push(v);
            }
        }
        return seen.Count == set.Count;
    }
}
