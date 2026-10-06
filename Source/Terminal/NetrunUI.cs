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

    /// <summary>A bordered button in the given color; disabled ones are muted and explain why.</summary>
    public static bool Button(Rect rect, string label, Color color, AcceptanceReport report, string tip = null, Texture2D icon = null)
    {
        bool active = report.Accepted;
        bool hover = Mouse.IsOver(rect);
        Color c = active ? color : Muted;
        Box(rect, active && hover ? new Color(c.r, c.g, c.b, 0.18f) : new Color(0f, 0f, 0f, 0.15f), new Color(c.r, c.g, c.b, active ? 1f : 0.5f));
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
            Tip(rect, fullTip);

        if (active && Widgets.ButtonInvisible(rect))
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
            var local = new Rect(0f, 0f, box.width, box.height);
            Widgets.DrawBoxSolid(local, new Color(BgWindow.r, BgWindow.g, BgWindow.b, 0.97f));
            GUI.color = new Color(Accent.r, Accent.g, Accent.b, 0.6f);
            Widgets.DrawBox(local);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(0f, 0f, 3f, local.height), Accent);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(12f, 7f, w + 4f, h + 2f), text);
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
