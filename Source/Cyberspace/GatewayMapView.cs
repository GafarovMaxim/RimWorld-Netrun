using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// The Gateway with the cyberspace map (docs/CYBERSPACE_V2.md): backbone nodes by district, links and their states,
/// branches as small dots around their node, runners walking in game time. The panel shows the runners, the selected
/// node with its branches, and the dive panel for the selected branch.
/// </summary>
public static partial class GatewayView
{
    private static int _selectedMapNode = -1;
    private static int _selectedRunner;
    private static bool _offeredSetup;

    private static readonly Vector2 FieldCenter = new(0.5f, 0.33f);

    public static Color DistrictColor(District d) => d switch
    {
        District.Home => NetrunUI.Accent,
        District.Outskirts => new Color(0.35f, 0.78f, 0.47f),
        District.Transit => new Color(0.24f, 0.82f, 0.9f),
        District.Corp => new Color(0.92f, 0.35f, 0.4f),
        District.Market => new Color(0.9f, 0.82f, 0.47f),
        District.Ruins => new Color(0.67f, 0.59f, 0.51f),
        District.Campus => new Color(0.43f, 0.78f, 0.82f),
        District.Entertainment => new Color(0.94f, 0.47f, 0.6f),
        District.Dump => new Color(0.59f, 0.67f, 0.35f),
        District.Dark => NetrunUI.Purple,
        District.OldNet => new Color(0.86f, 0.24f, 0.27f),
        District.Orbit => new Color(0.78f, 0.86f, 1f),
        _ => NetrunUI.Muted,
    };

    /// <summary>Home is picked: the panel shows the colony defense instead of a dive target.</summary>
    public static bool HomeSelected => NetMapManager.Get() is { Ready: true } net && net.Map.Node(_selectedMapNode)?.District == District.Home;

    public static void SelectMapNode(int id) => FocusNode(id);

    private static NetRunner SelectedRunner(NetMapManager net)
        => net.Runners.Count == 0 ? null : net.Runners[Mathf.Clamp(_selectedRunner, 0, net.Runners.Count - 1)];

