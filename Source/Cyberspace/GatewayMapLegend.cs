using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// D1: the interactive map legend (docs/CONCEPTS_D_2026-10-09.md). A folding plate in the lower left corner of the
/// map lists what the marks mean; a click on a row lights every hub it applies to and dims the rest to 35%, a second
/// click (or Esc) lets go. The lit set is built once on the click, not per frame. Also the textures of the v4 look
/// (D9): the hollow hub ring and the dashed ring of fortresses and corporate ground.
/// </summary>
public static partial class GatewayView
{
    // ---- Textures (D9) ----

    /// <summary>A hub's ring: a band about a sixth of the radius wide, so it stays ~2 px at hub size.</summary>
    internal static readonly Texture2D HubRing = MakeRing(64, 4.5f, 0);

    /// <summary>A thin dashed circle (fortresses, the edge of corporate ground).</summary>
    internal static readonly Texture2D DashedRing = MakeRing(256, 1.6f, 28);

    private static Texture2D MakeRing(int size, float halfBand, int dashes)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { filterMode = FilterMode.Bilinear };
        float r = size / 2f - halfBand - 1f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - size / 2f, dy = y + 0.5f - size / 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(halfBand + 0.5f - Mathf.Abs(d - r));
                if (dashes > 0 && a > 0f)
                {
                    float t = (Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 1f) * dashes % 1f;
                    a *= Mathf.Clamp01((0.5f - Mathf.Abs(t - 0.5f) * 2f) * 6f + 0.5f);
                }
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // ---- Legend ----

    private enum LegendItem
    {
        Home,
        Exit,
        Fortress,
        Guarded,
        ContractNode,
        GuardGate,
        Visited,
        Watched,
        Patrol,
        Offline,
        Storm,
        Transit,
        LinkFast,
        LinkDark,
        LinkWall,
        LinkJammed,
        LinkToll,
    }

    private static readonly LegendItem[] LegendItems = (LegendItem[])System.Enum.GetValues(typeof(LegendItem));
    private static string[] _legendKeys;
    private static bool _legendOpen;
    // The row picked (an item, or 100 + a corporation slot) and the hubs it lights; null when nothing is picked.
    private static int _legendPick = -1;
    private static HashSet<int> _legendLit;

    private const float LegendRow = 18f, LegendColW = 168f;
    private static readonly string[] LegendHeadKeys = ["NR_Legend_HeadHubs", "NR_Legend_HeadLinks", "NR_Legend_HeadCorps"];

    // The flowed layout: kind 0 = heading (idx = head key), 1 = item (idx = LegendItem), 2 = corp (idx = index in LegendCorps).
    private struct LegendCell
    {
        public int Kind, Idx, Col, Row;
    }

    private static readonly List<LegendCell> LegendCells = [];
    private static (int cols, int corps) _legendCellsFor = (-1, -1);
    private static int _legendRows;

    private static int LegendCols(Rect mapRect) => Mathf.Clamp(Mathf.FloorToInt((mapRect.width * 0.85f - 20f) / LegendColW), 1, 4);

    /// <summary>Hubs, links and corporations flowed into columns top to bottom; a heading is never left alone at a column's foot.</summary>
    private static void LegendLayout(int cols, int corps)
    {
        if (_legendCellsFor == (cols, corps))
            return;
        _legendCellsFor = (cols, corps);
        LegendCells.Clear();
        int hubs = (int)LegendItem.LinkFast;
        int total = 3 + LegendItems.Length + corps;
        int perCol = Mathf.CeilToInt(total / (float)cols);
        int col = 0, row = 0;
        void Place(int kind, int idx)
        {
            if (row >= perCol && col < cols - 1 || kind == 0 && row == perCol - 1 && col < cols - 1)
            {
                col++;
                row = 0;
            }
            LegendCells.Add(new LegendCell { Kind = kind, Idx = idx, Col = col, Row = row });
            row++;
        }
        Place(0, 0);
        for (int i = 0; i < hubs; i++)
            Place(1, i);
        Place(0, 1);
        for (int i = hubs; i < LegendItems.Length; i++)
            Place(1, i);
        if (corps > 0)
        {
            Place(0, 2);
            for (int k = 0; k < corps; k++)
                Place(2, k);
        }
        _legendRows = 0;
        foreach (LegendCell c in LegendCells)
            _legendRows = Mathf.Max(_legendRows, c.Row + 1);
    }

