using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Netrun;

/// <summary>
/// "Net found" (docs/CYBERSPACE_V2.md, section 10): at the first entry the player picks the layout template and the size,
/// watches a sketch of the net being probed, and jacks in. Both choices are kept in the game; the mod settings stay the
/// defaults for new games. Terminal look (Maxim, 2026-10-17): brackets, scan line, everything in the terminal accent.
/// </summary>
public class Dialog_NetFound : Window
{
    private const float RevealSeconds = 1.1f;
    private const float Pad = 16f;

    /// <summary>-1 = the world seed's pick, otherwise a NetLayout.</summary>
    private int _layout;
    private NetSize _size;
    private CyberMap _preview;
    private int _previewLayout = -9;
    private NetSize _previewSize = (NetSize)(-1);
    private float _revealStart;
    private readonly Dictionary<(int, NetSize), int> _hubCounts = [];

    public Dialog_NetFound()
    {
        _size = NetrunMod.Settings.MapSize;
        _layout = Mathf.Clamp(NetrunMod.Settings.MapLayout, -1, (int)NetLayout.Metro);
        forcePause = true;
        doCloseX = false;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
    }

    public override Vector2 InitialSize => new(Mathf.Min(900f, UI.screenWidth - 40f), Mathf.Min(620f, UI.screenHeight - 40f));
    protected override float Margin => 0f;

    private static int Seed => Find.World?.info?.Seed.GetHashCode() ?? 0;

    private static Color Dim(Color c, float k, float a = 1f) => new(c.r * k, c.g * k, c.b * k, a);

    public override void PostOpen()
    {
        base.PostOpen();
        NetrunSound.Play("UI_Open");
    }

    private int HubCount(int layout, NetSize size)
    {
        if (!_hubCounts.TryGetValue((layout, size), out int n))
        {
            n = CyberMapGenerator.Generate(size, Seed, layout).Nodes.Count(x => x.District is not (District.Home or District.Dark or District.OldNet));
            _hubCounts[(layout, size)] = n;
        }
        return n;
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (_preview == null || _previewLayout != _layout || _previewSize != _size)
        {
            _preview = CyberMapGenerator.Generate(_size, Seed, _layout);
            _previewLayout = _layout;
            _previewSize = _size;
            _revealStart = Time.realtimeSinceStartup;
        }

        bool font = NetrunFont.Push();
        try
        {
            Color accent = NetrunUI.Accent;
            Widgets.DrawBoxSolid(inRect, Dim(NetrunUI.BgWindow, 0.8f));
            NetrunUI.Box(inRect, Color.clear, Dim(accent, 0.45f));
            NetrunUI.Brackets(inRect.ContractedBy(3f), accent, 2f, 16f);
            Rect r = inRect.ContractedBy(Pad);

            // Header: title with a blinking cursor, status line, close.
            Text.Font = GameFont.Medium;
            string title = "NR_NetFoundTitle".Translate();
            float tw = Text.CalcSize(title).x;
            Widgets.Label(new Rect(r.x, r.y, tw + 4f, 34f), title.Colorize(accent));
            if (Time.realtimeSinceStartup % 1f < 0.5f)
                Widgets.DrawBoxSolid(new Rect(r.x + tw + 4f, r.y + 24f, 12f, 3f), accent);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            string status = "NR_NetFoundStatus".Translate(((uint)Seed % 0xFFFFFF).ToString("X6"));
            Widgets.Label(new Rect(r.x, r.y, r.width - 34f, 30f), status.Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            if (NetrunUI.Button(new Rect(r.xMax - 26f, r.y + 2f, 26f, 26f), "×", NetrunUI.Muted, true, "CloseButton".Translate()))
                Close();
            float y = r.y + 36f;
            Widgets.DrawBoxSolid(new Rect(r.x, y, r.width, 1f), Dim(accent, 0.35f));
            y += 8f;

            Text.Font = GameFont.Small;
            string intro = "> " + "NR_NetFoundIntro".Translate();
            float ih = Text.CalcHeight(intro, r.width);
            Widgets.Label(new Rect(r.x, y, r.width, ih), intro.Colorize(NetrunUI.Subtle));
            y += ih + 10f;

            const float footer = 54f;
            float bodyH = r.yMax - footer - y;
            float leftW = Mathf.Floor(r.width * 0.6f);
            var left = new Rect(r.x, y, leftW, bodyH);
            var right = new Rect(left.xMax + 14f, y, r.width - leftW - 14f, bodyH);
            DrawPreviewPanel(left, accent);
            DrawPickers(right, accent);

            // Footer: note and the jack-in button.
            float fy = r.yMax - footer + 10f;
            Widgets.DrawBoxSolid(new Rect(r.x, fy - 4f, r.width, 1f), Dim(accent, 0.25f));
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(r.x, fy + 4f, r.width - 250f, 36f), "NR_NetFoundNote".Translate().Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (NetrunUI.Button(new Rect(r.xMax - 230f, fy + 4f, 230f, 38f), "NR_NetFoundStart".Translate(), accent, true))
            {
                NetMapManager.Get()?.Generate(_size, pickedLayout: _layout);
                NetrunSound.Play("Dive_Start");
                Close();
            }
        }
        finally
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            NetrunFont.Pop(font);
        }
    }

