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

    private static readonly Texture2D Disc = MakeCircle(filled: true);
    private static readonly Texture2D Ring = MakeCircle(filled: false);

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

    private static Texture2D MakeCircle(bool filled)
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { filterMode = FilterMode.Bilinear };
        float r = size / 2f - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                float alpha = filled
                    ? Mathf.Clamp01(r - d + 0.5f)
                    : Mathf.Clamp01(1f - Mathf.Abs(d - (r - 3f)) / 3f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    public static void Draw(Rect rect, Map map)
    {
        GatewayManager gateway = GatewayManager.Get();
        if (gateway == null)
            return;

        IReadOnlyList<ExternalNode> nodes = gateway.Nodes;
        ExternalNode selected = nodes.FirstOrDefault(n => n.Id == _selectedId);

        var mapRect = new Rect(rect.x, rect.y, rect.width - PanelWidth - 12f, rect.height - 24f);
        if (_listMode)
            DrawList(mapRect, nodes);
        else
            DrawCircles(mapRect, nodes);

        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(rect.x, rect.yMax - 20f, mapRect.width, 20f), "NR_GatewayLegend".Translate().Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;

        DrawPanel(new Rect(rect.xMax - PanelWidth, rect.y - 40f, PanelWidth, rect.height + 40f), map, gateway, selected);
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
            float bob = Mathf.Sin(time * 0.8f + node.Id) * 4f;
            // Zoom spreads the slots around the field centre; circles grow with it.
            var slotPos = new Vector2(local.width * slot.x, local.height * slot.y + bob);
            var pos = center + (slotPos - center) * _zoom + _pan;
            float radius = (node.Hidden ? 36f : Mathf.Lerp(46f, 68f, Mathf.Clamp01(node.RewardScore))) * Mathf.Sqrt(_zoom);
            placed.Add((node, pos, radius));
        }

        foreach (var p in placed.Where(p => !p.node.Hidden))
            Widgets.DrawLine(hub, p.pos, new Color(NetrunUI.Line.r, NetrunUI.Line.g, NetrunUI.Line.b, 0.5f), 1f);

        foreach (var (node, pos, radius) in placed)
            DrawCircle(node, pos, radius);
        Widgets.EndGroup();

        DrawZoomButtons(new Rect(rect.xMax - 3 * 34f - 4f, rect.y + 4f, 3 * 34f, 30f));
    }

    /// <summary>Zoom in, zoom out and reset (also resets the pan).</summary>
    private static void DrawZoomButtons(Rect rect)
    {
        if (NetrunUI.Button(new Rect(rect.x, rect.y, 30f, 30f), "+", NetrunUI.Accent, _zoom < MaxZoom ? true : (AcceptanceReport)false, "NR_GatewayZoomIn".Translate()))
            _zoom = Mathf.Min(MaxZoom, _zoom + 0.2f);
        if (NetrunUI.Button(new Rect(rect.x + 34f, rect.y, 30f, 30f), "−", NetrunUI.Accent, _zoom > MinZoom ? true : (AcceptanceReport)false, "NR_GatewayZoomOut".Translate()))
            _zoom = Mathf.Max(MinZoom, _zoom - 0.2f);
        if (NetrunUI.Button(new Rect(rect.x + 68f, rect.y, 30f, 30f), "◎", NetrunUI.Muted, true, "NR_GatewayZoomReset".Translate()))
        {
            _zoom = 1f;
            _pan = Vector2.zero;
        }
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
        else if (e.type == EventType.ScrollWheel && Mouse.IsOver(rect))
        {
            _zoom = Mathf.Clamp(_zoom - e.delta.y * 0.05f, MinZoom, MaxZoom);
            e.Use();
        }
    }

    /// <summary>The "space" behind the nodes: a faint grid that moves with the pan.</summary>
    private static void DrawGrid(Rect rect)
    {
        float step = 40f * _zoom;
        var color = new Color(NetrunUI.Line.r, NetrunUI.Line.g, NetrunUI.Line.b, 0.12f);
        float ox = Mathf.Repeat(_pan.x, step);
        float oy = Mathf.Repeat(_pan.y, step);
        for (float x = ox; x < rect.width; x += step)
            Widgets.DrawBoxSolid(new Rect(x, 0f, 1f, rect.height), color);
        for (float y = oy; y < rect.height; y += step)
            Widgets.DrawBoxSolid(new Rect(0f, y, rect.width, 1f), color);
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
        Rect inner = rect.ContractedBy(10f);
        float y = inner.y;

        if (node == null || node.Hidden)
        {
            Widgets.Label(new Rect(inner.x, y, inner.width, 60f), (node == null ? "NR_GatewayPickNode" : "NR_GatewayNodeHidden").Translate().Colorize(NetrunUI.Muted));
            y += 64f;
        }
        else
        {
            y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), node.Name);
            y = Row(inner, y, node.Icon, "NR_GatewayRowType".Translate(), node.TypeLabel, NetrunUI.TextColor, "NR_GatewayTipType".Translate());
            y = Row(inner, y, NetrunTex.CyberIcon("Dive_GATE"), "NR_GatewayRowDefense".Translate(),
                "NR_GatewayDefenseValue".Translate(1 + node.Defense / 2, node.Defense), NetrunUI.Warn, "NR_GatewayTipDefense".Translate());
            y = Row(inner, y, NetrunTex.CyberIcon(node.BlackIce ? "Dive_BLACKICE" : "Dive_ICE"), "NR_GatewayRowIce".Translate(),
                node.BlackIce ? "NR_GatewayIceBlack".Translate(node.IceStrength) : "NR_GatewayIceWhite".Translate(),
                node.BlackIce ? NetrunUI.Bad : NetrunUI.TextColor, "NR_GatewayTipIce".Translate());
            y = Row(inner, y, null, "NR_GatewayRowReward".Translate(), node.RewardText, NetrunUI.Ok, "NR_GatewayTipReward".Translate(), node.RewardIcons);
            y = Row(inner, y, NetrunTex.CyberIcon("Dive_Heat"), "NR_GatewayRowTrace".Translate(),
                "NR_GatewayTraceValue".Translate(node.HeatPerTrace.ToString("0.#")), NetrunUI.Warn, "NR_GatewayTipTrace".Translate());
            Color riskColor = node.Risk switch { 2 => NetrunUI.Bad, 1 => NetrunUI.Warn, _ => NetrunUI.Muted };
            y = Row(inner, y, node.RiskIcon, "NR_GatewayRowRisk".Translate(), ("NR_GatewayRisk_" + node.Risk).Translate(), riskColor, "NR_GatewayTipRisk".Translate());
            if (node.Contract)
            {
                y += 4f;
                NetrunUI.Chip(inner.x, y, "NR_GatewayContract".Translate(), NetrunUI.Ok);
                y += 24f;
            }
        }

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
        y += 40f;

        // Dive cost with its breakdown, then the button.
        int cost = gateway.DiveCost(map, node, out string breakdown);
        var costRect = new Rect(inner.x, y, inner.width, 22f);
        Widgets.Label(costRect, "NR_DiveCostLine".Translate(cost).Colorize(NetrunUI.Warn));
        TooltipHandler.TipRegion(costRect, breakdown);
        y += 26f;

        AcceptanceReport dive = gateway.CanDive(map, node);
        string diveTip = (runner != null ? "NR_GatewayDiveTip".Translate(runner.LabelShort) + "\n\n" : string.Empty) + breakdown;
        if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 48f), "NR_GatewayDive".Translate(), NetrunUI.Accent, dive, diveTip, NetrunTex.Dive))
            gateway.Dive(map, node);
        y += 52f;

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
        Widgets.Label(new Rect(inner.x, y, inner.width, 40f),
            "NR_GatewayRefresh".Translate(gateway.TicksUntilRefresh.ToStringTicksToPeriod()).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
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
        TooltipHandler.TipRegion(new Rect(inner.x, y, inner.width, h), tip);
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
            : "NR_GatewayPodNone".Translate().Colorize(NetrunUI.Bad)) - 8f;
        if (runner != null)
            NetrunUI.HeaderChip(x, y, "NR_GatewayRunner".Translate(runner.LabelShort));
    }
}
