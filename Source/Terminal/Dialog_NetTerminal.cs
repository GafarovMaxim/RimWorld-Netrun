using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

public class Dialog_NetTerminal : Window
{
    private enum Tab
    {
        Overview,
        Fixers,
        Contracts,
        AI,
        Braindance,
        Journal,
    }

    private const float TitleHeight = 40f;
    private const float JournalRowHeight = 28f;

    private readonly Map _map;
    private Tab _tab = Tab.Overview;
    private Vector2 _journalScroll;
    private Vector2 _fixersScroll;
    private Vector2 _contractsScroll;

    public override Vector2 InitialSize => new(760f, 560f);

    public Dialog_NetTerminal(Map map)
    {
        _map = map;
        doCloseX = true;
        doCloseButton = false;
        draggable = true;
        forcePause = false;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = false;
        preventCameraMotion = false;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleHeight), "NR_TerminalTitle".Translate());
        Text.Font = GameFont.Small;

        var body = new Rect(inRect.x, inRect.y + TitleHeight + TabDrawer.TabHeight,
            inRect.width, inRect.height - TitleHeight - TabDrawer.TabHeight);

        TabDrawer.DrawTabs(body, BuildTabs());
        Widgets.DrawMenuSection(body);

        Rect content = body.ContractedBy(12f);
        HeatTracker tracker = HeatTracker.Get();
        if (tracker == null)
            return;

        switch (_tab)
        {
            case Tab.Overview:
                DrawOverview(content, tracker);
                break;
            case Tab.Fixers:
                FixersTab.Draw(content, _map, ref _fixersScroll);
                break;
            case Tab.Contracts:
                ContractsTab.Draw(content, _map, ref _contractsScroll);
                break;
            case Tab.Journal:
                DrawJournal(content, tracker);
                break;
            default:
                DrawComingSoon(content);
                break;
        }
    }

    private List<TabRecord> BuildTabs()
    {
        var tabs = new List<TabRecord>();
        foreach (Tab tab in System.Enum.GetValues(typeof(Tab)))
        {
            Tab captured = tab;
            tabs.Add(new TabRecord(("NR_TerminalTab_" + tab).Translate(), () => _tab = captured, _tab == tab));
        }
        return tabs;
    }

    private static void DrawOverview(Rect rect, HeatTracker tracker)
    {
        var list = new Listing_Standard();
        list.Begin(rect);

        HeatLevel level = tracker.Level;

        Text.Font = GameFont.Medium;
        list.Label("NR_TerminalHeatValue".Translate(tracker.Heat.ToString("0"), level.Label().Colorize(level.Color())));
        Text.Font = GameFont.Small;

        Rect barRect = list.GetRect(24f);
        Widgets.FillableBar(barRect, tracker.Heat / HeatTracker.MaxHeat, level.BarTexture(), BaseContent.BlackTex, doBorder: true);
        list.Gap(6f);

        list.Label("NR_TerminalHeatDecay".Translate(NetrunMod.Settings.HeatDecayPerDay.ToString("0.0")));
        list.GapLine();

        list.Label("NR_TerminalLevelsHeader".Translate());
        list.Gap(4f);

        foreach (HeatLevel each in System.Enum.GetValues(typeof(HeatLevel)))
        {
            string range = each == HeatLevel.RedZone
                ? $"{each.MinHeat():0}–100"
                : $"{each.MinHeat():0}–{NextMin(each) - 1:0}";

            string line = $"{each.Label().Colorize(each.Color())} ({range}): {each.Description()}";
            if (each == level)
                line = "> " + line;

            list.Label(line);
        }

        list.End();
    }

    private static float NextMin(HeatLevel level) => (level + 1).MinHeat();

    private void DrawJournal(Rect rect, HeatTracker tracker)
    {
        IReadOnlyList<HeatLogEntry> log = tracker.Log;
        if (log.Count == 0)
        {
            Widgets.Label(rect, "NR_TerminalJournalEmpty".Translate());
            return;
        }

        var viewRect = new Rect(0f, 0f, rect.width - 16f, log.Count * JournalRowHeight);
        Widgets.BeginScrollView(rect, ref _journalScroll, viewRect);

        int now = Find.TickManager.TicksGame;
        float y = 0f;
        for (int i = log.Count - 1; i >= 0; i--)
        {
            HeatLogEntry entry = log[i];
            var row = new Rect(0f, y, viewRect.width, JournalRowHeight);
            if (i % 2 == 0)
                Widgets.DrawLightHighlight(row);

            string ago = "NR_TimeAgo".Translate((now - entry.Tick).ToStringTicksToPeriod());
            string amount = entry.Amount switch
            {
                > 0.05f => $"+{entry.Amount:0.0}".Colorize(ColorLibrary.RedReadable),
                < -0.05f => $"{entry.Amount:0.0}".Colorize(ColorLibrary.Green),
                _ => string.Empty,
            };

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x + 6f, row.y, 140f, row.height), ago.Colorize(ColorLibrary.Grey));
            Widgets.Label(new Rect(row.x + 150f, row.y, row.width - 220f, row.height), entry.Text);
            Widgets.Label(new Rect(row.xMax - 64f, row.y, 60f, row.height), amount);
            Text.Anchor = TextAnchor.UpperLeft;

            y += JournalRowHeight;
        }

        Widgets.EndScrollView();
    }

    private static void DrawComingSoon(Rect rect)
    {
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = Color.gray;
        Widgets.Label(rect, "NR_TerminalComingSoon".Translate());
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
    }
}
