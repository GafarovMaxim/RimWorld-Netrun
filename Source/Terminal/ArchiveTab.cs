using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Netrun;

/// <summary>
/// The Archive (docs/CONCEPTS_6.md, section 1): three tabs (trace journal, dive reports, raids), a 7-day Heat inset that
/// filters the journal by day, filter chips and a search, lines grouped by day with the day's total, and a details panel.
/// </summary>
public static class ArchiveTab
{
    public enum Tab
    {
        Journal,
        Reports,
        Raids,
    }

    private enum Filter
    {
        All,
        Hack,
        Dive,
        Scrub,
        AI,
        Events,
    }

    private const float RowHeight = 48f;
    private const float DayHeight = 30f;
    private const float InsetHeight = 70f;

    private static Tab _tab;
    private static Filter _filter;
    private static string _search = string.Empty;
    private static int _day = -1;
    private static HeatLogEntry _entry;
    private static DiveReport _report;
    private static Vector2 _scroll;
    private static float _openedAt = -10f;
    private static float _flashAt = -10f;
    private static float _tabLineX = -1f, _tabLineW;

    private static readonly HashSet<HeatLogEntry> Fresh = [];

    /// <summary>Lines that came in since the Archive was last open: they glow until the mouse passes over them.</summary>
    public static void MarkFresh(IEnumerable<HeatLogEntry> entries)
    {
        foreach (HeatLogEntry e in entries)
            Fresh.Add(e);
    }

    /// <summary>Open the Archive on a tab; on the reports tab the newest report is picked.</summary>
    public static void Open(Tab tab)
    {
        SwitchTab(tab);
        if (tab == Tab.Reports)
            _report = GatewayManager.Get()?.Reports.LastOrDefault();
    }

    private static void SwitchTab(Tab tab)
    {
        _tab = tab;
        _entry = null;
        _report = null;
        _scroll = Vector2.zero;
        _openedAt = Time.realtimeSinceStartup;
    }

    public static void Draw(Rect rect, Map map, HeatTracker tracker, System.Action<string> openHelp, System.Action openHeat)
    {
        GuideRegistry.Area("archive.tabs", new Rect(rect.x, rect.y, rect.width * 0.6f, 32f));
        DrawTabs(new Rect(rect.x, rect.y, rect.width, 32f), tracker);
        rect.yMin += 42f;

        float detailWidth = Mathf.Min(330f, rect.width * 0.36f);
        var left = new Rect(rect.x, rect.y, rect.width - detailWidth - 12f, rect.height);
        var right = new Rect(left.xMax + 12f, rect.y, detailWidth, rect.height);

        float y = left.y;
        GuideRegistry.Area("archive.filters", new Rect(left.x, y, left.width, 26f));
        y = DrawFilters(new Rect(left.x, y, left.width, 26f)) + 8f;
        if (_tab == Tab.Journal)
        {
            GuideRegistry.Area("archive.inset", new Rect(left.x, y, left.width, InsetHeight));
            DrawInset(new Rect(left.x, y, left.width, InsetHeight), map, tracker);
            y += InsetHeight + 8f;
        }
        var list = new Rect(left.x, y, left.width, left.yMax - y);
        GuideRegistry.Area("archive.list", list);
        GuideRegistry.Area("archive.details", right);
        if (_tab == Tab.Reports)
            DrawReports(list);
        else
            DrawEntries(list, map, tracker);

        DrawDetails(right, map, openHelp, openHeat);
    }

    // ---- Tabs: a sliding line under the active one ----

