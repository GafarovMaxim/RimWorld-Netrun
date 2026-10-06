using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Netrun;

/// <summary>
/// The Core screen (docs/CORE_V3.md; map from docs/CORE_V2.md): the node map with a Heat ring in the middle and counters on the nodes,
/// and a Summary panel with Heat, alerts and resources. Answers "how loud am I, what is burning, where next".
/// </summary>
public partial class Dialog_NetTerminal
{
    private const float SummaryWidth = 300f;
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
        NetworkGate gate = NetworkGate.Get();
        NetrunUI.Cached("core.gate." + _map.uniqueID, () => { gate?.Refresh(_map); return true; }, 1f);
        if (gate != null && !gate.OnboardingDone)
        {
            GuideRegistry.Area("onboarding.list", rect.ContractedBy(Mathf.Min(48f, rect.width * 0.05f)));
            DrawOnboarding(rect);
            return;
        }
        GuideRegistry.Area("core.map", new Rect(rect.x, rect.y, rect.width - SummaryWidth - 12f, rect.height));
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

        NetworkGate gate = NetworkGate.Get();
        bool Open(View v) => gate == null || gate.IsUnlocked(v.ToString());
        foreach (var node in nodes)
            Widgets.DrawLine(center, node.pos, new Color(NetrunUI.Line.r, NetrunUI.Line.g, NetrunUI.Line.b, Open(node.view) ? 0.8f : 0.25f), 2f);
        foreach (var node in nodes)
        {
            if (Open(node.view))
                DrawNode(node.view, node.pos, NodeSize, node.icon, node.color, node.sub, node.badge);
            else
                DrawLockedNode(node.view, node.pos, NodeSize, node.icon);
        }

