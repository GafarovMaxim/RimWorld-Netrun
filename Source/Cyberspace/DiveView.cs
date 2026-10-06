using System.Linq;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// A dive in progress (mockup 3-dive): the node's elements as boxes on a staircase from the lobby up to
/// the exit, and a panel with the actions left this turn, the programs and the log.
/// </summary>
public static class DiveView
{
    private const float PanelWidth = 280f;
    private const float MinBoxWidth = 108f;
    private const float MaxBoxWidth = 160f;
    private const float MinBoxHeight = 66f;
    private const float MaxBoxHeight = 110f;

    private static Vector2 _panelScroll;
    private static float _panelHeight;

    public static void Draw(Rect rect, DiveSession session)
    {
        var mapRect = new Rect(rect.x, rect.y, rect.width - PanelWidth - 12f, rect.height);
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

    private static void DrawElements(Rect rect, DiveSession session)
    {
        List<DiveElement> elements = session.Elements;
        int n = elements.Count;
        // Wider boxes when there is room; tall enough for the longest text (Tiny font), within limits.
        float BoxWidth = n > 0 ? Mathf.Clamp(rect.width / n - 10f, MinBoxWidth, MaxBoxWidth) : MinBoxWidth;
        Text.Font = GameFont.Tiny;
        float BoxHeight = MinBoxHeight;
        for (int i = 0; i < n; i++)
            BoxHeight = Mathf.Max(BoxHeight, 26f + Text.CalcHeight(ElementInfo(elements[i], i == session.Position), BoxWidth - 10f));
        BoxHeight = Mathf.Min(BoxHeight, MaxBoxHeight);
        Text.Font = GameFont.Small;
        float stepX = n > 1 ? (rect.width - BoxWidth) / (n - 1) : 0f;
        float stepY = n > 1 ? (rect.height - BoxHeight - 20f) / (n - 1) : 0f;

        var centers = new Vector2[n];
        for (int i = 0; i < n; i++)
            centers[i] = new Vector2(rect.x + BoxWidth / 2f + i * stepX, rect.yMax - BoxHeight / 2f - 10f - i * stepY);

        for (int i = 1; i < n; i++)
        {
            Vector2 a = centers[i - 1];
            Vector2 b = centers[i];
            // Elbow links like the mockup: across, then up.
            Widgets.DrawLine(a, new Vector2(b.x, a.y), NetrunUI.Line, 2f);
            Widgets.DrawLine(new Vector2(b.x, a.y), b, NetrunUI.Line, 2f);
        }

        for (int i = 0; i < n; i++)
        {
            DiveElement e = elements[i];
            var box = new Rect(centers[i].x - BoxWidth / 2f, centers[i].y - BoxHeight / 2f, BoxWidth, BoxHeight);
            Color color = ElementColor(e);
            bool here = i == session.Position;
            NetrunUI.Box(box, here ? new Color(color.r, color.g, color.b, 0.18f) : NetrunUI.BgWindow, color, here ? 3 : 2);

            Text.Font = GameFont.Tiny;
            string title = DiveSession.ElementLabel(e).ToUpper();
            string info = ElementInfo(e, here);
            Widgets.Label(new Rect(box.x + 6f, box.y + 4f, box.width - 10f, 18f), title.Truncate(box.width - 10f).Colorize(color));
            Widgets.Label(new Rect(box.x + 6f, box.y + 22f, box.width - 10f, box.height - 24f), info.Colorize(NetrunUI.TextColor));
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(box, title + "\n" + info);
        }
    }

    private static string ElementInfo(DiveElement e, bool here)
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
        return here ? info + "\n" + "NR_DiveInfo_Here".Translate() : info;
    }

    private static void DrawPanel(Rect rect, DiveSession session)
    {
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(1f, 1f, 1f, 0.12f));
        Rect outer = rect.ContractedBy(10f);
        bool scrolls = _panelHeight > outer.height;
        var inner = new Rect(0f, 0f, outer.width - (scrolls ? 16f : 0f), Mathf.Max(_panelHeight, outer.height));
        Widgets.BeginScrollView(outer, ref _panelScroll, inner);
        float y = inner.y;

