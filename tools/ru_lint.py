"""Checks the Russian texts against the English ones and docs/GLOSSARY_RU.md.

Errors (exit code 1): placeholder mismatch with English, a banned term variant from the glossary.
Warnings (with --warn): stale translation (English changed since the Russian was last checked), text identical
to English, Latin words outside LATIN_OK, \\n count mismatch, typography, pawn past tense without a gender form.

Run from the mod root:
  python tools/ru_lint.py            errors and a summary
  python tools/ru_lint.py --warn     also every warning
  python tools/ru_lint.py --update   after updating Russian texts: record the current English as the checked source
"""
import glob
import hashlib
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

EN_DIR = "Languages/English"
RU_DIR = "Languages/Russian (Русский)"
GLOSSARY = "docs/GLOSSARY_RU.md"
SOURCES = "tools/ru_sources.json"

LATIN_OK = {
    "ICE", "Black", "Heat", "Netsick", "HE", "BD", "AI", "Hacking", "Expansion", "Netrun", "RimWorld", "Steam",
    "Arasaka", "Militech", "Biotechnica", "Kang", "Tao", "Zetatech", "Petrochem", "Trauma", "Team",
    "Esc", "Tab", "Shift", "Ctrl", "Alt", "GitHub", "Tektur", "LOBBY", "GATE", "FILES", "HIDDEN", "BLACKICE", "MEMORY", "EXIT", "OK", "ID", "LCD", "PC", "DNA", "RAM", "CPU",
}
# Past-tense look-alikes that are not verbs.
NOT_VERBS = {"сигнал", "канал", "материал", "капитал", "финал", "журнал", "терминал", "интервал", "ритуал", "оригинал",
             "персонал", "арсенал", "потенциал", "минимал", "профил", "мобил", "крокодил", "фитил", "автомобил"}

PLACEHOLDER = re.compile(r"\{([A-Za-z0-9]+)")
BRACES = re.compile(r"\{[^{}]*\}|<[^<>]*>")
LATIN = re.compile(r"(?<![A-Za-z_])[A-Za-z][A-Za-z']{2,}")
PAST = re.compile(r"(?<![а-яё])([а-яё]+(?:ал|ил|ел|ыл|ял|ул))(?![а-яё])", re.I)


def keyed(lang_dir):
    out = {}
    for path in glob.glob(os.path.join(lang_dir, "Keyed", "*.xml")):
        for el in ET.parse(path).getroot():
            if isinstance(el.tag, str):
                out[el.tag] = (el.text or "", os.path.basename(path))
    return out


def def_injected(lang_dir):
    out = {}
    for path in glob.glob(os.path.join(lang_dir, "DefInjected", "*", "*.xml")):
        rel = os.path.relpath(path, lang_dir)
        for el in ET.parse(path).getroot():
            if not isinstance(el.tag, str):
                continue
            if len(el):
                out[rel + ":" + el.tag] = "\n".join(li.text or "" for li in el)
            else:
                out[rel + ":" + el.tag] = el.text or ""
    return out


def banned_terms():
    """Column 3 of the glossary table: comma-separated banned variants, each with the term it replaces."""
    rules = []
    for line in open(GLOSSARY, encoding="utf-8"):
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) != 3 or cells[0] in ("English", "---") or not cells[2]:
            continue
        for word in (w.strip() for w in cells[2].split(",")):
            if not word:
                continue
            # Short words match exactly; longer ones match any ending (киберкапсулу, кибердаты).
            stem = re.escape(word if len(word) <= 5 else word[:-1])
            tail = "" if len(word) <= 5 else r"[\w-]*"
            rules.append((re.compile(r"(?<![\w-])" + stem + tail + r"(?![\w-])", re.I), word, cells[1]))
    return rules


def bases(text):
    return {p.split("_")[0] for p in PLACEHOLDER.findall(text)}


def digest(text):
    return hashlib.sha1(text.encode("utf-8")).hexdigest()[:8]


def main():
    show_warn = "--warn" in sys.argv
    en, ru = keyed(EN_DIR), keyed(RU_DIR)
    texts = {k: v for k, (v, _) in ru.items()}
    texts.update(def_injected(RU_DIR))
    errors, warns = [], []

    for key, (en_text, _) in en.items():
        if key not in ru:
            continue
        ru_text = ru[key][0]
        if bases(en_text) != bases(ru_text):
            errors.append(f"{key}: placeholders EN {sorted(bases(en_text))} RU {sorted(bases(ru_text))}")
        if en_text.count("\\n") != ru_text.count("\\n"):
            warns.append(f"{key}: \\n EN {en_text.count(chr(92) + 'n')} RU {ru_text.count(chr(92) + 'n')}")
        plain = BRACES.sub("", en_text)
        if en_text == ru_text and len(re.findall(r"[A-Za-z]", plain)) >= 4 and not set(LATIN.findall(plain)) <= LATIN_OK:
            warns.append(f"{key}: same as English: {en_text[:60]}")

    sources = json.load(open(SOURCES, encoding="utf-8")) if os.path.exists(SOURCES) else {}
    if "--update" in sys.argv:
        json.dump({k: digest(en[k][0]) for k in sorted(en) if k in ru}, open(SOURCES, "w", encoding="utf-8"), indent=0)
        print("recorded", sum(1 for k in en if k in ru), "English sources")
        return 0
    stale = [k for k in en if k in ru and k in sources and sources[k] != digest(en[k][0])]
    warns += [f"{k}: English changed, check the Russian: {en[k][0][:60]}" for k in stale]

    rules = banned_terms()
    for key, text in texts.items():
        plain = BRACES.sub("§", text).replace("\n", " ")
        for rx, word, right in rules:
            if rx.search(plain):
                errors.append(f"{key}: «{word}» → {right}")
        latin = [w for w in LATIN.findall(plain) if w not in LATIN_OK and w.upper() != w]
        if latin and not key.startswith("USH_"):
            warns.append(f"{key}: Latin {sorted(set(latin))[:5]}")
        if '"' in plain:
            warns.append(f"{key}: straight quotes, use «»")
        if " - " in plain or "--" in plain:
            warns.append(f"{key}: hyphen as a dash, use —")
        if "  " in plain:
            warns.append(f"{key}: double space")
        if "PAWN" in text and "_gender" not in text:
            verbs = [v for v in PAST.findall(plain) if v.lower() not in NOT_VERBS]
            if verbs:
                warns.append(f"{key}: pawn past tense without gender: {verbs[:3]}")

    for e in errors:
        print("ERROR", e)
    if show_warn:
        for w in warns:
            print("warn ", w)
    print(f"ru_lint: {len(errors)} errors, {len(warns)} warnings ({len(stale)} stale); --warn to list warnings")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
