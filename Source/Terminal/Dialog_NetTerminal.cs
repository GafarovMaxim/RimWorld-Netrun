using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Netrun;

/// <summary>
/// The network terminal as a node map (docs/TERMINAL_UI.md, mockup 1-core-map): the Core in the middle,
/// every section is a node around it. A node opens its view inside the window; "Core" goes back.
/// </summary>
public partial class Dialog_NetTerminal : Window
{
    private enum View
    {
        Map,
        Core,
        Club,
        Board,
        Archive,
        Server,
        Studio,
        Gateway,
    }

    private const float TitleHeight = 44f;
    private const float StatusHeight = 40f;
    private const float JournalRowHeight = 30f;
    private const float LevelRowHeight = 36f;
    private const float NodeSize = 84f;

    private readonly Map _map;
    private View _view = View.Map;
    private Vector2 _journalScroll;
    private Vector2 _fixersScroll;
    private Vector2 _contractsScroll;
    private Vector2 _aiScroll;
    private Vector2 _studioScroll;

    public override Vector2 InitialSize => new(900f, 720f);
    protected override float Margin => 0f;

    public Dialog_NetTerminal(Map map)
    {
        _map = map;
        doCloseX = false;
        doCloseButton = false;
        draggable = true;
        // Paused once on opening (PreOpen); the player may unpause and change speed while it is open.
        forcePause = false;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = true;
        preventCameraMotion = false;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Widgets.DrawBoxSolid(inRect, NetrunUI.BgWindow);
        HeatTracker tracker = HeatTracker.Get();

        DrawTitle(new Rect(inRect.x, inRect.y, inRect.width, TitleHeight), tracker);
        var body = new Rect(inRect.x, inRect.y + TitleHeight, inRect.width, inRect.height - TitleHeight - StatusHeight);
        DrawStatusBar(new Rect(inRect.x, body.yMax, inRect.width, StatusHeight), tracker);

        if (tracker == null)
            return;

        if (_view == View.Map)
        {
            DrawCoreScreen(body.ContractedBy(12f), tracker);
            return;
        }

        // Inside a node: a back button, then the node's own view. A dive replaces the Gateway until it ends.
        Rect content = body.ContractedBy(12f);
        DiveSession dive = Dive;
        if (dive != null)
        {
            string back = dive.Finished ? "NR_DiveBackToGateway".Translate() : "NR_DiveRetreatButton".Translate();
            if (NetrunUI.Button(new Rect(content.x, content.y, 130f, 28f), back, dive.Finished ? NetrunUI.Accent : NetrunUI.Bad, true,
                    dive.Finished ? null : "NR_DiveActTip_Retreat".Translate()))
            {
                if (dive.Finished)
                    GatewayManager.Get()?.ClearFinished();
                else
                    dive.Retreat();
            }
            content.yMin += 40f;
            DiveView.Draw(content, dive);
            return;
        }

        if (NetrunUI.Button(new Rect(content.x, content.y, 90f, 28f), "NR_BackToCore".Translate(), NetrunUI.Accent, true, "NR_TipBackToCore".Translate()))
            _view = View.Map;
        content.yMin += 40f;

        switch (_view)
        {
            case View.Core:
                HeatScreen.Draw(content, _map, tracker, () => OpenNode(View.Club));
                break;
            case View.Club:
                FixersTab.Draw(content, _map, ref _fixersScroll);
                break;
            case View.Board:
                ContractsTab.Draw(content, _map, ref _contractsScroll);
                break;
            case View.Archive:
                DrawJournal(content, tracker);
                break;
            case View.Server:
                AITab.Draw(content, _map, ref _aiScroll);
                break;
            case View.Gateway:
                GatewayView.Draw(content, _map);
                break;
            case View.Studio:
                StudioTab.Draw(content, _map, ref _studioScroll);
                break;
            default:
                DrawCentered(content, "NR_TerminalComingSoon".Translate());
                break;
        }
    }

