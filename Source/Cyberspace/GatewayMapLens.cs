using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>The map's lenses (docs/concept-cyberspace-map-v4.md, principle 4): each recolours the hubs on one scale.</summary>
public enum MapLens
{
    Overview,
    Loot,
    Danger,
    Corps,
    Contracts,
    Time,
}

/// <summary>
/// Map v4, second queue (docs/TASKS_2026-10-08.md G2): lenses 1-6 with their scale line, the route preview (fast and
/// quiet) on hover, the hover card, waypoints (Shift+click) and double-click to send, and the reachable next steps.
/// Every computed value is cached (per lens, per hovered hub) and rebuilt on change or every second, not per frame.
/// </summary>
public static partial class GatewayView
{
    private static readonly MapLens[] Lenses = [MapLens.Overview, MapLens.Loot, MapLens.Danger, MapLens.Corps, MapLens.Contracts, MapLens.Time];
    private static MapLens Lens => (MapLens)Mathf.Clamp(NetrunMod.Settings.MapLens, 0, Lenses.Length - 1);

    private static int _hoverHub = -1;
    private static int _lastClickHub = -1;
    private static float _lastClickAt;

    // ---- Lens bar and hotkeys ----

    private static Rect LensBarRect(Rect mapRect) => new(mapRect.x + 274f, mapRect.y + 6f, Lenses.Length * 58f, 26f);

