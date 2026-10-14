using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// "Heat and trace" v2 (docs/HEAT_V2.md, mockup 10-heat-v2): a hero strip with the value, trend and the
/// level scale; the 7-day graph and the level effects; ways to lower Heat (through a fixer, a Ghost AI,
/// waiting) and what raised it this week.
/// </summary>
public static class HeatScreen
{
    private const float HeroHeight = 92f;
    private const float Gap = 10f;

    private static Vector2 _lowerScroll;
    private static float _lowerHeight;
    /// <summary>The level shown in "Level effects" (null = the current one); picked by clicking the scale.</summary>
    private static HeatLevel? _shownLevel;
    /// <summary>Graph period: false = 7 days, true = the last 24 hours.</summary>
    private static bool _period24;
    private static System.Action<string> _openHelp;
    private static System.Action _openArchive;

    public static void Draw(Rect rect, Map map, HeatTracker tracker, System.Action openClub, System.Action openServer,
        System.Action<string> openHelp = null, System.Action openArchive = null)
    {
        _openHelp = openHelp;
        _openArchive = openArchive;
        Rect hero = new(rect.x, rect.y, rect.width, HeroHeight);
        GuideRegistry.Area("heat.hero", hero);
        DrawHero(hero, tracker);

        float top = hero.yMax + Gap;
        float rest = rect.yMax - top - Gap;
        float upperH = Mathf.Max(150f, rest * 0.46f);
        float lowerH = rest - upperH;
        float leftW = Mathf.Round(rect.width * 0.6f);
        float rightW = rect.width - leftW - Gap;

        var graph = new Rect(rect.x, top, leftW, upperH);
        var effects = new Rect(rect.x + leftW + Gap, top, rightW, upperH);
        var lower = new Rect(rect.x, top + upperH + Gap, leftW, lowerH);
        var raised = new Rect(rect.x + leftW + Gap, top + upperH + Gap, rightW, lowerH);
        GuideRegistry.Area("heat.graph", graph);
        GuideRegistry.Area("heat.effects", effects);
        GuideRegistry.Area("heat.lower", lower);
        GuideRegistry.Area("heat.raised", raised);

        DrawGraph(graph, tracker);
        DrawEffects(effects, tracker);
        DrawLower(lower, map, tracker, openClub, openServer);
        DrawRaised(raised, tracker);
    }

