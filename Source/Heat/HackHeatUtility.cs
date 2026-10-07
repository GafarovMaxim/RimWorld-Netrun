using RimWorld;
using UnityEngine;
using USH_HE;
using Verse;

namespace Netrun;

public static class HackHeatUtility
{
    private static HeatSettingsDef Settings => NetrunDefOf.NR_Heat;

    /// <summary>The trace of a hack by a hacker with ordinary stealth, outside a pod (before the global multiplier).</summary>
    public static float BaseHeat(CompHackable comp) => Settings.hackHeat.ClampToRange(comp.defence / Settings.defencePerHeat);

    public static float HeatForHack(CompHackable comp, Pawn hacker)
    {
        float heat = Settings.hackHeat.ClampToRange(comp.defence / Settings.defencePerHeat);

        float stealth = Mathf.Max(1f, hacker.GetStatValue(StatDefOf.HackingStealth));
        heat *= Settings.stealthFactor.ClampToRange(Settings.baseStealth / stealth);

        if (CyberpodUtility.InCyberpod(hacker, out NetAI coprocessorAI))
            heat *= Settings.cyberpodHeatFactor * CyberpodUtility.HeatFactor(coprocessorAI);

        // Biotech: a hacked mech leaves less trace when the colony has a mechanitor ("our own protocols").
        if (ModsConfig.BiotechActive && comp.parent is Pawn { RaceProps.IsMechanoid: true } && ColonyHasMechanitor(hacker.MapHeld))
            heat *= NetrunDefOf.NR_Cyberspace.pawnNet.mechHackTraceFactor;

        return heat;
    }

    private static bool ColonyHasMechanitor(Map map)
    {
        if (map == null)
            return false;
        foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
            if (p.mechanitor != null)
                return true;
        return false;
    }
}
