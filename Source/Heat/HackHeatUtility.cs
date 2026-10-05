using RimWorld;
using UnityEngine;
using USH_HE;
using Verse;

namespace Netrun;

public static class HackHeatUtility
{
    private static HeatSettingsDef Settings => NetrunDefOf.NR_Heat;

    public static float HeatForHack(CompHackable comp, Pawn hacker)
    {
        float heat = Settings.hackHeat.ClampToRange(comp.defence / Settings.defencePerHeat);

        float stealth = Mathf.Max(1f, hacker.GetStatValue(StatDefOf.HackingStealth));
        heat *= Settings.stealthFactor.ClampToRange(Settings.baseStealth / stealth);

        if (CyberpodUtility.InCyberpod(hacker, out NetAI coprocessorAI))
            heat *= Settings.cyberpodHeatFactor * CyberpodUtility.HeatFactor(coprocessorAI);

        return heat;
    }
}