    private static Rect Card(Rect rect, string title, Texture2D icon = null)
    {
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(1f, 1f, 1f, 0.1f));
        Rect inner = rect.ContractedBy(10f);
        float shift = 0f;
        if (icon != null)
        {
            GUI.DrawTexture(new Rect(inner.x, inner.y - 2f, 22f, 22f), icon);
            shift = 28f;
        }
        float y = NetrunUI.Section(new Rect(inner.x + shift, inner.y - 4f, inner.width - shift, 32f), title);
        return new Rect(inner.x, y, inner.width, inner.yMax - y);
    }

    // ---- 1. Hero: value and level, trend, the scale with labels under the segments ----

    private static void DrawHero(Rect rect, HeatTracker tracker)
    {
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(1f, 1f, 1f, 0.1f));
        Rect r = rect.ContractedBy(12f);
        HeatLevel level = tracker.Level;

        // The level's icon leads the title.
        GUI.DrawTexture(new Rect(r.x, r.y + 1f, 30f, 30f), NetrunTex.HeatIcon(level));
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(r.x + 38f, r.y, r.width * 0.55f - 38f, 32f), $"{tracker.Heat:0} · {level.Label().ToUpper()}".Colorize(level.Color()));
        Text.Font = GameFont.Small;

        float week = WeekChange(tracker);
        string weekText = (week > 0.05f ? "+" : string.Empty) + week.ToString("0.#");
        string trend = (week is <= 0.05f and >= -0.05f ? "• " : string.Empty) + "NR_HeatV2Trend".Translate(
            weekText.Colorize(week > 0.05f ? NetrunUI.Bad : week < -0.05f ? NetrunUI.Ok : NetrunUI.Subtle),
            tracker.DecayPerDay.ToString("0.#").Colorize(NetrunUI.Ok));
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(new Rect(r.x + r.width * 0.45f, r.y + 6f, r.width * 0.55f, 24f), trend.Colorize(NetrunUI.Subtle));
        Text.Anchor = TextAnchor.UpperLeft;
        // Which way the week went: Heat_Rise or Heat_Reduction before the line.
        if ((week > 0.05f ? NetrunTex.HeatRise : week < -0.05f ? NetrunTex.HeatIcon(HeatLevel.Shadow) : null) is { } weekIcon)
            GUI.DrawTexture(new Rect(r.xMax - Text.CalcSize(trend).x - 24f, r.y + 7f, 20f, 20f), weekIcon);

        var bar = new Rect(r.x, r.y + 38f, r.width, 14f);
        foreach (HeatLevel l in System.Enum.GetValues(typeof(HeatLevel)))
        {
            float from = l.MinHeat() / HeatTracker.MaxHeat;
            float to = l == HeatLevel.RedZone ? 1f : (l + 1).MinHeat() / HeatTracker.MaxHeat;
            var seg = new Rect(bar.x + bar.width * from + 1f, bar.y, bar.width * (to - from) - 2f, bar.height);
            Color c = l.Color();
            bool hover = Mouse.IsOver(seg.ExpandedBy(0f, 6f));
            c.a = l == level || hover ? 0.95f : l < level ? 0.6f : 0.3f;
            Widgets.DrawBoxSolid(seg, c);
            if (l == level || hover || _shownLevel == l)
            {
                GUI.color = hover ? Color.white : _shownLevel == l ? NetrunUI.Accent : l.Color();
                Widgets.DrawBox(seg.ExpandedBy(2f), l == level ? 2 : 1);
                GUI.color = Color.white;
            }
            if (Widgets.ButtonInvisible(seg.ExpandedBy(0f, 6f)))
                _shownLevel = l == level ? null : l;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(seg.x, bar.yMax + 3f, seg.width, 16f), $"{l.Label().CapitalizeFirst()} {l.MinHeat():0}".Colorize(l == level ? l.Color() : NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            NetrunUI.Tip(seg.ExpandedBy(0f, 6f), l.Label().CapitalizeFirst() + ": " + l.Description() + "\n\n"
                + string.Join("\n", (HeatLevelInfoDef.For(l)?.effects ?? []).Select(e => "• " + e.text)) + "\n\n" + "NR_HeatV2ScaleClick".Translate().Colorize(NetrunUI.Muted));
        }
        float mx = bar.x + bar.width * tracker.Heat / HeatTracker.MaxHeat;
        Widgets.DrawBoxSolid(new Rect(mx - 1.5f, bar.y - 6f, 3f, bar.height + 12f), Color.white);
    }

    /// <summary>Heat now minus Heat a week ago (or at the start of the history).</summary>
    private static float WeekChange(HeatTracker tracker)
        => tracker.History.Count == 0 ? 0f : tracker.Heat - tracker.History[0];

    // ---- 2. Graph: zones, the line, colored event dots, axis labels, legend ----

    private static void DrawGraph(Rect rect, HeatTracker tracker)
    {
        Rect plot = Card(rect, (_period24 ? "NR_HeatV2Graph24" : "NR_HeatScreenGraph").Translate());
        // Period chips at the top right, the legend left of them.
        string d7 = "NR_HeatV2Period7".Translate(), d1 = "NR_HeatV2Period24".Translate();
        float cw7 = NetrunUI.ButtonWidth(d7) - 10f, cw1 = NetrunUI.ButtonWidth(d1) - 10f;
        var c7 = new Rect(rect.xMax - 12f - cw7, rect.y + 8f, cw7, 22f);
        var c1 = new Rect(c7.x - 4f - cw1, rect.y + 8f, cw1, 22f);
        Text.Font = GameFont.Tiny;
        if (NetrunUI.Button(c1, d1, _period24 ? NetrunUI.Ok : NetrunUI.Muted, true))
            _period24 = true;
        if (NetrunUI.Button(c7, d7, !_period24 ? NetrunUI.Ok : NetrunUI.Muted, true))
            _period24 = false;
        Text.Anchor = TextAnchor.UpperRight;
        string legend = "● ".Colorize(NetrunUI.Ok) + "NR_HeatV2LegendScrub".Translate() + "   " + "● ".Colorize(NetrunUI.Warn) + "NR_HeatV2LegendDive".Translate()
            + "   " + "● ".Colorize(NetrunUI.Bad) + "NR_HeatV2LegendHack".Translate();
        Widgets.Label(new Rect(rect.x, rect.y + 11f, c1.x - rect.x - 10f, 18f), legend);
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        plot.yMax -= 16f;
        foreach (HeatLevel level in System.Enum.GetValues(typeof(HeatLevel)))
        {
            float from = level.MinHeat() / HeatTracker.MaxHeat;
            float to = level == HeatLevel.RedZone ? 1f : (level + 1).MinHeat() / HeatTracker.MaxHeat;
            Color c = level.Color();
            c.a = 0.08f;
            Widgets.DrawBoxSolid(new Rect(plot.x, plot.yMax - plot.height * to, plot.width, plot.height * (to - from)), c);
        }

        // Axis: 7 days ago, 3 days ago, now.
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(plot.x, plot.yMax + 1f, 100f, 16f), (_period24 ? "NR_HeatV2Axis24" : "NR_HeatV2Axis7").Translate().Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperCenter;
        float mid = _period24 ? 0.5f : 4f / 7f;
        Widgets.Label(new Rect(plot.x + plot.width * mid - 50f, plot.yMax + 1f, 100f, 16f), (_period24 ? "NR_HeatV2Axis12" : "NR_HeatV2Axis3").Translate().Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(new Rect(plot.xMax - 100f, plot.yMax + 1f, 100f, 16f), "NR_HeatV2AxisNow".Translate().Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        IReadOnlyList<float> history = tracker.History;
        int now = Find.TickManager.TicksGame;
        int span = (_period24 ? 24 : HeatTracker.HistorySize) * HeatTracker.HistoryInterval;
        Vector2 Point(int tick, float heat) => new(
            plot.x + plot.width * Mathf.Clamp01(1f - (now - tick) / (float)span),
            plot.yMax - plot.height * Mathf.Clamp01(heat / HeatTracker.MaxHeat));

        if (history.Count < 24)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(plot, "NR_HeatV2NoHistory".Translate().Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
        }
        if (history.Count >= 2)
        {
            int lastSample = now - now % HeatTracker.HistoryInterval;
            int first = _period24 ? Mathf.Max(1, history.Count - 24) : 1;
            for (int i = first; i < history.Count; i++)
            {
                int t0 = lastSample - (history.Count - i) * HeatTracker.HistoryInterval;
                int t1 = t0 + HeatTracker.HistoryInterval;
                Widgets.DrawLine(Point(t0, history[i - 1]), Point(t1, history[i]), HeatLevelUtility.LevelOf(history[i]).Color(), 2f);
            }
            Widgets.DrawLine(Point(lastSample, history[history.Count - 1]), Point(now, tracker.Heat), tracker.Level.Color(), 2f);
        }

        foreach (HeatLogEntry entry in tracker.Log.Where(e => now - e.Tick <= span && Mathf.Abs(e.Amount) > 0.05f))
        {
            Vector2 p = Point(entry.Tick, SampleAt(history, now, entry.Tick, tracker.Heat));
            bool hoverDot = Mouse.IsOver(new Rect(p.x - 8f, p.y - 8f, 16f, 16f));
            var dot = hoverDot ? new Rect(p.x - 7f, p.y - 7f, 14f, 14f) : new Rect(p.x - 5f, p.y - 5f, 10f, 10f);
            GUI.color = entry.Amount < 0f ? NetrunUI.Ok : entry.Source == HeatSource.Dive ? NetrunUI.Warn : NetrunUI.Bad;
            GUI.DrawTexture(dot, NetrunUI.Disc);
            GUI.color = Color.white;
            NetrunUI.Tip(dot.ExpandedBy(3f), $"{entry.Text}: {entry.Amount:+0.#;-0.#}\n" + "NR_TimeAgo".Translate((now - entry.Tick).ToStringTicksToPeriod())
                + "\n\n" + "NR_HeatV2DotClick".Translate().Colorize(NetrunUI.Muted));
            if (_openArchive != null && Widgets.ButtonInvisible(dot.ExpandedBy(3f)))
                _openArchive();
        }

        // Crosshair: the Heat at the hovered moment.
        if (Mouse.IsOver(plot) && history.Count >= 2)
        {
            Vector2 m = Event.current.mousePosition;
            float frac = Mathf.Clamp01((m.x - plot.x) / plot.width);
            int tick = now - (int)((1f - frac) * span);
            float value = SampleAt(history, now, tick, tracker.Heat);
            Widgets.DrawBoxSolid(new Rect(m.x, plot.y, 1f, plot.height), new Color(1f, 1f, 1f, 0.25f));
            Text.Font = GameFont.Tiny;
            string label = $"{value:0} · " + "NR_TimeAgo".Translate((now - tick).ToStringTicksToPeriod());
            float lw = Text.CalcSize(label).x + 10f;
            var lr = new Rect(Mathf.Min(m.x + 6f, plot.xMax - lw), plot.y + 2f, lw, 18f);
            Widgets.DrawBoxSolid(lr, NetrunUI.BgWindow);
            Widgets.Label(new Rect(lr.x + 5f, lr.y + 1f, lw, 18f), label.Colorize(HeatLevelUtility.LevelOf(value).Color()));
            Text.Font = GameFont.Small;
        }
    }

    private static float SampleAt(IReadOnlyList<float> history, int now, int tick, float current)
    {
        if (history.Count == 0)
            return current;
        int hoursAgo = (now - tick) / HeatTracker.HistoryInterval;
        int index = history.Count - 1 - hoursAgo;
        return index >= 0 && index < history.Count ? history[index] : current;
    }

    // ---- 3. Level effects ----

    private static void DrawEffects(Rect rect, HeatTracker tracker)
    {
        HeatLevel level = _shownLevel ?? tracker.Level;
        bool current = level == tracker.Level;
        string cardTitle = "NR_HeatV2Effects".Translate(level.Label().ToUpper());
        if (!current)
            cardTitle += " · " + "NR_HeatV2NotCurrent".Translate();
        // The level icon goes first in the card's title.
        Rect r = Card(rect, cardTitle, NetrunTex.HeatIcon(level));
        Text.Font = GameFont.Tiny;
        if (!current && NetrunUI.Button(new Rect(rect.xMax - 44f - 70f, rect.y + 8f, 66f, 22f), "NR_HeatV2BackToCurrent".Translate(), NetrunUI.Muted, true))
            _shownLevel = null;
        Text.Font = GameFont.Small;
        GUI.BeginGroup(r);
        float y = 0f;
        Text.Font = GameFont.Tiny;
        string desc = level.Description();
        float dh = Text.CalcHeight(desc, r.width);
        Widgets.Label(new Rect(0f, y, r.width, dh), desc.Colorize(level.Color()));
        y += dh + 4f;

        foreach (HeatEffect e in HeatLevelInfoDef.For(level)?.effects ?? [])
            y = EffectLine(r.width, y, e.severity, e.text, "heat.fx." + level);

        // Live numbers: the best scrub rate and the cooling.
        Fixer best = BestFixer();
        if (best != null)
            y = EffectLine(r.width, y, HeatEffectSeverity.Info, "NR_HeatV2EffectScrub".Translate(best.HeatPerCyberdata.ToString("0.#"), best.Name));
        y = EffectLine(r.width, y, HeatEffectSeverity.Info, "NR_HeatV2EffectDecay".Translate(tracker.DecayPerDay.ToString("0.#")));

        y += 4f;
        string next = level == HeatLevel.Shadow
            ? "NR_HeatV2NoTrace".Translate()
            : (string)"NR_HeatV2ToLower".Translate((level - 1).Label(), DaysText(tracker.DaysToLowerLevel()));
        Widgets.Label(new Rect(0f, y, r.width, 32f), next.Colorize(NetrunUI.Ok));
        Text.Font = GameFont.Small;
        GUI.EndGroup();
    }

    private static float EffectLine(float width, float y, HeatEffectSeverity severity, string text, string linkGroup = null)
    {
        (string mark, Color color) = severity switch
        {
            HeatEffectSeverity.Bad => ("!", NetrunUI.Bad),
            HeatEffectSeverity.Warn => ("▲", NetrunUI.Warn),
            _ => ("●", NetrunUI.Muted),
        };
        float h = Mathf.Max(18f, linkGroup != null ? HyperText.Height(text, width - 18f, linkGroup) : Text.CalcHeight(text, width - 18f));
        Widgets.Label(new Rect(0f, y, 14f, 18f), mark.Colorize(color));
        if (linkGroup != null)
            HyperText.Label(new Rect(16f, y, width - 18f, h), text, null, linkGroup);
        else
            Widgets.Label(new Rect(16f, y, width - 18f, h), text);
        GUI.color = new Color(1f, 1f, 1f, 0.06f);
        Widgets.DrawLineHorizontal(0f, y + h + 1f, width);
        GUI.color = Color.white;
        return y + h + 4f;
    }

    private static string DaysText(float days)
        => float.IsInfinity(days) ? "∞" : days < 1f ? "NR_HeatV2LessThanDay".Translate() : (string)"NR_HeatV2Days".Translate(Mathf.CeilToInt(days));

    private static Fixer BestFixer()
        => FixerManager.Get()?.Fixers.Where(f => !f.IsSilent).OrderByDescending(f => f.HeatPerCyberdata).FirstOrDefault();

    // ---- 4. How to lower: through a fixer (the pool, each with its action), a Ghost AI, waiting ----

    private static void DrawLower(Rect rect, Map map, HeatTracker tracker, System.Action openClub, System.Action openServer)
    {
        Rect r = Card(rect, "NR_HeatScreenLowers".Translate());
        bool scrolls = _lowerHeight > r.height;
        var view = new Rect(0f, 0f, r.width - (scrolls ? 16f : 0f), Mathf.Max(_lowerHeight, r.height));
        NetrunUI.BeginScroll(r, ref _lowerScroll, view);
        float y = 0f;

        // Through a fixer.
        int data = ColonyGoods.CountCached(map, NetrunDefOf.USH_Cyberdata);
        int adv = ColonyGoods.CountCached(map, NetrunDefOf.USH_AdvancedCyberdata);
        List<Fixer> fixers = FixerManager.Get()?.Fixers.ToList() ?? [];
        float cardH = 44f + Mathf.Max(1, fixers.Count) * 30f + 8f;
        var card = new Rect(0f, y, view.width, cardH);
        LowerCardFrame(card, NetrunUI.Ok, false);
        Widgets.Label(new Rect(card.x + 12f, card.y + 4f, card.width - 20f, 22f), "NR_HeatV2ViaFixer".Translate());
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(card.x + 12f, card.y + 24f, card.width - 20f, 16f), "NR_HeatV2ViaFixerDesc".Translate(data, adv).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        float fy = card.y + 44f;
        if (fixers.Count == 0)
        {
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(card.x + 12f, fy, card.width - 20f, 20f), "NR_HeatScreenNoFixer".Translate().Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
        }
        foreach (Fixer fixer in fixers)
        {
            FixerRow(new Rect(card.x + 12f, fy, card.width - 20f, 26f), fixer, map);
            fy += 30f;
        }
        y = card.yMax + 6f;

        // A Ghost AI: dimmed when the colony has none.
        NetAI ghost = AIManager.Get()?.AIs.FirstOrDefault(a => a.Personality == AIPersonality.Ghost && (a.InServer || a.InHost));
        var ghostCard = new Rect(0f, y, view.width, 46f);
        LowerCardFrame(ghostCard, NetrunUI.Purple, ghost == null);
        Color text = ghost == null ? NetrunUI.Muted : NetrunUI.TextColor;
        Widgets.Label(new Rect(ghostCard.x + 12f, ghostCard.y + 4f, ghostCard.width - 180f, 22f), "NR_HeatV2Ghost".Translate().Colorize(text));
        Text.Font = GameFont.Tiny;
        string ghostDesc = ghost == null
            ? "NR_HeatV2GhostNone".Translate()
            : (string)"NR_HeatV2GhostDesc".Translate(NetrunDefOf.NR_AI.ghostHeatScrub.ToString("0"), NetrunDefOf.NR_AI.serviceTrustCost.ToString("0"));
        Widgets.Label(new Rect(ghostCard.x + 12f, ghostCard.y + 24f, ghostCard.width - 180f, 18f), ghostDesc.Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        string go = (ghost == null ? "NR_HeatV2Unavailable" : "NR_HeatV2ToServer").Translate();
        float gw = NetrunUI.ButtonWidth(go);
        if (NetrunUI.Button(new Rect(ghostCard.xMax - gw - 8f, ghostCard.y + 8f, gw, 30f), go, NetrunUI.Purple,
                ghost == null ? (AcceptanceReport)"NR_HeatV2GhostNone".Translate() : true))
            openServer();
        y = ghostCard.yMax + 6f;

        // Wait it out: information only.
        var wait = new Rect(0f, y, view.width, 46f);
        LowerCardFrame(wait, NetrunUI.Muted, false);
        Widgets.Label(new Rect(wait.x + 12f, wait.y + 4f, wait.width - 180f, 22f), "NR_HeatV2Wait".Translate());
        Text.Font = GameFont.Tiny;
        float low = NetrunMod.Settings.HeatDecayPerDay;
        Widgets.Label(new Rect(wait.x + 12f, wait.y + 24f, wait.width - 180f, 18f),
            "NR_HeatV2WaitDesc".Translate(low.ToString("0.#"), (low * NetrunDefOf.NR_Heat.highHeatDecayFactor).ToString("0.#")).Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(new Rect(wait.xMax - 170f, wait.y, 160f, wait.height),
            (tracker.Heat <= 0f ? "NR_HeatV2NoTrace".Translate() : "NR_HeatV2ToZero".Translate(DaysText(tracker.DaysToZero()))).Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        y = wait.yMax;

        _lowerHeight = y + 2f;
        Widgets.EndScrollView();
    }

    private static void LowerCardFrame(Rect card, Color color, bool dimmed)
    {
        Widgets.DrawBoxSolid(card, NetrunUI.BgWindow);
        Widgets.DrawBoxSolid(new Rect(card.x, card.y, 3f, card.height), dimmed ? new Color(color.r, color.g, color.b, 0.3f) : color);
    }

    /// <summary>One fixer of the pool: name, rate, discount and the right action for the channel state.</summary>
    private static void FixerRow(Rect row, Fixer fixer, Map map)
    {
        Widgets.DrawBoxSolid(row, NetrunUI.BgCard);
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        float discount = 1f - fixer.PriceFactor;
        string info = fixer.Name.Colorize(FixersTab.SpecialtyColor(fixer.Specialty)) + "  "
            + "NR_HeatV2FixerRate".Translate(fixer.HeatPerCyberdata.ToString("0.#"), fixer.HeatPerAdvancedCyberdata.ToString("0.#")).Colorize(NetrunUI.Muted)
            + (discount > 0.01f ? "  " + "NR_HeatV2FixerDiscount".Translate(discount.ToStringPercent()).Colorize(NetrunUI.Ok) : string.Empty);
        Widgets.Label(new Rect(row.x + 8f, row.y, row.width - 170f, row.height), info);
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        var button = new Rect(row.xMax - 156f, row.y + 1f, 152f, row.height - 2f);
        if (fixer.IsSilent)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(button, "NR_HeatV2FixerSilent".Translate((fixer.SilentUntilTick - Find.TickManager.TicksGame).ToStringTicksToPeriod()).Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        else if (fixer.FreeScrubs > 0)
        {
            if (NetrunUI.Button(button, "NR_FixerFreeScrub".Translate(fixer.FreeScrubs), NetrunUI.Purple, true,
                    "NR_FixerFreeScrubTip".Translate(NetrunDefOf.NR_Cyberspace.freeScrubHeat.ToString("0"))))
            {
                fixer.FreeScrubs--;
                HeatTracker.Get()?.ReduceHeat(NetrunDefOf.NR_Cyberspace.freeScrubHeat, "NR_LogScrub".Translate(fixer.Name));
            }
        }
        else if (fixer.ChannelOpen)
        {
            if (NetrunUI.Button(button, "NR_HeatV2ScrubWith".Translate(), NetrunUI.Ok, true, "NR_HeatV2ScrubWithTip".Translate(fixer.Name)))
                Find.WindowStack.Add(new Dialog_FixerScrub(fixer, map));
        }
        else if (NetrunUI.Button(button, "NR_HeatV2CallFixer".Translate(fixer.EffectiveCallCost), NetrunUI.Warn, true,
                     "NR_HeatV2CallFixerTip".Translate(fixer.Name, NetrunDefOf.NR_Fixers.channelHours)))
        {
            FixerDeals.Call(fixer, map);
            if (fixer.ChannelOpen)
                Find.WindowStack.Add(new Dialog_FixerScrub(fixer, map));
        }
    }

    // ---- 5. What raised it this week ----

    private static void DrawRaised(Rect rect, HeatTracker tracker)
    {
        Rect r = Card(rect, "NR_HeatScreenRaises".Translate());
        GUI.BeginGroup(r);
        int week = Find.TickManager.TicksGame - 7 * GenDate.TicksPerDay;
        List<HeatLogEntry> entries = NetrunUI.Cached("heat.week", () => tracker.Log.Where(e => e.Tick >= week).ToList(), 1f);
        var top = entries.Where(e => e.Amount > 0f)
            .GroupBy(e => e.Text).Select(g => (text: g.Key, sum: g.Sum(e => e.Amount)))
            .OrderByDescending(g => g.sum).Take(5).ToList();
        float y = 0f;
        Text.Font = GameFont.Tiny;
        if (top.Count == 0)
        {
            Widgets.Label(new Rect(0f, y, r.width, 20f), "NR_HeatScreenNothingRaised".Translate().Colorize(NetrunUI.Muted));
            y += 22f;
        }
        float max = top.Count > 0 ? top[0].sum : 1f;
        const float barMax = 70f;
        foreach ((string text, float sum) in top)
        {
            if (NetrunTex.HeatRise is { } rise)
                GUI.DrawTexture(new Rect(0f, y + 1f, 18f, 18f), rise);
            Widgets.Label(new Rect(20f, y, 40f, 20f), ("+" + sum.ToString("0.#")).Colorize(NetrunUI.Bad));
            Widgets.DrawBoxSolid(new Rect(62f, y + 6f, Mathf.Max(4f, (barMax - 20f) * sum / max), 8f), NetrunUI.Bad);
            var whole = new Rect(0f, y, r.width, 20f);
            if (Mouse.IsOver(whole))
                Widgets.DrawHighlight(whole);
            if (_openArchive != null && Widgets.ButtonInvisible(whole))
                _openArchive();
            var label = new Rect(42f + barMax + 8f, y, r.width - barMax - 52f, 20f);
            Widgets.Label(label, text.Truncate(label.width));
            NetrunUI.Tip(label, text);
            y += 22f;
        }

        float floor = tracker.NoiseFloor;
        if (floor > 0.5f)
        {
            Widgets.Label(new Rect(0f, y, r.width, 20f), Dialog_NetTerminal.NoiseLine(floor).Colorize(NetrunUI.Warn));
            y += 22f;
        }

        // Totals: in, scrubbed out, and the cooling that explains the rest.
        float gained = entries.Where(e => e.Amount > 0f).Sum(e => e.Amount);
        float scrubbed = entries.Where(e => e.Amount < 0f).Sum(e => e.Amount);
        float start = tracker.History.Count > 0 ? tracker.History[0] : tracker.Heat;
        float cooled = Mathf.Min(0f, tracker.Heat - start - gained - scrubbed);
        y += 6f;
        Widgets.Label(new Rect(0f, y, r.width, 34f),
            "NR_HeatV2Totals".Translate(gained.ToString("0.#").Colorize(NetrunUI.Bad), scrubbed.ToString("0.#").Colorize(NetrunUI.Ok),
                cooled.ToString("0.#").Colorize(NetrunUI.Line)).Colorize(NetrunUI.Subtle));
        Text.Font = GameFont.Small;
        GUI.EndGroup();
    }

    // ---- The status bar's advice on this screen ----

    /// <summary>One sentence for the bottom bar while the Heat screen is open.</summary>
    public static string Advice(HeatTracker tracker)
    {
        HeatLevel level = tracker.Level;
        if (level >= HeatLevel.Target)
            return "NR_HeatV2AdviceHigh".Translate(tracker.DecayPerDay.ToString("0.#"));
        if ((level + 1).MinHeat() - tracker.Heat <= 15f && tracker.Heat > 0f)
            return "NR_HeatV2AdviceNear".Translate(((level + 1).MinHeat() - 1f).ToString("0"), (level + 1).Label());
        if (tracker.Heat > 0f)
            return "NR_HeatV2AdviceOk".Translate(DaysText(tracker.DaysToZero()));
        return "NR_HeatV2AdviceClean".Translate();
    }
}
