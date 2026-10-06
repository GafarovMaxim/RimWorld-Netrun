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

    /// <summary>True while the terminal collects areas (outside the terminal, calls are ignored).</summary>
    public static bool Collecting;

    public static IReadOnlyList<(string id, Rect rect)> Current => Areas;

    public static void Begin()
    {
        Areas.Clear();
        Collecting = true;
    }

    public static void End() => Collecting = false;

    public static void Area(string id, Rect rect)
    {
        if (!Collecting || rect.width < 4f || rect.height < 4f)
            return;
        Areas.RemoveAll(a => a.id == id);
        Areas.Add((id, rect));
    }
}
