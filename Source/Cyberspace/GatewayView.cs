using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using USH_HE;
using Verse;
using Verse.Sound;

namespace Netrun;

/// <summary>
/// The Gateway node (mockup 5-gateway-nodes): external nodes as floating circles (size = haul, color = type,
/// dashed = hidden, star = contract) or as a list, and a panel for the selected node with programs and "Dive".
/// </summary>
[StaticConstructorOnStartup]
public static partial class GatewayView
{
    private const float PanelWidth = 270f;

    private static Texture2D Disc => NetrunUI.Disc;
    private static Texture2D Ring => NetrunUI.Ring;

    // Where circles float, as fractions of the map area.
    private static readonly Vector2[] Slots =
    [
        new(0.45f, 0.25f), new(0.75f, 0.55f), new(0.25f, 0.62f), new(0.55f, 0.82f),
        new(0.12f, 0.28f), new(0.80f, 0.20f), new(0.88f, 0.85f), new(0.28f, 0.90f),
        new(0.66f, 0.10f), new(0.95f, 0.45f), new(0.08f, 0.55f),
    ];

    private static int _selectedId = -1;
    private static bool _listMode;
    private static Vector2 _pan;
    private static float _zoom = DefaultZoom;
    private const float MinZoom = 0.6f;
    private const float MaxZoom = 2.4f;
    private static Vector2 _listScroll;
    private static Vector2 _panelScroll;
    /// <summary>The vanilla transport pod: the haul comes home by pod.</summary>
    private static Texture2D RewardIcon => (DefDatabase<ThingDef>.GetNamedSilentFail("TransportPod") ?? DefDatabase<ThingDef>.GetNamedSilentFail("DropPodIncoming"))?.uiIcon;
    private static float _panelHeight;
    /// <summary>The panel's scroll frame, for guide areas registered from inside it.</summary>
    private static Rect _panelOuter;

    public static void Draw(Rect rect, Map map)
    {
        GatewayManager gateway = GatewayManager.Get();
        if (gateway == null)
            return;

        IReadOnlyList<ExternalNode> nodes = gateway.Nodes;
        ExternalNode selected = nodes.FirstOrDefault(n => n.Id == _selectedId);

        var mapRect = new Rect(rect.x, rect.y, rect.width - PanelWidth - 12f, rect.height);
        GuideRegistry.Area("gateway.field", mapRect);
        GuideRegistry.Area("gateway.panel", new Rect(rect.xMax - PanelWidth, rect.y, PanelWidth, rect.height));
        NetMapManager net = NetMapManager.Get();
        if (_listMode)
            DrawList(mapRect, nodes);
        else if (net is { Ready: true })
            DrawMap(mapRect, net);
        else if (net != null)
            DrawSetupPrompt(mapRect);
        else
            DrawCircles(mapRect, nodes);

        DrawPanel(new Rect(rect.xMax - PanelWidth, rect.y, PanelWidth, rect.height), map, gateway, selected);
    }

    /// <summary>
    /// The node field: a grid that can be dragged with the mouse, links from a hub, and the circles.
    /// Drawn in a clipped group so dragged circles never spill over the panel.
    /// </summary>
    private static void DrawCircles(Rect rect, IReadOnlyList<ExternalNode> nodes)
    {
        HandleDrag(rect, 0);

        Widgets.BeginGroup(rect);
        var local = new Rect(0f, 0f, rect.width, rect.height);
        DrawGrid(local);

        Vector2 center = new(local.width * 0.5f, local.height * 0.55f);
        Vector2 hub = center + _pan;
        float time = Time.realtimeSinceStartup;

        var placed = new List<(ExternalNode node, Vector2 pos, float radius)>();
        // The boss, a counter-hack and contract nodes are never the ones left without a slot.
        List<ExternalNode> ordered = nodes.OrderByDescending(n => n.IsBoss).ThenByDescending(n => n.CounterHack).ThenByDescending(n => n.Contract).ToList();
        for (int i = 0; i < ordered.Count && i < Slots.Length; i++)
        {
            ExternalNode node = ordered[i];
            Vector2 slot = Slots[i];
            float bob = NetrunMod.Settings.Animations ? Mathf.Sin(time * 0.8f + node.Id) * 4f : 0f;
            // Zoom spreads the slots around the field centre; circles grow with it.
            var slotPos = new Vector2(local.width * slot.x, local.height * slot.y + bob);
            // Slots sit a little further apart, and circles scale with the zoom like the spacing, so they never touch.
            var pos = center + (slotPos - center) * (1.12f * _zoom) + _pan;
            float radius = (node.Hidden ? 26f : Mathf.Lerp(30f, 42f, Mathf.Clamp01(node.RewardScore))) * _zoom;
            placed.Add((node, pos, radius));
        }

        foreach (var p in placed.Where(p => !p.node.Hidden))
        {
            // The line leaves the home node's edge and stops at the circle's rim.
            Vector2 dir = (p.pos - hub).normalized;
            DrawLink(hub + dir * (54f * Mathf.Sqrt(_zoom)), p.pos - dir * p.radius, p.node.Color, p.node.Id);
        }
        DrawHome(hub);

        foreach (var (node, pos, radius) in placed)
            DrawCircle(node, pos, radius);
        Widgets.EndGroup();

        string reports = "NR_GatewayReports".Translate(GatewayManager.Get()?.Reports.Count ?? 0);
        float rw = Mathf.Max(3 * 34f - 4f, NetrunUI.ButtonWidth(reports, icon: true));
        DrawReportsButton(new Rect(rect.xMax - rw - 4f, rect.y + 4f, rw, 34f), reports);
        DrawZoomButtons(new Rect(rect.xMax - 3 * 34f, rect.y + 44f, 3 * 34f, 30f));
    }

