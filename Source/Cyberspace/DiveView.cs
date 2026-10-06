using System.Linq;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// A dive in progress (mockup 3-dive): the node's steps on a staircase from the lobby to the exit, and a HUD
/// panel with the actions, programs, turn and trace gauges, the haul and a terminal-style log.
/// </summary>
public static class DiveView
{
    private const float PanelWidth = 290f;
    private const float MinBoxWidth = 116f;
    private const float MaxBoxWidth = 190f;
    private const float MinBoxHeight = 70f;
    private const float MaxBoxHeight = 132f;
    private const float TraceGaugeMax = 20f;

    private static Vector2 _panelScroll;
    private static float _panelHeight;
    private static Vector2 _logScroll;
    private static int _logCount = -1;
    private static float _logChangedAt;

    public static void Draw(Rect rect, DiveSession session)
    {
        var mapRect = new Rect(rect.x, rect.y, rect.width - PanelWidth - 12f, rect.height);
        GuideRegistry.Area("dive.steps", mapRect);
        DrawElements(mapRect, session);
        DrawPanel(new Rect(rect.xMax - PanelWidth, rect.y - 40f, PanelWidth, rect.height + 40f), session);
    }

    private static Color ElementColor(DiveElement e)
    {
        if (e.Hidden)
            return NetrunUI.Muted;
        return e.Kind switch
        {
            DiveElementKind.Lobby => NetrunUI.Ok,
            DiveElementKind.Gate => NetrunUI.Warn,
            DiveElementKind.Ice => NetrunUI.Bad,
            DiveElementKind.Files => NetrunUI.Accent,
            DiveElementKind.Memory => NetrunUI.Purple,
            _ => NetrunUI.Ok,
        };
    }

    /// <summary>The step's icon and whether it is a white line icon to tint in the step's color.</summary>
    private static (Texture2D icon, bool tint) ElementIcon(DiveElement e)
    {
        if (e.Hidden)
            return (NetrunTex.CyberIcon("Dive_HIDDEN"), true);
        return e.Kind switch
        {
            DiveElementKind.Lobby => (NetrunTex.CyberIcon("Dive_LOBBY"), true),
            DiveElementKind.Gate => (NetrunTex.CyberIcon("Dive_GATE"), false),
            DiveElementKind.Ice => (NetrunTex.CyberIcon("Dive_BLACKICE"), false),
            DiveElementKind.Files => (NetrunTex.CyberIcon("Dive_FILES"), true),
            DiveElementKind.Memory => (NetrunTex.CyberIcon("Dive_MEMORY"), true),
            _ => (NetrunTex.CyberIcon("Dive_EXIT"), true),
        };
    }

    // ---- Steps ----

    private static void DrawElements(Rect rect, DiveSession session)
    {
        List<DiveElement> elements = session.Elements;
        int n = elements.Count;
        float boxW = n > 0 ? Mathf.Clamp(rect.width / n - 10f, MinBoxWidth, MaxBoxWidth) : MinBoxWidth;
        Text.Font = GameFont.Tiny;
        float boxH = MinBoxHeight;
        for (int i = 0; i < n; i++)
            boxH = Mathf.Max(boxH, HeaderHeight(elements[i], boxW) + 12f + (elements[i].Kind is DiveElementKind.Gate or DiveElementKind.Ice ? 10f : 0f) + Text.CalcHeight(ElementInfo(elements[i]), boxW - 16f));
        boxH = Mathf.Min(boxH, MaxBoxHeight);
        Text.Font = GameFont.Small;

        float stepX = n > 1 ? (rect.width - boxW) / (n - 1) : 0f;
        float stepY = n > 1 ? (rect.height - boxH - 20f) / (n - 1) : 0f;
        var boxes = new Rect[n];
        for (int i = 0; i < n; i++)
            boxes[i] = new Rect(rect.x + i * stepX, rect.yMax - boxH - 10f - i * stepY, boxW, boxH);

        // Links: from the right edge of a box across to the next box's centre, then up into its bottom edge.
        // Solid 2 px bars that overlap at the corner, so there are no gaps; a faint wider bar under them glows.
        for (int i = 1; i < n; i++)
        {
            Rect a = boxes[i - 1];
            Rect b = boxes[i];
            bool done = i <= session.Position;
            Color line = done ? new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.9f) : NetrunUI.Line;
            float y = a.center.y;
            float x = b.center.x;
            var across = new Rect(a.xMax, y - 1f, Mathf.Max(0f, x - a.xMax + 1f), 2f);
            var up = new Rect(x - 1f, b.yMax, 2f, Mathf.Max(0f, y - b.yMax + 1f));
            Color glow = new(line.r, line.g, line.b, 0.15f);
            Widgets.DrawBoxSolid(across.ExpandedBy(2f), glow);
            Widgets.DrawBoxSolid(up.ExpandedBy(2f), glow);
            Widgets.DrawBoxSolid(across, line);
            Widgets.DrawBoxSolid(up, line);
        }

