using RimWorld;
using Verse;

namespace Netrun;

[DefOf]
public static class NetrunDefOf
{
    static NetrunDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(NetrunDefOf));

    public static FixerSettingsDef NR_Fixers;
    public static RulePackDef NR_NamerFixer;

    // Hacking Expansion
    public static ThingDef USH_Cyberdata;
    public static ThingDef USH_AdvancedCyberdata;
}
