using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Icons drawn in the terminal's accent (docs/TASKS_2026-10-08.md, D1). Maxim's template icons keep their coloured
/// parts and have white or grey parts; those parts take the terminal's colour scheme. The copy is made once per
/// texture and scheme on the CPU and cached (no work per frame), so a white × accent icon stays clean on every
/// scheme instead of going muddy as with GUI.color.
/// </summary>
public static class NetrunTint
{
    /// <summary>Texture names (files in Textures/UI/Netrun) whose white parts follow the accent.</summary>
    private static readonly HashSet<string> Tintable =
    [
        "Home_Archive", "Home_Board", "Home_Club", "Home_Gateway", "Home_Server", "Home_Studio",
        "Icon_Guide", "Icon_NetworkLock", "Menu_Main",
        "Disconnect", "Fixer_Corp", "Fixer_Shop", "Fixer_Street", "Handshake",
        "Gizmo_AI_Awaken", "Gizmo_AI_CoprocessorUpload", "Gizmo_AI_Embody", "Gizmo_AI_Erase", "Gizmo_AI_Feed",
        "Gizmo_AI_Isolate", "Gizmo_AI_ReturnServer", "Gizmo_AI_Use", "Gizmo_BD_Record", "Gizmo_BD_Watch",
        "Gizmo_CallFixer", "Gizmo_NetrunDive", "Gizmo_Watcher",
        "Header_NetworkAttacks", "Header_Nodes", "Icon_DiveReport",
        "Tab_AI", "Tab_Braindance", "Tab_Contracts", "Tab_Cyberpedia", "Tab_Events_AI", "Tab_Events_Blue",
        "Tab_Events_Cyberspace", "Tab_Fixers", "Tab_Log", "Tab_Overview", "Tab_Settings",
    ];

    // One copy per texture and colour. (A single-colour cache was cleared every frame while the guide asked for its own
    // colour, and every icon was rebuilt with a pixel read each frame: the 17 ms in guide mode.)
    // The key packs the colour into an int (Color32 has no IEquatable and boxes in a dictionary: PERF_AUDIT_4, 1).
    private static readonly Dictionary<(Texture2D, int), Texture2D> Cache = [];
    // Whether a texture is a template icon, by texture (Unity makes a new string for every Texture.name).
    private static readonly Dictionary<Texture2D, bool> IsTintable = [];

    private static bool CanTint(Texture2D src)
    {
        if (!IsTintable.TryGetValue(src, out bool yes))
            IsTintable[src] = yes = Tintable.Contains(BaseName(src));
        return yes;
    }