    /// <summary>The plate's rect over the map (folded: the small button only), above the lens scale.</summary>
    private static Rect LegendRect(Rect mapRect, NetMapManager net)
    {
        float bottom = mapRect.yMax - 6f - (Lens != MapLens.Overview ? LensBoxH + 6f : 0f);
        if (!_legendOpen)
            return new Rect(mapRect.x + 6f, bottom - 26f, 110f, 26f);
        int cols = LegendCols(mapRect);
        LegendLayout(cols, LegendCorps(net).Count);
        float h = 30f + 4f + _legendRows * LegendRow + 8f;
        return new Rect(mapRect.x + 6f, bottom - h, LegendColW * cols + 16f, h);
    }

    private static List<(int slot, Faction corp)> LegendCorps(NetMapManager net) => NetrunUI.Cached("map.legendCorps",
        () => net.Map.Nodes.Where(n => n.District == District.Corp).Select(n => n.CorpSlot).Distinct()
            .Select(s => (s, net.CorpOfSlot(s))).Where(x => x.Item2 != null).ToList(), 2f);

    /// <summary>A hub is dimmed while a legend row is picked and it is not one of the lit hubs.</summary>
    private static bool LegendDims(int hubId) => _legendLit != null && !_legendLit.Contains(hubId);

    private static void DrawLegend(Rect mapRect, NetMapManager net)
    {
        if (!NetrunMod.Settings.MapLegend)
        {
            _legendPick = -1;
            _legendLit = null;
            return;
        }
        _legendKeys ??= LegendItems.Select(i => "NR_Legend_" + i).ToArray();
        Rect box = LegendRect(mapRect, net);
        Widgets.DrawBoxSolid(box, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.94f));
        GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, _legendOpen ? 0.5f : 0.35f);
        Widgets.DrawBox(box);
        GUI.color = Color.white;
        Widgets.DrawBoxSolid(new Rect(box.x, box.y, 3f, box.height), NetrunUI.Accent);
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        var head = new Rect(box.x, box.y, box.width, 26f);
        if (Mouse.IsOver(head))
            Widgets.DrawBoxSolid(head, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.1f));
        GUI.color = NetrunUI.Accent;
        Widgets.Label(new Rect(head.x + 10f, head.y, head.width - 16f, head.height), (_legendOpen ? "▾  " : "▸  ") + NetrunUI.Upper(NetrunUI.T("NR_Legend_Title")));
        if (_legendPick >= 0)
        {
            // While a row is lit, the head offers to clear it.
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = NetrunUI.Warn;
            Widgets.Label(new Rect(head.x, head.y, head.width - 10f, head.height), NetrunUI.T("NR_Legend_Clear"));
        }
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.MiddleLeft;
        NetrunUI.Tip(head, () => NetrunUI.T("NR_Legend_Tip"));
        if (Widgets.ButtonInvisible(head))
        {
            NetrunSound.Click();
            if (_legendPick >= 0 && Event.current.mousePosition.x > head.center.x)
            {
                _legendPick = -1;
                _legendLit = null;
            }
            else
                _legendOpen = !_legendOpen;
        }
        if (_legendOpen)
        {
            Widgets.DrawBoxSolid(new Rect(box.x + 8f, head.yMax, box.width - 16f, 1f), new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.25f));
            List<(int slot, Faction corp)> corps = LegendCorps(net);
            LegendLayout(LegendCols(mapRect), corps.Count);
            float y0 = head.yMax + 4f;
            for (int n = 0; n < LegendCells.Count; n++)
            {
                LegendCell c = LegendCells[n];
                var cr = new Rect(box.x + 10f + c.Col * LegendColW, y0 + c.Row * LegendRow, LegendColW - 4f, LegendRow);
                if (c.Kind == 0)
                    LegendHeading(cr, LegendHeadKeys[c.Idx]);
                else if (c.Kind == 1)
                {
                    int i = c.Idx;
                    LegendLine(cr, i, NetrunUI.T(_legendKeys[i]), r => DrawLegendSwatch(r, LegendItems[i]), net);
                }
                else
                {
                    Faction corp = corps[c.Idx].corp;
                    LegendLine(cr, 100 + corps[c.Idx].slot, corp.Name, r =>
                    {
                        GUI.color = CorpColor(corp);
                        GUI.DrawTexture(r.ContractedBy(2f), ShapeTex(Shape.Hexagon, true));
                        GUI.color = Color.white;
                    }, net);
                }
            }
        }
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        if (_legendPick >= 0 && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            _legendPick = -1;
            _legendLit = null;
            Event.current.Use();
        }
    }

    private static void LegendHeading(Rect r, string key)
    {
        GUI.color = NetrunUI.Muted;
        Widgets.Label(r, NetrunUI.Upper(NetrunUI.T(key)));
        GUI.color = Color.white;
    }

    /// <summary>One legend row: swatch, label (cut to fit), lit when picked; a click lights its hubs.</summary>
    private static void LegendLine(Rect row, int pick, string label, System.Action<Rect> swatch, NetMapManager net)
    {
        bool on = pick == _legendPick;
        if (on)
        {
            Widgets.DrawBoxSolid(row, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.22f));
            Widgets.DrawBoxSolid(new Rect(row.x, row.y, 2f, row.height), NetrunUI.Accent);
        }
        else if (Mouse.IsOver(row))
            Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.06f));
        swatch(new Rect(row.x + 4f, row.y + 2f, 14f, 14f));
        GUI.color = on ? Color.white : NetrunUI.TextColor;
        float w = row.width - 26f;
        Widgets.Label(new Rect(row.x + 22f, row.y, w, row.height), label.Truncate(w));
        GUI.color = Color.white;
        if (Widgets.ButtonInvisible(row))
        {
            NetrunSound.Click();
            if (on)
            {
                _legendPick = -1;
                _legendLit = null;
            }
            else
            {
                _legendPick = pick;
                _legendLit = LegendHubs(net, pick);
            }
        }
    }

    private static Color CorpColor(Faction corp)
    {
        Color.RGBToHSV(corp.Color, out float h, out float s, out float v);
        return Color.HSVToRGB(h, Mathf.Min(s, 0.8f), Mathf.Max(v, 0.85f));
    }

    /// <summary>The hubs a legend row stands for (built once, on the click).</summary>
    private static HashSet<int> LegendHubs(NetMapManager net, int pick)
    {
        CyberMap map = net.Map;
        var lit = new HashSet<int>();
        if (pick >= 100)
        {
            foreach (MapNode n in map.Nodes)
                if (n.District == District.Corp && n.CorpSlot == pick - 100)
                    lit.Add(n.Id);
            return lit;
        }
        LegendItem item = LegendItems[pick];
        HashSet<int> watched = WatchedHubs();
        foreach (MapNode n in map.Nodes)
        {
            bool yes = item switch
            {
                LegendItem.Home => n.District == District.Home,
                LegendItem.Exit => n.Exit,
                LegendItem.Fortress => n.Fortress,
                LegendItem.Guarded => n.Guarded && NetrunMod.Settings.ProtectedHubs || GatewayManager.Get()?.AllBranches.Any(b => b.GuardFor >= 0 && b.MapNodeId == n.Id) == true,
                LegendItem.ContractNode => GatewayManager.Get()?.AllBranches.Any(b => b.Contract && !b.Hidden && b.MapNodeId == n.Id) == true,
                LegendItem.GuardGate => GatewayManager.Get()?.AllBranches.Any(b => b.GuardFor >= 0 && !b.Hidden && b.MapNodeId == n.Id) == true,
                LegendItem.Visited => n.LastVisitTick >= 0,
                LegendItem.Watched => watched.Contains(n.Id) || net.Watch.Contains(n.Id),
                LegendItem.Patrol => n.Patrol,
                LegendItem.Offline => n.Offline,
                LegendItem.Storm => n.Storm,
                LegendItem.Transit => n.Transit,
                _ => false,
            };
            if (yes)
                lit.Add(n.Id);
        }
        if (item >= LegendItem.LinkFast)
            foreach (MapLink l in map.Links)
            {
                if (!l.Known)
                    continue;
                bool yes = item switch
                {
                    LegendItem.LinkFast => l.Kind == LinkKind.Fast,
                    LegendItem.LinkDark => l.Kind is LinkKind.Dark or LinkKind.Backdoor,
                    LegendItem.LinkWall => l.Kind == LinkKind.Wall,
                    LegendItem.LinkJammed => l.Jammed,
                    LegendItem.LinkToll => l.Toll,
                    _ => false,
                };
                if (yes)
                {
                    lit.Add(l.A);
                    lit.Add(l.B);
                }
            }
        return lit;
    }

    private static void DrawLegendSwatch(Rect r, LegendItem item)
    {
        Vector2 a = new(r.x, r.center.y), b = new(r.xMax, r.center.y);
        switch (item)
        {
            case LegendItem.Home:
                GUI.color = NetrunUI.Accent;
                GUI.DrawTexture(r, NetrunUI.Ring);
                break;
            case LegendItem.Exit:
                DrawExitMark(r.ContractedBy(1f), 1f);
                break;
            case LegendItem.Fortress:
                GUI.color = new Color(NetrunUI.TextColor.r, NetrunUI.TextColor.g, NetrunUI.TextColor.b, 0.6f);
                GUI.DrawTexture(r, NetrunUI.ThinRing);
                GUI.color = NetrunUI.TextColor;
                GUI.DrawTexture(r.ContractedBy(4f), ShapeTex(Shape.Diamond, true));
                break;
            case LegendItem.Guarded:
                GUI.color = NetrunUI.Warn;
                GUI.DrawTexture(r, DashedRing);
                if (NetrunTex.CyberIcon("Icon_Lock") is { } lockTex)
                    GUI.DrawTexture(r.ContractedBy(3f), lockTex);
                break;
            case LegendItem.ContractNode:
                DrawNodeBadge(r.ContractedBy(3f), true);
                break;
            case LegendItem.GuardGate:
                DrawNodeBadge(r.ContractedBy(3f), false);
                break;
            case LegendItem.Visited:
                GUI.color = NetrunUI.TextColor;
                GUI.DrawTexture(r.ContractedBy(2f), NetrunUI.Disc);
                break;
            case LegendItem.Watched:
                GUI.color = NetrunUI.Accent;
                Widgets.Label(r.ExpandedBy(2f), "◉");
                break;
            case LegendItem.Patrol:
                GUI.color = NetrunUI.Bad;
                GUI.DrawTexture(r.ContractedBy(1f), ShapeTex(Shape.Triangle, true));
                break;
            case LegendItem.Offline:
                GUI.color = NetrunUI.Muted;
                GUI.DrawTexture(r, DashedRing);
                break;
            case LegendItem.Storm:
                GUI.color = NetrunUI.Purple;
                GUI.DrawTexture(r.ContractedBy(2f), NetrunUI.Disc);
                break;
            case LegendItem.Transit:
                GUI.color = new Color(0.24f, 0.82f, 0.9f);
                GUI.DrawTexture(r.ContractedBy(2f), NetrunUI.Disc);
                break;
            case LegendItem.LinkFast:
                Widgets.DrawLine(a, b, new Color(0.24f, 0.82f, 0.9f, 0.9f), 2f);
                break;
            case LegendItem.LinkDark:
                Widgets.DrawLine(a, Vector2.Lerp(a, b, 0.4f), NetrunUI.Purple, 2f);
                Widgets.DrawLine(Vector2.Lerp(a, b, 0.6f), b, NetrunUI.Purple, 2f);
                break;
            case LegendItem.LinkWall:
                Widgets.DrawLine(a, Vector2.Lerp(a, b, 0.4f), NetrunUI.Bad, 2f);
                Widgets.DrawLine(Vector2.Lerp(a, b, 0.6f), b, NetrunUI.Bad, 2f);
                break;
            case LegendItem.LinkJammed:
                Widgets.DrawLine(a, b, new Color(0.95f, 0.6f, 0.2f), 2f);
                break;
            case LegendItem.LinkToll:
                Widgets.DrawLine(a, b, new Color(0.55f, 0.62f, 0.55f, 0.6f), 1.5f);
                GUI.color = NetrunUI.Warn;
                GUI.DrawTexture(new Rect(r.center.x - 3f, r.center.y - 3f, 6f, 6f), NetrunUI.Disc);
                break;
        }
        GUI.color = Color.white;
    }
}
