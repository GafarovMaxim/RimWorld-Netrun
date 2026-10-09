using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Map v4, first queue (docs/concept-cyberspace-map-v4.md, docs/TASKS_2026-10-08.md G1): camera focus with a pulse,
/// search ("Market-81" centres the camera), hotkeys H, R, Tab, 0, F, the watchlist with contract targets, a right-click
/// menu on hubs, the minimap and a branch index rebuilt twice a second instead of per hub per frame.
/// </summary>
public static partial class GatewayView
{
    private const float DefaultZoom = 1.15f;
    private const string SearchControl = "NetrunMapSearchField";

    private static int _centerOn = -1;
    private static bool _mouseOnOverlay;
    private static bool _swallowF;
    private static int _pulseNode = -1;
    private static float _pulseUntil;
    private static string _search = string.Empty;
    private static string _searchFor;
    private static readonly List<(string label, int hub, int branch)> SearchHits = [];

    // ---- Branch index: hub id -> its branches ----

    private static Dictionary<int, List<ExternalNode>> BranchIndex => NetrunUI.Cached("map.branchIndex", BuildBranchIndex, 0.5f);

    private static Dictionary<int, List<ExternalNode>> BuildBranchIndex()
    {
        var index = new Dictionary<int, List<ExternalNode>>();
        foreach (ExternalNode b in GatewayManager.Get()?.AllBranches ?? Enumerable.Empty<ExternalNode>())
        {
            if (b.MapNodeId < 0)
                continue;
            if (!index.TryGetValue(b.MapNodeId, out List<ExternalNode> list))
                index[b.MapNodeId] = list = [];
            list.Add(b);
        }
        return index;
    }

    private static readonly List<ExternalNode> NoBranches = [];

    private static List<ExternalNode> BranchesOf(int hubId) => BranchIndex.TryGetValue(hubId, out List<ExternalNode> list) ? list : NoBranches;

    // ---- Camera ----

    /// <summary>Selects a hub (and a branch), centres the camera on it and pulses a ring around it for three seconds.</summary>
    public static void FocusNode(int hubId, int branchId = -1)
    {
        _selectedMapNode = hubId;
        _selectedId = branchId;
        _centerOn = hubId;
        _pulseNode = hubId;
        _pulseUntil = Time.realtimeSinceStartup + 3f;
    }

    private static float FitScale(Rect local) => Mathf.Min(local.width / 1.04f, local.height / 0.70f);

    private static void ApplyCenter(Rect local, CyberMap map)
    {
        if (_centerOn < 0)
            return;
        if (map.Node(_centerOn) is { } n)
            _pan = -(n.Pos - FieldCenter) * FitScale(local) * _zoom;
        _centerOn = -1;
        ClampPan(local);
    }

    /// <summary>The pan may reach further the closer the camera is.</summary>
    private static void ClampPan(Rect r)
    {
        float k = 0.6f * Mathf.Max(1f, _zoom);
        _pan.x = Mathf.Clamp(_pan.x, -r.width * k, r.width * k);
        _pan.y = Mathf.Clamp(_pan.y, -r.height * k, r.height * k);
    }

    private static Vector2 ToWorld(Rect local, Vector2 screen) => (screen - local.center - _pan) / (FitScale(local) * _zoom) + FieldCenter;

    private static void DrawPulse(Rect nodeRect, int id)
    {
        if (id != _pulseNode)
            return;
        float left = _pulseUntil - Time.realtimeSinceStartup;
        if (left <= 0f)
            return;
        float k = 1f - Mathf.Repeat(left, 1f);
        GUI.color = new Color(NetrunUI.Warn.r, NetrunUI.Warn.g, NetrunUI.Warn.b, 1f - k);
        GUI.DrawTexture(nodeRect.ExpandedBy(6f + 22f * k), NetrunUI.ThinRing);
        GUI.color = Color.white;
    }

    // ---- Hotkeys: H home, R and Tab the next runner, 0 the whole map, F search ----

