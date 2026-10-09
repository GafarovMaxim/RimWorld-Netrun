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

    private const float LegendRow = 17f, LegendColW = 150f;

    /// <summary>The plate's rect over the map (folded: the small button only), above the lens scale.</summary>
    private static Rect LegendRect(Rect mapRect, NetMapManager net)
    {
        float bottom = mapRect.yMax - 6f - (Lens != MapLens.Overview ? 32f : 0f);
        if (!_legendOpen)
            return new Rect(mapRect.x + 6f, bottom - 24f, 96f, 24f);
        int rows = Mathf.CeilToInt((LegendItems.Length + LegendCorps(net).Count) / 2f);
        float h = 26f + rows * LegendRow + 6f;
        return new Rect(mapRect.x + 6f, bottom - h, LegendColW * 2f + 12f, h);
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
        Widgets.DrawBoxSolid(box, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.92f));
        GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.4f);
        Widgets.DrawBox(box);
        GUI.color = Color.white;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        var head = new Rect(box.x, box.y, box.width, 24f);
        GUI.color = NetrunUI.Accent;
        Widgets.Label(new Rect(head.x + 8f, head.y, head.width - 16f, head.height), NetrunUI.Upper(NetrunUI.T("NR_Legend_Title")) + (_legendOpen ? "  ▾" : "  ▸"));
        GUI.color = Color.white;
        NetrunUI.Tip(head, () => NetrunUI.T("NR_Legend_Tip"));
        if (Widgets.ButtonInvisible(head))
        {
            NetrunSound.Click();
            _legendOpen = !_legendOpen;
        }
        if (_legendOpen)
        {
            List<(int slot, Faction corp)> corps = LegendCorps(net);
            int count = LegendItems.Length + corps.Count;
            int rows = Mathf.CeilToInt(count / 2f);
            for (int i = 0; i < count; i++)
            {
                var row = new Rect(box.x + 6f + i / rows * LegendColW, box.y + 26f + i % rows * LegendRow, LegendColW, LegendRow);
                int pick = i < LegendItems.Length ? i : 100 + corps[i - LegendItems.Length].slot;
                bool on = pick == _legendPick;
                if (on)
                    Widgets.DrawBoxSolid(row, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.2f));
                else if (Mouse.IsOver(row))
                    Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.06f));
                var sw = new Rect(row.x + 2f, row.y + 2f, 13f, 13f);
                string label;
                if (i < LegendItems.Length)
                {
                    DrawLegendSwatch(sw, LegendItems[i]);
                    label = NetrunUI.T(_legendKeys[i]);
                }
                else
                {
                    Faction corp = corps[i - LegendItems.Length].corp;
                    GUI.color = CorpColor(corp);
                    GUI.DrawTexture(sw.ContractedBy(1f), NetrunUI.Disc);
                    GUI.color = Color.white;
                    label = corp.Name;
                }
                GUI.color = on ? Color.white : NetrunUI.TextColor;
                Widgets.Label(new Rect(row.x + 20f, row.y, row.width - 22f, row.height), label);
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
                GUI.color = NetrunUI.Ok;
                GUI.DrawTexture(r, NetrunUI.ThinRing);
                break;
            case LegendItem.Fortress:
                GUI.color = NetrunUI.Bad;
                GUI.DrawTexture(r, DashedRing);
                break;
            case LegendItem.Guarded:
                GUI.color = NetrunUI.Warn;
                GUI.DrawTexture(r, DashedRing);
                if (NetrunTex.CyberIcon("Icon_Lock") is { } lockTex)
                    GUI.DrawTexture(r.ContractedBy(3f), lockTex);
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
                Widgets.Label(r.ExpandedBy(2f), " !");
                break;
            case LegendItem.Offline:
                GUI.color = NetrunUI.Bad;
                Widgets.Label(r.ExpandedBy(2f), "✕");
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