    /// <summary>The dive in progress, shown while the Gateway is open.</summary>
    private DiveSession Dive
    {
        get
        {
            if (_view != View.Gateway)
                return null;
            GatewayManager gateway = GatewayManager.Get();
            gateway?.ValidateSession();
            return gateway?.Session;
        }
    }

    public override void PreOpen()
    {
        base.PreOpen();
        if (Find.TickManager is { Paused: false } ticks)
            ticks.TogglePaused();
    }

    public override void PostClose()
    {
        base.PostClose();
        GatewayManager.Get()?.ClearFinished();
    }

    // ---- Title and status bar ----

    private void DrawTitle(Rect rect, HeatTracker tracker)
    {
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        DiveSession dive = Dive;
        string title = _view == View.Map
            ? "NR_TerminalTitleMap".Translate()
            : dive != null
                ? "NR_TerminalTitleDive".Translate(dive.Node.Name.ToUpper())
                : "NR_TerminalTitleNode".Translate(NodeLabel(_view).ToUpper());
        Widgets.Label(new Rect(rect.x + 14f, rect.y, rect.width - 28f, rect.height), title.Colorize(NetrunUI.Accent));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        float x = rect.xMax - 12f;
        float y = rect.y + 10f;
        if (_view == View.Map)
        {
            // Core: who would dive and whether the pod is ready, next to the window title.
            GatewayView.HeaderChips(x, y, _map);
        }
        else if (dive != null)
        {
            DiveView.HeaderChips(x, y, dive);
        }
        else if (_view == View.Gateway)
        {
            GatewayView.HeaderChips(x, y, _map);
        }
        else if (_view == View.Board)
        {
            ContractsTab.HeaderChips(x, y);
        }
        else if (_view == View.Server)
        {
            // Awaken button, fragments and AI count live in the title bar of the Server room.
            string awaken = "NR_CommandAwakenAI".Translate();
            float bw = NetrunUI.ButtonWidth(awaken, icon: true);
            if (NetrunUI.Button(new Rect(x - bw, y - 3f, bw, 30f), awaken, NetrunUI.Accent, AIAwakening.CanAwaken(_map),
                    "NR_CommandAwakenAIDesc".Translate(), NetrunTex.AIEmbody))
                AIAwakening.OpenMenu(_map);
            x -= bw + 8f;
            x = NetrunUI.HeaderChip(x, y, "NR_ChipFragments".Translate(AIAwakening.Fragments(_map).Count())) - 8f;
            NetrunUI.HeaderChip(x, y, "NR_ChipAIs".Translate(AIManager.Get()?.AIs.Count ?? 0));
        }
        else if (_view == View.Club)
        {
            x = NetrunUI.HeaderChip(x, y, "NR_ChipCyberdata".Translate(ColonyGoods.Count(_map, NetrunDefOf.USH_Cyberdata),
                ColonyGoods.Count(_map, NetrunDefOf.USH_AdvancedCyberdata))) - 8f;
            NetrunUI.HeaderChip(x, y, "NR_ChipSilver".Translate(ColonyGoods.Count(_map, ThingDefOf.Silver)));
        }

        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width);
        GUI.color = Color.white;
    }

    private void DrawStatusBar(Rect rect, HeatTracker tracker)
    {
        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width);
        GUI.color = Color.white;

        float x = rect.x + 14f;
        Text.Anchor = TextAnchor.MiddleLeft;
        if (tracker != null)
        {
            string trace = "NR_StatusTrace".Translate();
            float w = Text.CalcSize(trace).x;
            Widgets.Label(new Rect(x, rect.y, w, rect.height), trace.Colorize(NetrunUI.Muted));
            x += w + 8f;
            NetrunUI.Bar(new Rect(x, rect.y + (rect.height - 12f) / 2f, 120f, 12f), tracker.Heat / HeatTracker.MaxHeat, tracker.Level.Color());
            TooltipHandler.TipRegion(new Rect(rect.x, rect.y, x + 128f - rect.x, rect.height), "NR_TipTrace".Translate());
            x += 128f;
            HeatLevel level = tracker.Level;
            string heatText = tracker.Heat.ToString("0") + "  " + level.Label().CapitalizeFirst().Colorize(level.Color());
            float hw = Text.CalcSize(heatText).x + 16f;
            Widgets.Label(new Rect(x, rect.y, hw, rect.height), heatText);
            TooltipHandler.TipRegion(new Rect(x, rect.y, hw, rect.height), level.Description());
            x += hw;
            bool online = AIAwakening.Console(_map) != null;
            string net = (online ? "NR_ChipNetOnline" : "NR_ChipNetOffline").Translate().Colorize(online ? NetrunUI.Ok : NetrunUI.Bad);
            float nw = Text.CalcSize(net).x + 16f;
            Widgets.Label(new Rect(x, rect.y, nw, rect.height), net);
            TooltipHandler.TipRegion(new Rect(x, rect.y, nw, rect.height), "NR_TipNet".Translate());
            x += nw;
        }

        int netsick = _map.mapPawns.FreeColonistsSpawned.Count(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick));
        string sick = netsick > 0
            ? "NR_StatusNetsick".Translate(netsick).Colorize(NetrunUI.Warn)
            : "NR_StatusNetsickNone".Translate().Colorize(NetrunUI.Muted);
        float sw = Text.CalcSize(sick).x + 20f;
        Widgets.Label(new Rect(x, rect.y, sw, rect.height), sick);
        TooltipHandler.TipRegion(new Rect(x, rect.y, sw, rect.height), "NR_HeatScreenNetsickTip".Translate());
        x += sw;

        // The latest trace event; a click opens the Archive.
        if (tracker != null && tracker.Log.Count > 0)
        {
            var eventRect = new Rect(x, rect.y, rect.xMax - 14f - x, rect.height);
            string last = tracker.Log[tracker.Log.Count - 1].Text;
            Widgets.Label(eventRect, last.Truncate(eventRect.width).Colorize(NetrunUI.Muted));
            TooltipHandler.TipRegion(eventRect, last + "\n\n" + "NR_StatusEventTip".Translate());
            if (Widgets.ButtonInvisible(eventRect))
                OpenNode(View.Archive);
        }

        Text.Anchor = TextAnchor.UpperLeft;
    }

    // ---- Archive: the Heat journal ----

    private void DrawJournal(Rect rect, HeatTracker tracker)
    {
        IReadOnlyList<HeatLogEntry> log = tracker.Log;
        if (log.Count == 0)
        {
            DrawCentered(rect, "NR_TerminalJournalEmpty".Translate());
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
            Widgets.DrawBoxSolid(row, i % 2 == 0 ? NetrunUI.BgCard : NetrunUI.BgCardAlt);
            if (Mouse.IsOver(row))
                Widgets.DrawHighlight(row);

            string ago = "NR_TimeAgo".Translate((now - entry.Tick).ToStringTicksToPeriod());
            string amount = entry.Amount switch
            {
                > 0.05f => $"+{entry.Amount:0.0}".Colorize(NetrunUI.Bad),
                < -0.05f => $"{entry.Amount:0.0}".Colorize(NetrunUI.Ok),
                _ => string.Empty,
            };

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x + 6f, row.y, 150f, row.height), ago.Colorize(NetrunUI.Muted));
            var textRect = new Rect(row.x + 160f, row.y, row.width - 230f, row.height);
            Widgets.Label(textRect, entry.Text.Truncate(textRect.width));
            TooltipHandler.TipRegion(textRect, entry.Text);
            Widgets.Label(new Rect(row.xMax - 64f, row.y, 60f, row.height), amount);
            Text.Anchor = TextAnchor.UpperLeft;

            y += JournalRowHeight;
        }

        Widgets.EndScrollView();
    }

    private static void DrawCentered(Rect rect, string text)
    {
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, text.Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
    }
}
