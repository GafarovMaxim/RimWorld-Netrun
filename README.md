# Hacking Expansion: Black ICE

Standalone RimWorld 1.6 addon for **Hacking Expansion**.

## Included systems

- Digital trace / digital heat that rises from hacking and creates escalating consequences.
- Fixer contracts: data delivery, system taps, and trace-burning jobs.
- Cyberdata as a physical resource that can be sold, used for fixer work, crafted into braindance, or burned to scrub trace.
- Unknown AI fragments that can be sold or awakened into persistent AI contacts with different personalities and benefits.
- Adaptive ICE: repeated successful hacks of the same target type gradually increase the chance of rejection and neural feedback.
- Cyberspace dependence: deep sessions through the Hacking Expansion cyberpod create a dependency that grows while connected and decays slowly offline.
- Braindance: ingestible recorded experiences with mood/work benefits, tolerance, and addiction.
- Network attacks: high digital heat schedules attacks that break down powered systems and can inflict neural noise.

## Installation

1. Install RimWorld 1.6.
2. Install the original **Hacking Expansion**.
3. Copy this folder into `RimWorld/Mods/`.
4. If you are using the source project, build against the RimWorld 1.6 managed assemblies and place `HackingExpansionBlackIce.dll` in `Assemblies/`.

## Important implementation note

The integration intentionally avoids a compile-time dependency on Hacking Expansion's C# assembly. It resolves `CompHackable` by type name at runtime and patches its `Hack`, `OnHacked`, `ProcessHacked`, and `CanHackNow` methods when present. This makes the addon less brittle across small Hacking Expansion code changes.

## First prototype scope

Version 0.1 is designed as a playable systems prototype rather than a finished content pack. It intentionally uses vanilla textures for new items/buildings, and the cyberspace layer is represented as a state attached to a cyberpod session rather than a separate graphical network map.
