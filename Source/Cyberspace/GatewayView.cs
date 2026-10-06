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
public static class GatewayView
{
    private const float PanelWidth = 270f;

    private static Texture2D Disc => NetrunUI.Disc;
    private static Texture2D Ring => NetrunUI.Ring;

    // Where circles float, as fractions of the map area.
    private static readonly Vector2[] Slots =
    [
        new(0.45f, 0.25f), new(0.75f, 0.55f), new(0.25f, 0.62f), new(0.55f, 0.82f),
        new(0.12f, 0.28f), new(0.80f, 0.20f), new(0.88f, 0.85f),
    ];

    private static int _selectedId = -1;
    private static bool _listMode;
    private static Vector2 _pan;
    private static float _zoom = 1f;
    private const float MinZoom = 0.6f;
    private const float MaxZoom = 1.8f;
    private static Vector2 _listScroll;
    private static Vector2 _panelScroll;
    /// <summary>The vanilla transport pod: the haul comes home by pod.</summary>
    private static Texture2D RewardIcon => (DefDatabase<ThingDef>.GetNamedSilentFail("TransportPod") ?? DefDatabase<ThingDef>.GetNamedSilentFail("DropPodIncoming"))?.uiIcon;
    private static float _panelHeight;

    public static void Draw(Rect rect, Map map)
    {
        GatewayManager gateway = GatewayManager.Get();
        if (gateway == null)
            return;

        IReadOnlyList<ExternalNode> nodes = gateway.Nodes;
        ExternalNode selected = nodes.FirstOrDefault(n => n.Id == _selectedId);

        var mapRect = new Rect(rect.x, rect.y, rect.width - PanelWidth - 12f, rect.height - 24f);
        GuideRegistry.Area("gateway.field", mapRect);
        GuideRegistry.Area("gateway.panel", new Rect(rect.xMax - PanelWidth, rect.y, PanelWidth, rect.height));
        if (_listMode)
            DrawList(mapRect, nodes);
        else
            DrawCircles(mapRect, nodes);

        if (gateway.StormActive)
        {
            var storm = new Rect(mapRect.x, mapRect.y, mapRect.width - 120f, 24f);
            Widgets.DrawBoxSolid(storm, new Color(NetrunUI.Purple.r, NetrunUI.Purple.g, NetrunUI.Purple.b, 0.2f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(storm, "NR_GatewayStorm".Translate(gateway.StormTicksLeft.ToStringTicksToPeriod()).Colorize(NetrunUI.Purple));
            Text.Anchor = TextAnchor.UpperLeft;
            NetrunUI.Tip(storm, "NR_GatewayStormTip".Translate());
        }

        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(rect.x, rect.yMax - 20f, mapRect.width, 20f), "NR_GatewayLegend".Translate().Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;

        DrawPanel(new Rect(rect.xMax - PanelWidth, rect.y, PanelWidth, rect.height), map, gateway, selected);
    }

    /// <summary>
    /// The node field: a grid that can be dragged with the mouse, links from a hub, and the circles.
    /// Drawn in a clipped group so dragged circles never spill over the panel.
    /// </summary>
    private static void DrawCircles(Rect rect, IReadOnlyList<ExternalNode> nodes)
    {
        HandleDrag(rect);

        Widgets.BeginGroup(rect);
        var local = new Rect(0f, 0f, rect.width, rect.height);
        DrawGrid(local);

        Vector2 center = new(local.width * 0.5f, local.height * 0.55f);
        Vector2 hub = center + _pan;
        float time = Time.realtimeSinceStartup;

        var placed = new List<(ExternalNode node, Vector2 pos, float radius)>();
        for (int i = 0; i < nodes.Count && i < Slots.Length; i++)
        {
            ExternalNode node = nodes[i];
            Vector2 slot = Slots[i];
            float bob = NetrunMod.Settings.Animations ? Mathf.Sin(time * 0.8f + node.Id) * 4f : 0f;
            // Zoom spreads the slots around the field centre; circles grow with it.
            var slotPos = new Vector2(local.width * slot.x, local.height * slot.y + bob);
            var pos = center + (slotPos - center) * _zoom + _pan;
            float radius = (node.Hidden ? 36f : Mathf.Lerp(46f, 68f, Mathf.Clamp01(node.RewardScore))) * Mathf.Sqrt(_zoom);
            placed.Add((node, pos, radius));
        }

        foreach (var p in placed.Where(p => !p.node.Hidden))
        {
            // The line leaves the home node's edge and stops at the circle's rim.
            Vector2 dir = (p.pos - hub).normalized;
            DrawLink(hub + dir * 34f, p.pos - dir * p.radius, p.node.Color, p.node.Id);
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
    }

    private static void ResetCamera()
    {
        _zoom = 1f;
        _pan = Vector2.zero;
    }

    /// <summary>Left-drag on the field pans it (kept within one field size); the mouse wheel zooms.</summary>
    private static void HandleDrag(Rect rect)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDrag && e.button == 0 && Mouse.IsOver(rect))
        {
            _pan += e.delta;
            _pan.x = Mathf.Clamp(_pan.x, -rect.width * 0.6f, rect.width * 0.6f);
            _pan.y = Mathf.Clamp(_pan.y, -rect.height * 0.6f, rect.height * 0.6f);
            e.Use();
        }
        else if (e.type == EventType.MouseDown && e.button == 2 && Mouse.IsOver(rect))
        {
            ResetCamera();
            e.Use();
        }
        else if (e.type == EventType.ScrollWheel && Mouse.IsOver(rect))
        {
            _zoom = Mathf.Clamp(_zoom - e.delta.y * 0.05f, MinZoom, MaxZoom);
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
            Widgets.DrawBoxSolid(new Rect(x, 0f, 1f, rect.height), new Color(GridColor.r, GridColor.g, GridColor.b, major ? 0.16f : 0.08f));
        }
        int row = 0;
        for (float y = oy; y < rect.height; y += step, row++)
        {
            bool major = (row - iy) % 5 == 0;
            Widgets.DrawBoxSolid(new Rect(0f, y, rect.width, 1f), new Color(GridColor.r, GridColor.g, GridColor.b, major ? 0.16f : 0.08f));
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
            var rnd = new System.Random(slot * 31 + k * 977);
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
        // Ripple: every ~7 s a faint double ring spreads over the grid.
        {
            const float period = 7f;
            float f = now / period;
            int slot = Mathf.FloorToInt(f);
            float t = f - slot;
            if (t < 0.45f)
            {
                var rnd = new System.Random(slot * 7919);
                var c = new Vector2((float)rnd.NextDouble() * rect.width, (float)rnd.NextDouble() * rect.height);
                float r = Mathf.Lerp(10f, Mathf.Max(rect.width, rect.height) * 0.45f, t / 0.45f);
                GUI.color = new Color(GridColor.r, GridColor.g, GridColor.b, 0.12f * (1f - t / 0.45f));
                GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), NetrunUI.ThinRing);
                GUI.DrawTexture(new Rect(c.x - r * 0.96f, c.y - r * 0.96f, r * 1.92f, r * 1.92f), NetrunUI.ThinRing);
            }
        }
        GUI.color = Color.white;
    }

