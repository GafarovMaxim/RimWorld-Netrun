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
    // 128 px is enough for a ring drawn at ~150 px; the radii are in the old 256 px units times K (PERF_AUDIT_4, 23).
    private const int Size = 128;
    private const float K = Size / 256f;
    private const float Thickness = 18f * K;

    private static Texture2D _tex;
    private static int _builtFor = -1;
    // Reused between rebuilds: the pixels, and per pixel the radius and the Heat value of its angle (fixed).
    private static Color32[] _pixels;
    private static float[] _radius, _value;
    private static float[] _thresholds;

    public static Texture2D Get(float heat)
    {
        int key = Mathf.RoundToInt(heat);
        if (_tex != null && key == _builtFor)
            return _tex;
        _builtFor = key;
        _tex ??= new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Build(_tex, heat);
        return _tex;
    }

    private static void Prepare()
    {
        if (_pixels != null)
            return;
        _pixels = new Color32[Size * Size];
        _radius = new float[Size * Size];
        _value = new float[Size * Size];
        float c = (Size - 1) / 2f;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x - c, dy = y - c;
                // Angle from the top, clockwise, 0..1 (texture rows go up, so flip y).
                float a = Mathf.Atan2(dx, dy) / (2f * Mathf.PI);
                if (a < 0f)
                    a += 1f;
                _radius[y * Size + x] = Mathf.Sqrt(dx * dx + dy * dy);
                _value[y * Size + x] = a * HeatTracker.MaxHeat;
            }
        var list = new System.Collections.Generic.List<float>();
        foreach (HeatLevel l in System.Enum.GetValues(typeof(HeatLevel)))
            if (l != HeatLevel.Shadow)
                list.Add(l.MinHeat());
        _thresholds = list.ToArray();
    }

    private static void Build(Texture2D tex, float heat)
    {
        Prepare();
        Color32[] pixels = _pixels;
        float c = (Size - 1) / 2f;
        float outer = c - 2f * K;
        float inner = outer - Thickness;
        float tickInner = inner - 7f * K;
        for (int i = 0; i < pixels.Length; i++)
        {
            float r = _radius[i];
            float value = _value[i];
            Color col = Color.clear;
            float band = Mathf.Clamp01(Mathf.Min(r - inner, outer - r) + 0.5f);
            if (band > 0f)
            {
                // Gaps at the level thresholds.
                float gap = 1f;
                for (int t = 0; t < _thresholds.Length; t++)
                {
                    float d = Mathf.Abs(value - _thresholds[t]) * (2f * Mathf.PI * r) / HeatTracker.MaxHeat;
                    gap = Mathf.Min(gap, Mathf.Clamp01(d - 3f * K));
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
                float from = major ? tickInner - 5f * K : tickInner;
                if (r > from && r < inner - 2f * K)
                {
                    float t = Mathf.Clamp01(1.2f - dist / K);
                    col = new Color(1f, 1f, 1f, (major ? 0.45f : 0.18f) * t);
                }
            }
            pixels[i] = col;
        }
        tex.SetPixels32(pixels);
        tex.Apply(false);
    }

    /// <summary>Where the ring ends at this Heat, for the marker (GUI coordinates, y down).</summary>
    public static Vector2 PointAt(Rect rect, float heat)
    {
        float a = heat / HeatTracker.MaxHeat * 2f * Mathf.PI;
        float r = rect.width / 2f * ((Size / 2f - 2f * K - Thickness / 2f) / (Size / 2f));
        return rect.center + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * r;
    }
}