    /// <summary>What is in effect in cyberspace right now, for the news line's tooltip.</summary>
    public static string Effects()
    {
        GatewayManager g = GatewayManager.Get();
        var lines = new List<string> { "NR_NewsEffectsHeader".Translate().Colorize(NetrunUI.Accent) };
        CyberspaceSettingsDef s = NetrunDefOf.NR_Cyberspace;
        if (g?.StormActive == true)
            lines.Add("• " + "NR_NewsEffectStorm".Translate(s.stormRewardMult.ToStringPercent()));
        if (g.GlutActive)
            lines.Add("• " + "NR_NewsEffectGlut".Translate(s.newsGlutLoot.ToStringPercent()));
        if (g.QuietActive)
            lines.Add("• " + "NR_NewsEffectQuiet".Translate(s.newsQuietTrace.ToStringPercent()));
        if (g.LockdownActive)
            lines.Add("• " + "NR_NewsEffectLockdown".Translate());
        HeatLevel level = HeatTracker.Get()?.Level ?? HeatLevel.Shadow;
        if (level >= HeatLevel.Target)
            lines.Add("• " + "NR_NewsEffectHunters".Translate());
        if (g?.Boss != null)
            lines.Add("• " + "NR_NewsEffectBoss".Translate());
        if (g != null && g.SelectedDaemon >= 0 && g.Daemons.Contains((DiveDaemon)g.SelectedDaemon))
            lines.Add("• " + "NR_NewsEffectDaemon".Translate(("NR_Daemon_" + (DiveDaemon)g.SelectedDaemon).Translate(), ("NR_DaemonDesc_" + (DiveDaemon)g.SelectedDaemon).Translate()));
        if (g?.SelectedPrograms.Count > 0)
            lines.Add("• " + "NR_NewsEffectPrograms".Translate(g.SelectedPrograms.Select(p => (string)("NR_Program_" + p).Translate()).ToCommaList()));
        if (UniqueBraindanceManager.Get()?.RevealNextDive == true)
            lines.Add("• " + "NR_NewsEffectConfession".Translate());
        if (lines.Count == 1)
            lines.Add("NR_NewsEffectNone".Translate().Colorize(NetrunUI.Muted));
        return string.Join("\n", lines);
    }

    /// <summary>Short news of cyberspace for the bottom bar: storms, the corporate core, rivals, wild AIs, contracts, refresh.</summary>
    /// <summary>Decorative news keys: the 32 NR_NewsFlavor_N lines plus the cloud session's 94 (ads, rumors, corps, street, culture, tips).</summary>
    private static readonly string[] FlavorKeys = [.. System.Linq.Enumerable.Range(0, 32).Select(i => "NR_NewsFlavor_" + i), "NR_Flavor_Ad_1", "NR_Flavor_Ad_2", "NR_Flavor_Ad_3", "NR_Flavor_Ad_4", "NR_Flavor_Ad_5", "NR_Flavor_Ad_6", "NR_Flavor_Ad_7", "NR_Flavor_Ad_8", "NR_Flavor_Ad_9", "NR_Flavor_Ad_10", "NR_Flavor_Ad_11", "NR_Flavor_Ad_12", "NR_Flavor_Ad_13", "NR_Flavor_Ad_14", "NR_Flavor_Ad_15", "NR_Flavor_Ad_16", "NR_Flavor_Ad_17", "NR_Flavor_Ad_18", "NR_Flavor_Ad_19", "NR_Flavor_Ad_20", "NR_Flavor_Rumor_1", "NR_Flavor_Rumor_2", "NR_Flavor_Rumor_3", "NR_Flavor_Rumor_4", "NR_Flavor_Rumor_5", "NR_Flavor_Rumor_6", "NR_Flavor_Rumor_7", "NR_Flavor_Rumor_8", "NR_Flavor_Rumor_9", "NR_Flavor_Rumor_10", "NR_Flavor_Rumor_11", "NR_Flavor_Rumor_12", "NR_Flavor_Rumor_13", "NR_Flavor_Rumor_14", "NR_Flavor_Rumor_15", "NR_Flavor_Rumor_16", "NR_Flavor_Rumor_17", "NR_Flavor_Rumor_18", "NR_Flavor_Rumor_19", "NR_Flavor_Rumor_20", "NR_Flavor_Corp_1", "NR_Flavor_Corp_2", "NR_Flavor_Corp_3", "NR_Flavor_Corp_4", "NR_Flavor_Corp_5", "NR_Flavor_Corp_6", "NR_Flavor_Corp_7", "NR_Flavor_Corp_8", "NR_Flavor_Corp_9", "NR_Flavor_Corp_10", "NR_Flavor_Corp_11", "NR_Flavor_Corp_12", "NR_Flavor_Corp_13", "NR_Flavor_Corp_14", "NR_Flavor_Street_1", "NR_Flavor_Street_2", "NR_Flavor_Street_3", "NR_Flavor_Street_4", "NR_Flavor_Street_5", "NR_Flavor_Street_6", "NR_Flavor_Street_7", "NR_Flavor_Street_8", "NR_Flavor_Street_9", "NR_Flavor_Street_10", "NR_Flavor_Street_11", "NR_Flavor_Street_12", "NR_Flavor_Street_13", "NR_Flavor_Street_14", "NR_Flavor_Street_15", "NR_Flavor_Street_16", "NR_Flavor_Culture_1", "NR_Flavor_Culture_2", "NR_Flavor_Culture_3", "NR_Flavor_Culture_4", "NR_Flavor_Culture_5", "NR_Flavor_Culture_6", "NR_Flavor_Culture_7", "NR_Flavor_Culture_8", "NR_Flavor_Culture_9", "NR_Flavor_Culture_10", "NR_Flavor_Culture_11", "NR_Flavor_Culture_12", "NR_Flavor_Tip_1", "NR_Flavor_Tip_2", "NR_Flavor_Tip_3", "NR_Flavor_Tip_4", "NR_Flavor_Tip_5", "NR_Flavor_Tip_6", "NR_Flavor_Tip_7", "NR_Flavor_Tip_8", "NR_Flavor_Tip_9", "NR_Flavor_Tip_10", "NR_Flavor_Tip_11", "NR_Flavor_Tip_12"];

    private static readonly int[] FlavorV2Counts = [16, 15, 15, 15];
    private static readonly string[] FlavorV2Prefix = ["NR_Flavor_Spam_", "NR_Flavor_Obit_", "NR_Flavor_FixerAd_", "NR_Flavor_CorpPR_"];