    private static void DrawLensBar(Rect mapRect)
    {
        Rect bar = LensBarRect(mapRect);
        Text.Font = GameFont.Tiny;
        for (int i = 0; i < Lenses.Length; i++)
        {
            var r = new Rect(bar.x + i * 58f, bar.y, 54f, bar.height);
            bool on = Lens == Lenses[i];
            Widgets.DrawBoxSolid(r, on ? new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.25f)
                : new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.9f));
            GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, on ? 1f : 0.35f);
            Widgets.DrawBox(r);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(r, $"{i + 1} {("NR_MapLens_" + Lenses[i]).Translate()}".Colorize(on ? NetrunUI.Accent : NetrunUI.TextColor));
            Text.Anchor = TextAnchor.UpperLeft;
            NetrunUI.Tip(r, ("NR_MapLensTip_" + Lenses[i]).Translate());
            if (Widgets.ButtonInvisible(r))
                SetLens(Lenses[i]);
        }
        Text.Font = GameFont.Small;
    }

    private static void SetLens(MapLens lens)
    {
        NetrunMod.Settings.MapLens = (int)lens;
        NetrunSound.Click();
    }

    /// <summary>Keys 1-6 pick a lens (called from the map's hotkeys).</summary>
    private static bool LensHotkey(KeyCode key)
    {
        int i = key >= KeyCode.Alpha1 && key <= KeyCode.Alpha6 ? key - KeyCode.Alpha1
            : key >= KeyCode.Keypad1 && key <= KeyCode.Keypad6 ? key - KeyCode.Keypad1 : -1;
        if (i < 0)
            return false;
        SetLens(Lenses[i]);
        return true;
    }

    // ---- Lens values: hub id -> 0..1 on the lens's scale, -1 when it does not apply ----

    // Rebuilt at most once a second, or when the lens or the selected runner changes (no per-hub string keys).
    private static Dictionary<int, float> _lensValues;
    private static (MapLens lens, int runner) _lensFor;
    private static float _lensAt = -10f;

    private static Dictionary<int, float> LensValues(NetMapManager net, MapLens lens)
    {
        float now = Time.realtimeSinceStartup;
        if (_lensValues == null || _lensFor != (lens, _selectedRunner) || now - _lensAt > 1f)
        {
            _lensValues = BuildLens(net, lens);
            _lensFor = (lens, _selectedRunner);
            _lensAt = now;
        }
        return _lensValues;
    }

    private static Dictionary<int, float> BuildLens(NetMapManager net, MapLens lens)
    {
        var values = new Dictionary<int, float>();
        if (lens == MapLens.Time)
        {
            NetRunner r = SelectedRunner(net);
            if (r == null)
                return values;
            Dictionary<int, float> hours = HoursFrom(net, r.Moving ? r.NextNodeId : r.NodeId, quiet: false, out _);
            float speed = NetMapManager.Speed(r.Pawn);
            foreach (KeyValuePair<int, float> kv in hours)
                values[kv.Key] = Mathf.Clamp01(kv.Value / speed / 12f);
            return values;
        }
        foreach (MapNode n in net.Map.Nodes)
        {
            List<ExternalNode> known = BranchesOf(n.Id).Where(b => !b.Hidden).ToList();
            float v = lens switch
            {
                MapLens.Loot => known.Count == 0 ? -1f : Mathf.Clamp01((known.Max(b => b.RewardMultiplier) - 0.5f) / 2.5f),
                MapLens.Danger => Danger(n, known),
                MapLens.Corps => n.District == District.Corp && net.CorpOfSlot(n.CorpSlot) != null ? 1f : -1f,
                MapLens.Contracts => known.Any(b => b.Contract) || net.Watch.Contains(n.Id) ? 1f : -1f,
                _ => -1f,
            };
            values[n.Id] = v;
        }
        return values;
    }

    /// <summary>0..1: the strongest known ICE or gate, plus a patrol and a fortress.</summary>
    private static float Danger(MapNode n, List<ExternalNode> known)
    {
        float d = known.Count == 0 ? 0f : known.Max(b => b.Defense / 6f + (b.BlackIce ? 0.35f : 0f));
        if (n.Patrol)
            d += 0.35f;
        if (n.Fortress)
            d += 0.45f;
        return Mathf.Clamp01(d);
    }

    private static readonly Color ScaleLow = NetrunUI.Hex(0x31c46a), ScaleMid = NetrunUI.Hex(0xe6c431), ScaleHigh = NetrunUI.Hex(0xff4a3c);

    private static Color LensColor(MapLens lens, float t, MapNode n, NetMapManager net) => lens switch
    {
        MapLens.Loot => Color.Lerp(new Color(0.45f, 0.45f, 0.4f), NetrunUI.Hex(0xffd34a), t),
        MapLens.Corps => HubColor(n, net),
        MapLens.Contracts => NetrunUI.Warn,
        _ => t < 0.5f ? Color.Lerp(ScaleLow, ScaleMid, t * 2f) : Color.Lerp(ScaleMid, ScaleHigh, (t - 0.5f) * 2f),
    };

    /// <summary>The hub's colour under the active lens; null in the Overview (the hub keeps its own colour).</summary>
    private static Color? LensHubColor(MapNode n, NetMapManager net)
    {
        MapLens lens = Lens;
        if (lens == MapLens.Overview)
            return null;
        if (!LensValues(net, lens).TryGetValue(n.Id, out float t) || t < 0f)
            return new Color(NetrunUI.Muted.r, NetrunUI.Muted.g, NetrunUI.Muted.b, 0.35f);
        return LensColor(lens, t, n, net);
    }

    /// <summary>One line under the map: the active lens and its scale.</summary>
    private static void DrawLensScale(Rect mapRect, NetMapManager net)
    {
        MapLens lens = Lens;
        if (lens == MapLens.Overview)
            return;
        var box = new Rect(mapRect.x + 6f, mapRect.yMax - 34f, 300f, 28f);
        Widgets.DrawBoxSolid(box, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.9f));
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        string name = ((string)("NR_MapLens_" + lens).Translate()).ToUpper();
        Widgets.Label(new Rect(box.x + 6f, box.y, 70f, box.height), name.Colorize(NetrunUI.Accent));
        var bar = new Rect(box.x + 80f, box.y + 9f, box.width - 150f, 10f);
        if (lens is MapLens.Corps or MapLens.Contracts)
        {
            Widgets.Label(new Rect(bar.x, box.y, box.width - 86f, box.height), ((string)("NR_MapLensScale_" + lens).Translate()).Colorize(NetrunUI.TextColor));
        }
        else
        {
            for (int i = 0; i < 20; i++)
                Widgets.DrawBoxSolid(new Rect(bar.x + bar.width * i / 20f, bar.y, bar.width / 20f + 1f, bar.height), LensColor(lens, i / 19f, null, net));
            Widgets.Label(new Rect(bar.xMax + 6f, box.y, 64f, box.height), ((string)("NR_MapLensScale_" + lens).Translate()).Colorize(NetrunUI.Muted));
            if (lens == MapLens.Time && SelectedRunner(net) == null)
                Widgets.Label(new Rect(bar.x, box.y, bar.width, box.height), ((string)"NR_MapLensNoRunner".Translate()).Colorize(NetrunUI.Warn));
        }
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }

    // ---- Routes: fastest, and quiet (least trace), from one hub ----

    /// <summary>Dijkstra from a hub: game hours to every reachable hub (before the runner's speed); quiet weighs trace heavily.</summary>
    private static Dictionary<int, float> HoursFrom(NetMapManager net, int from, bool quiet, out Dictionary<int, int> prev)
    {
        CyberMap map = net.Map;
        var cost = new Dictionary<int, float> { [from] = 0f };
        var hours = new Dictionary<int, float> { [from] = 0f };
        prev = new Dictionary<int, int>();
        var open = new List<int> { from };
        var done = new HashSet<int>();
        while (open.Count > 0)
        {
            int u = open[0];
            for (int i = 1; i < open.Count; i++)
                if (cost[open[i]] < cost[u])
                    u = open[i];
            open.Remove(u);
            if (!done.Add(u))
                continue;
            if (u != from && map.Node(u) is { Offline: true })
                continue;
            foreach (MapLink l in map.LinksOf(u))
            {
                if (!map.Passable(l))
                    continue;
                int v = l.Other(u);
                if (done.Contains(v))
                    continue;
                float h = map.Hours(l, u);
                float c = cost[u] + h + (quiet ? 10f * NetMapManager.HopTrace(map.Node(v), l) : 0f);
                if (!cost.TryGetValue(v, out float old) || c < old)
                {
                    cost[v] = c;
                    hours[v] = hours[u] + h;
                    prev[v] = u;
                    if (!open.Contains(v))
                        open.Add(v);
                }
            }
        }
        return hours;
    }

    private struct RoutePreview
    {
        public List<int> Fast, Quiet;
        public float FastHours, QuietHours, FastTrace, QuietTrace;
    }

    private static (int from, int to) _previewFor = (-1, -1);
    private static RoutePreview _preview;

    /// <summary>The fast and quiet routes from the selected runner to a hub, rebuilt only when either end changes.</summary>
    private static RoutePreview? Preview(NetMapManager net, int target)
    {
        NetRunner r = SelectedRunner(net);
        if (r == null || r.Busy || target < 0)
            return null;
        int from = r.Moving ? r.NextNodeId : r.NodeId;
        if (from == target)
            return null;
        if (_previewFor != (from, target))
        {
            _previewFor = (from, target);
            _preview = new RoutePreview();
            Fill(net, from, target, false, out _preview.Fast, out _preview.FastHours, out _preview.FastTrace);
            Fill(net, from, target, true, out _preview.Quiet, out _preview.QuietHours, out _preview.QuietTrace);
            float speed = NetMapManager.Speed(r.Pawn);
            _preview.FastHours /= speed;
            _preview.QuietHours /= speed;
        }
        return _preview.Fast == null ? null : _preview;
    }

    private static void Fill(NetMapManager net, int from, int to, bool quiet, out List<int> path, out float hours, out float trace)
    {
        Dictionary<int, float> h = HoursFrom(net, from, quiet, out Dictionary<int, int> prev);
        path = null;
        hours = trace = 0f;
        if (!h.TryGetValue(to, out hours))
            return;
        path = [];
        for (int c = to; c != from; c = prev[c])
        {
            path.Add(c);
            trace += NetMapManager.HopTrace(net.Map.Node(c), net.Map.LinkBetween(prev[c], c));
        }
        path.Add(from);
        path.Reverse();
    }

    /// <summary>Draws the previewed routes on the field: fast as a bright line, quiet dashed when it differs.</summary>
    private static void DrawPreview(Rect local, NetMapManager net, int hub)
    {
        if (Preview(net, hub) is not { } p)
            return;
        bool differs = p.Quiet != null && !p.Quiet.SequenceEqual(p.Fast);
        if (differs)
            DrawPath(local, net, p.Quiet, NetrunUI.Purple, dashed: true);
        DrawPath(local, net, p.Fast, NetrunUI.Ok, dashed: false);
    }

    private static void DrawPath(Rect local, NetMapManager net, List<int> path, Color c, bool dashed)
    {
        for (int i = 1; i < path.Count; i++)
        {
            Vector2 a = ToScreen(local, net.Map.Node(path[i - 1]).Pos), b = ToScreen(local, net.Map.Node(path[i]).Pos);
            if (!dashed)
            {
                Widgets.DrawLine(a, b, new Color(c.r, c.g, c.b, 0.25f), 9f);
                Widgets.DrawLine(a, b, c, 3f);
                continue;
            }
            int segments = Mathf.Max(2, Mathf.FloorToInt(Vector2.Distance(a, b) / 8f));
            for (int s = 0; s < segments; s += 2)
                Widgets.DrawLine(Vector2.Lerp(a, b, s / (float)segments), Vector2.Lerp(a, b, Mathf.Min(1f, (s + 1) / (float)segments)), c, 2.5f);
        }
    }

    // ---- The hover card ----

    private static string HubCard(MapNode n, NetMapManager net, List<ExternalNode> branches)
    {
        var lines = new List<string> { NetMapManager.NodeLabel(n).ToUpper().Colorize(NetrunUI.Accent) };
        var known = branches.Where(b => !b.Hidden).ToList();
        string district = n.District == District.Corp && net.CorpOfSlot(n.CorpSlot) is { } corp ? corp.Name : ("NR_District_" + n.District).Translate();
        lines.Add("NR_MapCardDistrict".Translate(district, Mathf.CeilToInt(Danger(n, known) * 5f)));
        lines.Add("NR_MapBranches".Translate(known.Count, branches.Count - known.Count));
        if (known.Count > 0)
            lines.Add("NR_MapCardHaul".Translate(known.Max(b => b.RewardMultiplier).ToString("0.#")));
        if (n.Exit)
            lines.Add("NR_MapExitPoint".Translate());
        if (n.Fortress)
            lines.Add("NR_MapFortress".Translate());
        lines.AddRange(NodeStates(n));
        if (known.Any(b => b.Contract))
            lines.Add(("★ " + "NR_MapCardContract".Translate()).Colorize(NetrunUI.Warn));
        if (Preview(net, n.Id) is { } p)
        {
            NetRunner r = SelectedRunner(net);
            lines.Add(("NR_MapCardRoute".Translate(r.Pawn?.LabelShort ?? "?", p.FastHours.ToString("0.#"), p.FastTrace.ToString("0.#"))).Colorize(NetrunUI.Ok));
            if (p.Quiet != null && !p.Quiet.SequenceEqual(p.Fast))
                lines.Add(("NR_MapCardQuiet".Translate(p.QuietHours.ToString("0.#"), p.QuietTrace.ToString("0.#"))).Colorize(NetrunUI.Purple));
        }
        lines.Add(n.LastVisitTick < 0 ? (string)"NR_MapCardNever".Translate()
            : (string)"NR_MapCardLast".Translate((Find.TickManager.TicksGame - n.LastVisitTick).ToStringTicksToPeriod()));
        lines.Add(((string)"NR_MapCardClicks".Translate()).Colorize(NetrunUI.Muted));
        return lines.ToLineList();
    }

    // ---- Clicks: shift adds a waypoint, a double click sends the runner ----

    /// <returns>True when the click was used for travel.</returns>
    private static bool TravelClick(MapNode n, NetMapManager net)
    {
        NetRunner r = SelectedRunner(net);
        float now = Time.realtimeSinceStartup;
        bool dbl = _lastClickHub == n.Id && now - _lastClickAt < 0.35f;
        _lastClickHub = n.Id;
        _lastClickAt = now;
        if (r == null)
            return false;
        if (Event.current.shift && r.Moving && n.Id != r.Path[r.Path.Count - 1])
        {
            // A waypoint: the route continues from where it now ends.
            List<int> more = net.Map.Route(r.Path[r.Path.Count - 1], n.Id, out _);
            if (more.Count > 0)
            {
                r.Path.AddRange(more);
                NetrunSound.Click();
                return true;
            }
        }
        if ((dbl || Event.current.shift) && net.CanTravel(r, n.Id).Accepted)
        {
            net.Travel(r, n.Id);
            NetrunSound.Click();
            return true;
        }
        return false;
    }

    // ---- Reachable next steps ----

    private static readonly HashSet<int> NeighbourSet = [];
    private static (int runner, int node) _neighboursFor = (-1, -1);

    private static HashSet<int> Neighbours(NetMapManager net)
    {
        NetRunner r = SelectedRunner(net);
        var key = (_selectedRunner, r?.NodeId ?? -1);
        if (key == _neighboursFor)
            return NeighbourSet;
        _neighboursFor = key;
        NeighbourSet.Clear();
        if (r == null)
            return NeighbourSet;
        foreach (MapLink l in net.Map.LinksOf(r.NodeId))
            if (net.Map.Passable(l))
                NeighbourSet.Add(l.Other(r.NodeId));
        NeighbourSet.Add(r.NodeId);
        return NeighbourSet;
    }
}
