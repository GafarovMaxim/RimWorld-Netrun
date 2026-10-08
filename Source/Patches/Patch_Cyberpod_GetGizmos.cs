using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>Adds the network terminal button to an occupied Hacking Expansion cyberpod.</summary>
[HarmonyPatch(typeof(Building_Cyberpod), nameof(Building_Cyberpod.GetGizmos))]
public static class Patch_Cyberpod_GetGizmos
{
    static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Building_Cyberpod __instance)
    {
        string wake = "USH_HE_CommandStartWaking".Translate();
        foreach (Gizmo gizmo in gizmos)
        {
            // Waking a pawn who is still out in the net: warn that the run should be ended first.
            if (gizmo is Command_Action wakeUp && wakeUp.defaultLabel == wake && __instance.ContainedThing is Pawn runner && NetMapManager.InRun(runner))
            {
                System.Action original = wakeUp.action;
                wakeUp.action = () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "NR_WakeMidRunConfirm".Translate(runner.LabelShort), original, destructive: true));
                wakeUp.defaultDesc += "\n\n" + "NR_WakeMidRunTip".Translate().Colorize(ColorLibrary.RedReadable);
            }
            yield return gizmo;
        }

        Gizmo terminal = null;
        try
        {
            if (__instance.Faction == Faction.OfPlayer && __instance.ContainedThing is Pawn)
            {
                CompPowerTrader power = __instance.GetComp<CompPowerTrader>();
                terminal = NetTerminalUtility.MakeGizmo(__instance.Map, power == null || power.PowerOn);
            }
        }
        catch (Exception ex)
        {
            Log.ErrorOnce("[Netrun] Failed to add the terminal button to the cyberpod: " + ex, 0x4E520002);
        }

        if (terminal != null)
            yield return terminal;

        Gizmo duty = null;
        try
        {
            if (__instance.Faction == Faction.OfPlayer && NetrunDefOf.NR_Res_ActiveICE?.IsFinished == true && NodeDefense.Get() is { } defense)
            {
                duty = new Command_Toggle
                {
                    defaultLabel = "NR_DutyGizmo".Translate(),
                    defaultDesc = "NR_DutyGizmoDesc".Translate(NetrunDefOf.NR_Defense.dutyIntellectPerPoint),
                    icon = NetrunTex.DaemonIcon(DiveDaemon.Aegis),
                    isActive = () => defense.OnDuty(__instance),
                    toggleAction = () => defense.ToggleDuty(__instance),
                };
            }
        }
        catch (Exception ex)
        {
            Log.ErrorOnce("[Netrun] Failed to add the node watch button to the cyberpod: " + ex, 0x4E520003);
        }

        if (duty != null)
            yield return duty;
    }
}