    /// <summary>
    /// One line from the newer categories: spam, an obituary with a callsign from the top 100, a fixer's ad, a corporation's
    /// PR with its real name, a wanted notice when a colony runner is famous, or a traffic report from the jammed links.
    /// </summary>
    private static (string, Color)? FlavorV2(int slot)
    {
        var rnd = new System.Random(slot * 31337);
        int pick = rnd.Next(6);
        NetRanking ranking = NetRanking.Get();
        Faction corp = NetFactions.Corps.OrderBy(f => f.loadID).Skip(rnd.Next(4)).FirstOrDefault() ?? NetFactions.Corps.FirstOrDefault();
        switch (pick)
        {
            case 0:
                return (FlavorV2Prefix[0] + (1 + rnd.Next(FlavorV2Counts[0])), NetrunUI.Muted).Translated();
            case 1:
            {
                string callsign = ranking?.Entries.Where(e => e.Kind == RankKind.Solo).Skip(rnd.Next(80)).FirstOrDefault()?.Callsign ?? "Static Moth";
                return ((FlavorV2Prefix[1] + (1 + rnd.Next(FlavorV2Counts[1]))).Translate(callsign), NetrunUI.Muted);
            }
            case 2:
                return ((FlavorV2Prefix[2] + (1 + rnd.Next(FlavorV2Counts[2]))).Translate(), NetrunUI.Ok);
            case 3:
                return corp == null ? null : ((FlavorV2Prefix[3] + (1 + rnd.Next(FlavorV2Counts[3]))).Translate(corp.Name), NetrunUI.Purple);
            case 4:
            {
                RankEntry star = ranking?.Entries.Where(e => e.Kind == RankKind.Colony).OrderByDescending(e => e.Fame).FirstOrDefault();
                if (star == null || corp == null || ranking.PlaceOf(star) > 50)
                    return null;
                return (("NR_Flavor_Wanted_" + (1 + rnd.Next(6))).Translate(corp.Name, star.Callsign), NetrunUI.Bad);
            }
            default:
            {
                NetMapManager net = NetMapManager.Get();
                MapLink jam = net?.Map?.Links.FirstOrDefault(l => l.Jammed);
                return jam == null ? null : ("NR_Flavor_Traffic".Translate(NetMapManager.NodeLabel(net.Map.Node(jam.A))), NetrunUI.Accent);
            }
        }
    }

    private static (string, Color) Translated(this (string key, Color color) t) => (t.key.Translate(), t.color);

    public static List<(string text, Color color)> News()
    {
        var list = new List<(string, Color)>();
        GatewayManager g = GatewayManager.Get();
        if (g == null)
            return list;
        foreach (NetHeadline headline in g.RecentHeadlines)
            list.Add((headline.Text, headline.Color));
        // Decoration: two lines of ads, rumors and street talk, changing every few hours of game time.
        int slot = Find.TickManager.TicksGame / (GenDate.TicksPerHour * 3);
        for (int k = 0; k < 2; k++)
            list.Add((FlavorKeys[(int)((uint)(slot * 7919 + k * 104729) % (uint)FlavorKeys.Length)].Translate(), NetrunUI.Muted));
        // The news v2 categories (docs/CONCEPTS_10.md, section 13.2): one line, names from this game.
        if (FlavorV2(slot) is { } extra)
            list.Add(extra);
        if (g.StormActive)
            list.Add(("NR_NewsStorm".Translate(g.StormTicksLeft.ToStringTicksToPeriod()), NetrunUI.Purple));
        if (g.Boss is { } boss)
            list.Add(("NR_NewsBossActive".Translate(boss.Name, boss.ExpireTick > 0 ? (boss.ExpireTick - Find.TickManager.TicksGame).ToStringTicksToPeriod() : "?"), NetrunUI.Bad));
        else if (g.TicksUntilBoss >= 0)
            list.Add(("NR_NewsBossSoon".Translate(g.TicksUntilBoss.ToStringTicksToPeriod()), NetrunUI.Warn));
        // Node news by kind: at most two lines of a kind, the rest folded into "and N more".
        var visible = g.Nodes.Where(n => !n.Hidden).ToList();
        var byKind = new List<List<(string, Color)>>
        {
            NodeNews(visible.Where(n => n.CounterHack), "NR_NewsCounterHack", NetrunUI.Bad),
            NodeNews(visible.Where(n => n.Rival), "NR_NewsRival", NetrunUI.Warn),
            NodeNews(visible.Where(n => n.WildAI), "NR_NewsWildAI", NetrunUI.Purple),
            NodeNews(visible.Where(n => n.Contract), "NR_NewsContract", NetrunUI.Ok),
        };
        if (g.LastReport is { } last)
            byKind.Add([("NR_NewsLastDive".Translate(last.RunnerName ?? "?", last.NodeName,
                (last.Success ? "NR_ReportSuccess" : last.Retreated ? "NR_ReportRetreat" : "NR_ReportFail").Translate()), NetrunUI.Muted)]);
        // Branches grow back by themselves on the map; the old refresh timer only means something without it.
        if (!GatewayManager.MapReady)
            byKind.Add([("NR_NewsRefresh".Translate(g.TicksUntilRefresh.ToStringTicksToPeriod()), NetrunUI.Accent)]);
        // Interleave the kinds with the general lines, so the ticker never runs the same kind back to back.
        var general = new List<(string, Color)>(list);
        list.Clear();
        int rounds = Mathf.Max(general.Count, byKind.Count == 0 ? 0 : byKind.Max(k => k.Count));
        for (int r = 0; r < rounds; r++)
        {
            if (r < general.Count)
                list.Add(general[r]);
            foreach (List<(string, Color)> kind in byKind)
                if (r < kind.Count)
                    list.Add(kind[r]);
        }
        return list;
    }

    private static List<(string, Color)> NodeNews(IEnumerable<ExternalNode> nodes, string key, Color color)
    {
        var all = nodes.ToList();
        var lines = all.Take(2).Select(n => ((string)key.Translate(n.Name), color)).ToList();
        if (all.Count > 2)
            lines.Add(("NR_NewsMore".Translate(all.Count - 2), color));
        return lines;
    }

    /// <summary>The dive summaries of recent runs: opens the Archive on its dive reports tab.</summary>
    private static void DrawReportsButton(Rect rect, string label)
    {
        GatewayManager gateway = GatewayManager.Get();
        int count = gateway?.Reports.Count ?? 0;
        GuideRegistry.Area("gateway.reports", rect.ExpandedBy(4f));
        bool clicked = NetrunUI.Button(rect, label, NetrunUI.Accent, count > 0 ? true : (AcceptanceReport)"NR_GatewayNoReports".Translate(),
            "NR_GatewayReportsTip".Translate(count), NetrunTex.DiveReport);
        if (!clicked || gateway == null)
            return;
        Find.WindowStack.WindowOfType<Dialog_NetTerminal>()?.OpenArchive(ArchiveTab.Tab.Reports);
    }

