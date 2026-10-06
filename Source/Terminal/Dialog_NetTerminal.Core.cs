using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Netrun;

/// <summary>
/// The Core screen (docs/CORE_V2.md): the node map with a Heat ring in the middle and counters on the nodes,
/// and a Summary panel with Heat, alerts and resources. Answers "how loud am I, what is burning, where next".
/// </summary>
public partial class Dialog_NetTerminal
{
    private const float SummaryWidth = 250f;
    private const float RingRadius = 62f;

    private static readonly Color StudioColor = new(1f, 0.45f, 0.35f);
    private static readonly Color BoardColor = new(0.35f, 0.85f, 1f);
    private static readonly Color GatewayColor = new(0.45f, 0.55f, 1f);

    // What the player has already seen this session, for the Archive and Gateway counters.
    private static int _seenLogCount = -1;
    private static int _seenNodeId = -1;

    private static string NodeLabel(View view) => ("NR_Node_" + view).Translate();

    private void OpenNode(View view)
    {
        SoundDefOf.Click.PlayOneShotOnCamera();
        _view = view;
        if (view == View.Archive)
            _seenLogCount = HeatTracker.Get()?.Log.Count ?? 0;
        if (view == View.Gateway)
            _seenNodeId = GatewayManager.Get()?.Nodes.Select(n => n.Id).DefaultIfEmpty(0).Max() ?? 0;
    }

    private void DrawCoreScreen(Rect rect, HeatTracker tracker)
    {
        DrawMap(new Rect(rect.x, rect.y, rect.width - SummaryWidth - 12f, rect.height), tracker);
        DrawSummary(new Rect(rect.xMax - SummaryWidth, rect.y, SummaryWidth, rect.height), tracker);
    }

    // ---- Map ----

    private struct NodeBadge
    {
        public string Text;
        public bool Urgent;
        public string Tip;
    }

    private void DrawMap(Rect rect, HeatTracker tracker)
    {
        Vector2 center = rect.center;
        float dx = rect.width * 0.38f;
        float dy = rect.height * 0.36f;

        var nodes = new List<(View view, Vector2 pos, Texture2D icon, Color color, string sub, NodeBadge badge)>
        {
            (View.Board, new Vector2(center.x, center.y - dy * 1.05f), NetrunTex.HomeNode("Board", NetrunTex.TabIcon(2)), BoardColor, BoardSub(), NetrunUI.Cached("core.badge.board", BoardBadge)),
            (View.Club, new Vector2(center.x - dx, center.y - dy * 0.55f), NetrunTex.HomeNode("Club", NetrunTex.TabIcon(1)), NetrunUI.Warn, ClubSub(), ClubBadge()),
            (View.Archive, new Vector2(center.x + dx, center.y - dy * 0.55f), NetrunTex.HomeNode("Archive", NetrunTex.TabIcon(5)), NetrunUI.Muted,
                "NR_NodeSub_Archive".Translate(tracker.Log.Count), ArchiveBadge(tracker)),
            (View.Server, new Vector2(center.x - dx, center.y + dy * 0.55f), NetrunTex.HomeNode("Server", NetrunTex.TabIcon(3)), NetrunUI.Purple, ServerSub(), NetrunUI.Cached("core.badge.server", ServerBadge)),
            (View.Studio, new Vector2(center.x + dx, center.y + dy * 0.55f), NetrunTex.HomeNode("Studio", NetrunTex.TabIcon(4)), StudioColor,
                "NR_NodeSub_Studio".Translate(NetrunUI.Cached("core.raws." + _map.uniqueID, () => BraindanceEditing.RawRecordings(_map).Count())), StudioBadge()),
            (View.Gateway, new Vector2(center.x, center.y + dy * 1.0f), NetrunTex.HomeNode("Gateway", NetrunTex.Cyberspace), GatewayColor,
                "NR_NodeSub_Gateway".Translate(), NetrunUI.Cached("core.badge.gateway", GatewayBadge)),
        };

        foreach (var node in nodes)
            Widgets.DrawLine(center, node.pos, new Color(NetrunUI.Line.r, NetrunUI.Line.g, NetrunUI.Line.b, 0.8f), 2f);
        foreach (var node in nodes)
            DrawNode(node.view, node.pos, NodeSize, node.icon, node.color, node.sub, node.badge);

        DrawHeatRing(center, tracker);
    }

