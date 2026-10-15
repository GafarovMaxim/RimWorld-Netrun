using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// "Net found" (docs/CYBERSPACE_V2.md, section 10): at the first entry the player picks the map size, sees a sketch of
/// this world's layout, and starts. The choice is kept in the game; the mod setting stays the default for new games.
/// </summary>
public class Dialog_NetFound : Window
{
    private NetSize _size;
    private CyberMap _preview;
    private NetSize _previewSize = (NetSize)(-1);

    public Dialog_NetFound()
    {
        _size = NetrunMod.Settings.MapSize;
        forcePause = true;
        doCloseX = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
    }

    public override Vector2 InitialSize => new(760f, 560f);

    private static int Seed => Find.World?.info?.Seed.GetHashCode() ?? 0;

    public override void DoWindowContents(Rect inRect)
    {
        if (_preview == null || _previewSize != _size)
        {
            _preview = CyberMapGenerator.Generate(_size, Seed);
            _previewSize = _size;
        }
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "NR_NetFoundTitle".Translate().Colorize(NetrunUI.Accent));
        Text.Font = GameFont.Small;
        string intro = "NR_NetFoundIntro".Translate(("NR_NetLayout_" + _preview.Layout).Translate());
        float ih = Text.CalcHeight(intro, inRect.width);
        Widgets.Label(new Rect(inRect.x, inRect.y + 38f, inRect.width, ih), intro);
        float y = inRect.y + 42f + ih;

        var field = new Rect(inRect.x, y, inRect.width, inRect.height - (y - inRect.y) - 100f);
        Widgets.DrawBoxSolid(field, NetrunUI.BgWindow);
        DrawPreview(field.ContractedBy(10f), _preview);
        y = field.yMax + 8f;

        float bw = (inRect.width - 12f) / 3f;
        for (int i = 0; i < 3; i++)
        {
            var size = (NetSize)i;
            int nodes = CyberMapGenerator.Generate(size, Seed).Nodes.Count(n => n.District is not (District.Home or District.Dark or District.OldNet));
            string label = ("NR_NetSize_" + size).Translate() + " (" + "NR_NetFoundNodes".Translate(nodes) + ")";
            if (NetrunUI.Button(new Rect(inRect.x + i * (bw + 6f), y, bw, 32f), (size == _size ? "✓ " : "") + label,
                    size == _size ? NetrunUI.Ok : NetrunUI.TextColor, true, ("NR_NetSizeTip_" + size).Translate()))
                _size = size;
        }
        y += 40f;
        if (NetrunUI.Button(new Rect(inRect.xMax - 220f, y, 220f, 40f), "NR_NetFoundStart".Translate(), NetrunUI.Accent, true))
        {
            NetMapManager.Get()?.Generate(_size);
            Close();
        }
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(inRect.x, y + 4f, inRect.width - 240f, 36f), "NR_NetFoundNote".Translate().Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
    }

    internal static void DrawPreview(Rect r, CyberMap map)
    {
        float scale = Mathf.Min(r.width / 1.0f, r.height / 0.66f);
        Vector2 P(MapNode n) => new(r.x + n.X * scale + (r.width - scale) / 2f, r.y + n.Y * scale);
        foreach (MapLink l in map.Links.Where(l => l.Known))
        {
            Color c = l.Kind == LinkKind.Fast ? new Color(0.24f, 0.82f, 0.9f, 0.8f) : l.Kind == LinkKind.Wall ? new Color(0.9f, 0.3f, 0.3f, 0.6f) : new Color(0.55f, 0.62f, 0.55f, 0.6f);
            Widgets.DrawLine(P(map.Node(l.A)), P(map.Node(l.B)), c, l.Kind == LinkKind.Fast ? 2f : 1f);
        }
        foreach (MapNode n in map.Nodes.Where(n => n.District != District.Dark))
        {
            Vector2 p = P(n);
            float s = n.District == District.Home ? 7f : 4.5f;
            GUI.color = GatewayView.DistrictColor(n.District);
            GUI.DrawTexture(new Rect(p.x - s, p.y - s, s * 2f, s * 2f), n.District == District.Home ? NetrunUI.Ring : NetrunUI.Disc);
        }
        GUI.color = Color.white;
    }
}
