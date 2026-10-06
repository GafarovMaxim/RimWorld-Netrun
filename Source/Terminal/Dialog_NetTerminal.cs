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

    /// <summary>Smallest size the player can drag the window down to.</summary>
    private static readonly Vector2 MinSize = new(860f, 640f);
    /// <summary>The size the player left the window at (kept for the session).</summary>
    private static Vector2 _savedSize;
    /// <summary>Window.resizer is private; set it to give the window a minimum size.</summary>
    private static readonly HarmonyLib.AccessTools.FieldRef<Window, WindowResizer> ResizerRef = HarmonyLib.AccessTools.FieldRefAccess<Window, WindowResizer>("resizer");

    public override Vector2 InitialSize
    {
        get
        {
            Vector2 wanted = _savedSize != Vector2.zero ? _savedSize : new Vector2(1000f, 760f);
            return new Vector2(Mathf.Clamp(wanted.x, Mathf.Min(MinSize.x, UI.screenWidth - 20f), UI.screenWidth - 20f),
                Mathf.Clamp(wanted.y, Mathf.Min(MinSize.y, UI.screenHeight - 20f), UI.screenHeight - 20f));
        }
    }
    protected override float Margin => 0f;

    public Dialog_NetTerminal(Map map)
    {
        _map = map;
        doCloseX = false;
        doCloseButton = false;
        draggable = true;
        resizeable = true;
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
        ResizerRef(this) = new WindowResizer { minWindowSize = new Vector2(Mathf.Min(MinSize.x, UI.screenWidth - 20f), Mathf.Min(MinSize.y, UI.screenHeight - 20f)) };
        if (NetrunMod.Settings.PauseOnOpen && Find.TickManager is { Paused: false } ticks)
            ticks.TogglePaused();
    }

    public override void PostClose()
    {
        base.PostClose();
        _savedSize = windowRect.size;
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
            // Core: who would dive and whether the pod is ready; before the onboarding is done, "network not active".
            if (NetworkGate.Get()?.OnboardingDone == false)
                NetrunUI.HeaderChip(x, y, "NR_ChipNetInactive".Translate().Colorize(NetrunUI.Muted));
            else
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

    /// <summary>
    /// Bottom bar in the terminal style: framed segments for the trace (icon, bar, value, level), the network,
    /// Netsick and the latest trace event. Every segment explains itself on hover; trace opens the Heat screen,
    /// the event opens the Archive.
    /// </summary>
    private void DrawStatusBar(Rect rect, HeatTracker tracker)
    {
        Widgets.DrawBoxSolid(rect, NetrunUI.BgCard);
        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width);
        GUI.color = Color.white;

        const float segHeight = 28f;
        float segY = rect.y + (rect.height - segHeight) / 2f;
        float x = rect.x + 10f;

        Rect Segment(float width, Color border)
        {
            var seg = new Rect(x, segY, width, segHeight);
            NetrunUI.Box(seg, NetrunUI.BgWindow, new Color(border.r, border.g, border.b, 0.45f));
            if (Mouse.IsOver(seg))
                Widgets.DrawHighlight(seg);
            x += width + 8f;
            return seg;
        }

        Text.Anchor = TextAnchor.MiddleLeft;
        if (tracker != null)
        {
            // Trace: level icon, label, bar, value and level name.
            HeatLevel level = tracker.Level;
            Color levelColor = level.Color();
            string value = tracker.Heat.ToString("0");
            string levelLabel = level.Label().CapitalizeFirst();
            Text.Font = GameFont.Tiny;
            float labelW = Text.CalcSize(((string)"NR_StatusTrace".Translate()).ToUpper()).x;
            float levelW = Text.CalcSize(levelLabel).x;
            Text.Font = GameFont.Small;
            float valueW = Text.CalcSize(value).x;
            Rect seg = Segment(8f + 18f + 6f + labelW + 8f + 100f + 8f + valueW + 6f + levelW + 10f, levelColor);
            float sx = seg.x + 8f;
            GUI.DrawTexture(new Rect(sx, seg.center.y - 9f, 18f, 18f), NetrunTex.HeatIcon(level));
            sx += 24f;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(sx, seg.y, labelW + 2f, seg.height), ((string)"NR_StatusTrace".Translate()).ToUpper().Colorize(NetrunUI.Muted));
            sx += labelW + 8f;
            Text.Font = GameFont.Small;
            NetrunUI.Bar(new Rect(sx, seg.center.y - 5f, 100f, 10f), tracker.Heat / HeatTracker.MaxHeat, levelColor);
            sx += 108f;
            Widgets.Label(new Rect(sx, seg.y, valueW + 4f, seg.height), value.Colorize(levelColor));
            sx += valueW + 6f;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(sx, seg.y, levelW + 4f, seg.height), levelLabel.Colorize(levelColor));
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(seg, "NR_TipTrace".Translate() + "\n\n" + level.Description() + "\n\n" + "NR_StatusTraceClick".Translate());
            if (Widgets.ButtonInvisible(seg))
                OpenNode(View.Core);

            // Network: a status dot and the word.
            bool online = AIAwakening.Console(_map) != null;
            Color netColor = online ? NetrunUI.Ok : NetrunUI.Bad;
            string net = (online ? "NR_StatusNetOnline" : "NR_StatusNetOffline").Translate();
            Text.Font = GameFont.Tiny;
            float netW = Text.CalcSize(net).x;
            Rect netSeg = Segment(8f + 10f + 6f + netW + 10f, netColor);
            GUI.color = netColor;
            GUI.DrawTexture(new Rect(netSeg.x + 8f, netSeg.center.y - 5f, 10f, 10f), NetrunUI.Disc);
            GUI.color = Color.white;
            Widgets.Label(new Rect(netSeg.x + 24f, netSeg.y, netW + 4f, netSeg.height), net.Colorize(netColor));
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(netSeg, "NR_TipNet".Translate());
        }

        // Netsick: how many colonists, the worst severity on hover.
        List<Pawn> sick = _map.mapPawns.FreeColonistsSpawned.Where(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick)).ToList();
        Color sickColor = sick.Count > 0 ? NetrunUI.Warn : NetrunUI.Muted;
        string sickText = sick.Count > 0 ? "NR_StatusNetsick".Translate(sick.Count) : "NR_StatusNetsickNone".Translate();
        Text.Font = GameFont.Tiny;
        float sickW = Text.CalcSize(sickText).x;
        Rect sickSeg = Segment(10f + sickW + 10f, sickColor);
        Widgets.Label(new Rect(sickSeg.x + 10f, sickSeg.y, sickW + 4f, sickSeg.height), sickText.Colorize(sickColor));
        Text.Font = GameFont.Small;
        string sickTip = "NR_HeatScreenNetsickTip".Translate();
        if (sick.Count > 0)
            sickTip += "\n\n" + string.Join("\n", sick.Select(p => p.LabelShort + ": " + p.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick).Severity.ToStringPercent()));
        TooltipHandler.TipRegion(sickSeg, sickTip);

        // The latest trace event fills the rest; a click opens the Archive.
        if (tracker != null && tracker.Log.Count > 0 && rect.xMax - 10f - x > 80f)
        {
            HeatLogEntry entry = tracker.Log[tracker.Log.Count - 1];
            var eventSeg = new Rect(x, segY, rect.xMax - 10f - x, segHeight);
            NetrunUI.Box(eventSeg, NetrunUI.BgWindow, new Color(1f, 1f, 1f, 0.12f));
            if (Mouse.IsOver(eventSeg))
                Widgets.DrawHighlight(eventSeg);
            Text.Font = GameFont.Tiny;
            string ago = "NR_TimeAgo".Translate((Find.TickManager.TicksGame - entry.Tick).ToStringTicksToPeriod());
            float agoW = Text.CalcSize(ago).x;
            string amount = entry.Amount > 0.05f ? $"+{entry.Amount:0.#}".Colorize(NetrunUI.Bad) : entry.Amount < -0.05f ? $"{entry.Amount:0.#}".Colorize(NetrunUI.Ok) : string.Empty;
            float amountW = amount.Length > 0 ? Text.CalcSize(amount.StripTags()).x + 8f : 0f;
            float textW = eventSeg.width - 20f - 14f - agoW - amountW - 8f;
            Widgets.Label(new Rect(eventSeg.x + 8f, eventSeg.y, 14f, eventSeg.height), "▸".Colorize(NetrunUI.Accent));
            Widgets.Label(new Rect(eventSeg.x + 22f, eventSeg.y, textW, eventSeg.height), entry.Text.Truncate(textW));
            if (amount.Length > 0)
                Widgets.Label(new Rect(eventSeg.x + 22f + textW + 4f, eventSeg.y, amountW, eventSeg.height), amount);
            Widgets.Label(new Rect(eventSeg.xMax - agoW - 8f, eventSeg.y, agoW + 4f, eventSeg.height), ago.Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(eventSeg, entry.Text + "\n\n" + "NR_StatusEventTip".Translate());
            if (Widgets.ButtonInvisible(eventSeg))
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
