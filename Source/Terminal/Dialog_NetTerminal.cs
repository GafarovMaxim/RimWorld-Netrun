using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Netrun;

public class Dialog_NetTerminal : Window
{
    // Order matches NetrunTex.TabIcon.
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
    private const float TabStripHeight = 40f;
    private const float TabIconSize = 24f;
    private const float JournalRowHeight = 30f;
    private const float LevelRowHeight = 36f;

    private readonly Map _map;
    private Tab _tab = Tab.Overview;
    private Vector2 _journalScroll;
    private Vector2 _fixersScroll;
    private Vector2 _contractsScroll;
    private Vector2 _aiScroll;

    public override Vector2 InitialSize => new(820f, 640f);

    public Dialog_NetTerminal(Map map)
    {
        _map = map;
        doCloseX = true;
        doCloseButton = false;
        draggable = true;
        forcePause = false;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = true;
        preventCameraMotion = false;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleHeight), "NR_TerminalTitle".Translate());
        Text.Font = GameFont.Small;

        DrawTabStrip(new Rect(inRect.x, inRect.y + TitleHeight, inRect.width, TabStripHeight));

        var body = new Rect(inRect.x, inRect.y + TitleHeight + TabStripHeight + 4f,
            inRect.width, inRect.height - TitleHeight - TabStripHeight - 4f);
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
            case Tab.AI:
                AITab.Draw(content, _map, ref _aiScroll);
                break;
            case Tab.Journal:
                DrawJournal(content, tracker);
                break;
            default:
                DrawComingSoon(content);
                break;
        }
    }

    /// <summary>Icon-and-label tabs; the selected one is highlighted.</summary>
    private void DrawTabStrip(Rect rect)
    {
        var tabs = (Tab[])System.Enum.GetValues(typeof(Tab));
        float width = rect.width / tabs.Length;

        for (int i = 0; i < tabs.Length; i++)
        {
            Tab tab = tabs[i];
            var tabRect = new Rect(rect.x + i * width, rect.y, width - 4f, rect.height);
            bool selected = _tab == tab;

            Widgets.DrawOptionBackground(tabRect, selected);
            if (selected)
                Widgets.DrawBox(tabRect, 2);

            var iconRect = new Rect(tabRect.x + 8f, tabRect.y + (tabRect.height - TabIconSize) / 2f, TabIconSize, TabIconSize);
            GUI.DrawTexture(iconRect, NetrunTex.TabIcon(i));

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(iconRect.xMax + 6f, tabRect.y, tabRect.width - TabIconSize - 16f, tabRect.height),
                ("NR_TerminalTab_" + tab).Translate());
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonInvisible(tabRect) && !selected)
            {
                _tab = tab;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }
    }

    private static void DrawOverview(Rect rect, HeatTracker tracker)
    {
        HeatLevel level = tracker.Level;
        float y = rect.y;

        // Current level: big icon, value, colored level name.
        GUI.DrawTexture(new Rect(rect.x, y, 48f, 48f), NetrunTex.HeatIcon(level));
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(rect.x + 58f, y + 8f, rect.width - 58f, 34f),
            "NR_TerminalHeatValue".Translate(tracker.Heat.ToString("0"), level.Label().Colorize(level.Color())));
        Text.Font = GameFont.Small;
        y += 56f;

        Widgets.FillableBar(new Rect(rect.x, y, rect.width, 22f), tracker.Heat / HeatTracker.MaxHeat,
            level.BarTexture(), BaseContent.BlackTex, doBorder: true);
        y += 30f;

        GUI.color = NetrunTex.DimText;
        Widgets.Label(new Rect(rect.x, y, rect.width, 24f),
            "NR_TerminalHeatDecay".Translate(NetrunMod.Settings.HeatDecayPerDay.ToString("0.0")));
        GUI.color = Color.white;
        y += 30f;

        Widgets.DrawLineHorizontal(rect.x, y, rect.width);
        y += 8f;
        Widgets.Label(new Rect(rect.x, y, rect.width, 24f), "NR_TerminalLevelsHeader".Translate());
        y += 28f;

        foreach (HeatLevel each in System.Enum.GetValues(typeof(HeatLevel)))
        {
            var row = new Rect(rect.x, y, rect.width, LevelRowHeight);
            if (each == level)
                Widgets.DrawHighlight(row);

            GUI.DrawTexture(new Rect(row.x + 4f, row.y + 4f, 28f, 28f), NetrunTex.HeatIcon(each));

            string range = each == HeatLevel.RedZone
                ? $"{each.MinHeat():0}–100"
                : $"{each.MinHeat():0}–{(each + 1).MinHeat() - 1:0}";

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x + 40f, row.y, 220f, row.height),
                $"{each.Label().Colorize(each.Color())} ({range})");
            Widgets.Label(new Rect(row.x + 260f, row.y, row.width - 260f, row.height), each.Description());
            Text.Anchor = TextAnchor.UpperLeft;

            y += LevelRowHeight;
        }

        // What can be hacked at all: new players asked.
        y += 8f;
        Widgets.DrawLineHorizontal(rect.x, y, rect.width);
        y += 8f;
        GUI.color = NetrunTex.DimText;
        Widgets.Label(new Rect(rect.x, y, rect.width, rect.yMax - y), "NR_TerminalHackHint".Translate());
        GUI.color = Color.white;
    }

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
            GUI.color = NetrunTex.DimText;
            Widgets.Label(new Rect(row.x + 6f, row.y, 150f, row.height), ago);
            GUI.color = Color.white;
            var textRect = new Rect(row.x + 160f, row.y, row.width - 230f, row.height);
            Widgets.Label(textRect, entry.Text.Truncate(textRect.width));
            TooltipHandler.TipRegion(textRect, entry.Text);
            Widgets.Label(new Rect(row.xMax - 64f, row.y, 60f, row.height), amount);
            Text.Anchor = TextAnchor.UpperLeft;

            y += JournalRowHeight;
        }

        Widgets.EndScrollView();
    }

    private static void DrawComingSoon(Rect rect)
    {
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = NetrunTex.DimText;
        Widgets.Label(rect, "NR_TerminalComingSoon".Translate());
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
    }
}
