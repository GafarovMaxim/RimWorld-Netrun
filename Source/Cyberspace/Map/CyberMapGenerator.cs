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

    /// <summary>
    /// The layout for this world: Ring 40%, the other three 20% each (CYBERSPACE_V2, section 10).
    /// <paramref name="picked"/>: a NetLayout, -1 for the seed's pick, -2 (default) for the mod setting.
    /// </summary>
    public static NetLayout LayoutFor(int seed, int picked = -2)
    {
        if (picked == -2)
            picked = NetrunMod.Settings.MapLayout;
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

    private static readonly Dictionary<NetLayout, CyberMap> Samples = [];

    /// <summary>A normal-size map of this layout from a fixed seed, for the preview in the settings (cached).</summary>
    public static CyberMap Sample(NetLayout layout)
    {
        if (Samples.TryGetValue(layout, out CyberMap map))
            return map;
        Rand.PushState(0x4E52 + (int)layout);
        try
        {
            map = Build(NetSize.Normal, layout);
        }
        finally
        {
            Rand.PopState();
        }
        Samples[layout] = map;
        return map;
    }

    public static CyberMap Generate(NetSize size, int seed, int pickedLayout = -2)
    {
        NetLayout layout = LayoutFor(seed, pickedLayout);
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
            float r = 0.07f + 0.016f * n;
            int placed = 0, tries = 0;
            // Room between hubs for their name plates and markers (Maxim, 2026-10-14 and 2026-10-16: the map was cramped).
            float minDist = Spacing;
            // Home goes on the left edge later: keep its spot clear, or a neighbour sat on top of it.
            var homeSpot = new Vector2(0.02f, L.Centers[Outskirts].y);
            while (placed < n && tries < 4000)
            {
                tries++;
                if (tries % 1000 == 0)
                    minDist *= 0.9f;
                float a = Rand.Range(0f, Mathf.PI * 2f), rr = r * Mathf.Sqrt(Rand.Value);
                float x = c.x + Mathf.Cos(a) * rr, y = c.y + Mathf.Sin(a) * rr * 0.9f;
                if (!(x > 0.03f && x < 0.985f && y > 0.03f && y < 0.64f))
                    continue;
                if (nodes.Any(o => Vector2.Distance(new Vector2(x, y), o.Pos) < minDist))
                    continue;
                if (Vector2.Distance(new Vector2(x, y), homeSpot) < HomeClearance)
                    continue;
                Add(key, x, y, Rand.RangeInclusive(lo, hi));
                placed++;
            }
        }

        Relax(nodes, new Vector2(0.02f, L.Centers[Outskirts].y));

        // Home and the darknet hub are placed before any link: every link then comes from one planar pool (below).
        MapNode home = Add(Outskirts, 0.02f, L.Centers[Outskirts].y, 0);
        home.District = District.Home;
        home.Exit = true;
        map.HomeId = home.Id;
        keyOf[home.Id] = "home";
        Vector2 dp = Enumerable.Range(0, 200).Select(_ => new Vector2(Rand.Range(0.30f, 0.80f), Rand.Range(0.10f, 0.58f)))
            .MaxBy(p => nodes.Min(n => Vector2.Distance(p, n.Pos)));
        MapNode dark = Add(Market, dp.x, dp.y, 2);
        dark.District = District.Dark;
        map.DarkHubId = dark.Id;
        keyOf[dark.Id] = "dark";

        List<int> Ids(string key) => nodes.Where(n => keyOf[n.Id] == key).Select(n => n.Id).ToList();
        List<int> main = nodes.Where(n => keyOf[n.Id] is not (OldNet or "home" or "dark")).Select(n => n.Id).ToList();
        List<int> net = main.Append(home.Id).ToList();
        float Dist(int a, int b) => Vector2.Distance(map.Node(a).Pos, map.Node(b).Pos);
        // Every link is a pair of the Gabriel graph of all hubs: any set of its pairs is planar, so links never cross
        // (Maxim, 2026-10-22: links still crossed, the forced links were the cause). Pairs that pass close to another hub
        // are kept only as a last resort, for the links the net cannot do without.
        Dictionary<long, bool> pool = GabrielPool(map);
        bool Link(int a, int b, LinkKind kind, bool loose = false)
        {
            if (a == b)
                return false;
            MapLink existing = map.LinkBetween(a, b);
            if (existing != null)
            {
                if (kind == LinkKind.Fast)
                    existing.Kind = LinkKind.Fast;
                return true;
            }
            if (!pool.TryGetValue(PairKey(a, b), out bool clear) || (!clear && !loose))
                return false;
            map.Links.Add(new MapLink { A = a, B = b, Kind = kind, Known = kind != LinkKind.Dark });
            map.Dirty();
            return true;
        }
        bool LinkBest(IEnumerable<(float d, int a, int b)> pairs, LinkKind kind, bool loose = false)
        {
            foreach ((float _, int a, int b) in pairs.OrderBy(p => p.d))
                if (Link(a, b, kind, loose))
                    return true;
            return false;
        }
        // The shortest tree through a set of hubs (Kruskal over the pool, clear pairs first): keeps them connected.
        void Tree(List<int> ids, LinkKind kind)
        {
            var set = new HashSet<int>(ids);
            var root = ids.ToDictionary(i => i, i => i);
            int Find(int i) => root[i] == i ? i : root[i] = Find(root[i]);
            var pairs = pool.Select(kv => (a: (int)(kv.Key >> 32), b: (int)(kv.Key & 0xFFFFFFFF), clear: kv.Value))
                .Where(p => set.Contains(p.a) && set.Contains(p.b))
                .OrderBy(p => p.clear ? 0 : 1).ThenBy(p => Dist(p.a, p.b));
            foreach ((int a, int b, bool _) in pairs)
            {
                int ra = Find(a), rb = Find(b);
                if (ra == rb)
                    continue;
                if (Link(a, b, kind, loose: true))
                    root[ra] = rb;
            }
        }
        List<(float d, int a, int b)> PoolPairs(System.Func<int, int, bool> where)
            => pool.Keys.Select(k => ((int)(k >> 32), (int)(k & 0xFFFFFFFF)))
                .SelectMany(p => new[] { p, (p.Item2, p.Item1) })
                .Where(p => where(p.Item1, p.Item2)).Select(p => (Dist(p.Item1, p.Item2), p.Item1, p.Item2)).ToList();

        // The backbone: the shortest tree through the main net and Home.
        Tree(net, LinkKind.Normal);

        // Short neighbours inside a district, a few links between districts.
        var crossPairs = new Dictionary<string, int>();
        foreach ((float d, int i, int j) in PoolPairs((a, b) => a < b && main.Contains(a) && main.Contains(b)).OrderBy(c => c.d))
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
            if (used < L.Cross && d < 0.20f && Link(i, j, LinkKind.Normal))
                crossPairs[pair] = used + 1;
        }

        // Fast links: between transit hubs that are neighbours in the pool, and from each district to the transit.
        var hub = new HashSet<int>(Ids(Hub));
        foreach ((float _, int a, int b) in PoolPairs((a, b) => a < b && hub.Contains(a) && hub.Contains(b)))
            Link(a, b, LinkKind.Fast, loose: true);
        foreach (string key in counts.Keys)
        {
            if (key is Hub or OldNet || hub.Count == 0)
                continue;
            var ids = new HashSet<int>(Ids(key));
            List<(float d, int a, int b)> pairs = PoolPairs((a, b) => ids.Contains(a) && hub.Contains(b));
            LinkBest(pairs, LinkKind.Fast);
            if (L.Express == "ring" && ids.Count > 2)
                LinkBest(pairs.Where(p => map.LinkBetween(p.a, p.b) == null), LinkKind.Fast);
        }

        // Degree cap 3 for normal links (transit hubs may keep more), as long as the net stays connected.
        foreach (int i in main)
        {
            int cap = keyOf[i] == Hub ? 5 : 3;
            foreach (MapLink l in map.LinksOf(i).Where(l => l.Kind == LinkKind.Normal).OrderByDescending(l => Dist(l.A, l.B)).ToList())
            {
                if (map.LinksOf(i).Count <= cap)
                    break;
                map.Links.Remove(l);
                map.Dirty();
                if (!Connected(map, net))
                {
                    map.Links.Add(l);
                    map.Dirty();
                }
            }
        }

        // Home: up to two links into the outskirts.
        var outskirts = new HashSet<int>(Ids(Outskirts));
        List<(float d, int a, int b)> homePairs = PoolPairs((a, b) => a == home.Id && outskirts.Contains(b));
        if (map.LinksOf(home.Id).Count < 2)
            LinkBest(homePairs.Where(p => map.LinkBetween(p.a, p.b) == null), LinkKind.Normal);

        // The Old Net behind the wall: its own tree, one wall link to the nearest main hub outside the transit.
        List<int> old = Ids(OldNet);
        Tree(old, LinkKind.Normal);
        if (old.Count > 0)
        {
            var oldSet = new HashSet<int>(old);
            var mainSet = new HashSet<int>(main);
            if (!LinkBest(PoolPairs((a, b) => oldSet.Contains(a) && mainSet.Contains(b) && !hub.Contains(b)), LinkKind.Wall, loose: true))
                LinkBest(PoolPairs((a, b) => oldSet.Contains(a) && mainSet.Contains(b)), LinkKind.Wall, loose: true);
        }

        // The darknet: hidden links from its hub to the nearest ruins, dump and market hub it can reach in the pool.
        foreach (string key in new[] { Ruins, Dump, Market })
        {
            var ids = new HashSet<int>(Ids(key));
            LinkBest(PoolPairs((a, b) => a == dark.Id && ids.Contains(b)), LinkKind.Dark, loose: true);
        }
        if (map.LinksOf(dark.Id).Count == 0)
            LinkBest(PoolPairs((a, b) => a == dark.Id && main.Contains(b)), LinkKind.Dark, loose: true);

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
        // Self-check: the pool makes crossings impossible, so the safety net should find nothing.
        int crossings = Uncross(map);
        if (crossings > 0 && Prefs.DevMode)
            Log.Warning($"[Netrun] Map generator: {crossings} crossing links had to be removed ({layout}).");
        map.Dirty();
        return map;
    }

    /// <summary>The gap the generator keeps between hubs (Maxim, 2026-10-16: the map was noisy, links crossed).</summary>
    public const float Spacing = 0.09f;

    /// <summary>
    /// Pushes apart hubs that the random placement left closer than the spacing (it shrinks the gap when a district runs
    /// out of room), inside the field and off Home's spot. Runs before any link is made.
    /// </summary>
    private static void Relax(List<MapNode> nodes, Vector2 homeSpot)
    {
        int count = nodes.Count;
        var push = new Vector2[count];
        for (int iter = 0; iter < 80; iter++)
        {
            bool moved = false;
            System.Array.Clear(push, 0, count);
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    Vector2 d = nodes[i].Pos - nodes[j].Pos;
                    float len = d.magnitude;
                    if (len >= Spacing)
                        continue;
                    Vector2 dir = len < 1e-5f ? new Vector2(Mathf.Cos(i * 2.4f), Mathf.Sin(i * 2.4f)) : d / len;
                    Vector2 step = dir * ((Spacing - len) * 0.5f);
                    push[i] += step;
                    push[j] -= step;
                    moved = true;
                }
                Vector2 h = nodes[i].Pos - homeSpot;
                if (h.magnitude < HomeClearance)
                    push[i] += (h.sqrMagnitude < 1e-6f ? Vector2.right : h.normalized) * (HomeClearance - h.magnitude);
            }
            if (!moved)
                break;
            for (int i = 0; i < count; i++)
            {
                nodes[i].X = Mathf.Clamp(nodes[i].X + push[i].x * 0.6f, 0.03f, 0.985f);
                nodes[i].Y = Mathf.Clamp(nodes[i].Y + push[i].y * 0.6f, 0.03f, 0.64f);
            }
        }
    }

    /// <summary>How far other hubs keep from Home (Maxim, 2026-10-15: a neighbour sat right on top of it).</summary>
    public const float HomeClearance = 0.11f;

    /// <summary>One key per unordered pair of hub ids.</summary>
    public static long PairKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

    /// <summary>
    /// The Gabriel graph of all hubs (a pair whose diameter circle holds no other hub): the pool every link comes from.
    /// It is planar, so no two of its pairs cross. The value: the pair keeps CyberMap.LinkClearance from every other hub.
    /// O(n^3) for about 60 hubs: only at generation and in the one-time pass for older saves.
    /// </summary>
    public static Dictionary<long, bool> GabrielPool(CyberMap map)
    {
        var pool = new Dictionary<long, bool>();
        List<MapNode> all = map.Nodes;
        for (int i = 0; i < all.Count; i++)
        {
            for (int j = i + 1; j < all.Count; j++)
            {
                Vector2 pi = all[i].Pos, pj = all[j].Pos, mid = (pi + pj) / 2f;
                float r2 = (pi - pj).sqrMagnitude / 4f;
                bool gabriel = true, clear = true;
                for (int k = 0; k < all.Count && gabriel; k++)
                {
                    if (k == i || k == j)
                        continue;
                    if ((all[k].Pos - mid).sqrMagnitude < r2)
                        gabriel = false;
                    else if (clear && CyberMap.DistanceToSegment(all[k].Pos, pi, pj) < CyberMap.LinkClearance)
                        clear = false;
                }
                if (gabriel)
                    pool[PairKey(all[i].Id, all[j].Id)] = clear;
            }
        }
        return pool;
    }

    /// <summary>
    /// Removes crossings, for older saves and as a safety net after generation. Of two crossing links a backdoor goes
    /// first, then the longer one (normal, fast, or a dark shortcut not on the darknet hub). When cutting it would split
    /// the net, the two parts are joined again by the shortest clear pair of the Gabriel pool, if there is one. Hubs
    /// never move; runners on a removed link find another way themselves.
    /// </summary>
    public static int Uncross(CyberMap map)
    {
        if (map.Node(map.HomeId) == null)
            return 0;
        List<int> reach = Reachable(map, map.HomeId);
        Dictionary<long, bool> pool = null;
        bool Removable(MapLink l) => l.Kind is LinkKind.Normal or LinkKind.Fast or LinkKind.Backdoor
            || (l.Kind == LinkKind.Dark && !l.Has(map.DarkHubId));
        float Len(MapLink l) => Vector2.Distance(map.Node(l.A).Pos, map.Node(l.B).Pos);
        int removed = 0;
        for (int guard = 0; guard < 200; guard++)
        {
            bool changed = false;
            List<MapLink> links = map.Links.ToList();
            for (int i = 0; i < links.Count && !changed; i++)
            {
                for (int j = i + 1; j < links.Count && !changed; j++)
                {
                    MapLink a = links[i], b = links[j];
                    if (a.Has(b.A) || a.Has(b.B) || !map.Crosses(a, b))
                        continue;
                    foreach (MapLink cut in new[] { a, b }.Where(Removable).OrderBy(l => l.Kind == LinkKind.Backdoor ? 0 : 1).ThenByDescending(Len))
                    {
                        map.Links.Remove(cut);
                        map.Dirty();
                        if (Connected(map, reach))
                        {
                            removed++;
                            changed = true;
                            break;
                        }
                        // A bridge: join the two parts by another way, or keep it.
                        pool ??= GabrielPool(map);
                        var near = new HashSet<int>(Reachable(map, map.HomeId));
                        var join = pool.Keys.Select(k => ((int)(k >> 32), (int)(k & 0xFFFFFFFF)))
                            .Where(p => near.Contains(p.Item1) != near.Contains(p.Item2) && reach.Contains(p.Item1) && reach.Contains(p.Item2)
                                && map.LinkClear(p.Item1, p.Item2))
                            .OrderBy(p => Vector2.Distance(map.Node(p.Item1).Pos, map.Node(p.Item2).Pos)).ToList();
                        if (join.Count > 0)
                        {
                            map.Links.Add(new MapLink { A = join[0].Item1, B = join[0].Item2, Kind = LinkKind.Normal });
                            map.Dirty();
                            if (Connected(map, reach))
                            {
                                removed++;
                                changed = true;
                                break;
                            }
                            map.Links.RemoveAt(map.Links.Count - 1);
                        }
                        map.Links.Add(cut);
                        map.Dirty();
                    }
                }
            }
            if (!changed)
                break;
        }
        return removed;
    }

    private static List<int> Reachable(CyberMap map, int from)
    {
        var seen = new HashSet<int> { from };
        var stack = new Stack<int>();
        stack.Push(from);
        while (stack.Count > 0)
        {
            int u = stack.Pop();
            foreach (MapLink l in map.LinksOf(u))
                if (l.Kind is LinkKind.Normal or LinkKind.Fast && seen.Add(l.Other(u)))
                    stack.Push(l.Other(u));
        }
        return seen.ToList();
    }

    /// <summary>Older saves: hubs too close to Home step back to the clearance, where there is room.</summary>
    public static void ClearHome(CyberMap map)
    {
        MapNode home = map.Node(map.HomeId);
        if (home == null)
            return;
        foreach (MapNode n in map.Nodes)
        {
            if (n.Id == home.Id || n.District is District.OldNet or District.Orbit)
                continue;
            Vector2 d = n.Pos - home.Pos;
            if (d.magnitude >= HomeClearance * 0.8f)
                continue;
            Vector2 to = home.Pos + (d.sqrMagnitude < 1e-6f ? Vector2.right : d.normalized) * HomeClearance;
            if (to.x < 0.03f || to.y < 0.03f || to.y > 0.64f || map.Nodes.Any(o => o.Id != n.Id && Vector2.Distance(o.Pos, to) < 0.05f))
                continue;
            n.X = to.x;
            n.Y = to.y;
        }
        map.Dirty();
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
