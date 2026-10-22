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
        // Outskirts, Corp and the Old Net moved off the green and red of Ok / Bad and the runner (Maxim, 2026-10-23).
        District.Outskirts => new Color(0.45f, 0.62f, 0.88f),
        District.Transit => new Color(0.24f, 0.82f, 0.9f),
        District.Corp => new Color(0.86f, 0.58f, 0.86f),
        District.Market => new Color(0.9f, 0.82f, 0.47f),
        District.Ruins => new Color(0.67f, 0.59f, 0.51f),
        District.Campus => new Color(0.43f, 0.78f, 0.82f),
        District.Entertainment => new Color(0.94f, 0.47f, 0.6f),
        District.Dump => new Color(0.59f, 0.67f, 0.35f),
        District.Dark => NetrunUI.Purple,
        District.OldNet => new Color(0.9f, 0.58f, 0.32f),
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
                          || new Rect(ZoomRect(rect).x - 4f, ZoomRect(rect).y - 4f, 106f, ZoomBlockH + 8f).Contains(mouse)
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
            // Map v5: each district reads as a soft patch of its colour under its hubs (Claude Design concept, 2026-10-22).
            float patch = 55f * _zoom;
            Vector2 cursor = Event.current.mousePosition;
            MapNode near = null;
            float best = patch * patch;
            foreach (MapNode n in map.Nodes)
                if (n.District is not (District.Home or District.Dark) && !n.Offline && net.Charted(n))
                {
                    Vector2 at = ToScreen(local, n.Pos);
                    NetrunUI.Glow(new Rect(at, Vector2.zero), HubColor(n, net), 0.09f, patch);
                    float d = (at - cursor).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        near = n;
                    }
                }
            TrackDistrictHover(_mouseOnOverlay || !local.Contains(cursor) ? null : near);

            // No district names on the map (Maxim, 2026-10-23): the colour patches and the hover card name the district.
            PlacedPlates.Clear();
            // The map's own buttons take their places first, so no plate hides under them (UI audit 2026-10-23).
            Vector2 o = rect.position;
            PlacedPlates.Add(new Rect(0f, 0f, local.width, 38f));
            Rect zoom = ZoomRect(rect);
            PlacedPlates.Add(new Rect(zoom.x - o.x - 4f, zoom.y - o.y - 4f, zoom.width + 8f, ZoomBlockH + 8f));
            Rect legend = LegendRect(rect, net);
            PlacedPlates.Add(new Rect(legend.x - o.x - 4f, legend.y - o.y - 4f, legend.width + 8f, legend.height + 8f));
            if (NetrunMod.Settings.MapMinimap)
                PlacedPlates.Add(new Rect(local.width - 190f, local.height - 126f, 188f, 124f));

            // Links.
            HashSet<(int, int)> route = RouteSet(selRunner);
            bool focus = _selectedMapNode >= 0 || route.Count > 0;
            foreach (MapLink l in map.Links)
            {
                if (!l.Known)
                    continue;
                MapNode na = map.Node(l.A), nb = map.Node(l.B);
                bool onRoute = route.Contains((Mathf.Min(l.A, l.B), Mathf.Max(l.A, l.B)));
                DrawMapLink(ToScreen(local, na.Pos), ToScreen(local, nb.Pos), LinkTrim(na), LinkTrim(nb), l, map, onRoute, focus,
                    onRoute ? RouteDirection(selRunner, l) : 1);
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
        {
            FlushHubNames();
            DrawDistrictName(local, net);
        }

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
        DrawMinimap(rect, net);
        DrawLensScale(rect, net);
        DrawLegend(rect, net);
        DrawLensBar(rect);
        // Over the lens bar's row, never under it.
        DrawRunnerArrows(rect, net);
        // Never past the lens bar, which starts 274 px in.
        DrawSearch(new Rect(rect.x + 6f, rect.y + 6f, Mathf.Min(rect.width * 0.45f, 262f), 26f), net);
        GuideRegistry.Area("gateway.searchbox", new Rect(rect.x + 6f, rect.y + 6f, 262f, 26f));
        GuideRegistry.Area("gateway.lenses", LensBarRect(rect));
        if (NetrunMod.Settings.MapMinimap)
            GuideRegistry.Area("gateway.minimap", new Rect(rect.xMax - 186f, rect.yMax - 122f, 180f, 116f));
    }

    /// <summary>The zoom buttons' row; the H / R buttons hang below it (<see cref="ZoomBlockH"/> in all).</summary>
    private static Rect ZoomRect(Rect map) => new(map.xMax - 3 * 34f - 6f, map.yMax - ZoomBlockH - 6f - (NetrunMod.Settings.MapMinimap ? 124f : 0f), 3 * 34f, 30f);

    private const float ZoomBlockH = 60f;

    // The district under the mouse: its name fades in, framed, at the district's centre (Maxim, 2026-10-23). Corp
    // districts are one per corporation slot.
    private static int _districtKey = -1;
    private static float _routeFlashAt = -10f;
    private static float _districtAlpha, _districtTime;

    private static int DistrictKey(MapNode n) => (int)n.District * 100 + (n.District == District.Corp ? n.CorpSlot + 1 : 0);

    private static void TrackDistrictHover(MapNode near)
    {
        float now = Time.realtimeSinceStartup, dt = Mathf.Min(now - _districtTime, 0.1f);
        _districtTime = now;
        int key = near == null ? -1 : DistrictKey(near);
        // Fade in on the same district; fade out first, then switch, on another one.
        if (key == _districtKey && key >= 0)
            _districtAlpha = Mathf.Min(1f, _districtAlpha + dt / 0.35f);
        else
        {
            _districtAlpha = Mathf.Max(0f, _districtAlpha - dt / 0.2f);
            if (_districtAlpha <= 0f)
                _districtKey = key;
        }
    }

    private static void DrawDistrictName(Rect local, NetMapManager net)
    {
        // Far out only: up close the player reads hubs, and the name covered their plates (Maxim, 2026-10-23).
        if (_districtKey < 0 || _districtAlpha <= 0f || _zoom >= 1.1f)
            return;
        Vector2 sum = Vector2.zero;
        int count = 0;
        MapNode any = null;
        foreach (MapNode n in net.Map.Nodes)
            if (DistrictKey(n) == _districtKey && net.Charted(n) && !n.Offline)
            {
                sum += ToScreen(local, n.Pos);
                count++;
                any = n;
            }
        if (count == 0)
            return;
        Vector2 c = sum / count;
        string text = NetrunUI.Upper(DistrictLabel(any, net));
        Text.Font = GameFont.Small;
        // A long name on two lines (Maxim, 2026-10-23).
        int space = text.LastIndexOf(' ');
        if (space > 0 && Text.CalcSize(text).x > 170f)
            text = text.Substring(0, space) + "\n" + text.Substring(space + 1);
        Vector2 size = Text.CalcSize(text);
        float w = size.x + 20f, h = size.y + 4f;
        float a = Mathf.SmoothStep(0f, 1f, _districtAlpha);
        DrawPlate(new Rect(c.x - w / 2f, c.y - h / 2f, w, h), text, HubColor(any, net), false, 0.8f, true, a, GameFont.Small);
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

    private const string PodPawnsKey = "map.podPawns", WatchedKey = "map.watched";

    /// <summary>D3: hubs that an accepted surveillance contract watches (twice a second).</summary>
    public static HashSet<int> WatchedHubs() => NetrunUI.Cached(WatchedKey,
        () => new HashSet<int>(ContractManager.Get()?.Active.Where(c => c.Accepted && c.WatchesHub).Select(c => c.TargetHub) ?? []), 0.5f);

    /// <summary>The "watch" tag left of the hub.</summary>
    private static void DrawWatchTag(Rect hubRect)
    {
        Text.Font = GameFont.Tiny;
        string text = NetrunUI.T("NR_MapWatchTag");
        float w = NetrunUI.Width(text) + 8f;
        // Top left, clear of the patrol triangle at the hub's corner; left out where a button or a plate already is.
        var tag = new Rect(hubRect.x - w - 14f, hubRect.y - 16f, w, 14f);
        if (!PlateFree(tag))
        {
            Text.Font = GameFont.Small;
            return;
        }
        PlacedPlates.Add(tag);
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

    /// <summary>+1 when the runner walks this route link from A to B, -1 from B to A (the route dashes crawl that way).</summary>
    private static int RouteDirection(NetRunner r, MapLink l)
    {
        int from = r.NodeId;
        foreach (int to in r.Path)
        {
            if (from == l.A && to == l.B)
                return 1;
            if (from == l.B && to == l.A)
                return -1;
            from = to;
        }
        return 1;
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

    private static readonly Vector2[] LinkPoints = new Vector2[2];

    /// <summary>The link's drawn line, cut back at both hubs' rings (links are straight: the generator keeps them clear).</summary>
    private static void LinkLine(Vector2 a, Vector2 b, float trimA, float trimB)
    {
        LinkPoints[0] = Trim(a, b, trimA);
        LinkPoints[1] = Trim(b, a, trimB);
    }

    private static Vector2 Trim(Vector2 from, Vector2 toward, float by)
    {
        Vector2 d = toward - from;
        float len = d.magnitude;
        return len <= by + 1f ? from + d * 0.5f : from + d / len * by;
    }

    /// <summary>The line, solid (dash 0) or dashed; offset slides the dashes along (the route crawls toward its end).</summary>
    private static void DrawLinkLine(Color color, float width, float dash = 0f, float gap = 0f, float offset = 0f)
    {
        Vector2 p = LinkPoints[0], q = LinkPoints[1];
        if (dash <= 0f)
        {
            Widgets.DrawLine(p, q, color, width);
            return;
        }
        float len = Vector2.Distance(p, q), period = dash + gap;
        for (float s = offset % period - period; s < len; s += period)
        {
            float s0 = Mathf.Max(0f, s), s1 = Mathf.Min(len, s + dash);
            if (s1 > s0)
                Widgets.DrawLine(Vector2.Lerp(p, q, s0 / len), Vector2.Lerp(p, q, s1 / len), color, width);
        }
    }

    // Map v5 link classes (Claude Design concept, approved by Maxim 2026-10-22): street, highway, darknet, shut; the route
    // on top. Streets are neutral (no district tint) and lighter than in the mockup (Maxim: too dark).
    private static readonly Color StreetColor = new(0.72f, 0.78f, 0.73f), HighwayColor = new(0.80f, 0.87f, 0.84f);

    private static void DrawMapLink(Vector2 a, Vector2 b, float trimA, float trimB, MapLink l, CyberMap map, bool onRoute, bool focus, int direction)
    {
        bool nearSelected = _selectedMapNode >= 0 && (l.A == _selectedMapNode || l.B == _selectedMapNode);
        bool dim = LegendDims(l.A) && LegendDims(l.B);
        MapNode na = map.Node(l.A), nb = map.Node(l.B);
        // A link to a hub that is offline (the orbit with no gravship in space, quarantine) reads like the shut Wall: red,
        // dashed, a lock (Maxim, 2026-10-23: links of an offline hub looked usable).
        bool orbitShut = na?.Offline == true || nb?.Offline == true;
        bool shut = orbitShut || l.Kind == LinkKind.Wall && !map.WallOpen;
        Color c = l.Kind switch
        {
            LinkKind.Fast => HighwayColor,
            LinkKind.Dark => NetrunUI.Purple,
            LinkKind.Wall => map.WallOpen ? NetrunUI.Warn : NetrunUI.Bad,
            LinkKind.Backdoor => NetrunUI.Warn,
            _ => StreetColor,
        };
        if (orbitShut)
            c = NetrunUI.Bad;
        if (l.Jammed)
            c = new Color(0.95f, 0.6f, 0.2f);
        float alpha = l.Broken ? 0.25f : l.Kind == LinkKind.Normal ? 0.55f : 0.75f;
        float width = l.Kind == LinkKind.Fast ? 2.5f : 1.5f;
        if (nearSelected)
        {
            alpha = Mathf.Max(alpha, 0.9f);
            width += 0.5f;
        }
        // A picked hub or a shown route: the rest steps back.
        else if (focus && !onRoute)
            alpha *= 0.55f;
        if (dim)
            alpha *= 0.25f;
        LinkLine(a, b, trimA, trimB);
        // A dark strip under every link: where two lines come close, the later one reads as passing over, not as a joint.
        DrawLinkLine(new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.9f), width + 3f);
        if (onRoute)
        {
            float flash = Mathf.Clamp01(1f - (Time.realtimeSinceStartup - _routeFlashAt) / 0.8f);
            DrawLinkLine(new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.25f + 0.5f * flash), 7f + 5f * flash);
            // Dashes that crawl toward the route's end (12 px a second), still when animations are off.
            float crawl = NetrunMod.Settings.Animations ? Time.realtimeSinceStartup * 12f * direction : 0f;
            DrawLinkLine(NetrunUI.Accent, 2.5f, 8f, 5f, crawl);
        }
        else if (shut || l.Kind == LinkKind.Wall)
            DrawLinkLine(new Color(c.r, c.g, c.b, alpha), width, 8f, 5f);
        else if (l.Kind == LinkKind.Dark)
            DrawLinkLine(new Color(c.r, c.g, c.b, alpha), width, 3f, 5f);
        else if (l.Kind == LinkKind.Backdoor)
            DrawLinkLine(new Color(c.r, c.g, c.b, alpha), width, 10f, 10f);
        else
            DrawLinkLine(new Color(c.r, c.g, c.b, alpha), width);
        // One mark in the middle at most: lock > broken > toll.
        Vector2 mid = (LinkPoints[0] + LinkPoints[1]) / 2f;
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
            GUI.color = NetrunUI.BgWindow;
            GUI.DrawTexture(new Rect(mid.x - 6f, mid.y - 6f, 12f, 12f), NetrunUI.Disc);
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
        // Far out there is no plate: the exit hangs beside the hub; closer it is an icon in the plate (2026-10-23).
        if (n.Exit && !home && _zoom < 0.8f)
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
            // Map v5: the LOCKED / OPEN tag is gone; a lock leads the name plate instead (Maxim, 2026-10-22).
            bool shut = n.GuardLocked;
            GUI.color = shut ? NetrunUI.Warn : new Color(NetrunUI.Warn.r, NetrunUI.Warn.g, NetrunUI.Warn.b, 0.5f);
            GUI.DrawTexture(rect.ExpandedBy(7f), DashedRing);
            // The line to the key only for the hub or its key hub picked or hovered: always drawn, they read as crossing links.
            if (shut && GuardKeyHub(n.Id) is { } keyHub && net.Map.Node(keyHub) is { } key && (selected || hover || keyHub == _selectedMapNode))
                DrawKeyLine(ToScreen(local, key.Pos), p, r);
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
        if (n.Patrol && _zoom < 0.8f)
        {
            // An ICE patrol far out, where there is no plate: a small red triangle with a mark, top left (closer it is an
            // icon in the plate).
            var warn = new Rect(rect.x - 11f, rect.y - 13f, 12f, 12f);
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

        // Map v5 (variant B, Maxim 2026-10-22): no markers around the hub; its nodes are chips in the name plate and one
        // badge sits top right: a contract target first, else a key.
        List<ExternalNode> branches = BranchesOf(n.Id);
        // Surveillance of a hub is today's only contract with a place on the map (taps and quiet runs count any hack).
        bool contract = WatchedHubs().Contains(n.Id);
        for (int i = 0; i < branches.Count; i++)
            contract |= !branches[i].Hidden && branches[i].Contract;
        // The contract target (Maxim, 2026-10-23: the badge got covered): an amber ring that pulses on every lens, the
        // map's only pulse besides the runner; the target node leads the plate's chips.
        if (contract)
        {
            float pulse = NetrunMod.Settings.Animations ? (Time.realtimeSinceStartup * 0.7f) % 1f : 0.35f;
            GUI.color = new Color(NetrunUI.Warn.r, NetrunUI.Warn.g, NetrunUI.Warn.b, 0.85f * (1f - pulse));
            GUI.DrawTexture(rect.ExpandedBy(5f + 16f * pulse * Mathf.Sqrt(_zoom)), NetrunUI.ThinRing);
            GUI.color = new Color(NetrunUI.Warn.r, NetrunUI.Warn.g, NetrunUI.Warn.b, 0.7f);
            GUI.DrawTexture(rect.ExpandedBy(4f), NetrunUI.ThinRing);
            GUI.color = Color.white;
        }
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        if (WatchedHubs().Contains(n.Id))
            DrawWatchTag(rect);
        DrawTimerTag(rect, n, net);
        // No loot bars under an uncharted hub: it would tell the loot before the scouting (Maxim, 2026-10-23).
        if (!QueueHubName(rect, n, net, branches) && net.Charted(n))
            DrawLootDots(rect, n, default);

        if (hover && !_mouseOnOverlay)
            // Built on Repaint only and at most twice a second per hub (it was rebuilt on every GUI event while hovered).
            NetrunUI.Tip(rect.ExpandedBy(4f), () => NetrunUI.Cached("map.hubCard." + n.Id, () => HubCard(n, net, branches)));
    }

    /// <summary>The pulse colour of an important node, or null for an ordinary one.</summary>
    private static Color? ImportantColor(ExternalNode b)
    {
        // A rival netrunner on the node: first among the hub's chips (Maxim, 2026-10-23: the mark was hard to find).
        if (b.Rival)
            return NetrunUI.Bad;
        if (b.Contract || b.GuardFor >= 0 && NetrunMod.Settings.ProtectedHubs)
            return NetrunUI.Warn;
        if (b.Lab)
            return NetrunUI.Purple;
        if (b.IsBoss || b.Ancient)
            return NetrunUI.Bad;
        return null;
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
            NetrunUI.RotateAround(Time.realtimeSinceStartup * 12f % 360f, outer.center);
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
        if (contract)
        {
            // The square contract badge keeps its dark rim; the key diamond goes without one (a black square behind it, Maxim 2026-10-22).
            Widgets.DrawBoxSolid(r.ExpandedBy(1f), NetrunUI.BgWindow);
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
            GUI.DrawTexture(r.ExpandedBy(1f), NetrunTex.CyberIcon("Icon_Key") ?? ShapeTex(Shape.Diamond, true));
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

    /// <summary>A hub's states with their colours: red danger, purple storm, green transit, grey offline.</summary>
    private static IEnumerable<(string text, Color color)> NodeStates(MapNode n)
    {
        if (n.Offline)
            yield return ("NR_MapStateOffline".Translate(), NetrunUI.Muted);
        if (n.Storm)
            yield return ("NR_MapStateStorm".Translate(), NetrunUI.Purple);
        if (n.Patrol)
            yield return ("NR_MapStatePatrol".Translate(), NetrunUI.Bad);
        if (n.Transit)
            yield return ("NR_MapStateTransit".Translate(), NetrunUI.Ok);
        if (n.RivalTrap)
            yield return ("NR_MapStateRivalTrap".Translate(), NetrunUI.Bad);
        if (n.Quarantine)
            yield return ("NR_MapStateQuarantine".Translate(), NetrunUI.Warn);
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

    private static float DrawFirstSteps(Rect inner, float y, Map colonyMap)
    {
        bool pod = GatewayManager.Runner(colonyMap, out _) != null;
        Text.Font = GameFont.Tiny;
        GUI.color = NetrunUI.Muted;
        Widgets.Label(new Rect(inner.x, y, inner.width, 16f), NetrunUI.Upper(NetrunUI.T("NR_MapFirstStepsHead")));
        y += 18f;
        for (int i = 1; i <= 3; i++)
        {
            bool done = i == 1 && pod;
            string line = (done ? "✓ " : i + ". ") + ("NR_MapFirstStep_" + i).Translate();
            float h = Text.CalcHeight(line, inner.width - 8f);
            GUI.color = done ? NetrunUI.Ok : NetrunUI.Subtle;
            Widgets.Label(new Rect(inner.x + 4f, y, inner.width - 8f, h), line);
            y += h + 2f;
        }
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        return y + 6f;
    }

    /// <summary>Runners and the selected map node, above the branch panel.</summary>
    private static float DrawMapPanel(Rect inner, float y, Map colonyMap, NetMapManager net)
    {
        // Runners: connected ones, then pawns in pods who could connect.
        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_MapRunners".Translate(net.Runners.Count, NetMapManager.Settings.maxRunners));
        // A new player: three first steps, ticked as they are done, until the first runner is in (Maxim, 2026-10-23, В9).
        if (net.Runners.Count == 0 && GatewayManager.Get() is { TrainingDone: false })
            y = DrawFirstSteps(inner, y, colonyMap);
        for (int i = 0; i < net.Runners.Count; i++)
        {
            NetRunner r = net.Runners[i];
            bool sel = i == Mathf.Clamp(_selectedRunner, 0, net.Runners.Count - 1);
            y = RunnerCard(inner, y, r, net, sel, i);
        }

        NetRunner selected = SelectedRunner(net);
        if (selected != null)
        {
            // Stop / Exit takes the row; the emergency exit is a small red icon button at its end (2026-10-23: two equal
            // buttons, one of them red, read as a pair of equal choices).
            float bw = inner.width - 34f;
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
            if (NetrunUI.Button(new Rect(inner.xMax - 28f, y, 28f, 28f), "", NetrunUI.Bad, true,
                    NetrunUI.T("NR_MapEmergencyButton").Colorize(NetrunUI.Bad) + "\n" + "NR_MapEmergencyTip".Translate(NetMapManager.Settings.emergencyNetsick.ToString("0.##"), NetMapManager.Settings.emergencyLockoutChance.ToStringPercent()), NetrunTex.CyberIcon("Action_Exit")))
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("NR_MapEmergencyConfirm".Translate(selected.Pawn?.LabelShort ?? "?"),
                    () => net.Disconnect(selected, emergency: true), destructive: true));
            y += 34f;
            // Go and exit and Home share one row, half each.
            bool showExit = !net.AtExit(selected), showHome = selected.NodeId != net.Map.HomeId || selected.Moving;
            float half = showExit && showHome ? (inner.width - 6f) / 2f : inner.width;
            if (showExit)
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
                if (NetrunUI.Button(new Rect(inner.x, y, half, 28f), "NR_MapGoAndExit".Translate(), NetrunUI.Ok, goExit, "NR_MapGoAndExitTip".Translate()))
                    net.GoAndExit(selected);
            }
            if (showHome && NetrunUI.Button(new Rect(inner.xMax - half, y, half, 28f), "NR_MapGoHome".Translate(), NetrunUI.Ok,
                    net.CanTravel(selected, net.Map.HomeId), "NR_MapGoHomeTip".Translate()))
                net.Travel(selected, net.Map.HomeId);
            if (showExit || showHome)
                y += 32f;
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
        // The hub's facts as rows (Maxim, 2026-10-15: grey lines were easy to miss): owner, exit, fortress, the states
        // with their timer on the right, then the route for the picked runner.
        if (node.District == District.Corp && net.CorpOfSlot(node.CorpSlot) is { } corp)
            y = DrawInfoRow(inner, y, new NowRow { Text = "NR_MapOwner".Translate(corp.Name), Color = NetrunUI.Accent, Hub = -1 });
        if (node.Exit)
            y = DrawInfoRow(inner, y, new NowRow { Text = "NR_MapExitPoint".Translate(), Color = NetrunUI.Ok, Hub = -1 });
        if (node.Fortress)
            y = DrawInfoRow(inner, y, new NowRow { Text = "NR_MapFortress".Translate(), Color = NetrunUI.Warn, Hub = -1 });
        // Every running event of the hub with its own time left, as in "Now in the net" (Maxim, 2026-10-23: the hub's
        // outage showed there but not in its own description); the bare states only when no timer covers them.
        bool timed = false;
        foreach (TimerEntry e in Timeline(net))
            if (e.Hub == node.Id && Left(e) > 0)
            {
                y = DrawInfoRow(inner, y, new NowRow { Text = e.State ?? e.Text, Right = Short(Left(e)), Color = e.Bad ? NetrunUI.Bad : NetrunUI.Warn, Hub = -1 });
                timed = true;
            }
        if (!timed)
            foreach ((string text, Color color) in NodeStates(node))
                y = DrawInfoRow(inner, y, new NowRow { Text = text, Color = color, Hub = -1 });
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
            bool reach = _panelRouteSteps > 0 || selected.NextNodeId == node.Id;
            y = DrawInfoRow(inner, y, new NowRow
            {
                Text = reach ? "NR_MapRouteInfo".Translate(_panelRouteSteps + (selected.Moving ? 1 : 0), (_panelRouteHours / NetMapManager.Speed(selected.Pawn)).ToString("0.#")) : "NR_MapNoRoute".Translate(),
                Color = reach ? NetrunUI.Accent : NetrunUI.Muted,
                Hub = -1,
            });
        }
        Text.Font = GameFont.Small;
        y += 4f;

        if (selected != null && (selected.NodeId != node.Id || selected.Moving))
        {
            if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 28f), "NR_MapGo".Translate(), NetrunUI.Accent, net.CanTravel(selected, node.Id), "NR_MapGoTip".Translate()))
            {
                net.Travel(selected, node.Id);
                // The new route lights up for a moment (UI audit 2026-10-23: a click gave only a sound).
                _routeFlashAt = Time.realtimeSinceStartup;
            }
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
        // A section header like the others (Maxim, 2026-10-23: the small grey NODES label got lost).
        if (nodes.Count > 0)
            y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), NetrunUI.T("NR_MapHubNodes"));
        foreach (ExternalNode b in nodes)
        {
            var row = new Rect(inner.x, y, inner.width, 30f);
            Color bc = b.Hidden ? NetrunUI.Muted : b.Offline ? NetrunUI.Muted : b.Color;
            bool picked = b.Id == _selectedId;
            Widgets.DrawBoxSolid(row, picked ? new Color(bc.r, bc.g, bc.b, 0.16f) : Mouse.IsOver(row) ? new Color(1f, 1f, 1f, 0.05f) : new Color(0f, 0f, 0f, 0.18f));
            if (picked)
                Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), bc);
            // The node's type icon, as in its card below; a hidden node keeps the map shape (its type is unknown).
            Texture2D icon = b.Hidden ? null : b.Icon;
            if (icon != null)
            {
                GUI.color = b.Offline ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
                GUI.DrawTexture(new Rect(row.x + 5f, row.y + 4f, 22f, 22f), icon, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }
            else
                DrawShape(new Rect(row.x + 9f, row.y + 8f, 14f, 14f), b, 1f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            float right = row.xMax - 74f;
            string name = b.Hidden ? (string)"NR_GatewayHiddenRow".Translate() : b.Name;
            GUI.color = b.Hidden ? NetrunUI.Muted : NetrunUI.TextColor;
            Widgets.Label(new Rect(row.x + 30f, row.y, right - row.x - 30f, 16f + 2f), name.Truncate(right - row.x - 30f));
            if (!b.Hidden)
            {
                GUI.color = NetrunUI.Subtle;
                string sub = b.TypeLabel + (b.Deserted ? " · " + NetrunUI.T("NR_MapDeserted") : "") + (b.Offline ? " · " + NetrunUI.T("NR_MapStateOffline") : "");
                Widgets.Label(new Rect(row.x + 30f, row.y + 14f, right - row.x - 30f, 16f), sub.Truncate(right - row.x - 30f));
                // Defense as pips (red with Black ICE), marks after them.
                // Muted red pips, a stronger red with Black ICE; further left, clear of the marks (Maxim, 2026-10-23).
                Color pip = b.BlackIce ? NetrunUI.Bad : new Color(0.78f, 0.47f, 0.43f);
                int def = Mathf.Clamp(b.Defense, 0, 6);
                for (int i = 0; i < 6; i++)
                    Widgets.DrawBoxSolid(new Rect(right - 22f + i * 7f, row.center.y - 3f, 5f, 6f), i < def ? pip : new Color(pip.r, pip.g, pip.b, 0.15f));
                // The node's marks, as on the hub plate (Maxim, 2026-10-23): rival, key, known honeypot, contract; two fit.
                float mx = row.xMax - 15f;
                int marks = 0;
                mx = RowMark(mx, row.center.y, b.Rival, "Event_Rival", NetrunUI.Bad, ref marks);
                mx = RowMark(mx, row.center.y, b.GuardFor >= 0 && NetrunMod.Settings.ProtectedHubs, "Icon_Key", NetrunUI.Warn, ref marks);
                mx = RowMark(mx, row.center.y, b.Honeypot && b.HoneypotKnown, "Event_Honeypot", NetrunUI.Warn, ref marks);
                if (b.Contract && marks < 2)
                    DrawNodeBadge(new Rect(mx + 1f, row.center.y - 5f, 10f, 10f), true);
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
        // A gap and a rule between the list and the picked node's card below it.
        if (nodes.Count > 0)
        {
            Widgets.DrawBoxSolid(new Rect(inner.x, y + 4f, inner.width, 1f), new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.25f));
            y += 12f;
        }
        return y + 4f;
    }

    private static float RowMark(float x, float cy, bool show, string icon, Color tint, ref int marks)
    {
        if (!show || marks >= 2 || NetrunTex.CyberIcon(icon) is not { } tex)
            return x;
        GUI.color = tint;
        GUI.DrawTexture(new Rect(x, cy - 6f, 12f, 12f), tex);
        GUI.color = Color.white;
        marks++;
        return x - 14f;
    }
}