    private static void MapHotkeys(Rect local, NetMapManager net)
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown)
            return;
        // The F that opened the search comes again as a typed character: swallow it once.
        if (_swallowF && (e.character == 'f' || e.character == 'F' || e.character == 'а' || e.character == 'А'))
        {
            _swallowF = false;
            e.Use();
            return;
        }
        if (GUI.GetNameOfFocusedControl() == SearchControl)
        {
            if (e.keyCode == KeyCode.Escape)
            {
                _search = string.Empty;
                GUI.FocusControl(null);
                e.Use();
            }
            else if (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter && SearchHits.Count > 0)
            {
                PickHit(SearchHits[0]);
                e.Use();
            }
            return;
        }
        if (e.keyCode == KeyCode.Escape && _selectedMapNode >= 0)
        {
            _selectedMapNode = -1;
            _selectedId = -1;
            e.Use();
            return;
        }
        if (NetrunMod.Settings.MapWasd && e.keyCode is KeyCode.W or KeyCode.A or KeyCode.S or KeyCode.D)
        {
            e.Use();
            return;
        }
        switch (e.keyCode)
        {
            case KeyCode.H:
                FocusNode(net.Map.HomeId);
                break;
            case KeyCode.R:
            case KeyCode.Tab:
                if (net.Runners.Count == 0)
                    return;
                _selectedRunner = (Mathf.Clamp(_selectedRunner, 0, net.Runners.Count - 1) + 1) % net.Runners.Count;
                _centerOn = net.Runners[_selectedRunner].NodeId;
                break;
            case KeyCode.Alpha0:
            case KeyCode.Keypad0:
                _zoom = 1f;
                _pan = Vector2.zero;
                break;
            case KeyCode.F:
                GUI.FocusControl(SearchControl);
                _swallowF = true;
                break;
            default:
                return;
        }
        e.Use();
    }

    private static float _wasdAt;

    /// <summary>
    /// W A S D (the game's camera keys) move the map smoothly while held (speed per real second, eased in), not by key-repeat steps. The game
    /// camera stays put meanwhile: the terminal sets preventCameraMotion while the map is shown with this option.
    /// </summary>
    private static void WasdPan(Rect local)
    {
        if (Event.current.type != EventType.Repaint)
            return;
        float now = Time.realtimeSinceStartup;
        float dt = Mathf.Min(0.05f, now - _wasdAt);
        _wasdAt = now;
        if (!NetrunMod.Settings.MapWasd || GUI.GetNameOfFocusedControl() == SearchControl)
            return;
        var dir = Vector2.zero;
        if (KeyBindingDefOf.MapDolly_Up.IsDown)
            dir.y += 1f;
        if (KeyBindingDefOf.MapDolly_Down.IsDown)
            dir.y -= 1f;
        if (KeyBindingDefOf.MapDolly_Left.IsDown)
            dir.x += 1f;
        if (KeyBindingDefOf.MapDolly_Right.IsDown)
            dir.x -= 1f;
        if (dir == Vector2.zero)
            return;
        float speed = Event.current.shift ? 1100f : 600f;
        _pan += dir.normalized * speed * dt;
        ClampPan(local);
    }

    /// <summary>The map is on screen with W A S D panning: the game camera must not move along.</summary>
    public static bool HoldsCamera => NetrunMod.Settings.MapWasd;

    // ---- Search ----

    private static void DrawSearch(Rect at, NetMapManager net)
    {
        var field = new Rect(at.x, at.y, Mathf.Min(260f, at.width), 26f);
        Widgets.DrawBoxSolid(field, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.92f));
        GUI.SetNextControlName(SearchControl);
        _search = Widgets.TextField(field.ContractedBy(2f), _search ?? string.Empty);
        if (_search.NullOrEmpty() && GUI.GetNameOfFocusedControl() != SearchControl)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(field.x + 8f, field.y, field.width - 10f, field.height), ((string)"NR_MapSearchHint".Translate()).Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.5f);
        Widgets.DrawBox(field);
        GUI.color = Color.white;
        if (_search != _searchFor)
            FindHits(net);
        if (_search.NullOrEmpty())
            return;
        float y = field.yMax + 2f;
        Text.Font = GameFont.Tiny;
        foreach ((string label, int hub, int branch) hit in SearchHits)
        {
            var row = new Rect(field.x, y, field.width, 22f);
            Widgets.DrawBoxSolid(row, Mouse.IsOver(row) ? NetrunUI.BgCardAlt : NetrunUI.BgCard);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x + 8f, row.y, row.width - 10f, row.height), hit.label);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(row))
                PickHit(hit);
            y += 23f;
        }
        if (SearchHits.Count == 0)
            Widgets.Label(new Rect(field.x + 8f, y, field.width, 20f), ((string)"NR_MapSearchNone".Translate()).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
    }

    /// <summary>Hubs, branches and corporations whose name holds the text (rebuilt when the text changes, not per frame).</summary>
    private static void FindHits(NetMapManager net)
    {
        _searchFor = _search;
        SearchHits.Clear();
        string q = Norm(_search);
        if (q.NullOrEmpty())
            return;
        bool Match(string s) => s != null && Norm(s).Contains(q);
        foreach (ExternalNode b in GatewayManager.Get()?.AllBranches ?? Enumerable.Empty<ExternalNode>())
            if (!b.Hidden && b.MapNodeId >= 0 && Match(b.Name))
                SearchHits.Add((b.Name + "  ·  " + NetMapManager.NodeLabel(net.Map.Node(b.MapNodeId)), b.MapNodeId, b.Id));
        foreach (MapNode n in net.Map.Nodes)
        {
            string label = NetMapManager.NodeLabel(n);
            if (Match(label))
                SearchHits.Add((label, n.Id, -1));
            else if (n.Fortress && n.District == District.Corp && net.CorpOfSlot(n.CorpSlot) is { } corp && Match(corp.Name))
                SearchHits.Add((corp.Name + "  ·  " + "NR_MapFortress".Translate(), n.Id, -1));
        }
        if (SearchHits.Count > 6)
            SearchHits.RemoveRange(6, SearchHits.Count - 6);
    }

    /// <summary>Lower case, letters and digits only: the search ignores spaces, dashes and signs.</summary>
    private static string Norm(string s)
    {
        if (s.NullOrEmpty())
            return string.Empty;
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s.StripTags())
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    private static void PickHit((string label, int hub, int branch) hit)
    {
        FocusNode(hit.hub, hit.branch);
        _search = string.Empty;
        GUI.FocusControl(null);
        NetrunSound.Click();
    }

    // ---- Right click on a hub ----

    private static void HubMenu(MapNode n, NetMapManager net)
    {
        var options = new List<FloatMenuOption>();
        NetRunner runner = SelectedRunner(net);
        if (runner != null && runner.NodeId != n.Id)
        {
            AcceptanceReport can = net.CanTravel(runner, n.Id);
            options.Add(can.Accepted
                ? new FloatMenuOption("NR_MapMenuSend".Translate(runner.Pawn?.LabelShort ?? "?"), () => net.Travel(runner, n.Id))
                : new FloatMenuOption("NR_MapMenuSend".Translate(runner.Pawn?.LabelShort ?? "?") + ": " + can.Reason, null));
        }
        if (runner != null && runner.Moving && runner.Path[runner.Path.Count - 1] != n.Id)
            options.Add(new FloatMenuOption("NR_MapMenuWaypoint".Translate(), () =>
            {
                List<int> more = net.Map.Route(runner.Path[runner.Path.Count - 1], n.Id, out _);
                if (more.Count > 0)
                    runner.Path.AddRange(more);
            }));
        bool watched = net.Watch.Contains(n.Id);
        options.Add(new FloatMenuOption((watched ? "NR_MapMenuUnwatch" : "NR_MapMenuWatch").Translate(), () => net.ToggleWatch(n.Id)));
        options.Add(new FloatMenuOption("NR_MapMenuCenter".Translate(), () => FocusNode(n.Id)));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    // ---- Watchlist: pinned hubs and contract targets ----

    private static float DrawWatchlist(Rect inner, float y, NetMapManager net)
    {
        var rows = new List<(string label, int hub, int branch, bool contract)>();
        foreach (ExternalNode b in GatewayManager.Get()?.AllBranches ?? Enumerable.Empty<ExternalNode>())
            if (b.Contract && !b.Hidden && b.MapNodeId >= 0)
                rows.Add((b.Name, b.MapNodeId, b.Id, true));
        foreach (int id in net.Watch)
            if (net.Map.Node(id) is { } n)
                rows.Add((NetMapManager.NodeLabel(n), id, -1, false));
        if (rows.Count == 0)
            return y;
        float top = y;
        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_MapWatchlist".Translate());
        foreach ((string label, int hub, int branch, bool contract) in rows)
        {
            var row = new Rect(inner.x, y, inner.width, 26f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x + 4f, row.y, row.width - 80f, row.height),
                ((contract ? "★ " : "☆ ") + label).Colorize(contract ? NetrunUI.Warn : NetrunUI.TextColor));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (NetrunUI.Button(new Rect(row.xMax - 74f, row.y + 1f, 74f, 24f), "NR_MapShow".Translate(), NetrunUI.Warn, true))
                FocusNode(hub, branch);
            y += 28f;
        }
        GuideRegistry.InScroll("gateway.watchlist", _panelOuter, _panelScroll, new Rect(inner.x, top, inner.width, y - top));
        return y + 4f;
    }

    // ---- Minimap: the whole net, the view as a frame; a click jumps there ----

    private static void DrawMinimap(Rect mapRect, NetMapManager net)
    {
        if (!NetrunMod.Settings.MapMinimap)
            return;
        var box = new Rect(mapRect.xMax - 186f, mapRect.yMax - 122f, 180f, 116f);
        if (Event.current.type != EventType.Repaint)
        {
            MinimapInput(mapRect, box);
            return;
        }
        Widgets.DrawBoxSolid(box, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.9f));
        GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.45f);
        Widgets.DrawBox(box);
        GUI.color = Color.white;
        Rect inner = box.ContractedBy(6f);
        float ms = Mathf.Min(inner.width / 1.04f, inner.height / 0.70f);
        Vector2 Mini(Vector2 world) => inner.center + (world - FieldCenter) * ms;
        foreach (MapNode n in net.Map.Nodes)
        {
            Vector2 p = Mini(n.Pos);
            Color c = n.District == District.Home ? NetrunUI.Accent : DistrictColor(n.District);
            Widgets.DrawBoxSolid(new Rect(p.x - 1.5f, p.y - 1.5f, 3f, 3f), new Color(c.r, c.g, c.b, 0.85f));
        }
        foreach (NetRunner r in net.Runners)
        {
            Vector2 p = Mini(net.Map.Node(r.NodeId).Pos);
            Widgets.DrawBoxSolid(new Rect(p.x - 2.5f, p.y - 2.5f, 5f, 5f), NetrunUI.Ok);
        }
        // The view frame: the map rect's corners in world space.
        var local = new Rect(0f, 0f, mapRect.width, mapRect.height);
        Vector2 a = Mini(ToWorld(local, Vector2.zero)), b = Mini(ToWorld(local, new Vector2(local.width, local.height)));
        var view = Rect.MinMaxRect(Mathf.Max(inner.x, a.x), Mathf.Max(inner.y, a.y), Mathf.Min(inner.xMax, b.x), Mathf.Min(inner.yMax, b.y));
        if (view.width > 2f && view.height > 2f)
        {
            GUI.color = new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.8f);
            Widgets.DrawBox(view);
            GUI.color = Color.white;
        }
        if (Mouse.IsOver(box))
            NetrunUI.Tip(box, "NR_MapMinimapTip".Translate());
    }

    private static void MinimapInput(Rect mapRect, Rect box)
    {
        Event e = Event.current;
        if ((e.type != EventType.MouseDown && e.type != EventType.MouseDrag) || e.button != 0 || !box.Contains(e.mousePosition))
            return;
        Rect inner = box.ContractedBy(6f);
        float ms = Mathf.Min(inner.width / 1.04f, inner.height / 0.70f);
        var local = new Rect(0f, 0f, mapRect.width, mapRect.height);
        Vector2 world = (e.mousePosition - inner.center) / ms + FieldCenter;
        _pan = -(world - FieldCenter) * FitScale(local) * _zoom;
        ClampPan(local);
        e.Use();
    }
}
