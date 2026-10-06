using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// "Heat and trace" (docs/CORE_V2.md, opened from the Core ring): the 0-100 scale with its zones, a 7-day
/// graph with events, what each level brings, what lowers and what raises Heat, Netsick, and two buttons.
/// </summary>
public static class HeatScreen
{
    private const float MinGraphHeight = 130f;
    private const float MaxGraphHeight = 340f;
    private const float ScaleHeight = 62f;
    private const float FooterHeight = 34f;
    /// <summary>Height of the two columns on the previous frame: the graph takes whatever room is left.</summary>
    private static float _columnsHeight = 230f;
    private const float RowHeight = 24f;

    public static void Draw(Rect rect, Map map, HeatTracker tracker, System.Action openClub)
    {
        float y = rect.y;
        y = DrawScale(new Rect(rect.x, y, rect.width, ScaleHeight), tracker) + 6f;
        // The graph grows into the free space so the screen has no empty band at the bottom.
        float graphHeight = Mathf.Clamp(rect.yMax - y - 12f - _columnsHeight - 8f - FooterHeight - 4f, MinGraphHeight, MaxGraphHeight);
        y = DrawGraph(new Rect(rect.x, y, rect.width, graphHeight), tracker) + 12f;

        float colWidth = (rect.width - 16f) / 2f;
        float left = DrawLevels(new Rect(rect.x, y, colWidth, 200f), tracker.Level);
        float right = DrawLowerRaise(new Rect(rect.x + colWidth + 16f, y, colWidth, 220f), map, tracker);
        _columnsHeight = Mathf.Max(left, right) - y;
        y = Mathf.Max(left, right) + 8f;

        DrawFooter(new Rect(rect.x, Mathf.Max(y, rect.yMax - FooterHeight), rect.width, FooterHeight), map, openClub);
    }

    // 1. The scale: five colored zones, a marker at the current value, threshold labels.
    private static float DrawScale(Rect rect, HeatTracker tracker)
    {
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(rect.x, rect.y, rect.width, 16f),
            "NR_HeatScreenNow".Translate(tracker.Heat.ToString("0"), tracker.Level.Label()).Colorize(tracker.Level.Color()));
        Text.Font = GameFont.Small;
        var bar = new Rect(rect.x, rect.y + 22f, rect.width, 16f);
        foreach (HeatLevel level in System.Enum.GetValues(typeof(HeatLevel)))
        {
            float from = level.MinHeat() / HeatTracker.MaxHeat;
            float to = level == HeatLevel.RedZone ? 1f : (level + 1).MinHeat() / HeatTracker.MaxHeat;
            Color c = level.Color();
            c.a = level == tracker.Level ? 0.9f : 0.35f;
            var zone = new Rect(bar.x + bar.width * from, bar.y, bar.width * (to - from), bar.height);
            Widgets.DrawBoxSolid(zone, c);
            TooltipHandler.TipRegion(zone, level.Label().CapitalizeFirst() + ": " + level.Description());
        }
        float mx = bar.x + bar.width * tracker.Heat / HeatTracker.MaxHeat;
        Widgets.DrawBoxSolid(new Rect(mx - 1.5f, bar.y - 5f, 3f, bar.height + 10f), Color.white);

