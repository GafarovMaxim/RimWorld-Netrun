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
        Menu,
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
    // The screen's minimum plus the field-deck casing around it.
    private static readonly Vector2 MinSize = new(960f, 760f);
    /// <summary>The size the player left the window at (kept for the session).</summary>
    private static Vector2 _savedSize;
    /// <summary>Window.resizer is private; set it to give the window a minimum size.</summary>
    private static readonly HarmonyLib.AccessTools.FieldRef<Window, WindowResizer> ResizerRef = HarmonyLib.AccessTools.FieldRefAccess<Window, WindowResizer>("resizer");

    public override Vector2 InitialSize
    {
        get
        {
            Vector2 wanted = _savedSize != Vector2.zero ? _savedSize : new Vector2(1100f, 880f) * NetrunMod.Settings.TerminalScale;
            return new Vector2(Mathf.Clamp(wanted.x, Mathf.Min(MinSize.x, UI.screenWidth - 20f), UI.screenWidth - 20f),
                Mathf.Clamp(wanted.y, Mathf.Min(MinSize.y, UI.screenHeight - 20f), UI.screenHeight - 20f));
        }
    }
    protected override float Margin => 0f;

    /// <summary>The screen the terminal was closed on, to open there again (kept for the session, not in the save).</summary>
    private static View _lastView = View.Map;

    public Dialog_NetTerminal(Map map, bool gateway = false)
    {
        _map = map;
        // Straight to the Gateway only once it is open; a locked section falls back to the navigation screen.
        View wanted = gateway ? View.Gateway : _lastView is View.Help or View.Menu ? View.Map : _lastView;
        _view = wanted == View.Map || NetworkGate.Get()?.IsUnlocked(wanted.ToString()) == true ? wanted : View.Map;
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
        // W A S D pan the cyberspace map, not the colony camera, while the map is open (option in the settings).
        preventCameraMotion = _view == View.Gateway && Dive == null && GatewayView.HoldsCamera;
        bool font = NetrunFont.Push();
        bool size = NetrunText.Push();
        try
        {
            // The loop of this screen: the dive, the Gateway's open net, or the terminal's hum.
            // One continuous loop for the whole terminal (the cyberspace one): it never restarts when the screen changes.
            NetrunSound.Ambient("Ambient_Cyberspace");
            // The interface grows with the window (Maxim, 2026-10-09): past the base size everything is drawn at a
            // larger scale, so a big terminal is not the same small layout with more empty room.
            float scale = ContentScale(inRect.size);
            if (scale > 1.001f)
            {
                Matrix4x4 old = GUI.matrix;
                try
                {
                    GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), inRect.position);
                    DrawContents(new Rect(inRect.x, inRect.y, inRect.width / scale, inRect.height / scale));
                }
                finally
                {
                    GUI.matrix = old;
                }
            }
            else
                DrawContents(inRect);
        }
        finally
        {
            NetrunText.Pop(size);
            NetrunFont.Pop(font);
        }
    }

    /// <summary>The layout's base size (the default window at 100%) and how far the content may grow.</summary>
    private static readonly Vector2 BaseContentSize = new(1100f, 880f);
    private const float MaxContentScale = 1.6f;

    private static float ContentScale(Vector2 size)
    {
        if (!NetrunMod.Settings.TerminalAutoScale)
            return 1f;
        float s = Mathf.Min(size.x / BaseContentSize.x, size.y / BaseContentSize.y);
        // Steps of 5% keep text from shimmering while the window is dragged larger.
        return Mathf.Clamp(Mathf.Floor(s * 20f) / 20f, 1f, MaxContentScale);
    }

    private void DrawContents(Rect inRect)
    {
        _drawTimer.Restart();
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F1)
        {
            OpenHelp();
            Event.current.Use();
        }
        // The rugged cyberdeck casing outside, the interface on its screen. Its hardware keys: back (out of the
        // menu, the Cyberpedia or a node), home, the Cyberpedia; on top close, the menu and the achievements.
        HeatTracker heat = HeatTracker.Get();
        inRect = NetrunUI.DeckBezel(inRect, "NR_DeckPlate".Translate(), (heat?.Heat ?? 0f) / HeatTracker.MaxHeat, NetworkGate.IsOpen, key =>
        {
            switch (key)
            {
                case NetrunUI.KeyClose:
                    Close();
                    break;
                case NetrunUI.KeyMenu:
                    if (_view == View.Menu && _menuPage == MenuPage.Home)
                        LeaveMenu();
                    else
                        OpenMenu();
                    break;
                case NetrunUI.KeyAchievements:
                    OpenMenu(MenuPage.Achievements);
                    break;
                case NetrunUI.KeyHelp:
                    OpenHelp();
                    break;
                case NetrunUI.KeyCore:
                    // Core from anywhere: the menu, the Cyberpedia, a section, the Gateway (a dive waits there, paused).
                    _menuPage = MenuPage.Home;
                    _guide = false;
                    GoHome();
                    break;
                default:
                    if (_view == View.Menu)
                        LeaveMenu();
                    else if (_view == View.Help)
                        LeaveHelp();
                    else if (Dive == null)
                        GoHome();
                    break;
            }
        });
        _guideScreen = inRect;
        GuideInput();
        GuideRegistry.Begin(_guide);
        NetrunSound.HoverMuted = _guide;
        DrawWindow(inRect);
        GuideRegistry.End();
        NetrunUI.Scanlines(inRect);
        NetrunUI.DeckGlass(inRect);
        DrawGuideOverlay(inRect);
        MaybeStartIntro();
        DrawIntro(inRect);
        DrawAmbientGlitch(inRect);
        if (Prefs.DevMode && Event.current.type == EventType.Repaint)
        {
            float ms = (float)_drawTimer.Elapsed.TotalMilliseconds;
            _drawMs = Mathf.Lerp(_drawMs, ms, 0.1f);
            _drawSum += ms;
            _drawCount++;
            _drawMax = Mathf.Max(_drawMax, ms);
            if (Time.realtimeSinceStartup - _drawWindowAt >= 5f)
            {
                _drawShown = $"dev: draw {_drawMs:0.00} ms · 5s avg {_drawSum / Mathf.Max(1, _drawCount):0.00} max {_drawMax:0.00}" + (_guide ? " · guide" : "");
                _drawSum = 0f;
                _drawCount = 0;
                _drawMax = 0f;
                _drawWindowAt = Time.realtimeSinceStartup;
                Log.Message($"[Netrun] {HelpContext}: {_drawShown}");
            }
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerRight;
            Widgets.Label(new Rect(inRect.xMax - 420f, inRect.yMax - StatusHeight - 18f, 416f, 16f), (_drawShown ?? $"dev: draw {_drawMs:0.00} ms").Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }

    private readonly System.Diagnostics.Stopwatch _drawTimer = new();
    private float _drawMs;
    // The 5-second window of the dev measure (A1): sum, count and maximum, shown and logged every 5 s.
    private float _drawSum, _drawMax, _drawWindowAt;
    private int _drawCount;
    private string _drawShown;

    /// <summary>In the Red Zone the terminal flickers now and then: a few torn slices for a tenth of a second.</summary>
    private static void DrawAmbientGlitch(Rect rect)
    {
        if (!NetrunMod.Settings.Animations || !NetrunMod.Settings.Glitches || HeatTracker.Get()?.Level != HeatLevel.RedZone)
            return;
        // Rarely and briefly: a soft flicker every 14 seconds or so.
        float f = Time.realtimeSinceStartup / 14f;
        float t = (f - Mathf.Floor(f)) * 14f;
        if (t < 0.25f)
            NetrunUI.Glitch(rect, t / 0.25f, Mathf.FloorToInt(f), 3);
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

        if (_view == View.Menu)
        {
            DrawMenu(body);
            return;
        }

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
                ClubTabs.Draw(content, _map, ref _fixersScroll);
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

    /// <summary>The dive in progress, shown while the Gateway is open (looked up once a frame: PERF_AUDIT_4, 27).</summary>
    private DiveSession Dive
    {
        get
        {
            if (_view != View.Gateway)
                return null;
            if (_diveFrame != Time.frameCount)
            {
                GatewayManager gateway = GatewayManager.Get();
                gateway?.ValidateSession();
                _dive = gateway?.Session;
                _diveFrame = Time.frameCount;
            }
            return _dive;
        }
    }

    private DiveSession _dive;
    private int _diveFrame = -1;
    private string _helpContext;
    private int _helpContextFrame = -1;
    private View _helpContextView;
    private MenuPage _helpContextPage;

    public override void PreOpen()
    {
        base.PreOpen();
        ResizerRef(this) = new WindowResizer { minWindowSize = new Vector2(Mathf.Min(MinSize.x, UI.screenWidth - 20f), Mathf.Min(MinSize.y, UI.screenHeight - 20f)) };
        if (NetrunMod.Settings.PauseOnOpen && Find.TickManager is { Paused: false } ticks)
            ticks.TogglePaused();
    }

    public override void PostOpen()
    {
        base.PostOpen();
        NetrunSound.Play("UI_Open");
    }

    public override void PostClose()
    {
        base.PostClose();
        NetrunSound.Play("UI_Close");
        NetrunSound.EndAmbient();
        // What was changed in the menu's settings pages.
        NetrunMod.Settings.Write();
        _lastView = _view;
        _savedSize = windowRect.size;
        GatewayManager.Get()?.ClearFinished();
    }

    // ---- Title and status bar ----

    /// <summary>The help context of the screen that is open now.</summary>
    private string HelpContext
    {
        get
        {
            if (_helpContextFrame != Time.frameCount || _helpContextView != _view || _helpContextPage != _menuPage)
            {
                _helpContext = HelpContextNow;
                _helpContextFrame = Time.frameCount;
                _helpContextView = _view;
                _helpContextPage = _menuPage;
            }
            return _helpContext;
        }
    }

    private string HelpContextNow => Dive != null ? "Dive" : _view switch
    {
        View.Help => "Help",
        View.Menu => _menuPage == MenuPage.Home ? "Menu" : "Menu" + _menuPage,
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
    /// <summary>Opens the Gateway with a map node selected (from the Club's "On the map").</summary>
    public void OpenGatewayAt(int mapNodeId)
    {
        if (mapNodeId >= 0)
            GatewayView.SelectMapNode(mapNodeId);
        OpenNode(View.Gateway);
    }

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
        string title = _view == View.Menu
            ? MenuTitle
            : _view == View.Help
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
            NetrunSound.Play("UI_Tab");
            NetrunSound.Glitch();
        }
        float titleX = rect.x + 14f;
        // Inside a node (not a dive, not the Cyberpedia): the way back sits before the title.
        if (_view != View.Map && _view != View.Help && dive == null)
        {
            bool backToGateway = _view == View.Archive && _archiveFromGateway;
            bool menu = _view == View.Menu;
            string backLabel = menu ? (_menuPage == MenuPage.Home ? "NR_MenuBack".Translate() : "NR_MenuBackToMenu".Translate())
                : backToGateway ? "NR_BackToGateway".Translate() : "NR_BackToCore".Translate();
            Text.Font = GameFont.Small;
            float backW = Mathf.Max(90f, NetrunUI.ButtonWidth(backLabel));
            var back = new Rect(titleX, rect.center.y - 15f, backW, 30f);
            GuideRegistry.Area("terminal.back", back.ExpandedBy(3f));
            if (NetrunUI.Button(back, backLabel, NetrunUI.Accent, true, backToGateway || menu ? null : "NR_TipBackToCore".Translate()))
            {
                if (menu)
                {
                    LeaveMenu();
                }
                else
                {
                    _view = backToGateway ? View.Gateway : View.Map;
                    _archiveFromGateway = false;
                }
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
        // The menu button (the casing's MENU key does the same).
        var menuRect = new Rect(help.x - 36f, y - 3f, 30f, 30f);
        if (_view == View.Menu)
            Widgets.DrawBoxSolid(menuRect, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.3f));
        Texture2D menuIcon = NetrunTint.Of(NetrunTex.MenuMain);
        if (NetrunUI.Button(menuRect, menuIcon == null ? "≡" : string.Empty, NetrunUI.Accent, true, "NR_MenuButtonTip".Translate()))
        {
            if (_view == View.Menu)
                LeaveMenu();
            else
                OpenMenu();
        }
        if (menuIcon != null)
        {
            GUI.DrawTexture(menuRect.ContractedBy(3f), menuIcon);
        }
        _helpRect = new Rect(help.x, help.y, 30f, 30f);
        help.x = menuRect.x;
        var guideRect = new Rect(help.x - 36f, y - 3f, 30f, 30f);
        _guideRectSeen = guideRect;
        if (_view != View.Help && !_guide && HelpProgress.Get()?.GuideScreenSeen(HelpContext) == false && NetrunMod.Settings.Animations)
        {
            float gp = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 3f + 1f);
            Widgets.DrawBoxSolid(guideRect.ExpandedBy(3f), new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.1f + 0.2f * gp));
        }
        bool guideShown = _view != View.Help;
        if (guideShown && GuideIconButton(guideRect))
            ToggleGuide();
        x = (guideShown ? guideRect.x : help.x) - 10f;
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
            x = NetrunUI.HeaderChip(x, y, "NR_ChipCyberdata".Translate(ColonyGoods.CountCached(_map, NetrunDefOf.USH_Cyberdata),
                ColonyGoods.CountCached(_map, NetrunDefOf.USH_AdvancedCyberdata))) - 8f;
            NetrunUI.HeaderChip(x, y, "NR_ChipSilver".Translate(ColonyGoods.CountCached(_map, ThingDefOf.Silver)));
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
            string traceLabel = NetrunUI.Cached(TraceLabelKey, () => NetrunUI.T("NR_StatusTrace").ToUpper(), 5f);
            Text.Font = GameFont.Tiny;
            float labelW = NetrunUI.Width(traceLabel);
            float levelW = NetrunUI.Width(levelLabel);
            Text.Font = GameFont.Small;
            float valueW = NetrunUI.Width(value);
            Rect seg = Segment(8f + 18f + 6f + labelW + 8f + 100f + 8f + valueW + 6f + levelW + 10f, levelColor);
            float sx = seg.x + 8f;
            GUI.DrawTexture(new Rect(sx, seg.center.y - 9f, 18f, 18f), NetrunTex.HeatIcon(level));
            sx += 24f;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(sx, seg.y, labelW + 2f, seg.height), Decode(traceLabel).Colorize(NetrunUI.Muted));
            sx += labelW + 8f;
            Text.Font = GameFont.Small;
            NetrunUI.Bar(new Rect(sx, seg.center.y - 5f, 100f, 10f), tracker.Heat / HeatTracker.MaxHeat, levelColor);
            sx += 108f;
            Widgets.Label(new Rect(sx, seg.y, valueW + 4f, seg.height), value.Colorize(levelColor));
            sx += valueW + 6f;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(sx, seg.y, levelW + 4f, seg.height), Decode(levelLabel).Colorize(levelColor));
            Text.Font = GameFont.Small;
            if (Mouse.IsOver(seg))
                NetrunUI.Tip(seg, "NR_TipTrace".Translate() + "\n\n" + level.Description() + "\n\n" + "NR_StatusTraceClick".Translate());
            if (Widgets.ButtonInvisible(seg))
                OpenNode(View.Core);

            // Network: a status dot and the word.
            Map map = _map;
            bool online = NetrunUI.Cached(ConsoleKey, () => AIAwakening.Console(map) != null, 1f);
            Color netColor = online ? NetrunUI.Ok : NetrunUI.Bad;
            string net = NetrunUI.T(online ? "NR_StatusNetOnline" : "NR_StatusNetOffline");
            Text.Font = GameFont.Tiny;
            float netW = NetrunUI.Width(net);
            Rect netSeg = Segment(8f + 10f + 6f + netW + 10f, netColor);
            GUI.color = netColor;
            GUI.DrawTexture(new Rect(netSeg.x + 8f, netSeg.center.y - 5f, 10f, 10f), NetrunUI.Disc);
            GUI.color = Color.white;
            Widgets.Label(new Rect(netSeg.x + 24f, netSeg.y, netW + 4f, netSeg.height), Decode(net).Colorize(netColor));
            Text.Font = GameFont.Small;
            NetrunUI.Tip(netSeg, NetrunUI.T("NR_TipNet"));
        }

        // Netsick: how many colonists, the worst severity on hover.
        Map sickMap = _map;
        List<Pawn> sick = NetrunUI.Cached(SickKey,
            () => sickMap.mapPawns.FreeColonistsSpawned.Where(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick)).ToList(), 1f);
        Color sickColor = sick.Count > 0 ? NetrunUI.Warn : NetrunUI.Muted;
        string sickText = sick.Count > 0 ? "NR_StatusNetsick".Translate(sick.Count) : NetrunUI.T("NR_StatusNetsickNone");
        Text.Font = GameFont.Tiny;
        float sickW = NetrunUI.Width(sickText);
        Texture2D sickIcon = NetrunTex.CyberIcon("Icon_Netsick");
        float sickPad = sickIcon != null ? 32f : 10f;
        Rect sickSeg = Segment(sickPad + sickW + 10f, sickColor);
        if (sickIcon != null)
            GUI.DrawTexture(new Rect(sickSeg.x + 6f, sickSeg.center.y - 11f, 22f, 22f), sickIcon);
        Widgets.Label(new Rect(sickSeg.x + sickPad, sickSeg.y, sickW + 4f, sickSeg.height), Decode(sickText).Colorize(sickColor));
        Text.Font = GameFont.Small;
        // Tooltip texts are built only under the mouse (Tip ignores them otherwise); before, every GUI event built them.
        if (Mouse.IsOver(sickSeg))
        {
            string sickTip = "NR_HeatScreenNetsickTip".Translate();
            if (sick.Count > 0)
                sickTip += "\n\n" + string.Join("\n", sick.Select(p => p.LabelShort + ": " + p.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick).Severity.ToStringPercent()));
            NetrunUI.Tip(sickSeg, sickTip);
        }

        // On the Heat screen: one sentence of advice instead of the latest event.
        if (tracker != null && _view == View.Core && rect.xMax - 10f - x > 80f)
        {
            var adviceSeg = new Rect(x, segY, rect.xMax - 10f - x, segHeight);
            NetrunUI.Box(adviceSeg, NetrunUI.BgWindow, new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.35f));
            Text.Font = GameFont.Tiny;
            string advice = "NR_HeatV2AdvicePrefix".Translate() + " " + HeatScreen.Advice(tracker);
            Widgets.Label(new Rect(adviceSeg.x + 8f, adviceSeg.y, adviceSeg.width - 12f, adviceSeg.height), advice.Truncate(adviceSeg.width - 12f).Colorize(NetrunUI.Ok));
            Text.Font = GameFont.Small;
            if (Mouse.IsOver(adviceSeg))
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
            string newsTag = NetrunUI.T("NR_NewsTag");
            GUI.color = NetrunUI.Accent;
            Widgets.Label(new Rect(newsSeg.x + 8f, newsSeg.y, 60f, newsSeg.height), newsTag);
            GUI.color = Color.white;
            float tagW = NetrunUI.Width(newsTag) + 16f;
            List<(string text, Color color)> news = NetrunUI.Cached("gateway.news", GatewayView.News, 2f);
            // Pending choices: a chip at the right end of the line opens them.
            int pending = NewsChoices.Get()?.Pending.Count ?? 0;
            float chipW = 0f;
            if (pending > 0)
            {
                string chip = "NR_ChoicesChip".Translate(pending);
                chipW = Text.CalcSize(chip).x + 16f;
                var chipRect = new Rect(newsSeg.xMax - chipW - 4f, newsSeg.y + 2f, chipW, newsSeg.height - 4f);
                Widgets.DrawBoxSolid(chipRect, new Color(NetrunUI.Warn.r, NetrunUI.Warn.g, NetrunUI.Warn.b, Mouse.IsOver(chipRect) ? 0.35f : 0.2f));
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(chipRect, chip.Colorize(NetrunUI.Warn));
                Text.Anchor = TextAnchor.MiddleLeft;
                if (Widgets.ButtonInvisible(chipRect))
                    Find.WindowStack.Add(new Dialog_NewsChoices());
                chipW += 6f;
            }
            NetrunUI.Ticker(new Rect(newsSeg.x + tagW, newsSeg.y, newsSeg.width - tagW - 6f - chipW, newsSeg.height), news);
            Text.Font = GameFont.Small;
            if (Mouse.IsOver(newsSeg))
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
            if (Mouse.IsOver(eventSeg))
                NetrunUI.Tip(eventSeg, entry.Text + "\n\n" + "NR_StatusEventTip".Translate());
            if (Widgets.ButtonInvisible(eventSeg))
                OpenNode(View.Archive);
        }

        Text.Anchor = TextAnchor.UpperLeft;
        float since = Time.realtimeSinceStartup - _titleChangedAt;
        _ = since;
    }

    // Cached keys of the bottom bar (static strings, not built per call: PERF_AUDIT_4, 11 and 12).
    private const string TraceLabelKey = "status.traceLabel", ConsoleKey = "status.console", SickKey = "status.sick";

    /// <summary>From the Gateway's report button: the Archive on the dive reports tab.</summary>
    public void OpenArchive(ArchiveTab.Tab tab)
    {
        ArchiveTab.Open(tab);
        _archiveFromGateway = _view == View.Gateway;
        OpenNode(View.Archive);
    }

    private bool _archiveFromGateway;

    private static void DrawCentered(Rect rect, string text) => NetrunUI.EmptyState(rect, NetrunUI.T("NR_StateOffline"), text, NetrunUI.Muted, blocked: true);
}