        if (session.Finished)
        {
            y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_DiveOver".Translate());
            string lootDone = "NR_DiveLoot".Translate(session.LootText());
            float ldh = Text.CalcHeight(lootDone, inner.width);
            Widgets.Label(new Rect(inner.x, y, inner.width, ldh), lootDone);
            y += ldh + 6f;
        }
        else
        {
            y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_DiveActions".Translate(session.ActionsLeft, session.ActionsPerTurn));
            float x = inner.x;
            float rowY = y;

            void Act(string key, Color color, AcceptanceReport report, System.Action action, string tip = null)
            {
                string label = ("NR_DiveAct_" + key).Translate();
                Texture2D icon = NetrunTex.CyberIcon("Action_" + key);
                float w = NetrunUI.ButtonWidth(label, icon != null);
                if (x + w > inner.xMax)
                {
                    x = inner.x;
                    rowY += 36f;
                }
                if (NetrunUI.Button(new Rect(x, rowY, w, 30f), label, color, report, tip ?? ("NR_DiveActTip_" + key).Translate(), icon))
                    action();
                x += w + 6f;
            }

            Act("Move", NetrunUI.Accent, session.CanMove(), session.Move);
            Act("Scan", NetrunUI.Ok, session.CanScan(), session.Scan);
            AcceptanceReport hack = session.CanHack();
            Act("Hack", NetrunUI.Warn, hack, session.Hack,
                hack.Accepted ? "NR_DiveActTip_HackChance".Translate(session.CurrentHackChance.ToStringPercent()) : null);
            Act("Download", NetrunUI.Accent, session.CanDownload(), session.Download);
            Act("Exit", NetrunUI.Ok, session.CanExit(), session.Exit);
            y = rowY + 44f;

            // Events of this dive: the hunter, the rival, the storm.
            if (session.HunterActive)
                y = Warning(inner, y, "NR_DiveHunterActive".Translate(), NetrunUI.Bad);
            if (session.Node.Rival && !session.RivalTook)
                y = Warning(inner, y, "NR_DiveRivalActive".Translate(System.Math.Max(0, NetrunDefOf.NR_Cyberspace.rivalTurns - session.Turn)), NetrunUI.Warn);
            if (session.Storm)
                y = Warning(inner, y, "NR_DiveStorm".Translate(), NetrunUI.Purple);

            y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_DivePrograms".Translate());
            string programs = session.Programs.Count == 0
                ? "NR_DiveNoPrograms".Translate()
                : (string)"NR_DiveProgramState".Translate(session.ShieldCharges, session.DecoyCharges, session.KillerCharges,
                    session.Programs.Contains(DiveProgram.Sword) ? "✓" : "-", session.Programs.Contains(DiveProgram.Stealth) ? "✓" : "-");
            float ph = Text.CalcHeight(programs, inner.width);
            Widgets.Label(new Rect(inner.x, y, inner.width, ph), programs.Colorize(NetrunUI.Muted));
            y += ph + 4f;
            if (session.Daemons.Count > 0)
            {
                string daemons = "NR_DiveDaemons".Translate(session.Daemons.Select(d => (string)("NR_Daemon_" + d).Translate()).ToCommaList());
                float dh = Text.CalcHeight(daemons, inner.width);
                Widgets.Label(new Rect(inner.x, y, inner.width, dh), daemons.Colorize(NetrunUI.Ok));
                y += dh + 4f;
            }
            Widgets.Label(new Rect(inner.x, y, inner.width, 22f), "NR_DiveTurn".Translate(session.Turn, NetrunDefOf.NR_Cyberspace.maxTurns).Colorize(NetrunUI.Muted));
            y += 26f;
            string loot = "NR_DiveLoot".Translate(session.LootText());
            float lh = Text.CalcHeight(loot, inner.width);
            Widgets.Label(new Rect(inner.x, y, inner.width, lh), loot.Colorize(NetrunUI.Ok));
            y += lh + 4f;
        }

        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_DiveLogHeader".Translate());
        Text.Font = GameFont.Tiny;
        foreach (string line in session.Log)
        {
            float h = Text.CalcHeight(line, inner.width);
            Widgets.Label(new Rect(inner.x, y, inner.width, h), line);
            y += h + 2f;
        }
        Text.Font = GameFont.Small;
        _panelHeight = y + 4f;
        Widgets.EndScrollView();
    }

    private static float Warning(Rect inner, float y, string text, Color color)
    {
        float h = Text.CalcHeight(text, inner.width - 10f) + 6f;
        var row = new Rect(inner.x, y, inner.width, h);
        Widgets.DrawBoxSolid(row, NetrunUI.BgWindow);
        Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), color);
        Widgets.Label(new Rect(row.x + 8f, row.y + 3f, row.width - 10f, h), text.Colorize(color));
        return y + h + 4f;
    }

    public static void HeaderChips(float xMax, float y, DiveSession session)
    {
        float x = NetrunUI.HeaderChip(xMax, y, "NR_DiveTraceChip".Translate(session.Trace.ToString("0")).Colorize(NetrunUI.Bad)) - 8f;
        NetrunUI.HeaderChip(x, y, "NR_DiveActionsChip".Translate(session.ActionsLeft, session.ActionsPerTurn));
    }
}