        Text.Font = GameFont.Tiny;
        foreach (HeatLevel level in System.Enum.GetValues(typeof(HeatLevel)))
        {
            if (level == HeatLevel.Shadow)
                continue;
            float tx = bar.x + bar.width * level.MinHeat() / HeatTracker.MaxHeat;
            Widgets.Label(new Rect(tx - 12f, bar.yMax + 4f, 24f, 16f), level.MinHeat().ToString("0").Colorize(NetrunUI.Muted));
        }
        Text.Font = GameFont.Small;
        return rect.yMax;
    }

    // 2. Seven days of hourly samples, level zones behind, logged events as dots.
    private static float DrawGraph(Rect rect, HeatTracker tracker)
    {
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(1f, 1f, 1f, 0.1f));
        Rect plot = rect.ContractedBy(8f);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(plot.x + 2f, plot.y - 2f, plot.width, 16f), "NR_HeatScreenGraph".Translate().Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        plot.yMin += 16f;
        foreach (HeatLevel level in System.Enum.GetValues(typeof(HeatLevel)))
        {
            float from = level.MinHeat() / HeatTracker.MaxHeat;
            float to = level == HeatLevel.RedZone ? 1f : (level + 1).MinHeat() / HeatTracker.MaxHeat;
            Color c = level.Color();
            c.a = 0.07f;
            Widgets.DrawBoxSolid(new Rect(plot.x, plot.yMax - plot.height * to, plot.width, plot.height * (to - from)), c);
        }

        IReadOnlyList<float> history = tracker.History;
        int now = Find.TickManager.TicksGame;
        int span = HeatTracker.HistorySize * HeatTracker.HistoryInterval;
        Vector2 Point(int tick, float heat) => new(
            plot.x + plot.width * Mathf.Clamp01(1f - (now - tick) / (float)span),
            plot.yMax - plot.height * Mathf.Clamp01(heat / HeatTracker.MaxHeat));

        if (history.Count < 2)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(plot, "NR_HeatScreenNoHistory".Translate().Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
        }
        else
        {
            int lastSample = now - now % HeatTracker.HistoryInterval;
            for (int i = 1; i < history.Count; i++)
            {
                int t0 = lastSample - (history.Count - i) * HeatTracker.HistoryInterval;
                int t1 = t0 + HeatTracker.HistoryInterval;
                Widgets.DrawLine(Point(t0, history[i - 1]), Point(t1, history[i]), HeatLevelUtility.LevelOf(history[i]).Color(), 2f);
            }
        }

        // Event dots at the time they happened, on the current curve height.
        foreach (HeatLogEntry entry in tracker.Log.Where(e => now - e.Tick <= span && Mathf.Abs(e.Amount) > 0.05f))
        {
            Vector2 p = Point(entry.Tick, SampleAt(history, now, entry.Tick, tracker.Heat));
            var dot = new Rect(p.x - 4f, p.y - 4f, 8f, 8f);
            GUI.color = entry.Amount > 0f ? NetrunUI.Bad : NetrunUI.Ok;
            GUI.DrawTexture(dot, NetrunUI.Disc);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(dot.ExpandedBy(3f), $"{entry.Text}: {entry.Amount:+0.#;-0.#}");
        }

        return rect.yMax;
    }

    private static float SampleAt(IReadOnlyList<float> history, int now, int tick, float current)
    {
        if (history.Count == 0)
            return current;
        int hoursAgo = (now - tick) / HeatTracker.HistoryInterval;
        int index = history.Count - 1 - hoursAgo;
        return index >= 0 && index < history.Count ? history[index] : current;
    }

    // 3. What each level brings; the current one highlighted.
    private static float DrawLevels(Rect rect, HeatLevel current)
    {
        float y = NetrunUI.Section(new Rect(rect.x, rect.y, rect.width, 36f), "NR_HeatScreenLevels".Translate());
        Text.Font = GameFont.Tiny;
        foreach (HeatLevel level in System.Enum.GetValues(typeof(HeatLevel)))
        {
            string text = level.Label().CapitalizeFirst().Colorize(level.Color()) + "  " + level.Description();
            float h = Mathf.Max(RowHeight, Text.CalcHeight(text, rect.width - 30f) + 4f);
            var row = new Rect(rect.x, y, rect.width, h);
            if (level == current)
                Widgets.DrawBoxSolid(row, NetrunUI.BgCardAlt);
            GUI.DrawTexture(new Rect(row.x + 2f, row.y + 2f, 20f, 20f), NetrunTex.HeatIcon(level));
            Widgets.Label(new Rect(row.x + 28f, row.y + 2f, row.width - 30f, h), text);
            y += h + 2f;
        }
        Text.Font = GameFont.Small;
        return y;
    }

    // 4-5. What lowers Heat (with prices) and what raised it this week.
    private static float DrawLowerRaise(Rect rect, Map map, HeatTracker tracker)
    {
        float y = NetrunUI.Section(new Rect(rect.x, rect.y, rect.width, 36f), "NR_HeatScreenLowers".Translate());
        Fixer best = FixerManager.Get()?.Fixers.Where(f => !f.IsSilent).OrderByDescending(f => f.HeatPerCyberdata).FirstOrDefault();
        Text.Font = GameFont.Tiny;
        y = Line(rect, y, best != null
            ? "NR_HeatScreenScrub".Translate(best.Name, best.HeatPerCyberdata.ToString("0.#"), best.HeatPerAdvancedCyberdata.ToString("0.#"))
            : "NR_HeatScreenNoFixer".Translate());
        y = Line(rect, y, "NR_HeatScreenDecay".Translate(tracker.DecayPerDay.ToString("0.#")));
        y = Line(rect, y, "NR_HeatScreenGhost".Translate());
        Text.Font = GameFont.Small;

        y = NetrunUI.Section(new Rect(rect.x, y + 6f, rect.width, 36f), "NR_HeatScreenRaises".Translate());
        int week = Find.TickManager.TicksGame - 7 * GenDate.TicksPerDay;
        var top = tracker.Log.Where(e => e.Tick >= week && e.Amount > 0f)
            .GroupBy(e => e.Text).Select(g => (text: g.Key, sum: g.Sum(e => e.Amount)))
            .OrderByDescending(g => g.sum).Take(5).ToList();
        Text.Font = GameFont.Tiny;
        if (top.Count == 0)
            y = Line(rect, y, "NR_HeatScreenNothingRaised".Translate().Colorize(NetrunUI.Muted));
        foreach ((string text, float sum) in top)
            y = Line(rect, y, ("+" + sum.ToString("0.#")).Colorize(NetrunUI.Bad) + "  " + text);
        Text.Font = GameFont.Small;
        return y;
    }

    private static float Line(Rect rect, float y, string text)
    {
        float h = Text.CalcHeight(text, rect.width);
        Widgets.Label(new Rect(rect.x, y, rect.width, h), text);
        return y + h + 3f;
    }

    // 6-7. Netsick in the colony and the two actions.
    private static void DrawFooter(Rect rect, Map map, System.Action openClub)
    {
        List<Pawn> sick = map.mapPawns.FreeColonistsSpawned.Where(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick)).ToList();
        string netsick = sick.Count == 0
            ? "NR_HeatScreenNetsickNone".Translate().Colorize(NetrunUI.Muted)
            : "NR_HeatScreenNetsick".Translate(sick.Select(p => p.LabelShort + " (" +
                p.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick).Severity.ToStringPercent() + ")").ToCommaList()).Colorize(NetrunUI.Warn);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(rect.x, rect.y, rect.width - 330f, 34f), netsick);
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(new Rect(rect.x, rect.y, rect.width - 330f, 34f), "NR_HeatScreenNetsickTip".Translate());

        string scrub = "NR_CoreScrubButton".Translate();
        string call = "NR_HeatScreenCallFixer".Translate();
        float sw = NetrunUI.ButtonWidth(scrub);
        float cw = NetrunUI.ButtonWidth(call, icon: true);
        if (NetrunUI.Button(new Rect(rect.xMax - sw, rect.y, sw, 34f), scrub, NetrunUI.Accent, true, "NR_CoreScrubTip".Translate()))
            openClub();
        if (NetrunUI.Button(new Rect(rect.xMax - sw - 8f - cw, rect.y, cw, 34f), call, NetrunUI.Warn, true, "NR_HeatScreenCallTip".Translate(), NetrunTex.CallFixer))
            openClub();
    }
}