    /// <summary>A link from the home node: a soft glow under a thin line, and data packets running out to the node.</summary>
    private static void DrawLink(Vector2 from, Vector2 to, Color color, int seed)
    {
        Widgets.DrawLine(from, to, new Color(color.r, color.g, color.b, 0.10f), 6f);
        Widgets.DrawLine(from, to, new Color(color.r, color.g, color.b, 0.45f), 1.5f);
        if (!NetrunMod.Settings.Animations || Event.current.type != EventType.Repaint)
            return;
        float length = Vector2.Distance(from, to);
        float period = Mathf.Max(1.2f, length / 90f);
        for (int k = 0; k < 2; k++)
        {
            float t = (Time.realtimeSinceStartup / period + k * 0.5f + seed * 0.37f) % 1f;
            Vector2 p = Vector2.Lerp(from, to, t);
            float a = Mathf.Sin(t * Mathf.PI);
            GUI.color = new Color(color.r, color.g, color.b, 0.4f * a);
            GUI.DrawTexture(new Rect(p.x - 7f, p.y - 7f, 14f, 14f), NetrunUI.GlowTex);
            GUI.color = new Color(1f, 1f, 1f, 0.85f * a);
            GUI.DrawTexture(new Rect(p.x - 2.5f, p.y - 2.5f, 5f, 5f), Disc);
        }
        GUI.color = Color.white;
    }

