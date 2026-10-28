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

    // Keys of the lens labels, built once (no string concatenation per button per event: PERF_AUDIT_4, 19).
    private static string[] _lensKeys, _lensTipKeys, _lensScaleKeys;

    private static void LensKeys()
    {
        if (_lensKeys != null)
            return;
        _lensKeys = Lenses.Select(l => "NR_MapLens_" + l).ToArray();
        _lensTipKeys = Lenses.Select(l => "NR_MapLensTip_" + l).ToArray();
        _lensScaleKeys = Lenses.Select(l => "NR_MapLensScale_" + l).ToArray();
    }

    private static readonly string[] LensIcons = ["Node_NeutralZone", "Reward_Valuables", "Dive_Lightning_Red", "Node_CorpVault", "Fixer_Service_Reward", "Dive_Lightning_Yellow"];
    private const float LensIconS = 14f, LensGap = 4f;
    private static float[] _lensW;
    private static string _lensWLang;
    private static float _lensTotal;

    // Button widths (icon + label + padding), measured once per language, not per frame.
    private static void LensWidths()
    {
        LensKeys();
        string lang = LanguageDatabase.activeLanguage?.folderName;
        if (_lensW != null && _lensWLang == lang)
            return;
        _lensWLang = lang;
        _lensW = new float[Lenses.Length];
        _lensTotal = 0f;
        GameFont old = Text.Font;
        Text.Font = GameFont.Tiny;
        for (int i = 0; i < Lenses.Length; i++)
        {
            _lensW[i] = Mathf.Ceil(NetrunUI.Width(NetrunUI.T(_lensKeys[i]))) + LensIconS + LensGap + 16f;
            _lensTotal += _lensW[i] + 4f;
        }
        Text.Font = old;
    }

    private static Rect LensBarRect(Rect mapRect)
    {
        LensWidths();
        return new Rect(mapRect.x + 274f, mapRect.y + 6f, _lensTotal - 4f, 26f);
    }

    private static void DrawLensBar(Rect mapRect)
    {
        Rect bar = LensBarRect(mapRect);
        Text.Font = GameFont.Tiny;
        float x = bar.x;
        for (int i = 0; i < Lenses.Length; i++)
        {
            var r = new Rect(x, bar.y, _lensW[i], bar.height);
            x += _lensW[i] + 4f;
            bool on = Lens == Lenses[i];
            Widgets.DrawBoxSolid(r, on ? new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.25f)
                : new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.9f));
            GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, on ? 1f : 0.35f);
            Widgets.DrawBox(r);
            GUI.color = Color.white;
            Color tc = on ? NetrunUI.Accent : NetrunUI.TextColor;
            Texture2D icon = NetrunTex.CyberIcon(LensIcons[i]);
            Rect label = r;
            if (icon != null && Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(new Rect(r.x + 8f, r.center.y - LensIconS / 2f, LensIconS, LensIconS), NetrunTint.OfAny(icon, tc));
                label = new Rect(r.x + 8f + LensIconS + LensGap, r.y, r.width - 8f - LensIconS - LensGap, r.height);
            }
            else if (icon != null)
                label = new Rect(r.x + 8f + LensIconS + LensGap, r.y, r.width - 8f - LensIconS - LensGap, r.height);
            Text.Anchor = icon != null ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            GUI.color = tc;
            Widgets.Label(label, NetrunUI.T(_lensKeys[i]));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            NetrunUI.Tip(r, NetrunUI.T(_lensTipKeys[i]));
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
                MapLens.Loot => known.Count == 0 ? -1f : Mathf.Clamp01((known.Max(b => b.ExpectedHaul) - 1f) / 9f),
                MapLens.Danger => Danger(n, known),
                MapLens.Corps => n.District == District.Corp && net.CorpOfSlot(n.CorpSlot) != null ? 1f : -1f,
                MapLens.Contracts => known.Any(b => b.Contract) || net.Watch.Contains(n.Id) || WatchedHubs().Contains(n.Id) ? 1f : -1f,
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
        MapLens.Loot => t < 0.5f ? Color.Lerp(NetrunUI.Hex(0x4a5f78), NetrunUI.Hex(0x2fc4a4), t * 2f) : Color.Lerp(NetrunUI.Hex(0x2fc4a4), NetrunUI.Hex(0xffd34a), (t - 0.5f) * 2f),
        MapLens.Corps => HubColor(n, net),
        MapLens.Contracts => NetrunUI.Warn,
        _ => t < 0.5f ? Color.Lerp(ScaleLow, ScaleMid, t * 2f) : Color.Lerp(ScaleMid, ScaleHigh, (t - 0.5f) * 2f),
    };

    /// <summary>The active lens's value for a hub (0..1), or -1 when it has none (or in the Overview).</summary>
    private static float LensValueOf(MapNode n, NetMapManager net)
        => Lens == MapLens.Overview || !LensValues(net, Lens).TryGetValue(n.Id, out float t) ? -1f : t;

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

    internal const float LensBoxH = 52f;

    /// <summary>
    /// The active lens under the map: its name, what it shows, and its scale (a gradient with both ends named) or a
    /// colour note for the lenses without a scale.
    /// </summary>
    private static void DrawLensScale(Rect mapRect, NetMapManager net)
    {
        MapLens lens = Lens;
        if (lens == MapLens.Overview)
            return;
        LensKeys();
        int li = System.Array.IndexOf(Lenses, lens);
        var box = new Rect(mapRect.x + 6f, mapRect.yMax - LensBoxH - 6f, 360f, LensBoxH);
        Widgets.DrawBoxSolid(box, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.94f));
        GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.45f);
        Widgets.DrawBox(box);
        GUI.color = Color.white;
        Widgets.DrawBoxSolid(new Rect(box.x, box.y, 3f, box.height), NetrunUI.Accent);
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = NetrunUI.Accent;
        string name = NetrunUI.Upper(NetrunUI.T(_lensKeys[li])) + " //";
        Widgets.Label(new Rect(box.x + 10f, box.y + 4f, 120f, 16f), name);
        float nameW = NetrunUI.Width(name) + 14f;
        GUI.color = NetrunUI.Muted;
        // What the lens shows (its tooltip text without the "Name:" part).
        string tip = NetrunUI.T(_lensTipKeys[li]);
        int colon = tip.IndexOf(": ", System.StringComparison.Ordinal);
        string what = colon > 0 ? tip.Substring(colon + 2) : tip;
        Widgets.Label(new Rect(box.x + 10f + nameW, box.y + 4f, box.width - nameW - 16f, 16f), what.Truncate(box.width - nameW - 16f));
        GUI.color = Color.white;
        var bar = new Rect(box.x + 10f, box.y + 26f, box.width - 20f, 8f);
        if (lens is MapLens.Corps or MapLens.Contracts)
        {
            GUI.color = NetrunUI.TextColor;
            Widgets.Label(new Rect(bar.x, box.y + 24f, bar.width, 20f), NetrunUI.T(_lensScaleKeys[li]));
            GUI.color = Color.white;
        }
        else
        {
            for (int i = 0; i < 40; i++)
                Widgets.DrawBoxSolid(new Rect(bar.x + bar.width * i / 40f, bar.y, bar.width / 40f + 1f, bar.height), LensColor(lens, i / 39f, null, net));
            GUI.color = NetrunUI.Muted;
            Widgets.Label(new Rect(bar.x, bar.yMax + 1f, 120f, 16f), NetrunUI.T("NR_MapLensLow_" + lens));
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(bar.xMax - 120f, bar.yMax + 1f, 120f, 16f), NetrunUI.T(_lensScaleKeys[li]));
            Text.Anchor = TextAnchor.UpperLeft;
            if (lens == MapLens.Time && SelectedRunner(net) == null)
            {
                GUI.color = NetrunUI.Warn;
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(new Rect(bar.x, bar.yMax + 1f, bar.width, 16f), NetrunUI.T("NR_MapLensNoRunner"));
                Text.Anchor = TextAnchor.UpperLeft;
            }
            GUI.color = Color.white;
        }
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
    /// <summary>
    /// The route being planned (hover): a light dashed line with dashes marching toward the target, and the quiet way in
    /// purple. The route a runner already walks stays a solid line in the accent colour (DrawMapLink), so the two never
    /// look alike (Maxim, 2026-10-14).
    /// </summary>
    private static void DrawPreview(Rect local, NetMapManager net, int hub)
    {
        if (Preview(net, hub) is not { } p)
            return;
        bool differs = p.Quiet != null && !p.Quiet.SequenceEqual(p.Fast);
        if (differs)
            DrawPath(local, net, p.Quiet, NetrunUI.Purple, 2f);
        DrawPath(local, net, p.Fast, NetrunUI.TextColor, 2.5f);
    }

    private static void DrawPath(Rect local, NetMapManager net, List<int> path, Color c, float width)
    {
        // Dashes of 8 px with 6 px gaps, shifting along the path over time (still when animations are off).
        float phase = NetrunMod.Settings.Animations ? Time.realtimeSinceStartup * 18f % 14f : 0f;
        Color glow = new(c.r, c.g, c.b, 0.12f);
        bool shared = false;
        for (int i = 1; i < path.Count; i++)
        {
            Vector2 a = ToScreen(local, net.Map.Node(path[i - 1]).Pos), b = ToScreen(local, net.Map.Node(path[i]).Pos);
            float len = Vector2.Distance(a, b);
            if (len < 1f)
                continue;
            // Where the new path runs on the active route it is not drawn: the route is already there (Maxim, 2026-10-23,
            // variant 2). A notch marks where the new path leaves it.
            if (Route.Contains((Mathf.Min(path[i - 1], path[i]), Mathf.Max(path[i - 1], path[i]))))
            {
                shared = true;
                continue;
            }
            if (shared)
            {
                shared = false;
                Vector2 n = new Vector2(-(b - a).y, (b - a).x) / len * 7f;
                Widgets.DrawLine(a - n, a + n, c, 2.5f);
            }
            Widgets.DrawLine(a, b, glow, 8f);
            Vector2 u = (b - a) / len;
            for (float d = phase - 14f; d < len; d += 14f)
            {
                float d0 = Mathf.Max(0f, d), d1 = Mathf.Min(len, d + 8f);
                if (d1 > d0)
                    Widgets.DrawLine(a + u * d0, a + u * d1, c, width);
            }
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
            lines.Add("NR_MapCardHaul".Translate(known.Max(b => b.ExpectedHaul).ToString("0.#")));
        if (n.Exit)
            lines.Add("NR_MapExitPoint".Translate());
        if (n.Fortress)
            lines.Add("NR_MapFortress".Translate());
        if (n.Guarded && NetrunMod.Settings.ProtectedHubs)
        {
            string key = GuardKeyHub(n.Id) is { } k ? NetMapManager.NodeLabel(net.Map.Node(k)) : "?";
            lines.Add((n.GuardLocked ? "NR_MapCardGuardShut".Translate(key) : "NR_MapCardGuardOpen".Translate((n.GuardOpenUntil - Find.TickManager.TicksGame).ToStringTicksToPeriod()))
                .Colorize(NetrunUI.Warn));
        }
        // This hub holds a guard gateway: say which hub it opens.
        if (known.Any(b => b.GuardFor >= 0))
            lines.Add("NR_MapCardGuardKey".Translate(NetMapManager.NodeLabel(net.Map.Node(known.First(b => b.GuardFor >= 0).GuardFor))).Colorize(NetrunUI.Warn));
        if (n.District == District.Orbit && !NetMapManager.OrbitOpen)
            lines.Add("NR_MapCardOrbitClosed".Translate().Colorize(NetrunUI.Bad));
        lines.AddRange(NodeStates(n).Select(s => s.text));
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
            if (net.Map.Passable(l) && net.Map.Node(l.Other(r.NodeId))?.Offline != true)
                NeighbourSet.Add(l.Other(r.NodeId));
        NeighbourSet.Add(r.NodeId);
        return NeighbourSet;
    }
}
