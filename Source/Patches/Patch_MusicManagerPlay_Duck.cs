using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The game's music fades down while the terminal is open, so it does not drown the terminal ambient, and comes back
/// smoothly when it closes (Maxim, 2026-10-15). Scales the music manager's own volume, once a frame.
/// </summary>
[HarmonyPatch(typeof(MusicManagerPlay), "CurVolume", MethodType.Getter)]
public static class Patch_MusicManagerPlay_Duck
{
    private static float _duck = 1f;
    private static int _frame = -1;

    static void Postfix(ref float __result)
    {
        try
        {
            if (_frame != Time.frameCount)
            {
                _frame = Time.frameCount;
                float target = NetrunSound.TerminalShown ? NetrunMod.Settings.TerminalMusic : 1f;
                _duck = Mathf.MoveTowards(_duck, target, Time.unscaledDeltaTime * 0.7f);
            }
            __result *= _duck;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Music fade failed: " + e.Message, 0x4E52_4D55);
        }
    }
}