    /// <summary>The home node at the hub: back to the terminal's main screen.</summary>
    private static void DrawHome(Vector2 hub)
    {
        const float size = 54f;
        var r = new Rect(hub.x - size / 2f, hub.y - size / 2f, size, size);
        float lit = NetrunUI.HoverFade(r);
        Color c = NetrunUI.Accent;
        if (lit > 0f)
            NetrunUI.Glow(r, c, 0.8f * lit, 12f);
        Widgets.DrawBoxSolid(r, NetrunUI.BgWindow);
        NetrunUI.Brackets(r, c, 2.5f, 14f);
        GUI.DrawTexture(r.ContractedBy(10f), NetrunTex.NetTerminal);
        NetrunUI.ScanlineOnce(r.ContractedBy(3f), c, NetrunUI.HoverTime(r));
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperCenter;
        Widgets.Label(new Rect(r.x - 30f, r.yMax + 4f, r.width + 60f, 18f), "NR_GatewayHome".Translate().Colorize(c));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        NetrunUI.Tip(r, "NR_GatewayHomeTip".Translate());
        if (Widgets.ButtonInvisible(r))
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
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

        Text.Anchor = TextAnchor.MiddleCenter;
        if (node.Hidden)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            GUI.DrawTexture(new Rect(rect.center.x - 18f, rect.center.y - 26f, 36f, 36f), NetrunTex.HiddenNode);
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x, rect.center.y + 10f, rect.width, 18f), "NR_GatewayHidden".Translate().Colorize(NetrunUI.Muted));
        }
        else
        {
            if (node.Icon != null)
                GUI.DrawTexture(new Rect(rect.center.x - 20f, rect.center.y - 44f, 40f, 40f), node.Icon);
            Widgets.Label(new Rect(rect.x - 6f, rect.center.y - 4f, rect.width + 12f, 22f), node.Name.Colorize(color));
            Text.Font = GameFont.Tiny;
            string sub = node.BlackIce ? "NR_GatewayBlackIce".Translate(node.Defense) : "NR_GatewayDefense".Translate(node.Defense);
            if (node.Contract)
                sub += " · ★";
            if (node.Rival)
                sub += " · ⚔";
            Widgets.Label(new Rect(rect.x - 10f, rect.center.y + 16f, rect.width + 20f, 18f), sub.Colorize(node.BlackIce ? NetrunUI.Bad : color));
        }
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        if (Widgets.ButtonInvisible(rect))
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            _selectedId = node.Id;
        }
    }
    private static void DrawList(Rect rect, IReadOnlyList<ExternalNode> nodes)
    {
        const float rowHeight = 40f;
        var view = new Rect(0f, 0f, rect.width - 16f, nodes.Count * (rowHeight + 4f));
        Widgets.BeginScrollView(rect, ref _listScroll, view);
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
        bool scrolls = _panelHeight > outer.height;
        var inner = new Rect(0f, 0f, outer.width - (scrolls ? 16f : 0f), Mathf.Max(_panelHeight, outer.height));
        Widgets.BeginScrollView(outer, ref _panelScroll, inner);
        float y = inner.y;
        float gNodeStart = y;

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
                var line = new Rect(inner.x + 24f, y, inner.width - 24f, 22f);
                Widgets.ThingIcon(new Rect(line.x, line.y + 1f, 20f, 20f), def);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(line.x + 26f, line.y, line.width - 90f, line.height), def.LabelCap.ToString().Truncate(line.width - 90f));
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
        Pawn runner = GatewayManager.Runner(map, out Building_Cyberpod pod);
        int slots = GatewayManager.ProgramSlots(pod);
        y = NetrunUI.Section(new Rect(inner.x, y + 6f, inner.width, 36f), "NR_GatewayPrograms".Translate(slots));
        float x = inner.x;
        foreach (DiveProgram program in System.Enum.GetValues(typeof(DiveProgram)))
        {
            bool on = gateway.SelectedPrograms.Contains(program);
            string label = ("NR_Program_" + program).Translate();
            Texture2D programIcon = NetrunTex.CyberIcon("Program_" + program);
            float w = NetrunUI.ButtonWidth(label, programIcon != null);
            if (x + w > inner.xMax)
            {
                x = inner.x;
                y += 34f;
            }
            AcceptanceReport report = on || gateway.SelectedPrograms.Count < slots ? true : (AcceptanceReport)"NR_GatewayNoSlots".Translate();
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

        // Daemons: permanent programs, always on, no slot. Icons for those that have art, then the text.
        y = DaemonIcons(inner, y, gateway.Daemons);
        Text.Font = GameFont.Tiny;
        string daemons = gateway.Daemons.Count == 0
            ? "NR_GatewayNoDaemons".Translate().Colorize(NetrunUI.Muted)
            : "NR_GatewayDaemons".Translate(gateway.Daemons.Select(d => (string)("NR_Daemon_" + d).Translate()).ToCommaList()).Colorize(NetrunUI.Ok);
        float dh = Text.CalcHeight(daemons, inner.width);
        var daemonRect = new Rect(inner.x, y, inner.width, dh);
        Widgets.Label(daemonRect, daemons);
        NetrunUI.Tip(daemonRect, gateway.Daemons.Count == 0
            ? "NR_GatewayDaemonsTip".Translate()
            : string.Join("\n", gateway.Daemons.Select(d => (string)("NR_Daemon_" + d).Translate() + ": " + ("NR_DaemonDesc_" + d).Translate())));
        Text.Font = GameFont.Small;
        y += dh + 8f;

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
        float gSearch = y;
        // Search for hidden nodes and switch the view.
        if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 30f), "NR_GatewaySearch".Translate(NetrunDefOf.NR_Cyberspace.searchCost), NetrunUI.Ok,
                gateway.CanSearch(map), "NR_GatewaySearchTip".Translate()))
        {
            ExternalNode found = gateway.Search(map);
            if (found != null)
                _selectedId = found.Id;
        }
        y += 38f;

        if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 26f), (_listMode ? "NR_GatewayViewCircles" : "NR_GatewayViewList").Translate(), NetrunUI.Muted, true))
            _listMode = !_listMode;
        y += 32f;

        Text.Font = GameFont.Tiny;
        string refresh = "NR_GatewayRefresh".Translate(gateway.TicksUntilRefresh.ToStringTicksToPeriod());
        float rh = Text.CalcHeight(refresh, inner.width);
        Widgets.Label(new Rect(inner.x, y, inner.width, rh), refresh.Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        GuideRegistry.InScroll("gateway.search", outer, _panelScroll, new Rect(inner.x, gSearch, inner.width, y - gSearch));
        _panelHeight = y + rh + 4f;
        Widgets.EndScrollView();
    }

    /// <summary>A row of daemon icons (only daemons with art); returns the next y.</summary>
    public static float DaemonIcons(Rect inner, float y, IEnumerable<DiveDaemon> daemons)
    {
        float x = inner.x;
        bool any = false;
        foreach (DiveDaemon d in daemons)
        {
            Texture2D icon = NetrunTex.DaemonIcon(d);
            if (icon == null)
                continue;
            var r = new Rect(x, y, 28f, 28f);
            GUI.DrawTexture(r, icon);
            NetrunUI.Tip(r, (string)("NR_Daemon_" + d).Translate() + ": " + ("NR_DaemonDesc_" + d).Translate());
            x += 32f;
            any = true;
        }
        return any ? y + 32f : y;
    }

    /// <summary>One line of the node panel: icon, label, then the value (after small item icons, if any).</summary>
    private static float Row(Rect inner, float y, Texture2D icon, string label, string value, Color color, string tip,
        IEnumerable<Texture2D> valueIcons = null)
    {
        const float labelWidth = 102f;
        if (icon != null)
            GUI.DrawTexture(new Rect(inner.x, y + 1f, 20f, 20f), icon);
        Widgets.Label(new Rect(inner.x + 24f, y, labelWidth - 24f, 22f), label.Colorize(NetrunUI.Muted));
        float vx = inner.x + labelWidth;
        if (valueIcons != null)
        {
            foreach (Texture2D tex in valueIcons.Where(t => t != null))
            {
                GUI.DrawTexture(new Rect(vx, y + 1f, 20f, 20f), tex);
                vx += 22f;
            }
            vx += 2f;
        }
        float h = Mathf.Max(22f, Text.CalcHeight(value, inner.xMax - vx));
        Widgets.Label(new Rect(vx, y, inner.xMax - vx, h), value.Colorize(color));
        NetrunUI.Tip(new Rect(inner.x, y, inner.width, h), tip);
        GUI.color = new Color(1f, 1f, 1f, 0.08f);
        Widgets.DrawLineHorizontal(inner.x, y + h + 1f, inner.width);
        GUI.color = Color.white;
        return y + h + 3f;
    }

    /// <summary>Title chips for the Gateway: who would dive and whether a pod is ready.</summary>
    public static void HeaderChips(float xMax, float y, Map map)
    {
        Pawn runner = GatewayManager.Runner(map, out _);
        float x = NetrunUI.HeaderChip(xMax, y, runner != null
            ? "NR_GatewayPodReady".Translate().Colorize(NetrunUI.Ok)
            : "NR_GatewayPodNone".Translate().Colorize(NetrunUI.Bad), "NR_TipPod".Translate()) - 8f;
        if (runner != null)
            NetrunUI.HeaderChip(x, y, "NR_GatewayRunner".Translate(runner.LabelShort), "NR_TipRunner".Translate());
    }
}
