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
    public static float Rolling(string key, float target, out float lastDelta, out float sinceChange, float seconds = 0.6f)
    {
        float now = Time.realtimeSinceStartup;
        if (!Values.TryGetValue(key, out var v))
            v = (target, -10f, 0f);
        if (Mathf.Abs(target - v.value) >= 0.5f && now - v.changed > seconds)
            v = (v.value, now, target - v.value);
        Values[key] = v;
        lastDelta = v.delta;
        sinceChange = now - v.changed;
        if (!NetrunMod.Settings.Animations || sinceChange >= seconds)
        {
            Values[key] = (target, v.changed, v.delta);
            return target;
        }
        float t = sinceChange / seconds;
        t = 1f - (1f - t) * (1f - t) * (1f - t);
        float start = target - v.delta;
        return Mathf.Lerp(start, target, t);
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
        float y = r.y + r.height * (t / duration);
        GUI.color = new Color(color.r, color.g, color.b, 0.12f);
        GUI.DrawTexture(new Rect(r.x, Mathf.Max(r.y, y - 12f), r.width, Mathf.Min(12f, y - r.y)), BaseContent.WhiteTex);
        GUI.color = new Color(color.r, color.g, color.b, 0.7f);
        GUI.DrawTexture(new Rect(r.x, y, r.width, 1f), BaseContent.WhiteTex);
        GUI.color = Color.white;
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
        ScanlineOnce(rect, stripe, HoverTime(rect), 0.5f);
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

    /// <summary>A bordered button in the given color; disabled ones are muted and explain why.</summary>
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
        Box(rect, Color.Lerp(new Color(0f, 0f, 0f, 0.15f), new Color(c.r, c.g, c.b, 0.18f), lit), new Color(c.r, c.g, c.b, active ? 0.75f + 0.25f * lit : 0.5f));
        Rect textRect = rect;
        if (icon != null)
        {
            float size = rect.height - 8f;
            GUI.color = active ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            GUI.DrawTexture(new Rect(rect.x + 5f, rect.y + 4f, size, size), icon);
            GUI.color = Color.white;
            // Keep the label centred on the whole button when the icon fits beside it.
            float labelWidth = Text.CalcSize(label).x;
            if ((rect.width - labelWidth) / 2f < size + 10f)
                textRect.xMin += size + 6f;
        }
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(textRect, label.Colorize(active ? c : new Color(c.r, c.g, c.b, 0.7f)));
        Text.Anchor = TextAnchor.UpperLeft;

        string fullTip = tip ?? string.Empty;
        if (!active && !report.Reason.NullOrEmpty())
            fullTip = (fullTip.NullOrEmpty() ? string.Empty : fullTip + "\n\n") + report.Reason.Colorize(ColorLibrary.RedReadable);
        if (!fullTip.NullOrEmpty())
            Tip(hit, fullTip);

        if (active && Widgets.ButtonInvisible(hit))
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            return true;
        }
        return false;
    }

    /// <summary>Width a bordered button needs for its label.</summary>
    public static float ButtonWidth(string label, bool icon = false) => Text.CalcSize(label).x + 28f + (icon ? 30f : 0f);

    public static void Bar(Rect rect, float fraction, Color color)
    {
        Widgets.DrawBoxSolid(rect, BarBack);
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), color);
        GUI.color = new Color(1f, 1f, 1f, 0.2f);
        Widgets.DrawBox(rect);
        GUI.color = Color.white;
    }

    /// <summary>A section title with a count and an accent line under it.</summary>
    public static float Section(Rect rect, string label)
    {
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(rect.x, rect.y, rect.width, 26f), label.ToUpper().Colorize(Accent));
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Accent;
        Widgets.DrawLineHorizontal(rect.x, rect.y + 28f, rect.width);
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
        Text.Font = GameFont.Small;
        float w = Mathf.Min(TipMaxWidth, Text.CalcSize(text.StripTags()).x + 4f);
        float h = Text.CalcHeight(text, w);
        var box = new Rect(mouse.x + 18f, mouse.y + 18f, w + 22f, h + 14f);
        if (box.xMax > UI.screenWidth - 4f)
            box.x = mouse.x - box.width - 8f;
        if (box.yMax > UI.screenHeight - 4f)
            box.y = mouse.y - box.height - 8f;
        Find.WindowStack.ImmediateWindow(TipWindowId, box, WindowLayer.Super, () =>
        {
            bool font = NetrunFont.Push();
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
