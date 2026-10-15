using UnityEngine;

namespace Netrun;

public static partial class NetrunUI
{
    private static System.Func<Vector2, Vector2> _unclip;

    /// <summary>
    /// Rotates the next GUI draws by <paramref name="angle"/> degrees around <paramref name="pivot"/> (local GUI
    /// coordinates). Unity's RotateAroundPivot (and Verse's UI.RotateAroundPivot) builds the rotation in screen space
    /// without the current GUI.matrix, so under the UI scale or the terminal's content scale the pivot lands elsewhere
    /// and the rotated sprite flies off. This one rotates in the unclipped space, before the current matrix.
    /// Restore GUI.matrix afterwards as usual.
    /// </summary>
    public static void RotateAround(float angle, Vector2 pivot)
    {
        _unclip ??= BuildUnclip();
        // Unclip as Unity's own pivot helpers do it: with an identity matrix, so the pivot comes out in the group
        // space the current matrix is applied to.
        Matrix4x4 m = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        Vector2 u = _unclip(pivot);
        GUI.matrix = m
                     * Matrix4x4.TRS(new Vector3(u.x, u.y, 0f), Quaternion.Euler(0f, 0f, angle), Vector3.one)
                     * Matrix4x4.TRS(new Vector3(-u.x, -u.y, 0f), Quaternion.identity, Vector3.one);
    }

    private static System.Func<Vector2, Vector2> BuildUnclip()
    {
        try
        {
            var method = HarmonyLib.AccessTools.Method("UnityEngine.GUIClip:Unclip", [typeof(Vector2)]);
            if (method != null)
                return (System.Func<Vector2, Vector2>)System.Delegate.CreateDelegate(typeof(System.Func<Vector2, Vector2>), method);
        }
        catch (System.Exception e)
        {
            Verse.Log.Warning("[Netrun] GUIClip.Unclip not found, rotations ignore groups: " + e.Message);
        }
        return p => p;
    }
}
