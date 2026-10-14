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
                          || new Rect(ZoomRect(rect).x, ZoomRect(rect).y, 102f, 76f).Contains(mouse)
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
            DrawGrid(local, _pan);

            // District names as plates (positions and texts rebuilt every 2 s, not per frame); they take their place first,
            // and hub names that would cover them are left out.
            PlacedPlates.Clear();
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            foreach ((Vector2 pos, string text, Color color, bool corp, float radius, Vector2 top) in NetrunUI.Cached(DistrictsKey, () => DistrictLabels(net), 2f))
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
                // A watermark under the links, fixed in map space (Maxim, 2026-10-14: no sliding to the screen edge) and
                // above the district's topmost hub, clear of the hubs and their branch markers; off screen it is not drawn.
                Vector2 t = ToScreen(local, top);
                float pw = NameWidth(text) + 16f;
                var plate = new Rect(t.x - pw / 2f, t.y - 30f * Mathf.Sqrt(_zoom) - 20f, pw, 17f);
                if (plate.Overlaps(local) && PlateFree(plate))
                {
                    PlacedPlates.Add(plate);
                    DrawPlate(plate, text, color, false, corp ? 0.5f : 0.3f, false);
                }
            }
            Text.Anchor = TextAnchor.UpperLeft;

            // Links.
            HashSet<(int, int)> route = RouteSet(selRunner);
            Dictionary<MapLink, Vector2> bends = LinkBends(map);
            foreach (MapLink l in map.Links)
            {
                if (!l.Known)
                    continue;
                MapNode na = map.Node(l.A), nb = map.Node(l.B);
                Vector2? bend = bends.TryGetValue(l, out Vector2 ctrl) ? ToScreen(local, ctrl) : null;
                DrawMapLink(ToScreen(local, na.Pos), ToScreen(local, nb.Pos), bend, LinkTrim(na), LinkTrim(nb), l, map,
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

        if (paint)
            FlushHubNames();

        // Intrusion packets walking toward Home.
        if (paint)
            foreach (IntrusionPacket packet in IntrusionManager.Get()?.Packets ?? [])
                DrawPacket(local, packet, map);

        // Runners.
        foreach (NetRunner r in net.Runners)
            DrawRunner(local, r, net, r == selRunner);
        Widgets.EndGroup();

        // Zoom, % and H / R in the bottom right corner (Maxim, 2026-10-14: at the top they covered the lens buttons),
        // above the minimap when it is shown.
        DrawZoomButtons(ZoomRect(rect));
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

    /// <summary>The zoom buttons' row; the % label and the H / R buttons hang below it (76 px in all).</summary>
    private static Rect ZoomRect(Rect map) => new(map.xMax - 3 * 34f - 6f, map.yMax - 82f - (NetrunMod.Settings.MapMinimap ? 124f : 0f), 3 * 34f, 30f);

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

    /// <summary>
    /// The exit mark: a green square with an arrow pointing out. An exit hub is drawn as this square alone (Maxim,
    /// 2026-10-14: as plain as possible), the legend uses it small. The arrow scales with the square.
    /// </summary>
    private static void DrawExitMark(Rect r, float alpha)
    {
        // Maxim's exit icon (2026-10-14) when it is there; the drawn square stays as the fallback.
        if (NetrunTex.CyberIcon("Icon_ExitHub") is { } exitTex)
        {
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(r, exitTex);
            GUI.color = Color.white;
            return;
        }
        Color g = new(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, alpha);
        Widgets.DrawBoxSolid(r, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.9f * alpha));
        GUI.color = g;
        Widgets.DrawBox(r, r.width >= 16f ? 2 : 1);
        GUI.color = Color.white;
        float cy = r.center.y, pad = r.width * 0.22f, head = r.width * 0.22f, w = r.width >= 16f ? 2f : 1.5f;
        Widgets.DrawLine(new Vector2(r.x + pad, cy), new Vector2(r.xMax - pad, cy), g, w);
        Widgets.DrawLine(new Vector2(r.xMax - pad - head, cy - head), new Vector2(r.xMax - pad, cy), g, w);
        Widgets.DrawLine(new Vector2(r.xMax - pad - head, cy + head), new Vector2(r.xMax - pad, cy), g, w);
    }

    /// <summary>
    /// An exit hub stays a round hub; its way out hangs beside it like a small node of its own (Maxim, 2026-10-14):
    /// a short green link outward, away from the middle of the map, and the exit square at its end.
    /// </summary>
    private static void DrawExitBeside(Rect local, Vector2 hub, float radius, float alpha)
    {
        Vector2 dir = hub - ToScreen(local, FieldCenter);
        dir = dir.sqrMagnitude < 1f ? Vector2.right : dir.normalized;
        float s = 12f * Mathf.Sqrt(_zoom);
        Vector2 at = hub + dir * (radius + 6f + s);
        Color g = new(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.6f * alpha);
        Widgets.DrawLine(hub + dir * (radius + 1f), at - dir * (s * 0.5f), g, 1.5f);
        DrawExitMark(new Rect(at.x - s / 2f, at.y - s / 2f, s, s), alpha);
    }

    /// <summary>
    /// The loot meter: five short gold bars hanging under the name tag (lit by the best known branch), or on their own
    /// under the hub when the name is not shown at this zoom.
    /// </summary>
    private static void DrawLootDots(Rect hubRect, MapNode n, Rect nameTag)
    {
        if (Lens != MapLens.Overview || _zoom < 1.1f || !LootDots().TryGetValue(n.Id, out int count))
            return;
        float width = nameTag.width > 0f ? Mathf.Clamp(nameTag.width - 8f, 30f, 60f) : 30f;
        float y = nameTag.width > 0f ? nameTag.yMax + 2f : hubRect.yMax + 8f * Mathf.Sqrt(_zoom);
        float x = hubRect.center.x - width / 2f, slot = width / 5f;
        Color gold = NetrunUI.Hex(0xffd34a);
        float a = LegendDims(n.Id) ? 0.25f : 1f;
        for (int i = 0; i < 5; i++)
            Widgets.DrawBoxSolid(new Rect(x + i * slot + 1f, y, slot - 2f, 2f), new Color(gold.r, gold.g, gold.b, (i < count ? 0.85f : 0.15f) * a));
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

    private static List<(Vector2, string, Color, bool, float, Vector2)> DistrictLabels(NetMapManager net)
    {
        var list = new List<(Vector2, string, Color, bool, float, Vector2)>();
        foreach (IGrouping<string, MapNode> g in net.Map.Nodes.Where(n => n.District is not (District.Home or District.Dark))
                     .GroupBy(n => n.District + "|" + n.CorpSlot))
        {
            MapNode first = g.First();
            bool corp = first.District == District.Corp && net.CorpOfSlot(first.CorpSlot) != null;
            // No district plate over the orbit or a corporation's ground: the orbit stands apart on its own, and the
            // capital's plate already names the corporation (Maxim, 2026-10-14).
            if (corp || first.District == District.Orbit)
                continue;
            Color dc = HubColor(first, net);
            Vector2 centre = g.Aggregate(Vector2.zero, (s, n) => s + n.Pos) / g.Count();
            float radius = g.Max(n => Vector2.Distance(n.Pos, centre));
            // The label stands over the district's topmost hub, at the district's middle across.
            var top = new Vector2(centre.x, g.Min(n => n.Pos.y));
            string label = net.Charted(first) ? DistrictLabel(first, net) : "NR_DistrictUncharted".Translate().ToString();
            list.Add((centre, label.ToUpper().Colorize(new Color(dc.r, dc.g, dc.b, corp ? 0.85f : 0.6f)), dc, corp, radius, top));
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

    /// <summary>How far a link stops short of a hub's centre: the hub's drawn radius plus a small gap (links end at the ring).</summary>
    private static float LinkTrim(MapNode n)
    {
        float r = (n.District == District.Home ? 15f : n.Fortress ? 11f : 8f) * Mathf.Sqrt(_zoom);
        if (n.Fortress || n.District == District.Home)
            r += 6f;
        else if (n.District == District.Orbit)
            r += 2f;
        return r + 3f;
    }

    /// <summary>Links that would pass right over another hub, with the control point of a curve around it (map units).</summary>
    // Rebuilt every 5 s, not per frame: hubs hardly move (only orbit hubs, when they are placed).
    private static Dictionary<MapLink, Vector2> LinkBends(CyberMap map) => NetrunUI.Cached("map.linkBends", () =>
    {
        const float Clearance = 0.022f;
        var bends = new Dictionary<MapLink, Vector2>();
        foreach (MapLink l in map.Links)
        {
            Vector2 a = map.Node(l.A).Pos, b = map.Node(l.B).Pos;
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 0.0001f)
                continue;
            MapNode blocker = null;
            float blockerDist = Clearance, blockerSide = 0f;
            foreach (MapNode o in map.Nodes)
            {
                if (o.Id == l.A || o.Id == l.B)
                    continue;
                float t = Vector2.Dot(o.Pos - a, ab) / len2;
                if (t <= 0.08f || t >= 0.92f)
                    continue;
                Vector2 off = o.Pos - (a + ab * t);
                float d = off.magnitude;
                if (d < blockerDist)
                {
                    blocker = o;
                    blockerDist = d;
                    blockerSide = ab.x * off.y - ab.y * off.x;
                }
            }
            if (blocker == null)
                continue;
            // Bend to the side away from the blocking hub, far enough that the curve's middle clears it.
            Vector2 normal = new Vector2(-ab.y, ab.x).normalized;
            if (blockerSide > 0f)
                normal = -normal;
            bends[l] = (a + b) / 2f + normal * (2f * (Clearance + blockerDist) + 0.01f);
        }
        return bends;
    }, 5f);

    private const int CurveSteps = 12;
    private static readonly Vector2[] LinkPoints = new Vector2[CurveSteps + 1];

    /// <summary>The link's drawn polyline (a straight line or a curve around a hub), cut back at both hubs' rings.</summary>
    private static int LinkPolyline(Vector2 a, Vector2 b, Vector2? bend, float trimA, float trimB)
    {
        int n;
        if (bend is { } c)
        {
            n = CurveSteps + 1;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)CurveSteps, u = 1f - t;
                LinkPoints[i] = u * u * a + 2f * u * t * c + t * t * b;
            }
        }
        else
        {
            n = 2;
            LinkPoints[0] = a;
            LinkPoints[1] = b;
        }
        LinkPoints[0] = Trim(LinkPoints[0], LinkPoints[1], trimA);
        LinkPoints[n - 1] = Trim(LinkPoints[n - 1], LinkPoints[n - 2], trimB);
        return n;
    }

    private static Vector2 Trim(Vector2 from, Vector2 toward, float by)
    {
        Vector2 d = toward - from;
        float len = d.magnitude;
        return len <= by + 1f ? from + d * 0.5f : from + d / len * by;
    }

    private static void DrawPolyline(int count, Color color, float width, bool dashed)
    {
        float carried = 0f;
        for (int i = 0; i + 1 < count; i++)
        {
            Vector2 p = LinkPoints[i], q = LinkPoints[i + 1];
            if (!dashed)
            {
                Widgets.DrawLine(p, q, color, width);
                continue;
            }
            // 10 px dashes with 10 px gaps, carried across the curve's segments.
            float len = Vector2.Distance(p, q);
            for (float s = -carried; s < len; s += 20f)
            {
                float s0 = Mathf.Max(0f, s), s1 = Mathf.Min(len, s + 10f);
                if (s1 > s0)
                    Widgets.DrawLine(Vector2.Lerp(p, q, s0 / len), Vector2.Lerp(p, q, s1 / len), color, width);
            }
            carried = (carried + len) % 20f;
        }
    }

    private static void DrawMapLink(Vector2 a, Vector2 b, Vector2? bend, float trimA, float trimB, MapLink l, CyberMap map, bool onRoute)
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
        // A plain link inside one district takes the district's colour, fainter: the districts read as groups and the
        // grey links between districts stand out as the ways across (Maxim, 2026-10-14: the map still read tangled).
        MapNode na = map.Node(l.A), nb = map.Node(l.B);
        bool inside = l.Kind == LinkKind.Normal && na != null && nb != null && na.District == nb.District && na.CorpSlot == nb.CorpSlot;
        if (inside && NetMapManager.Get() is { } mgr)
            c = HubColor(na, mgr);
        // A link up to the orbit while no gravship is in space reads like the shut Wall: red, dashed, a lock.
        bool orbitShut = !NetMapManager.OrbitOpen && (na?.District == District.Orbit || nb?.District == District.Orbit);
        bool shut = orbitShut || l.Kind == LinkKind.Wall && !map.WallOpen;
        if (orbitShut)
            c = NetrunUI.Bad;
        if (l.Jammed)
            c = new Color(0.95f, 0.6f, 0.2f);
        // A plain link is a thin, quiet line (map v4, D9): the hubs are the picture. Fast links keep their colour, thinner.
        float alpha = l.Broken ? 0.25f : inside ? 0.38f : l.Kind == LinkKind.Normal ? 0.55f : 0.65f;
        float width = 1.5f;
        if (nearSelected)
        {
            alpha = Mathf.Max(alpha, 0.85f);
            width = 2f;
        }
        if (dim)
            alpha *= 0.25f;
        int count = LinkPolyline(a, b, bend, trimA, trimB);
        if (onRoute)
        {
            DrawPolyline(count, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.35f), 7f, false);
            c = NetrunUI.Accent;
            alpha = 1f;
        }
        bool dashed = orbitShut || l.Kind is LinkKind.Dark or LinkKind.Backdoor or LinkKind.Wall;
        if (!dashed && (onRoute || nearSelected))
            DrawPolyline(count, new Color(c.r, c.g, c.b, 0.12f), width + 4f, false);
        DrawPolyline(count, new Color(c.r, c.g, c.b, alpha), width, dashed);
        Vector2 mid = count == 2 ? (LinkPoints[0] + LinkPoints[1]) / 2f : LinkPoints[count / 2];
        Text.Font = GameFont.Tiny;
        if (shut)
        {
            // A lock on the dashed line: the way into the Old Net (or up to the orbit) is shut.
            var lockRect = new Rect(mid.x - 8f, mid.y - 8f, 16f, 16f);
            Widgets.DrawBoxSolid(lockRect.ContractedBy(1f), NetrunUI.BgWindow);
            GUI.color = NetrunUI.Bad;
            if (NetrunTex.CyberIcon("Icon_Lock") is { } lockTex)
                GUI.DrawTexture(lockRect, lockTex);
            GUI.color = Color.white;
            NetrunUI.Tip(lockRect, (orbitShut ? "NR_MapCardOrbitClosed" : "NR_MapWallLocked").Translate());
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
        // I1: an uncharted district is drawn faint, with a question mark and no name.
        bool charted = net.Charted(n);
        if (!charted)
            c = new Color(c.r, c.g, c.b, c.a * 0.4f);
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
        DrawEventPulse(rect, n, net);
        DrawVisited(rect, n);
        GUI.color = NetrunUI.BgWindow;
        GUI.DrawTexture(rect.ExpandedBy(2f), NetrunUI.Disc);
        // Map v4 (D9): a hollow ring in the hub's colour with a faint fill; full only for hubs a runner has been to,
        // and under a lens (its colour is the information) unless the lens has no data for it.
        float a = (n.Offline ? 0.5f : 1f) * c.a;
        bool full = !home && !noData && !n.Offline && (n.LastVisitTick >= 0 || Lens != MapLens.Overview);
        bool orbit = n.District == District.Orbit;
        if (n.Exit && !home)
            DrawExitBeside(local, p, r, a);
        if (home)
            DrawHomeHub(rect, c, a);
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
            // A capital: a diamond core and an outer ring, both in the corporation's colour (the old red dashed ring
            // read as a warning nobody could explain; Maxim, 2026-10-14). Its plate names the corporation.
            GUI.color = new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, a);
            GUI.DrawTexture(rect.ContractedBy(rect.width * 0.18f), NetrunUI.Disc);
            Color core = HubColor(n, net);
            GUI.color = new Color(core.r, core.g, core.b, a);
            GUI.DrawTexture(rect.ContractedBy(rect.width * 0.3f), ShapeTex(Shape.Diamond, true));
            GUI.color = new Color(core.r, core.g, core.b, LegendDims(n.Id) ? 0.2f : 0.6f);
            GUI.DrawTexture(rect.ExpandedBy(5f), NetrunUI.ThinRing);
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
            // D2: a protected hub. Shut: an amber dashed ring, a "LOCKED" tag above it and a dashed amber line to the
            // hub that holds its guard gateway (the key). Open: a faint ring and an "OPEN" tag. The hover card explains
            // it in words (Maxim, 2026-10-14: a bare lock told the player nothing).
            bool shut = n.GuardLocked;
            GUI.color = shut ? NetrunUI.Warn : new Color(NetrunUI.Warn.r, NetrunUI.Warn.g, NetrunUI.Warn.b, 0.5f);
            GUI.DrawTexture(rect.ExpandedBy(7f), DashedRing);
            if (shut && GuardKeyHub(n.Id) is { } keyHub && net.Map.Node(keyHub) is { } key)
                DrawKeyLine(ToScreen(local, key.Pos), p, r);
            DrawGuardTag(rect, shut);
        }
        if (n.Id == net?.MarkedNodeId)
        {
            GUI.color = NetrunUI.Accent;
            GUI.DrawTexture(rect.ExpandedBy(9f), NetrunUI.ThinRing);
        }
        GUI.color = Color.white;
        Text.Font = GameFont.Tiny;
        if (!charted)
        {
            GUI.color = new Color(NetrunUI.Muted.r, NetrunUI.Muted.g, NetrunUI.Muted.b, 0.9f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "?");
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
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
            // HOME in the same tag as the hub names, in the accent colour.
            string label = NetrunUI.Upper(NetrunUI.T("NR_District_Home"));
            float w = NetrunUI.Width(label) + 14f;
            var tag = new Rect(p.x - w / 2f, rect.yMax + 9f, w, 16f);
            Widgets.DrawBoxSolid(tag, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.85f));
            GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.7f);
            Widgets.DrawBox(tag);
            GUI.color = NetrunUI.Accent;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(tag, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        // Branches: small dots around the node, colored by type; unknown ones grey.
        List<ExternalNode> branches = BranchesOf(n.Id);
        bool markers = _zoom >= 0.8f;
        float[] angles = BranchAngles(n, net.Map, branches.Count);
        for (int i = 0; i < branches.Count; i++)
        {
            ExternalNode b = branches[i];
            float ang = angles[i];
            Vector2 bp = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (r + (markers ? 11f : 7f) * Mathf.Sqrt(_zoom));
            float br = (markers ? (b.IsBoss || b.Ancient ? 7.5f : 5.5f) : b.IsBoss || b.Ancient ? 5f : 3.5f) * Mathf.Sqrt(_zoom);
            var brect = new Rect(bp.x - br, bp.y - br, br * 2f, br * 2f);
            // The node's type as a shape (square market, ring archive, hexagon corp, triangle abandoned, diamond exchange).
            DrawShape(brect, b, c.a);
            if (!b.Hidden && b.Contract)
                DrawNodeBadge(new Rect(brect.xMax - 4f, brect.y - 6f, 10f, 10f), true);
            else if (!b.Hidden && b.GuardFor >= 0 && NetrunMod.Settings.ProtectedHubs)
            {
                DrawNodeBadge(new Rect(brect.xMax - 4f, brect.y - 6f, 10f, 10f), false);
                int locked = b.GuardFor;
                TooltipHandler.TipRegion(brect.ExpandedBy(4f), () => "NR_MapKeyTip".Translate(NetMapManager.NodeLabel(NetMapManager.Get()?.Map?.Node(locked))), 0x4E52 + b.Id);
            }
        }
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        if (WatchedHubs().Contains(n.Id))
            DrawWatchTag(rect);
        DrawTimerTag(rect, n, net);
        if (!QueueHubName(rect, n, net, branches))
            DrawLootDots(rect, n, default);

        if (hover && !_mouseOnOverlay)
            NetrunUI.Tip(rect.ExpandedBy(4f), HubCard(n, net, branches));
    }

    /// <summary>The hub holding the guard gateway of a protected hub (its key), by protected hub id; rebuilt every 2 s.</summary>
    private static int? GuardKeyHub(int protectedHub)
    {
        Dictionary<int, int> keys = NetrunUI.Cached("map.guardKeys", () =>
        {
            var d = new Dictionary<int, int>();
            foreach (ExternalNode b in GatewayManager.Get()?.AllBranches ?? [])
                if (b.GuardFor >= 0 && b.MapNodeId >= 0)
                    d[b.GuardFor] = b.MapNodeId;
            return d;
        }, 2f);
        return keys.TryGetValue(protectedHub, out int hub) ? hub : null;
    }

    /// <summary>A dashed amber line from the key hub to the shut hub, stopping short of both rings.</summary>
    private static void DrawKeyLine(Vector2 from, Vector2 to, float radius)
    {
        Vector2 d = to - from;
        float len = d.magnitude;
        if (len < radius * 3f)
            return;
        Vector2 u = d / len;
        Color c = new(NetrunUI.Warn.r, NetrunUI.Warn.g, NetrunUI.Warn.b, 0.45f);
        for (float t = radius + 4f; t < len - radius - 10f; t += 10f)
            Widgets.DrawLine(from + u * t, from + u * Mathf.Min(t + 5f, len - radius - 10f), c, 1.5f);
    }

    /// <summary>"LOCKED" (amber, with the lock) or "OPEN" over a protected hub.</summary>
    private static void DrawGuardTag(Rect hub, bool shut)
    {
        Text.Font = GameFont.Tiny;
        string label = NetrunUI.Upper(NetrunUI.T(shut ? "NR_MapTagLocked" : "NR_MapTagOpen"));
        Texture2D lockTex = shut ? NetrunTex.CyberIcon("Icon_Lock") : null;
        float w = NetrunUI.Width(label) + 10f + (lockTex != null ? 13f : 0f);
        var tag = new Rect(hub.center.x - w / 2f, hub.y - 24f, w, 15f);
        Color c = shut ? NetrunUI.Warn : NetrunUI.Ok;
        Widgets.DrawBoxSolid(tag, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.9f));
        GUI.color = new Color(c.r, c.g, c.b, 0.8f);
        Widgets.DrawBox(tag);
        float x = tag.x + 5f;
        if (lockTex != null)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, tag.y + 2f, 11f, 11f), lockTex);
            x += 13f;
        }
        GUI.color = c;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(x, tag.y, tag.xMax - x, tag.height), label);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
    }

    // Where a hub's branch markers sit, by hub and marker count; dropped when the map changes (CyberMap.Dirty) via the 2 s cache.
    private static Dictionary<int, float[]> BranchAngleCache => NetrunUI.Cached("map.branchAngles", () => new Dictionary<int, float[]>(), 2f);

    /// <summary>
    /// Angles for a hub's branch markers (Maxim, 2026-10-14: markers sat on the links and under the name plates).
    /// Each marker takes the free direction farthest from the hub's links, from the markers already placed and from
    /// straight down (the name plate); an exit's mark counts as a link. Computed once per hub and count, then cached.
    /// </summary>
    private static float[] BranchAngles(MapNode n, CyberMap map, int count)
    {
        int key = n.Id * 32 + Mathf.Min(count, 31);
        Dictionary<int, float[]> cache = BranchAngleCache;
        if (cache.TryGetValue(key, out float[] found))
            return found;
        var taken = new List<float> { Mathf.PI / 2f };
        if (n.Guarded)
            taken.Add(-Mathf.PI / 2f); // the LOCKED / OPEN tag sits above
        foreach (MapLink l in map.LinksOf(n.Id))
        {
            Vector2 d = map.Node(l.Other(n.Id)).Pos - n.Pos;
            taken.Add(Mathf.Atan2(d.y, d.x));
        }
        if (n.Exit)
        {
            Vector2 d = n.Pos - FieldCenter;
            taken.Add(Mathf.Atan2(d.y, d.x));
        }
        var result = new float[count];
        for (int i = 0; i < count; i++)
        {
            float best = -Mathf.PI / 2f, bestGap = -1f;
            for (int s = 0; s < 24; s++)
            {
                float a = -Mathf.PI + s * (Mathf.PI / 12f);
                float gap = float.MaxValue;
                foreach (float t in taken)
                    gap = Mathf.Min(gap, Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, t * Mathf.Rad2Deg)));
                // A slight pull upward keeps markers above the hub when the room is equal.
                gap += Mathf.Sin(-a) * 4f;
                if (gap > bestGap)
                {
                    bestGap = gap;
                    best = a;
                }
            }
            result[i] = best;
            taken.Add(best);
        }
        cache[key] = result;
        return result;
    }

    /// <summary>
    /// The colony's own hub as a small fortified core (Maxim, 2026-10-14: the plain ring was dull): a soft glow, a slowly
    /// turning dashed outer ring, the main ring, a hexagon with a diamond heart and four crosshair ticks.
    /// </summary>
    private static void DrawHomeHub(Rect rect, Color c, float a)
    {
        NetrunUI.Glow(rect, c, 0.45f * a, 12f);
        Rect outer = rect.ExpandedBy(7f);
        if (NetrunMod.Settings.Animations)
        {
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(Time.realtimeSinceStartup * 12f % 360f, outer.center);
            GUI.color = new Color(c.r, c.g, c.b, 0.45f * a);
            GUI.DrawTexture(outer, DashedRing);
            GUI.matrix = old;
        }
        else
        {
            GUI.color = new Color(c.r, c.g, c.b, 0.45f * a);
            GUI.DrawTexture(outer, DashedRing);
        }
        GUI.color = new Color(c.r, c.g, c.b, a);
        GUI.DrawTexture(rect, NetrunUI.Ring);
        Rect hex = rect.ContractedBy(rect.width * 0.24f);
        GUI.color = new Color(c.r, c.g, c.b, 0.22f * a);
        GUI.DrawTexture(hex, ShapeTex(Shape.Hexagon, true));
        GUI.color = new Color(c.r, c.g, c.b, 0.9f * a);
        GUI.DrawTexture(hex, ShapeTex(Shape.Hexagon, false));
        GUI.DrawTexture(rect.ContractedBy(rect.width * 0.4f), ShapeTex(Shape.Diamond, true));
        GUI.color = Color.white;
        Color tick = new(c.r, c.g, c.b, 0.8f * a);
        Vector2 m = rect.center;
        float r0 = rect.width / 2f + 10f, r1 = r0 + 5f;
        Widgets.DrawLine(new Vector2(m.x, m.y - r0), new Vector2(m.x, m.y - r1), tick, 1.5f);
        Widgets.DrawLine(new Vector2(m.x, m.y + r0), new Vector2(m.x, m.y + r1), tick, 1.5f);
        Widgets.DrawLine(new Vector2(m.x - r0, m.y), new Vector2(m.x - r1, m.y), tick, 1.5f);
        Widgets.DrawLine(new Vector2(m.x + r0, m.y), new Vector2(m.x + r1, m.y), tick, 1.5f);
    }

    /// <summary>
    /// A node's badge (Maxim, 2026-10-14: the old amber diamond was unclear and looked like a capital's core): a contract
    /// target is a small amber job tag with "!", a guard gateway (the lock of a protected hub) a small lock. Both are in the legend.
    /// </summary>
    private static void DrawNodeBadge(Rect r, bool contract)
    {
        Widgets.DrawBoxSolid(r.ExpandedBy(1f), NetrunUI.BgWindow);
        if (contract)
        {
            Widgets.DrawBoxSolid(r, NetrunUI.Warn);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = NetrunUI.BgWindow;
            Widgets.Label(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), "!");
            Text.Anchor = TextAnchor.UpperLeft;
        }
        else
        {
            // The key of a protected hub: an amber diamond (the lock is kept for the shut hub itself, not its key).
            GUI.color = NetrunUI.Warn;
            GUI.DrawTexture(r, ShapeTex(Shape.Diamond, true));
        }
        GUI.color = Color.white;
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
            y = DrawNow(inner, y + 4f);
            y = DrawActiveContracts(inner, y);
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
                        node.Offline ? (AcceptanceReport)"NR_MapHubOffline".Translate() : net.CanConnect(pawn), "NR_MapJumpTip".Translate()))
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
                // The same badges as on the map: a contract's amber "!" tag, a guard gateway's amber diamond (the key).
                if (b.Contract)
                    DrawNodeBadge(new Rect(row.xMax - 18f, row.center.y - 5f, 10f, 10f), true);
                else if (b.GuardFor >= 0 && NetrunMod.Settings.ProtectedHubs)
                    DrawNodeBadge(new Rect(row.xMax - 18f, row.center.y - 5f, 10f, 10f), false);
            }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (!b.Hidden && Mouse.IsOver(row))
                NetrunUI.Tip(row, () => b.Name + " · " + b.TypeLabel + "\n" + (string)(b.BlackIce ? "NR_GatewayBlackIce" : "NR_GatewayDefense").Translate(b.Defense)
                    + (b.Contract ? "\n" + (string)"NR_GatewayContract".Translate() : string.Empty)
                    + (b.GuardFor >= 0 && NetrunMod.Settings.ProtectedHubs
                        ? "\n\n" + ((string)"NR_MapKeyTip".Translate(NetMapManager.NodeLabel(NetMapManager.Get()?.Map?.Node(b.GuardFor)))).Colorize(NetrunUI.Warn) : string.Empty));
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
