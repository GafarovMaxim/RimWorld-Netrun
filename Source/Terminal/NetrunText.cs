using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The mod setting "text size": while the mod's windows draw, the game's three text styles get a larger (or smaller)
/// font size, put back right after. The font itself never changes, so measuring stays right.
/// </summary>
public static class NetrunText
{
    private static readonly int[] Saved = new int[3];
    private static readonly int[] SavedFields = new int[3];
    private static int _depth;

    private static float Scale => Mathf.Clamp(NetrunMod.Settings?.TextScale ?? 1f, 0.85f, 1.25f);

    /// <summary>Line height of the current font at the current size (Text.LineHeight is cached at the base size).</summary>
    public static float LineHeight => Text.CalcSize("Ag").y;

    public static bool Push()
    {
        float scale = Scale;
        if (Mathf.Abs(scale - 1f) < 0.01f)
            return false;
        if (_depth++ == 0)
        {
            for (int i = 0; i < 3; i++)
            {
                GUIStyle style = Text.fontStyles[i];
                GUIStyle field = Text.textFieldStyles[i];
                Saved[i] = style.fontSize;
                SavedFields[i] = field.fontSize;
                int baseSize = style.fontSize > 0 ? style.fontSize : style.font != null ? style.font.fontSize : 12;
                int baseField = field.fontSize > 0 ? field.fontSize : field.font != null ? field.font.fontSize : 12;
                style.fontSize = Mathf.Max(6, Mathf.RoundToInt(baseSize * scale));
                field.fontSize = Mathf.Max(6, Mathf.RoundToInt(baseField * scale));
            }
        }
        return true;
    }

    public static void Pop(bool pushed)
    {
        if (!pushed || _depth == 0)
            return;
        if (--_depth == 0)
        {
            for (int i = 0; i < 3; i++)
            {
                Text.fontStyles[i].fontSize = Saved[i];
                Text.textFieldStyles[i].fontSize = SavedFields[i];
            }
        }
    }
}
