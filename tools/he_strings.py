"""Hacking Expansion strings for our Russian translation of it (Maxim, 2026-10-22).

Lists every translatable string of HE (def fields with their DefInjected paths, and Keyed) and which of them our
`Languages/Russian (Русский)` does not have yet. Run after an HE update to see what needs translating:

    python tools/he_strings.py            # missing ones
    python tools/he_strings.py --all      # everything, as "DefType/key = English"
    python tools/he_strings.py --skeleton OUT_DIR   # English skeleton files to translate

HE folder: the Steam workshop copy (RIMWORLD_WORKSHOP overrides the default).
"""
import os
import sys
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WORKSHOP = os.environ.get("RIMWORLD_WORKSHOP", r"E:\SteamLibrary\steamapps\workshop\content\294100")
HE = os.path.join(WORKSHOP, "3573344880", "1.6")
RU = os.path.join(ROOT, "Languages", "Russian (Русский)")

# Def fields the game translates through DefInjected.
FIELDS = {
    "label", "description", "labelNoun", "labelShort", "labelPlural", "useLabel", "jobString", "reportString",
    "verb", "gerund", "letterLabel", "letterText", "ingestCommandString", "ingestReportString", "pawnLabel",
    "deathMessage", "customLabel", "labelSocial", "text", "baseInspectLine", "skillLabel", "labelTendedWell",
    "labelTendedWellInner", "labelSolidTendedWell", "labelTended", "labelTendedInner", "destroyedLabel",
    "destroyedOutLabel", "oldLabel", "formatString", "formatStringUnfinalized", "successMessage",
    "outcomeLabel", "letterInfoText", "extraTooltip", "confirmationDialogText", "tooltip",
}


def texts(elem, path, out):
    """Translatable leaves under elem as {path: text}; list items get their index."""
    lis = [c for c in elem if c.tag == "li"]
    if lis:
        for i, li in enumerate(lis):
            texts(li, path + [handle(li, path, i)], out)
        return
    for child in elem:
        if len(child) == 0:
            if child.tag in FIELDS and (child.text or "").strip():
                out[".".join(path + [child.tag])] = child.text.strip()
        else:
            texts(child, path + [child.tag], out)


def handle(li, path, i):
    """List items are found by index, but comps by their comp class (comps.CompUsableImplant.useLabel), like the
    game's own translations: indices shift when a parent def adds comps."""
    if path and path[-1] == "comps":
        if li.find("compClass") is not None:
            return li.find("compClass").text.strip().split(".")[-1]
        cls = (li.get("Class") or "").split(".")[-1]
        if cls.startswith("HediffCompProperties_"):
            return "HediffComp_" + cls[len("HediffCompProperties_"):]
        if cls.startswith("CompProperties_"):
            return "Comp" + cls[len("CompProperties_"):]
    return str(i)


def load_defs():
    named, concrete = {}, []
    for dirpath, _, files in os.walk(os.path.join(HE, "Defs")):
        for f in files:
            if not f.endswith(".xml"):
                continue
            for d in ET.parse(os.path.join(dirpath, f)).getroot():
                if not isinstance(d.tag, str):
                    continue
                if d.get("Name"):
                    named[d.get("Name")] = d
                if d.get("Abstract", "").lower() != "true" and d.find("defName") is not None:
                    concrete.append(d)
    return named, concrete


def resolved(d, named):
    """Own strings over the parent's (whole sublists are replaced, not merged: close enough for text)."""
    out = {}
    parent = named.get(d.get("ParentName") or "")
    if parent is not None:
        out.update(resolved(parent, named))
    own = {}
    texts(d, [], own)
    nested = {k.split(".")[0] for k in own if "." in k}
    out = {k: v for k, v in out.items() if k.split(".")[0] not in nested}
    out.update(own)
    return out


def he_strings():
    named, concrete = load_defs()
    result = {}
    for d in concrete:
        def_type = d.tag  # DefInjected folders use the full type name (USH_HE.HacksetDef)
        name = d.find("defName").text.strip()
        for key, text in resolved(d, named).items():
            result.setdefault(def_type, []).append((name + "." + key, text))
    keyed = []
    for f in os.listdir(os.path.join(HE, "Languages", "English", "Keyed")):
        for e in ET.parse(os.path.join(HE, "Languages", "English", "Keyed", f)).getroot():
            if isinstance(e.tag, str):
                keyed.append((e.tag, (e.text or "").strip()))
    return result, keyed


def ours():
    have = set()
    for dirpath, _, files in os.walk(RU):
        for f in files:
            if f.endswith(".xml"):
                for e in ET.parse(os.path.join(dirpath, f)).getroot():
                    if isinstance(e.tag, str):
                        have.add(e.tag)
    return have


def main():
    defs, keyed = he_strings()
    if "--skeleton" in sys.argv:
        out = sys.argv[sys.argv.index("--skeleton") + 1]
        from xml.sax.saxutils import escape
        for def_type, rows in defs.items():
            os.makedirs(os.path.join(out, "DefInjected", def_type), exist_ok=True)
            with open(os.path.join(out, "DefInjected", def_type, "HE.xml"), "w", encoding="utf-8") as fh:
                fh.write('<?xml version="1.0" encoding="utf-8"?>\n<LanguageData>\n')
                for k, v in rows:
                    fh.write(f"  <{k}>{escape(v)}</{k}>\n")
                fh.write("</LanguageData>\n")
        os.makedirs(os.path.join(out, "Keyed"), exist_ok=True)
        with open(os.path.join(out, "Keyed", "HE.xml"), "w", encoding="utf-8") as fh:
            fh.write('<?xml version="1.0" encoding="utf-8"?>\n<LanguageData>\n')
            for k, v in keyed:
                fh.write(f"  <{k}>{escape(v)}</{k}>\n")
            fh.write("</LanguageData>\n")
        print("skeleton written to", out)
        return
    have = ours()
    show_all = "--all" in sys.argv
    missing = 0
    for def_type, rows in sorted(defs.items()):
        for k, v in rows:
            if show_all or k not in have:
                missing += k not in have
                print(f"{def_type}/{k} = {v}")
    for k, v in keyed:
        if show_all or k not in have:
            missing += k not in have
            print(f"Keyed/{k} = {v}")
    total = sum(len(r) for r in defs.values()) + len(keyed)
    print(f"HE strings: {total}, not translated: {missing}")


if __name__ == "__main__":
    main()