    /// <summary>
    /// The file name of a texture: RimWorld 1.6 names a loaded texture by its file, the 1.7 renderer may give the whole
    /// content path (or an extension), which made every icon miss the template list and stay white.
    /// </summary>
    private static string BaseName(Texture2D src)
    {
        string name = src.name ?? string.Empty;
        int slash = System.Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
        if (slash >= 0)
            name = name.Substring(slash + 1);
        return name.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 4) : name;
    }

    private static int Pack(Color c)
    {
        Color32 c32 = c;
        return c32.r | c32.g << 8 | c32.b << 16 | c32.a << 24;
    }

    /// <summary>The icon in the terminal's colours (itself when it is not a template icon).</summary>
    public static Texture2D Of(Texture2D src) => Of(src, NetrunUI.Accent);

    public static Texture2D Of(Texture2D src, Color accent)
        => src == null || !CanTint(src) ? src : Tinted(src, accent);

    /// <summary>Any icon's white and grey parts in the given colour (the Cyberpedia paints its icons in the article's colour).</summary>
    public static Texture2D OfAny(Texture2D src, Color color) => src == null ? null : Tinted(src, color);

    private static Texture2D Tinted(Texture2D src, Color accent)
    {
        var key = (src, Pack(accent));
        if (Cache.TryGetValue(key, out Texture2D tinted))
            return tinted;
        if (Cache.Count > 800)
        {
            foreach (KeyValuePair<(Texture2D, int), Texture2D> kv in Cache)
                if (kv.Value != null && kv.Value != kv.Key.Item1)
                    Object.Destroy(kv.Value);
            Cache.Clear();
        }
        if (Event.current == null || Event.current.type != EventType.Repaint)
            return src;
        // A few new copies per frame at most: a scheme change no longer stalls one frame for every icon at once.
        if (Time.frameCount != _budgetFrame)
        {
            _budgetFrame = Time.frameCount;
            _budgetLeft = MakesPerFrame;
        }
        if (_budgetLeft <= 0)
            return src;
        _budgetLeft--;
        try
        {
            tinted = Make(src, accent);
        }
        catch (System.Exception e)
        {
            Log.WarningOnce("[Netrun] could not tint " + src.name + ": " + e.Message, src.name.GetHashCode());
            tinted = src;
        }
        Cache[key] = tinted;
        return tinted;
    }

    /// <summary>Dev: the home node icons as the tint sees them (name, template or not, file found, copies cached).</summary>
    public static string DebugReport()
    {
        var lines = new List<string> { "Cached copies: " + Cache.Count };
        foreach (string node in new[] { "Board", "Club", "Archive", "Server", "Studio", "Gateway" })
        {
            Texture2D tex = ContentFinder<Texture2D>.Get("UI/Netrun/Nodes/Home_" + node, reportFailure: false);
            lines.Add(tex == null
                ? "Home_" + node + ": not loaded"
                : $"Home_{node}: name \"{tex.name}\" -> {BaseName(tex)}, template {CanTint(tex)}, file {FileOf(BaseName(tex)) != null}, {tex.width}x{tex.height}");
        }
        return string.Join("\n", lines);
    }

    private const int MakesPerFrame = 16;
    private static int _budgetFrame = -1, _budgetLeft;

    // Our PNG files by texture name, to read the pixels from disk (null until first used).
    private static Dictionary<string, string> _files;

    private static string FileOf(string name)
    {
        if (_files == null)
        {
            _files = [];
            try
            {
                string root = System.IO.Path.Combine(LoadedModManager.GetMod<NetrunMod>().Content.RootDir, "Textures");
                foreach (string f in System.IO.Directory.GetFiles(root, "*.png", System.IO.SearchOption.AllDirectories))
                {
                    string key = System.IO.Path.GetFileNameWithoutExtension(f);
                    if (!_files.ContainsKey(key))
                        _files[key] = f;
                }
            }
            catch (System.Exception e)
            {
                Log.WarningOnce("[Netrun] could not index the icon files: " + e.Message, 0x4E520077);
            }
        }
        return _files.TryGetValue(name, out string path) ? path : null;
    }

    // Decoded pixels per texture name: every colour of an icon reuses one decode (no disk read or PNG decode per colour).
    private static readonly Dictionary<string, (Color32[] px, int w, int h)> Decoded = [];

    /// <summary>
    /// The icon's pixels: decoded from our PNG on the CPU once (RimWorld 1.7 runs on a new renderer, where reading a texture
    /// back from the GPU stalls and the icons came late or not at all), or read back from the GPU when there is no file.
    /// </summary>
    private static (Color32[] px, int w, int h) Pixels(Texture2D src)
    {
        string name = BaseName(src);
        if (Decoded.TryGetValue(name, out var cached))
            return cached;
        Texture2D tex = null;
        string path = FileOf(name);
        if (path != null)
        {
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(System.IO.File.ReadAllBytes(path)))
            {
                Object.Destroy(tex);
                tex = null;
            }
        }
        tex ??= ReadBack(src);
        var result = (tex.GetPixels32(), tex.width, tex.height);
        Object.Destroy(tex);
        Decoded[name] = result;
        return result;
    }

    private static Texture2D Make(Texture2D src, Color accent)
    {
        (Color32[] px, int w, int h) = Pixels(src);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = src.name + "#tint" };
        tex.SetPixels32(Recolor((Color32[])px.Clone(), accent));
        tex.filterMode = src.filterMode;
        tex.Apply(true, true);
        return tex;
    }

    private static Texture2D ReadBack(Texture2D src)
    {
        // Mod textures are not readable: copy through a render texture.
        RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        RenderTexture prev = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0f, 0f, src.width, src.height), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }

    private static Color32[] Recolor(Color32[] px, Color accent)
    {
        for (int i = 0; i < px.Length; i++)
        {
            Color32 p = px[i];
            if (p.a == 0)
                continue;
            float r = p.r / 255f, g = p.g / 255f, b = p.b / 255f;
            float lum = (r + g + b) / 3f;
            float sat = Mathf.Max(r, Mathf.Max(g, b)) - Mathf.Min(r, Mathf.Min(g, b));
            // White and light grey take the accent; coloured parts stay as drawn.
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.85f, lum)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.2f, sat)));
            if (k <= 0f)
                continue;
            float l = Mathf.Max(lum, 0.75f);
            px[i] = new Color32(
                (byte)Mathf.RoundToInt(Mathf.Lerp(r, accent.r * l, k) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Lerp(g, accent.g * l, k) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Lerp(b, accent.b * l, k) * 255f),
                p.a);
        }
        return px;
    }
}
