using RimWorld;
using UnityEngine;
using USH_HE;
using Verse;

namespace Netrun;

public static class HackHeatUtility
{
    private const float DefencePerHeat = 1000f;
    private const float MinHackHeat = 2f;
    private const float MaxHackHeat = 15f;

    // Vanilla HackingStealth base value; higher stealth means a smaller trace.
    private const float BaseStealth = 100f;
    private const float MinStealthFactor = 0.25f;
    private const float MaxStealthFactor = 2f;

    private const float CyberpodHeatFactor = 0.6f;

    public static float HeatForHack(CompHackable comp, Pawn hacker)
    {
        float heat = Mathf.Clamp(comp.defence / DefencePerHeat, MinHackHeat, MaxHackHeat);

        float stealth = Mathf.Max(1f, hacker.GetStatValue(StatDefOf.HackingStealth));
        heat *= Mathf.Clamp(BaseStealth / stealth, MinStealthFactor, MaxStealthFactor);

        if (hacker.TryGetHoldingCyberpod(out _))
            heat *= CyberpodHeatFactor;

        return heat;
    }
}