    /// <summary>Zoom in, zoom out and reset (also resets the pan).</summary>
    private static void DrawZoomButtons(Rect rect)
    {
        GuideRegistry.Area("gateway.zoom", rect.ExpandedBy(4f));
        if (NetrunUI.Button(new Rect(rect.x, rect.y, 30f, 30f), "+", NetrunUI.Accent, _zoom < MaxZoom ? true : (AcceptanceReport)false, "NR_GatewayZoomIn".Translate()))
            _zoom = Mathf.Min(MaxZoom, _zoom + 0.2f);
        if (NetrunUI.Button(new Rect(rect.x + 34f, rect.y, 30f, 30f), "−", NetrunUI.Accent, _zoom > MinZoom ? true : (AcceptanceReport)false, "NR_GatewayZoomOut".Translate()))
            _zoom = Mathf.Max(MinZoom, _zoom - 0.2f);
        if (NetrunUI.Button(new Rect(rect.x + 68f, rect.y, 30f, 30f), "◎", NetrunUI.Muted, true, "NR_GatewayZoomReset".Translate()))
            ResetCamera();
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperCenter;
        GUI.color = NetrunUI.Muted;
        Widgets.Label(new Rect(rect.x, rect.yMax + 2f, 98f, 16f), Mathf.RoundToInt(_zoom * 100f) + "%");
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        // The H and R hotkeys as buttons too (map v4, D9).
        NetMapManager net = NetMapManager.Get();
        if (net is { Ready: true })
        {
            float by = rect.yMax + 20f;
            if (NetrunUI.Button(new Rect(rect.x, by, 47f, 26f), "⌂ H", NetrunUI.Accent, true, "NR_MapHomeButtonTip".Translate()))
                FocusNode(net.Map.HomeId);
            AcceptanceReport anyRunner = net.Runners.Count > 0 ? true : (AcceptanceReport)"NR_MapNoRunnerToCycle".Translate();
            if (NetrunUI.Button(new Rect(rect.x + 51f, by, 47f, 26f), "▸ R", NetrunUI.Accent, anyRunner, "NR_MapNextRunnerTip".Translate()))
            {
                _selectedRunner = (Mathf.Clamp(_selectedRunner, 0, net.Runners.Count - 1) + 1) % net.Runners.Count;
                _centerOn = net.Runners[_selectedRunner].NodeId;
            }
        }
    }

    private static void ResetCamera()
    {
        _zoom = DefaultZoom;
        _pan = Vector2.zero;
    }

    private static Vector2 _downAt;
    private static bool _downOnField;

