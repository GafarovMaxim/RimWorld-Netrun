using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// Royalty: imperial clients come through the fixer pool. Their contracts pay less silver and add royal favor to the
/// colonist the Empire knows best. They only deal with a colony at peace with the Empire.
/// </summary>
public static class ImperialClients
{
    private static FixerSettingsDef Settings => NetrunDefOf.NR_Fixers;

    public static bool EmpireFriendly
        => ModsConfig.RoyaltyActive && Faction.OfEmpire is { } empire && !empire.defeated && !empire.HostileTo(Faction.OfPlayer);

    /// <summary>A random specialty for a newcomer; an imperial client only when the Empire would send one.</summary>
    public static FixerSpecialty RandomSpecialty()
    {
        if (EmpireFriendly && Rand.Chance(Settings.imperialChance))
            return FixerSpecialty.Imperial;
        return (FixerSpecialty)Rand.RangeInclusive(0, (int)FixerSpecialty.Fence);
    }

    /// <summary>Favor for a reward in silver (the silver itself is cut to imperialSilverFactor).</summary>
    public static int FavorFor(int silver)
        => UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(silver / Settings.imperialSilverPerFavor), 1, Settings.imperialMaxFavor);

    /// <summary>The colonist who gets the favor: the one with the most already, else any free colonist.</summary>
    public static Pawn FavorReceiver()
    {
        Faction empire = Faction.OfEmpire;
        if (empire == null)
            return null;
        return PawnsFinder.AllMaps_FreeColonists.Where(p => p.royalty != null && !p.IsQuestLodger())
            .OrderByDescending(p => p.royalty.GetFavor(empire)).FirstOrDefault();
    }

    public static void GiveFavor(int favor, string fixerName)
    {
        if (favor <= 0 || FavorReceiver() is not { } pawn)
            return;
        pawn.royalty.GainFavor(Faction.OfEmpire, favor);
        Messages.Message("NR_ImperialFavor".Translate(fixerName, pawn.LabelShort, favor), pawn, MessageTypeDefOf.PositiveEvent, historical: true);
    }
}
