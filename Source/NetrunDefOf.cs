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
    public static ThingDef NR_Firewall;
    public static IncidentDef NR_CrewHelp;
    public static IncidentDef NR_CrewRaid;
    public static DefenseSettingsDef NR_Defense;

    // Research woven into the HE tree (Defs/Research.xml).
    public static ResearchProjectDef NR_Res_NetSecurity;
    public static ResearchProjectDef NR_Res_Neurorecording;
    public static ResearchProjectDef NR_Res_ActiveICE;
    public static ResearchProjectDef NR_Res_AIAwakening;
    public static ResearchProjectDef NR_Res_AIArchitecture;
    public static ResearchProjectDef NR_Res_BlackICEDefense;
    public static ResearchProjectDef NR_Res_ExecPrograms;

    [MayRequireBiotech]
    public static LearningDesireDef NR_Learning_Braindance;
    public static ThingDef NR_AICoreModule;
    public static ThingDef NR_AIServer;
    public static JobDef NR_AwakenAI;
    public static ThingDef NR_BraindanceChip;
    public static ThingDef NR_BraindanceGoggles;
    public static ThingDef NR_BraindanceRaw;
    public static ThingDef NR_BlackBraindanceChip;
    public static ThoughtDef NR_BraindanceGoodRecording;
    public static JobDef NR_EditBraindance;
    public static HediffDef NR_NeuralNoise;
    public static FactionDef NR_Mercenaries;
    public static IncidentDef NR_MercRaid;
    public static LetterDef NR_AIEventLetter;
    public static JobDef NR_AIEventSession;
    public static CyberspaceSettingsDef NR_Cyberspace;
    public static UniqueBraindanceSettingsDef NR_UniqueBraindance;
    public static IncidentDef NR_NetAttack_Breakdown;
    public static MapEventsSettingsDef NR_MapEvents;
    public static IncidentDef NR_MapHack;
    public static ThingDef NR_DistressServer;
    public static JobDef NR_ConnectDistressServer;
    public static ChemicalDef NR_Braindance;
    public static HediffDef NR_BraindanceTolerance;
    public static HediffDef NR_PredatorBoost;
    public static ThoughtDef NR_EchoPresence;
    public static HediffDef NR_AISocketInstalled;
    public static NetsickSettingsDef NR_NetsickSettings;
    public static HediffDef NR_Netsick;
    public static HediffDef NR_NeurostabilizerHigh;
    public static ThingDef NR_NetrunCoprocessor;
    public static ThingDef NR_AIServerRack;
    public static JobDef NR_UseNetTerminal;
    public static JobDef NR_DeliverToAI;
    public static RulePackDef NR_NamerFixer;

    // Hacking Expansion
    public static ThingDef USH_Cyberdata;
    public static ThingDef USH_AdvancedCyberdata;
}
