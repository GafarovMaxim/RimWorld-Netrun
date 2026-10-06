using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// Hacking Expansion hard-codes 5000 ticks (2 hours) to wake a pawn up in a cyberpod.
/// Swap that constant for our own number (NR_Cyberspace.podWakeUpTicks) in the tick, the progress bar and the inspect text.
/// </summary>
[HarmonyPatch]
public static class Patch_Cyberpod_WakeUp
{
    private const int HeWakeUpTicks = 5000;

    public static int WakeUpTicks => NetrunDefOf.NR_Cyberspace?.podWakeUpTicks ?? HeWakeUpTicks;
    public static float WakeUpTicksF => WakeUpTicks;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "WakingUpTick", "GetWakingUpText", "SustainWakingUpProgressBar" })
        {
            MethodInfo method = AccessTools.Method(typeof(Building_Cyberpod), name);
            if (method != null)
                yield return method;
        }
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction code in instructions)
        {
            if (code.opcode == OpCodes.Ldc_I4 && code.operand is int i && i == HeWakeUpTicks)
                yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Patch_Cyberpod_WakeUp), nameof(WakeUpTicks))).MoveLabelsFrom(code);
            else if (code.opcode == OpCodes.Ldc_R4 && code.operand is float f && f == HeWakeUpTicks)
                yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Patch_Cyberpod_WakeUp), nameof(WakeUpTicksF))).MoveLabelsFrom(code);
            else
                yield return code;
        }
    }
}