    private void DrawPreviewPanel(Rect r, Color accent)
    {
        const float legendH = 38f;
        var field = new Rect(r.x, r.y, r.width, r.height - legendH);
        Widgets.DrawBoxSolid(field, new Color(0.03f, 0.05f, 0.05f));
        NetrunUI.Box(field, Color.clear, Dim(accent, 0.3f));
        float reveal = (Time.realtimeSinceStartup - _revealStart) / RevealSeconds;
        DrawPreview(field.ContractedBy(14f), _preview, NetrunMod.Settings.Animations ? reveal : 99f);
        NetrunUI.Scanline(field, accent, 2.4f);

        Text.Font = GameFont.Tiny;
        string layoutName = ("NR_NetLayout_" + _preview.Layout).Translate();
        string tag = "NR_NetFoundTag".Translate(_layout < 0 ? layoutName + " (" + "NR_NetLayout_Random".Translate() + ")" : layoutName);
        Widgets.Label(new Rect(field.x + 8f, field.y + 5f, field.width - 16f, 20f), tag.Colorize(accent));

        // Legend: the districts this map has, then the hub count.
        float x = r.x;
        float ly = field.yMax + 6f;
        foreach (District d in _preview.Nodes.Select(n => n.District).Distinct().Where(d => d is not (District.Dark or District.Home)).OrderBy(d => (int)d))
        {
            string name = ("NR_District_" + d).Translate();
            float w = Text.CalcSize(name).x + 14f;
            if (x + w > r.xMax - 80f)
            {
                x = r.x;
                ly += 15f;
                if (ly > r.yMax - 12f)
                    break;
            }
            GUI.color = GatewayView.DistrictColor(d);
            GUI.DrawTexture(new Rect(x, ly + 4f, 7f, 7f), NetrunUI.Disc);
            GUI.color = NetrunUI.Muted;
            Widgets.Label(new Rect(x + 10f, ly, w, 16f), name);
            x += w + 6f;
        }
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(new Rect(r.xMax - 80f, field.yMax + 6f, 80f, 18f), "NR_NetFoundNodes".Translate(HubCount(_layout, _size)).Colorize(NetrunUI.TextColor));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }

    private void DrawPickers(Rect r, Color accent)
    {
        float y = r.y;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(r.x, y, r.width, 18f), "NR_NetFoundStepLayout".Translate().Colorize(accent));
        y += 20f;
        const float sizeBlock = 74f;
        float cardH = Mathf.Min(54f, (r.height - 20f - sizeBlock - 4f * 5f) / 5f);
        for (int i = -1; i <= (int)NetLayout.Metro; i++)
        {
            var card = new Rect(r.x, y, r.width, cardH);
            DrawLayoutCard(card, i, accent);
            y += cardH + 5f;
        }