    private static void DrawTabs(Rect rect, HeatTracker tracker)
    {
        int reports = GatewayManager.Get()?.Reports.Count ?? 0;
        int raids = tracker.Log.Count(e => e.Source == HeatSource.Raid);
        float x = rect.x;
        float activeX = 0f, activeW = 0f;
        foreach (Tab tab in System.Enum.GetValues(typeof(Tab)))
        {
            string label = ("NR_ArchiveTab_" + tab).Translate();
            int count = tab == Tab.Reports ? reports : tab == Tab.Raids ? raids : tracker.Log.Count;
            string countText = count.ToString();
            float w = Text.CalcSize(label).x + Text.CalcSize(countText).x + 34f;
            var r = new Rect(x, rect.y, w, rect.height);
            bool active = _tab == tab;
            float lit = NetrunUI.HoverFade(r);
            NetrunUI.Box(r, active ? NetrunUI.BgCardAlt : Color.Lerp(NetrunUI.BgCard, NetrunUI.BgCardAlt, lit),
                active ? new Color(1f, 1f, 1f, 0.5f) : new Color(1f, 1f, 1f, 0.12f + 0.2f * lit));
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(r.x + 12f, r.y, w, r.height), active ? label : label.Colorize(NetrunUI.Muted));
            Widgets.Label(new Rect(r.xMax - Text.CalcSize(countText).x - 10f, r.y, 40f, r.height), countText.Colorize(count > 0 ? NetrunUI.Accent : NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            if (active)
            {
                activeX = r.x;
                activeW = r.width;
            }
            if (Widgets.ButtonInvisible(r) && !active)
            {
                NetrunSound.Click();
                SwitchTab(tab);
            }
            x += w + 6f;
        }
        // The line glides to the active tab.
        if (_tabLineX < 0f || !NetrunMod.Settings.Animations)
        {
            _tabLineX = activeX;
            _tabLineW = activeW;
        }
        else
        {
            float k = Mathf.Clamp01(Time.unscaledDeltaTime * 14f);
            _tabLineX = Mathf.Lerp(_tabLineX, activeX, k);
            _tabLineW = Mathf.Lerp(_tabLineW, activeW, k);
        }
        Widgets.DrawBoxSolid(new Rect(_tabLineX, rect.yMax - 2f, _tabLineW, 2f), NetrunUI.Accent);
        GUI.color = new Color(1f, 1f, 1f, 0.12f);
        Widgets.DrawLineHorizontal(rect.x, rect.yMax + 4f, rect.width);
        GUI.color = Color.white;
    }

    // ---- Filter chips and the search ----

    private static float DrawFilters(Rect rect)
    {
        float x = rect.x;
        if (_tab == Tab.Journal)
        {
            foreach (Filter f in System.Enum.GetValues(typeof(Filter)))
            {
                string label = ("NR_ArchiveFilter_" + f).Translate();
                float w = Text.CalcSize(label).x + 20f;
                var r = new Rect(x, rect.y, w, rect.height);
                bool on = _filter == f;
                float lit = NetrunUI.HoverFade(r, 0.1f);
                NetrunUI.Box(r, on ? new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.15f) : NetrunUI.BgCard,
                    on ? NetrunUI.Accent : new Color(1f, 1f, 1f, 0.15f + 0.25f * lit));
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(r, on ? label.Colorize(NetrunUI.Accent) : label.Colorize(NetrunUI.Muted));
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonInvisible(r))
                {
                    NetrunSound.Click();
                    _filter = f;
                    _openedAt = Time.realtimeSinceStartup;
                }
                x += w + 6f;
            }
        }
        if (_day >= 0 && _tab == Tab.Journal)
        {
            string day = "NR_ArchiveDayChip".Translate(DayName(_day));
            float w = Text.CalcSize(day).x + 20f;
            if (x + w < rect.xMax - 180f)
            {
                var r = new Rect(x + 6f, rect.y, w, rect.height);
                NetrunUI.Box(r, new Color(NetrunUI.Warn.r, NetrunUI.Warn.g, NetrunUI.Warn.b, 0.12f), NetrunUI.Warn);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(r, day.Colorize(NetrunUI.Warn));
                Text.Anchor = TextAnchor.UpperLeft;
                NetrunUI.Tip(r, "NR_ArchiveDayChipTip".Translate());
                if (Widgets.ButtonInvisible(r))
                    _day = -1;
            }
        }

