using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Building blocks for the Cyberpedia's live demos, so every block looks the same and nothing spills out:
/// a frame that sizes itself to what was drawn in it, label | value rows in the normal font, buttons that wrap,
/// and the terminal's own toggles (with the mod's click sound, not the game's checkbox).
/// </summary>
public partial class HelpBrowser
{
    private readonly Dictionary<string, float> _frameHeights = [];

    /// <summary>
    /// A demo frame: card background, corner brackets in <paramref name="accent"/>. The body draws from the top of
    /// the inner rect and returns where it ended; the frame keeps that height (last frame's, so it never cuts).
    /// </summary>
    private float Frame(string id, float y, float w, Color accent, Func<Rect, float> body)
    {
        float h = _frameHeights.TryGetValue(id, out float known) ? known : 200f;
        var box = new Rect(0f, y, w, h);
        Widgets.DrawBoxSolid(box, NetrunUI.BgCard);
        NetrunUI.Brackets(box, new Color(accent.r, accent.g, accent.b, 0.85f), 2f, 16f);
        var inner = new Rect(box.x + 14f, box.y + 12f, box.width - 28f, 100000f);
        GameFont font = Text.Font;
        float end = body(inner);
        Text.Font = font;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
        float need = Mathf.Ceil(end - y + 12f);
        if (Event.current.type == EventType.Repaint || !_frameHeights.ContainsKey(id))
            _frameHeights[id] = need;
        return y + Mathf.Max(h, need) + 14f;
    }

    /// <summary>A sub-panel inside a frame (darker), sized to its body in the same way.</summary>
    private float Panel(string id, Rect area, float y, Color stripe, Func<Rect, float> body)
    {
        float h = _frameHeights.TryGetValue(id, out float known) ? known : 120f;
        var box = new Rect(area.x, y, area.width, h);
        Widgets.DrawBoxSolid(box, NetrunUI.BgWindow);
        Widgets.DrawBoxSolid(new Rect(box.x, box.y, 3f, box.height), stripe);
        var inner = new Rect(box.x + 12f, box.y + 10f, box.width - 22f, 100000f);
        float end = body(inner);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
        float need = Mathf.Ceil(end - y + 10f);
        if (Event.current.type == EventType.Repaint || !_frameHeights.ContainsKey(id))
            _frameHeights[id] = need;
        return y + Mathf.Max(h, need);
    }

    /// <summary>Plain text in the given font, wrapped to the width; returns the next y.</summary>
    private static float Para(Rect area, float y, string text, Color color, GameFont font = GameFont.Small)
    {
        Text.Font = font;
        float h = Text.CalcHeight(text, area.width);
        Widgets.Label(new Rect(area.x, y, area.width, h), text.Colorize(color));
        Text.Font = GameFont.Small;
        return y + h + 4f;
    }

