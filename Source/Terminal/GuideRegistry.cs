using System.Collections.Generic;
using UnityEngine;

namespace Netrun;

/// <summary>
/// Screen areas for the guide mode (docs/HELP.md): each screen calls Area(id, rect) while drawing;
/// the terminal clears the list every frame and, in guide mode, draws the overlay over these rects.
/// Rects are in the terminal window's coordinates.
/// </summary>
public static class GuideRegistry
{
    private static readonly List<(string id, Rect rect)> Areas = [];
    private static readonly Dictionary<string, int> Index = [];

    /// <summary>True while the terminal collects areas (outside the terminal, calls are ignored).</summary>
    public static bool Collecting;

    public static IReadOnlyList<(string id, Rect rect)> Current => Areas;

    /// <summary>Starts a frame; areas are only collected while the guide is on (they cost nothing otherwise).</summary>
    public static void Begin(bool collect = true)
    {
        Areas.Clear();
        Index.Clear();
        Collecting = collect;
    }

    public static void End() => Collecting = false;

    /// <summary>An area inside a scroll view: <paramref name="local"/> in view coordinates, clipped to the visible part.</summary>
    public static void InScroll(string id, Rect outer, Vector2 scroll, Rect local)
    {
        if (!Collecting)
            return;
        var r = new Rect(outer.x + local.x - scroll.x, outer.y + local.y - scroll.y, local.width, local.height);
        float x0 = Mathf.Max(r.x, outer.x), y0 = Mathf.Max(r.y, outer.y);
        float x1 = Mathf.Min(r.xMax, outer.xMax), y1 = Mathf.Min(r.yMax, outer.yMax);
        if (x1 - x0 > 8f && y1 - y0 > 8f)
            Area(id, new Rect(x0, y0, x1 - x0, y1 - y0));
    }

    public static void Area(string id, Rect rect)
    {
        if (!Collecting || rect.width < 4f || rect.height < 4f)
            return;
        if (Index.TryGetValue(id, out int i))
        {
            Areas[i] = (id, rect);
            return;
        }
        Index[id] = Areas.Count;
        Areas.Add((id, rect));
    }
}
