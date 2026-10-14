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
                    if (left > GenDate.TicksPerDay || list.Count >= 4)
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
                    if (now - h.Tick > GenDate.TicksPerDay || shown++ >= 3)
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
        foreach (NowRow row in rows)
            y = DrawInfoRow(inner, y, row);
        Text.Font = GameFont.Small;
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
        float h = Mathf.Min(Text.CalcHeight(row.Text, textW), 30f);
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

    /// <summary>Time left on this hub's own timer (patrol, storm, outage...), or null.</summary>
    private static string HubTimer(NetMapManager net, int hub)
    {
        foreach (TimerEntry e in Timeline(net))
            if (e.Hub == hub && Left(e) > 0)
                return Short(Left(e));
        return null;
    }
    /// <summary>
    /// A hub where something is going on (a timer: patrol, storm, transit, outage, intrusion) breathes with a soft
    /// amber ring, instead of arrows pointing at it (Maxim, 2026-10-14). Uses the hourly timeline, nothing extra.
    /// </summary>
    private static void DrawEventPulse(Rect hubRect, MapNode n, NetMapManager net)
    {
        Timeline(net);
        if (!_nearestTimer.TryGetValue(n.Id, out TimerEntry e))
            return;
        Color c = e.Bad ? NetrunUI.Bad : NetrunUI.Warn;
        if (!NetrunMod.Settings.Animations)
        {
            GUI.color = new Color(c.r, c.g, c.b, 0.35f);
            GUI.DrawTexture(hubRect.ExpandedBy(6f), NetrunUI.ThinRing);
            GUI.color = Color.white;
            return;
        }
        // Two rings half a beat apart, each hub on its own phase so a busy district does not blink in step.
        float t = Time.realtimeSinceStartup * 0.6f + n.Id * 0.137f;
        for (int i = 0; i < 2; i++)
        {
            float k = Mathf.Repeat(t + i * 0.5f, 1f);
            GUI.color = new Color(c.r, c.g, c.b, 0.55f * (1f - k));
            GUI.DrawTexture(hubRect.ExpandedBy(4f + 18f * k * Mathf.Sqrt(_zoom)), NetrunUI.ThinRing);
        }
        GUI.color = Color.white;
    }

    /// <summary>A small timer tag next to a hub whose state ends soon (shown from zoom 0.8).</summary>
    private static void DrawTimerTag(Rect hubRect, MapNode n, NetMapManager net, Rect guardTag)
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
        var tag = guardTag.width > 0f ? new Rect(guardTag.xMax + 3f, guardTag.y + 0.5f, w, 14f) : new Rect(hubRect.xMax + 4f, hubRect.y - 16f, w, 14f);
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
        if (NameWidths.TryGetValue(text, out float w))
            return w;
        Text.Font = GameFont.Tiny;
        w = Text.CalcSize(text).x;
        if (NameWidths.Count > 400)
            NameWidths.Clear();
        NameWidths[text] = w;
        return w;
    }

    private struct NamePlate
    {
        public Rect Tag, Hub;
        public MapNode Node;
        public string Text;
        public Color Edge;
        public int Priority;
        public bool Capital, Interesting;
    }

    // This frame's name plates and the places already taken (district plates first, then hub names by priority).
    private static readonly List<NamePlate> NameQueue = [];
    private static readonly List<Rect> PlacedPlates = [];

    /// <summary>
    /// Queues the hub's name plate (Maxim, 2026-10-14: a terminal tag under the hub, loot bars under it). The plates are
    /// drawn after all hubs by <see cref="FlushHubNames"/>, most important first, and one that would overlap a plate
    /// already drawn is left out (its name is still in the hover card). False when the name is not shown at this zoom.
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
        if (!interesting && _zoom < 1.3f)
            return false;
        bool capital = n.Fortress && net.CorpOfSlot(n.CorpSlot) != null;
        string text = capital ? NetrunUI.Upper(net.CorpOfSlot(n.CorpSlot).Name) : HubName(n, net.Map, interesting);
        float w = NameWidth(text) + 14f;
        NameQueue.Add(new NamePlate
        {
            Tag = new Rect(hubRect.center.x - w / 2f, hubRect.yMax + 7f * Mathf.Sqrt(_zoom), w, 16f),
            Hub = hubRect,
            Node = n,
            Text = text,
            Edge = HubColor(n, net),
            Priority = n.Id == _selectedMapNode ? 4 : capital ? 3 : interesting ? 2 : 1,
            Capital = capital,
            Interesting = interesting,
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
            DrawPlate(plate.Tag, plate.Text, plate.Edge, LegendDims(plate.Node.Id), plate.Capital ? 0.8f : plate.Interesting ? 0.55f : 0.3f, plate.Capital);
            DrawLootDots(plate.Hub, plate.Node, plate.Tag);
        }
        NameQueue.Clear();
    }

    /// <summary>A terminal plate: a dark tag with a thin edge in the given colour and a marker tick on the left.</summary>
    private static void DrawPlate(Rect tag, string text, Color edge, bool dims, float edgeAlpha, bool colouredText)
    {
        float a = dims ? 0.3f : 1f;
        Widgets.DrawBoxSolid(tag, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.82f * a));
        GUI.color = new Color(edge.r, edge.g, edge.b, edgeAlpha * a);
        Widgets.DrawBox(tag);
        GUI.color = Color.white;
        Widgets.DrawBoxSolid(new Rect(tag.x + 1f, tag.y + 3f, 2f, tag.height - 6f), new Color(edge.r, edge.g, edge.b, 0.9f * a));
        Text.Font = GameFont.Tiny;
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
