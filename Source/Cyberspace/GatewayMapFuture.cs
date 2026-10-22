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
        // The state alone, without the hub's name, for the hub's own description (null: use Text).
        public string State;
        public bool Bad;
    }

    private static List<TimerEntry> _timeline = [];
    private static int _timelineHour = -1;
    private static int _timelineTick = -1;
    private static readonly Dictionary<int, TimerEntry> _nearestTimer = [];
    private static readonly Dictionary<int, (int bucket, string text, float width)> _timerTags = [];

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
        void Add(int until, int hub, string key, bool bad, string arg, string state = null)
        {
            int left = until - tick;
            if (left > 0 && left <= horizon)
                list.Add(new TimerEntry { Ticks = left, Hub = hub, Text = key.Translate(arg), State = state?.Translate(), Bad = bad });
        }
        foreach (MapNode n in net.Map.Nodes)
        {
            string label = NetMapManager.NodeLabel(n);
            Add(n.TransitUntil, n.Id, "NR_MapTimerTransit", false, label, "NR_MapStateTransit");
            Add(n.PatrolUntil, n.Id, "NR_MapTimerPatrol", true, label, "NR_MapStatePatrol");
            Add(n.OfflineUntil, n.Id, "NR_MapTimerOffline", false, label, "NR_MapStateOffline");
            Add(n.StormUntil, n.Id, "NR_MapTimerStorm", false, label, "NR_MapStateStorm");
            Add(n.QuarantineUntil, n.Id, "NR_MapTimerQuarantine", false, label, "NR_MapStateQuarantine");
            Add(n.RivalTrapUntil, n.Id, "NR_MapTimerRivalTrap", true, label, "NR_MapStateRivalTrap");
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
        // The nearest running timer of every hub, for the tags (no pass over the whole list per hub: PERF_AUDIT_4, 18).
        _nearestTimer.Clear();
        for (int i = 0; i < list.Count; i++)
            if (list[i].Ticks > 0 && !_nearestTimer.ContainsKey(list[i].Hub))
                _nearestTimer[list[i].Hub] = list[i];
        return list;
    }

    /// <summary>
    /// D4 "Today on the map" for the Core: the events of the next 24 game hours, nearest first (map timers, intrusions,
    /// contracts that close), as (hub or -1, ticks left, text, bad). Uses the hourly timeline, so it costs nothing extra.
    /// </summary>
    public static List<(int hub, int left, string text, bool bad)> Today()
    {
        var list = new List<(int, int, string, bool)>();
        NetMapManager net = NetMapManager.Get();
        if (net is { Ready: true })
            foreach (TimerEntry e in Timeline(net))
            {
                int left = Left(e);
                if (left <= GenDate.TicksPerDay)
                    list.Add((e.Hub, left, e.Text, e.Bad));
            }
        foreach (Contract c in ContractManager.Get()?.Active ?? [])
            if (c.Accepted && c.TicksLeft <= GenDate.TicksPerDay)
                list.Add((-1, c.TicksLeft, "NR_CoreTodayContract".Translate(c.Title), true));
        list.Sort((a, b) => a.Item2.CompareTo(b.Item2));
        return list;
    }

    /// <summary>A short "3h" / "now" for a time left.</summary>
    public static string ShortLeft(int ticks) => Short(ticks);

    /// <summary>Ticks left now (the list was built at the start of the hour).</summary>
    private static int Left(TimerEntry e) => e.Ticks <= 0 ? 0 : Mathf.Max(0, e.Ticks - (Find.TickManager.TicksGame - _timelineTick));

    private static string Short(int ticks) => ticks <= 0 ? (string)"NR_MapTimerNow".Translate()
        : ticks < GenDate.TicksPerDay ? (ticks / (float)GenDate.TicksPerHour).ToString("0") + "h" : (ticks / (float)GenDate.TicksPerDay).ToString("0.#") + "d";

    /// <summary>The "Next 3 days" section of the panel; a row centres its hub.</summary>
    private static float DrawTimeline(Rect inner, float y, NetMapManager net)
    {
        List<TimerEntry> list = Timeline(net);
        if (!list.Any(x => Left(x) > GenDate.TicksPerDay))
            return y;
        float top = y;
        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_MapNext3Days".Translate());
        Text.Font = GameFont.Tiny;
        // Rows like "Now in the net": a coloured bar (red danger, amber timer), the text, the time left on the right.
        foreach (TimerEntry e in list.Where(x => Left(x) > GenDate.TicksPerDay).Take(6))
            y = DrawInfoRow(inner, y, new NowRow { Text = e.Text, Right = Short(Left(e)), Color = e.Bad ? NetrunUI.Bad : NetrunUI.Warn, Hub = e.Hub, Tip = e.Text });
        Text.Font = GameFont.Small;
        GuideRegistry.InScroll("gateway.timeline", _panelOuter, _panelScroll, new Rect(inner.x, top, inner.width, y - top));
        return y + 6f;
    }

    private struct NowRow
    {
        public string Text, Right, Tip;
        public Color Color;
        public int Hub;
    }

    /// <summary>
    /// "Now in the net" at the top of the panel when nothing is picked (Maxim, 2026-10-14: a block of small text was
    /// hard to read). One row per thing: a coloured bar (red danger, amber timer, purple effect, the headline's own
    /// colour), the text, the time left on the right; a row with a hub centres the map on it. Events of the next day
    /// first, then the effects in force, then the last headlines. Rebuilt once a second.
    /// </summary>
    private static float DrawNow(Rect inner, float y)
    {
        List<NowRow> rows = NetrunUI.Cached("map.nowRows", () =>
        {
            var list = new List<NowRow>();
            NetMapManager net = NetMapManager.Get();
            if (net is { Ready: true })
                foreach (TimerEntry e in Timeline(net))
                {
                    int left = Left(e);
                    if (left > GenDate.TicksPerDay || list.Count >= 3)
                        continue;
                    list.Add(new NowRow { Text = e.Text, Right = Short(left), Color = e.Bad ? NetrunUI.Bad : NetrunUI.Warn, Hub = e.Hub, Tip = e.Text });
                }
            string effects = Effects();
            int cut = effects.IndexOf('\n');
            if (cut >= 0)
                foreach (string line in effects.Substring(cut + 1).Split('\n'))
                    // Only the effects themselves ("• ..."), not the "no active effects" line.
                    if (line.StartsWith("• "))
                        list.Add(new NowRow { Text = line.Substring(2).StripTags(), Color = NetrunUI.Purple, Hub = -1, Tip = line });
            GatewayManager g = GatewayManager.Get();
            if (g != null)
            {
                int now = Find.TickManager.TicksGame, shown = 0;
                foreach (NetHeadline h in g.RecentHeadlines)
                {
                    if (now - h.Tick > GenDate.TicksPerDay || shown++ >= 2)
                        break;
                    list.Add(new NowRow { Text = h.Text.StripTags(), Right = "NR_MapNowAgo".Translate(Short(now - h.Tick)), Color = h.Color, Hub = -1, Tip = h.Text });
                }
            }
            return list;
        }, 1f);
        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_MapNow".Translate());
        Text.Font = GameFont.Tiny;
        if (rows.Count == 0)
        {
            Widgets.Label(new Rect(inner.x + 4f, y, inner.width - 6f, 18f), NetrunUI.T("NR_MapNowQuiet").Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
            return y + 24f;
        }
        // The news reads in Small, fewer rows (UI audit 2026-10-23: Tiny was too small for the panel's main feed).
        Text.Font = GameFont.Small;
        foreach (NowRow row in rows)
            y = DrawInfoRow(inner, y, row);
        return y + 6f;
    }

    /// <summary>
    /// One panel row: a tinted strip with a coloured bar, the text and an optional value on the right (time left).
    /// A row with a hub centres the map on it. Used by "Now in the net", "Next 3 days" and the hub's own facts.
    /// </summary>
    private static float DrawInfoRow(Rect inner, float y, NowRow row)
    {
        float rightW = row.Right.NullOrEmpty() ? 0f : NetrunUI.Width(row.Right) + 8f;
        float textW = inner.width - 12f - rightW;
        float h = Mathf.Min(Text.CalcHeight(row.Text, textW), Text.Font == GameFont.Small ? 44f : 30f);
        var r = new Rect(inner.x, y, inner.width, Mathf.Max(20f, h + 4f));
        Widgets.DrawBoxSolid(r, new Color(row.Color.r, row.Color.g, row.Color.b, Mouse.IsOver(r) && row.Hub >= 0 ? 0.14f : 0.06f));
        Widgets.DrawBoxSolid(new Rect(r.x, r.y, 3f, r.height), row.Color);
        GUI.color = NetrunUI.TextColor;
        Widgets.Label(new Rect(r.x + 8f, r.y + 2f, textW, r.height - 2f), row.Text);
        if (rightW > 0f)
        {
            GUI.color = row.Color;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(r.xMax - rightW - 4f, r.y, rightW, r.height), row.Right);
            Text.Anchor = TextAnchor.UpperLeft;
        }
        GUI.color = Color.white;
        if (!row.Tip.NullOrEmpty())
            NetrunUI.Tip(r, row.Tip + (row.Hub >= 0 ? "\n\n" + NetrunUI.T("NR_MapNowClick") : ""));
        if (row.Hub >= 0 && Widgets.ButtonInvisible(r))
            FocusNode(row.Hub);
        return y + r.height + 3f;
    }

    private static readonly HashSet<long> PulseSeen = [];

    /// <summary>
    /// A hub where something is going on (a timer: patrol, storm, transit, outage, intrusion) breathes with a soft
    /// amber ring, instead of arrows pointing at it (Maxim, 2026-10-14). Uses the hourly timeline, nothing extra.
    /// </summary>
    private static void DrawEventPulse(Rect hubRect, MapNode n, NetMapManager net)
    {
        Timeline(net);
        if (!_nearestTimer.TryGetValue(n.Id, out TimerEntry e))
            return;
        // Once the hub is clicked the event is seen: no more waves for it (Maxim, 2026-10-23). Session-only, events are short.
        long seen = ((long)n.Id << 32) | (uint)e.Ticks;
        if (_selectedMapNode == n.Id)
        {
            if (PulseSeen.Count > 256)
                PulseSeen.Clear();
            PulseSeen.Add(seen);
        }
        if (PulseSeen.Contains(seen))
            return;
        Color c = e.Bad ? NetrunUI.Bad : NetrunUI.Warn;
        if (!NetrunMod.Settings.Animations)
        {
            GUI.color = new Color(c.r, c.g, c.b, 0.35f);
            GUI.DrawTexture(hubRect.ExpandedBy(6f), NetrunUI.ThinRing);
            GUI.color = Color.white;
            return;
        }
        // Two rings half a beat apart, each hub on its own phase so a busy district does not blink in step; slow, eased
        // out and faded in and out so the waves breathe rather than tick (Maxim, 2026-10-23).
        float t = Time.realtimeSinceStartup * 0.35f + n.Id * 0.137f;
        for (int i = 0; i < 2; i++)
        {
            float k = Mathf.Repeat(t + i * 0.5f, 1f);
            float grow = 1f - (1f - k) * (1f - k);
            GUI.color = new Color(c.r, c.g, c.b, 0.45f * Mathf.Sin(k * Mathf.PI));
            GUI.DrawTexture(hubRect.ExpandedBy(4f + 18f * grow * Mathf.Sqrt(_zoom)), NetrunUI.ThinRing);
        }
        GUI.color = Color.white;
    }

    /// <summary>A small timer tag next to a hub whose state ends soon (shown from zoom 0.8).</summary>
    private static void DrawTimerTag(Rect hubRect, MapNode n, NetMapManager net)
    {
        if (_zoom < 0.8f)
            return;
        Timeline(net);
        if (!_nearestTimer.TryGetValue(n.Id, out TimerEntry entry))
            return;
        int best = Mathf.Max(1, Left(entry));
        // The text and width change once a game hour (or a tenth of a day): cached per hub by that bucket.
        int bucket = best < GenDate.TicksPerDay ? best / GenDate.TicksPerHour : 1000 + best / (GenDate.TicksPerDay / 10);
        Text.Font = GameFont.Tiny;
        if (!_timerTags.TryGetValue(n.Id, out var tagText) || tagText.bucket != bucket)
        {
            string s = "◷ " + Short(best);
            _timerTags[n.Id] = tagText = (bucket, s, Text.CalcSize(s).x);
        }
        string text = tagText.text;
        float w = tagText.width + 8f;
        // Right of the hub, or right of its LOCKED / OPEN tag so the two never overlap.
        // Top right, past the hub's badge.
        var tag = new Rect(hubRect.xMax + 10f, hubRect.y - 16f, w, 14f);
        if (!PlateFree(tag))
        {
            Text.Font = GameFont.Small;
            return;
        }
        PlacedPlates.Add(tag);
        Widgets.DrawBoxSolid(tag, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.85f));
        GUI.color = new Color(NetrunUI.Warn.r, NetrunUI.Warn.g, NetrunUI.Warn.b, 0.7f);
        Widgets.DrawBox(tag);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = NetrunUI.Warn;
        Widgets.Label(tag, text);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
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
                Mathf.Clamp(at.y - 9f - dir.y * 22f, mapRect.y + 38f, mapRect.yMax - 20f), w, 18f);
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

    // Names already coloured (bright for interesting hubs, muted for the rest): Colorize per hub per event made a new
    // string for every visible name several times a frame (docs/PERF_AUDIT_4.md).
    private static readonly Dictionary<int, string> HubNames = [];
    private static readonly Dictionary<int, string> HubNamesMuted = [];
    private static CyberMap _hubNamesFor;

    private static string HubName(MapNode n, CyberMap map, bool interesting)
    {
        if (_hubNamesFor != map)
        {
            HubNames.Clear();
            HubNamesMuted.Clear();
            _hubNamesFor = map;
        }
        Dictionary<int, string> names = interesting ? HubNames : HubNamesMuted;
        if (!names.TryGetValue(n.Id, out string s))
            names[n.Id] = s = NetMapManager.NodeLabel(n).Colorize(interesting ? NetrunUI.TextColor : NetrunUI.Muted);
        return s;
    }

    // A plate's text width, measured once per text (not per frame); the texts are cached strings, so this stays small.
    private static readonly Dictionary<string, float> NameWidths = [];

    private static float NameWidth(string text)
    {
        // Big plates measure in their own font: keyed apart so the two never mix.
        string key = BigPlates ? "\u0001" + text : text;
        if (NameWidths.TryGetValue(key, out float w))
            return w;
        Text.Font = PlateFont;
        w = Text.CalcSize(text).x;
        if (NameWidths.Count > 400)
            NameWidths.Clear();
        NameWidths[key] = w;
        return w;
    }

    // Up close the plates read in Small on a taller row (Maxim, 2026-10-23: from 180 %).
    private static bool BigPlates => _zoom >= 1.8f;
    private static GameFont PlateFont => BigPlates ? GameFont.Small : GameFont.Tiny;
    private static float PlateRow => BigPlates ? 22f : 16f;

    // A long name breaks at its last space onto a second line (Maxim, 2026-10-23: "Entertainment quarter-36" made the
    // widest plates). Cached per text and font.
    private static readonly Dictionary<string, string> Wrapped = [];

    private static string WrapName(string text)
    {
        string key = BigPlates ? "\u0001" + text : text;
        if (Wrapped.TryGetValue(key, out string w))
            return w;
        int space = text.LastIndexOf(' ');
        w = space > 0 && NameWidth(text) > (BigPlates ? 150f : 110f) ? text.Substring(0, space) + "\n" + text.Substring(space + 1) : text;
        if (Wrapped.Count > 400)
            Wrapped.Clear();
        Wrapped[key] = w;
        return w;
    }

    private static readonly Dictionary<string, (string, string)> Lines = [];

    /// <summary>A wrapped name's two lines (the second null for one line), split once per text.</summary>
    private static (string, string) NameLines(string text)
    {
        if (Lines.TryGetValue(text, out var lines))
            return lines;
        int nl = text.IndexOf('\n');
        lines = nl < 0 ? (text, null) : (text.Substring(0, nl), text.Substring(nl + 1));
        if (Lines.Count > 400)
            Lines.Clear();
        Lines[text] = lines;
        return lines;
    }

    private static float NameRow(string text) => text != null && text.IndexOf('\n') >= 0 ? PlateRow + (BigPlates ? 18f : 12f) : PlateRow;

    private struct NamePlate
    {
        public Rect Tag, Hub;
        public MapNode Node;
        public string Text;
        public Color Edge;
        public int Priority;
        public bool Capital, Interesting, Named;
        public int States;
        public List<ExternalNode> Branches;
    }

    // Map v5 (variant B, Maxim 2026-10-22): up to three of a hub's nodes as chips at the plate's right end, "+N" for the rest.
    private const int PlateChips = 3, PlateStates = 3;
    private const float ChipSize = 8f, ChipStep = 11f, LockWidth = 13f, ChipRow = 12f;

    // The hub's states left of its name, most important first; three at most (Maxim, 2026-10-23: key, exit, patrol, trap
    // lead). A bit per state in NamePlate.States, in this order.
    private static readonly (string icon, Color tint)[] StateIcons =
    [
        ("Icon_Key", NetrunUI.Warn),
        ("Icon_ExitHub", Color.white),
        ("Event_HunterICE", NetrunUI.Bad),
        ("Event_Honeypot", NetrunUI.Warn),
        ("Icon_Lock", NetrunUI.Warn),
        ("Event_Rival", NetrunUI.Bad),
        ("Event_DataStorm", NetrunUI.Purple),
        ("Dive_Lightning_Grey", NetrunUI.Muted),
        ("Node_CorpLab", NetrunUI.Purple),
        ("Node_CorpCore", NetrunUI.Bad),
    ];

    private static int StateCount(int states)
    {
        int count = 0;
        for (; states != 0 && count < PlateStates; states &= states - 1)
            count++;
        return count;
    }
    private static readonly string[] PlusN = Enumerable.Range(0, 10).Select(i => ("+" + i).Colorize(NetrunUI.Muted)).ToArray();

    private static string MoreText(int count) => count <= 0 ? null : PlusN[Mathf.Min(count, 9)];

    // This frame's name plates and the places already taken (district plates first, then hub names by priority).
    private static readonly List<NamePlate> NameQueue = [];
    private static readonly List<Rect> PlacedPlates = [];

    /// <summary>
    /// Queues the hub's name plate (Maxim, 2026-10-14: a terminal tag under the hub, loot bars under it). The plates are
    /// drawn after all hubs by <see cref="FlushHubNames"/>, most important first, and one that would overlap a plate
    /// already drawn is left out (its name is still in the hover card). False when no plate is shown at this zoom.
    /// </summary>
    private static bool QueueHubName(Rect hubRect, MapNode n, NetMapManager net, List<ExternalNode> branches)
    {
        if (n.District == District.Home || _zoom < 0.8f || !net.Charted(n))
            return false;
        bool interesting = n.Fortress || n.Exit || net.Watch.Contains(n.Id) || n.Id == _selectedMapNode;
        if (!interesting)
            for (int i = 0; i < branches.Count; i++)
                if (branches[i].Contract && !branches[i].Hidden)
                {
                    interesting = true;
                    break;
                }
        // Up close every hub has its name; farther out only the interesting ones, the rest keep just their node chips.
        bool named = interesting || _zoom >= 1.3f;
        int states = (n.Exit ? 1 << 1 : 0) | (n.Patrol ? 1 << 2 : 0) | (n.Guarded && n.GuardLocked && NetrunMod.Settings.ProtectedHubs ? 1 << 4 : 0)
                     | (n.Storm ? 1 << 6 : 0) | (n.Offline ? 1 << 7 : 0);
        bool target = false;
        for (int i = 0; i < branches.Count; i++)
        {
            ExternalNode b = branches[i];
            if (b.Hidden)
                continue;
            target |= b.Contract;
            states |= (b.GuardFor >= 0 && NetrunMod.Settings.ProtectedHubs ? 1 : 0) | (b.Honeypot && b.HoneypotKnown ? 1 << 3 : 0) | (b.Rival ? 1 << 5 : 0)
                      | (b.Lab ? 1 << 8 : 0) | (b.IsBoss ? 1 << 9 : 0);
        }
        int icons = StateCount(states);
        if (!named && branches.Count == 0 && icons == 0)
            return false;
        bool capital = n.Fortress && net.CorpOfSlot(n.CorpSlot) != null;
        string text = !named ? null : capital ? NetrunUI.Upper(net.CorpOfSlot(n.CorpSlot).Name) : HubName(n, net.Map, interesting);
        if (text != null)
            text = WrapName(text);
        int chips = Mathf.Min(branches.Count, PlateChips);
        string more = MoreText(branches.Count - chips);
        // Named: states and name on the first row, the node chips centred on a second row (Maxim, 2026-10-23: one long
        // row made the plate too wide). Unnamed: one row of states and chips.
        float chipW = chips * ChipStep + (target ? 4f : 0f) + (more != null ? NameWidth(more) + 2f : 0f);
        (string first, string second) = named ? NameLines(text) : (null, null);
        float w = named ? Mathf.Max(Mathf.Max(NameWidth(first) + icons * LockWidth, second != null ? NameWidth(second) : 0f) + 16f, chipW + 10f)
            : 6f + icons * LockWidth + chipW;
        float h = named && branches.Count > 0 ? NameRow(text) + ChipRow : NameRow(text);
        NameQueue.Add(new NamePlate
        {
            Tag = new Rect(hubRect.center.x - w / 2f, hubRect.yMax + 7f * Mathf.Sqrt(_zoom), w, h),
            Hub = hubRect,
            Node = n,
            Text = text,
            Edge = HubColor(n, net),
            Priority = n.Id == _selectedMapNode ? 4 : capital ? 3 : interesting ? 2 : 1,
            Capital = capital,
            Interesting = interesting,
            Named = named,
            States = states,
            Branches = branches,
        });
        return true;
    }

    private static bool PlateFree(Rect r)
    {
        Rect padded = r.ExpandedBy(2f);
        for (int i = 0; i < PlacedPlates.Count; i++)
            if (PlacedPlates[i].Overlaps(padded))
                return false;
        return true;
    }

    /// <summary>Draws the queued hub names, most important first, skipping any that would cover a plate already drawn.</summary>
    private static void FlushHubNames()
    {
        NameQueue.Sort((x, y) => y.Priority.CompareTo(x.Priority));
        foreach (NamePlate plate in NameQueue)
        {
            // The loot bars hang under the plate: they need room too.
            Rect footprint = new(plate.Tag.x, plate.Tag.y, plate.Tag.width, plate.Tag.height + 5f);
            if (plate.Priority < 4 && !PlateFree(footprint))
                continue;
            PlacedPlates.Add(footprint);
            DrawHubPlate(plate);
            DrawLootDots(plate.Hub, plate.Node, plate.Tag);
        }
        NameQueue.Clear();
    }

    /// <summary>
    /// A hub's plate (map v5): the framed name tag (Maxim kept the frame, 2026-10-22) led by a lock when the hub is shut,
    /// then its node chips, important ones first (contract, key, lab, core). Without a name: only the chips, on a dark strip.
    /// </summary>
    private static void DrawHubPlate(NamePlate plate)
    {
        Rect tag = plate.Tag;
        bool dims = LegendDims(plate.Node.Id);
        float a = dims ? 0.3f : 1f;
        Color edge = plate.Edge;
        Widgets.DrawBoxSolid(tag, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, (plate.Named ? 0.82f : 0.6f) * a));
        if (plate.Named)
        {
            GUI.color = new Color(edge.r, edge.g, edge.b, (plate.Capital ? 0.8f : plate.Interesting ? 0.55f : 0.3f) * a);
            Widgets.DrawBox(tag);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(tag.x + 1f, tag.y + 3f, 2f, tag.height - 6f), new Color(edge.r, edge.g, edge.b, 0.9f * a));
        }
        List<ExternalNode> branches = plate.Branches;
        float row = plate.Named ? NameRow(plate.Text) : PlateRow;
        int icons = StateCount(plate.States);
        Text.Font = PlateFont;
        float x, cy = tag.y + PlateRow / 2f;
        if (plate.Named)
        {
            // First row: the states and the name's first line, centred together; a wrapped name's second line centred
            // on its own under it, the icons staying on the first (Maxim, 2026-10-23).
            (string first, string second) = NameLines(plate.Text);
            float tw = NameWidth(first);
            x = tag.center.x - (icons * LockWidth + tw) / 2f + 1f;
            x = DrawStates(x, cy, plate.States, a);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = plate.Capital ? new Color(edge.r, edge.g, edge.b, a) : new Color(1f, 1f, 1f, a);
            Widgets.Label(new Rect(x, tag.y, tw + 2f, PlateRow), first);
            if (second != null)
            {
                // Same anchor and colour as the first line, placed by its measured width: centred exactly.
                float sw = NameWidth(second);
                Widgets.Label(new Rect(tag.center.x - sw / 2f, tag.y + PlateRow - 4f, sw + 2f, row - PlateRow + 4f), second);
            }
            // Second row: the chips, centred under the name.
            if (branches.Count == 0)
            {
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                return;
            }
            int shown = Mathf.Min(branches.Count, PlateChips);
            bool hasTarget = false;
            for (int i = 0; i < branches.Count; i++)
                hasTarget |= !branches[i].Hidden && branches[i].Contract;
            string rest = MoreText(branches.Count - shown);
            float chipW = shown * ChipStep - (ChipStep - ChipSize) + (hasTarget ? 4f : 0f) + (rest != null ? NameWidth(rest) + 3f : 0f);
            x = tag.center.x - chipW / 2f;
            cy = tag.y + row + ChipRow / 2f - 2f;
        }
        else
            x = DrawStates(tag.x + 3f, cy, plate.States, a);
        // Chips: the contract target first (in an amber frame), then other important nodes, then the rest, three at most
        // (passes, no sorting allocation per frame).
        int drawn = 0;
        for (int pass = 0; pass < 3 && drawn < PlateChips; pass++)
            for (int i = 0; i < branches.Count && drawn < PlateChips; i++)
            {
                ExternalNode b = branches[i];
                bool target = !b.Hidden && b.Contract, important = !b.Hidden && ImportantColor(b) != null;
                if (pass == 0 ? !target : pass == 1 ? target || !important : important)
                    continue;
                if (target)
                    x += 2f;
                var chip = new Rect(x, cy - ChipSize / 2f, ChipSize, ChipSize);
                if (target)
                {
                    x += 2f;
                    GUI.color = new Color(NetrunUI.Warn.r, NetrunUI.Warn.g, NetrunUI.Warn.b, a);
                    Widgets.DrawBox(chip.ExpandedBy(2f));
                }
                DrawShape(chip, b, a);
                x += ChipStep;
                drawn++;
            }
        if (MoreText(branches.Count - drawn) is { } more)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(1f, 1f, 1f, a);
            Widgets.Label(new Rect(x + 1f, cy - 11f, NameWidth(more) + 2f, 22f), more);
        }
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }

    /// <summary>Up to three of the hub's states in priority order (<see cref="StateIcons"/>); returns the x after them.</summary>
    private static float DrawStates(float x, float cy, int states, float a)
    {
        int drawn = 0;
        for (int i = 0; i < StateIcons.Length && drawn < PlateStates; i++)
        {
            if ((states & (1 << i)) == 0)
                continue;
            drawn++;
            (string icon, Color tint) = StateIcons[i];
            if (NetrunTex.CyberIcon(icon) is { } tex)
            {
                GUI.color = new Color(tint.r, tint.g, tint.b, a);
                GUI.DrawTexture(new Rect(x, cy - 5.5f, 11f, 11f), tex);
            }
            x += LockWidth;
        }
        return x;
    }

    /// <summary>A terminal plate: a dark tag with a thin edge in the given colour and a marker tick on the left.</summary>
    private static void DrawPlate(Rect tag, string text, Color edge, bool dims, float edgeAlpha, bool colouredText, float alpha = 1f, GameFont font = GameFont.Tiny)
    {
        float a = (dims ? 0.3f : 1f) * alpha;
        Widgets.DrawBoxSolid(tag, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.82f * a));
        GUI.color = new Color(edge.r, edge.g, edge.b, edgeAlpha * a);
        Widgets.DrawBox(tag);
        GUI.color = Color.white;
        Widgets.DrawBoxSolid(new Rect(tag.x + 1f, tag.y + 3f, 2f, tag.height - 6f), new Color(edge.r, edge.g, edge.b, 0.9f * a));
        Text.Font = font;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = colouredText ? new Color(edge.r, edge.g, edge.b, a) : new Color(1f, 1f, 1f, a);
        Widgets.Label(new Rect(tag.x + 3f, tag.y, tag.width - 3f, tag.height), text);
        GUI.color = Color.white;
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