        DrawHeatRing(center, tracker);
    }

    private void DrawNode(View view, Vector2 pos, float size, Texture2D icon, Color color, string sub, NodeBadge badge)
    {
        var box = new Rect(pos.x - size / 2f, pos.y - size / 2f - 14f, size, size);
        bool hover = Mouse.IsOver(box);
        // Opaque tile first, so the links never show through, then the hover tint on top.
        if (NetrunMod.Settings.Animations && !badge.Text.NullOrEmpty())
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 2.2f);
            GUI.color = new Color(color.r, color.g, color.b, 0.10f + 0.12f * pulse);
            GUI.DrawTexture(box.ExpandedBy(8f), BaseContent.WhiteTex);
            GUI.color = Color.white;
        }
        Widgets.DrawBoxSolid(box, NetrunUI.BgWindow);
        NetrunUI.Box(box, hover ? new Color(color.r, color.g, color.b, 0.15f) : NetrunUI.BgWindow, color, hover ? 3 : 2);
        GUI.DrawTexture(box.ContractedBy(size * 0.18f), icon);
        DrawBadge(new Vector2(box.xMax, box.y), badge);

        DrawNodeLabel(pos, box.yMax + 4f, NodeLabel(view).ToUpper(), color, sub);

        NetrunUI.Tip(box, ("NR_NodeTip_" + view).Translate() + "\n\n" + sub);
        if (Widgets.ButtonInvisible(box))
            OpenNode(view);
    }

    /// <summary>A node not open yet: dim, a lock mark, and how it opens.</summary>
    private static void DrawLockedNode(View view, Vector2 pos, float size, Texture2D icon)
    {
        var box = new Rect(pos.x - size / 2f, pos.y - size / 2f - 14f, size, size);
        Widgets.DrawBoxSolid(box, NetrunUI.BgWindow);
        NetrunUI.Box(box, NetrunUI.BgWindow, new Color(1f, 1f, 1f, 0.15f), 2);
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        GUI.DrawTexture(box.ContractedBy(size * 0.18f), icon);
        GUI.color = Color.white;
        if (NetrunTex.CyberIcon("Node_Locked") is { } lockIcon)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(new Rect(box.center.x - 18f, box.center.y - 18f, 36f, 36f), lockIcon);
            GUI.color = Color.white;
        }
        else
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(box, "?".Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        string how = ("NR_NodeLocked_" + view).Translate();
        DrawNodeLabel(pos, box.yMax + 4f, NodeLabel(view).ToUpper(), NetrunUI.Muted, "NR_NodeLockedSub".Translate());
        NetrunUI.Tip(box, how);
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
            NetrunUI.Tip(rect, badge.Tip);
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
        GuideRegistry.Area("core.ring", rect.ExpandedBy(10f));

        GUI.color = NetrunUI.BgWindow;
        GUI.DrawTexture(rect.ExpandedBy(6f), NetrunUI.Disc);
        GUI.color = Color.white;

        // Soft glow in the level color (pulses in the Red Zone when animations are on).
        Color lc = current.Color();
        float pulse = current == HeatLevel.RedZone && NetrunMod.Settings.Animations ? 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 3f) : 0.5f;
        GUI.color = new Color(lc.r, lc.g, lc.b, 0.08f + 0.1f * pulse);
        GUI.DrawTexture(rect.ExpandedBy(18f), NetrunUI.Disc);
        GUI.color = new Color(lc.r, lc.g, lc.b, 0.06f);
        GUI.DrawTexture(rect.ExpandedBy(6f), NetrunUI.Disc);
        GUI.color = Color.white;

        // The smooth ring (anti-aliased texture with level arcs, gaps and ticks) and the marker at the current value.
        GUI.DrawTexture(rect, HeatRing.Get(tracker.Heat));
        Vector2 tip = HeatRing.PointAt(rect, tracker.Heat);
        var marker = new Rect(tip.x - 7f, tip.y - 7f, 14f, 14f);
        GUI.color = NetrunUI.BgWindow;
        GUI.DrawTexture(marker.ExpandedBy(2f), NetrunUI.Disc);
        GUI.color = Color.white;
        GUI.DrawTexture(marker, NetrunUI.Disc);
        GUI.color = lc;
        GUI.DrawTexture(marker.ContractedBy(3f), NetrunUI.Disc);
        GUI.color = Color.white;

        // Inside: the level icon over the number, the level and the trend.
        GUI.DrawTexture(new Rect(c.x - 14f, c.y - 40f, 28f, 28f), NetrunTex.HeatIcon(current));
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(c.x - 50f, c.y - 14f, 100f, 30f), tracker.Heat.ToString("0").Colorize(lc));
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(c.x - 55f, c.y + 12f, 110f, 16f), current.Label().ToUpper().Colorize(lc));
        float trend = HeatTrendPerDay(tracker);
        string trendText = trend > 0.05f
            ? ("▲ " + "NR_CoreTrendUp".Translate(trend.ToString("0.#"))).Colorize(NetrunUI.Bad)
            : trend < -0.05f
                ? ("▼ " + "NR_CoreTrendDown".Translate((-trend).ToString("0.#"))).Colorize(NetrunUI.Ok)
                : "NR_CoreTrendFlat".Translate().Colorize(NetrunUI.Muted);
        Widgets.Label(new Rect(c.x - 55f, c.y + 27f, 110f, 16f), trendText);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        DrawNodeLabel(new Vector2(c.x, 0f), rect.yMax + 8f, NodeLabel(View.Core).ToUpper(), NetrunUI.Accent, "NR_NodeSub_Core".Translate());

        NetrunUI.Tip(rect, "NR_CoreRingTip".Translate(tracker.Heat.ToString("0"), current.Label(), current.Description()));
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

    // ---- Summary panel (Core v3): Now, Alerts, Colony online ----

    private void DrawSummary(Rect rect, HeatTracker tracker)
    {
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(1f, 1f, 1f, 0.12f));
        Rect inner = rect.ContractedBy(10f);
        float y = inner.y;

        float y0 = y;
        y = DrawNow(inner, y, tracker);
        GuideRegistry.Area("core.now", new Rect(inner.x - 4f, y0, inner.width + 8f, y - y0));
        float y1 = y + 4f;
        y = DrawAlerts(inner, y1, tracker);
        GuideRegistry.Area("core.alerts", new Rect(inner.x - 4f, y1, inner.width + 8f, y - y1));
        GuideRegistry.Area("core.colony", new Rect(inner.x - 4f, y + 4f, inner.width + 8f, inner.yMax - y - 4f));
        DrawColony(inner, y + 4f);
    }

    /// <summary>"Now": the one thing worth doing, from the CoreAdviceDef rules, with up to two quick buttons.</summary>
    private float DrawNow(Rect inner, float y, HeatTracker tracker)
    {
        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_CoreSummaryNow".Translate());
        (CoreAdviceDef advice, NamedArgument[] args) = NetrunUI.Cached("core.advice." + _map.uniqueID, () => CoreAdviceDef.Pick(_map, tracker));
        if (advice == null)
            return y;

        string title = advice.label.Formatted(args).Resolve();
        string body = (advice.text ?? string.Empty).Formatted(args).Resolve();
        Text.Font = GameFont.Tiny;
        float bodyH = Text.CalcHeight(body, inner.width - 16f);
        Text.Font = GameFont.Small;
        float titleH = Text.CalcHeight(title, inner.width - 16f);
        int buttons = Mathf.Min(2, advice.buttons.Count);
        float h = 8f + titleH + 2f + bodyH + (buttons > 0 ? 8f + 30f : 0f) + 8f;
        var box = new Rect(inner.x, y, inner.width, h);
        Widgets.DrawBoxSolid(box, NetrunUI.BgWindow);
        Widgets.DrawBoxSolid(new Rect(box.x, box.y, 3f, box.height), advice.Color);

        float ty = box.y + 8f;
        string typeKey = "now:" + advice.defName + title;
        Widgets.Label(new Rect(box.x + 12f, ty, box.width - 16f, titleH), NetrunUI.Typed(typeKey + ":t", title).Colorize(advice.Color));
        ty += titleH + 2f;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(box.x + 12f, ty, box.width - 16f, bodyH), NetrunUI.Typed(typeKey + ":b", body, 90f, title.Length / 70f).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        ty += bodyH + 8f;

        if (buttons > 0)
        {
            float bw = (box.width - 20f - (buttons - 1) * 6f) / buttons;
            for (int i = 0; i < buttons; i++)
            {
                CoreAdviceButton b = advice.buttons[i];
                Color c = i == 0 ? NetrunUI.Ok : NetrunUI.Accent;
                if (NetrunUI.Button(new Rect(box.x + 12f + i * (bw + 6f), ty, bw, 30f), CoreAdviceDef.ButtonLabel(b), c, true))
                    OpenNode(b switch
                    {
                        CoreAdviceButton.Scrub or CoreAdviceButton.Club => View.Club,
                        CoreAdviceButton.Board => View.Board,
                        CoreAdviceButton.Server => View.Server,
                        CoreAdviceButton.Studio => View.Studio,
                        CoreAdviceButton.Gateway => View.Gateway,
                        _ => View.Core,
                    });
            }
        }
        return box.yMax + 6f;
    }

    /// <summary>Alerts: up to four rows, a colored stripe and mark, the node on the right; a click leads there.</summary>
    private float DrawAlerts(Rect inner, float y, HeatTracker tracker)
    {
        List<(string text, Color color, View? target)> alerts = NetrunUI.Cached("core.alerts." + _map.uniqueID, () => Alerts(tracker));
        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_CoreSummaryAlerts".Translate());
        if (alerts.Count > 0)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(inner.xMax - 40f, y - 30f, 40f, 22f), alerts.Count.ToString().Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        if (alerts.Count == 0)
        {
            Widgets.Label(new Rect(inner.x, y, inner.width, 22f), "NR_CoreNoAlerts".Translate().Colorize(NetrunUI.Muted));
            return y + 26f;
        }
        Text.Font = GameFont.Tiny;
        foreach ((string text, Color color, View? target) in alerts.Take(4))
        {
            string where = target == null ? string.Empty : NodeLabel(target.Value);
            float whereWidth = target == null ? 0f : Text.CalcSize(where).x + 8f;
            float h = Mathf.Max(26f, Text.CalcHeight(text, inner.width - 30f - whereWidth) + 8f);
            var row = new Rect(inner.x, y, inner.width, h);
            Widgets.DrawBoxSolid(row, Mouse.IsOver(row) && target != null ? NetrunUI.BgCardAlt : NetrunUI.BgWindow);
            Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), color);
            string mark = color == NetrunUI.Bad ? "!" : color == NetrunUI.Warn ? "▲" : "●";
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x + 8f, row.y, 14f, h), mark.Colorize(color));
            Widgets.Label(new Rect(row.x + 22f, row.y, row.width - 28f - whereWidth, h), text);
            if (target != null)
            {
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(row.xMax - whereWidth - 4f, row.y, whereWidth, h), where.Colorize(NetrunUI.Muted));
                NetrunUI.Tip(row, "NR_CoreAlertGo".Translate(where));
                if (Widgets.ButtonInvisible(row))
                    OpenNode(target.Value);
            }
            Text.Anchor = TextAnchor.UpperLeft;
            y += h + 3f;
        }
        Text.Font = GameFont.Small;
        return y + 3f;
    }

    /// <summary>"Colony online": four tiles (cyberdata, AIs, chips, group) and the network, Gateway and boss rows.</summary>
    private void DrawColony(Rect inner, float y)
    {
        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_CoreSummaryColony".Translate());
        ColonyStats s = NetrunUI.Cached("core.colony." + _map.uniqueID, () => ColonyStats.Of(_map), 1f);

        float tw = (inner.width - 6f) / 2f;
        const float th = 52f;
        Tile(new Rect(inner.x, y, tw, th), "NR_CoreTileCyberdata".Translate(), s.Cyberdata.ToString(), "NR_CoreTileAdvanced".Translate(s.Advanced), NetrunUI.Accent,
            "NR_CoreTileCyberdataTip".Translate());
        Tile(new Rect(inner.x + tw + 6f, y, tw, th), "NR_CoreTileAI".Translate(), s.AIs.ToString(),
            s.Slots > 0 ? (string)"NR_CoreTileSlots".Translate(s.Used, s.Slots) : (string)"NR_CoreNoServer".Translate(), NetrunUI.Purple, "NR_CoreTileAITip".Translate());
        y += th + 6f;
        Tile(new Rect(inner.x, y, tw, th), "NR_CoreTileChips".Translate(), s.Chips.ToString(), "NR_CoreTileBlack".Translate(s.BlackChips), StudioColor,
            "NR_CoreTileChipsTip".Translate());
        GatewayManager gateway = GatewayManager.Get();
        int favor = gateway?.GroupFavor ?? 0;
        Tile(new Rect(inner.x + tw + 6f, y, tw, th), "NR_CoreTileGroup".Translate(), $"{favor}/{NetrunDefOf.NR_Cyberspace.groupFavorForAllies}",
            "NR_CoreTileFavor".Translate(), NetrunUI.Warn,
            "NR_ClubFavorTip".Translate(NetrunDefOf.NR_Cyberspace.groupFavorForAllies, NetrunDefOf.NR_Cyberspace.alliesCooldownDays), NetrunTex.GroupFavor);
        y += th + 8f;

        bool online = AIAwakening.Console(_map) != null;
        y = SummaryRow(inner, y, "NR_CoreRowNet".Translate(),
            "NR_CoreRowNetValue".Translate(s.Watts.ToString("0"), (online ? "NR_CoreOnline" : "NR_CoreOffline").Translate()), online ? NetrunUI.Ok : NetrunUI.Bad,
            "NR_CoreRowNetTip".Translate());
        if (gateway == null)
            return;
        y = SummaryRow(inner, y, "NR_CoreGatewayRefresh".Translate(), "NR_CoreIn".Translate(gateway.TicksUntilRefresh.ToStringTicksToPeriod()), NetrunUI.TextColor);
        ExternalNode boss = gateway.Boss;
        if (boss != null)
            SummaryRow(inner, y, "NR_CoreBossActive".Translate(), boss.ExpireTick > 0 ? (boss.ExpireTick - Find.TickManager.TicksGame).ToStringTicksToPeriod() : "✓", NetrunUI.Bad);
        else if (gateway.TicksUntilBoss >= 0)
            SummaryRow(inner, y, "NR_CoreBossNext".Translate(), "NR_CoreIn".Translate(gateway.TicksUntilBoss.ToStringTicksToPeriod()), NetrunUI.TextColor);
    }

    private static void Tile(Rect rect, string label, string big, string small, Color color, string tip, Texture2D icon = null)
    {
        Widgets.DrawBoxSolid(rect, NetrunUI.BgWindow);
        if (Mouse.IsOver(rect))
            Widgets.DrawHighlight(rect);
        float x = rect.x + 8f;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(x, rect.y + 4f, rect.width - 12f, 16f), label.ToUpper().Colorize(NetrunUI.Muted));
        if (icon != null)
            GUI.DrawTexture(new Rect(rect.xMax - 22f, rect.y + 4f, 16f, 16f), icon);
        Text.Font = GameFont.Medium;
        float bw = Text.CalcSize(big).x;
        Widgets.Label(new Rect(x, rect.y + 18f, bw + 4f, 30f), big.Colorize(color));
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(x + bw + 6f, rect.y + 22f, rect.xMax - x - bw - 10f, 22f), small.Truncate(rect.xMax - x - bw - 10f).Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        NetrunUI.Tip(rect, tip);
    }

    private static float SummaryRow(Rect inner, float y, string label, string value, Color color, string tip = null)
    {
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(inner.x, y, inner.width - 130f, 22f), label);
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(new Rect(inner.xMax - 130f, y, 130f, 22f), value.Colorize(color));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        GUI.color = new Color(1f, 1f, 1f, 0.08f);
        Widgets.DrawLineHorizontal(inner.x, y + 23f, inner.width);
        GUI.color = Color.white;
        if (tip != null)
            NetrunUI.Tip(new Rect(inner.x, y, inner.width, 22f), tip);
        return y + 25f;
    }

    /// <summary>The colony's network at a glance, recomputed about once a second.</summary>
    private struct ColonyStats
    {
        public int Cyberdata, Advanced, AIs, Used, Slots, Chips, BlackChips;
        public float Watts;

        private static readonly string[] PoweredNet = ["USH_Cyberpod", "USH_DataServer", "USH_OptimizationCoprocessor", "USH_JammerCoprocessor",
            "USH_VitalityCoprocessor", "NR_AIServer", "NR_AIServerRack", "NR_NetrunCoprocessor"];

        public static ColonyStats Of(Map map)
        {
            CompAIServer server = map.listerBuildings.AllBuildingsColonistOfDef(NetrunDefOf.NR_AIServer).Select(b => b.GetComp<CompAIServer>()).FirstOrDefault(c => c != null);
            int black = ColonyGoods.Count(map, NetrunDefOf.NR_BlackBraindanceChip);
            float watts = 0f;
            foreach (string defName in PoweredNet)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def == null)
                    continue;
                foreach (Building b in map.listerBuildings.AllBuildingsColonistOfDef(def))
                    watts += Mathf.Max(0f, -(b.GetComp<CompPowerTrader>()?.PowerOutput ?? 0f));
            }
            if (AIAwakening.Console(map) is { } console)
                watts += Mathf.Max(0f, -(console.GetComp<CompPowerTrader>()?.PowerOutput ?? 0f));
            return new ColonyStats
            {
                Cyberdata = ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata),
                Advanced = ColonyGoods.Count(map, NetrunDefOf.USH_AdvancedCyberdata),
                AIs = AIManager.Get()?.AIs.Count ?? 0,
                Used = server?.Used ?? 0,
                Slots = server?.Slots ?? 0,
                Chips = ColonyGoods.Count(map, NetrunDefOf.NR_BraindanceChip) + black,
                BlackChips = black,
                Watts = watts,
            };
        }
    }

    // ---- Onboarding (before the colony is online) ----

    private void DrawOnboarding(Rect rect)
    {
        Rect r = rect.ContractedBy(Mathf.Min(48f, rect.width * 0.05f));
        float y = r.y + 8f;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(r.x, y, r.width, 34f), "NR_OnboardingTitle".Translate().Colorize(NetrunUI.Accent));
        Text.Font = GameFont.Small;
        y += 40f;
        string intro = "NR_OnboardingIntro".Translate();
        float ih = Text.CalcHeight(intro, r.width);
        Widgets.Label(new Rect(r.x, y, r.width, ih), intro.Colorize(NetrunUI.Muted));
        y += ih + 12f;

        bool currentFound = false;
        int number = 1;
        foreach (OnboardingStep step in System.Enum.GetValues(typeof(OnboardingStep)))
        {
            bool done = NetworkGate.StepDone(step, _map);
            bool current = !done && !currentFound;
            if (current)
                currentFound = true;
            Color color = done ? NetrunUI.Ok : current ? NetrunUI.Warn : NetrunUI.Muted;
            string text = done ? OnboardingDoneText(step) : ("NR_OnboardingStep_" + step).Translate(ResearchLabel("USH_CybertechMachinery"), ResearchLabel("USH_Cyberlink"));
            y = OnboardingRow(r, y, done ? "✔" : number.ToString(), text, color, done ? null : "0 / 1", current || done);
            if (!done)
                number++;
        }
        // The first contact opens by itself with the first Heat.
        y = OnboardingRow(r, y, number.ToString(), "NR_OnboardingContact".Translate(), NetrunUI.Muted, "NR_OnboardingLocked".Translate(), false);

        Text.Font = GameFont.Tiny;
        string note = NetworkGate.IsOpen ? "NR_OnboardingNoteOpen".Translate() : "NR_OnboardingNoteProtected".Translate();
        Widgets.Label(new Rect(r.x, y + 4f, r.width, 34f), note.Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;

        string skip = "NR_OnboardingSkip".Translate();
        float sw = NetrunUI.ButtonWidth(skip);
        if (NetrunUI.Button(new Rect(r.xMax - sw, r.yMax - 32f, sw, 30f), skip, NetrunUI.Muted, true, "NR_OnboardingSkipTip".Translate()))
            NetworkGate.Get()?.SkipOnboarding();
    }

    private string OnboardingDoneText(OnboardingStep step)
    {
        if (step == OnboardingStep.Console)
        {
            Building console = _map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.CommsConsole).FirstOrDefault(b => b.TryGetComp<CompNetTerminal>() != null);
            return "NR_OnboardingDone_Console".Translate(console?.LabelCap ?? ThingDefOf.CommsConsole.LabelCap);
        }
        return ("NR_OnboardingDone_" + step).Translate();
    }

    private static string ResearchLabel(string defName) => DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName)?.LabelCap ?? defName;

    private static float OnboardingRow(Rect r, float y, string mark, string text, Color color, string right, bool bright)
    {
        const float h = 42f;
        var row = new Rect(r.x, y, r.width, h);
        Widgets.DrawBoxSolid(row, NetrunUI.BgCard);
        Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), color);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(row.x + 16f, row.y, 20f, h), mark.Colorize(color));
        float rw = 0f;
        if (right != null)
        {
            Text.Font = GameFont.Tiny;
            rw = Text.CalcSize(right).x + 12f;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(row.xMax - rw - 8f, row.y, rw, h), right.Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
        }
        Widgets.Label(new Rect(row.x + 40f, row.y, row.width - 56f - rw, h), bright ? text : text.Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
        return y + h + 4f;
    }

    private List<(string text, Color color, View? target)> Alerts(HeatTracker tracker)
    {
        var list = new List<(string, Color, View?)>();
        List<NetAI> ais = AIManager.Get()?.AIs.ToList() ?? [];

        foreach (NetAI ai in ais.Where(a => a.Escaping))
            list.Add(("NR_CoreAlertEscape".Translate(ai.Name, (ai.EscapeAtTick - Find.TickManager.TicksGame).ToStringTicksToPeriod()), NetrunUI.Bad, View.Server));
        MapEventsManager events = MapEventsManager.Get();
        if (events?.CounterHackRunning == true)
            list.Add(("NR_CoreAlertCounterHack".Translate(), NetrunUI.Bad, View.Gateway));
        foreach (Contract c in ContractManager.Get()?.Active.Where(c => c.TicksLeft < GenDate.TicksPerDay) ?? Enumerable.Empty<Contract>())
            list.Add(("NR_CoreAlertContract".Translate(c.Title, c.TicksLeft.ToStringTicksToPeriod()), NetrunUI.Bad, View.Board));
        if (tracker.Level >= HeatLevel.Target)
            list.Add(("NR_CoreAlertMercs".Translate(), NetrunUI.Bad, (View?)View.Club));
        foreach (NetAI ai in ais.Where(a => a.HasRequest))
            list.Add(("NR_CoreAlertRequest".Translate(ai.Name, AIManager.RequestText(ai)), NetrunUI.Purple, View.Server));
        foreach (NetAI ai in ais.Where(a => !a.HasRequest && a.Hunger >= 70f))
            list.Add(("NR_CoreAlertHungry".Translate(ai.Name), NetrunUI.Warn, View.Server));
        int netsick = _map.mapPawns.FreeColonistsSpawned.Count(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick));
        if (netsick > 0)
            list.Add(("NR_CoreAlertNetsick".Translate(netsick), NetrunUI.Warn, null));
        if (events?.BlackMarketOpen == true)
            list.Add(("NR_CoreAlertBlackMarket".Translate(events.BlackMarketTicksLeft.ToStringTicksToPeriod()), NetrunUI.Accent, View.Club));
        return list;
    }
}