        for (int i = 0; i < n; i++)
            DrawStep(boxes[i], elements[i], i == session.Position, i < session.Position);
    }

    /// <summary>Header height: the title wraps onto a second line instead of being cut.</summary>
    private static float HeaderHeight(DiveElement e, float boxW)
    {
        Text.Font = GameFont.Tiny;
        float h = Text.CalcHeight(DiveSession.ElementLabel(e).ToUpper(), boxW - 2f - 30f - 18f);
        return Mathf.Clamp(h + 6f, 24f, 36f);
    }

    private static void DrawStep(Rect box, DiveElement e, bool here, bool passed)
    {
        Color color = ElementColor(e);
        float pulse = here && NetrunMod.Settings.Animations ? 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 3f) : 0.5f;
        if (here)
            NetrunUI.Glow(box, color, 0.35f + 0.3f * pulse, 10f);
        Widgets.DrawBoxSolid(box, NetrunUI.BgWindow);
        if (here)
            Widgets.DrawBoxSolid(box, new Color(color.r, color.g, color.b, 0.12f));
        float alpha = passed ? 0.5f : 1f;
        Color edge = new(color.r, color.g, color.b, alpha * (e.Hidden ? 0.6f : 1f));
        GUI.color = edge;
        Widgets.DrawBox(box, here ? 2 : 1);
        GUI.color = Color.white;
        Corners(box, edge, here ? 10f : 7f);

        // Header strip: icon and title (wraps to two lines), the state mark on the right.
        Text.Font = GameFont.Tiny;
        float hh = HeaderHeight(e, box.width);
        var header = new Rect(box.x + 1f, box.y + 1f, box.width - 2f, hh);
        Widgets.DrawBoxSolid(header, new Color(color.r, color.g, color.b, 0.18f * alpha));
        (Texture2D icon, bool tint) = ElementIcon(e);
        float tx = header.x + 6f;
        if (icon != null)
        {
            GUI.color = tint ? edge : new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(header.x + 4f, header.center.y - 10f, 20f, 20f), icon);
            GUI.color = Color.white;
            tx += 24f;
        }
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        string title = DiveSession.ElementLabel(e).ToUpper();
        Widgets.Label(new Rect(tx, header.y, header.xMax - tx - 18f, header.height), title.Colorize(edge));
        string state = e.Hidden ? "?" : passed || e.Cleared && e.Kind != DiveElementKind.Lobby ? "✓" : e.Blocks ? "■" : string.Empty;
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(new Rect(header.xMax - 18f, header.y, 14f, header.height), state.Colorize(edge));
        Text.Anchor = TextAnchor.UpperLeft;

        // Body: the info, a little larger spacing; strength as pips under it.
        string info = ElementInfo(e);
        var body = new Rect(box.x + 8f, header.yMax + 6f, box.width - 16f, box.yMax - header.yMax - 10f);
        Widgets.Label(body, info.Colorize(passed ? NetrunUI.Muted : NetrunUI.TextColor));
        if (!e.Hidden && !e.Cleared && e.Strength > 0 && e.Kind is DiveElementKind.Gate or DiveElementKind.Ice)
        {
            int pips = Mathf.Clamp(e.Strength, 1, 10);
            float py = box.yMax - 11f;
            for (int i = 0; i < pips; i++)
                Widgets.DrawBoxSolid(new Rect(body.x + i * 9f, py, 6f, 5f), new Color(color.r, color.g, color.b, alpha));
        }
        Text.Font = GameFont.Small;

        // "You are here": a tag above the box instead of a line inside it.
        if (here)
        {
            Text.Font = GameFont.Tiny;
            string tag = "NR_DiveInfo_Here".Translate();
            float tw = Text.CalcSize(tag).x + 12f;
            var tagRect = new Rect(box.x, box.y - 22f, tw, 18f);
            Widgets.DrawBoxSolid(tagRect, new Color(color.r, color.g, color.b, 0.25f + 0.2f * pulse));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(tagRect, tag.Colorize(Color.white));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        NetrunUI.Tip(box, title + "\n" + info);
    }

    /// <summary>Small corner brackets around a step box.</summary>
    private static void Corners(Rect r, Color color, float len)
    {
        const float t = 2f;
        Widgets.DrawBoxSolid(new Rect(r.x - 2f, r.y - 2f, len, t), color);
        Widgets.DrawBoxSolid(new Rect(r.x - 2f, r.y - 2f, t, len), color);
        Widgets.DrawBoxSolid(new Rect(r.xMax + 2f - len, r.y - 2f, len, t), color);
        Widgets.DrawBoxSolid(new Rect(r.xMax, r.y - 2f, t, len), color);
        Widgets.DrawBoxSolid(new Rect(r.x - 2f, r.yMax, len, t), color);
        Widgets.DrawBoxSolid(new Rect(r.x - 2f, r.yMax + 2f - len, t, len), color);
        Widgets.DrawBoxSolid(new Rect(r.xMax + 2f - len, r.yMax, len, t), color);
        Widgets.DrawBoxSolid(new Rect(r.xMax, r.yMax + 2f - len, t, len), color);
    }

    private static string ElementInfo(DiveElement e)
    {
        string info = e.Hidden
            ? "NR_DiveInfo_Hidden".Translate()
            : e.Kind switch
            {
                DiveElementKind.Gate => e.Cleared ? "NR_DiveInfo_Open".Translate() : "NR_DiveInfo_Gate".Translate(e.Strength),
                DiveElementKind.Ice => e.Cleared ? "NR_DiveInfo_Broken".Translate() : "NR_DiveInfo_Ice".Translate(e.Strength),
                DiveElementKind.Files or DiveElementKind.Memory => e.Looted ? "NR_DiveInfo_Looted".Translate() : ("NR_DiveInfo_" + e.Kind).Translate(),
                _ => ("NR_DiveInfo_" + e.Kind).Translate(),
            };
        return info;
    }

    // ---- HUD panel ----

    private static void DrawPanel(Rect rect, DiveSession session)
    {
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.35f));
        Rect outer = rect.ContractedBy(10f);
        // The log takes the bottom part; the rest scrolls when the window is short.
        float logHeight = Mathf.Clamp(outer.height * 0.36f, 120f, 260f);
        var top = new Rect(outer.x, outer.y, outer.width, outer.height - logHeight - 8f);
        var log = new Rect(outer.x, top.yMax + 8f, outer.width, logHeight);

        bool scrolls = _panelHeight > top.height;
        var inner = new Rect(0f, 0f, top.width - (scrolls ? 16f : 0f), Mathf.Max(_panelHeight, top.height));
        Widgets.BeginScrollView(top, ref _panelScroll, inner);
        float y = 0f;

        if (session.Finished)
        {
            y = HudHeader(inner, y, "NR_DiveOver".Translate(), null);
            y = DrawHaul(inner, y, session);
        }
        else
        {
            // Actions: pips for the actions left, buttons in a two-column grid.
            float gAct = y;
            y = HudHeader(inner, y, "NR_DiveHudActions".Translate(), Pips(session.ActionsLeft, session.ActionsPerTurn, NetrunUI.Accent));
            float bw = (inner.width - 6f) / 2f;
            int index = 0;
            float gridY = y;
            void Act(string key, Color color, AcceptanceReport report, System.Action action, string tip = null)
            {
                var r = new Rect(inner.x + index % 2 * (bw + 6f), gridY + index / 2 * 34f, bw, 30f);
                if (NetrunUI.Button(r, ("NR_DiveAct_" + key).Translate(), color, report, tip ?? ("NR_DiveActTip_" + key).Translate(), NetrunTex.CyberIcon("Action_" + key)))
                    action();
                index++;
            }
            Act("Move", NetrunUI.Accent, session.CanMove(), session.Move);
            Act("Scan", NetrunUI.Ok, session.CanScan(), session.Scan);
            AcceptanceReport hack = session.CanHack();
            Act("Hack", NetrunUI.Warn, hack, session.Hack, hack.Accepted ? "NR_DiveActTip_HackChance".Translate(session.CurrentHackChance.ToStringPercent()) : null);
            Act("Download", NetrunUI.Accent, session.CanDownload(), session.Download);
            Act("Exit", NetrunUI.Ok, session.CanExit(), () =>
            {
                session.Exit();
                GatewayManager.Get()?.ClearFinished();
            });
            y = gridY + (index + 1) / 2 * 34f + 6f;

            // Threats of this dive.
            if (session.HunterActive)
                y = Warning(inner, y, "NR_DiveHunterActive".Translate(), NetrunUI.Bad, NetrunTex.CyberIcon("Event_HunterICE"));
            if (session.Node.Rival && !session.RivalTook)
                y = Warning(inner, y, "NR_DiveRivalActive".Translate(System.Math.Max(0, NetrunDefOf.NR_Cyberspace.rivalTurns - session.Turn)), NetrunUI.Warn, NetrunTex.CyberIcon("Event_Rival"));
            if (session.Storm)
                y = Warning(inner, y, "NR_DiveStorm".Translate(), NetrunUI.Purple, NetrunTex.CyberIcon("Event_DataStorm"));

            GuideRegistry.InScroll("dive.actions", top, _panelScroll, new Rect(inner.x, gAct, inner.width, y - gAct));
            float gGauge = y;
            // Gauges: turn and trace.
            int maxTurns = NetrunDefOf.NR_Cyberspace.maxTurns;
            y = Gauge(inner, y, "NR_DiveHudTurn".Translate(session.Turn, maxTurns), session.Turn / (float)maxTurns,
                session.Turn >= maxTurns - 1 ? NetrunUI.Bad : NetrunUI.Accent, "NR_DiveTurn".Translate(session.Turn, maxTurns));
            float heat = session.Trace * session.Node.HeatPerTrace;
            y = Gauge(inner, y, "NR_DiveHudTrace".Translate(session.Trace.ToString("0.#"), heat.ToString("0.#")), session.Trace / TraceGaugeMax,
                HeatLevelUtility.LevelOf(Mathf.Min(99f, session.Trace / TraceGaugeMax * 100f)).Color(), "NR_DiveHudTraceTip".Translate());

            GuideRegistry.InScroll("dive.gauges", top, _panelScroll, new Rect(inner.x, gGauge, inner.width, y - gGauge));
            float gProg = y;
            // Programs and daemons as chips with icons and charges.
            y = HudHeader(inner, y + 4f, "NR_DivePrograms".Translate(), null);
            if (session.Programs.Count == 0 && session.Daemons.Count == 0)
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(inner.x, y, inner.width, 20f), "NR_DiveNoPrograms".Translate().Colorize(NetrunUI.Muted));
                Text.Font = GameFont.Small;
                y += 22f;
            }
            else
            {
                float x = inner.x;
                foreach (DiveProgram p in session.Programs.Distinct())
                {
                    int charges = p switch
                    {
                        DiveProgram.Shield => session.ShieldCharges,
                        DiveProgram.Decoy => session.DecoyCharges,
                        DiveProgram.Killer => session.KillerCharges,
                        _ => -1,
                    };
                    string label = ("NR_Program_" + p).Translate() + (charges >= 0 ? " ×" + charges : string.Empty);
                    Chip(inner, ref x, ref y, NetrunTex.CyberIcon("Program_" + p), label, charges != 0, ("NR_ProgramDesc_" + p).Translate(NetrunDefOf.NR_Cyberspace.programCost));
                }
                foreach (DiveDaemon d in session.Daemons)
                    Chip(inner, ref x, ref y, NetrunTex.DaemonIcon(d), ("NR_Daemon_" + d).Translate(), true, ("NR_DaemonDesc_" + d).Translate());
                y += 30f;
            }

            GuideRegistry.InScroll("dive.programs", top, _panelScroll, new Rect(inner.x, gProg, inner.width, y - gProg));
            float gHaul = y;
            y = DrawHaul(inner, y + 2f, session);
            GuideRegistry.InScroll("dive.haul", top, _panelScroll, new Rect(inner.x, gHaul, inner.width, y - gHaul));
        }
        _panelHeight = y + 4f;
        Widgets.EndScrollView();

        GuideRegistry.Area("dive.log", log);
        DrawLog(log, session);
    }

    private static void Chip(Rect inner, ref float x, ref float y, Texture2D icon, string label, bool active, string tip)
    {
        Text.Font = GameFont.Tiny;
        float w = 26f + Text.CalcSize(label).x + 8f;
        if (x + w > inner.xMax)
        {
            x = inner.x;
            y += 28f;
        }
        var r = new Rect(x, y, w, 24f);
        Color c = active ? NetrunUI.Ok : NetrunUI.Muted;
        NetrunUI.Box(r, NetrunUI.BgWindow, new Color(c.r, c.g, c.b, active ? 0.8f : 0.35f));
        if (icon != null)
        {
            GUI.color = active ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            GUI.DrawTexture(new Rect(r.x + 3f, r.y + 3f, 18f, 18f), icon);
            GUI.color = Color.white;
        }
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(r.x + 24f, r.y, w - 26f, r.height), label.Colorize(c));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        NetrunUI.Tip(r, tip);
        x += w + 4f;
    }

    private static float HudHeader(Rect inner, float y, string label, string right)
    {
        Text.Font = GameFont.Tiny;
        Widgets.DrawBoxSolid(new Rect(inner.x, y + 6f, 3f, 12f), NetrunUI.Accent);
        Widgets.Label(new Rect(inner.x + 8f, y + 2f, inner.width - 90f, 20f), ("// " + label.ToUpper()).Colorize(NetrunUI.Accent));
        if (right != null)
        {
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inner.xMax - 90f, y + 2f, 90f, 20f), right);
            Text.Anchor = TextAnchor.UpperLeft;
        }
        Text.Font = GameFont.Small;
        GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.35f);
        Widgets.DrawLineHorizontal(inner.x, y + 22f, inner.width);
        GUI.color = Color.white;
        return y + 28f;
    }

    private static string Pips(int left, int total, Color color)
        => new string('●', Mathf.Max(0, left)).Colorize(color) + new string('○', Mathf.Max(0, total - left)).Colorize(NetrunUI.Muted);

    private static float Gauge(Rect inner, float y, string label, float fraction, Color color, string tip)
    {
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(inner.x, y, inner.width, 16f), label.Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        NetrunUI.Bar(new Rect(inner.x, y + 17f, inner.width, 8f), fraction, color);
        NetrunUI.Tip(new Rect(inner.x, y, inner.width, 26f), tip);
        return y + 32f;
    }

    private static float DrawHaul(Rect inner, float y, DiveSession session)
    {
        y = HudHeader(inner, y, "NR_DiveHudHaul".Translate(), null);
        var haul = new List<(Texture2D icon, string label, string tip)>();
        if (session.LootCyberdata > 0)
            haul.Add((NetrunDefOf.USH_Cyberdata.uiIcon, "×" + session.LootCyberdata, NetrunDefOf.USH_Cyberdata.LabelCap));
        if (session.LootAdvanced > 0)
            haul.Add((NetrunDefOf.USH_AdvancedCyberdata.uiIcon, "×" + session.LootAdvanced, NetrunDefOf.USH_AdvancedCyberdata.LabelCap));
        if (session.LootSilver > 0)
            haul.Add((ThingDefOf.Silver.uiIcon, "×" + session.LootSilver, ThingDefOf.Silver.LabelCap));
        foreach (IGrouping<ThingDef, ThingDef> g in session.LootItems.GroupBy(d => d))
            haul.Add((g.Key.uiIcon, "×" + g.Count(), g.Key.LabelCap));
        Text.Font = GameFont.Tiny;
        if (haul.Count == 0)
        {
            Widgets.Label(new Rect(inner.x, y, inner.width, 20f), "NR_DiveHudHaulEmpty".Translate().Colorize(NetrunUI.Muted));
            y += 22f;
        }
        float x = inner.x;
        foreach ((Texture2D icon, string label, string tip) in haul)
        {
            float w = 28f + Text.CalcSize(label).x + 6f;
            if (x + w > inner.xMax)
            {
                x = inner.x;
                y += 30f;
            }
            var r = new Rect(x, y, w, 26f);
            Widgets.DrawBoxSolid(r, NetrunUI.BgWindow);
            GUI.DrawTexture(new Rect(r.x + 2f, r.y + 1f, 24f, 24f), icon);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(r.x + 28f, r.y, w - 28f, r.height), label.Colorize(NetrunUI.Ok));
            Text.Anchor = TextAnchor.UpperLeft;
            NetrunUI.Tip(r, tip);
            x += w + 4f;
        }
        Text.Font = GameFont.Small;
        return y + (haul.Count > 0 ? 32f : 0f);
    }

    private static float Warning(Rect inner, float y, string text, Color color, Texture2D icon)
    {
        float h = Mathf.Max(28f, Text.CalcHeight(text, inner.width - 40f) + 6f);
        var row = new Rect(inner.x, y, inner.width, h);
        Widgets.DrawBoxSolid(row, new Color(color.r, color.g, color.b, 0.1f));
        Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), color);
        if (icon != null)
            GUI.DrawTexture(new Rect(row.x + 7f, row.y + (h - 22f) / 2f, 22f, 22f), icon);
        Widgets.Label(new Rect(row.x + 34f, row.y + 3f, row.width - 38f, h), text.Colorize(color));
        return y + h + 4f;
    }

    // ---- Log: a terminal ----

    private static void DrawLog(Rect rect, DiveSession session)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.03f, 0.05f, 0.04f));
        GUI.color = new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.35f);
        Widgets.DrawBox(rect);
        GUI.color = Color.white;
        Text.Font = GameFont.Tiny;
        string header = "// " + ((string)"NR_DiveLogHeader".Translate()).ToUpper() + " · " + session.Node.Name.ToLower();
        Widgets.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 16f), header.Colorize(NetrunUI.Ok));
        var outer = new Rect(rect.x + 4f, rect.y + 20f, rect.width - 8f, rect.height - 24f);

        List<string> lines = session.Log;
        if (lines.Count != _logCount)
        {
            _logCount = lines.Count;
            _logChangedAt = Time.realtimeSinceStartup;
            _logScroll.y = float.MaxValue;
        }
        float w = outer.width - 16f;
        float total = 18f;
        foreach (string line in lines)
            total += Text.CalcHeight("> " + line, w) + 1f;
        var view = new Rect(0f, 0f, w, Mathf.Max(total, outer.height));
        Widgets.BeginScrollView(outer, ref _logScroll, view);
        float y = 0f;
        for (int i = 0; i < lines.Count; i++)
        {
            string text = lines[i];
            bool last = i == lines.Count - 1;
            // The newest line types itself out when animations are on.
            if (last && NetrunMod.Settings.Animations)
            {
                string plain = text.StripTags();
                int shown = Mathf.Clamp(Mathf.FloorToInt((Time.realtimeSinceStartup - _logChangedAt) * 90f), 0, plain.Length);
                if (shown < plain.Length)
                    text = plain.Substring(0, shown);
            }
            string prefix = last ? ">_ " : "> ";
            float h = Text.CalcHeight(prefix + text, w);
            string body = text.Contains("<color") ? text : text.Colorize(new Color(0.75f, 0.86f, 0.75f));
            Widgets.Label(new Rect(0f, y, w, h), prefix.Colorize(NetrunUI.Ok) + body);
            y += h + 1f;
        }
        bool blink = !NetrunMod.Settings.Animations || Mathf.FloorToInt(Time.realtimeSinceStartup * 2f) % 2 == 0;
        if (!session.Finished && blink)
            Widgets.Label(new Rect(0f, y, 20f, 16f), "█".Colorize(NetrunUI.Ok));
        Widgets.EndScrollView();
        Text.Font = GameFont.Small;
    }

    public static void HeaderChips(float xMax, float y, DiveSession session)
    {
        float x = NetrunUI.HeaderChip(xMax, y, "NR_DiveTraceChip".Translate(session.Trace.ToString("0")).Colorize(NetrunUI.Bad)) - 8f;
        NetrunUI.HeaderChip(x, y, "NR_DiveActionsChip".Translate(session.ActionsLeft, session.ActionsPerTurn));
    }
}