        y = r.yMax - sizeBlock + 6f;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(r.x, y, r.width, 18f), "NR_NetFoundStepSize".Translate().Colorize(accent));
        y += 20f;
        float bw = (r.width - 12f) / 3f;
        for (int i = 0; i < 3; i++)
        {
            var size = (NetSize)i;
            var chip = new Rect(r.x + i * (bw + 6f), y, bw, 44f);
            bool on = size == _size;
            bool over = Mouse.IsOver(chip);
            Widgets.DrawBoxSolid(chip, on ? Dim(accent, 0.22f) : Dim(NetrunUI.BgWindow, 0.6f));
            NetrunUI.Box(chip, Color.clear, on ? accent : over ? Dim(accent, 0.6f) : new Color(1f, 1f, 1f, 0.1f));
            Text.Anchor = TextAnchor.UpperCenter;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(chip.x, chip.y + 4f, chip.width, 20f), ("NR_NetSize_" + size).Translate().Colorize(on ? accent : NetrunUI.TextColor));
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(chip.x, chip.y + 24f, chip.width, 18f), "NR_NetFoundNodes".Translate(HubCount(_layout, size)).Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(chip, ("NR_NetSizeTip_" + size).Translate());
            if (Widgets.ButtonInvisible(chip) && !on)
            {
                _size = size;
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
        }
        Text.Font = GameFont.Small;
    }

    private void DrawLayoutCard(Rect card, int layout, Color accent)
    {
        bool on = layout == _layout;
        bool over = Mouse.IsOver(card);
        Widgets.DrawBoxSolid(card, on ? Dim(accent, 0.22f) : Dim(NetrunUI.BgWindow, 0.6f));
        NetrunUI.Box(card, Color.clear, on ? accent : over ? Dim(accent, 0.6f) : new Color(1f, 1f, 1f, 0.1f));
        if (on)
            Widgets.DrawBoxSolid(new Rect(card.x, card.y, 3f, card.height), accent);

        // Thumbnail: the real layout's sample map, or a die for the seed's pick.
        var thumb = new Rect(card.x + 8f, card.y + 5f, (card.height - 10f) * 1.45f, card.height - 10f);
        Widgets.DrawBoxSolid(thumb, new Color(0.03f, 0.05f, 0.05f));
        if (layout >= 0)
            DrawPreview(thumb.ContractedBy(2f), CyberMapGenerator.Sample((NetLayout)layout), 99f, 0.45f);
        else
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(thumb, "?".Colorize(on ? accent : NetrunUI.Muted));
        }

        string key = layout < 0 ? "Random" : ((NetLayout)layout).ToString();
        float tx = thumb.xMax + 10f;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(tx, card.y + 4f, card.xMax - tx - 6f, 22f), ("NR_NetLayout_" + key).Translate().Colorize(on ? accent : NetrunUI.TextColor));
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(tx, card.y + 23f, card.xMax - tx - 6f, card.height - 24f), ("NR_NetLayoutDesc_" + key).Translate().Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        if (Widgets.ButtonInvisible(card) && !on)
        {
            _layout = layout;
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }
    }

    /// <summary>
    /// The map sketch. <paramref name="reveal"/> runs 0..1 after a change: hubs light up outward from Home, a link once
    /// both its ends are lit (99 = all shown).
    /// </summary>
    internal static void DrawPreview(Rect r, CyberMap map, float reveal = 99f, float dotScale = 1f)
    {
        float scale = Mathf.Min(r.width / 1.0f, r.height / 0.66f);
        Vector2 P(MapNode n) => new(r.x + n.X * scale + (r.width - scale) / 2f, r.y + n.Y * scale);
        MapNode home = map.Nodes.FirstOrDefault(n => n.District == District.Home);
        float Lit(MapNode n)
        {
            if (reveal >= 2f || home == null)
                return 1f;
            float d = Mathf.Clamp01(Vector2.Distance(new Vector2(n.X, n.Y), new Vector2(home.X, home.Y)) / 1.1f);
            return Mathf.Clamp01((reveal - d) * 5f);
        }
        foreach (MapLink l in map.Links.Where(l => l.Known))
        {
            MapNode a = map.Node(l.A), b = map.Node(l.B);
            float alpha = Mathf.Min(Lit(a), Lit(b));
            if (alpha <= 0f)
                continue;
            Color c = l.Kind == LinkKind.Fast ? new Color(0.24f, 0.82f, 0.9f, 0.8f) : l.Kind == LinkKind.Wall ? new Color(0.9f, 0.3f, 0.3f, 0.6f) : new Color(0.55f, 0.62f, 0.55f, 0.6f);
            c.a *= alpha;
            Widgets.DrawLine(P(a), P(b), c, l.Kind == LinkKind.Fast ? 2f * dotScale : 1f);
        }
        foreach (MapNode n in map.Nodes.Where(n => n.District != District.Dark))
        {
            float alpha = Lit(n);
            if (alpha <= 0f)
                continue;
            Vector2 p = P(n);
            float s = (n.District == District.Home ? 7f : 4.5f) * dotScale;
            Color c = GatewayView.DistrictColor(n.District);
            c.a = alpha;
            GUI.color = c;
            GUI.DrawTexture(new Rect(p.x - s, p.y - s, s * 2f, s * 2f), n.District == District.Home ? NetrunUI.Ring : NetrunUI.Disc);
        }
        GUI.color = Color.white;
    }
}
