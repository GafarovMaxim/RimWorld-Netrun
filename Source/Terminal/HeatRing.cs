using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// A smooth, anti-aliased Heat ring drawn into a texture (docs/CORE_V3.md): five level arcs with gaps at the
/// thresholds, lit up to the current Heat and dim after it, plus tick marks. Rebuilt only when the rounded Heat or
/// the size changes.
/// </summary>
public static class HeatRing
{
    private const int Size = 256;
    private const float Thickness = 18f;

    private static Texture2D _tex;
    private static int _builtFor = -1;

    public static Texture2D Get(float heat)
    {
        int key = Mathf.RoundToInt(heat * 2f);
        if (_tex != null && key == _builtFor)
            return _tex;
        _builtFor = key;
        _tex ??= new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Build(_tex, heat);
        return _tex;
    }

    private static void Build(Texture2D tex, float heat)
    {
        var pixels = new Color32[Size * Size];
        float c = (Size - 1) / 2f;
        float outer = c - 2f;
        float inner = outer - Thickness;
        float tickInner = inner - 7f;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                // Angle from the top, clockwise, 0..1 (texture rows go up, so flip y).
                float a = Mathf.Atan2(dx, dy) / (2f * Mathf.PI);
                if (a < 0f)
                    a += 1f;
                float value = a * HeatTracker.MaxHeat;
                Color col = Color.clear;

                float band = Mathf.Clamp01(Mathf.Min(r - inner, outer - r) + 0.5f);
                if (band > 0f)
                {
                    // Gaps at the level thresholds.
                    float gap = 1f;
                    foreach (HeatLevel l in System.Enum.GetValues(typeof(HeatLevel)))
                    {
                        if (l == HeatLevel.Shadow)
                            continue;
                        float d = Mathf.Abs(value - l.MinHeat()) * (2f * Mathf.PI * r) / HeatTracker.MaxHeat;
                        gap = Mathf.Min(gap, Mathf.Clamp01(d - 3f));
                    }
                    Color lc = HeatLevelUtility.LevelOf(value).Color();
                    bool lit = value <= heat;
                    // A soft inner shade gives the band some depth.
                    float shade = Mathf.Lerp(0.75f, 1f, (r - inner) / Thickness);
                    col = lit ? new Color(lc.r * shade, lc.g * shade, lc.b * shade, band * gap) : new Color(lc.r, lc.g, lc.b, 0.16f * band * gap);
                }
                else
                {
                    // Thin tick marks every 5 Heat, longer at the thresholds.
                    float step = value % 5f;
                    float dist = Mathf.Min(step, 5f - step) * (2f * Mathf.PI * r) / HeatTracker.MaxHeat;
                    bool major = Mathf.Abs(value - Mathf.Round(value / 25f) * 25f) < 0.6f || Mathf.Abs(value - 95f) < 0.6f;
                    float from = major ? tickInner - 5f : tickInner;
                    if (r > from && r < inner - 2f)
                    {
                        float t = Mathf.Clamp01(1.2f - dist);
                        col = new Color(1f, 1f, 1f, (major ? 0.45f : 0.18f) * t);
                    }
                }
                pixels[y * Size + x] = col;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false);
    }

    /// <summary>Where the ring ends at this Heat, for the marker (GUI coordinates, y down).</summary>
    public static Vector2 PointAt(Rect rect, float heat)
    {
        float a = heat / HeatTracker.MaxHeat * 2f * Mathf.PI;
        float r = rect.width / 2f * ((Size / 2f - 2f - Thickness / 2f) / (Size / 2f));
        return rect.center + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * r;
    }
}
