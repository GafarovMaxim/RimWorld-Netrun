using RimWorld;
using Verse;

namespace Netrun;

[DefOf]
public static class NetrunDefOf
{
    static NetrunDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(NetrunDefOf));

    public static HeatSettingsDef NR_Heat;
    public static FixerSettingsDef NR_Fixers;
    public static AISettingsDef NR_AI;
    public static ThingDef NR_AIFragment;
    public static ThingDef NR_AICoreModule;
    public static ThingDef NR_AIServer;
    public static JobDef NR_AwakenAI;
    public static ThingDef NR_BraindanceChip;
    public static HediffDef NR_PredatorBoost;
    public static ThoughtDef NR_EchoPresence;
    public static HediffDef NR_AISocketInstalled;
    public static NetsickSettingsDef NR_NetsickSettings;
    public static HediffDef NR_Netsick;
    public static ThingDef NR_NetrunCoprocessor;
    public static ThingDef NR_AIServerRack;
    public static JobDef NR_UseNetTerminal;
    public static JobDef NR_DeliverToAI;
    public static RulePackDef NR_NamerFixer;

    // Hacking Expansion
    public static ThingDef USH_Cyberdata;
    public static ThingDef USH_AdvancedCyberdata;
}
