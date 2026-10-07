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
        Help,
    }

    private const float TitleHeight = 44f;
    private const float StatusHeight = 40f;
    private const float LevelRowHeight = 36f;
    private const float NodeSize = 84f;

    private readonly Map _map;
    private View _view = View.Map;
    private Vector2 _fixersScroll;
    private Vector2 _contractsScroll;
    private Vector2 _aiScroll;
    private Vector2 _studioScroll;
    private readonly HelpBrowser _help = new();
    private View _viewBeforeHelp = View.Map;

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
        bool font = NetrunFont.Push();
        bool size = NetrunText.Push();
        try
        {
            DrawContents(inRect);
        }
        finally
        {
            NetrunText.Pop(size);
            NetrunFont.Pop(font);
        }
    }

    private void DrawContents(Rect inRect)
    {
        _drawTimer.Restart();
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F1)
        {
            OpenHelp();
            Event.current.Use();
        }
        // The cyberdeck casing outside, the interface on its screen.
        inRect = NetrunUI.Bezel(inRect, 12f, "NR_DeckPlate".Translate());
        GuideInput();
        GuideRegistry.Begin();
        DrawWindow(inRect);
        GuideRegistry.End();
        NetrunUI.Scanlines(inRect);
        DrawGuideOverlay(inRect);
        DrawAmbientGlitch(inRect);
        if (Prefs.DevMode && Event.current.type == EventType.Repaint)
        {
            _drawMs = Mathf.Lerp(_drawMs, (float)_drawTimer.Elapsed.TotalMilliseconds, 0.1f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerRight;
            Widgets.Label(new Rect(inRect.xMax - 160f, inRect.yMax - StatusHeight - 18f, 156f, 16f), $"dev: draw {_drawMs:0.00} ms".Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }

    private readonly System.Diagnostics.Stopwatch _drawTimer = new();
    private float _drawMs;

    /// <summary>In the Red Zone the terminal flickers now and then: a few torn slices for a tenth of a second.</summary>
    private static void DrawAmbientGlitch(Rect rect)
    {
        if (!NetrunMod.Settings.Animations || HeatTracker.Get()?.Level != HeatLevel.RedZone)
            return;
        float f = Time.realtimeSinceStartup / 9f;
        float t = (f - Mathf.Floor(f)) * 9f;
        if (t < 0.12f)
            NetrunUI.Glitch(rect, t / 0.12f, Mathf.FloorToInt(f), 5);
    }

    /// <summary>
    /// The bottom bar re-reads itself when the screen changes: for a moment its words are scrambled glyphs that settle
    /// letter by letter from the left.
    /// </summary>
    private string Decode(string text)
    {
        float k = (Time.realtimeSinceStartup - _titleChangedAt) / 0.4f;
        if (!NetrunMod.Settings.Animations || k >= 1f || text.NullOrEmpty())
            return text;
        const string glyphs = "01#$%&*+=?/|[]{}~^";
        int settled = Mathf.FloorToInt(text.Length * Mathf.Clamp01(k));
        var rnd = new System.Random(Time.frameCount / 3 + text.Length);
        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
            sb.Append(i < settled || char.IsWhiteSpace(text[i]) ? text[i] : glyphs[rnd.Next(glyphs.Length)]);
        return sb.ToString();
    }

    private View _titleView;
    private float _titleChangedAt = -10f;

    private void DrawWindow(Rect inRect)
    {
        Widgets.DrawBoxSolid(inRect, NetrunUI.BgWindow);
        HeatTracker tracker = HeatTracker.Get();

        DrawTitle(new Rect(inRect.x, inRect.y, inRect.width, TitleHeight), tracker);
        var body = new Rect(inRect.x, inRect.y + TitleHeight, inRect.width, inRect.height - TitleHeight - StatusHeight);
        var statusRect = new Rect(inRect.x, body.yMax, inRect.width, StatusHeight);
        GuideRegistry.Area("terminal.status", statusRect);
        DrawStatusBar(statusRect, tracker);

        if (tracker == null)
            return;

        if (_view == View.Help)
        {
            // The Cyberpedia must never take the terminal down: on an error, log once and go back.
            try
            {
                _help.Draw(body);
            }
            catch (System.Exception e)
            {
                Log.ErrorOnce("[Netrun] Cyberpedia failed to draw, back to the previous screen: " + e, 0x4E52_4350);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                LeaveHelp();
            }
            return;
        }

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
            GuideRegistry.Area("dive.retreat", new Rect(content.x, content.y, 130f, 28f));
            content.yMin += 40f;
            DiveView.Draw(content, dive);
            return;
        }

        // List screens: the whole content is one guide area.
        switch (_view)
        {
            case View.Board: GuideRegistry.Area("board.list", content); break;
            case View.Studio: GuideRegistry.Area("studio.main", content); break;
        }

        switch (_view)
        {
            case View.Core:
                HeatScreen.Draw(content, _map, tracker, () => OpenNode(View.Club), () => OpenNode(View.Server),
                    page => OpenHelp(DefDatabase<HelpPageDef>.GetNamedSilentFail(page)), () => OpenNode(View.Archive));
                break;
            case View.Club:
                FixersTab.Draw(content, _map, ref _fixersScroll);
                break;
            case View.Board:
                ContractsTab.Draw(content, _map, ref _contractsScroll, page => OpenHelp(DefDatabase<HelpPageDef>.GetNamedSilentFail(page)));
                break;
            case View.Archive:
                ArchiveTab.Draw(content, _map, tracker, page => OpenHelp(DefDatabase<HelpPageDef>.GetNamedSilentFail(page)), () => OpenNode(View.Core));
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

    /// <summary>The help context of the screen that is open now.</summary>
    private string HelpContext => Dive != null ? "Dive" : _view switch
    {
        View.Help => "Help",
        View.Map => NetworkGate.Get()?.OnboardingDone == false ? "Onboarding" : "Core",
        View.Core => "Heat",
        _ => _view.ToString(),
    };

    /// <summary>"> open:" links of the help: a screen by name.</summary>
    private void OpenScreenByName(string name)
    {
        if (name == "Heat")
            name = nameof(View.Core);
        if (name == "Core")
            name = nameof(View.Map);
        if (System.Enum.TryParse(name, out View view))
            OpenNode(view);
    }

    /// <summary>"?" / F1: the Cyberpedia on this screen's page; again (or Esc) goes back where it was opened.</summary>
    private void OpenHelp(HelpPageDef page = null)
    {
        if (_view == View.Help && page == null)
        {
            LeaveHelp();
            return;
        }
        if (_view != View.Help)
        {
            _help.FromLabel = HelpContext == "Dive" ? (string)"NR_HelpCtx_Dive".Translate() : (string)("NR_HelpCtx_" + HelpContext).Translate();
            page ??= HelpPageDef.ForContext(HelpContext);
            _viewBeforeHelp = _view;
        }
        _guide = false;
        _help.OpenScreen = name => OpenScreenByName(name);
        _help.Leave = LeaveHelp;
        _help.Show(page);
        _view = View.Help;
    }

    private void LeaveHelp()
    {
        if (_view == View.Help)
            _view = _viewBeforeHelp;
    }

    /// <summary>The Gateway's home node: back to the main screen.</summary>
    public void GoHome()
    {
        _view = View.Map;
        _archiveFromGateway = false;
    }

    /// <summary>A link in the terminal's text: the Cyberpedia on that page (in the open terminal).</summary>
    public static void OpenPage(HelpPageDef page)
    {
        if (page != null)
            Find.WindowStack.WindowOfType<Dialog_NetTerminal>()?.OpenHelp(page);
    }

    /// <summary>From outside the terminal (the dive summary): open it on a Cyberpedia page.</summary>
    public static void OpenCyberpedia(Map map, string context)
    {
        if (map == null)
            return;
        NetTerminalUtility.OpenTerminal(map);
        Find.WindowStack.WindowOfType<Dialog_NetTerminal>()?.OpenHelp(HelpPageDef.ForContext(context));
    }

    private void DrawTitle(Rect rect, HeatTracker tracker)
    {
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        DiveSession dive = Dive;
        string title = _view == View.Help
            ? "NR_HelpTitle".Translate()
            : _view == View.Map
            ? "NR_TerminalTitleMap".Translate()
            : dive != null
                ? "NR_TerminalTitleDive".Translate(dive.Node.Name.ToUpper())
                : "NR_TerminalTitleNode".Translate(NodeLabel(_view).ToUpper());
        if (_titleView != _view)
        {
            _titleView = _view;
            _titleChangedAt = Time.realtimeSinceStartup;
        }
        float titleX = rect.x + 14f;
        // Inside a node (not a dive, not the Cyberpedia): the way back sits before the title.
        if (_view != View.Map && _view != View.Help && dive == null)
        {
            bool backToGateway = _view == View.Archive && _archiveFromGateway;
            string backLabel = backToGateway ? "NR_BackToGateway".Translate() : "NR_BackToCore".Translate();
            Text.Font = GameFont.Small;
            float backW = Mathf.Max(90f, NetrunUI.ButtonWidth(backLabel));
            var back = new Rect(titleX, rect.center.y - 15f, backW, 30f);
            GuideRegistry.Area("terminal.back", back.ExpandedBy(3f));
            if (NetrunUI.Button(back, backLabel, NetrunUI.Accent, true, backToGateway ? null : "NR_TipBackToCore".Translate()))
            {
                _view = backToGateway ? View.Gateway : View.Map;
                _archiveFromGateway = false;
            }
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            titleX = back.xMax + 14f;
        }
        var titleRect = new Rect(titleX, rect.y, rect.xMax - 14f - titleX, rect.height);
        float since = Time.realtimeSinceStartup - _titleChangedAt;
        if (NetrunMod.Settings.Animations && since < 0.28f)
        {
            // A short RGB split as the screen changes.
            float k = 1f - since / 0.28f;
            Widgets.Label(new Rect(titleRect.x - 3f * k, titleRect.y, titleRect.width, titleRect.height), title.Colorize(new Color(1f, 0.2f, 0.5f, 0.6f * k)));
            Widgets.Label(new Rect(titleRect.x + 3f * k, titleRect.y, titleRect.width, titleRect.height), title.Colorize(new Color(0.2f, 0.9f, 1f, 0.6f * k)));
        }
        Widgets.Label(titleRect, title.Colorize(NetrunUI.Accent));
        if (NetrunMod.Settings.Animations && Time.realtimeSinceStartup % 1.1f < 0.55f)
        {
            float cw = Text.CalcSize(title).x;
            Widgets.DrawBoxSolid(new Rect(titleRect.x + cw + 4f, titleRect.center.y - 8f, 9f, 16f), new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.8f));
        }
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        float x = rect.xMax - 12f;
        float y = rect.y + 10f;

        // "?": the encyclopedia on this screen's topic (F1). Pulses until the help has been opened once.
        var help = new Rect(x - 30f, y - 3f, 30f, 30f);
        if (HelpProgress.Get()?.HelpOpened == false && NetrunMod.Settings.Animations)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 3f);
            Widgets.DrawBoxSolid(help.ExpandedBy(3f), new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.15f + 0.25f * pulse));
        }
        if (_view == View.Help)
            Widgets.DrawBoxSolid(help, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.3f));
        Texture2D wiki = NetrunTex.TabIconByName("Tab_Cyberpedia");
        if (NetrunUI.Button(help, wiki == null ? "?" : string.Empty, NetrunUI.Accent, true, (_view == View.Help ? "NR_HelpLeaveTip" : "NR_HelpButtonTip").Translate()))
            OpenHelp();
        if (wiki != null)
            GUI.DrawTexture(help.ContractedBy(1f), wiki);
        var guideRect = new Rect(help.x - 36f, y - 3f, 30f, 30f);
        if (_view != View.Help && !_guide && HelpProgress.Get()?.GuideScreenSeen(HelpContext) == false && NetrunMod.Settings.Animations)
        {
            float gp = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 3f + 1f);
            Widgets.DrawBoxSolid(guideRect.ExpandedBy(3f), new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.1f + 0.2f * gp));
        }
        if (_view != View.Help && GuideIconButton(guideRect))
            ToggleGuide();
        x = (_view == View.Help ? help.x : guideRect.x) - 10f;
        if (_view == View.Help)
            _help.DrawSearch(new Rect(x - 240f, y - 1f, 240f, 26f));
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
                    "NR_CommandAwakenAIDesc".Translate(), NetrunTex.AIAwaken))
                AIAwakening.OpenMenu(_map);
            x -= bw + 8f;
            x = NetrunUI.HeaderChip(x, y, "NR_ChipFragments".Translate(AIAwakening.Fragments(_map).Count())) - 8f;
            NetrunUI.HeaderChip(x, y, "NR_ChipAIs".Translate(AIManager.Get()?.AIs.Count ?? 0));
        }
        else if (_view == View.Archive && tracker != null)
        {
            NetrunUI.HeaderChip(x, y, "NR_ChipArchiveEvents".Translate(tracker.Log.Count));
        }
        else if (_view == View.Club)
        {
            if (UniqueBraindanceManager.Get() is { MarketDiscountActive: true } ub)
                x = NetrunUI.HeaderChip(x, y, "NR_ChipMarketDiscount".Translate(NetrunDefOf.NR_UniqueBraindance.marketDiscount.ToStringPercent()).Colorize(NetrunUI.Ok),
                    "NR_UB_MarketDiscount".Translate(NetrunDefOf.NR_UniqueBraindance.marketDiscount.ToStringPercent(), ub.MarketDiscountTicksLeft.ToStringTicksToPeriod())) - 8f;
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
            Widgets.Label(new Rect(sx, seg.y, labelW + 2f, seg.height), Decode(((string)"NR_StatusTrace".Translate()).ToUpper()).Colorize(NetrunUI.Muted));
            sx += labelW + 8f;
            Text.Font = GameFont.Small;
            NetrunUI.Bar(new Rect(sx, seg.center.y - 5f, 100f, 10f), tracker.Heat / HeatTracker.MaxHeat, levelColor);
            sx += 108f;
            Widgets.Label(new Rect(sx, seg.y, valueW + 4f, seg.height), value.Colorize(levelColor));
            sx += valueW + 6f;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(sx, seg.y, levelW + 4f, seg.height), Decode(levelLabel).Colorize(levelColor));
            Text.Font = GameFont.Small;
            NetrunUI.Tip(seg, "NR_TipTrace".Translate() + "\n\n" + level.Description() + "\n\n" + "NR_StatusTraceClick".Translate());
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
            Widgets.Label(new Rect(netSeg.x + 24f, netSeg.y, netW + 4f, netSeg.height), Decode(net).Colorize(netColor));
            Text.Font = GameFont.Small;
            NetrunUI.Tip(netSeg, "NR_TipNet".Translate());
        }

        // Netsick: how many colonists, the worst severity on hover.
        List<Pawn> sick = NetrunUI.Cached("status.sick." + _map.uniqueID,
            () => _map.mapPawns.FreeColonistsSpawned.Where(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick)).ToList(), 1f);
        Color sickColor = sick.Count > 0 ? NetrunUI.Warn : NetrunUI.Muted;
        string sickText = sick.Count > 0 ? "NR_StatusNetsick".Translate(sick.Count) : "NR_StatusNetsickNone".Translate();
        Text.Font = GameFont.Tiny;
        float sickW = Text.CalcSize(sickText).x;
        Texture2D sickIcon = NetrunTex.CyberIcon("Icon_Netsick");
        float sickPad = sickIcon != null ? 32f : 10f;
        Rect sickSeg = Segment(sickPad + sickW + 10f, sickColor);
        if (sickIcon != null)
            GUI.DrawTexture(new Rect(sickSeg.x + 6f, sickSeg.center.y - 11f, 22f, 22f), sickIcon);
        Widgets.Label(new Rect(sickSeg.x + sickPad, sickSeg.y, sickW + 4f, sickSeg.height), Decode(sickText).Colorize(sickColor));
        Text.Font = GameFont.Small;
        string sickTip = "NR_HeatScreenNetsickTip".Translate();
        if (sick.Count > 0)
            sickTip += "\n\n" + string.Join("\n", sick.Select(p => p.LabelShort + ": " + p.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick).Severity.ToStringPercent()));
        NetrunUI.Tip(sickSeg, sickTip);

        // On the Heat screen: one sentence of advice instead of the latest event.
        if (tracker != null && _view == View.Core && rect.xMax - 10f - x > 80f)
        {
            var adviceSeg = new Rect(x, segY, rect.xMax - 10f - x, segHeight);
            NetrunUI.Box(adviceSeg, NetrunUI.BgWindow, new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.35f));
            Text.Font = GameFont.Tiny;
            string advice = "NR_HeatV2AdvicePrefix".Translate() + " " + HeatScreen.Advice(tracker);
            Widgets.Label(new Rect(adviceSeg.x + 8f, adviceSeg.y, adviceSeg.width - 12f, adviceSeg.height), advice.Truncate(adviceSeg.width - 12f).Colorize(NetrunUI.Ok));
            Text.Font = GameFont.Small;
            NetrunUI.Tip(adviceSeg, advice);
        }
        // In cyberspace: the news line runs instead of the latest trace event.
        else if (_view == View.Gateway && rect.xMax - 10f - x > 80f)
        {
            var newsSeg = new Rect(x, segY, rect.xMax - 10f - x, segHeight);
            GuideRegistry.Area("terminal.news", newsSeg);
            NetrunUI.Box(newsSeg, NetrunUI.BgWindow, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.3f));
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(newsSeg.x + 8f, newsSeg.y, 60f, newsSeg.height), "NR_NewsTag".Translate().Colorize(NetrunUI.Accent));
            float tagW = Text.CalcSize("NR_NewsTag".Translate()).x + 16f;
            List<(string text, Color color)> news = NetrunUI.Cached("gateway.news", GatewayView.News, 2f);
            NetrunUI.Ticker(new Rect(newsSeg.x + tagW, newsSeg.y, newsSeg.width - tagW - 6f, newsSeg.height), news);
            Text.Font = GameFont.Small;
            NetrunUI.Tip(newsSeg, NetrunUI.Cached("gateway.effects", GatewayView.Effects, 1f));
        }
        // The latest trace event fills the rest; a click opens the Archive.
        else if (tracker != null && tracker.Log.Count > 0 && rect.xMax - 10f - x > 80f)
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
            Texture2D trend = entry.Amount > 0.05f ? NetrunTex.HeatRise : entry.Amount < -0.05f ? NetrunTex.HeatReduction : null;
            if (trend != null)
                GUI.DrawTexture(new Rect(eventSeg.x + 4f, eventSeg.center.y - 9f, 18f, 18f), trend);
            else
                Widgets.Label(new Rect(eventSeg.x + 8f, eventSeg.y, 14f, eventSeg.height), "▸".Colorize(NetrunUI.Accent));
            Widgets.Label(new Rect(eventSeg.x + 22f, eventSeg.y, textW, eventSeg.height), Decode(entry.Text.Truncate(textW)));
            if (amount.Length > 0)
                Widgets.Label(new Rect(eventSeg.x + 22f + textW + 4f, eventSeg.y, amountW, eventSeg.height), amount);
            // A wider rect anchored right: fractional window positions while dragging can no longer wrap it.
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(Mathf.Round(eventSeg.xMax - agoW - 40f), eventSeg.y, agoW + 32f, eventSeg.height), ago.Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Small;
            NetrunUI.Tip(eventSeg, entry.Text + "\n\n" + "NR_StatusEventTip".Translate());
            if (Widgets.ButtonInvisible(eventSeg))
                OpenNode(View.Archive);
        }

        Text.Anchor = TextAnchor.UpperLeft;
        float since = Time.realtimeSinceStartup - _titleChangedAt;
        _ = since;
    }

    /// <summary>From the Gateway's report button: the Archive on the dive reports tab.</summary>
    public void OpenArchive(ArchiveTab.Tab tab)
    {
        ArchiveTab.Open(tab);
        _archiveFromGateway = _view == View.Gateway;
        OpenNode(View.Archive);
    }

    private bool _archiveFromGateway;

    private static void DrawCentered(Rect rect, string text)
    {
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, NetrunUI.Prompt(text).Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
    }
}
