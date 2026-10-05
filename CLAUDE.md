# Hacking Expansion: Netrun: notes for Claude sessions

RimWorld 1.6 mod, a cyberpunk layer on top of Ushankas Hacking Expansion (HE). Owner: Maxim (GafarovMaxim). Talk to him in Russian.

## Start of every session
1. `git pull` (the repo is the sync point between sessions and devices).
2. Read `docs/STATUS.md` (current state, next steps, open questions) and `docs/CONCEPT.md` (design).

## End of every task
1. Build: `dotnet build Source/Netrun.csproj -c Release` (0 errors, 0 warnings).
   Check translations: `python tools/check_keys.py` (every NR_ key used in code exists in English and Russian).
2. Update `docs/STATUS.md`: what changed, what is next, open questions.
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
- `Hacking Expansion Netrun Art/`: full-size source art, not loaded by the game (`Art/` and unused files stay on the author's PC only, see .gitignore). Game copies go to `Textures/` (items 128 px, 2x2 buildings 256 px).
- `docs/`: STATUS, CONCEPT, ART_REQUESTS.

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
