# Hacking Expansion: Netrun

A cyberpunk layer for RimWorld 1.6, built on top of [Ushanka's Hacking Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3573344880).

Every successful intrusion leaves a digital trace. Keep your Heat low, deal with fixers, bring rogue AIs home, dive into cyberspace and record braindances, but remember that the network always answers.

## Status

Work in progress, version 0.2.0 (Phase 0: clean foundation).

Working now:
- **Digital Heat.** Every completed hack by a colonist raises the colony's trace. Hacker stealth and Hacking Expansion cyberpods reduce it, and it cools down over time. An alert shows the current level: Ghost, Noticed, Hunted, Burned.
- **Braindance chip.** A craftable recorded experience (drug lab, Hacking Expansion cyberdata + neutroamine) with its own tolerance and addiction.

Planned: network terminal on the comms console, fixers, AI personalities with an AI server, cyberspace dependency, braindance goggles and recording, network attacks.

## Requirements

- RimWorld 1.6
- Harmony
- Vanilla Expanded Framework
- Ushankas Hacking Expansion

## Building

Requires the .NET SDK. The project references the game, Harmony and Hacking Expansion from the Steam folders.

```
build.bat
```

If RimWorld is not in `E:\SteamLibrary\steamapps\common\RimWorld`, set `RIMWORLD_DIR` first. The DLL goes to `Assemblies/`.

## Layout

- `About/`, `Defs/`, `Languages/` (English, Russian), `Textures/`: mod content.
- `Source/`: C# code (`Netrun` namespace).
- `Art/`: full-size source art, not loaded by the game.
- `docs/`: design notes and art requests.
