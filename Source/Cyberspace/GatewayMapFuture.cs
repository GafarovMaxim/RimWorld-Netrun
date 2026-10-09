using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Map v4, third queue (docs/TASKS_2026-10-08.md G3): timers on hubs and the "Next 3 days" column, runners off the
/// edge as arrows with "Lebo → Market-81 · 1h40", the Heat strip and vignette, the ping wave, visited hubs and the
/// semantic zoom of hub names. All lists are rebuilt once a second (timeline) or when the map changes (labels).
/// </summary>
public static partial class GatewayView
{
    // ---- Timeline: every timer that ends (or starts) within three days ----

    private struct TimerEntry
    {
        public int Ticks;
        public int Hub;
        public string Text;
        public bool Bad;
    }

    private static List<TimerEntry> _timeline = [];
    private static int _timelineHour = -1;
    private static int _timelineTick = -1;

    /// <summary>Rebuilt once per game hour (or when the game ticks back); the times shown count down from the build.</summary>
    private static List<TimerEntry> Timeline(NetMapManager net)
    {
        int nowTick = Find.TickManager.TicksGame;
        int hour = nowTick / GenDate.TicksPerHour;
        if (hour == _timelineHour && nowTick >= _timelineTick)
            return _timeline;
        _timelineHour = hour;
        _timelineTick = nowTick;
        var list = new List<TimerEntry>();
        int tick = Find.TickManager.TicksGame;
        int horizon = 3 * GenDate.TicksPerDay;
        void Add(int until, int hub, string key, bool bad, string arg)
        {
            int left = until - tick;
            if (left > 0 && left <= horizon)
                list.Add(new TimerEntry { Ticks = left, Hub = hub, Text = key.Translate(arg), Bad = bad });
        }
        foreach (MapNode n in net.Map.Nodes)
        {
            string label = NetMapManager.NodeLabel(n);
            Add(n.TransitUntil, n.Id, "NR_MapTimerTransit", false, label);
            Add(n.PatrolUntil, n.Id, "NR_MapTimerPatrol", true, label);
            Add(n.OfflineUntil, n.Id, "NR_MapTimerOffline", false, label);
            Add(n.StormUntil, n.Id, "NR_MapTimerStorm", false, label);
            Add(n.QuarantineUntil, n.Id, "NR_MapTimerQuarantine", false, label);
        }
        GatewayManager g = GatewayManager.Get();
        if (g?.Boss is { } boss)
            Add(boss.ExpireTick, boss.MapNodeId, "NR_MapTimerCoreCloses", false, boss.Name);
        else if (g != null && g.TicksUntilBoss >= 0)
            Add(tick + g.TicksUntilBoss, -1, "NR_MapTimerCoreOpens", false, string.Empty);
        foreach (IntrusionPacket p in IntrusionManager.Get()?.Packets ?? [])
            list.Add(new TimerEntry { Ticks = 0, Hub = p.NodeId, Text = "NR_MapTimerIntrusion".Translate(p.Attack?.label ?? "?"), Bad = true });
        list.Sort((a, b) => a.Ticks.CompareTo(b.Ticks));
        _timeline = list;
        return list;
    }

    /// <summary>Ticks left now (the list was built at the start of the hour).</summary>
    private static int Left(TimerEntry e) => e.Ticks <= 0 ? 0 : Mathf.Max(0, e.Ticks - (Find.TickManager.TicksGame - _timelineTick));

    private static string Short(int ticks) => ticks <= 0 ? (string)"NR_MapTimerNow".Translate()
        : ticks < GenDate.TicksPerDay ? (ticks / (float)GenDate.TicksPerHour).ToString("0") + "h" : (ticks / (float)GenDate.TicksPerDay).ToString("0.#") + "d";

