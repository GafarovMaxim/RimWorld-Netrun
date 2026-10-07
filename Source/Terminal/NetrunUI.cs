using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Netrun;

/// <summary>
/// The terminal's visual language from docs/TERMINAL_UI.md: color tokens and small components
/// (chips, bordered buttons, cards, bars) shared by every node view.
/// </summary>
[StaticConstructorOnStartup]
public static class NetrunUI
{
    public static readonly Color BgWindow = Hex(0x1c1f1b);
    public static readonly Color BgCard = Hex(0x262a24);
    public static readonly Color BgCardAlt = Hex(0x2f342b);
    public static readonly Color Ok = Hex(0x4fd36a);
    public static readonly Color Warn = Hex(0xf0c040);
    public static readonly Color Bad = Hex(0xe5533d);
    public static readonly Color Purple = Hex(0xa070e0);
    public static readonly Color Muted = Hex(0x8b9183);
    public static readonly Color TextColor = Hex(0xe6e8e0);
    public static readonly Color Line = new(0.25f, 0.55f, 0.58f);

    private static readonly Color BarBack = new(0.05f, 0.06f, 0.05f);

    public static Color Hex(int rgb) => new(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);

    public static Color Accent => NetrunTex.Accent;

    private static readonly System.Collections.Generic.Dictionary<string, (float time, object value)> Cache = [];

    /// <summary>
    /// A value for the UI recomputed at most every few real seconds. OnGUI runs several times a frame, so counts
    /// and lists shown in the terminal come from here instead of being rebuilt on every call.
    /// </summary>
    public static T Cached<T>(string key, System.Func<T> compute, float seconds = 0.5f)
    {
        float now = Time.realtimeSinceStartup;
        if (Cache.TryGetValue(key, out var entry) && now - entry.time < seconds && entry.value is T value)
            return value;
        T fresh = compute();
        // Keys can carry state (filters, counts): drop the lot now and then so old ones do not pile up.
        if (Cache.Count > 400)
            Cache.Clear();
        Cache[key] = (now, fresh);
        return fresh;
    }