    /// <summary>
    /// Left-drag on the field pans it (kept within one field size); the mouse wheel zooms; a click on empty field (no
    /// drag, no hub under the cursor) clears the selected hub.
    /// </summary>
    private static void HandleDrag(Rect rect, int hovered)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && Mouse.IsOver(rect))
        {
            _downAt = e.mousePosition;
            _downOnField = hovered < 0;
        }
        else if (e.type == EventType.MouseUp && e.button == 0)
        {
            if (_downOnField && hovered < 0 && Mouse.IsOver(rect) && (e.mousePosition - _downAt).sqrMagnitude < 25f && _selectedMapNode >= 0)
            {
                _selectedMapNode = -1;
                _selectedId = -1;
            }
            _downOnField = false;
        }
        if (e.type == EventType.MouseDrag && e.button == 0 && Mouse.IsOver(rect))
        {
            _pan += e.delta;
            ClampPan(rect);
            e.Use();
        }
        else if (e.type == EventType.MouseDown && e.button == 2 && Mouse.IsOver(rect))
        {
            ResetCamera();
            e.Use();
        }
        else if (e.type == EventType.ScrollWheel && Mouse.IsOver(rect))
        {
            // Zoom toward the cursor: the point under it stays under it.
            var local = new Rect(0f, 0f, rect.width, rect.height);
            Vector2 cursor = e.mousePosition - rect.position;
            Vector2 world = ToWorld(local, cursor);
            _zoom = Mathf.Clamp(_zoom - e.delta.y * 0.05f, MinZoom, MaxZoom);
            _pan = cursor - local.center - (world - FieldCenter) * FitScale(local) * _zoom;
            ClampPan(local);
            e.Use();
        }
    }

    /// <summary>The "space" behind the nodes: a faint grid that moves with the pan.</summary>
    private static readonly Color GridColor = new(0.22f, 0.78f, 0.38f);

    /// <summary>
    /// A green grid that pans and zooms with the field. Quietly alive: now and then a packet runs along a line,
    /// and a soft ripple spreads from a random point.
    /// </summary>
    private static void DrawGrid(Rect rect)
    {
        float step = 40f * _zoom;
        float ox = Mathf.Repeat(_pan.x, step);
        float oy = Mathf.Repeat(_pan.y, step);
        int ix = Mathf.FloorToInt(_pan.x / step);
        int iy = Mathf.FloorToInt(_pan.y / step);
        int col = 0;
        for (float x = ox; x < rect.width; x += step, col++)
        {
            bool major = (col - ix) % 5 == 0;
            Widgets.DrawBoxSolid(new Rect(x, 0f, 1f, rect.height), new Color(GridColor.r, GridColor.g, GridColor.b, major ? 0.09f : 0.045f));
        }
        int row = 0;
        for (float y = oy; y < rect.height; y += step, row++)
        {
            bool major = (row - iy) % 5 == 0;
            Widgets.DrawBoxSolid(new Rect(0f, y, rect.width, 1f), new Color(GridColor.r, GridColor.g, GridColor.b, major ? 0.09f : 0.045f));
        }
        if (!NetrunMod.Settings.Animations || Event.current.type != EventType.Repaint)
            return;

        float now = Time.realtimeSinceStartup;
        // Packets: a few at a time, each on its own line; every time slot picks new lines.
        for (int k = 0; k < 3; k++)
        {
            float period = 3.2f + k * 0.9f;
            float f = (now + k * 1.7f) / period;
            int slot = Mathf.FloorToInt(f);
            float t = f - slot;
            var rnd = new GridRng(slot * 31 + k * 977);
            bool horizontal = rnd.Next(2) == 0;
            bool forward = rnd.Next(2) == 0;
            float a = 0.55f * Mathf.Sin(t * Mathf.PI);
            GUI.color = new Color(GridColor.r, GridColor.g, GridColor.b, a);
            if (horizontal)
            {
                float y = oy + step * rnd.Next(Mathf.Max(1, Mathf.FloorToInt(rect.height / step)));
                float x = Mathf.Lerp(-60f, rect.width, forward ? t : 1f - t);
                GUI.DrawTexture(new Rect(x, y - 0.5f, 60f, 2f), BaseContent.WhiteTex);
            }
            else
            {
                float x = ox + step * rnd.Next(Mathf.Max(1, Mathf.FloorToInt(rect.width / step)));
                float y = Mathf.Lerp(-60f, rect.height, forward ? t : 1f - t);
                GUI.DrawTexture(new Rect(x - 0.5f, y, 2f, 60f), BaseContent.WhiteTex);
            }
        }
        // Faint motes: a handful of points that light up and fade, each in its own time.
        for (int k = 0; k < 7; k++)
        {
            float period = 3.5f + k * 0.6f;
            float f = (now + k * 2.3f) / period;
            int slot = Mathf.FloorToInt(f);
            float t = f - slot;
            var rnd = new GridRng(slot * 131 + k * 7);
            var p = new Vector2((float)rnd.NextDouble() * rect.width, (float)rnd.NextDouble() * rect.height);
            float a = 0.35f * Mathf.Sin(t * Mathf.PI);
            GUI.color = new Color(GridColor.r, GridColor.g, GridColor.b, a * 0.5f);
            GUI.DrawTexture(new Rect(p.x - 5f, p.y - 5f, 10f, 10f), NetrunUI.GlowTex);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.DrawTexture(new Rect(p.x - 1f, p.y - 1f, 2f, 2f), BaseContent.WhiteTex);
        }
        // Ripple: every ~7 s a faint double ring spreads over the grid.
        {
            const float period = 7f;
            float f = now / period;
            int slot = Mathf.FloorToInt(f);
            float t = f - slot;
            if (t < 0.45f)
            {
                var rnd = new GridRng(slot * 7919);
                var c = new Vector2((float)rnd.NextDouble() * rect.width, (float)rnd.NextDouble() * rect.height);
                float r = Mathf.Lerp(10f, Mathf.Max(rect.width, rect.height) * 0.45f, t / 0.45f);
                GUI.color = new Color(GridColor.r, GridColor.g, GridColor.b, 0.12f * (1f - t / 0.45f));
                GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), NetrunUI.ThinRing);
                GUI.DrawTexture(new Rect(c.x - r * 0.96f, c.y - r * 0.96f, r * 1.92f, r * 1.92f), NetrunUI.ThinRing);
            }
        }
        GUI.color = Color.white;
    }

    /// <summary>A link from the home node: a soft glow under a thin line, and now and then a packet running out to the node.</summary>
    private static void DrawLink(Vector2 from, Vector2 to, Color color, int seed)
    {
        Widgets.DrawLine(from, to, new Color(color.r, color.g, color.b, 0.10f), 6f);
        Widgets.DrawLine(from, to, new Color(color.r, color.g, color.b, 0.45f), 1.5f);
        if (!NetrunMod.Settings.Animations || Event.current.type != EventType.Repaint)
            return;
        // The same calm rhythm as the Core map: one packet, a pause, its own phase per link.
        float period = 2.8f + seed % 5 * 0.37f;
        float f = (Time.realtimeSinceStartup + seed * 0.91f) / period % 1f;
        if (f > 0.7f)
            return;
        float t = f / 0.7f;
        Vector2 p = Vector2.Lerp(from, to, t);
        float a = Mathf.Sin(t * Mathf.PI);
        GUI.color = new Color(color.r, color.g, color.b, 0.35f * a);
        GUI.DrawTexture(new Rect(p.x - 8f, p.y - 8f, 16f, 16f), NetrunUI.GlowTex);
        GUI.color = new Color(color.r, color.g, color.b, 0.9f * a);
        GUI.DrawTexture(new Rect(p.x - 3f, p.y - 3f, 6f, 6f), Disc);
        GUI.color = Color.white;
    }

    /// <summary>
    /// The home node at the hub: a small Core (the Heat ring, the level icon, slowly turning notches); a click goes back to
    /// the terminal's main screen.
    /// </summary>
    private static void DrawHome(Vector2 hub)
    {
        HeatTracker tracker = HeatTracker.Get();
        float size = 96f * Mathf.Sqrt(_zoom);
        var r = new Rect(hub.x - size / 2f, hub.y - size / 2f, size, size);
        float lit = NetrunUI.HoverFade(r);
        HeatLevel level = tracker?.Level ?? HeatLevel.Shadow;
        Color c = level.Color();
        NetrunUI.Glow(r.ContractedBy(6f), c, 0.25f + 0.5f * lit, 14f);
        GUI.color = NetrunUI.BgWindow;
        GUI.DrawTexture(r.ExpandedBy(4f), Disc);
        GUI.color = Color.white;
        if (tracker != null)
            GUI.DrawTexture(r, HeatRing.Get(tracker.Heat));
        // Outer thin circle with three uneven notches, turning slowly.
        Rect outer = r.ExpandedBy(8f);
        GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.35f + 0.3f * lit);
        GUI.DrawTexture(outer, NetrunUI.ThinRing);
        GUI.color = Color.white;
        float turn = NetrunUI.AnimTime * 8f;
        foreach ((float angle, float length) in HomeNotches)
        {
            float ang = (angle + turn) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Widgets.DrawLine(outer.center + dir * (outer.width / 2f - length * 0.4f), outer.center + dir * (outer.width / 2f + length * 0.6f), NetrunUI.Accent, 2f);
        }
        float ic = size * 0.36f;
        GUI.DrawTexture(new Rect(hub.x - ic / 2f, hub.y - ic * 0.75f, ic, ic), NetrunTex.HeatIcon(level));
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperCenter;
        if (tracker != null)
            Widgets.Label(new Rect(hub.x - 30f, hub.y + ic * 0.3f, 60f, 16f), tracker.Heat.ToString("0").Colorize(c));
        Widgets.Label(new Rect(r.x - 40f, outer.yMax + 2f, r.width + 80f, 18f), ((string)"NR_GatewayHome".Translate()).ToUpper().Colorize(NetrunUI.Accent));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        NetrunUI.Tip(r, "NR_GatewayHomeTip".Translate());
        if (Widgets.ButtonInvisible(r))
        {
            NetrunSound.Click();
            Find.WindowStack.WindowOfType<Dialog_NetTerminal>()?.GoHome();
        }
    }

    private static void DrawCircle(ExternalNode node, Vector2 pos, float radius)
    {
        var rect = new Rect(pos.x - radius, pos.y - radius, radius * 2f, radius * 2f);
        bool selected = node.Id == _selectedId;
        bool hover = Mouse.IsOver(rect);
        Color color = node.Hidden ? NetrunUI.Muted : node.Color;

        if (!node.Hidden)
        {
            GUI.color = new Color(color.r, color.g, color.b, selected || hover ? 0.22f : 0.1f);
            GUI.DrawTexture(rect.ExpandedBy(selected ? 8f : 4f), Disc);
        }
        GUI.color = new Color(0.07f, 0.08f, 0.07f, 1f);
        GUI.DrawTexture(rect, Disc);
        GUI.color = new Color(color.r, color.g, color.b, node.Hidden ? 0.5f : 1f);
        GUI.DrawTexture(rect, Ring);
        GUI.color = Color.white;

        // Inside the circle only the icon, sized to it; the name and defense sit on a plate under the circle,
        // so text never spills over the ring or the links whatever the zoom.
        float icon = radius * 1.05f;
        var iconRect = new Rect(rect.center.x - icon / 2f, rect.center.y - icon / 2f, icon, icon);
        string name, sub;
        Color subColor;
        if (node.Hidden)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            GUI.DrawTexture(iconRect, NetrunTex.HiddenNode);
            GUI.color = Color.white;
            name = "NR_GatewayHidden".Translate();
            sub = null;
            subColor = NetrunUI.Muted;
        }
        else
        {
            if (node.Icon != null)
                GUI.DrawTexture(iconRect, node.Icon);
            name = node.Name;
            sub = node.BlackIce ? "NR_GatewayBlackIce".Translate(node.Defense) : "NR_GatewayDefense".Translate(node.Defense);
            if (node.Contract)
                sub += " · ★";
            if (node.Rival)
                sub += " · ⚔";
            subColor = node.BlackIce ? NetrunUI.Bad : color;
        }
        Text.Font = GameFont.Tiny;
        float nameW = Text.CalcSize(name).x, subW = sub == null ? 0f : Text.CalcSize(sub).x;
        float plateW = Mathf.Max(nameW, subW) + 12f;
        var plate = new Rect(rect.center.x - plateW / 2f, rect.yMax + 3f, plateW, sub == null ? 18f : 32f);
        Widgets.DrawBoxSolid(plate, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.88f));
        Widgets.DrawBoxSolid(new Rect(plate.x, plate.y, plate.width, 1f), new Color(color.r, color.g, color.b, selected || hover ? 0.9f : 0.45f));
        Text.Anchor = TextAnchor.UpperCenter;
        Widgets.Label(new Rect(plate.x, plate.y + 1f, plate.width, 16f), name.Colorize(selected || hover ? NetrunUI.TextColor : new Color(0.85f, 0.87f, 0.82f)));
        if (sub != null)
            Widgets.Label(new Rect(plate.x, plate.y + 15f, plate.width, 16f), sub.Colorize(subColor));
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        if (Widgets.ButtonInvisible(rect))
        {
            NetrunSound.Click();
            _selectedId = node.Id;
        }
    }
    private static void DrawList(Rect rect, IReadOnlyList<ExternalNode> nodes)
    {
        const float rowHeight = 40f;
        var view = new Rect(0f, 0f, rect.width - 16f, nodes.Count * (rowHeight + 4f));
        NetrunUI.BeginScroll(rect, ref _listScroll, view);
        float y = 0f;
        foreach (ExternalNode node in nodes)
        {
            var row = new Rect(0f, y, view.width, rowHeight);
            Color color = node.Hidden ? NetrunUI.Muted : node.Color;
            NetrunUI.Card(row, color);
            if (node.Id == _selectedId)
                Widgets.DrawBoxSolid(row, new Color(color.r, color.g, color.b, 0.12f));
            Text.Anchor = TextAnchor.MiddleLeft;
            string text = node.Hidden
                ? "NR_GatewayHiddenRow".Translate().Colorize(NetrunUI.Muted)
                : $"{node.Name.Colorize(color)}   {node.TypeLabel}   " + (node.BlackIce ? "NR_GatewayBlackIce" : "NR_GatewayDefense").Translate(node.Defense)
                  + (node.Contract ? "   ★" : string.Empty);
            Widgets.Label(new Rect(row.x + 14f, row.y, row.width - 20f, row.height), text);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(row))
                _selectedId = node.Id;
            y += rowHeight + 4f;
        }
        Widgets.EndScrollView();
    }

    private static void DrawPanel(Rect rect, Map map, GatewayManager gateway, ExternalNode node)
    {
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(1f, 1f, 1f, 0.12f));
        // The panel scrolls when the window is too short for it (height measured on the previous frame).
        Rect outer = rect.ContractedBy(10f);
        _panelOuter = outer;
        bool scrolls = _panelHeight > outer.height;
        var inner = new Rect(0f, 0f, outer.width - (scrolls ? 16f : 0f), Mathf.Max(_panelHeight, outer.height));
        NetrunUI.BeginScroll(outer, ref _panelScroll, inner);
        float y = inner.y;
        bool mapPanel = NetMapManager.Get() is { Ready: true };
        if (mapPanel)
            y = DrawMapPanel(inner, y, map, NetMapManager.Get());
        float gNodeStart = y;

        // No hub picked on the map: the overview above, then the dive reports and the view switch.
        if (mapPanel && _selectedMapNode < 0)
        {
            y = DrawPanelFooter(inner, y + 6f, outer, withReports: true);
            _panelHeight = y + 4f;
            Widgets.EndScrollView();
            return;
        }

        if (HomeSelected)
        {
            // Home: the defense block above is all; no programs, daemons or dive here.
            GuideRegistry.InScroll("gateway.defense", outer, _panelScroll, new Rect(inner.x, 0f, inner.width, y));
            _panelHeight = y + 4f;
            Widgets.EndScrollView();
            return;
        }
        if (node == null || node.Hidden)
        {
            Widgets.Label(new Rect(inner.x, y, inner.width, 60f), (node == null ? "NR_GatewayPickNode" : "NR_GatewayNodeHidden").Translate().Colorize(NetrunUI.Muted));
            y += 64f;
        }
        else
        {
            float gNode = y;
            y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), node.Name);
            y = Row(inner, y, node.Icon, "NR_GatewayRowType".Translate(), node.TypeLabel, NetrunUI.TextColor, "NR_GatewayTipType".Translate());
            y = Row(inner, y, NetrunTex.CyberIcon("Dive_GATE"), "NR_GatewayRowDefense".Translate(),
                "NR_GatewayDefenseValue".Translate(1 + node.Defense / 2, node.Defense), NetrunUI.Warn, "NR_GatewayTipDefense".Translate());
            y = Row(inner, y, NetrunTex.CyberIcon(node.BlackIce ? "Dive_BLACKICE" : "Dive_ICE"), "NR_GatewayRowIce".Translate(),
                node.BlackIce ? "NR_GatewayIceBlack".Translate(node.IceStrength) : "NR_GatewayIceWhite".Translate(),
                node.BlackIce ? NetrunUI.Bad : NetrunUI.TextColor, "NR_GatewayTipIce".Translate());
            y = Row(inner, y, RewardIcon, "NR_GatewayRowReward".Translate(), "NR_GatewayRewardMult".Translate(node.RewardMultiplier.ToString("0.#")), NetrunUI.Ok,
                "NR_GatewayTipReward".Translate() + "\n\n" + node.RewardText);
            // Items in a column: icon, name, amount or chance.
            int rewardCost = gateway.DiveCost(map, node, out _);
            foreach ((ThingDef def, string amount) in node.RewardLines(rewardCost))
            {
                float s = NetrunUI.Icon(26f);
                var line = new Rect(inner.x + 24f, y, inner.width - 24f, s + 2f);
                Widgets.ThingIcon(new Rect(line.x, line.y + 1f, s, s), def);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(line.x + s + 6f, line.y, line.width - 90f, line.height), def.LabelCap.ToString().Truncate(line.width - 90f));
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(line.xMax - 64f, line.y, 64f, line.height), amount.Colorize(NetrunUI.Ok));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                NetrunUI.Tip(line, def.LabelCap + ": " + amount);
                y += 22f;
            }
            y += 4f;
            y = Row(inner, y, NetrunTex.CyberIcon("Dive_Heat"), "NR_GatewayRowTrace".Translate(),
                "NR_GatewayTraceValue".Translate(node.HeatPerTrace.ToString("0.#")), NetrunUI.Warn, "NR_GatewayTipTrace".Translate());
            Color riskColor = node.Risk switch { 2 => NetrunUI.Bad, 1 => NetrunUI.Warn, _ => NetrunUI.Muted };
            y = Row(inner, y, node.RiskIcon, "NR_GatewayRowRisk".Translate(), ("NR_GatewayRisk_" + node.Risk).Translate(), riskColor, "NR_GatewayTipRisk".Translate());
            List<string> effects = node.EffectLabels.ToList();
            if (effects.Count > 0)
            {
                y += 4f;
                float ex = inner.x;
                foreach (string effect in effects)
                {
                    float w = Text.CalcSize(effect).x + 16f;
                    if (ex + w > inner.xMax)
                    {
                        ex = inner.x;
                        y += 24f;
                    }
                    ex += NetrunUI.Chip(ex, y, effect, NetrunUI.Purple, "NR_NodeEffectTip".Translate()) + 6f;
                }
                y += 24f;
            }
            if (node.Contract || node.Rival)
            {
                y += 4f;
                float cx = inner.x;
                if (node.Contract)
                    cx += NetrunUI.Chip(cx, y, "NR_GatewayContract".Translate(), NetrunUI.Ok) + 6f;
                if (node.Rival)
                    NetrunUI.Chip(cx, y, "NR_GatewayRival".Translate(), NetrunUI.Warn, "NR_GatewayRivalTip".Translate(NetrunDefOf.NR_Cyberspace.rivalTurns));
                y += 24f;
            }
        }

        if (node != null && !node.Hidden)
            GuideRegistry.InScroll("gateway.node", outer, _panelScroll, new Rect(inner.x, gNodeStart, inner.width, y - gNodeStart));
        float gProg = y;
        // Programs: toggles, limited by the pod's slots, paid in cyberdata on diving.
        Pawn runner = GatewayManager.Runner(map, node, out Building_Cyberpod pod);
        int slots = GatewayManager.ProgramSlots(pod);
        y = NetrunUI.Section(new Rect(inner.x, y + 6f, inner.width, 36f), "NR_GatewayPrograms".Translate(slots));
        float x = inner.x;
        foreach (DiveProgram program in System.Enum.GetValues(typeof(DiveProgram)))
        {
            bool on = gateway.SelectedPrograms.Contains(program);
            string label = ("NR_Program_" + program).Translate();
            if (HePrograms.IsHeItem(program) && NetrunDefOf.NR_Res_ExecPrograms?.IsFinished != true && !on)
                continue;
            Texture2D programIcon = NetrunTex.CyberIcon("Program_" + program) ?? HePrograms.IconDef(program)?.uiIcon;
            float w = NetrunUI.ButtonWidth(label, programIcon != null);
            if (x + w > inner.xMax)
            {
                x = inner.x;
                y += 34f;
            }
            AcceptanceReport report = on ? true
                : gateway.SelectedPrograms.Count >= slots ? (AcceptanceReport)"NR_GatewayNoSlots".Translate()
                : HePrograms.CanSelect(map, program, gateway.SelectedPrograms);
            Color color = on ? NetrunUI.Ok : NetrunUI.Muted;
            if (NetrunUI.Button(new Rect(x, y, w, 28f), (on ? "✓ " : string.Empty) + label, on ? color : NetrunUI.TextColor, report,
                    ("NR_ProgramDesc_" + program).Translate(NetrunDefOf.NR_Cyberspace.programCost), programIcon))
            {
                if (on)
                    gateway.SelectedPrograms.Remove(program);
                else
                    gateway.SelectedPrograms.Add(program);
            }
            x += w + 6f;
        }
        y += 36f;

        // Daemons: one per dive, free, no slot. Buttons like the programs; a second click takes it off.
        // Until the first one is found, a single hint stands in for the block.
        int daemonsFound = gateway.Daemons.Count;
        if (daemonsFound == 0)
        {
            Text.Font = GameFont.Tiny;
            string hint = "NR_GatewayDaemonHint".Translate();
            float hh = Text.CalcHeight(hint, inner.width);
            Widgets.Label(new Rect(inner.x, y + 4f, inner.width, hh), hint.Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
            y += hh + 8f;
        }
        else
        {
            y = DrawDaemonPick(inner, y, gateway, daemonsFound);
        }

        GuideRegistry.InScroll("gateway.programs", outer, _panelScroll, new Rect(inner.x, gProg, inner.width, y - gProg));
        float gDive = y;
        // Dive cost with its breakdown, then the button.
        int cost = gateway.DiveCost(map, node, out string breakdown);
        var costRect = new Rect(inner.x, y, inner.width, 22f);
        Widgets.Label(costRect, "NR_DiveCostLine".Translate(cost).Colorize(NetrunUI.Warn));
        NetrunUI.Tip(costRect, breakdown);
        y += 26f;

        AcceptanceReport dive = gateway.CanDive(map, node);
        string diveTip = (runner != null ? "NR_GatewayDiveTip".Translate(runner.LabelShort) + "\n\n" : string.Empty) + breakdown;
        if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 48f), "NR_GatewayDive".Translate(), NetrunUI.Accent, dive, diveTip, NetrunTex.Dive))
            gateway.Dive(map, node);
        y += 52f;

        GuideRegistry.InScroll("gateway.dive", outer, _panelScroll, new Rect(inner.x, gDive, inner.width, y - gDive));
        y = DrawPanelFooter(inner, y, outer, withReports: !mapPanel);
        _panelHeight = y + 4f;
        Widgets.EndScrollView();
    }

    /// <summary>The panel's last row: dive reports (when shown) and the view switch (the map or a list), side by side.</summary>
    private static float DrawPanelFooter(Rect inner, float y, Rect outer, bool withReports)
    {
        float gSearch = y;
        float w = withReports ? (inner.width - 6f) / 2f : inner.width;
        if (withReports)
        {
            int count = GatewayManager.Get()?.Reports.Count ?? 0;
            var rr = new Rect(inner.x, y, w, 30f);
            if (NetrunUI.Button(rr, "NR_GatewayReports".Translate(count), NetrunUI.Accent, count > 0 ? true : (AcceptanceReport)"NR_GatewayNoReports".Translate(),
                    "NR_GatewayReportsTip".Translate(count), NetrunTex.DiveReport))
                Find.WindowStack.WindowOfType<Dialog_NetTerminal>()?.OpenArchive(ArchiveTab.Tab.Reports);
            GuideRegistry.InScroll("gateway.reports", outer, _panelScroll, rr);
        }
        var vr = new Rect(withReports ? inner.x + w + 6f : inner.x, y, w, 30f);
        if (NetrunUI.Button(vr, (_listMode ? "NR_GatewayViewCircles" : "NR_GatewayViewList").Translate(), NetrunUI.Muted, true))
            _listMode = !_listMode;
        y += 36f;
        GuideRegistry.InScroll("gateway.search", outer, _panelScroll, new Rect(inner.x, gSearch, inner.width, y - gSearch));
        return y;
    }


    /// <summary>The daemon buttons (shown once at least one daemon has been found).</summary>
    private static float DrawDaemonPick(Rect inner, float y, GatewayManager gateway, int daemonsFound)
    {
        y = NetrunUI.Section(new Rect(inner.x, y + 2f, inner.width, 36f), "NR_GatewayDaemonPick".Translate(daemonsFound));
        float x = inner.x;
        foreach (DiveDaemon d in System.Enum.GetValues(typeof(DiveDaemon)))
        {
            bool unlocked = gateway.Daemons.Contains(d);
            bool on = unlocked && gateway.SelectedDaemon == (int)d;
            string label = ("NR_Daemon_" + d).Translate();
            Texture2D icon = NetrunTex.DaemonIcon(d);
            float w = NetrunUI.ButtonWidth(label, icon != null);
            if (x + w > inner.xMax)
            {
                x = inner.x;
                y += 34f;
            }
            AcceptanceReport report = unlocked ? true : (AcceptanceReport)"NR_GatewayDaemonLocked".Translate();
            string tip = ("NR_DaemonDesc_" + d).Translate() + "\n\n" + "NR_GatewayDaemonOne".Translate();
            var dr = new Rect(x, y, w, 28f);
            if (NetrunUI.Button(dr, (on ? "✓ " : string.Empty) + label, on ? NetrunUI.Purple : NetrunUI.TextColor, report, tip, unlocked ? icon : null))
                gateway.SelectedDaemon = on ? -1 : (int)d;
            if (unlocked && !on && gateway.SelectedDaemon >= 0)
                Widgets.DrawBoxSolid(dr.ContractedBy(1f), new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.45f));
            x += w + 6f;
        }
        y += 36f;
        return y;
    }

    /// <summary>One line of the node panel: icon, label, then the value (after small item icons, if any).</summary>
    private static float Row(Rect inner, float y, Texture2D icon, string label, string value, Color color, string tip,
        IEnumerable<Texture2D> valueIcons = null)
    {
        // Two tiers, so nothing breaks mid-word in the narrow panel: the icon and the label (small, muted) on top,
        // the value under them across the whole width (its item icons first).
        float s = NetrunUI.Icon(22f);
        var icons = valueIcons?.Where(t => t != null).ToList();
        float vx = inner.x + s + 6f;
        float iconsW = icons == null || icons.Count == 0 ? 0f : icons.Count * (s + 2f) + 4f;
        Text.Font = GameFont.Small;
        float valueH = Mathf.Max(iconsW > 0f ? s : 0f, Text.CalcHeight(value, inner.xMax - vx - iconsW));
        float h = 4f + 16f + valueH + 6f;
        var row = new Rect(inner.x, y, inner.width, h);
        if (Mouse.IsOver(row))
            Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.04f));
        if (icon != null)
            NetrunUI.BrightIcon(new Rect(inner.x, y + 4f, s, s), icon);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(vx, y + 4f, inner.xMax - vx, 16f), ((string)label).ToUpper().Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        float vy = y + 20f;
        if (icons != null)
        {
            foreach (Texture2D tex in icons)
            {
                NetrunUI.BrightIcon(new Rect(vx, vy, s, s), tex);
                vx += s + 2f;
            }
            vx += 4f;
        }
        Widgets.Label(new Rect(vx, vy, inner.xMax - vx, valueH), value.Colorize(color));
        NetrunUI.Tip(row, tip);
        GUI.color = new Color(1f, 1f, 1f, 0.07f);
        Widgets.DrawLineHorizontal(inner.x, y + h, inner.width);
        GUI.color = Color.white;
        return y + h + 1f;
    }

    /// <summary>Title chips for the Gateway: who would dive and whether a pod is ready.</summary>
    public static void HeaderChips(float xMax, float y, Map map)
    {
        if (NetMapManager.Get() is { Ready: true } net)
        {
            NetrunUI.HeaderChip(xMax, y, "NR_MapRunnersChip".Translate(net.Runners.Count, NetMapManager.Settings.maxRunners), "NR_MapRunnersChipTip".Translate());
            return;
        }
        Pawn runner = GatewayManager.Runner(map, out _);
        float x = NetrunUI.HeaderChip(xMax, y, runner != null
            ? "NR_GatewayPodReady".Translate().Colorize(NetrunUI.Ok)
            : "NR_GatewayPodNone".Translate().Colorize(NetrunUI.Bad), "NR_TipPod".Translate()) - 8f;
        if (runner != null)
            NetrunUI.HeaderChip(x, y, "NR_GatewayRunner".Translate(runner.LabelShort), "NR_TipRunner".Translate());
    }

    private static readonly (float angle, float length)[] HomeNotches = [(-60f, 7f), (75f, 5f), (190f, 9f)];

    /// <summary>A tiny xorshift for the grid's packets and motes (a System.Random per spark made garbage every frame).</summary>
    private struct GridRng
    {
        private uint _s;

        public GridRng(int seed) => _s = (uint)seed * 2654435761u ^ 0x9E3779B9u | 1u;

        private uint Step()
        {
            _s ^= _s << 13;
            _s ^= _s >> 17;
            _s ^= _s << 5;
            return _s;
        }

        public int Next(int n) => n <= 1 ? 0 : (int)(Step() % (uint)n);

        public double NextDouble() => (Step() & 0xFFFFFF) / (double)0x1000000;
    }
}
