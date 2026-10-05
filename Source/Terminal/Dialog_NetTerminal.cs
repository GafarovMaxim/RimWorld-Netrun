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
public class Dialog_NetTerminal : Window
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
    private const float CoreSize = 110f;

    private readonly Map _map;
    private View _view = View.Map;
    private Vector2 _journalScroll;
    private Vector2 _fixersScroll;
    private Vector2 _contractsScroll;
    private Vector2 _aiScroll;

    public override Vector2 InitialSize => new(900f, 720f);
    protected override float Margin => 0f;

    public Dialog_NetTerminal(Map map)
    {
        _map = map;
        doCloseX = false;
        doCloseButton = false;
        draggable = true;
        forcePause = true;
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
            DrawMap(body.ContractedBy(12f), tracker);
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

        if (NetrunUI.Button(new Rect(content.x, content.y, 90f, 28f), "NR_BackToCore".Translate(), NetrunUI.Accent, true))
            _view = View.Map;
        content.yMin += 40f;

        switch (_view)
        {
            case View.Core:
                DrawOverview(content, tracker);
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
        if (dive != null)
        {
            DiveView.HeaderChips(x, y, dive);
        }
        else if (_view == View.Gateway)
        {
            GatewayView.HeaderChips(x, y, _map);
        }
        else if (_view == View.Club)
        {
            x = NetrunUI.HeaderChip(x, y, "NR_ChipCyberdata".Translate(ColonyGoods.Count(_map, NetrunDefOf.USH_Cyberdata),
                ColonyGoods.Count(_map, NetrunDefOf.USH_AdvancedCyberdata))) - 8f;
            NetrunUI.HeaderChip(x, y, "NR_ChipSilver".Translate(ColonyGoods.Count(_map, ThingDefOf.Silver)));
        }
        else if (tracker != null)
        {
            bool online = AIAwakening.Console(_map) != null;
            x = NetrunUI.HeaderChip(x, y, (online ? "NR_ChipNetOnline" : "NR_ChipNetOffline").Translate()) - 8f;
            HeatLevel level = tracker.Level;
            NetrunUI.HeaderChip(x, y, "NR_ChipHeat".Translate((level.Label() + " " + tracker.Heat.ToString("0")).Colorize(level.Color())));
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
            x += 128f;
            Widgets.Label(new Rect(x, rect.y, 40f, rect.height), tracker.Heat.ToString("0"));
            x += 48f;
        }

        int netsick = _map.mapPawns.FreeColonistsSpawned.Count(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick));
        string sick = netsick > 0
            ? "NR_StatusNetsick".Translate(netsick).Colorize(NetrunUI.Warn)
            : "NR_StatusNetsickNone".Translate().Colorize(NetrunUI.Muted);
        Widgets.Label(new Rect(x, rect.y, 260f, rect.height), sick);

        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(new Rect(rect.xMax - 300f, rect.y, 286f, rect.height), "NR_StatusClickOutside".Translate().Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
    }

    // ---- Node map ----

    private static string NodeLabel(View view) => ("NR_Node_" + view).Translate();

    private void DrawMap(Rect rect, HeatTracker tracker)
    {
        Vector2 center = rect.center;
        float dx = rect.width * 0.36f;
        float dy = rect.height * 0.36f;

        var nodes = new List<(View view, Vector2 pos, Texture2D icon, Color color, string sub, string badge)>
        {
            (View.Board, new Vector2(center.x, center.y - dy * 1.05f), NetrunTex.TabIcon(2), NetrunUI.Ok, BoardSub(), BoardBadge()),
            (View.Club, new Vector2(center.x - dx, center.y - dy * 0.55f), NetrunTex.TabIcon(1), NetrunUI.Warn, ClubSub(), ClubBadge()),
            (View.Archive, new Vector2(center.x + dx, center.y - dy * 0.55f), NetrunTex.TabIcon(5), NetrunUI.Muted, "NR_NodeSub_Archive".Translate(tracker.Log.Count), null),
            (View.Server, new Vector2(center.x - dx, center.y + dy * 0.55f), NetrunTex.TabIcon(3), NetrunUI.Purple, ServerSub(), ServerBadge()),
            (View.Studio, new Vector2(center.x + dx, center.y + dy * 0.55f), NetrunTex.TabIcon(4), StudioColor, "NR_NodeSub_Studio".Translate(), null),
            (View.Gateway, new Vector2(center.x, center.y + dy * 1.0f), NetrunTex.Dive, NetrunUI.Accent, "NR_NodeSub_Gateway".Translate(), null),
        };

        // Links first, so the nodes are drawn over them.
        foreach (var node in nodes)
            Widgets.DrawLine(center, node.pos, new Color(NetrunUI.Line.r, NetrunUI.Line.g, NetrunUI.Line.b, 0.8f), 2f);

        foreach (var node in nodes)
            DrawNode(node.view, node.pos, NodeSize, node.icon, node.color, node.sub, node.badge);

        DrawNode(View.Core, center, CoreSize, NetrunTex.TabIcon(0), NetrunUI.Accent, "NR_NodeSub_Core".Translate(), null);
    }

    private static readonly Color StudioColor = new(1f, 0.45f, 0.35f);

    private void DrawNode(View view, Vector2 pos, float size, Texture2D icon, Color color, string sub, string badge)
    {
        var box = new Rect(pos.x - size / 2f, pos.y - size / 2f - 14f, size, size);
        bool hover = Mouse.IsOver(box);
        NetrunUI.Box(box, hover ? new Color(color.r, color.g, color.b, 0.15f) : NetrunUI.BgWindow, color, hover ? 3 : 2);
        GUI.DrawTexture(box.ContractedBy(size * 0.2f), icon);

        if (!badge.NullOrEmpty())
        {
            var badgeRect = new Rect(box.xMax - 11f, box.y - 9f, 20f, 20f);
            Widgets.DrawBoxSolid(badgeRect, NetrunUI.Bad);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(badgeRect, badge);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        Text.Anchor = TextAnchor.UpperCenter;
        var labelRect = new Rect(pos.x - 110f, box.yMax + 4f, 220f, 22f);
        Widgets.Label(labelRect, NodeLabel(view).ToUpper().Colorize(color));
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(pos.x - 110f, labelRect.yMax, 220f, 18f), sub.Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        TooltipHandler.TipRegion(box, ("NR_NodeTip_" + view).Translate());
        if (Widgets.ButtonInvisible(box))
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            _view = view;
        }
    }

    private static string ClubSub() => "NR_NodeSub_Club".Translate(FixerManager.Get()?.Fixers.Count ?? 0);

    private static string ClubBadge()
    {
        int open = FixerManager.Get()?.Fixers.Count(f => f.ChannelOpen) ?? 0;
        return open > 0 ? open.ToString() : null;
    }

    private static string BoardSub()
    {
        Contract active = ContractManager.Get()?.Active.FirstOrDefault();
        return active == null
            ? "NR_NodeSub_BoardNone".Translate()
            : (string)"NR_NodeSub_Board".Translate(active.Progress, active.Goal);
    }

    private static string BoardBadge()
        => ContractManager.Get()?.Active.Any(c => c.TicksLeft < GenDate.TicksPerDay) == true ? "!" : null;

    private static string ServerSub()
    {
        IReadOnlyList<NetAI> ais = AIManager.Get()?.AIs;
        int count = ais?.Count ?? 0;
        return ais != null && ais.Any(a => a.HasRequest)
            ? "NR_NodeSub_ServerRequest".Translate(count)
            : (string)"NR_NodeSub_Server".Translate(count);
    }

    private static string ServerBadge()
        => AIManager.Get()?.AIs.Any(a => a.HasRequest || a.Escaping) == true ? "!" : null;

    // ---- Core: Heat overview ----

    private static void DrawOverview(Rect rect, HeatTracker tracker)
    {
        HeatLevel level = tracker.Level;
        float y = rect.y;

        GUI.DrawTexture(new Rect(rect.x, y, 48f, 48f), NetrunTex.HeatIcon(level));
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(rect.x + 58f, y + 8f, rect.width - 58f, 34f),
            "NR_TerminalHeatValue".Translate(tracker.Heat.ToString("0"), level.Label().Colorize(level.Color())));
        Text.Font = GameFont.Small;
        y += 56f;

        NetrunUI.Bar(new Rect(rect.x, y, rect.width, 18f), tracker.Heat / HeatTracker.MaxHeat, level.Color());
        y += 26f;

        Widgets.Label(new Rect(rect.x, y, rect.width, 24f),
            "NR_TerminalHeatDecay".Translate(NetrunMod.Settings.HeatDecayPerDay.ToString("0.0")).Colorize(NetrunUI.Muted));
        y += 30f;

        y = NetrunUI.Section(new Rect(rect.x, y, rect.width, 36f), "NR_TerminalLevelsHeader".Translate());

        foreach (HeatLevel each in System.Enum.GetValues(typeof(HeatLevel)))
        {
            var row = new Rect(rect.x, y, rect.width, LevelRowHeight);
            if (each == level)
                Widgets.DrawBoxSolid(row, NetrunUI.BgCardAlt);

            GUI.DrawTexture(new Rect(row.x + 2f, row.y + 2f, 32f, 32f), NetrunTex.HeatIcon(each));

            string range = each == HeatLevel.RedZone
                ? $"{each.MinHeat():0}–100"
                : $"{each.MinHeat():0}–{(each + 1).MinHeat() - 1:0}";

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x + 40f, row.y, 220f, row.height), $"{each.Label().Colorize(each.Color())} ({range})");
            Widgets.Label(new Rect(row.x + 260f, row.y, row.width - 260f, row.height), each.Description());
            Text.Anchor = TextAnchor.UpperLeft;

            y += LevelRowHeight;
        }

        y += 8f;
        Widgets.Label(new Rect(rect.x, y, rect.width, rect.yMax - y), "NR_TerminalHackHint".Translate().Colorize(NetrunUI.Muted));
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