    /// <summary>Before the map exists: the prompt that opens the "Net found" window.</summary>
    private static void DrawSetupPrompt(Rect rect)
    {
        Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.25f));
        var r = new Rect(rect.center.x - 180f, rect.center.y - 50f, 360f, 100f);
        Text.Anchor = TextAnchor.UpperCenter;
        Widgets.Label(new Rect(r.x, r.y, r.width, 40f), "NR_NetFoundPrompt".Translate().Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
        if (NetrunUI.Button(new Rect(r.x + 60f, r.y + 46f, r.width - 120f, 40f), "NR_NetFoundOpen".Translate(), NetrunUI.Accent, true))
            Find.WindowStack.Add(new Dialog_NetFound());
        if (!_offeredSetup)
        {
            _offeredSetup = true;
            Find.WindowStack.Add(new Dialog_NetFound());
        }
    }

    private static Vector2 ToScreen(Rect local, Vector2 p)
    {
        float scale = Mathf.Min(local.width / 1.04f, local.height / 0.70f) * _zoom;
        return local.center + (p - FieldCenter) * scale + _pan;
    }

    private static void DrawMap(Rect rect, NetMapManager net)
    {
        // The search box and the minimap lie over the field: clicks and drags there are not for the field below.
        Vector2 mouse = Event.current.mousePosition;
        _mouseOnOverlay = new Rect(rect.x + 6f, rect.y + 6f, 262f, 26f + (_search.NullOrEmpty() ? 0f : 6 * 23f)).Contains(mouse)
                          || NetrunMod.Settings.MapMinimap && new Rect(rect.xMax - 186f, rect.yMax - 122f, 180f, 116f).Contains(mouse)
                          || LensBarRect(rect).Contains(mouse)
                          || Lens != MapLens.Overview && new Rect(rect.x + 6f, rect.yMax - LensBoxH - 6f, 360f, LensBoxH).Contains(mouse)
                          || NetrunMod.Settings.MapLegend && LegendRect(rect, net).Contains(mouse);
        int hovered = _hoverHub;
        _hoverHub = -1;
        if (!_mouseOnOverlay)
            HandleDrag(rect, hovered);
        CyberMap map = net.Map;
        var local = new Rect(0f, 0f, rect.width, rect.height);
        ApplyCenter(local, map);
        MapHotkeys(local, net);
        WasdPan(local);
        Widgets.BeginGroup(rect);
        // The static layer (grid, district names, links, the route preview, packets) only draws on Repaint; the other GUI
        // events of the frame only need the hubs and runners for clicks (docs/PERF_AUDIT_4.md, 6).
        bool paint = Event.current.type == EventType.Repaint;
        NetRunner selRunner = SelectedRunner(net);
        if (paint)
        {
            DrawGrid(local);

            // District names, faint, at the middle of each district (positions and texts rebuilt every 2 s, not per frame).
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            foreach ((Vector2 pos, string text, Color color, bool corp, float radius) in NetrunUI.Cached(DistrictsKey, () => DistrictLabels(net), 2f))
            {
                Vector2 c = ToScreen(local, pos);
                // A corporation's ground: a soft glow in its own colour and a thin dashed edge (red stays for ICE,
                // patrols and fortresses).
                if (corp)
                {
                    float s = 34f * _zoom;
                    NetrunUI.Glow(new Rect(c.x - s, c.y - s, s * 2f, s * 2f), color, 0.24f, 70f * _zoom);
                    float edge = radius * MapScale(local) + 26f * Mathf.Sqrt(_zoom);
                    GUI.color = new Color(color.r, color.g, color.b, 0.3f);
                    GUI.DrawTexture(new Rect(c.x - edge, c.y - edge, edge * 2f, edge * 2f), DashedRing);
                    GUI.color = Color.white;
                }
                // A watermark under the links, moved inside the map when it would run off an edge.
                float lx = Mathf.Clamp(c.x - 90f, 4f, local.width - 184f);
                Widgets.Label(new Rect(lx, c.y - 34f * _zoom - 10f, 180f, 18f), text);
            }
            Text.Anchor = TextAnchor.UpperLeft;

            // Links.
            HashSet<(int, int)> route = RouteSet(selRunner);
            foreach (MapLink l in map.Links)
            {
                if (!l.Known)
                    continue;
                DrawMapLink(ToScreen(local, map.Node(l.A).Pos), ToScreen(local, map.Node(l.B).Pos), l, map,
                    route.Contains((Mathf.Min(l.A, l.B), Mathf.Max(l.A, l.B))));
            }

            DrawPreview(local, net, hovered);
        }

        // Nodes.
        foreach (MapNode n in map.Nodes)
        {
            if (n.District == District.Dark && !HasKnownLink(map, n.Id))
                continue;
            DrawMapNode(local, n, net, paint);
        }

        // Intrusion packets walking toward Home.
        if (paint)
            foreach (IntrusionPacket packet in IntrusionManager.Get()?.Packets ?? [])
                DrawPacket(local, packet, map);

        // Runners.
        foreach (NetRunner r in net.Runners)
            DrawRunner(local, r, net, r == selRunner);
        Widgets.EndGroup();

        DrawZoomButtons(new Rect(rect.xMax - 3 * 34f - 4f, rect.y + 6f, 3 * 34f, 30f));
        DrawHeatEdge(rect);
        DrawRunnerArrows(rect, net);
        DrawMinimap(rect, net);
        DrawLensScale(rect, net);
        DrawLegend(rect, net);
        DrawLensBar(rect);
        DrawSearch(new Rect(rect.x + 6f, rect.y + 6f, rect.width * 0.45f, 26f), net);
        GuideRegistry.Area("gateway.searchbox", new Rect(rect.x + 6f, rect.y + 6f, 262f, 26f));
        GuideRegistry.Area("gateway.lenses", LensBarRect(rect));
        if (NetrunMod.Settings.MapMinimap)
            GuideRegistry.Area("gateway.minimap", new Rect(rect.xMax - 186f, rect.yMax - 122f, 180f, 116f));
    }

    private static string DistrictLabel(MapNode n, NetMapManager net)
    {
        if (n.District == District.Corp)
        {
            Faction owner = net.CorpOfSlot(n.CorpSlot);
            if (owner != null)
                return owner.Name;
        }
        // The Old Net sits behind the Wall: say so while the Wall is shut.
        if (n.District == District.OldNet && !net.Map.WallOpen)
            return "NR_DistrictOldNetWalled".Translate();
        return ("NR_District_" + n.District).Translate();
    }

    /// <summary>D9: up to five loot dots per hub (the best known branch), rebuilt every 2 s, not per frame.</summary>
    private static Dictionary<int, int> LootDots() => NetrunUI.Cached("map.lootDots", () =>
    {
        var dots = new Dictionary<int, int>();
        foreach (KeyValuePair<int, List<ExternalNode>> kv in BranchIndex)
        {
            float best = -1f;
            foreach (ExternalNode b in kv.Value)
                if (!b.Hidden)
                    best = Mathf.Max(best, Mathf.Clamp01((b.RewardMultiplier - 0.5f) / 2.5f));
            if (best >= 0f)
                dots[kv.Key] = Mathf.Clamp(Mathf.CeilToInt(best * 5f), 1, 5);
        }
        return dots;
    }, 2f);

    /// <summary>The exit mark: a small green square with an arrow pointing out; the word "EXIT" from zoom 130%.</summary>
    private static void DrawExitMark(Rect r, float alpha)
    {
        Color g = new(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, alpha);
        Widgets.DrawBoxSolid(r, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.9f * alpha));
        GUI.color = g;
        Widgets.DrawBox(r);
        GUI.color = Color.white;
        float cy = r.center.y;
        Widgets.DrawLine(new Vector2(r.x + 3f, cy), new Vector2(r.xMax - 3f, cy), g, 1.5f);
        Widgets.DrawLine(new Vector2(r.xMax - 6f, cy - 3f), new Vector2(r.xMax - 3f, cy), g, 1.5f);
        Widgets.DrawLine(new Vector2(r.xMax - 6f, cy + 3f), new Vector2(r.xMax - 3f, cy), g, 1.5f);
        if (_zoom >= 1.3f)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = g;
            Widgets.Label(new Rect(r.x - 2f, r.y - 15f, 40f, 14f), "EXIT");
            GUI.color = Color.white;
        }
    }

    private static void DrawLootDots(Rect hubRect, MapNode n)
    {
        if (Lens != MapLens.Overview || _zoom < 1.1f || !LootDots().TryGetValue(n.Id, out int count))
            return;
        float y = hubRect.yMax + 10f * Mathf.Sqrt(_zoom) + 16f;
        float x = hubRect.center.x - count * 2.5f + 0.5f;
        Color gold = NetrunUI.Hex(0xffd34a);
        for (int i = 0; i < count; i++)
            Widgets.DrawBoxSolid(new Rect(x + i * 5f, y, 3f, 3f), new Color(gold.r, gold.g, gold.b, LegendDims(n.Id) ? 0.2f : 0.75f));
    }

    private const string DistrictsKey = "map.districts", PodPawnsKey = "map.podPawns", WatchedKey = "map.watched";

    /// <summary>D3: hubs that an accepted surveillance contract watches (twice a second).</summary>
    public static HashSet<int> WatchedHubs() => NetrunUI.Cached(WatchedKey,
        () => new HashSet<int>(ContractManager.Get()?.Active.Where(c => c.Accepted && c.WatchesHub).Select(c => c.TargetHub) ?? []), 0.5f);

    private static void DrawWatchTag(Rect hubRect)
    {
        Text.Font = GameFont.Tiny;
        string text = NetrunUI.T("NR_MapWatchTag");
        float w = NetrunUI.Width(text) + 8f;
        var tag = new Rect(hubRect.x - w - 4f, hubRect.y - 16f, w, 14f);
        Widgets.DrawBoxSolid(tag, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.85f));
        GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.8f);
        Widgets.DrawBox(tag);
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = NetrunUI.Accent;
        Widgets.Label(tag, text);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }

    /// <summary>A dark hub shows once one of its links is known (a loop, no LINQ enumerator per hub per event).</summary>
    private static bool HasKnownLink(CyberMap map, int id)
    {
        List<MapLink> links = map.LinksOf(id);
        for (int i = 0; i < links.Count; i++)
            if (links[i].Known)
                return true;
        return false;
    }

    private static float MapScale(Rect local) => Mathf.Min(local.width / 1.04f, local.height / 0.70f) * _zoom;

    private static List<(Vector2, string, Color, bool, float)> DistrictLabels(NetMapManager net)
    {
        var list = new List<(Vector2, string, Color, bool, float)>();
        foreach (IGrouping<string, MapNode> g in net.Map.Nodes.Where(n => n.District is not (District.Home or District.Dark))
                     .GroupBy(n => n.District + "|" + n.CorpSlot))
        {
            MapNode first = g.First();
            bool corp = first.District == District.Corp && net.CorpOfSlot(first.CorpSlot) != null;
            Color dc = HubColor(first, net);
            Vector2 centre = g.Aggregate(Vector2.zero, (s, n) => s + n.Pos) / g.Count();
            float radius = g.Max(n => Vector2.Distance(n.Pos, centre));
            list.Add((centre, DistrictLabel(first, net).ToUpper().Colorize(new Color(dc.r, dc.g, dc.b, corp ? 0.5f : 0.3f)), dc, corp, radius));
        }
        return list;
    }

    /// <summary>A hub's colour: its district, or for corporate ground the corporation's own colour, lifted to read on black.</summary>
    private static Color HubColor(MapNode n, NetMapManager net)
    {
        if (n.District == District.Corp && net.CorpOfSlot(n.CorpSlot) is { } owner)
        {
            Color.RGBToHSV(owner.Color, out float h, out float s, out float v);
            return Color.HSVToRGB(h, Mathf.Min(s, 0.8f), Mathf.Max(v, 0.85f));
        }
        return DistrictColor(n.District);
    }

    // The selected runner's route as link pairs, rebuilt only when the runner, its hub or its path changes.
    private static readonly HashSet<(int, int)> Route = [];
    private static (NetRunner runner, int node, int steps, int first) _routeFor;

    private static HashSet<(int, int)> RouteSet(NetRunner r)
    {
        var key = (r, r?.NodeId ?? -1, r?.Path.Count ?? 0, r != null && r.Path.Count > 0 ? r.Path[0] : -1);
        if (key == _routeFor)
            return Route;
        _routeFor = key;
        Route.Clear();
        if (r == null || !r.Moving)
            return Route;
        int from = r.NodeId;
        foreach (int to in r.Path)
        {
            Route.Add((Mathf.Min(from, to), Mathf.Max(from, to)));
            from = to;
        }
        return Route;
    }

    private static void DrawMapLink(Vector2 a, Vector2 b, MapLink l, CyberMap map, bool onRoute)
    {
        bool nearSelected = _selectedMapNode >= 0 && (l.A == _selectedMapNode || l.B == _selectedMapNode);
        bool dim = LegendDims(l.A) && LegendDims(l.B);
        Color c = l.Kind switch
        {
            LinkKind.Fast => new Color(0.24f, 0.82f, 0.9f),
            LinkKind.Dark => NetrunUI.Purple,
            LinkKind.Wall => map.WallOpen ? NetrunUI.Warn : NetrunUI.Bad,
            LinkKind.Backdoor => NetrunUI.Warn,
            _ => new Color(0.55f, 0.62f, 0.55f),
        };
        if (l.Jammed)
            c = new Color(0.95f, 0.6f, 0.2f);
        // A plain link is a thin, quiet line (map v4, D9): the hubs are the picture. Fast links keep their colour, thinner.
        float alpha = l.Broken ? 0.25f : l.Kind == LinkKind.Normal ? 0.5f : 0.65f;
        float width = 1.5f;
        if (nearSelected)
        {
            alpha = Mathf.Max(alpha, 0.85f);
            width = 2f;
        }
        if (dim)
            alpha *= 0.25f;
        if (onRoute)
        {
            Widgets.DrawLine(a, b, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.35f), 7f);
            c = NetrunUI.Accent;
            alpha = 1f;
        }
        if (l.Kind is LinkKind.Dark or LinkKind.Backdoor or LinkKind.Wall)
        {
            // Dashed.
            float len = Vector2.Distance(a, b);
            int segments = Mathf.Max(2, Mathf.FloorToInt(len / 10f));
            for (int i = 0; i < segments; i += 2)
                Widgets.DrawLine(Vector2.Lerp(a, b, i / (float)segments), Vector2.Lerp(a, b, Mathf.Min(1f, (i + 1) / (float)segments)), new Color(c.r, c.g, c.b, alpha), width);
        }
        else
        {
            if (onRoute || nearSelected)
                Widgets.DrawLine(a, b, new Color(c.r, c.g, c.b, 0.12f), width + 4f);
            Widgets.DrawLine(a, b, new Color(c.r, c.g, c.b, alpha), width);
        }
        Vector2 mid = (a + b) / 2f;
        Text.Font = GameFont.Tiny;
        if (l.Kind == LinkKind.Wall && !map.WallOpen)
        {
            // A lock on the Wall's dashed line: the way into the Old Net is shut.
            var lockRect = new Rect(mid.x - 8f, mid.y - 8f, 16f, 16f);
            Widgets.DrawBoxSolid(lockRect.ContractedBy(1f), NetrunUI.BgWindow);
            GUI.color = NetrunUI.Bad;
            if (NetrunTex.CyberIcon("Icon_Lock") is { } lockTex)
                GUI.DrawTexture(lockRect, lockTex);
            GUI.color = Color.white;
            NetrunUI.Tip(lockRect, "NR_MapWallLocked".Translate());
        }
        else if (l.Broken)
            Widgets.Label(new Rect(mid.x - 5f, mid.y - 9f, 14f, 18f), "✕".Colorize(NetrunUI.Bad));
        else if (l.Toll)
        {
            GUI.color = NetrunUI.Warn;
            GUI.DrawTexture(new Rect(mid.x - 4f, mid.y - 4f, 8f, 8f), NetrunUI.Disc);
            GUI.color = Color.white;
        }
        Text.Font = GameFont.Small;
    }

    private static void DrawMapNode(Rect local, MapNode n, NetMapManager net, bool paint)
    {
        Vector2 p = ToScreen(local, n.Pos);
        bool home = n.District == District.Home;
        float r = (home ? 15f : n.Fortress ? 11f : 8f) * Mathf.Sqrt(_zoom);
        var rect = new Rect(p.x - r, p.y - r, r * 2f, r * 2f);
        // Offline hubs and the Old Net behind a shut Wall are greyed out, not drawn like open ground.
        Color c = n.Offline || n.District == District.OldNet && !net.Map.WallOpen ? NetrunUI.Muted : HubColor(n, net);
        if (LensHubColor(n, net) is { } lensColor)
            c = lensColor;
        bool selected = n.Id == _selectedMapNode;
        bool hover = Mouse.IsOver(rect.ExpandedBy(4f));
        // With a runner standing still, the hubs it can step to now are bright and the rest dimmed (Overview only).
        if (Lens == MapLens.Overview && !selected && SelectedRunner(net) is { Moving: false } && !Neighbours(net).Contains(n.Id))
            c = new Color(c.r, c.g, c.b, c.a * 0.55f);
        // D1: a picked legend row dims every hub it does not stand for.
        bool legendLit = _legendLit != null && !LegendDims(n.Id);
        if (LegendDims(n.Id))
            c = new Color(c.r, c.g, c.b, c.a * 0.2f);
        bool noData = Lens != MapLens.Overview && LensValueOf(n, net) < 0f;
        if (hover && !_mouseOnOverlay)
            _hoverHub = n.Id;
        if (!paint)
        {
            HubInput(n, net, rect, hover, selected);
            return;
        }

        if (n.Storm)
            NetrunUI.Glow(rect, NetrunUI.Purple, 0.6f, 10f);
        if (n.Transit)
            NetrunUI.Glow(rect, new Color(0.24f, 0.82f, 0.9f), 0.6f, 8f);
        if (selected || hover || legendLit)
            NetrunUI.Glow(rect, selected ? NetrunUI.Accent : c, selected ? 1f : 0.7f, selected ? 14f : 8f);
        DrawPulse(rect, n.Id);
        DrawPing(rect, n.Id);
        DrawVisited(rect, n);
        GUI.color = NetrunUI.BgWindow;
        GUI.DrawTexture(rect.ExpandedBy(2f), NetrunUI.Disc);
        // Map v4 (D9): a hollow ring in the hub's colour with a faint fill; full only for hubs a runner has been to,
        // and under a lens (its colour is the information) unless the lens has no data for it.
        float a = (n.Offline ? 0.5f : 1f) * c.a;
        bool full = !home && !noData && !n.Offline && (n.LastVisitTick >= 0 || Lens != MapLens.Overview);
        bool orbit = n.District == District.Orbit;
        if (home)
        {
            GUI.color = new Color(c.r, c.g, c.b, a);
            GUI.DrawTexture(rect, NetrunUI.Ring);
        }
        else if (n.Offline)
        {
            // Offline: only a faint dashed outline, no fill and no cross.
            GUI.color = new Color(c.r, c.g, c.b, 0.55f * c.a);
            GUI.DrawTexture(rect, orbit ? ShapeTex(Shape.Diamond, false) : DashedRing);
        }
        else
        {
            Texture2D fill = orbit ? ShapeTex(Shape.Diamond, true) : NetrunUI.Disc;
            Texture2D ring = orbit ? ShapeTex(Shape.Diamond, false) : HubRing;
            GUI.color = new Color(c.r, c.g, c.b, a * (full ? 1f : 0.18f));
            GUI.DrawTexture(orbit ? rect.ExpandedBy(2f) : rect.ContractedBy(1f), fill);
            GUI.color = new Color(c.r, c.g, c.b, a);
            GUI.DrawTexture(orbit ? rect.ExpandedBy(2f) : rect, ring);
        }
        if (n.Fortress)
        {
            // A capital: a diamond core in the corporation's colour and a dashed red ring.
            GUI.color = new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, a);
            GUI.DrawTexture(rect.ContractedBy(rect.width * 0.18f), NetrunUI.Disc);
            Color core = HubColor(n, net);
            GUI.color = new Color(core.r, core.g, core.b, a);
            GUI.DrawTexture(rect.ContractedBy(rect.width * 0.3f), ShapeTex(Shape.Diamond, true));
            GUI.color = new Color(NetrunUI.Bad.r, NetrunUI.Bad.g, NetrunUI.Bad.b, LegendDims(n.Id) ? 0.25f : 0.9f);
            GUI.DrawTexture(rect.ExpandedBy(6f), DashedRing);
        }
        if (selected)
        {
            // The picked hub: corner brackets and a slow pulse ring.
            float pulse = NetrunMod.Settings.Animations ? (Time.realtimeSinceStartup * 0.8f) % 1f : 0.3f;
            NetrunUI.Brackets(rect.ExpandedBy(10f), NetrunUI.Accent, 2f, 6f);
            GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.7f * (1f - pulse));
            GUI.DrawTexture(rect.ExpandedBy(4f + 14f * pulse), NetrunUI.ThinRing);
        }
        if (n.Guarded && NetrunMod.Settings.ProtectedHubs)
        {
            // D2: a protected hub: a lock while shut, an open amber ring while the guard is broken.
            bool shut = n.GuardLocked;
            GUI.color = shut ? NetrunUI.Warn : new Color(NetrunUI.Warn.r, NetrunUI.Warn.g, NetrunUI.Warn.b, 0.5f);
            GUI.DrawTexture(rect.ExpandedBy(7f), DashedRing);
            if (shut && NetrunTex.CyberIcon("Icon_Lock") is { } lockTex)
                GUI.DrawTexture(new Rect(rect.center.x - 7f, rect.y - 18f, 14f, 14f), lockTex);
        }
        if (n.Id == net?.MarkedNodeId)
        {
            GUI.color = NetrunUI.Accent;
            GUI.DrawTexture(rect.ExpandedBy(9f), NetrunUI.ThinRing);
        }
        if (n.Exit && !home)
        {
            GUI.color = new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.8f * c.a);
            GUI.DrawTexture(rect.ExpandedBy(4f), NetrunUI.ThinRing);
            DrawExitMark(new Rect(rect.x - 15f, rect.y - 15f, 12f, 12f), c.a);
        }
        GUI.color = Color.white;
        Text.Font = GameFont.Tiny;
        if (n.Patrol)
        {
            // An ICE patrol: a small red triangle with a mark.
            var warn = new Rect(rect.xMax - 1f, rect.y - 13f, 12f, 12f);
            GUI.color = NetrunUI.Bad;
            GUI.DrawTexture(warn, ShapeTex(Shape.Triangle, true));
            GUI.color = NetrunUI.BgWindow;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(warn.x, warn.y + 1f, warn.width, warn.height), "!");
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
        if (home)
        {
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(p.x - 40f, rect.yMax + 2f, 80f, 16f), ((string)"NR_District_Home".Translate()).ToUpper().Colorize(NetrunUI.Accent));
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // Branches: small dots around the node, colored by type; unknown ones grey.
        List<ExternalNode> branches = BranchesOf(n.Id);
        bool markers = _zoom >= 0.8f;
        for (int i = 0; i < branches.Count; i++)
        {
            ExternalNode b = branches[i];
            float ang = -Mathf.PI / 2f + i * (Mathf.PI * 2f / Mathf.Max(3, branches.Count));
            Vector2 bp = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (r + (markers ? 11f : 7f) * Mathf.Sqrt(_zoom));
            float br = (markers ? (b.IsBoss || b.Ancient ? 7.5f : 5.5f) : b.IsBoss || b.Ancient ? 5f : 3.5f) * Mathf.Sqrt(_zoom);
            var brect = new Rect(bp.x - br, bp.y - br, br * 2f, br * 2f);
            // The node's type as a shape (square market, ring archive, hexagon corp, triangle abandoned, diamond exchange).
            DrawShape(brect, b, c.a);
            if ((b.Contract || b.GuardFor >= 0 && NetrunMod.Settings.ProtectedHubs) && !b.Hidden)
            {
                // A contract target or a guard gateway: a small amber diamond badge on the node.
                var badge = new Rect(brect.xMax - 3f, brect.y - 5f, 8f, 8f);
                GUI.color = NetrunUI.BgWindow;
                GUI.DrawTexture(badge.ExpandedBy(1.5f), ShapeTex(Shape.Diamond, true));
                GUI.color = NetrunUI.Warn;
                GUI.DrawTexture(badge, ShapeTex(Shape.Diamond, true));
                GUI.color = Color.white;
            }
        }
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        if (WatchedHubs().Contains(n.Id))
            DrawWatchTag(rect);
        DrawTimerTag(rect, n, net);
        DrawHubName(rect, n, net, branches);
        DrawLootDots(rect, n);

        if (hover && !_mouseOnOverlay)
            NetrunUI.Tip(rect.ExpandedBy(4f), HubCard(n, net, branches));
    }

    /// <summary>Right click (menu) and left click (select, deselect, travel) on a hub.</summary>
    private static void HubInput(MapNode n, NetMapManager net, Rect rect, bool hover, bool selected)
    {
        if (_mouseOnOverlay)
            return;
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && hover)
        {
            HubMenu(n, net);
            Event.current.Use();
            return;
        }
        if (!Widgets.ButtonInvisible(rect.ExpandedBy(4f)))
            return;
        NetrunSound.Click();
        float sinceLast = Time.realtimeSinceStartup - _lastClickAt;
        if (!TravelClick(n, net) && selected && sinceLast > 0.4f)
        {
            _selectedMapNode = -1;
            _selectedId = -1;
            return;
        }
        _selectedMapNode = n.Id;
        ExternalNode first = BranchesOf(n.Id).FirstOrDefault(b => !b.Hidden);
        _selectedId = first?.Id ?? -1;
    }

    private static IEnumerable<string> NodeStates(MapNode n)
    {
        if (n.Offline)
            yield return "NR_MapStateOffline".Translate();
        if (n.Storm)
            yield return "NR_MapStateStorm".Translate();
        if (n.Patrol)
            yield return "NR_MapStatePatrol".Translate();
        if (n.Transit)
            yield return "NR_MapStateTransit".Translate();
        if (n.RivalTrap)
            yield return "NR_MapStateRivalTrap".Translate();
        if (n.Quarantine)
            yield return "NR_MapStateQuarantine".Translate();
    }

    private static void DrawRunner(Rect local, NetRunner r, NetMapManager net, bool selected)
    {
        CyberMap map = net.Map;
        Vector2 p = ToScreen(local, map.Node(r.NodeId).Pos);
        if (r.Moving && map.Node(r.NextNodeId) is { } next)
        {
            MapLink l = map.LinkBetween(r.NodeId, r.NextNodeId);
            float t = l == null ? 0f : Mathf.Clamp01(r.Progress / Mathf.Max(0.05f, map.Hours(l, r.NodeId)));
            p = Vector2.Lerp(p, ToScreen(local, next.Pos), t);
        }
        float s = 9f * Mathf.Sqrt(_zoom);
        var rect = new Rect(p.x - s, p.y - s - 14f * Mathf.Sqrt(_zoom), s * 2f, s * 2f);
        NetrunUI.Glow(rect, NetrunUI.Ok, selected ? 0.9f : 0.5f, 8f);
        // A pin: a stem down to the hub, a pulsing ring, a dark badge with a green rim and the initial.
        Widgets.DrawLine(new Vector2(rect.center.x, rect.yMax), new Vector2(p.x, p.y), new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.7f), 1.5f);
        if (NetrunMod.Settings.Animations)
        {
            float pulse = (Time.realtimeSinceStartup * 0.9f + r.Pawn?.thingIDNumber % 7 * 0.13f ?? 0f) % 1f;
            GUI.color = new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.6f * (1f - pulse));
            GUI.DrawTexture(rect.ExpandedBy(2f + 8f * pulse), NetrunUI.ThinRing);
        }
        GUI.color = NetrunUI.BgWindow;
        GUI.DrawTexture(rect.ExpandedBy(1f), NetrunUI.Disc);
        GUI.color = selected ? NetrunUI.Ok : new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.85f);
        GUI.DrawTexture(rect.ContractedBy(2f), NetrunUI.Disc);
        GUI.DrawTexture(rect.ExpandedBy(1f), HubRing);
        GUI.color = Color.white;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        string initial = r.Pawn?.LabelShort is { Length: > 0 } name ? name.Substring(0, 1).ToUpper() : "?";
        Widgets.Label(rect, initial.Colorize(NetrunUI.BgWindow));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        TooltipHandler.TipRegion(rect, r.Pawn?.LabelShort ?? "?");
        if (r.Moving && _zoom >= 0.8f)
        {
            string tag = RunnerTag(r, net);
            Text.Font = GameFont.Tiny;
            float w = Text.CalcSize(tag).x + 10f;
            var box = new Rect(rect.center.x - w / 2f, rect.y - 20f, w, 16f);
            Widgets.DrawBoxSolid(box, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.85f));
            GUI.color = new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.7f);
            Widgets.DrawBox(box);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(box, tag.Colorize(NetrunUI.Ok));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        if (Widgets.ButtonInvisible(rect))
            _selectedRunner = net.Runners.IndexOf(r);
    }

    private static void DrawPacket(Rect local, IntrusionPacket p, CyberMap map)
    {
        Vector2 pos = ToScreen(local, map.Node(p.NodeId).Pos);
        if (p.Path.Count > 0 && map.Node(p.Path[0]) is { } next)
            pos = Vector2.Lerp(pos, ToScreen(local, next.Pos), Mathf.Clamp01(p.Progress / 2f));
        float s = 7f * Mathf.Sqrt(_zoom);
        var rect = new Rect(pos.x - s, pos.y - s, s * 2f, s * 2f);
        float pulse = NetrunMod.Settings.Animations ? 0.6f + 0.4f * Mathf.Sin(Time.realtimeSinceStartup * 5f) : 1f;
        NetrunUI.Glow(rect, NetrunUI.Bad, pulse, 10f);
        GUI.color = NetrunUI.Bad;
        GUI.DrawTexture(rect, NetrunUI.Disc);
        GUI.color = Color.white;
        TooltipHandler.TipRegion(rect, "NR_IntrusionTip".Translate(p.Attack?.label ?? "?", (p.Path.Count * 2f - p.Progress).ToString("0.#")));
    }

    // ---- Panel ----

    private static int _exitFor = -1;
    private static float _exitAt = -10f;
    private static bool _exitFound;
    private static (int from, int to) _panelRouteFor = (-1, -1);
    private static float _panelRouteAt = -10f;
    private static int _panelRouteSteps;
    private static float _panelRouteHours;

    /// <summary>Runners and the selected map node, above the branch panel.</summary>
    private static float DrawMapPanel(Rect inner, float y, Map colonyMap, NetMapManager net)
    {
        // Runners: connected ones, then pawns in pods who could connect.
        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_MapRunners".Translate(net.Runners.Count, NetMapManager.Settings.maxRunners));
        for (int i = 0; i < net.Runners.Count; i++)
        {
            NetRunner r = net.Runners[i];
            bool sel = i == Mathf.Clamp(_selectedRunner, 0, net.Runners.Count - 1);
            y = RunnerCard(inner, y, r, net, sel, i);
        }

        NetRunner selected = SelectedRunner(net);
        if (selected != null)
        {
            float bw = (inner.width - 6f) / 2f;
            if (selected.Moving)
            {
                if (NetrunUI.Button(new Rect(inner.x, y, bw, 28f), "NR_MapStop".Translate(), NetrunUI.Muted, true, "NR_MapStopTip".Translate()))
                    net.Stop(selected);
            }
            else
            {
                bool atExit = net.AtExit(selected);
                if (NetrunUI.Button(new Rect(inner.x, y, bw, 28f), "NR_MapExit".Translate(), NetrunUI.Ok, atExit ? true : (AcceptanceReport)"NR_MapNotAtExit".Translate(),
                        "NR_MapExitTip".Translate()))
                    net.Disconnect(selected, emergency: false);
            }
            if (NetrunUI.Button(new Rect(inner.x + bw + 6f, y, bw, 28f), "NR_MapEmergencyButton".Translate(), NetrunUI.Bad, true,
                    "NR_MapEmergencyTip".Translate(NetMapManager.Settings.emergencyNetsick.ToString("0.##"), NetMapManager.Settings.emergencyLockoutChance.ToStringPercent())))
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("NR_MapEmergencyConfirm".Translate(selected.Pawn?.LabelShort ?? "?"),
                    () => net.Disconnect(selected, emergency: true), destructive: true));
            y += 34f;
            if (!net.AtExit(selected))
            {
                // NearestExit runs a route search per exit hub: once a second or when the runner's hub changes, not per GUI event.
                int exitFrom = selected.Moving ? selected.NextNodeId : selected.NodeId;
                float exitNow = Time.realtimeSinceStartup;
                if (_exitFor != exitFrom || exitNow - _exitAt > 1f)
                {
                    _exitFor = exitFrom;
                    _exitAt = exitNow;
                    _exitFound = net.NearestExit(selected) >= 0;
                }
                AcceptanceReport goExit = _exitFound ? true : (AcceptanceReport)"NR_MapNoRoute".Translate();
                if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 28f), "NR_MapGoAndExit".Translate(), NetrunUI.Ok, goExit, "NR_MapGoAndExitTip".Translate()))
                    net.GoAndExit(selected);
                y += 32f;
            }
            if (selected.NodeId != net.Map.HomeId || selected.Moving)
            {
                if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 28f), "NR_MapGoHome".Translate(), NetrunUI.Ok, net.CanTravel(selected, net.Map.HomeId), "NR_MapGoHomeTip".Translate()))
                    net.Travel(selected, net.Map.HomeId);
                y += 32f;
            }
        }

        // Pawns lying in pods who are not connected yet (the list twice a second: PERF_AUDIT_4, 21).
        List<(Pawn pawn, Building_Cyberpod pod)> podPawns = NetrunUI.Cached(PodPawnsKey, () => NetMapManager.PodPawns(colonyMap).ToList(), 0.5f);
        foreach ((Pawn pawn, Building_Cyberpod _) in podPawns)
        {
            if (net.RunnerOf(pawn) != null)
                continue;
            AcceptanceReport can = net.CanConnect(pawn);
            if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 28f), "NR_MapConnect".Translate(pawn.LabelShort), NetrunUI.Accent, can, "NR_MapConnectTip".Translate()))
            {
                net.Connect(pawn, net.Map.HomeId, colonyMap);
                _selectedRunner = net.Runners.Count - 1;
            }
            y += 32f;
        }
        if (net.Runners.Count == 0 && podPawns.Count == 0)
        {
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inner.x, y, inner.width, 34f), "NR_MapNoPodPawn".Translate().Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
            y += 36f;
        }

        // The selected hub; with nothing picked, the running contracts, the watchlist and the next three days.
        MapNode node = net.Map.Node(_selectedMapNode);
        if (node == null)
        {
            y = DrawActiveContracts(inner, y + 4f);
            y = DrawWatchlist(inner, y, net);
            return DrawTimeline(inner, y, net);
        }
        if (node.District == District.Home)
            return DefensePanel.Draw(inner, y, colonyMap);
        y = NetrunUI.Section(new Rect(inner.x, y + 4f, inner.width, 36f), NetMapManager.NodeLabel(node));
        Text.Font = GameFont.Tiny;
        var info = new List<string>();
        if (node.District == District.Corp && net.CorpOfSlot(node.CorpSlot) is { } corp)
            info.Add("NR_MapOwner".Translate(corp.Name));
        if (node.Exit)
            info.Add("NR_MapExitPoint".Translate());
        if (node.Fortress)
            info.Add("NR_MapFortress".Translate());
        info.AddRange(NodeStates(node));
        if (selected != null && selected.NodeId != node.Id)
        {
            // The route search (Dijkstra with fresh collections) ran on every GUI event; now once a second or on a new pair.
            int routeFrom = selected.Moving ? selected.NextNodeId : selected.NodeId;
            float now = Time.realtimeSinceStartup;
            if (_panelRouteFor != (routeFrom, node.Id) || now - _panelRouteAt > 1f)
            {
                List<int> route = net.Map.Route(routeFrom, node.Id, out float routeHours);
                _panelRouteFor = (routeFrom, node.Id);
                _panelRouteAt = now;
                _panelRouteSteps = route.Count;
                _panelRouteHours = routeHours;
            }
            info.Add(_panelRouteSteps > 0 || selected.NextNodeId == node.Id
                ? "NR_MapRouteInfo".Translate(_panelRouteSteps + (selected.Moving ? 1 : 0), (_panelRouteHours / NetMapManager.Speed(selected.Pawn)).ToString("0.#"))
                : "NR_MapNoRoute".Translate());
        }
        string infoText = info.ToLineList();
        float ih = Text.CalcHeight(infoText, inner.width);
        Widgets.Label(new Rect(inner.x, y, inner.width, ih), infoText.Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        y += ih + 4f;

        if (selected != null && (selected.NodeId != node.Id || selected.Moving))
        {
            if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 30f), "NR_MapGo".Translate(), NetrunUI.Accent, net.CanTravel(selected, node.Id), "NR_MapGoTip".Translate()))
                net.Travel(selected, node.Id);
            y += 34f;
        }
        // Connect straight at an exit point (a jump for a little cyberdata).
        if (node.Exit && node.District != District.Home)
        {
            // The first pod pawn not already in the net (a plain loop: this runs on every GUI event).
            Pawn pawn = null;
            foreach ((Pawn p, Building_Cyberpod _) in podPawns)
            {
                if (net.RunnerOf(p) == null)
                {
                    pawn = p;
                    break;
                }
            }
            if (pawn != null)
            {
                if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 28f), "NR_MapJump".Translate(pawn.LabelShort, NetMapManager.Settings.jumpCost), NetrunUI.Ok,
                        net.CanConnect(pawn), "NR_MapJumpTip".Translate()))
                {
                    net.Connect(pawn, node.Id, colonyMap);
                    _selectedRunner = net.Runners.Count - 1;
                }
                y += 32f;
            }
        }

        // An intrusion packet here or about to come: a runner standing here can go out to meet it.
        if (IntrusionManager.Get()?.At(node.Id) is { } packet)
        {
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inner.x, y, inner.width, 18f), "NR_IntrusionHere".Translate(packet.Attack?.label ?? "?").Colorize(NetrunUI.Bad));
            Text.Font = GameFont.Small;
            y += 20f;
            bool runnerHere = net.RunnerAt(node.Id) != null;
            if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 30f), "NR_IntrusionIntercept".Translate(), NetrunUI.Bad,
                    runnerHere ? true : (AcceptanceReport)"NR_MapNeedRunnerHere".Translate(), "NR_IntrusionInterceptTip".Translate()))
            {
                ExternalNode branch = IntrusionManager.Get().OpenIntercept(packet, node.Id);
                if (branch != null)
                    _selectedId = branch.Id;
            }
            y += 34f;
        }

        // The hub's nodes, as in the mockup: the type's shape, the name and type, the defense as pips, marks on the right.
        List<ExternalNode> nodes = BranchesOf(node.Id);
        if (nodes.Count > 0)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = NetrunUI.Muted;
            Widgets.Label(new Rect(inner.x, y, inner.width, 16f), NetrunUI.Upper(NetrunUI.T("NR_MapHubNodes")));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 18f;
        }
        foreach (ExternalNode b in nodes)
        {
            var row = new Rect(inner.x, y, inner.width, 30f);
            Color bc = b.Hidden ? NetrunUI.Muted : b.Offline ? NetrunUI.Muted : b.Color;
            bool picked = b.Id == _selectedId;
            Widgets.DrawBoxSolid(row, picked ? new Color(bc.r, bc.g, bc.b, 0.16f) : Mouse.IsOver(row) ? new Color(1f, 1f, 1f, 0.05f) : new Color(0f, 0f, 0f, 0.18f));
            if (picked)
                Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), bc);
            DrawShape(new Rect(row.x + 9f, row.y + 8f, 14f, 14f), b, 1f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            float right = row.xMax - 74f;
            string name = b.Hidden ? (string)"NR_GatewayHiddenRow".Translate() : b.Name;
            GUI.color = b.Hidden ? NetrunUI.Muted : NetrunUI.TextColor;
            Widgets.Label(new Rect(row.x + 30f, row.y, right - row.x - 30f, 16f + 2f), name.Truncate(right - row.x - 30f));
            if (!b.Hidden)
            {
                GUI.color = NetrunUI.Muted;
                string sub = b.TypeLabel + (b.Deserted ? " · " + NetrunUI.T("NR_MapDeserted") : "") + (b.Offline ? " · " + NetrunUI.T("NR_MapStateOffline") : "");
                Widgets.Label(new Rect(row.x + 30f, row.y + 14f, right - row.x - 30f, 16f), sub.Truncate(right - row.x - 30f));
                // Defense as pips (red with Black ICE), marks after them.
                Color pip = b.BlackIce ? NetrunUI.Bad : NetrunUI.Warn;
                int def = Mathf.Clamp(b.Defense, 0, 6);
                for (int i = 0; i < 6; i++)
                    Widgets.DrawBoxSolid(new Rect(right + 4f + i * 7f, row.center.y - 3f, 5f, 6f), i < def ? pip : new Color(pip.r, pip.g, pip.b, 0.15f));
                if (b.Contract || b.GuardFor >= 0 && NetrunMod.Settings.ProtectedHubs)
                {
                    GUI.color = NetrunUI.Warn;
                    GUI.DrawTexture(new Rect(row.xMax - 18f, row.center.y - 5f, 10f, 10f), ShapeTex(Shape.Diamond, true));
                }
            }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (!b.Hidden && Mouse.IsOver(row))
                NetrunUI.Tip(row, () => b.Name + " · " + b.TypeLabel + "\n" + (string)(b.BlackIce ? "NR_GatewayBlackIce" : "NR_GatewayDefense").Translate(b.Defense)
                    + (b.Contract ? "\n" + (string)"NR_GatewayContract".Translate() : string.Empty));
            if (Widgets.ButtonInvisible(row))
            {
                NetrunSound.Click();
                _selectedId = b.Id;
            }
            y += 32f;
        }
        return y + 4f;
    }
}
