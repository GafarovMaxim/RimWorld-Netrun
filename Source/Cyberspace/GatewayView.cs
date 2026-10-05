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

    private static void DrawCircles(Rect rect, IReadOnlyList<ExternalNode> nodes)
    {
        Vector2 hub = new(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.55f);
        float time = Time.realtimeSinceStartup;

        var placed = new List<(ExternalNode node, Vector2 pos, float radius)>();
        for (int i = 0; i < nodes.Count && i < Slots.Length; i++)
        {
            ExternalNode node = nodes[i];
            Vector2 slot = Slots[i];
            float bob = Mathf.Sin(time * 0.8f + node.Id) * 4f;
            var pos = new Vector2(rect.x + rect.width * slot.x, rect.y + rect.height * slot.y + bob);
            float radius = node.Hidden ? 30f : Mathf.Lerp(38f, 62f, Mathf.Clamp01(node.RewardScore));
            placed.Add((node, pos, radius));
        }

        foreach (var p in placed.Where(p => !p.node.Hidden))
            Widgets.DrawLine(hub, p.pos, new Color(NetrunUI.Line.r, NetrunUI.Line.g, NetrunUI.Line.b, 0.5f), 1f);

        foreach (var (node, pos, radius) in placed)
            DrawCircle(node, pos, radius);
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
            GUI.color = new Color(0.07f, 0.08f, 0.07f, 1f);
            GUI.DrawTexture(rect, Disc);
        }
        GUI.color = new Color(color.r, color.g, color.b, node.Hidden ? 0.5f : 1f);
        GUI.DrawTexture(rect, Ring);
        GUI.color = Color.white;

        Text.Anchor = TextAnchor.MiddleCenter;
        if (node.Hidden)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.4f);
            GUI.DrawTexture(new Rect(rect.center.x - 12f, rect.center.y - 22f, 24f, 24f), NetrunTex.HiddenNode);
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x, rect.center.y + 2f, rect.width, 18f), "NR_GatewayHidden".Translate().Colorize(NetrunUI.Muted));
        }
        else
        {
            if (node.Icon != null)
                GUI.DrawTexture(new Rect(rect.center.x - 14f, rect.center.y - 46f, 28f, 28f), node.Icon);
            Widgets.Label(new Rect(rect.x, rect.center.y - 18f, rect.width, 22f), node.Name.Colorize(color));
            Text.Font = GameFont.Tiny;
            string sub = node.BlackIce ? "NR_GatewayBlackIce".Translate(node.Defense) : "NR_GatewayDefense".Translate(node.Defense);
            if (node.Contract)
                sub += " · ★";
            Widgets.Label(new Rect(rect.x - 10f, rect.center.y + 2f, rect.width + 20f, 18f), sub.Colorize(node.BlackIce ? NetrunUI.Bad : color));
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
            y = Row(inner, y, "NR_GatewayRowType".Translate(), node.TypeLabel, NetrunUI.TextColor);
            y = Row(inner, y, "NR_GatewayRowDefense".Translate(), "NR_GatewayDefenseValue".Translate(1 + node.Defense / 2, node.Defense), NetrunUI.Warn);
            y = Row(inner, y, "NR_GatewayRowIce".Translate(),
                node.BlackIce ? "NR_GatewayIceBlack".Translate(node.IceStrength) : "NR_GatewayIceWhite".Translate(),
                node.BlackIce ? NetrunUI.Bad : NetrunUI.TextColor);
            y = Row(inner, y, "NR_GatewayRowReward".Translate(), node.RewardText, NetrunUI.Ok);
            y = Row(inner, y, "NR_GatewayRowTrace".Translate(), "NR_GatewayTraceValue".Translate(node.HeatPerTrace.ToString("0.#")), NetrunUI.Warn);
            y = Row(inner, y, "NR_GatewayRowRisk".Translate(), node.BlackIce ? "NR_GatewayRiskIce".Translate() : "NR_GatewayRiskLow".Translate(),
                node.BlackIce ? NetrunUI.Bad : NetrunUI.Muted);
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

        // Dive.
        AcceptanceReport dive = gateway.CanDive(map, node);
        if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 44f), "NR_GatewayDive".Translate(), NetrunUI.Accent, dive,
                runner != null ? "NR_GatewayDiveTip".Translate(runner.LabelShort) : null))
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

    private static float Row(Rect inner, float y, string label, string value, Color color)
    {
        Widgets.Label(new Rect(inner.x, y, 80f, 22f), label.Colorize(NetrunUI.Muted));
        float h = Mathf.Max(22f, Text.CalcHeight(value, inner.width - 84f));
        Widgets.Label(new Rect(inner.x + 84f, y, inner.width - 84f, h), value.Colorize(color));
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
