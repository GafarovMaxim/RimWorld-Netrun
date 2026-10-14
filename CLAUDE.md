# Hacking Expansion: Netrun: notes for Claude sessions

RimWorld 1.6 mod, a cyberpunk layer on top of Ushankas Hacking Expansion (HE). Owner: Maxim (GafarovMaxim). Talk to him in Russian.

**Mandatory optimization rules: `docs/TECH_DETAILS.md`, follow them on every code change; beyond the rules, always look for ways to optimize the mod yourself** (Maxim's decision, 2026-10-08). Rules for ticks, save list caps, data cleanup, compatibility; check its checklist before each commit.

**Routine data work (reading, comparing, searching) and simple operations go to an agent with the Haiku model (Agent tool, model "haiku"); design, code and mockups are done directly** (Maxim's decision, 2026-10-09).

**PC session: fix bugs and small corrections right away; do not queue them or wait to be asked** (Maxim's decision, 2026-10-08).

## Start of every session
1. `git pull` (the repo is the sync point between sessions and devices).
2. Read `docs/STATUS.md` (short current state, next steps, open questions), `docs/TASKS.md` (the only task list) and `docs/CONCEPT.md` (full current design, with a table of contents).
3. Before writing code for a new system, GameComponent, saved list or pawn reference, read `docs/TECH_DETAILS.md` (rules for ticks, save list caps, data cleanup, compatibility) and follow its checklist.

## End of every task
1. Build: `dotnet build Source/Netrun.csproj -c Release` (0 errors, 0 warnings).
   Check translations: `python tools/check_keys.py` (every NR_ key used in code exists in English and Russian).
2. Update `docs/STATUS.md` (short, current state only; history goes to `docs/ARCHIVE.md`) and tick items in `docs/TASKS.md`.
3. Commit and `git push`. One logical step per commit.

## GitHub automation
- `.github/workflows/build.yml` runs on every push and PR: XML validation, English/Russian Keyed key parity, build with `-warnaserror` (CI uses Krafs.Rimworld.Ref, Lib.Harmony and the HE DLL from its GitHub repo), and a zipped mod as an artifact.
- Release: push a tag `vX.Y.Z` (keep `About.xml` modVersion and the csproj Version in sync) and CI attaches the zip to a GitHub Release.
- Check a run: `gh run list --limit 3`, `gh run view <id> --log-failed`.

## Layout
- `About/`: mod metadata. packageId `GafarovMaxim.HackingExpansionNetrun`.
- `Defs/`, `Patches/` (XML patches), `Languages/English`, `Languages/Russian (Русский)`, `Textures/`: game content.
- `Source/`: C#, namespace `Netrun`, net48, references the game, Harmony and `HackingExpansion.dll` from the local Steam folders (`RIMWORLD_DIR` overrides the default path).
- `Assemblies/HackingExpansionNetrun.dll`: built output, committed so the mod works straight from the repo.
- Source art (full-size originals, old variants, templates) lives outside the repo in `G:\Modding\RimWorld\Hacking Expansion Netrun\Reserve`, paths kept as `Hacking Expansion Netrun Art/...` (2026-10-15). Only game copies are in the repo, in `Textures/` (items 128 px, 2x2 buildings 256 px). New art: put the original in Reserve, the game copy in Textures.
- `docs/`: STATUS (current state), TASKS (task list), CONCEPT (design), TECH_DETAILS (code and performance rules), ART (art style and art still needed), RELEASE (Steam release checklist), HELP (in-game help format), ARCHIVE (history and old file to new file map); folders `mockups/`, `ui-mockups/`, `steam/`, `news_flavor/`.

## Conventions
- defName prefix `NR_`, translation keys `NR_`.
- Every player-facing string goes to Keyed or DefInjected in both English and Russian.
- Harmony: `PatchAll` with attribute patches. Prefer vanilla hooks (`CompHackable.ProcessHacked`) and public HE members; wrap patch bodies in try/catch.
- Save data through `ExposeData`; guard collections against null after loading.

## Hacking Expansion integration points
HE source lives in the Steam workshop folder `294100/3573344880/Source` and at github.com/BlueEagle421/HackingExpansion.
- Neural interface = cyberlink: `USH_HE.CyberUtils.IsHacker(pawn)`. Do not derive our hediffs from `Hediff_Cyberlink`.
- Cyberdata: `USH_Cyberdata`, `USH_AdvancedCyberdata`. Any ThingDef with `USH_HE.RippableExtension` becomes rippable.
- Cyberpod: `USH_HE.Building_Cyberpod`; `pawn.TryGetHoldingCyberpod(out pod)`. It cannot hack targets with the `USH_BlackICE` hackset.
- ICE: `USH_HE.HacksetDef` and `HackingOutcomeDef` are plain defs, extend them with XML patches.
- Delayed raids: `USH_HE.DelayedRaidUtility`.
- HE does not touch the vanilla `CommsConsole`.

## Limits
- Claude does not run the game. Maxim tests in game and sends the log.
- Never edit Hacking Expansion files.
- No credentials or secrets in the repo.
