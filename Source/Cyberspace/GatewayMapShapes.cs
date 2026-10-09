using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Map v5 (Maxim 2026-10-09, mockups 34a/34b): node types as geometric shapes. Square: data market; ring: archive;
/// hexagon: corporate; triangle: abandoned; diamond: dark exchange (and, as an outline, an unknown node); a big
/// hexagon with a core: the corporate core. Textures are made once, white, tinted with GUI.color.
/// </summary>
public static partial class GatewayView
{
    internal enum Shape
    {
        Square,
        Circle,
        Hexagon,
        Triangle,
        Diamond,
    }

    private static readonly Vector2[] SquarePoly = [new(-0.78f, -0.78f), new(0.78f, -0.78f), new(0.78f, 0.78f), new(-0.78f, 0.78f)];
    private static readonly Vector2[] DiamondPoly = [new(0f, -1f), new(1f, 0f), new(0f, 1f), new(-1f, 0f)];
    private static readonly Vector2[] TrianglePoly = [new(0f, -0.95f), new(0.95f, 0.75f), new(-0.95f, 0.75f)];
    private static readonly Vector2[] HexagonPoly = MakeHexagon();

    private static Vector2[] MakeHexagon()
    {
        var pts = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.PI / 3f * i + Mathf.PI / 6f;
            pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.95f;
        }
        return pts;
    }

    private static readonly Texture2D[] ShapeFill = new Texture2D[5];
    private static readonly Texture2D[] ShapeLine = new Texture2D[5];

    /// <summary>A shape's texture, filled or as an outline (made on first use).</summary>
    internal static Texture2D ShapeTex(Shape shape, bool filled)
    {
        int i = (int)shape;
        Texture2D[] set = filled ? ShapeFill : ShapeLine;
        if (set[i] != null)
            return set[i];
        if (shape == Shape.Circle)
            return set[i] = filled ? NetrunUI.Disc : HubRing;
        Vector2[] poly = shape switch
        {
            Shape.Square => SquarePoly,
            Shape.Diamond => DiamondPoly,
            Shape.Triangle => TrianglePoly,
            _ => HexagonPoly,
        };
        return set[i] = MakePoly(poly, filled);
    }

    private static Texture2D MakePoly(Vector2[] poly, bool filled)
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Texture rows go up; the polygons are drawn with y down, so flip.
                var p = new Vector2((x + 0.5f - half) / half, -(y + 0.5f - half) / half);
                float d = EdgeDistance(p, poly) * half; // in pixels
                bool inside = Inside(p, poly);
                // Filled: the inside, anti-aliased at the edge. Outline: a band about 4 px wide inside the edge.
                float a = !inside ? Mathf.Clamp01(0.5f - d) : filled ? Mathf.Clamp01(d + 0.5f) : Mathf.Clamp01(4.5f - d);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private static float EdgeDistance(Vector2 p, Vector2[] poly)
    {
        float best = float.MaxValue;
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }

    private static bool Inside(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        return inside;
    }

    /// <summary>The shape that stands for a node's type on the map.</summary>
    internal static Shape ShapeOf(ExternalNode b) => b.Type switch
    {
        ExternalNodeType.Market => Shape.Square,
        ExternalNodeType.Archive => Shape.Circle,
        ExternalNodeType.Corp => Shape.Hexagon,
        ExternalNodeType.Abandoned => Shape.Triangle,
        ExternalNodeType.DarkExchange => Shape.Diamond,
        _ => Shape.Hexagon,
    };

    /// <summary>A node marker: a dark rim, the shape in its colour; unknown ones a grey outlined diamond.</summary>
    internal static void DrawShape(Rect r, ExternalNode b, float alpha)
    {
        if (b.Hidden)
        {
            GUI.color = new Color(NetrunUI.Muted.r, NetrunUI.Muted.g, NetrunUI.Muted.b, 0.7f * alpha);
            GUI.DrawTexture(r, ShapeTex(Shape.Diamond, false));
            GUI.color = Color.white;
            return;
        }
        Shape s = ShapeOf(b);
        Color c = b.Offline ? NetrunUI.Muted : b.Color;
        GUI.color = new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.9f * alpha);
        GUI.DrawTexture(r.ExpandedBy(1.5f), ShapeTex(s, true));
        GUI.color = new Color(c.r, c.g, c.b, (b.Offline ? 0.45f : 1f) * alpha);
        GUI.DrawTexture(r, ShapeTex(s, !b.Offline));
        if (b.IsBoss || b.Ancient)
        {
            // A core: a dark centre and a bright dot.
            GUI.color = new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, alpha);
            GUI.DrawTexture(r.ContractedBy(r.width * 0.28f), NetrunUI.Disc);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(r.ContractedBy(r.width * 0.4f), NetrunUI.Disc);
        }
        GUI.color = Color.white;
    }
}