        float sw = Mathf.Min(170f, rect.width * 0.3f);
        var search = new Rect(rect.xMax - sw, rect.y, sw, rect.height);
        _search = Widgets.TextField(search, _search);
        if (_search.NullOrEmpty() && GUI.GetNameOfFocusedControl().NullOrEmpty())
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(search.x + 8f, search.y, search.width - 10f, search.height), ("⌕ " + "NR_ArchiveSearch".Translate()).Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
        }
        return rect.yMax;
    }

    private static bool Matches(string text) => _search.NullOrEmpty() || (text ?? string.Empty).IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool Passes(HeatLogEntry e) => _filter switch
    {
        Filter.Hack => e.Source == HeatSource.Hack,
        Filter.Dive => e.Source == HeatSource.Dive,
        Filter.Scrub => e.Source == HeatSource.Scrub,
        Filter.AI => e.Source == HeatSource.AI,
        Filter.Events => e.Source is HeatSource.Event or HeatSource.Raid or HeatSource.Other,
        _ => true,
    };

    // ---- Days: calendar days at the colony's longitude ----

    private static int AbsTick(int gameTick) => gameTick + (Find.TickManager.TicksAbs - Find.TickManager.TicksGame);

    private static float Longitude(Map map) => map != null ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;

    private static int DayKey(int gameTick, float longitude)
    {
        long abs = AbsTick(gameTick);
        return GenDate.Year(abs, longitude) * GenDate.DaysPerYear + GenDate.DayOfYear(abs, longitude);
    }

    private static int DaysAgo(int gameTick, Map map)
    {
        float lon = Longitude(map);
        return DayKey(Find.TickManager.TicksGame, lon) - DayKey(gameTick, lon);
    }

    private static string DayName(int daysAgo) => daysAgo switch
    {
        0 => "NR_ArchiveToday".Translate(),
        1 => "NR_ArchiveYesterday".Translate(),
        _ => "NR_ArchiveDaysAgo".Translate(daysAgo),
    };

    private static string Date(int gameTick, Map map) => GenDate.DateFullStringAt(AbsTick(gameTick), Find.WorldGrid.LongLatOf(map?.Tile ?? 0));

    private static string Clock(int gameTick, Map map)
    {
        float hour = GenDate.HourFloat(AbsTick(gameTick), Longitude(map));
        int h = Mathf.FloorToInt(hour);
        int m = Mathf.FloorToInt((hour - h) * 60f);
        return $"{h:00}:{m:00}";
    }

    // ---- Heat inset: 7 days, a click picks a day, the wheel steps ----

    private static void DrawInset(Rect rect, Map map, HeatTracker tracker)
    {
        NetrunUI.Box(rect, NetrunUI.BgWindow, new Color(1f, 1f, 1f, 0.12f));
        int now = Find.TickManager.TicksGame;
        float hourNow = GenDate.HourFloat(AbsTick(now), Longitude(map));
        int todayStart = now - Mathf.RoundToInt(hourNow * GenDate.TicksPerHour);
        const int span = 7 * GenDate.TicksPerDay;
        int from = todayStart - 6 * GenDate.TicksPerDay;
        float X(int tick) => rect.x + (tick - from) / (float)span * rect.width;
        float Y(float heat) => rect.yMax - 6f - Mathf.Clamp01(heat / HeatTracker.MaxHeat) * (rect.height - 22f);

        // Day columns: the picked one lit, hover lighter; a click picks, again clears.
        for (int d = 0; d < 7; d++)
        {
            int start = todayStart - d * GenDate.TicksPerDay;
            var col = new Rect(X(start), rect.y + 1f, rect.width / 7f, rect.height - 2f);
            if (_day == d)
            {
                Widgets.DrawBoxSolid(col, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.15f));
                GUI.color = NetrunUI.Accent;
                Widgets.DrawBox(col);
                GUI.color = Color.white;
            }
            else if (Mouse.IsOver(col))
            {
                Widgets.DrawBoxSolid(col, new Color(1f, 1f, 1f, 0.05f));
            }
            if (d > 0)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.06f);
                Widgets.DrawLineVertical(col.xMax, rect.y, rect.height);
                GUI.color = Color.white;
            }
            NetrunUI.Tip(col, DayName(d) + ": " + Date(start + GenDate.TicksPerHour, map));
            if (Widgets.ButtonInvisible(col))
            {
                _day = _day == d ? -1 : d;
                _openedAt = Time.realtimeSinceStartup;
            }
        }
        if (Mouse.IsOver(rect) && Event.current.type == EventType.ScrollWheel)
        {
            _day = Mathf.Clamp(_day + (Event.current.delta.y > 0f ? -1 : 1), -1, 6);
            Event.current.Use();
        }

        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 16f), "NR_ArchiveInset".Translate().Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;

        // The line draws itself left to right when the tab opens.
        float reveal = NetrunMod.Settings.Animations ? Mathf.Clamp01((Time.realtimeSinceStartup - _openedAt) / 0.5f) : 1f;
        IReadOnlyList<float> history = tracker.History;
        int n = history.Count;
        Vector2? last = null;
        for (int i = 0; i < n; i++)
        {
            int tick = now - (n - 1 - i) * HeatTracker.HistoryInterval;
            if (tick < from)
                continue;
            var p = new Vector2(X(tick), Y(history[i]));
            if (p.x > rect.x + rect.width * reveal)
                break;
            if (last != null)
                Widgets.DrawLine(last.Value, p, NetrunUI.Bad, 2f);
            last = p;
        }
        if (last != null && reveal >= 1f)
            Widgets.DrawLine(last.Value, new Vector2(X(now), Y(tracker.Heat)), NetrunUI.Bad, 2f);

        // Event dots: grow on hover, a click opens the event.
        foreach (HeatLogEntry e in tracker.Log)
        {
            if (e.Tick < from || Mathf.Abs(e.Amount) < 0.05f || e.After < 0f)
                continue;
            var p = new Vector2(X(e.Tick), Y(e.After));
            if (p.x > rect.x + rect.width * reveal)
                continue;
            var hit = new Rect(p.x - 6f, p.y - 6f, 12f, 12f);
            float size = Mouse.IsOver(hit) ? 10f : _entry == e ? 9f : 6f;
            GUI.color = e.Amount > 0f ? NetrunUI.Bad : NetrunUI.Ok;
            GUI.DrawTexture(new Rect(p.x - size / 2f, p.y - size / 2f, size, size), NetrunUI.Disc);
            GUI.color = Color.white;
            NetrunUI.Tip(hit, e.Text + "\n" + $"{e.Amount:+0.#;-0.#}".Colorize(e.Amount > 0f ? NetrunUI.Bad : NetrunUI.Ok));
            if (Widgets.ButtonInvisible(hit))
                Select(e);
        }
    }

    private static void Select(HeatLogEntry e)
    {
        NetrunSound.Click();
        _entry = e;
        _report = null;
        _flashAt = Time.realtimeSinceStartup;
    }

    // ---- Journal and raids: grouped by day ----

    private static void DrawEntries(Rect rect, Map map, HeatTracker tracker)
    {
        string key = $"archive.entries.{_tab}.{_filter}.{_day}.{_search}.{tracker.Log.Count}.{(tracker.Log.Count > 0 ? tracker.Log[tracker.Log.Count - 1].Tick : 0)}";
        (List<HeatLogEntry> entries, List<IGrouping<int, HeatLogEntry>> groups) = NetrunUI.Cached(key, () =>
        {
            List<HeatLogEntry> list = tracker.Log
                .Where(e => _tab == Tab.Raids ? e.Source == HeatSource.Raid : Passes(e))
                .Where(e => Matches(e.Text))
                .Where(e => _tab != Tab.Journal || _day < 0 || DaysAgo(e.Tick, map) == _day)
                .Reverse().ToList();
            return (list, list.GroupBy(e => DaysAgo(e.Tick, map)).ToList());
        }, 2f);
        if (entries.Count == 0)
        {
            Empty(rect, (_tab == Tab.Raids ? "NR_ArchiveNoRaids" : "NR_ArchiveEmpty").Translate());
            return;
        }

        float height = groups.Count * DayHeight + entries.Count * (RowHeight + 4f);
        var view = new Rect(0f, 0f, rect.width - 16f, height);
        NetrunUI.BeginScroll(rect, ref _scroll, view);
        float y = 0f;
        int index = 0;
        foreach (var group in groups)
        {
            DayHeader(new Rect(0f, y, view.width, DayHeight), group.Key, group.First().Tick, group.Sum(e => e.Amount), map);
            y += DayHeight;
            foreach (HeatLogEntry e in group)
            {
                if (y + RowHeight >= _scroll.y && y <= _scroll.y + rect.height)
                    EntryRow(new Rect(0f, y, view.width, RowHeight), e, map, index);
                y += RowHeight + 4f;
                index++;
            }
        }
        Widgets.EndScrollView();
    }

    private static void DayHeader(Rect r, int daysAgo, int tick, float sum, Map map)
    {
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        string label = (daysAgo <= 1 ? DayName(daysAgo).ToUpper() + " · " : string.Empty) + Date(tick, map);
        Widgets.Label(new Rect(r.x, r.y, r.width - 160f, r.height), label.Colorize(NetrunUI.Muted));
        if (Mathf.Abs(sum) >= 0.05f)
        {
            Text.Anchor = TextAnchor.MiddleRight;
            string total = sum > 0f
                ? "NR_ArchiveDaySum".Translate($"+{sum:0.#}").Colorize(NetrunUI.Bad)
                : "NR_ArchiveDaySum".Translate($"{sum:0.#}").Colorize(NetrunUI.Ok);
            Widgets.Label(new Rect(r.xMax - 200f, r.y, 200f, r.height), total);
            if ((sum > 0f ? NetrunTex.HeatRise : NetrunTex.HeatReduction) is { } dayIcon)
                GUI.DrawTexture(new Rect(r.xMax - Text.CalcSize(total.StripTags()).x - 22f, r.center.y - 9f, 18f, 18f), dayIcon);
        }
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        GUI.color = new Color(1f, 1f, 1f, 0.1f);
        Widgets.DrawLineHorizontal(r.x, r.yMax - 4f, r.width);
        GUI.color = Color.white;
    }

    private static Color SourceColor(HeatSource s) => s switch
    {
        HeatSource.Hack => NetrunUI.Bad,
        HeatSource.Dive => NetrunUI.Warn,
        HeatSource.Scrub => NetrunUI.Ok,
        HeatSource.AI => NetrunUI.Purple,
        HeatSource.Raid => NetrunUI.Bad,
        HeatSource.Event => NetrunUI.Accent,
        _ => NetrunUI.Muted,
    };

    private static Texture2D SourceIcon(HeatSource s, HeatTracker tracker) => s switch
    {
        HeatSource.Hack => NetrunTex.CyberIcon("Action_Hack"),
        HeatSource.Dive => NetrunTex.CyberIcon("Dive_GATE") ?? NetrunTex.Cyberspace,
        HeatSource.Scrub => NetrunTex.TabIcon(1),
        HeatSource.AI => NetrunTex.CyberIcon("Icon_AIRequest"),
        HeatSource.Raid => NetrunTex.CyberIcon("Event_CorpHunt"),
        HeatSource.Event => NetrunTex.CyberIcon("Event_MapEvents") ?? NetrunTex.EventIcon("DataCourier"),
        _ => NetrunTex.CyberIcon("Dive_Heat") ?? NetrunTex.HeatIcon(tracker?.Level ?? HeatLevel.Shadow),
    };

    /// <summary>Rows come in as a cascade: each slides in a little after the one above.</summary>
    private static float Cascade(int index)
    {
        if (!NetrunMod.Settings.Animations)
            return 1f;
        float t = Mathf.Clamp01((Time.realtimeSinceStartup - _openedAt - index * 0.03f) / 0.2f);
        return 1f - (1f - t) * (1f - t);
    }

    private static void EntryRow(Rect r, HeatLogEntry e, Map map, int index)
    {
        float k = Cascade(index);
        r.x += 14f * (1f - k);
        Color c = SourceColor(e.Source);
        bool selected = _entry == e;
        float lit = NetrunUI.HoverFade(r, 0.12f);
        Widgets.DrawBoxSolid(r, Color.Lerp(NetrunUI.BgCard, NetrunUI.BgCardAlt, lit));
        Widgets.DrawBoxSolid(new Rect(r.x, r.y, 3f, r.height), c);
        if (selected)
        {
            GUI.color = NetrunUI.Accent;
            Widgets.DrawBox(r);
            GUI.color = Color.white;
            float flash = Time.realtimeSinceStartup - _flashAt;
            if (NetrunMod.Settings.Animations && flash < 0.35f)
                Widgets.DrawBoxSolid(r, new Color(c.r, c.g, c.b, 0.35f * (1f - flash / 0.35f)));
        }

        if (Fresh.Contains(e))
        {
            float p = NetrunMod.Settings.Animations ? 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 2.5f) : 1f;
            Widgets.DrawBoxSolid(r, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.06f + 0.06f * p));
            Widgets.DrawBoxSolid(new Rect(r.xMax - 4f, r.y, 4f, r.height), NetrunUI.Accent);
            if (Mouse.IsOver(r))
                Fresh.Remove(e);
        }
        var iconBox = new Rect(r.x + 12f, r.y + 9f, 30f, 30f);
        NetrunUI.Box(iconBox, NetrunUI.BgWindow, c);
        // Lines that are about Heat itself show which way it went.
        Texture2D heatIcon = e.Level >= 0 ? NetrunTex.HeatIcon((HeatLevel)e.Level) : e.Source is HeatSource.Other or HeatSource.Event or HeatSource.Raid && e.Amount > 0.05f ? NetrunTex.HeatRise
            : e.Source == HeatSource.Other && e.Amount < -0.05f ? NetrunTex.HeatReduction : null;
        if ((heatIcon ?? SourceIcon(e.Source, HeatTracker.Get())) is { } icon)
            GUI.DrawTexture(iconBox.ContractedBy(4f), icon);

        string amount = e.Amount > 0.05f ? $"+{e.Amount:0.#}" : e.Amount < -0.05f ? $"{e.Amount:0.#}" : "0";
        Color ac = e.Amount > 0.05f ? NetrunUI.Bad : e.Amount < -0.05f ? NetrunUI.Ok : NetrunUI.Muted;
        float textX = iconBox.xMax + 10f;
        float textW = r.xMax - textX - 70f;
        Widgets.Label(new Rect(textX, r.y + 5f, textW, 22f), e.Text.Truncate(textW));
        Text.Font = GameFont.Tiny;
        string sub = ("NR_ArchiveType_" + e.Source).Translate() + " · " + Clock(e.Tick, map) + " · " + "NR_TimeAgo".Translate((Find.TickManager.TicksGame - e.Tick).ToStringTicksToPeriod());
        Widgets.Label(new Rect(textX, r.y + 26f, textW, 18f), sub.Truncate(textW).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(new Rect(r.xMax - 70f, r.y, 60f, r.height), amount.Colorize(ac));
        Text.Anchor = TextAnchor.UpperLeft;

        NetrunUI.Tip(r, e.Text);
        if (Widgets.ButtonInvisible(r))
            Select(e);
    }

    // ---- Dive reports ----

    private static string Outcome(DiveReport r) => (r.Success ? "NR_ReportSuccess" : r.Retreated ? "NR_ReportRetreat" : "NR_ReportFail").Translate();

    private static Color OutcomeColor(DiveReport r) => r.Success ? NetrunUI.Ok : r.Retreated ? NetrunUI.Warn : NetrunUI.Bad;

    private static string Loot(DiveReport r)
    {
        var parts = new List<string>();
        if (r.Cyberdata > 0)
            parts.Add($"{NetrunDefOf.USH_Cyberdata.label} ×{r.Cyberdata}");
        if (r.Advanced > 0)
            parts.Add($"{NetrunDefOf.USH_AdvancedCyberdata.label} ×{r.Advanced}");
        if (r.Silver > 0)
            parts.Add($"{ThingDefOf.Silver.label} {r.Silver}");
        if (r.Items.Count > 0)
            parts.Add(string.Join(", ", r.Items.Where(d => d != null).Select(d => d.label)));
        return parts.Count > 0 ? string.Join(", ", parts) : (string)"NR_ArchiveLootNone".Translate();
    }

    private static void DrawReports(Rect rect)
    {
        List<DiveReport> reports = (GatewayManager.Get()?.Reports ?? []).AsEnumerable().Reverse().Where(r => Matches(r.NodeName) || Matches(r.RunnerName)).ToList();
        if (reports.Count == 0)
        {
            Empty(rect, "NR_ArchiveNoReports".Translate());
            return;
        }
        var view = new Rect(0f, 0f, rect.width - 16f, reports.Count * (RowHeight + 4f));
        NetrunUI.BeginScroll(rect, ref _scroll, view);
        float y = 0f;
        for (int i = 0; i < reports.Count; i++)
        {
            ReportRow(new Rect(0f, y, view.width, RowHeight), reports[i], i);
            y += RowHeight + 4f;
        }
        Widgets.EndScrollView();
    }

    private static void ReportRow(Rect r, DiveReport rep, int index)
    {
        float k = Cascade(index);
        r.x += 14f * (1f - k);
        Color c = OutcomeColor(rep);
        float lit = NetrunUI.HoverFade(r, 0.12f);
        Widgets.DrawBoxSolid(r, Color.Lerp(NetrunUI.BgCard, NetrunUI.BgCardAlt, lit));
        Widgets.DrawBoxSolid(new Rect(r.x, r.y, 3f, r.height), c);
        if (_report == rep)
        {
            GUI.color = NetrunUI.Accent;
            Widgets.DrawBox(r);
            GUI.color = Color.white;
            float flash = Time.realtimeSinceStartup - _flashAt;
            if (NetrunMod.Settings.Animations && flash < 0.35f)
                Widgets.DrawBoxSolid(r, new Color(c.r, c.g, c.b, 0.35f * (1f - flash / 0.35f)));
        }
        var iconBox = new Rect(r.x + 12f, r.y + 9f, 30f, 30f);
        NetrunUI.Box(iconBox, NetrunUI.BgWindow, c);
        if (((rep.Success ? NetrunTex.ReportOk : NetrunTex.ReportFail) ?? NetrunTex.DiveReport) is { } icon)
            GUI.DrawTexture(iconBox.ContractedBy(2f), icon);

        float heat = rep.HeatAfter - rep.HeatBefore;
        float textX = iconBox.xMax + 10f;
        float textW = r.xMax - textX - 70f;
        Widgets.Label(new Rect(textX, r.y + 5f, textW, 22f), rep.NodeName.Truncate(textW));
        Text.Font = GameFont.Tiny;
        string ago = rep.EndTick > 0 ? " · " + "NR_TimeAgo".Translate((Find.TickManager.TicksGame - rep.EndTick).ToStringTicksToPeriod()) : string.Empty;
        string sub = Outcome(rep).Colorize(c) + " · " + (rep.RunnerName ?? "?") + " · " + "NR_ArchiveTurnsShort".Translate(rep.Turns) + ago;
        Widgets.Label(new Rect(textX, r.y + 26f, textW, 18f), sub.Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleRight;
        string heatText = heat > 0.05f ? $"+{heat:0.#}" : heat < -0.05f ? $"{heat:0.#}" : "0";
        Widgets.Label(new Rect(r.xMax - 70f, r.y, 60f, r.height), heatText.Colorize(heat > 0.05f ? NetrunUI.Bad : heat < -0.05f ? NetrunUI.Ok : NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
        if (Widgets.ButtonInvisible(r))
        {
            NetrunSound.Click();
            _report = rep;
            _entry = null;
            _flashAt = Time.realtimeSinceStartup;
        }
    }

    /// <summary>The dive report a journal line belongs to: the one that ended closest to it, within an hour.</summary>
    private static DiveReport ReportFor(HeatLogEntry e)
    {
        if (e == null || e.Source != HeatSource.Dive)
            return null;
        return GatewayManager.Get()?.Reports.Where(r => r.EndTick > 0 && Mathf.Abs(r.EndTick - e.Tick) <= GenDate.TicksPerHour)
            .OrderBy(r => Mathf.Abs(r.EndTick - e.Tick)).FirstOrDefault();
    }

    // ---- Details on the right ----

    private static string ArticleFor(HeatSource s) => s switch
    {
        HeatSource.Hack => "NR_Help_Start_Hacking",
        HeatSource.Dive => "NR_Help_Gateway_Dive",
        HeatSource.Scrub => "NR_Help_Fixers_Scrub",
        HeatSource.AI => "NR_Help_AI",
        HeatSource.Raid => "NR_Help_Events_Attacks",
        HeatSource.Event => "NR_Help_Events_Map",
        _ => "NR_Help_Heat",
    };

    private static void DrawDetails(Rect rect, Map map, System.Action<string> openHelp, System.Action openHeat)
    {
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(1f, 1f, 1f, 0.12f));
        Rect inner = rect.ContractedBy(12f);
        DiveReport report = _report ?? ReportFor(_entry);
        if (_entry == null && report == null)
        {
            Empty(inner, "NR_ArchivePick".Translate());
            return;
        }

        float y = NetrunUI.Section(new Rect(inner.x, inner.y, inner.width, 36f), (_entry != null ? "NR_ArchiveDetail" : "NR_ArchiveDetailReport").Translate());
        string title = _entry?.Text ?? report.NodeName;
        float th = Text.CalcHeight(title, inner.width);
        Widgets.Label(new Rect(inner.x, y, inner.width, th), title);
        y += th + 2f;
        Text.Font = GameFont.Tiny;
        int tick = _entry?.Tick ?? report.EndTick;
        string when = tick > 0 ? Date(tick, map) + ", " + Clock(tick, map) : string.Empty;
        if (_entry != null)
            when += " · " + ("NR_ArchiveType_" + _entry.Source).Translate();
        else
            when += " · " + (report.RunnerName ?? "?");
        float wh = Text.CalcHeight(when, inner.width);
        Widgets.Label(new Rect(inner.x, y, inner.width, wh), when.Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        y += wh + 10f;

        if (_entry != null)
        {
            string heat = $"{_entry.Amount:+0.#;-0.#;0}";
            if (_entry.After >= 0f)
                heat += $" ({_entry.After - _entry.Amount:0} → {_entry.After:0})";
            y = Row(inner, y, "NR_ArchiveHeat".Translate(), heat, _entry.Amount > 0.05f ? NetrunUI.Bad : _entry.Amount < -0.05f ? NetrunUI.Ok : NetrunUI.Muted);
        }
        if (report != null)
        {
            if (_entry == null)
                y = Row(inner, y, "NR_ArchiveHeat".Translate(), $"{report.HeatAfter - report.HeatBefore:+0.#;-0.#;0} ({report.HeatBefore:0} → {report.HeatAfter:0})",
                    report.HeatAfter > report.HeatBefore ? NetrunUI.Bad : NetrunUI.Ok);
            y = Row(inner, y, "NR_ArchiveOutcome".Translate(), Outcome(report), OutcomeColor(report));
            y = Row(inner, y, "NR_ArchiveLoot".Translate(), Loot(report), TextColorFor(report));
            y = Row(inner, y, "NR_ArchiveTurns".Translate(), report.Turns.ToString(), NetrunUI.TextColor);
            if (report.Events.Count > 0)
                y = Row(inner, y, "NR_ArchiveEvents".Translate(), report.Events.Count.ToString(), NetrunUI.TextColor);
        }

        y += 10f;
        if (report != null)
        {
            if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 30f), "▶ " + "NR_ArchiveOpenReport".Translate(), NetrunUI.Accent, true))
                Find.WindowStack.Add(new Dialog_DiveReport(report));
            y += 36f;
        }
        if (openHeat != null && NetrunUI.Button(new Rect(inner.x, y, inner.width, 30f), "NR_ArchiveHeatThen".Translate(), NetrunUI.Muted, true, "NR_ArchiveHeatThenTip".Translate()))
            openHeat();
        y += 36f;
        string page = ArticleFor(_entry?.Source ?? HeatSource.Dive);
        HelpPageDef def = DefDatabase<HelpPageDef>.GetNamedSilentFail(page);
        if (def != null && openHelp != null && NetrunUI.Button(new Rect(inner.x, y, inner.width, 30f), "NR_ArchiveArticle".Translate(def.LabelCap), NetrunUI.Muted, true))
            openHelp(page);
    }

    private static Color TextColorFor(DiveReport r) => r.Cyberdata + r.Advanced + r.Silver + r.Items.Count > 0 ? NetrunUI.TextColor : NetrunUI.Muted;

    private static float Row(Rect inner, float y, string label, string value, Color color)
    {
        Text.Font = GameFont.Tiny;
        float vw = inner.width * 0.62f;
        float h = Mathf.Max(24f, Text.CalcHeight(value, vw) + 6f);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(inner.x, y, inner.width - vw, h), label.Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(new Rect(inner.xMax - vw, y, vw, h), value.Colorize(color));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        GUI.color = new Color(1f, 1f, 1f, 0.08f);
        Widgets.DrawLineHorizontal(inner.x, y + h, inner.width);
        GUI.color = Color.white;
        return y + h + 2f;
    }

    private static void Empty(Rect rect, string text)
    {
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, NetrunUI.Prompt(text).Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
    }
}