    /// <summary>White circle textures for round widgets (Heat ring, Gateway nodes); tint with GUI.color.</summary>
    public static readonly Texture2D Disc = MakeCircle(filled: true);
    public static readonly Texture2D Ring = MakeCircle(filled: false);

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
                float alpha = filled ? Mathf.Clamp01(r - d + 0.5f) : Mathf.Clamp01(1f - Mathf.Abs(d - (r - 3f)) / 3f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    /// <summary>A thin circle line (about 1.5 px at 256 px) for outer rings drawn large.</summary>
    public static readonly Texture2D ThinRing = MakeThinRing();

    /// <summary>A soft square glow: opaque in the middle, fading to the edges. Stretch over a box, tint with GUI.color.</summary>
    public static readonly Texture2D GlowTex = MakeGlow();

    private static Texture2D MakeThinRing()
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { filterMode = FilterMode.Bilinear };
        float r = size / 2f - 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1.2f - Mathf.Abs(d - r))));
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D MakeGlow()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance outside the inner square (40% of the texture), fading over the rest.
                float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - size / 2f) - size * 0.2f);
                float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - size / 2f) - size * 0.2f);
                float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / (size * 0.3f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, (1f - t) * (1f - t)));
            }
        }
        tex.Apply();
        return tex;
    }

    /// <summary>An icon size scaled by the "icon size" setting.</summary>
    public static float Icon(float size) => Mathf.Round(size * Mathf.Clamp(NetrunMod.Settings?.IconScale ?? 1f, 0.8f, 1.4f));

    /// <summary>Seconds since the game started, for animations; frozen at 0 when animations are off.</summary>
    public static float AnimTime => NetrunMod.Settings.Animations ? Time.realtimeSinceStartup : 0f;

    /// <summary>A soft glow around <paramref name="rect"/> in the color.</summary>
    public static void Glow(Rect rect, Color color, float strength = 1f, float spread = 16f)
    {
        GUI.color = new Color(color.r, color.g, color.b, 0.55f * strength);
        GUI.DrawTexture(rect.ExpandedBy(spread), GlowTex);
        GUI.color = Color.white;
    }

    /// <summary>
    /// Corner brackets, deliberately uneven: the top-left and bottom-right arms run long along one side and short along the
    /// other, the other two corners are short, and a small notch sits on the top and the left edge off centre.
    /// </summary>
    public static void Brackets(Rect r, Color color, float thickness = 3f, float arm = 18f)
    {
        float t = thickness;
        Widgets.DrawBoxSolid(new Rect(r.x, r.y, arm * 1.5f, t), color);
        Widgets.DrawBoxSolid(new Rect(r.x, r.y, t, arm * 0.8f), color);
        Widgets.DrawBoxSolid(new Rect(r.xMax - arm, r.y, arm, t), color);
        Widgets.DrawBoxSolid(new Rect(r.xMax - t, r.y, t, arm), color);
        Widgets.DrawBoxSolid(new Rect(r.x, r.yMax - t, arm, t), color);
        Widgets.DrawBoxSolid(new Rect(r.x, r.yMax - arm, t, arm), color);
        Widgets.DrawBoxSolid(new Rect(r.xMax - arm * 0.8f, r.yMax - t, arm * 0.8f, t), color);
        Widgets.DrawBoxSolid(new Rect(r.xMax - t, r.yMax - arm * 1.5f, t, arm * 1.5f), color);
        // Off-centre notches.
        Widgets.DrawBoxSolid(new Rect(r.x + r.width * 0.62f, r.y - 3f, 2f, 5f), color);
        Widgets.DrawBoxSolid(new Rect(r.x - 3f, r.y + r.height * 0.34f, 5f, 2f), color);
    }

    /// <summary>A dashed outline (the "current" mark); the dashes slowly run around clockwise.</summary>
    public static void Dashed(Rect r, Color color, float dash = 5f, float gap = 4f)
    {
        float period = dash + gap;
        float shift = AnimTime * 10f % period;
        GUI.color = color;
        // Top goes right, right goes down, bottom goes left, left goes up.
        for (float s = shift - period; s < r.width; s += period)
        {
            float a = Mathf.Max(0f, s), b = Mathf.Min(r.width, s + dash);
            if (b > a)
            {
                GUI.DrawTexture(new Rect(r.x + a, r.y, b - a, 1f), BaseContent.WhiteTex);
                GUI.DrawTexture(new Rect(r.xMax - b, r.yMax - 1f, b - a, 1f), BaseContent.WhiteTex);
            }
        }
        for (float s = shift - period; s < r.height; s += period)
        {
            float a = Mathf.Max(0f, s), b = Mathf.Min(r.height, s + dash);
            if (b > a)
            {
                GUI.DrawTexture(new Rect(r.xMax - 1f, r.y + a, 1f, b - a), BaseContent.WhiteTex);
                GUI.DrawTexture(new Rect(r.x, r.yMax - b, 1f, b - a), BaseContent.WhiteTex);
            }
        }
        GUI.color = Color.white;
    }

    // ---- Hover and change tracking for small animations (keyed by a rect or a name) ----

    private static readonly System.Collections.Generic.Dictionary<long, (float since, int frame)> Hovers = [];
    private static readonly System.Collections.Generic.Dictionary<string, (float value, float changed, float delta)> Values = [];

    private static long Key(Rect r) => ((long)Mathf.RoundToInt(r.x) << 32) ^ ((long)Mathf.RoundToInt(r.y) << 12) ^ Mathf.RoundToInt(r.width);

    /// <summary>Real seconds the mouse has been over <paramref name="r"/>, or -1 when it is not.</summary>
    public static float HoverTime(Rect r)
    {
        if (!Mouse.IsOver(r))
            return -1f;
        long key = Key(r);
        int frame = Time.frameCount;
        float now = Time.realtimeSinceStartup;
        if (!Hovers.TryGetValue(key, out var h) || frame - h.frame > 2)
        {
            if (Hovers.Count > 300)
                Hovers.Clear();
            h = (now, frame);
        }
        Hovers[key] = (h.since, frame);
        return now - h.since;
    }

    private static readonly System.Collections.Generic.Dictionary<string, (float since, int frame)> Screens = [];

    /// <summary>When the screen <paramref name="key"/> came on (again) after not being drawn: lists cascade in from there.</summary>
    public static float CascadeStart(string key)
    {
        int frame = Time.frameCount;
        if (!Screens.TryGetValue(key, out var s) || frame - s.frame > 2)
            s = (Time.realtimeSinceStartup, frame);
        Screens[key] = (s.since, frame);
        return s.since;
    }

    private static readonly System.Collections.Generic.Dictionary<long, float> Sweeps = [];

    /// <summary>
    /// Seconds since a sweep started on <paramref name="r"/>: it starts when the mouse comes in and runs its full
    /// <paramref name="duration"/> even after the mouse leaves; -1 when no sweep is running.
    /// </summary>
    public static float SweepTime(Rect r, float duration)
    {
        long key = Key(r);
        float now = Time.realtimeSinceStartup;
        bool known = Sweeps.TryGetValue(key, out float start);
        bool running = known && now - start < duration;
        // A new sweep only when the mouse comes in again and the last one has had a moment to rest.
        if (!running && HoverTime(r) >= 0f && (!known || now - start > duration + 0.6f))
        {
            if (Sweeps.Count > 300)
                Sweeps.Clear();
            Sweeps[key] = start = now;
            running = true;
        }
        return running ? now - start : -1f;
    }

    /// <summary>0..1, how lit a hover effect is: rises over <paramref name="fade"/> seconds after the mouse comes in.</summary>
    public static float HoverFade(Rect r, float fade = 0.15f)
    {
        float t = HoverTime(r);
        if (t < 0f)
            return 0f;
        return NetrunMod.Settings.Animations ? Mathf.Clamp01(t / fade) : 1f;
    }

    /// <summary>
    /// A shown number that rolls to its new value over about <paramref name="seconds"/>; also returns the last jump and
    /// how long ago it happened (for a "+8" that floats away).
    /// </summary>
    public static float Rolling(string key, float target, out float lastDelta, out float sinceChange, float seconds = 0.6f, float minJump = 1.5f)
    {
        float now = Time.realtimeSinceStartup;
        if (!Values.TryGetValue(key, out var v))
            v = (target, -10f, 0f);
        bool rolling = now - v.changed < seconds;
        float diff = target - v.value;
        if (!rolling)
        {
            // Small drifts (passive cooling) follow at once; only a real jump rolls and shows its delta.
            if (Mathf.Abs(diff) >= minJump && NetrunMod.Settings.Animations)
                v = (target, now, diff);
            else
                v = (target, v.changed, v.delta);
        }
        else
        {
            v = (target, v.changed, v.delta + diff);
        }
        Values[key] = v;
        lastDelta = v.delta;
        sinceChange = now - v.changed;
        if (sinceChange >= seconds)
            return target;
        float t = sinceChange / seconds;
        t = 1f - (1f - t) * (1f - t) * (1f - t);
        return Mathf.Lerp(target - v.delta, target, t);
    }

    /// <summary>1 normally; briefly up to 1.15 right after the counter under <paramref name="key"/> grows.</summary>
    public static float CountPulse(string key, int count)
    {
        float now = Time.realtimeSinceStartup;
        if (!Values.TryGetValue("pulse:" + key, out var v))
            v = (count, -10f, 0f);
        if (count > v.value)
            v = (count, now, count - v.value);
        else if (count < v.value)
            v = (count, v.changed, 0f);
        Values["pulse:" + key] = v;
        float t = now - v.changed;
        return NetrunMod.Settings.Animations && t < 0.35f ? 1f + 0.15f * Mathf.Sin(t / 0.35f * Mathf.PI) : 1f;
    }

    /// <summary>A bright line sweeping down the rect with a faint trail (hover effect).</summary>
    public static void Scanline(Rect r, Color color, float period = 1.6f)
    {
        if (!NetrunMod.Settings.Animations)
            return;
        float f = Time.realtimeSinceStartup / period % 1f;
        float y = r.y + r.height * f;
        GUI.color = new Color(color.r, color.g, color.b, 0.10f);
        GUI.DrawTexture(new Rect(r.x, Mathf.Max(r.y, y - 10f), r.width, Mathf.Min(10f, y - r.y)), BaseContent.WhiteTex);
        GUI.color = new Color(color.r, color.g, color.b, 0.55f);
        GUI.DrawTexture(new Rect(r.x, y, r.width, 1f), BaseContent.WhiteTex);
        GUI.color = Color.white;
    }

    /// <summary>One sweep of the scanline, <paramref name="t"/> seconds after the mouse came in.</summary>
    public static void ScanlineOnce(Rect r, Color color, float t, float duration = 0.45f)
    {
        if (!NetrunMod.Settings.Animations || t < 0f || t > duration)
            return;
        float k = t / duration;
        float eased = k * k * (3f - 2f * k);
        float env = Mathf.Sin(k * Mathf.PI);
        float y = r.y + r.height * eased;
        GUI.color = new Color(color.r, color.g, color.b, 0.12f * env);
        GUI.DrawTexture(new Rect(r.x, Mathf.Max(r.y, y - 16f), r.width, Mathf.Min(16f, y - r.y)), BaseContent.WhiteTex);
        GUI.color = new Color(color.r, color.g, color.b, 0.7f * env);
        GUI.DrawTexture(new Rect(r.x, y, r.width, 1f), BaseContent.WhiteTex);
        GUI.color = Color.white;
    }

    /// <summary>A news line running right to left inside <paramref name="rect"/>, items split by " /// ", clipped at the edges.</summary>
    public static void Ticker(Rect rect, System.Collections.Generic.List<(string text, Color color)> items)
    {
        if (items.Count == 0)
            return;
        string sep = "   ///   ";
        float sepW = Text.CalcSize(sep).x;
        float total = 0f;
        foreach ((string text, Color _) in items)
            total += Text.CalcSize(text).x + sepW;
        float speed = 38f;
        float offset = NetrunMod.Settings.Animations ? Time.realtimeSinceStartup * speed % total : 0f;
        Widgets.BeginGroup(rect);
        Text.Anchor = TextAnchor.MiddleLeft;
        // Two passes so the line wraps around without a gap.
        for (int pass = 0; pass < 2; pass++)
        {
            float x = -offset + pass * total;
            foreach ((string text, Color color) in items)
            {
                float w = Text.CalcSize(text).x;
                if (x + w > 0f && x < rect.width)
                    Widgets.Label(new Rect(Mathf.Round(x), 0f, w + 4f, rect.height), text.Colorize(color));
                x += w;
                if (x + sepW > 0f && x < rect.width)
                    Widgets.Label(new Rect(Mathf.Round(x), 0f, sepW + 4f, rect.height), sep.Colorize(new Color(1f, 1f, 1f, 0.25f)));
                x += sepW;
            }
        }
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.EndGroup();
        // Soft fade at both ends.
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + 1f, 6f, rect.height - 2f), new Color(BgWindow.r, BgWindow.g, BgWindow.b, 0.8f));
        Widgets.DrawBoxSolid(new Rect(rect.xMax - 6f, rect.y + 1f, 6f, rect.height - 2f), new Color(BgWindow.r, BgWindow.g, BgWindow.b, 0.8f));
    }

    private static readonly Color BezelColor = Hex(0x15171a);
    private static readonly Color BezelLight = Hex(0x2c3036);
    private static readonly Texture2D ScanlineTex = MakeScanlines();

    private static Texture2D MakeScanlines()
    {
        var tex = new Texture2D(1, 3, TextureFormat.ARGB32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.16f));
        tex.SetPixel(0, 1, new Color(0f, 0f, 0f, 0f));
        tex.SetPixel(0, 2, new Color(0f, 0f, 0f, 0f));
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// The casing of a retro cyberdeck around the screen: a dark bezel with a lit inner bevel, corner screws, a maker's
    /// plate and a power light. Returns the screen inside it, where the interface draws.
    /// </summary>
    public static Rect Bezel(Rect outer, float thickness, string plate = null, Color? accent = null)
    {
        Color c = accent ?? Accent;
        Widgets.DrawBoxSolid(outer, BezelColor);
        // Outer edge and inner bevel: a light line on top/left, a dark one at the screen.
        GUI.color = BezelLight;
        Widgets.DrawBox(outer);
        GUI.color = Color.white;
        Rect screen = outer.ContractedBy(thickness);
        Widgets.DrawBoxSolid(new Rect(screen.x - 2f, screen.y - 2f, screen.width + 4f, 2f), new Color(0f, 0f, 0f, 0.6f));
        Widgets.DrawBoxSolid(new Rect(screen.x - 2f, screen.y - 2f, 2f, screen.height + 4f), new Color(0f, 0f, 0f, 0.6f));
        Widgets.DrawBoxSolid(new Rect(screen.x - 2f, screen.yMax, screen.width + 4f, 2f), BezelLight);
        Widgets.DrawBoxSolid(new Rect(screen.xMax, screen.y - 2f, 2f, screen.height + 4f), BezelLight);
        // Corner screws.
        float s = Mathf.Min(6f, thickness * 0.5f);
        foreach (Vector2 p in new[] { new Vector2(outer.x + thickness / 2f, outer.y + thickness / 2f), new Vector2(outer.xMax - thickness / 2f, outer.y + thickness / 2f),
                     new Vector2(outer.x + thickness / 2f, outer.yMax - thickness / 2f), new Vector2(outer.xMax - thickness / 2f, outer.yMax - thickness / 2f) })
        {
            GUI.color = BezelLight;
            GUI.DrawTexture(new Rect(p.x - s / 2f, p.y - s / 2f, s, s), Disc);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(p.x - s / 2f, p.y - 0.5f, s, 1f), BaseContent.WhiteTex);
        }
        GUI.color = Color.white;
        // Accent ticks on the casing, uneven like the node frames.
        Widgets.DrawBoxSolid(new Rect(outer.x + 40f, outer.y + 1f, 60f, 2f), new Color(c.r, c.g, c.b, 0.55f));
        Widgets.DrawBoxSolid(new Rect(outer.xMax - 160f, outer.yMax - 3f, 120f, 2f), new Color(c.r, c.g, c.b, 0.55f));
        if (plate != null && thickness >= 10f)
        {
            Text.Font = thickness >= 16f ? GameFont.Small : GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            float pw = Text.CalcSize(plate).x + 16f;
            var pr = new Rect(outer.x + thickness + 8f, outer.yMax - thickness, pw, thickness);
            // An engraved plate: a darker inlay with a lit lower edge.
            Widgets.DrawBoxSolid(pr.ContractedBy(0f, 3f), new Color(0f, 0f, 0f, 0.35f));
            Widgets.DrawBoxSolid(new Rect(pr.x, pr.yMax - 3f, pr.width, 1f), BezelLight);
            Widgets.Label(new Rect(pr.x + 8f, pr.y, pr.width, pr.height), plate.Colorize(new Color(1f, 1f, 1f, 0.62f)));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (thickness >= 16f)
            {
                // Vent slots on the right and a row of port marks on top.
                for (int i = 0; i < 6; i++)
                    Widgets.DrawBoxSolid(new Rect(outer.xMax - thickness - 120f + i * 9f, outer.yMax - thickness + 5f, 5f, thickness - 10f), new Color(0f, 0f, 0f, 0.55f));
                for (int i = 0; i < 4; i++)
                    Widgets.DrawBoxSolid(new Rect(outer.x + 130f + i * 16f, outer.y + thickness / 2f - 2f, 10f, 4f), new Color(c.r, c.g, c.b, 0.18f + 0.1f * (i % 2)));
            }
            // The power light breathes.
            float a = NetrunMod.Settings.Animations ? 0.6f + 0.4f * Mathf.Sin(Time.realtimeSinceStartup * 1.5f) : 1f;
            Led(new Rect(outer.xMax - thickness - 26f, outer.yMax - thickness / 2f - 1.5f, 14f, 3f), new Color(Ok.r, Ok.g, Ok.b, a));
        }
        return screen;
    }

    private static readonly Color DeckBody = Hex(0x1b1e22);
    private static readonly Color DeckRubber = Hex(0x0c0d0f);
    private static readonly Color DeckOrange = Hex(0xf08a24);
    public const float DeckSide = 22f, DeckTop = 32f, DeckBottom = 38f;

    /// <summary>
    /// The terminal's rugged cyberdeck casing (after Maxim's references): rubber corner bumpers on bolts, side rails
    /// with grip ridges, two live gauges on top (Heat and the net), a tuning scale and an antenna port, and along the
    /// bottom an engraved plate, three hardware keys, a vent grille, an orange key and the power light. The keys call
    /// <paramref name="onKey"/> (0 back, 1 home, 2 help). Returns the screen inside.
    /// </summary>
    public static Rect DeckBezel(Rect outer, string plate, float heat01, bool online, System.Action<int> onKey = null)
    {
        bool anim = NetrunMod.Settings.Animations;
        float time = Time.realtimeSinceStartup;
        // Body: a darker shell with a lighter upper band, the outer edge lit.
        Widgets.DrawBoxSolid(outer, DeckBody);
        Widgets.DrawBoxSolid(new Rect(outer.x, outer.y, outer.width, DeckTop * 0.45f), new Color(1f, 1f, 1f, 0.025f));
        Widgets.DrawBoxSolid(new Rect(outer.x, outer.yMax - DeckBottom * 0.35f, outer.width, DeckBottom * 0.35f), new Color(0f, 0f, 0f, 0.18f));
        GUI.color = BezelLight;
        Widgets.DrawBox(outer);
        GUI.color = Color.white;

        var screen = new Rect(outer.x + DeckSide, outer.y + DeckTop, outer.width - 2f * DeckSide, outer.height - DeckTop - DeckBottom);
        // The screen sits in a recess: dark shadow on top and left, a lit lip below and right.
        Widgets.DrawBoxSolid(new Rect(screen.x - 3f, screen.y - 3f, screen.width + 6f, 3f), new Color(0f, 0f, 0f, 0.7f));
        Widgets.DrawBoxSolid(new Rect(screen.x - 3f, screen.y - 3f, 3f, screen.height + 6f), new Color(0f, 0f, 0f, 0.7f));
        Widgets.DrawBoxSolid(new Rect(screen.x - 3f, screen.yMax, screen.width + 6f, 2f), BezelLight);
        Widgets.DrawBoxSolid(new Rect(screen.xMax, screen.y - 3f, 2f, screen.height + 5f), BezelLight);

        // Side rails: a recessed handle slot with grip ridges and two mounting blocks.
        foreach (float rx in new[] { outer.x + DeckSide / 2f - 4f, outer.xMax - DeckSide / 2f - 4f })
        {
            var slot = new Rect(rx, outer.y + 78f, 8f, outer.height - 156f);
            if (slot.height < 40f)
                continue;
            Widgets.DrawBoxSolid(slot, new Color(0f, 0f, 0f, 0.55f));
            for (float gy = slot.y + 30f; gy < slot.yMax - 30f; gy += 6f)
                Widgets.DrawBoxSolid(new Rect(slot.x + 1f, gy, slot.width - 2f, 1f), new Color(1f, 1f, 1f, 0.06f));
            foreach (float my in new[] { slot.y - 4f, slot.yMax - 10f })
            {
                Widgets.DrawBoxSolid(new Rect(slot.x - 3f, my, slot.width + 6f, 14f), BezelLight);
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(slot.center.x - 2.5f, my + 4.5f, 5f, 5f), Disc);
                GUI.color = Color.white;
            }
        }

        // Rubber corner bumpers, each held by a bolt.
        const float bump = 56f;
        foreach ((float bx, float by, int sx, int sy) in new[]
                 {
                     (outer.x, outer.y, 1, 1), (outer.xMax, outer.y, -1, 1), (outer.x, outer.yMax, 1, -1), (outer.xMax, outer.yMax, -1, -1),
                 })
        {
            var h = new Rect(sx > 0 ? bx : bx - bump, sy > 0 ? by : by - (sy > 0 ? DeckTop : DeckBottom), bump, sy > 0 ? DeckTop : DeckBottom);
            var v = new Rect(sx > 0 ? bx : bx - DeckSide, sy > 0 ? by : by - bump, DeckSide, bump);
            Widgets.DrawBoxSolid(h, DeckRubber);
            Widgets.DrawBoxSolid(v, DeckRubber);
            Widgets.DrawBoxSolid(new Rect(h.x, sy > 0 ? h.y : h.yMax - 1f, h.width, 1f), new Color(1f, 1f, 1f, 0.08f));
            var bolt = new Rect((sx > 0 ? bx + DeckSide / 2f : bx - DeckSide / 2f) - 4.5f, (sy > 0 ? by + Mathf.Min(DeckTop, DeckBottom) / 2f : by - Mathf.Min(DeckTop, DeckBottom) / 2f) - 4.5f, 9f, 9f);
            GUI.color = BezelLight;
            GUI.DrawTexture(bolt, Disc);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(bolt.x + 1f, bolt.center.y - 0.75f, bolt.width - 2f, 1.5f), BaseContent.WhiteTex);
            GUI.color = Color.white;
        }

        // Top: two gauges (Heat, the net), a tuning scale with a drifting marker, an antenna port on the right.
        float gx = outer.x + bump + 12f;
        float gd = DeckTop - 8f;
        DeckGauge(new Rect(gx, outer.y + 4f, gd, gd), heat01, heat01 >= 0.6f ? Bad : heat01 >= 0.3f ? Warn : Ok, "NR_DeckGaugeHeat".Translate());
        float net = online ? 0.55f + (anim ? 0.08f * Mathf.Sin(time * 2.3f) + 0.04f * Mathf.Sin(time * 7.1f) : 0f) : 0.02f;
        DeckGauge(new Rect(gx + gd + 8f, outer.y + 4f, gd, gd), net, online ? Accent : Muted, "NR_DeckGaugeNet".Translate());
        var scale = new Rect(gx + 2f * gd + 22f, outer.y + 9f, Mathf.Min(150f, outer.width * 0.18f), DeckTop - 18f);
        Widgets.DrawBoxSolid(scale, new Color(0f, 0f, 0f, 0.55f));
        for (int i = 0; i <= 20; i++)
        {
            float tx = scale.x + 4f + (scale.width - 8f) * i / 20f;
            Widgets.DrawBoxSolid(new Rect(tx, scale.yMax - (i % 5 == 0 ? 8f : 4f), 1f, i % 5 == 0 ? 8f : 4f), new Color(1f, 1f, 1f, 0.35f));
        }
        float drift = anim ? 0.5f + 0.42f * Mathf.Sin(time * 0.37f) : 0.5f;
        Widgets.DrawBoxSolid(new Rect(scale.x + 4f + (scale.width - 8f) * drift - 1f, scale.y + 1f, 2f, scale.height - 2f), DeckOrange);
        var port = new Rect(outer.xMax - bump - 30f, outer.y + 6f, DeckTop - 12f, DeckTop - 12f);
        GUI.color = BezelLight;
        GUI.DrawTexture(port, Disc);
        GUI.color = DeckRubber;
        GUI.DrawTexture(port.ContractedBy(4f), Disc);
        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        GUI.DrawTexture(port.ContractedBy(7f), Disc);
        GUI.color = Color.white;
        Widgets.DrawBoxSolid(new Rect(port.xMax - 2f, port.center.y - 2f, outer.xMax - bump - port.xMax + 2f, 4f), DeckRubber);

        // Bottom: the plate, three hardware keys, a vent grille, the orange key and the power light.
        float by0 = outer.yMax - DeckBottom;
        if (!plate.NullOrEmpty())
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            float pw = Text.CalcSize(plate).x + 18f;
            var pr = new Rect(outer.x + bump + 10f, by0 + 7f, pw, DeckBottom - 14f);
            Widgets.DrawBoxSolid(pr, new Color(0f, 0f, 0f, 0.4f));
            Widgets.DrawBoxSolid(new Rect(pr.x, pr.yMax - 1f, pr.width, 1f), BezelLight);
            Widgets.Label(new Rect(pr.x + 9f, pr.y, pr.width, pr.height), plate.Colorize(new Color(1f, 1f, 1f, 0.66f)));
            Text.Anchor = TextAnchor.UpperLeft;
        }
        string[] glyphs = ["◄", "●", "►"];
        const float kw = 40f, kh = 22f;
        float kx = outer.center.x - (3 * kw + 2 * 8f) / 2f;
        for (int i = 0; i < 3; i++)
        {
            var key = new Rect(kx + i * (kw + 8f), by0 + (DeckBottom - kh) / 2f, kw, kh);
            bool over = Mouse.IsOver(key);
            Widgets.DrawBoxSolid(key, over ? Hex(0x2a2f36) : Hex(0x15181c));
            Widgets.DrawBoxSolid(new Rect(key.x, key.y, key.width, 1f), new Color(1f, 1f, 1f, over ? 0.18f : 0.1f));
            Widgets.DrawBoxSolid(new Rect(key.x, key.yMax - 2f, key.width, 2f), new Color(0f, 0f, 0f, 0.6f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(key, glyphs[i].Colorize(over ? Accent : new Color(1f, 1f, 1f, 0.55f)));
            Text.Anchor = TextAnchor.UpperLeft;
            if (onKey != null)
            {
                Tip(key, ("NR_DeckKey_" + i).Translate());
                if (Widgets.ButtonInvisible(key, doMouseoverSound: false))
                {
                    NetrunSound.Click();
                    onKey(i);
                }
            }
        }
        float vx = outer.xMax - bump - 150f;
        for (int i = 0; i < 9; i++)
            Widgets.DrawBoxSolid(new Rect(vx + i * 8f, by0 + 9f, 4f, DeckBottom - 18f), new Color(0f, 0f, 0f, 0.6f));
        var orange = new Rect(vx + 9 * 8f + 10f, by0 + 10f, 14f, DeckBottom - 20f);
        Widgets.DrawBoxSolid(orange, DeckOrange);
        Widgets.DrawBoxSolid(new Rect(orange.x, orange.y, orange.width, 1f), new Color(1f, 1f, 1f, 0.35f));
        float a = anim ? 0.6f + 0.4f * Mathf.Sin(time * 1.5f) : 1f;
        Led(new Rect(orange.xMax + 14f, by0 + DeckBottom / 2f - 2f, 16f, 4f), new Color(Ok.r, Ok.g, Ok.b, a));
        return screen;
    }

    /// <summary>A round gauge in the casing: a black face, ticks and a needle from -135° to +135°.</summary>
    private static void DeckGauge(Rect r, float value, Color needle, string tip)
    {
        GUI.color = BezelLight;
        GUI.DrawTexture(r, Disc);
        GUI.color = Hex(0x07080a);
        GUI.DrawTexture(r.ContractedBy(2f), Disc);
        GUI.color = Color.white;
        Vector2 c = r.center;
        float rad = r.width / 2f - 3f;
        for (int i = 0; i <= 6; i++)
        {
            float ang = Mathf.Deg2Rad * (-135f + 270f * i / 6f - 90f);
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Widgets.DrawLine(c + dir * (rad - 3f), c + dir * rad, new Color(1f, 1f, 1f, 0.45f), 1f);
        }
        float va = Mathf.Deg2Rad * (-135f + 270f * Mathf.Clamp01(value) - 90f);
        Widgets.DrawLine(c, c + new Vector2(Mathf.Cos(va), Mathf.Sin(va)) * (rad - 2f), needle, 1.5f);
        GUI.color = BezelLight;
        GUI.DrawTexture(new Rect(c.x - 2f, c.y - 2f, 4f, 4f), Disc);
        GUI.color = Color.white;
        Tip(r, tip);
    }

    private static readonly System.Collections.Generic.List<(Rect screen, Texture2D tex, Color color)> LateIcons = [];
    private static int _lateFrame;
    private static readonly System.Reflection.PropertyInfo VisibleRect =
        typeof(GUI).Assembly.GetType("UnityEngine.GUIClip")?.GetProperty("visibleRect", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

    /// <summary>
    /// Draws an icon and remembers it, so the CRT lines drawn over the screen afterwards do not dim it. Only icons that
    /// are fully visible (not cut by a scroll view) are redrawn.
    /// </summary>
    public static void BrightIcon(Rect rect, Texture2D tex)
    {
        if (tex == null)
            return;
        GUI.DrawTexture(rect, tex);
        if (Event.current.type != EventType.Repaint)
            return;
        if (_lateFrame != Time.frameCount)
        {
            LateIcons.Clear();
            _lateFrame = Time.frameCount;
        }
        if (VisibleRect?.GetValue(null) is Rect visible && !(visible.Contains(rect.min) && visible.Contains(rect.max)))
            return;
        LateIcons.Add((new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size), tex, GUI.color));
    }

    /// <summary>Faint CRT lines over a screen (one draw call); the bright icons go back on top.</summary>
    public static void Scanlines(Rect screen)
    {
        if (Event.current.type != EventType.Repaint)
            return;
        GUI.DrawTextureWithTexCoords(screen, ScanlineTex, new Rect(0f, 0f, 1f, screen.height / 3f));
        if (_lateFrame == Time.frameCount)
        {
            Color old = GUI.color;
            foreach ((Rect r, Texture2D tex, Color color) in LateIcons)
            {
                GUI.color = color;
                GUI.DrawTexture(new Rect(GUIUtility.ScreenToGUIPoint(r.position), r.size), tex);
            }
            GUI.color = old;
        }
        LateIcons.Clear();
    }

    /// <summary>A prompt line for empty states and hints: "> text" with a blinking cursor.</summary>
    public static string Prompt(string text)
    {
        bool cursor = NetrunMod.Settings.Animations && Time.realtimeSinceStartup % 1.1f < 0.55f;
        return "> " + text + (cursor ? " _" : "  ");
    }

    /// <summary>A small status light: steady, or blinking once every 1.5 s when <paramref name="blink"/>.</summary>
    public static void Led(Rect r, Color color, bool blink = false)
    {
        float phase = Time.realtimeSinceStartup / 1.5f % 1f;
        float a = blink && NetrunMod.Settings.Animations ? 0.35f + 0.65f * Mathf.Clamp01(1f - Mathf.Abs(phase - 0.15f) / 0.15f) : 1f;
        GUI.color = new Color(color.r, color.g, color.b, 0.35f * a);
        GUI.DrawTexture(r.ExpandedBy(3f), GlowTex);
        GUI.color = new Color(color.r, color.g, color.b, a);
        GUI.DrawTexture(r, BaseContent.WhiteTex);
        GUI.color = Color.white;
    }

    /// <summary>Filled rectangle with a 1 px border.</summary>
    public static void Box(Rect rect, Color fill, Color border, int thickness = 1)
    {
        Widgets.DrawBoxSolid(rect, fill);
        GUI.color = border;
        Widgets.DrawBox(rect, thickness);
        GUI.color = Color.white;
    }

    /// <summary>A card: dark body, colored stripe on the left, thin border.</summary>
    public static void Card(Rect rect, Color stripe)
    {
        Box(rect, BgCard, new Color(stripe.r, stripe.g, stripe.b, 0.35f));
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 4f, rect.height), stripe);
        ScanlineOnce(rect, stripe, SweepTime(rect, 1.4f), 1.4f);
    }

    /// <summary>A small bordered tag like "BROKER"; returns its width.</summary>
    public static float Chip(float x, float y, string text, Color color, string tip = null)
    {
        Text.Font = GameFont.Tiny;
        float width = Text.CalcSize(text).x + 12f;
        var rect = new Rect(x, y, width, 18f);
        Box(rect, new Color(color.r, color.g, color.b, 0.12f), color);
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, text.Colorize(color));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        if (!tip.NullOrEmpty())
            Tip(rect, tip);
        return width;
    }

    /// <summary>A header chip on the window's title bar, right-aligned at xMax; returns its left edge.</summary>
    public static float HeaderChip(float xMax, float y, string text, string tip = null)
    {
        float width = Text.CalcSize(text).x + 20f;
        var rect = new Rect(xMax - width, y, width, 24f);
        Box(rect, BgCardAlt, new Color(1f, 1f, 1f, 0.15f));
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, text);
        Text.Anchor = TextAnchor.UpperLeft;
        if (!tip.NullOrEmpty())
            Tip(rect, tip);
        return rect.x;
    }

    private static bool _pressed;
    private static readonly System.Collections.Generic.Dictionary<long, float> Clicks = [];

    /// <summary>
    /// A light glitch over <paramref name="r"/>: a few thin slices shifted sideways in magenta and cyan, fading as
    /// <paramref name="t"/> goes 0 → 1. The slices change every couple of frames.
    /// </summary>
    public static void Glitch(Rect r, float t, int seed, int slices = 3)
    {
        if (Event.current.type != EventType.Repaint)
            return;
        var rnd = new System.Random(seed * 7349 + Time.frameCount / 2);
        float a = 1f - t;
        for (int i = 0; i < slices; i++)
        {
            float h = 1f + (float)rnd.NextDouble() * Mathf.Min(5f, r.height * 0.2f);
            float y = r.y + (float)rnd.NextDouble() * (r.height - h);
            float shift = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Min(10f, r.width * 0.08f);
            float w = r.width * (0.3f + 0.7f * (float)rnd.NextDouble());
            float x = r.x + (float)rnd.NextDouble() * (r.width - w);
            Color c = i % 2 == 0 ? new Color(1f, 0.2f, 0.55f) : new Color(0.25f, 0.9f, 1f);
            GUI.color = new Color(BgWindow.r, BgWindow.g, BgWindow.b, 0.7f * a);
            GUI.DrawTexture(new Rect(x, y, w, h), BaseContent.WhiteTex);
            GUI.color = new Color(c.r, c.g, c.b, 0.45f * a);
            GUI.DrawTexture(new Rect(x + shift, y, w, Mathf.Max(1f, h * 0.5f)), BaseContent.WhiteTex);
        }
        GUI.color = Color.white;
    }

    /// <summary>A bordered button in the given color; disabled ones are muted and explain why.</summary>
    private static int _hoverKey;

    private const string Glyphs = "01#$%&*+=?/|[]{}~^<>";
    private static readonly System.Collections.Generic.Dictionary<long, float> RewriteStarts = [];
    private static readonly System.Collections.Generic.Dictionary<string, (float start, int frame)> Appears = [];

    /// <summary>Scrambled glyphs settling left to right: <paramref name="k"/> 0 all noise, 1 the plain text.</summary>
    public static string Rewrite(string text, float k, int seed)
    {
        if (k >= 1f || text.NullOrEmpty())
            return text;
        int settled = Mathf.FloorToInt(text.Length * Mathf.Clamp01(k));
        var rnd = new System.Random(seed ^ (Time.frameCount / 3));
        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
            sb.Append(i < settled || !char.IsLetterOrDigit(text[i]) ? text[i] : Glyphs[rnd.Next(Glyphs.Length)]);
        return sb.ToString();
    }

    /// <summary>
    /// A button's label rewrites itself when the mouse comes onto it: 0.25 s of glyphs, and at most once in
    /// 4 seconds per button, so it never gets in the way of reading.
    /// </summary>
    private static string RewriteOnHover(string label, Rect hit)
    {
        if (!NetrunMod.Settings.Animations)
            return label;
        float t = HoverTime(hit);
        if (t < 0f)
            return label;
        long key = Key(hit);
        float now = Time.realtimeSinceStartup;
        if (t < 0.05f && (!RewriteStarts.TryGetValue(key, out float last) || now - last > 4f))
        {
            if (RewriteStarts.Count > 300)
                RewriteStarts.Clear();
            RewriteStarts[key] = now - t;
        }
        return RewriteStarts.TryGetValue(key, out float start) && now - start < 0.25f ? Rewrite(label, (now - start) / 0.25f, (int)key) : label;
    }

    /// <summary>Text that decodes itself when it appears on screen (not seen in the last half second).</summary>
    public static string RewriteOnAppear(string id, string text, float seconds = 0.35f)
    {
        if (!NetrunMod.Settings.Animations)
            return text;
        float now = Time.realtimeSinceStartup;
        int frame = Time.frameCount;
        if (!Appears.TryGetValue(id, out var a) || frame - a.frame > 30)
        {
            if (Appears.Count > 300)
                Appears.Clear();
            a = (now, frame);
        }
        Appears[id] = (a.start, frame);
        return now - a.start < seconds ? Rewrite(text, (now - a.start) / seconds, id.GetHashCode()) : text;
    }

    public static bool Button(Rect rect, string label, Color color, AcceptanceReport report, string tip = null, Texture2D icon = null)
    {
        bool active = report.Accepted;
        Rect hit = rect;
        if (Event.current.rawType == EventType.MouseDown && Event.current.button == 0)
            _pressed = true;
        else if (Event.current.rawType == EventType.MouseUp)
            _pressed = false;
        float lit = active ? HoverFade(hit, 0.1f) : 0f;
        if (active && lit > 0f && _pressed && NetrunMod.Settings.Animations)
            rect.y += 2f;
        Color c = active ? color : Muted;
        float glitch = Clicks.TryGetValue(Key(hit), out float clickedAt) ? Time.realtimeSinceStartup - clickedAt : 99f;
        if (glitch < 0.18f && NetrunMod.Settings.Animations)
            rect.x += (Time.frameCount / 2 % 2 == 0 ? 1.5f : -1.5f) * (1f - glitch / 0.18f);
        Box(rect, Color.Lerp(new Color(0f, 0f, 0f, 0.15f), new Color(c.r, c.g, c.b, 0.18f), lit), new Color(c.r, c.g, c.b, active ? 0.75f + 0.25f * lit : 0.5f));
        Rect textRect = rect;
        if (icon != null)
        {
            float size = Mathf.Min(rect.height - 4f, Icon(rect.height - 8f));
            GUI.color = active ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            BrightIcon(new Rect(rect.x + 5f, rect.center.y - size / 2f, size, size), icon);
            GUI.color = Color.white;
            // Keep the label centred on the whole button when the icon fits beside it.
            float labelWidth = Text.CalcSize(label).x;
            if ((rect.width - labelWidth) / 2f < size + 10f)
                textRect.xMin += size + 6f;
        }
        Text.Anchor = TextAnchor.MiddleCenter;
        string shownLabel = active ? RewriteOnHover(label, hit) : label;
        Widgets.Label(textRect, shownLabel.Colorize(active ? c : new Color(c.r, c.g, c.b, 0.7f)));
        Text.Anchor = TextAnchor.UpperLeft;

        string fullTip = tip ?? string.Empty;
        if (!active && !report.Reason.NullOrEmpty())
            fullTip = (fullTip.NullOrEmpty() ? string.Empty : fullTip + "\n\n") + report.Reason.Colorize(ColorLibrary.RedReadable);
        if (!fullTip.NullOrEmpty())
            Tip(hit, fullTip);

        if (glitch < 0.18f && NetrunMod.Settings.Animations)
            Glitch(rect, glitch / 0.18f, (int)Key(hit), 2);
        if (Event.current.type == EventType.Repaint)
        {
            int key = (int)Key(hit);
            if (active && Mouse.IsOver(hit))
            {
                if (_hoverKey != key)
                {
                    _hoverKey = key;
                    NetrunSound.Hover();
                }
            }
            else if (_hoverKey == key)
                _hoverKey = 0;
        }
        if (!active && Widgets.ButtonInvisible(hit, doMouseoverSound: false))
            NetrunSound.Play("UI_Deny");
        if (active && Widgets.ButtonInvisible(hit))
        {
            NetrunSound.Click();
            if (Clicks.Count > 200)
                Clicks.Clear();
            Clicks[Key(hit)] = Time.realtimeSinceStartup;
            return true;
        }
        return false;
    }

    /// <summary>Width a bordered button needs for its label.</summary>
    public static float ButtonWidth(string label, bool icon = false) => Text.CalcSize(label).x + 28f + (icon ? 30f : 0f);

    public static void Bar(Rect rect, float fraction, Color color)
    {
        fraction = Mathf.Clamp01(fraction);
        Widgets.DrawBoxSolid(rect, BarBack);
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 1f), new Color(0f, 0f, 0f, 0.5f));
        float w = Mathf.Round(rect.width * fraction);
        if (w > 0f)
        {
            var fill = new Rect(rect.x, rect.y, w, rect.height);
            Widgets.DrawBoxSolid(fill, new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, color.a));
            Widgets.DrawBoxSolid(new Rect(fill.x, fill.y, fill.width, Mathf.Ceil(fill.height / 2f)), color);
            if (rect.height >= 6f)
                Widgets.DrawBoxSolid(new Rect(fill.x, fill.y, fill.width, 1f), new Color(1f, 1f, 1f, 0.25f));
            if (w > 3f && fraction < 1f)
            {
                GUI.color = new Color(color.r, color.g, color.b, 0.45f);
                GUI.DrawTexture(new Rect(fill.xMax - 6f, fill.y - 4f, 12f, fill.height + 8f), GlowTex);
                Widgets.DrawBoxSolid(new Rect(fill.xMax - 2f, fill.y, 2f, fill.height), new Color(1f, 1f, 1f, 0.6f));
            }
        }
        // Segment ticks every tenth (every quarter on short bars).
        int segments = rect.width >= 120f ? 10 : 4;
        for (int i = 1; i < segments; i++)
        {
            float x = Mathf.Round(rect.x + rect.width * i / segments);
            Widgets.DrawBoxSolid(new Rect(x, rect.y, 1f, rect.height), new Color(0f, 0f, 0f, 0.35f));
        }
        GUI.color = new Color(1f, 1f, 1f, 0.16f);
        Widgets.DrawBox(rect);
        GUI.color = Color.white;
    }

    /// <summary>A name on a plate: a dark box with a colored bar and corner ticks, the name in the given font.</summary>
    public static float NamePlate(float x, float y, string name, Color color, GameFont font = GameFont.Medium, float maxWidth = 9999f)
    {
        Text.Font = font;
        float w = Mathf.Min(Text.CalcSize(name).x + 22f, maxWidth);
        float h = font == GameFont.Medium ? 30f : 24f;
        var r = new Rect(x, y, w, h);
        Widgets.DrawBoxSolid(r, new Color(color.r * 0.15f, color.g * 0.15f, color.b * 0.15f, 0.9f));
        Widgets.DrawBoxSolid(new Rect(r.x, r.y, 3f, r.height), color);
        Brackets(r, new Color(color.r, color.g, color.b, 0.7f), 1.5f, 7f);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(r.x + 10f, r.y, r.width - 12f, r.height), name.Truncate(r.width - 12f));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        return w;
    }

    /// <summary>A section title with a count and an accent line under it.</summary>
    public static float Section(Rect rect, string label)
    {
        // Terminal look with the game's own font: a block marker, the title in capitals, a "//" tail,
        // and a bright tick where the line ends.
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + 6f, 3f, 15f), Accent);
        Text.Anchor = TextAnchor.MiddleLeft;
        string text = label.ToUpper();
        GameFont font = Text.Font;
        float room = rect.width - 14f;
        if (Text.CalcSize(text).x > room)
            Text.Font = GameFont.Tiny;
        string shown = Text.CalcSize(text).x > room ? text.Truncate(room) : text;
        var titleRect = new Rect(rect.x + 10f, rect.y, rect.width - 10f, 26f);
        Widgets.Label(titleRect, RewriteOnAppear("section." + label, shown).Colorize(Accent));
        if (shown != text)
            Tip(titleRect, label);
        float tw = Text.CalcSize(shown).x;
        Text.Font = font;
        if (rect.x + 10f + tw + 30f < rect.xMax)
            Widgets.Label(new Rect(rect.x + 10f + tw + 8f, rect.y, 30f, 26f), "//".Colorize(new Color(Accent.r, Accent.g, Accent.b, 0.35f)));
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = new Color(Accent.r, Accent.g, Accent.b, 0.55f);
        Widgets.DrawLineHorizontal(rect.x, rect.y + 28f, rect.width);
        GUI.color = Accent;
        GUI.DrawTexture(new Rect(rect.xMax - 10f, rect.y + 27f, 10f, 3f), BaseContent.WhiteTex);
        // Now and then a bright spark runs along the line.
        if (NetrunMod.Settings.Animations)
        {
            float f = (Time.realtimeSinceStartup + rect.y * 0.013f) / 5f % 1f;
            if (f < 0.35f)
            {
                float x = rect.x + rect.width * (f / 0.35f);
                GUI.color = new Color(1f, 1f, 1f, 0.8f * Mathf.Sin(f / 0.35f * Mathf.PI));
                GUI.DrawTexture(new Rect(Mathf.Max(rect.x, x - 30f), rect.y + 27.5f, Mathf.Min(30f, x - rect.x), 2f), BaseContent.WhiteTex);
            }
        }
        GUI.color = Color.white;
        return rect.y + 36f;
    }


    // ---- Tooltips in the terminal style ----

    private const int TipWindowId = 0x4E52_5450;
    private const float TipMaxWidth = 320f;

    /// <summary>A terminal-style tooltip over <paramref name="rect"/>: dark box, accent bar, the text. Use instead of TooltipHandler.TipRegion.</summary>
    public static void Tip(Rect rect, string text)
    {
        if (text.NullOrEmpty() || !Mouse.IsOver(rect) || Event.current.type != EventType.Repaint)
            return;
        float shown = NetrunMod.Settings.Animations ? Mathf.Clamp01((HoverTime(rect) - 0.2f) / 0.15f) : 1f;
        if (shown <= 0f)
            return;
        Vector2 mouse = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
        // Measure in the tooltip's font, then give the caller its font back: rows drawn after a hovered one keep their size.
        GameFont callerFont = Text.Font;
        Text.Font = GameFont.Small;
        float w = Mathf.Min(TipMaxWidth, Text.CalcSize(text.StripTags()).x + 4f);
        float h = Text.CalcHeight(text, w);
        Text.Font = callerFont;
        var box = new Rect(mouse.x + 18f, mouse.y + 18f, w + 22f, h + 14f);
        if (box.xMax > UI.screenWidth - 4f)
            box.x = mouse.x - box.width - 8f;
        if (box.yMax > UI.screenHeight - 4f)
            box.y = mouse.y - box.height - 8f;
        Find.WindowStack.ImmediateWindow(TipWindowId, box, WindowLayer.Super, () =>
        {
            bool font = NetrunFont.Push();
            bool size = NetrunText.Push();
            var local = new Rect(0f, 0f, box.width, box.height);
            Widgets.DrawBoxSolid(local, new Color(BgWindow.r, BgWindow.g, BgWindow.b, 0.97f * shown));
            GUI.color = new Color(Accent.r, Accent.g, Accent.b, 0.6f * shown);
            Widgets.DrawBox(local);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(0f, 0f, 3f, local.height), new Color(Accent.r, Accent.g, Accent.b, shown));
            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 1f, 1f, shown);
            Widgets.Label(new Rect(12f, 7f, w + 4f, h + 2f), text);
            GUI.color = Color.white;
            NetrunText.Pop(size);
            NetrunFont.Pop(font);
        }, doBackground: false, absorbInputAroundWindow: false, shadowAlpha: 0f);
    }


    // ---- Typewriter: text that types itself out once, like the dive log ----

    private static readonly System.Collections.Generic.Dictionary<string, float> TypeStarts = [];

    /// <summary>
    /// The part of <paramref name="text"/> typed so far (plain, with a "█" cursor) the first time this key is seen;
    /// the full text once done or when animations are off. A new key (new text) types again.
    /// </summary>
    public static string Typed(string key, string text, float charsPerSecond = 70f, float delay = 0f)
    {
        if (!NetrunMod.Settings.Animations || text.NullOrEmpty())
            return text;
        float now = Time.realtimeSinceStartup;
        if (!TypeStarts.TryGetValue(key, out float start))
        {
            if (TypeStarts.Count > 200)
                TypeStarts.Clear();
            TypeStarts[key] = start = now;
        }
        float elapsed = now - start - delay;
        string plain = text.StripTags();
        if (elapsed <= 0f)
            return string.Empty;
        int shown = Mathf.FloorToInt(elapsed * charsPerSecond);
        return shown >= plain.Length ? text : plain.Substring(0, shown) + "█";
    }

    /// <summary>Forget a typed key so the text types again (e.g. a window opened anew).</summary>
    public static void Retype(string key) => TypeStarts.Remove(key);
}
