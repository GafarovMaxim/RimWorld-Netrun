"""Checks that every NR_ translation key used in Source/ exists in English and Russian Keyed files.

Keys built at runtime from a prefix ("NR_HeatLevel_" + level) are listed in DYNAMIC below.
Run from the mod root: python tools/check_keys.py
"""
import glob
import re
import sys
import xml.dom.minidom
import xml.etree.ElementTree as ET

LANGUAGES = ["English", "Russian (Русский)"]

# Def names that look like keys but are not.
NOT_KEYS = {"NR_Fixers", "NR_NamerFixer", "NR_Heat", "NR_AI", "NR_AISocket"}

HEAT = ["Shadow", "Noticed", "Tracked", "Target", "RedZone"]
PERSONALITIES = ["DataHunter", "Ghost", "Muse", "Predator", "Warden", "Oracle", "Forge", "Siren", "Trickster", "Doc", "Archivist", "Seed", "Echo"]
FIXER_TRAITS = ["Greedy", "Reliable", "Rat", "Paranoid", "Gambler", "Loyal", "Veteran", "Collector", "Hothead"]
AI_TRAITS = ['Curious', 'Loyal', 'Paranoid', 'Vain', 'Glitchy', 'Hoarder', 'Pacifist', 'NightOwl', 'Rebel', 'Nostalgic', 'Remembering']
SOUND_PACKS = ["Clean", "Minimal", "Neon", "RetroDeck", "Deep"]
CONTRACTS = ["DataDelivery", "SystemTap", "QuietRun", "Surveillance", "LayLow", "Rookie", "AIOrder", "CleanLab"]
DYNAMIC = {
    "NR_BlueprintNote_": ["1", "2", "3", "4"],
    "NR_TransitionWords_": [str(i) for i in range(12)],
    "NR_GlossTerms_": ["BlackIce", "Ice", "Heat", "Netsick", "Cyberdata", "Trace"],
    "NR_Gloss_": ["BlackIce", "Ice", "Heat", "Netsick", "Cyberdata", "Trace"],
    "NR_HintTitle_": ["Ice", "Netsick", "Fixer", "AIEvent"],
    "NR_Hint_": ["Ice", "Netsick", "Fixer", "AIEvent"],
    "NR_IntroTitle_": ["0", "1", "2"],
    "NR_Intro_": ["0", "1", "2"],
    "NR_Tour_": [str(i) for i in range(6)],
    # Glass Screen is commented out for now (2026-10-09); its setting keys stay for when it comes back.
    "NR_CustomGlass": ["", "Tip"],
    "NR_Legend_": ["Home", "Exit", "Fortress", "Guarded", "ContractNode", "GuardGate", "Visited", "Watched", "Patrol", "Offline", "Storm", "Transit",
                   "LinkFast", "LinkDark", "LinkWall", "LinkJammed", "LinkToll"],
    "NR_MapLensLow_": ["Loot", "Danger", "Time"],
    "NR_MapLens_": ["Overview", "Loot", "Danger", "Corps", "Contracts", "Time"],
    "NR_MapLensTip_": ["Overview", "Loot", "Danger", "Corps", "Contracts", "Time"],
    "NR_MapLensScale_": ["Overview", "Loot", "Danger", "Corps", "Contracts", "Time"],
    "NR_HelpAIHome_": ["Server", "Coprocessor", "Host"],
    "NR_HelpAIHomeService_": ["Server", "Coprocessor", "Host"],
    "NR_HelpAIHomeHunger_": ["Server", "Coprocessor", "Host"],
    "NR_HelpAIHomeGives_": ["Server", "Coprocessor", "Host"],
    "NR_HelpAwakenSrc_": ["Dive", "Memory", "WildAI", "Distress", "Market", "Hack"],
    "NR_HelpAwakenSrcDesc_": ["Dive", "Memory", "WildAI", "Distress", "Market", "Hack"],
    "NR_HelpTermNode_": ["Core", "Club", "Board", "Archive", "Server", "Studio", "Gateway"],
    "NR_HelpTermNodeDesc_": ["Core", "Club", "Board", "Archive", "Server", "Studio", "Gateway"],
    "NR_HelpChecklist_": ["Console", "Pod", "Link", "Hack", "Fixer", "Dive", "AI"],
    "NR_DeckKey_": ["0", "1", "2", "3", "4", "5"],
    "NR_DeckTheme_": ["Field", "Milspec", "Street", "OpsPad"],
    "NR_DeckThemeDesc_": ["Field", "Milspec", "Street", "OpsPad"],
    "NR_MenuPage_": ["Home", "Settings", "Achievements", "Difficulty", "Customization", "Continue", "Cyberpedia"],
    "NR_MenuDesc_": ["Continue", "Settings", "Cyberpedia", "Achievements", "Difficulty", "Customization"],
    "NR_CustomTab_": ["frame", "sound", "graphics"],
    "NR_HelpContractStage_": ["Board", "Taken", "Work", "Pay"],
    "NR_HelpContractStageDesc_": ["Board", "Taken", "Work", "Pay"],
    "NR_HelpBDTier_": ["0", "1", "2"],
    "NR_HelpIce_": ["Strike", "Shield", "Decoy", "Killer", "Hit"],
    "NR_HelpIceDesc_": ["Strike", "Shield", "Decoy", "Killer", "Hit"],
    "NR_HelpGwNote_": ["Market", "Archive", "Corp", "Abandoned", "DarkExchange", "CorpCore"],
    "NR_NewsFlavor_": [str(i) for i in range(32)],
    "NR_SoundPack_": SOUND_PACKS,
    "NR_SoundPackDesc_": SOUND_PACKS,
    "NR_AITrait_": AI_TRAITS,
    "NR_AITraitDesc_": AI_TRAITS,
    "NR_StudioTab_": ["Summary", "Raw", "Library", "Addiction"],
    "NR_UB_Author_": ["Concert", "ZeroRun", "Morton", "NightMarket", "Confession", "StreetRunner", "EchoLullaby", "Oracle"],
    "NR_UB_Good_": ["Concert", "ZeroRun", "Morton", "NightMarket", "Confession", "StreetRunner", "EchoLullaby", "Oracle"],
    "NR_UB_Bad_": ["Morton", "Confession", "Oracle"],
    "NR_HelpDiveStep_": ["LOBBY", "GATE", "FILES", "HIDDEN", "BLACKICE", "MEMORY", "EXIT"],
    "NR_HelpDiveStepDesc_": ["LOBBY", "GATE", "FILES", "HIDDEN", "BLACKICE", "MEMORY", "EXIT"],
    "NR_ArchiveTab_": ["Journal", "Reports", "Raids"],
    "NR_ArchiveFilter_": ["All", "Hack", "Dive", "Scrub", "AI", "Events"],
    "NR_ArchiveType_": ["Other", "Hack", "Dive", "Scrub", "AI", "Raid", "Event"],
    "NR_BoardFilter_": ["Any", "Easy", "Hard"],
    "NR_BoardDifficulty_": ["1", "2", "3"],
    "NR_HeatLevel_": HEAT,
    "NR_HeatLevelDesc_": HEAT,
    "NR_ExtNodeName_": ["Market", "Archive", "Corp", "Abandoned", "DarkExchange", "CorpCore"],
    "NR_ExtNodeType_": ["Market", "Archive", "Corp", "Abandoned", "DarkExchange", "CorpCore"],
    "NR_ExtNodeReward_": ["Market", "Archive", "Corp", "Abandoned", "DarkExchange", "CorpCore"],
    "NR_DiveElement_": ["Lobby", "Gate", "Ice", "Files", "Memory", "Exit"],
    "NR_DiveInfo_": ["Files", "Memory", "Lobby", "Exit"],
    "NR_DiveAct_": ["Move", "Back", "Scan", "Hack", "Download", "Exit"],
    "NR_DiveActTip_": ["Move", "Back", "Scan", "Hack", "Download", "Exit"],
    "NR_District_": ["Home", "Outskirts", "Transit", "Corp", "Market", "Ruins", "Campus", "Entertainment", "Dump", "Dark", "OldNet", "Orbit"],
    "NR_DistrictHint_": ["Home", "Outskirts", "Transit", "Corp", "Market", "Ruins", "Campus", "Entertainment", "Dump", "Dark", "OldNet", "Orbit"],
    "NR_NetSize_": ["Small", "Normal", "Large"],
    "NR_ScenIntro_": ["LastNode", "CorpRunaways", "PirateStudio", "FlyingNode"],  # textKey in Defs/Scenarios.xml
    "NR_ScenSummary_": ["LastNode", "CorpRunaways", "PirateStudio", "FlyingNode"],
    "NR_NetSizeTip_": ["Small", "Normal", "Large"],
    "NR_NetLayout_": ["Ring", "Spine", "Archipelago", "Metro"],
    "NR_ClubTab_": ["Fixers", "Corps", "Crews", "Top"],
    "NR_CorpStatus_": ["Calm", "Wary", "Hunting", "Lockdown", "Expanding", "Brink"],
    "NR_CrewFocus_": ["Corps", "Markets", "Contracts", "Hunters"],
    "NR_RankKind_": ["Solo", "Crew", "Colony"],
    "NR_Flavor_Spam_": [str(i) for i in range(1, 17)],
    "NR_Flavor_Obit_": [str(i) for i in range(1, 16)],
    "NR_Flavor_FixerAd_": [str(i) for i in range(1, 16)],
    "NR_Flavor_CorpPR_": [str(i) for i in range(1, 16)],
    "NR_Flavor_Wanted_": [str(i) for i in range(1, 7)],
    "NR_ChoiceTitle_": ["Phishing", "MapLeak", "FixerTip", "CrewChallenge", "SellOutCrew", "Intrusion", "TricksterJoke"],
    "NR_NodeEffect_": ["Overload", "Mirror", "Interference", "Auction", "OpenDoor", "Cache"],
    "NR_AIQ_Name_": ["BloodOfCorp", "AnotherName", "AnotherMemory", "Retaliation", "Drills", "Forecast", "MissingPart", "HeartToHeart", "ChoiceOfJoke", "Patient", "MissingVolume", "FirstStep", "WhoAmI", "HungryChannel", "QuietHour", "ForeignCode"],
    "NR_AIQ_Desc_": ["BloodOfCorp", "AnotherName", "AnotherMemory", "Retaliation", "Drills", "Forecast", "MissingPart", "HeartToHeart", "ChoiceOfJoke", "Patient", "MissingVolume", "FirstStep", "WhoAmI", "HungryChannel", "QuietHour", "ForeignCode"],
    "NR_AIQ_": ["BloodOfCorp_Step1", "BloodOfCorp_Step2", "AnotherName_Step1", "AnotherName_Step2", "AnotherMemory_Step1", "AnotherMemory_Step2", "AnotherMemory_Step3", "Retaliation_Step1", "Retaliation_Step2", "Drills_Step1", "Drills_Step2", "Forecast_Step1", "MissingPart_Step1", "MissingPart_Step2", "HeartToHeart_Step1", "HeartToHeart_Step2", "ChoiceOfJoke_Step1", "ChoiceOfJoke_Step2", "Patient_Step1", "Patient_Step2", "MissingVolume_Step1", "FirstStep_Step1", "FirstStep_Step2", "WhoAmI_Step1", "WhoAmI_Step2", "HungryChannel_Step1", "QuietHour_Step1", "ForeignCode_Step1"],
    "NR_AI2_": ["DataHunter", "Ghost", "Muse", "Predator", "Warden", "Oracle", "Forge", "Siren", "Trickster", "Doc", "Archivist", "Seed", "Echo"],
    "NR_AI2Desc_": ["DataHunter", "Ghost", "Muse", "Predator", "Warden", "Oracle", "Forge", "Siren", "Trickster", "Doc", "Archivist", "Seed", "Echo"],
    "NR_AIQ_Level_": ["2", "3"],
    "NR_IceTier_": ["None", "Firewall", "Ice", "BlackIce"],
    "NR_Program_": ["Sword", "Shield", "Decoy", "Stealth", "Killer", "IceBreaker", "Exec"],
    "NR_ProgramDesc_": ["Sword", "Shield", "Decoy", "Stealth", "Killer", "IceBreaker", "Exec"],
    "NR_GatewayRisk_": ["0", "1", "2"],
    "NR_Daemon_": ["Blade", "Aegis", "Wraith", "Hound", "Lens"],
    "NR_DaemonDesc_": ["Blade", "Aegis", "Wraith", "Hound", "Lens"],
    "NR_Celebrity_": ["0", "1", "2", "3", "4", "5", "6", "7"],
    "NR_Node_": ["Core", "Club", "Board", "Archive", "Server", "Studio", "Gateway"],
    "NR_NodeTip_": ["Core", "Club", "Board", "Archive", "Server", "Studio", "Gateway"],
    "NR_FixerSpecialty_": ["Broker", "Cleaner", "Fence", "Imperial"],
    "NR_FixerSpecialtyDesc_": ["Broker", "Cleaner", "Fence", "Imperial"],
    "NR_FixerTrait_": FIXER_TRAITS,
    "NR_FixerTraitDesc_": FIXER_TRAITS,
    "NR_Contract_": CONTRACTS,
    "NR_ContractDesc_": CONTRACTS,
    "NR_AIPersonality_": PERSONALITIES,
    "NR_AIPersonalityDesc_": PERSONALITIES,
    "NR_AIService_": PERSONALITIES,
    "NR_AIServiceDesc_": PERSONALITIES,
    "NR_AIFoodSpecial_": ["Ghost", "Trickster", "Seed"],
    "NR_BDMood_": ["Bright", "Dark", "Combat"],
    "NR_BDQuality_": ["0", "1", "2"],
    "NR_SettingsAccent_": ["0", "1", "2", "3", "4"],
    "NR_HelpCtx_": ["Core", "Onboarding", "Heat", "Club", "Board", "Archive", "Server", "Studio", "Gateway", "Dive", "Settings", "DiveReport", "Help", "Menu", "MenuSettings", "MenuAchievements", "MenuDifficulty", "MenuCustomization"],
    "NR_SettingsDifficultyName_": ["Easy", "Normal", "Hard", "Cyberpunk"],
    "NR_SettingsDifficulty_": ["Easy", "Normal", "Hard", "Custom", "Cyberpunk"],
    "NR_AdviceButton_": ["Scrub", "Club", "Board", "Server", "Studio", "Gateway", "HeatScreen"],
    "NR_NodeLocked_": ["Club", "Board", "Archive", "Server", "Studio", "Gateway"],
    "NR_OnboardingStep_": ["Console", "Cyberpod", "Cyberlink", "FirstHack"],
    "NR_OnboardingDone_": ["Console", "Cyberpod", "Cyberlink", "FirstHack"],
    "NR_SettingsFreqAttacks": ["Tip"],
    "NR_SettingsFreqMap": ["Tip"],
    "NR_SettingsFreqCyber": ["Tip"],
    "NR_SettingsAIEventFrequency": ["Tip"],
}


def main() -> int:
    for path in glob.glob("**/*.xml", recursive=True):
        xml.dom.minidom.parse(path)

    used = set()
    for path in glob.glob("Source/**/*.cs", recursive=True):
        with open(path, encoding="utf-8") as f:
            used |= set(re.findall(r'"(NR_[A-Za-z0-9_]+)"', f.read()))
    # Strings that name our own defs (DefDatabase lookups) are not translation keys.
    def_names = set()
    for path in glob.glob("Defs/**/*.xml", recursive=True):
        def_names |= {e.text for e in ET.parse(path).iter("defName") if e.text}

    used = {k for k in used if not k.endswith("_") and k not in NOT_KEYS and k not in def_names}
    for prefix, suffixes in DYNAMIC.items():
        used |= {prefix + s for s in suffixes}

    ok = True
    for lang in LANGUAGES:
        keys = set()
        for path in glob.glob(f"Languages/{lang}/Keyed/*.xml"):
            keys |= {e.tag for e in ET.parse(path).getroot()}
        missing, unused = sorted(used - keys), sorted(keys - used)
        print(f"{lang}: missing {missing or 'none'}, unused {unused or 'none'}")
        ok &= not missing
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
