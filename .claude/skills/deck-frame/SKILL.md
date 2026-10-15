---
name: deck-frame
description: Use when making or fixing a terminal deck frame theme (casing around the Netrun terminal) in Hacking Expansion: Netrun - procedural textures in tools/gen_deck_*.py, mockups, the Deck*.cs drawing code, theme registration, unlocks. Pipeline, helpers and a checklist of bugs Maxim already caught.
---

# Deck frame themes (Netrun)

A frame = a Python generator that writes textures + a C# partial of `NetrunUI` that draws the casing and its controls around the screen rect. Existing ones to copy from: Nomad (`DeckNomad.cs`, `gen_deck_nomad.py`), Street, Kurenai, Iron, Retro, Cyber (the default, `DeckCyber.cs`, `gen_deck_classic.py`: the cleanest small example).

## Pipeline
1. References: Maxim says where they are or attaches them in chat; do not assume a folder. Describe 2-3 variants in words first if the brief is vague.
2. Generator `tools/gen_deck_<theme>.py`: `build()` writes textures to `Textures/UI/Netrun/Frame/<Art>/`, `--mock OUT` renders a full mockup into `docs/mockups/NN-<theme>.png`. Note: in iron/kurenai `--mock` does NOT rebuild textures; run without args first.
3. Show mockups (SendUserFile), wait for Maxim's pick. Variants: keep a `V`/`VARIANTS` dict and `--variant X`; delete unchosen variant folders only after he agrees.
4. C#: `Source/Terminal/Deck<Theme>.cs` with constants `Out L/T/R/B` (casing outside the box), `In L/T/R/B` (box to screen), `Slice`, `Extra`; a `<Theme>Tex` static class (`[StaticConstructorOnStartup]`, ContentFinder paths); `<Theme>Bezel(outer, heat, level, online, anim, time, onKey, menu)` returning the screen rect; casing / top / bottom functions.
5. Register: `DeckStyle` enum and the theme entry in `DeckThemes.cs` (keep the old `Id`, set `Art` to the folder; `UnlockKey`/`UnlockCount`), `NetrunDeck.DeckExtra` + `DeckBezel` switch, `Preview.png` in the art folder for the settings card.
6. Texts: `NR_DeckTheme_<Id>` and `_Desc` in English and Russian `Keyed/Terminal.xml`.
7. Build, `python tools/check_keys.py`, CHANGELOG + STATUS, commit, push to dev.

## Helpers
- `tools/deck_art.py` (as `A`): `mask`, `fbm` (about ±0.3, centred), `grain`, `shade` (top-left light), `inner_edge`, `blur`, `streaks`, `to_img`, `over`, `save(img, folder, name)`, `nine_slice`, `rivet`, `text_layer`, `spray`, `glow`, `font` (Tektur/bahnschrift; no exotic glyphs like ⏻ - they render as boxes), `SS = 3`.
- `tools/deck_stickers.py` (as `S`): `Big` canvas (`rect`, `poly`, `ellipse`, `line`, `text`, `done`), `jfont` (Japanese), `barcode`, `graffiti`.
- C# shared in `NetrunUI`: `NineSlice` (snaps to device pixels via `GUI.matrix`, overlaps 1 px), `Sprite`, `Stencil`, `GlyphText`, `GlyphTex`, `Glow`, `Tip`, `Cached`, `DrawLcd`/`LcdLines`, `TexturedKey`, state `_pressed`, `_rf`, `_tuner`; colours `Ok`, `Bad`, `DeckOrange`, `PodGreen`, `LcdGreen`, `HeatSegments`.
- Repeating art (carbon, ridges) is drawn cropped with `GUI.DrawTextureWithTexCoords` and a partial uv rect, never stretched.
- High Heat: every frame reacts from Target (pulse) and harder in the Red Zone (strobe); `Noise(time, rate, seed)` gives stepped jitter.
- Mirroring a sprite: `GUI.DrawTextureWithTexCoords(r, tex, new Rect(1, 0, -1, 1))`.

## Checklist (bugs Maxim already caught)
- Python default args bind at definition time: palette params default to `None` and resolve inside, or variants all get the same colour.
- 9-slice seams: always `NineSlice`, never raw stretched DrawTexture for sliced art.
- Nothing past the casing or the window edge: antennas, handles, labels, warning text. Check at the smallest window.
- Controls sit inside the casing, clear of its lip, hinges, bumpers and handle mounts; keys never cut the frame edge.
- The screen opening is baked into the body texture: `IN_*` in the generator and `In*` in C# must match, change both and regenerate (Nomad bug: code moved, texture did not).
- Labels under keys never touch the screen bezel; leave 6-8 px. Raise `In T`/`In B` instead of squeezing.
- Panels never overlap each other or bumpers; vents/plates never sit on screws (skip occupied zones when placing screws).
- Every interactive part has a tooltip and a hover state; hover-sound keys must be unique per rect (see the `Key(hit)` hash fix in NetrunUI).
- Keys need volume: a skirt plus a domed cap with a highlight, not a flat rounded rect. No grey collars around coloured buttons.
- Mechanics must make sense: a guard cover hides a push button, not a lever; slanted keys looked unnatural.
- Colours: Maxim prefers muted, not bright white; light themes are greyed down.
- Generated textures: body 1400x900 max; small parts at real pixel size (no runtime scaling).
- Theme Ids are saved in settings: never rename an Id, use `Art` for the folder.
- The game locks the DLL while running (MSB3026): rebuild after Maxim closes it.