    /// <summary>The "Next 3 days" section of the panel; a row centres its hub.</summary>
    private static float DrawTimeline(Rect inner, float y, NetMapManager net)
    {
        List<TimerEntry> list = Timeline(net);
        if (list.Count == 0)
            return y;
        float top = y;
        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_MapNext3Days".Translate());
        Text.Font = GameFont.Tiny;
        foreach (TimerEntry e in list.Take(6))
        {
            var row = new Rect(inner.x, y, inner.width, 20f);
            if (Mouse.IsOver(row))
                Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.05f));
            string text = (e.Bad ? "⚠ " : "◷ ") + Short(Left(e)) + " · " + e.Text;
            Widgets.Label(new Rect(row.x + 4f, row.y + 2f, row.width - 6f, row.height), text.Colorize(e.Bad ? NetrunUI.Bad : NetrunUI.TextColor));
            if (e.Hub >= 0 && Widgets.ButtonInvisible(row))
                FocusNode(e.Hub);
            y += 21f;
        }
        Text.Font = GameFont.Small;
        GuideRegistry.InScroll("gateway.timeline", _panelOuter, _panelScroll, new Rect(inner.x, top, inner.width, y - top));
        return y + 6f;
    }

    /// <summary>A small timer tag next to a hub whose state ends soon (shown from zoom 0.8).</summary>
    private static void DrawTimerTag(Rect hubRect, MapNode n, NetMapManager net)
    {
        if (_zoom < 0.8f)
            return;
        int best = int.MaxValue;
        foreach (TimerEntry e in Timeline(net))
            if (e.Hub == n.Id && e.Ticks > 0 && Left(e) < best)
                best = Mathf.Max(1, Left(e));
        if (best == int.MaxValue)
            return;
        string text = "◷ " + Short(best);
        Text.Font = GameFont.Tiny;
        float w = Text.CalcSize(text).x + 8f;
        var tag = new Rect(hubRect.xMax + 4f, hubRect.y - 16f, w, 14f);
        Widgets.DrawBoxSolid(tag, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.85f));
        GUI.color = new Color(NetrunUI.Warn.r, NetrunUI.Warn.g, NetrunUI.Warn.b, 0.7f);
        Widgets.DrawBox(tag);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(tag, text.Colorize(NetrunUI.Warn));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }

    /// <summary>The contracts running now, with their time left (the panel's content when no hub is picked).</summary>
    private static float DrawActiveContracts(Rect inner, float y)
    {
        var active = ContractManager.Get()?.Active.Where(c => c.Accepted).ToList();
        if (active == null || active.Count == 0)
            return y;
        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_MapActiveContracts".Translate());
        Text.Font = GameFont.Tiny;
        foreach (Contract c in active)
        {
            string text = c.Title + "  " + $"{c.Progress}/{c.Goal}".Colorize(NetrunUI.Ok) + "  · " + c.TicksLeft.ToStringTicksToPeriod().Colorize(NetrunUI.Muted);
            Widgets.Label(new Rect(inner.x + 4f, y, inner.width - 6f, 20f), text);
            y += 21f;
        }
        Text.Font = GameFont.Small;
        return y + 6f;
    }

    // ---- Runners: a label on the way, an arrow at the edge when off screen ----

    private static Vector2 RunnerPos(Rect local, NetRunner r, CyberMap map)
    {
        Vector2 p = ToScreen(local, map.Node(r.NodeId).Pos);
        if (r.Moving && map.Node(r.NextNodeId) is { } next)
        {
            MapLink l = map.LinkBetween(r.NodeId, r.NextNodeId);
            float t = l == null ? 0f : Mathf.Clamp01(r.Progress / Mathf.Max(0.05f, map.Hours(l, r.NodeId)));
            p = Vector2.Lerp(p, ToScreen(local, next.Pos), t);
        }
        return p;
    }

    private static string RunnerTag(NetRunner r, NetMapManager net)
    {
        string name = r.Pawn?.LabelShort ?? "?";
        if (!r.Moving)
            return name;
        float h = net.HoursLeft(r);
        return "NR_MapRunnerTag".Translate(name, NetMapManager.NodeLabel(net.Map.Node(r.Path[r.Path.Count - 1])), $"{(int)h}h{(int)((h - (int)h) * 60f):00}");
    }

    /// <summary>Runners outside the view: an arrow on the edge with the label; a click centres the runner.</summary>
    private static void DrawRunnerArrows(Rect mapRect, NetMapManager net)
    {
        var local = new Rect(0f, 0f, mapRect.width, mapRect.height);
        Rect inside = local.ContractedBy(14f);
        for (int i = 0; i < net.Runners.Count; i++)
        {
            NetRunner r = net.Runners[i];
            Vector2 p = RunnerPos(local, r, net.Map);
            if (inside.Contains(p))
                continue;
            Vector2 c = new(Mathf.Clamp(p.x, inside.x, inside.xMax), Mathf.Clamp(p.y, inside.y, inside.yMax));
            Vector2 dir = (p - c).normalized;
            Vector2 at = mapRect.position + c;
            Widgets.DrawLine(at - dir * 10f, at + dir * 6f, NetrunUI.Ok, 4f);
            Widgets.DrawLine(at + dir * 6f, at + dir * 6f - (Vector2)(Quaternion.Euler(0f, 0f, 35f) * dir) * 9f, NetrunUI.Ok, 3f);
            Widgets.DrawLine(at + dir * 6f, at + dir * 6f - (Vector2)(Quaternion.Euler(0f, 0f, -35f) * dir) * 9f, NetrunUI.Ok, 3f);
            string tag = RunnerTag(r, net);
            Text.Font = GameFont.Tiny;
            float w = Text.CalcSize(tag).x + 10f;
            var box = new Rect(Mathf.Clamp(at.x - w / 2f - dir.x * 30f, mapRect.x + 2f, mapRect.xMax - w - 2f),
                Mathf.Clamp(at.y - 9f - dir.y * 22f, mapRect.y + 2f, mapRect.yMax - 20f), w, 18f);
            Widgets.DrawBoxSolid(box, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.9f));
            GUI.color = NetrunUI.Ok;
            Widgets.DrawBox(box);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(box, tag.Colorize(NetrunUI.Ok));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (Widgets.ButtonInvisible(box.ExpandedBy(4f)))
            {
                _selectedRunner = i;
                _centerOn = r.NodeId;
            }
        }
    }

    // ---- Heat: a strip along the top edge, a vignette while it grows and in the Red Zone ----

    private static float _heatSeen = -1f, _heatSeenAt, _heatRiseAt = -10f;

    private static void DrawHeatEdge(Rect mapRect)
    {
        HeatTracker heat = HeatTracker.Get();
        if (heat == null || Event.current.type != EventType.Repaint)
            return;
        float now = Time.realtimeSinceStartup;
        if (_heatSeen < 0f || now - _heatSeenAt > 2f)
        {
            if (_heatSeen >= 0f && heat.Heat > _heatSeen + 0.5f)
                _heatRiseAt = now;
            _heatSeen = heat.Heat;
            _heatSeenAt = now;
        }
        Color lc = heat.Level.Color();
        Widgets.DrawBoxSolid(new Rect(mapRect.x, mapRect.y, mapRect.width, 3f), new Color(lc.r, lc.g, lc.b, 0.25f));
        Widgets.DrawBoxSolid(new Rect(mapRect.x, mapRect.y, mapRect.width * Mathf.Clamp01(heat.Heat / HeatTracker.MaxHeat), 3f), lc);
        float rise = NetrunMod.Settings.Animations ? Mathf.Clamp01(1f - (now - _heatRiseAt) / 2f) : 0f;
        float a = Mathf.Max(rise * 0.35f, heat.Level == HeatLevel.RedZone ? 0.22f : 0f);
        if (a <= 0f)
            return;
        // A soft red vignette: bands fading in from each edge.
        for (int i = 0; i < 6; i++)
        {
            float k = a * (1f - i / 6f) * 0.35f;
            float d = i * 6f;
            var col = new Color(NetrunUI.Bad.r, NetrunUI.Bad.g, NetrunUI.Bad.b, k);
            Widgets.DrawBoxSolid(new Rect(mapRect.x + d, mapRect.y + d, mapRect.width - 2f * d, 6f), col);
            Widgets.DrawBoxSolid(new Rect(mapRect.x + d, mapRect.yMax - d - 6f, mapRect.width - 2f * d, 6f), col);
            Widgets.DrawBoxSolid(new Rect(mapRect.x + d, mapRect.y + d + 6f, 6f, mapRect.height - 2f * d - 12f), col);
            Widgets.DrawBoxSolid(new Rect(mapRect.xMax - d - 6f, mapRect.y + d + 6f, 6f, mapRect.height - 2f * d - 12f), col);
        }
    }

    // ---- Ping: a wave from a hub when a runner scans around it ----

    private static readonly Dictionary<int, float> Pings = [];

    /// <summary>Called by the map when a runner pings: the wave shows if the terminal is open.</summary>
    public static void NotePing(int hubId) => Pings[hubId] = Time.realtimeSinceStartup;

    private static void DrawPing(Rect hubRect, int hubId)
    {
        if (!Pings.TryGetValue(hubId, out float at))
            return;
        float k = (Time.realtimeSinceStartup - at) / 1.5f;
        if (k >= 1f)
        {
            Pings.Remove(hubId);
            return;
        }
        GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 1f - k);
        GUI.DrawTexture(hubRect.ExpandedBy(10f + 90f * k * _zoom), NetrunUI.ThinRing);
        GUI.color = Color.white;
    }

    // ---- Hub names by zoom: interesting hubs from 0.8, all from 1.3 ----

    private static readonly Dictionary<int, string> HubNames = [];
    private static CyberMap _hubNamesFor;

    private static string HubName(MapNode n, CyberMap map)
    {
        if (_hubNamesFor != map)
        {
            HubNames.Clear();
            _hubNamesFor = map;
        }
        if (!HubNames.TryGetValue(n.Id, out string s))
            HubNames[n.Id] = s = NetMapManager.NodeLabel(n);
        return s;
    }

    private static void DrawHubName(Rect hubRect, MapNode n, NetMapManager net, List<ExternalNode> branches)
    {
        if (n.District == District.Home || _zoom < 0.8f)
            return;
        bool interesting = n.Fortress || n.Exit || net.Watch.Contains(n.Id) || branches.Any(b => b.Contract && !b.Hidden) || n.Id == _selectedMapNode;
        if (!interesting && _zoom < 1.3f)
            return;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperCenter;
        Color c = interesting ? NetrunUI.TextColor : NetrunUI.Muted;
        Widgets.Label(new Rect(hubRect.center.x - 60f, hubRect.yMax + 10f * Mathf.Sqrt(_zoom), 120f, 16f), HubName(n, net.Map).Colorize(c));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }

    /// <summary>Visited hubs keep a faint rim (the last visit is in the hover card).</summary>
    private static void DrawVisited(Rect hubRect, MapNode n)
    {
        if (n.LastVisitTick < 0 || n.District == District.Home)
            return;
        GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.35f);
        GUI.DrawTexture(hubRect.ExpandedBy(3f), NetrunUI.ThinRing);
        GUI.color = Color.white;
    }
}
