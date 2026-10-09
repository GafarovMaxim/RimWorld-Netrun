using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The terminal's own scroll bar (docs/TASKS_2026-10-08.md, B4): a thin guide with ticks and a thumb with an accent
/// frame and notches, 10 px wide, in the 16 px the callers already leave for a bar. Drag the thumb or click the
/// guide; the mouse wheel works as before (the game's scroll view, its own bars hidden).
/// </summary>
public static partial class NetrunUI
{
    private const float ScrollBarWidth = 10f;
    private static int _scrollDragId = -1;
    private static float _scrollDragOffset;

    /// <summary>Use in place of Widgets.BeginScrollView; close with Widgets.EndScrollView as usual.</summary>
    public static void BeginScroll(Rect outer, ref Vector2 scroll, Rect view)
    {
        float overflow = view.height - outer.height;
        if (overflow > 0.5f)
            ScrollBar(outer, ref scroll, overflow, view.height);
        Widgets.BeginScrollView(outer, ref scroll, view, showScrollbars: false);
    }

    private static void ScrollBar(Rect outer, ref Vector2 scroll, float overflow, float viewHeight)
    {
        var track = new Rect(outer.xMax - ScrollBarWidth, outer.y + 2f, ScrollBarWidth, outer.height - 4f);
        float thumbH = Mathf.Max(28f, track.height * outer.height / viewHeight);
        float room = Mathf.Max(1f, track.height - thumbH);
        float t = Mathf.Clamp01(scroll.y / overflow);
        var thumb = new Rect(track.x, track.y + room * t, ScrollBarWidth, thumbH);
        int id = Mathf.RoundToInt(outer.x * 7f + outer.y * 13f + outer.width * 17f) ^ 0x5C2011;
        Event e = Event.current;
        Vector2 mouse = e.mousePosition;

        if (e.type == EventType.MouseDown && e.button == 0 && track.ExpandedBy(2f, 0f).Contains(mouse))
        {
            // Grab the thumb where it was clicked, or jump the thumb's middle to the click on the guide.
            _scrollDragId = id;
            _scrollDragOffset = thumb.Contains(mouse) ? mouse.y - thumb.y : thumbH / 2f;
            scroll.y = Mathf.Clamp01((mouse.y - _scrollDragOffset - track.y) / room) * overflow;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && _scrollDragId == id)
        {
            scroll.y = Mathf.Clamp01((mouse.y - _scrollDragOffset - track.y) / room) * overflow;
            e.Use();
        }
        else if (e.rawType == EventType.MouseUp && _scrollDragId == id)
        {
            _scrollDragId = -1;
        }

        if (e.type != EventType.Repaint)
            return;
        bool hot = _scrollDragId == id || Mouse.IsOver(track.ExpandedBy(2f, 0f));
        // The guide: a thin line with ticks every 24 px.
        Widgets.DrawBoxSolid(new Rect(track.center.x - 1f, track.y, 2f, track.height), new Color(Accent.r, Accent.g, Accent.b, hot ? 0.25f : 0.14f));
        for (float ty = track.y + 6f; ty < track.yMax - 4f; ty += 24f)
            Widgets.DrawBoxSolid(new Rect(track.center.x - 3f, ty, 6f, 1f), new Color(Accent.r, Accent.g, Accent.b, 0.12f));
        // The thumb: dark, an accent frame, three notches in the middle; a soft glow while held or hovered.
        if (hot)
            Glow(thumb, Accent, 0.35f, 6f);
        Widgets.DrawBoxSolid(thumb, new Color(BgCard.r, BgCard.g, BgCard.b, 0.95f));
        Widgets.DrawBoxSolid(thumb.ContractedBy(1f), new Color(Accent.r, Accent.g, Accent.b, hot ? 0.22f : 0.1f));
        GUI.color = new Color(Accent.r, Accent.g, Accent.b, hot ? 1f : 0.65f);
        Widgets.DrawBox(thumb);
        GUI.color = Color.white;
        for (int i = -1; i <= 1; i++)
            Widgets.DrawBoxSolid(new Rect(thumb.x + 2f, thumb.center.y + i * 4f, thumb.width - 4f, 1f), new Color(Accent.r, Accent.g, Accent.b, hot ? 0.9f : 0.55f));
    }
}