    private void DrawNode(View view, Vector2 pos, float size, Texture2D icon, Color color, string sub, NodeBadge badge)
    {
        var box = new Rect(pos.x - size / 2f, pos.y - size / 2f - 14f, size, size);
        bool hover = Mouse.IsOver(box);
        // Opaque tile first, so the links never show through, then the hover tint on top.
        Widgets.DrawBoxSolid(box, NetrunUI.BgWindow);
        NetrunUI.Box(box, hover ? new Color(color.r, color.g, color.b, 0.15f) : NetrunUI.BgWindow, color, hover ? 3 : 2);
        GUI.DrawTexture(box.ContractedBy(size * 0.18f), icon);
        DrawBadge(new Vector2(box.xMax, box.y), badge);

        DrawNodeLabel(pos, box.yMax + 4f, NodeLabel(view).ToUpper(), color, sub);

        TooltipHandler.TipRegion(box, ("NR_NodeTip_" + view).Translate() + "\n\n" + sub);
        if (Widgets.ButtonInvisible(box))
            OpenNode(view);
    }

    private static void DrawBadge(Vector2 corner, NodeBadge badge)
    {
        if (badge.Text.NullOrEmpty())
            return;
        Text.Font = GameFont.Tiny;
        float w = Mathf.Max(20f, Text.CalcSize(badge.Text).x + 10f);
        var rect = new Rect(corner.x - w / 2f, corner.y - 10f, w, 20f);
        Widgets.DrawBoxSolid(rect, badge.Urgent ? NetrunUI.Bad : NetrunUI.BgCardAlt);
        GUI.color = badge.Urgent ? NetrunUI.Bad : NetrunUI.Accent;
        Widgets.DrawBox(rect);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, badge.Text);
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        if (!badge.Tip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, badge.Tip);
    }

    /// <summary>Label and sub-line on a background patch, so the links pass under them.</summary>
    private static void DrawNodeLabel(Vector2 pos, float y, string label, Color color, string sub)
    {
        float labelWidth = Text.CalcSize(label).x + 12f;
        Text.Font = GameFont.Tiny;
        float subWidth = Text.CalcSize(sub).x + 12f;
        Text.Font = GameFont.Small;
        Widgets.DrawBoxSolid(new Rect(pos.x - labelWidth / 2f, y, labelWidth, 22f), NetrunUI.BgWindow);
        Widgets.DrawBoxSolid(new Rect(pos.x - subWidth / 2f, y + 22f, subWidth, 18f), NetrunUI.BgWindow);

        Text.Anchor = TextAnchor.UpperCenter;
        Widgets.Label(new Rect(pos.x - 110f, y, 220f, 22f), label.Colorize(color));
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(pos.x - 110f, y + 22f, 220f, 18f), sub.Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    // ---- Heat ring (the Core node) ----

    /// <summary>Heat change over the last day: logged gains and losses minus the passive decay.</summary>
    private static float HeatTrendPerDay(HeatTracker tracker)
    {
        int since = Find.TickManager.TicksGame - GenDate.TicksPerDay;
        float logged = tracker.Log.Where(e => e.Tick >= since).Sum(e => e.Amount);
        float decay = tracker.Heat > 0f ? tracker.DecayPerDay : 0f;
        return logged - decay;
    }

    private void DrawHeatRing(Vector2 center, HeatTracker tracker)
    {
        HeatLevel current = tracker.Level;
        var rect = new Rect(center.x - RingRadius, center.y - RingRadius - 14f, RingRadius * 2f, RingRadius * 2f);
        Vector2 c = rect.center;

        GUI.color = NetrunUI.BgWindow;
        GUI.DrawTexture(rect.ExpandedBy(6f), NetrunUI.Disc);
        GUI.color = Color.white;

        // Five segments, one per level; the reached ones lit in their level color.
        var levels = (HeatLevel[])System.Enum.GetValues(typeof(HeatLevel));
        for (int i = 0; i < levels.Length; i++)
        {
            bool lit = levels[i] <= current;
            Color col = levels[i].Color();
            col.a = lit ? (levels[i] == current ? 1f : 0.7f) : 0.18f;
            float from = -90f + i * 72f + 4f;
            float to = -90f + (i + 1) * 72f - 4f;
            const int steps = 10;
            for (int s = 0; s < steps; s++)
            {
                float a1 = Mathf.Deg2Rad * Mathf.Lerp(from, to, s / (float)steps);
                float a2 = Mathf.Deg2Rad * Mathf.Lerp(from, to, (s + 1) / (float)steps);
                Widgets.DrawLine(c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * (RingRadius - 4f),
                    c + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * (RingRadius - 4f), col, 7f);
            }
        }

        // Inside: the level icon faintly, the number, the level and the trend.
        GUI.color = new Color(1f, 1f, 1f, 0.18f);
        GUI.DrawTexture(new Rect(c.x - 34f, c.y - 34f, 68f, 68f), NetrunTex.HeatIcon(current));
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(c.x - 50f, c.y - 26f, 100f, 32f), tracker.Heat.ToString("0").Colorize(current.Color()));
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(c.x - 55f, c.y + 2f, 110f, 16f), current.Label().ToUpper().Colorize(current.Color()));
        float trend = HeatTrendPerDay(tracker);
        string trendText = trend > 0.05f
            ? ("▲ " + "NR_CoreTrendUp".Translate(trend.ToString("0.#"))).Colorize(NetrunUI.Bad)
            : trend < -0.05f
                ? ("▼ " + "NR_CoreTrendDown".Translate((-trend).ToString("0.#"))).Colorize(NetrunUI.Ok)
                : "NR_CoreTrendFlat".Translate().Colorize(NetrunUI.Muted);
        Widgets.Label(new Rect(c.x - 55f, c.y + 18f, 110f, 16f), trendText);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        DrawNodeLabel(new Vector2(c.x, 0f), rect.yMax + 8f, NodeLabel(View.Core).ToUpper(), NetrunUI.Accent, "NR_NodeSub_Core".Translate());

        TooltipHandler.TipRegion(rect, "NR_CoreRingTip".Translate(tracker.Heat.ToString("0"), current.Label(), current.Description()));
        if (Widgets.ButtonInvisible(rect))
            OpenNode(View.Core);
    }

    // ---- Node sub-lines and counters ----

    private static string ClubSub() => "NR_NodeSub_Club".Translate(FixerManager.Get()?.Fixers.Count ?? 0);

    private static NodeBadge ClubBadge()
    {
        int open = FixerManager.Get()?.Fixers.Count(f => f.ChannelOpen) ?? 0;
        return new NodeBadge { Text = open > 0 ? open.ToString() : null, Tip = "NR_BadgeClub".Translate(open) };
    }

    private static string BoardSub()
    {
        Contract active = ContractManager.Get()?.Active.FirstOrDefault();
        return active == null
            ? "NR_NodeSub_BoardNone".Translate()
            : (string)"NR_NodeSub_Board".Translate(active.Progress, active.Goal);
    }

    private static NodeBadge BoardBadge()
    {
        List<Contract> active = ContractManager.Get()?.Active.ToList() ?? [];
        return new NodeBadge { Text = active.Count > 0 ? active.Count.ToString() : null, Urgent = active.Any(c => c.TicksLeft < GenDate.TicksPerDay), Tip = "NR_BadgeBoard".Translate(active.Count) };
    }

    private static NodeBadge ArchiveBadge(HeatTracker tracker)
    {
        if (_seenLogCount < 0)
            _seenLogCount = tracker.Log.Count;
        int fresh = tracker.Log.Count - _seenLogCount;
        return new NodeBadge { Text = fresh > 0 ? fresh.ToString() : null, Tip = "NR_BadgeArchive".Translate(fresh) };
    }

    private static string ServerSub()
    {
        IReadOnlyList<NetAI> ais = AIManager.Get()?.AIs;
        int count = ais?.Count ?? 0;
        return ais != null && ais.Any(a => a.HasRequest)
            ? "NR_NodeSub_ServerRequest".Translate(count)
            : (string)"NR_NodeSub_Server".Translate(count);
    }

    /// <summary>AIs that need attention: a request, hunger or an escape (urgent).</summary>
    private static NodeBadge ServerBadge()
    {
        List<NetAI> ais = AIManager.Get()?.AIs.ToList() ?? [];
        int attention = ais.Count(a => a.HasRequest || a.Escaping || a.Hunger >= 70f);
        return new NodeBadge { Text = attention > 0 ? attention.ToString() : null, Urgent = ais.Any(a => a.Escaping), Tip = "NR_BadgeServer".Translate(attention) };
    }

    private NodeBadge StudioBadge()
    {
        int raws = NetrunUI.Cached("core.raws." + _map.uniqueID, () => BraindanceEditing.RawRecordings(_map).Count());
        return new NodeBadge { Text = raws > 0 ? raws.ToString() : null, Tip = "NR_BadgeStudio".Translate(raws) };
    }

    private static NodeBadge GatewayBadge()
    {
        GatewayManager gateway = GatewayManager.Get();
        if (gateway == null)
            return default;
        int fresh = gateway.Nodes.Count(n => !n.Hidden && n.Id > _seenNodeId);
        return new NodeBadge { Text = fresh > 0 ? fresh.ToString() : null, Tip = "NR_BadgeGateway".Translate(fresh) };
    }

    // ---- Summary panel ----

    private void DrawSummary(Rect rect, HeatTracker tracker)
    {
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(1f, 1f, 1f, 0.12f));
        Rect inner = rect.ContractedBy(10f);
        float y = inner.y;

        // Heat: only the level and the way down; what the level means, the next threshold and cooling are on the Heat screen (click the ring).
        HeatLevel level = tracker.Level;
        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_CoreSummaryHeatLevel".Translate(level.Label().ToUpper()));
        if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 30f), "NR_CoreScrubButton".Translate(), NetrunUI.Ok, true,
                "NR_CoreScrubTip".Translate() + "\n\n" + level.Description()))
            OpenNode(View.Club);
        y += 38f;

        // Alerts: the most urgent first, striped by severity, the node on the right; a click leads there.
        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_CoreSummaryAlerts".Translate());
        List<(string text, Color color, View? target)> alerts = NetrunUI.Cached("core.alerts." + _map.uniqueID, () => Alerts(tracker));
        if (alerts.Count == 0)
        {
            Widgets.Label(new Rect(inner.x, y, inner.width, 22f), "NR_CoreNoAlerts".Translate().Colorize(NetrunUI.Muted));
            y += 26f;
        }
        Text.Font = GameFont.Tiny;
        foreach ((string text, Color color, View? target) in alerts.Take(4))
        {
            string where = target == null ? string.Empty : NodeLabel(target.Value);
            float whereWidth = target == null ? 0f : Text.CalcSize(where).x + 8f;
            float h = Mathf.Max(24f, Text.CalcHeight(text, inner.width - 14f - whereWidth) + 6f);
            var row = new Rect(inner.x, y, inner.width, h);
            Widgets.DrawBoxSolid(row, Mouse.IsOver(row) && target != null ? NetrunUI.BgCardAlt : NetrunUI.BgWindow);
            Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), color);
            Widgets.Label(new Rect(row.x + 8f, row.y + 3f, row.width - 14f - whereWidth, h), text);
            if (target != null)
            {
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(row.xMax - whereWidth - 4f, row.y, whereWidth, h), where.Colorize(NetrunUI.Muted));
                Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(row, "NR_CoreAlertGo".Translate(where));
                if (Widgets.ButtonInvisible(row))
                    OpenNode(target.Value);
            }
            y += h + 3f;
        }
        Text.Font = GameFont.Small;
        y += 6f;
        // Resources.
        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_CoreSummaryResources".Translate());
        y = ResourceRow(inner, y, NetrunDefOf.USH_Cyberdata.uiIcon, NetrunDefOf.USH_Cyberdata.LabelCap, ColonyGoods.Count(_map, NetrunDefOf.USH_Cyberdata).ToString());
        y = ResourceRow(inner, y, NetrunDefOf.USH_AdvancedCyberdata.uiIcon, NetrunDefOf.USH_AdvancedCyberdata.LabelCap,
            ColonyGoods.Count(_map, NetrunDefOf.USH_AdvancedCyberdata).ToString());
        CompAIServer server = _map.listerBuildings.AllBuildingsColonistOfDef(NetrunDefOf.NR_AIServer).Select(b => b.GetComp<CompAIServer>()).FirstOrDefault(s => s != null);
        y = ResourceRow(inner, y, NetrunTex.HomeNode("Server", NetrunTex.TabIcon(3)), "NR_CoreAISlots".Translate(),
            server == null ? "NR_CoreNoServer".Translate() : (string)$"{server.Used}/{server.Slots}");
        int chips = ColonyGoods.Count(_map, NetrunDefOf.NR_BraindanceChip) + ColonyGoods.Count(_map, NetrunDefOf.NR_BlackBraindanceChip);
        y = ResourceRow(inner, y, NetrunDefOf.NR_BraindanceChip.uiIcon, "NR_CoreChips".Translate(), chips.ToString());
        GatewayManager gateway = GatewayManager.Get();
        if (gateway == null)
            return;
        y = ResourceRow(inner, y, NetrunTex.GroupFavor ?? NetrunTex.HomeNode("Club", NetrunTex.TabIcon(1)), "NR_CoreGroupFavor".Translate(),
            $"{gateway.GroupFavor}/{NetrunDefOf.NR_Cyberspace.groupFavorForAllies}");
        TooltipHandler.TipRegion(new Rect(inner.x, y - 24f, inner.width, 24f),
            "NR_ClubFavorTip".Translate(NetrunDefOf.NR_Cyberspace.groupFavorForAllies, NetrunDefOf.NR_Cyberspace.alliesCooldownDays));

        // Gateway timers: the next node refresh and the boss node.
        y = NetrunUI.Section(new Rect(inner.x, y + 4f, inner.width, 36f), "NR_CoreSummaryGateway".Translate());
        y = SummaryRow(inner, y, "NR_CoreGatewayRefresh".Translate(), gateway.TicksUntilRefresh.ToStringTicksToPeriod(), NetrunUI.Accent);
        ExternalNode boss = gateway.Boss;
        if (boss != null)
            y = SummaryRow(inner, y, "NR_CoreBossActive".Translate(), boss.ExpireTick > 0 ? (boss.ExpireTick - Find.TickManager.TicksGame).ToStringTicksToPeriod() : "✓", NetrunUI.Bad);
        else if (gateway.TicksUntilBoss >= 0)
            SummaryRow(inner, y, "NR_CoreBossNext".Translate(), gateway.TicksUntilBoss.ToStringTicksToPeriod(), NetrunUI.Muted);
    }

    private static float SummaryRow(Rect inner, float y, string label, string value, Color color)
    {
        Widgets.Label(new Rect(inner.x, y, inner.width - 100f, 22f), label);
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(new Rect(inner.xMax - 100f, y, 100f, 22f), value.Colorize(color));
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = new Color(1f, 1f, 1f, 0.08f);
        Widgets.DrawLineHorizontal(inner.x, y + 23f, inner.width);
        GUI.color = Color.white;
        return y + 26f;
    }

    private static float ResourceRow(Rect inner, float y, Texture2D icon, string label, string value)
    {
        GUI.DrawTexture(new Rect(inner.x, y + 1f, 20f, 20f), icon);
        Widgets.Label(new Rect(inner.x + 26f, y, inner.width - 90f, 22f), label.Truncate(inner.width - 90f));
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(new Rect(inner.xMax - 70f, y, 70f, 22f), value);
        Text.Anchor = TextAnchor.UpperLeft;
        return y + 24f;
    }

    private List<(string text, Color color, View? target)> Alerts(HeatTracker tracker)
    {
        var list = new List<(string, Color, View?)>();
        List<NetAI> ais = AIManager.Get()?.AIs.ToList() ?? [];

        foreach (NetAI ai in ais.Where(a => a.Escaping))
            list.Add(("NR_CoreAlertEscape".Translate(ai.Name, (ai.EscapeAtTick - Find.TickManager.TicksGame).ToStringTicksToPeriod()), NetrunUI.Bad, View.Server));
        foreach (Contract c in ContractManager.Get()?.Active.Where(c => c.TicksLeft < GenDate.TicksPerDay) ?? Enumerable.Empty<Contract>())
            list.Add(("NR_CoreAlertContract".Translate(c.Title, c.TicksLeft.ToStringTicksToPeriod()), NetrunUI.Bad, View.Board));
        if (tracker.Level >= HeatLevel.Target)
            list.Add(("NR_CoreAlertMercs".Translate(), NetrunUI.Bad, (View?)View.Club));
        foreach (NetAI ai in ais.Where(a => a.HasRequest))
            list.Add(("NR_CoreAlertRequest".Translate(ai.Name, AIManager.RequestText(ai)), NetrunUI.Warn, View.Server));
        foreach (NetAI ai in ais.Where(a => !a.HasRequest && a.Hunger >= 70f))
            list.Add(("NR_CoreAlertHungry".Translate(ai.Name), NetrunUI.Warn, View.Server));
        int netsick = _map.mapPawns.FreeColonistsSpawned.Count(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick));
        if (netsick > 0)
            list.Add(("NR_CoreAlertNetsick".Translate(netsick), NetrunUI.Warn, null));
        MapEventsManager events = MapEventsManager.Get();
        if (events?.CounterHackRunning == true)
            list.Insert(0, ("NR_CoreAlertCounterHack".Translate(), NetrunUI.Bad, View.Gateway));
        if (events?.BlackMarketOpen == true)
            list.Add(("NR_CoreAlertBlackMarket".Translate(events.BlackMarketTicksLeft.ToStringTicksToPeriod()), NetrunUI.Purple, View.Club));
        return list;
    }
}
