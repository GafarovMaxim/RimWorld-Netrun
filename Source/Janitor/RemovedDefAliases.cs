using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace Netrun;

/// <summary>
/// A save reference to a def Netrun removed (child mid-lesson, T1.9) loads as a vanilla def instead of a red
/// "Could not load reference" error. Add a line here with the defName in <see cref="NetrunJanitor.KnownRemovedDefs"/>.
/// </summary>
[HarmonyPatch]
public static class RemovedDefAliases
{
    private static readonly Dictionary<(string Type, string Name), string> Map = new()
    {
        [("JobDef", "NR_BraindanceLesson")] = "Wait",
        [("LearningDesireDef", "NR_Learning_Braindance")] = "Reading",
    };

    private static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(typeof(BackCompatibility), "BackCompatibleDefName");

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix(object[] __args, ref string __result)
    {
        try
        {
            if (__args.Length > 1 && __args[0] is Type type && __args[1] is string name && Map.TryGetValue((type.Name, name), out string alias))
                __result = alias;
        }
        catch (Exception e)
        {
            Log.Warning("[Netrun] removed def alias failed: " + e.Message);
        }
    }
}
