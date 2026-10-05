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
    private const float BoxWidth = 108f;
    private const float BoxHeight = 66f;

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
            Widgets.Label(new Rect(box.x + 6f, box.y + 4f, box.width - 10f, 18f), DiveSession.ElementLabel(e).ToUpper().Colorize(color));
            Widgets.Label(new Rect(box.x + 6f, box.y + 22f, box.width - 10f, box.height - 24f), ElementInfo(e, here).Colorize(NetrunUI.TextColor));
            Text.Font = GameFont.Small;
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
        Rect inner = rect.ContractedBy(10f);
        float y = inner.y;

        if (session.Finished)
        {
            y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_DiveOver".Translate());
            Widgets.Label(new Rect(inner.x, y, inner.width, 60f), "NR_DiveLoot".Translate(session.LootText()));
            y += 60f;
        }
        else
        {
            y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_DiveActions".Translate(session.ActionsLeft, session.ActionsPerTurn));
            float x = inner.x;
            float rowY = y;

            void Act(string key, Color color, AcceptanceReport report, System.Action action, string tip = null)
            {
                string label = ("NR_DiveAct_" + key).Translate();
                float w = NetrunUI.ButtonWidth(label);
                if (x + w > inner.xMax)
                {
                    x = inner.x;
                    rowY += 36f;
                }
                if (NetrunUI.Button(new Rect(x, rowY, w, 30f), label, color, report, tip ?? ("NR_DiveActTip_" + key).Translate()))
                    action();
                x += w + 6f;
            }

            Act("Move", NetrunUI.Accent, session.CanMove(), session.Move);
            Act("Scan", NetrunUI.Ok, session.CanScan(), session.Scan);
            AcceptanceReport hack = session.CanHack();
            Act("Hack", NetrunUI.Warn, hack, session.Hack,
                hack.Accepted ? "NR_DiveActTip_HackChance".Translate(session.HackChance(session.Next).ToStringPercent()) : null);
            Act("Download", NetrunUI.Accent, session.CanDownload(), session.Download);
            Act("Exit", NetrunUI.Ok, session.CanExit(), session.Exit);
            y = rowY + 44f;

            y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_DivePrograms".Translate());
            string programs = session.Programs.Count == 0
                ? "NR_DiveNoPrograms".Translate()
                : (string)"NR_DiveProgramState".Translate(session.ShieldCharges, session.DecoyCharges, session.KillerCharges,
                    session.Programs.Contains(DiveProgram.Sword) ? "✓" : "-", session.Programs.Contains(DiveProgram.Stealth) ? "✓" : "-");
            float ph = Text.CalcHeight(programs, inner.width);
            Widgets.Label(new Rect(inner.x, y, inner.width, ph), programs.Colorize(NetrunUI.Muted));
            y += ph + 4f;
            Widgets.Label(new Rect(inner.x, y, inner.width, 22f), "NR_DiveTurn".Translate(session.Turn, NetrunDefOf.NR_Cyberspace.maxTurns).Colorize(NetrunUI.Muted));
            y += 26f;
            Widgets.Label(new Rect(inner.x, y, inner.width, 40f), "NR_DiveLoot".Translate(session.LootText()).Colorize(NetrunUI.Ok));
            y += 40f;
        }

        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_DiveLogHeader".Translate());
        Text.Font = GameFont.Tiny;
        foreach (string line in session.Log)
        {
            float h = Text.CalcHeight(line, inner.width);
            if (y + h > inner.yMax)
                break;
            Widgets.Label(new Rect(inner.x, y, inner.width, h), line);
            y += h + 2f;
        }
        Text.Font = GameFont.Small;
    }

    public static void HeaderChips(float xMax, float y, DiveSession session)
    {
        float x = NetrunUI.HeaderChip(xMax, y, "NR_DiveTraceChip".Translate(session.Trace.ToString("0")).Colorize(NetrunUI.Bad)) - 8f;
        NetrunUI.HeaderChip(x, y, "NR_DiveActionsChip".Translate(session.ActionsLeft, session.ActionsPerTurn));
    }
}
