using System.Linq;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

public static class CyberpodUtility
{
    /// <summary>True when the pod has a powered, linked Netrun coprocessor.</summary>
    public static bool HasNetrunCoprocessor(Building_Cyberpod pod)
    {
        CompAffectedByFacilities facilities = pod?.GetComp<CompAffectedByFacilities>();
        if (facilities == null)
            return false;

        return facilities.LinkedFacilitiesListForReading
            .Any(f => f.def == NetrunDefOf.NR_NetrunCoprocessor && facilities.IsFacilityActive(f));
    }

    /// <summary>The pod holding this hacker, and whether a Netrun coprocessor boosts it.</summary>
    public static bool InCyberpod(Pawn hacker, out bool boosted)
    {
        boosted = false;
        if (!hacker.TryGetHoldingCyberpod(out Building_Cyberpod pod))
            return false;

        boosted = HasNetrunCoprocessor(pod);
        return true;
    }
}