    /// <summary>A label | value row (normal font, both wrap), with a faint line under it.</summary>
    private static float KV(Rect area, float y, string label, string value, Color? valueColor = null, float labelWidth = 130f, Texture2D icon = null)
    {
        Text.Font = GameFont.Small;
        float lw = Mathf.Min(labelWidth, area.width * 0.4f);
        float vw = area.width - lw - 8f;
        float h = Mathf.Max(24f, Mathf.Max(Text.CalcHeight(label, lw - (icon != null ? 24f : 0f)), Text.CalcHeight(value, vw)) + 4f);
        float lx = area.x;
        if (icon != null)
        {
            GUI.DrawTexture(new Rect(lx, y + (h - 20f) / 2f, 20f, 20f), icon);
            lx += 24f;
        }
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(lx, y, lw - (lx - area.x), h), label.Colorize(NetrunUI.Muted));
        Widgets.Label(new Rect(area.x + lw + 8f, y, vw, h), valueColor.HasValue ? value.Colorize(valueColor.Value) : value);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = new Color(1f, 1f, 1f, 0.07f);
        Widgets.DrawLineHorizontal(area.x, y + h, area.width);
        GUI.color = Color.white;
        return y + h + 2f;
    }

    /// <summary>The terminal's toggle: a square that lights with a ✓, the label beside it; our click sound.</summary>
    private static float Toggle(Rect area, float y, string label, ref bool value, string tip = null)
    {
        Text.Font = GameFont.Small;
        float h = Mathf.Max(24f, Text.CalcHeight(label, area.width - 30f) + 2f);
        var row = new Rect(area.x, y, area.width, h);
        bool over = Mouse.IsOver(row);
        if (over)
            Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.04f));
        var box = new Rect(area.x + 1f, y + (h - 18f) / 2f, 18f, 18f);
        Color c = value ? NetrunUI.Ok : over ? NetrunUI.Accent : NetrunUI.Muted;
        NetrunUI.Box(box, value ? new Color(c.r, c.g, c.b, 0.22f) : NetrunUI.BgWindow, c);
        if (value)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(box, "✓".Colorize(NetrunUI.Ok));
        }
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(area.x + 28f, y, area.width - 28f, h), value ? label : label.Colorize(NetrunUI.TextColor));
        Text.Anchor = TextAnchor.UpperLeft;
        if (tip != null)
            NetrunUI.Tip(row, tip);
        if (Widgets.ButtonInvisible(row, doMouseoverSound: false))
        {
            value = !value;
            NetrunSound.Click();
        }
        return y + h + 2f;
    }

    /// <summary>A labelled slider; returns the next y.</summary>
    private static float Slider(Rect area, float y, string label, ref float value, float min, float max, float roundTo = 1f)
    {
        y = Para(area, y, label, NetrunUI.Muted, GameFont.Tiny) - 2f;
        value = Widgets.HorizontalSlider(new Rect(area.x, y, area.width, 18f), value, min, max, roundTo: roundTo);
        return y + 24f;
    }

    /// <summary>
    /// A row of choice buttons that wraps to new lines instead of running off the frame. Returns the next y.
    /// </summary>
    private static float Choices<T>(Rect area, float y, IEnumerable<T> items, Func<T, string> label, Func<T, bool> isOn, Action<T> pick,
        Func<T, Color> color = null, Func<T, Texture2D> icon = null, Func<T, string> tip = null)
    {
        float x = area.x;
        const float h = 28f;
        foreach (T item in items)
        {
            string text = label(item);
            Texture2D tex = icon?.Invoke(item);
            float bw = Mathf.Min(area.width, NetrunUI.ButtonWidth(text, tex != null));
            if (x > area.x && x + bw > area.xMax)
            {
                x = area.x;
                y += h + 6f;
            }
            bool on = isOn(item);
            Color c = on ? color?.Invoke(item) ?? NetrunUI.Ok : NetrunUI.Muted;
            var r = new Rect(x, y, bw, h);
            if (on)
                Widgets.DrawBoxSolid(r, new Color(c.r, c.g, c.b, 0.14f));
            if (NetrunUI.Button(r, text, c, true, tip?.Invoke(item), tex))
                pick(item);
            x += bw + 6f;
        }
        return y + h + 8f;
    }

    /// <summary>The big number of a demo (a result) with a bar under it.</summary>
    private static float Result(Rect area, float y, string big, float fraction, Color color)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(area.x, y, area.width, 34f), big.Colorize(color));
        Text.Font = GameFont.Small;
        NetrunUI.Bar(new Rect(area.x, y + 36f, area.width, 12f), Mathf.Clamp01(fraction), color);
        return y + 56f;
    }

    /// <summary>Two columns side by side when the frame is wide enough, stacked otherwise; returns the lower end.</summary>
    private static float Columns(Rect area, float y, float leftShare, Func<Rect, float, float> left, Func<Rect, float, float> right)
    {
        if (area.width < 520f)
        {
            float mid = left(area, y);
            return right(area, mid + 6f);
        }
        float lw = area.width * leftShare - 8f;
        var l = new Rect(area.x, area.y, lw, area.height);
        var r = new Rect(area.x + lw + 16f, area.y, area.width - lw - 16f, area.height);
        return Mathf.Max(left(l, y), right(r, y));
    }
}
