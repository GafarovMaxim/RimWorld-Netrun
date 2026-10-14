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

    private static GUIStyle SearchStyle;

    private static void DrawSearch(Rect at, NetMapManager net)
    {
        var field = new Rect(at.x, at.y, Mathf.Min(260f, at.width), 26f);
        bool focused = GUI.GetNameOfFocusedControl() == SearchControl;
        // A terminal prompt instead of the game's grey text box: a dark field, an accent frame that lights on focus,
        // a ">" in front and the text in the terminal's colour.
        if (focused)
            NetrunUI.Glow(field, NetrunUI.Accent, 0.35f, 6f);
        Widgets.DrawBoxSolid(field, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.95f));
        GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, focused ? 0.9f : 0.45f);
        Widgets.DrawBox(field);
        GUI.color = Color.white;
        Widgets.DrawBoxSolid(new Rect(field.x, field.y, 3f, field.height), NetrunUI.Accent);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = NetrunUI.Accent;
        Widgets.Label(new Rect(field.x + 8f, field.y, 14f, field.height), ">");
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        SearchStyle ??= new GUIStyle(Text.CurTextFieldStyle)
        {
            normal = { background = null, textColor = NetrunUI.TextColor },
            focused = { background = null, textColor = NetrunUI.TextColor },
            hover = { background = null, textColor = NetrunUI.TextColor },
            active = { background = null, textColor = NetrunUI.TextColor },
        };
        // Esc in the field clears it (Maxim, 2026-10-14: the search could not be reset without reopening the terminal).
        if (focused && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            ClearSearch();
            Event.current.Use();
        }
        GUI.SetNextControlName(SearchControl);
        _search = GUI.TextField(new Rect(field.x + 22f, field.y + 1f, field.width - 48f, field.height - 2f), _search ?? string.Empty, SearchStyle);
        if (!_search.NullOrEmpty())
        {
            var clear = new Rect(field.xMax - 22f, field.y + 3f, 20f, 20f);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Mouse.IsOver(clear) ? NetrunUI.Bad : NetrunUI.Muted;
            Widgets.Label(clear, "✕");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(clear, NetrunUI.T("NR_MapSearchClear"));
            if (Widgets.ButtonInvisible(clear))
                ClearSearch();
        }
        if (_search.NullOrEmpty() && !focused)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = NetrunUI.Muted;
            Widgets.Label(new Rect(field.x + 24f, field.y, field.width - 28f, field.height), NetrunUI.T("NR_MapSearchHint"));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        if (_search != _searchFor)
            FindHits(net);
        if (_search.NullOrEmpty())
        {
            if (focused && Event.current.rawType == EventType.MouseDown && !field.Contains(Event.current.mousePosition))
                GUI.FocusControl(null);
            return;
        }
        // A click anywhere outside the field and its results drops the search.
        var area = new Rect(field.x, field.y, field.width, field.height + 4f + Mathf.Max(1, SearchHits.Count) * 23f);
        if (Event.current.rawType == EventType.MouseDown && !area.Contains(Event.current.mousePosition))
        {
            ClearSearch();
            return;
        }
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

    private static void ClearSearch()
    {
        _search = string.Empty;
        GUI.FocusControl(null);
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
        var items = new List<TerminalMenu.Item>();
        NetRunner runner = SelectedRunner(net);
        if (runner != null && (runner.NodeId != n.Id || runner.Moving))
        {
            AcceptanceReport can = net.CanTravel(runner, n.Id);
            items.Add(new TerminalMenu.Item
            {
                Label = "NR_MapMenuSend".Translate(runner.Pawn?.LabelShort ?? "?"), Glyph = "▶",
                Action = () => net.Travel(runner, n.Id), Disabled = can.Accepted ? null : can.Reason,
            });
        }
        if (runner != null && runner.Moving && runner.Path[runner.Path.Count - 1] != n.Id)
            items.Add(new TerminalMenu.Item
            {
                Label = "NR_MapMenuWaypoint".Translate(), Glyph = "⇢",
                Action = () =>
                {
                    List<int> more = net.Map.Route(runner.Path[runner.Path.Count - 1], n.Id, out _);
                    if (more.Count > 0)
                        runner.Path.AddRange(more);
                },
            });
        bool watched = net.Watch.Contains(n.Id);
        items.Add(new TerminalMenu.Item { Label = (watched ? "NR_MapMenuUnwatch" : "NR_MapMenuWatch").Translate(), Glyph = watched ? "☆" : "★", Action = () => net.ToggleWatch(n.Id) });
        items.Add(new TerminalMenu.Item { Label = "NR_MapMenuCenter".Translate(), Glyph = "◎", Action = () => FocusNode(n.Id) });
        TerminalMenu.Open(NetMapManager.NodeLabel(n), items);
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
            // A card per pin: a star (amber for a contract target), the hub, its timer if one runs, "Show".
            var row = new Rect(inner.x, y, inner.width, 34f);
            Color c = contract ? NetrunUI.Warn : NetrunUI.Accent;
            NetrunUI.Card(row, c);
            GUI.color = c;
            GUI.DrawTexture(new Rect(row.x + 10f, row.y + 11f, 12f, 12f), ShapeTex(Shape.Diamond, contract));
            GUI.color = NetrunUI.TextColor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            float w = row.width - 30f - 80f;
            Widgets.Label(new Rect(row.x + 30f, row.y, w, row.height), label.Truncate(w));
            if (_nearestTimer.TryGetValue(hub, out TimerEntry timer))
            {
                Text.Font = GameFont.Tiny;
                GUI.color = NetrunUI.Warn;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(row.x + 30f, row.y, w, row.height), "◷ " + Short(Mathf.Max(1, Left(timer))));
            }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (NetrunUI.Button(new Rect(row.xMax - 74f, row.y + 4f, 66f, 26f), "NR_MapShow".Translate(), c, true))
                FocusNode(hub, branch);
            y += 38f;
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
