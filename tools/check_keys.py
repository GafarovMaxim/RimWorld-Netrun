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
PERSONALITIES = ["DataHunter", "Ghost", "Muse", "Predator", "Warden", "Oracle", "Forge", "Siren", "Trickster", "Echo"]
DYNAMIC = {
    "NR_HeatLevel_": HEAT,
    "NR_HeatLevelDesc_": HEAT,
    "NR_TerminalTab_": ["Overview", "Fixers", "Contracts", "AI", "Braindance", "Journal"],
    "NR_FixerSpecialty_": ["Broker", "Cleaner", "Fence"],
    "NR_FixerSpecialtyDesc_": ["Broker", "Cleaner", "Fence"],
    "NR_FixerTrait_": ["Greedy", "Reliable", "Rat"],
    "NR_FixerTraitDesc_": ["Greedy", "Reliable", "Rat"],
    "NR_Contract_": ["DataDelivery", "SystemTap", "QuietRun"],
    "NR_ContractDesc_": ["DataDelivery", "SystemTap", "QuietRun"],
    "NR_AIPersonality_": PERSONALITIES,
    "NR_AIPersonalityDesc_": PERSONALITIES,
    "NR_AIService_": PERSONALITIES,
    "NR_AIServiceDesc_": PERSONALITIES,
    "NR_AIFoodSpecial_": ["Ghost", "Trickster"],
}


def main() -> int:
    for path in glob.glob("**/*.xml", recursive=True):
        xml.dom.minidom.parse(path)

    used = set()
    for path in glob.glob("Source/**/*.cs", recursive=True):
        with open(path, encoding="utf-8") as f:
            used |= set(re.findall(r'"(NR_[A-Za-z_]+)"', f.read()))
    used = {k for k in used if not k.endswith("_") and k not in NOT_KEYS and not k.startswith("NR_Netrun")}
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
