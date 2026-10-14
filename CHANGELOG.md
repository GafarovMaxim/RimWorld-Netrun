# Changelog

All notable changes to Hacking Expansion: Netrun. Format: [Keep a Changelog](https://keepachangelog.com/), versions follow `About/About.xml`.

## [Unreleased]

## [0.6.2] - 2026-10-15
### Fixed
- Config errors for Hacking Expansion's cyberpod: now that it can be uninstalled, it has a storage category (misc buildings) and a mass of 60.

## [0.6.1] - 2026-10-15
### Fixed
- Config errors on load: the Transition research now names a faction tag that no faction has (the game wants one with techprints); the neurostabilizer no longer lists the Drugs category twice.

## [0.6.0] - 2026-10-15
### Added
- Cyberspace map: one living map per game with about 45 hubs in districts charted by visits, the Old Net behind a wall, links walked in game time, sessions and exits, map events, Odyssey orbital hubs.
- Map tools: lenses (overview, loot, danger, corporations, jobs, time), search, routes with autopause on threats, a watchlist with timers, a "Next 3 days" column, a legend, WASD panning.
- Corporations with Alert and Strength (hardening, patrols, intrusions along the map, captures, collapse); protected corporate hubs opened by keys found in neighbouring hubs; rival runner crews with their own emblems; the top-100 of the net; Club tabs for corporations, crews and the top.
- Dives: forks of easy and hard paths, a Back button, one daemon per dive, node alerts and ICE patrols, Hacking Expansion items as dive programs, dive effects on the runner (flow, migraine, burned link and more).
- A free training dive in a simulator with a tour of the dive screen; a newcomer tour of the terminal, first-time hint cards, a glossary in tooltips and a Guide mode for every screen.
- Hub defense: wall-mounted firewall, ICE tiers with upkeep, honeypot, watch duty, network isolation, intercepting intrusions with a dive.
- The Transition: a late-game ending with a rumour, an insider in the Club, corporate black labs, five prototype techprints and a mind upload from a cyberpod.
- AI quests in the vanilla Quests tab, AI levels and second services; AI traits; moving an AI between coprocessors.
- Fixer traits, imperial clients (Royalty), five new contract kinds (surveillance, lay low, rookie training, AI order and more).
- Braindance: 8 unique recordings, "Edit all" in one click with a small glitch risk, a braindance category in storage.
- Pawns and DLC: dive traits, nine backstories, pod atrophy, dive stories and tales; a gene and a braindance learning desire (Biotech), a royal permit (Royalty), net culture issues, memes, rituals and the Net Archon role (Ideology).
- Four scenarios; 26 achievements for Vanilla Achievements Expanded (optional).
- News feed with real effects and choices; neurostabilizer drug; five sound packs with volume settings; terminal scaling; Cyberpedia articles for every system.
- Feedback: floating +Heat over hacked objects, a message when a netrunner in a pod gets hungry or tired, safe time left in a pod.
### Changed
- AI balance: network noise, awakening needs research and cyberdata and can fail, fragments decay away from a server.
- Research projects moved into the Hacking Expansion tree.
- The terminal reopens on the screen it was closed on; mod windows behave like the game's (time, camera, click outside closes).
- Every mod item has a mass and a storage category.
- RimWorld 1.7 listed as supported next to 1.6.
### Fixed
- Hacking a black box after loading a save no longer throws (Hacking Expansion lost its factions; Netrun saves them).
- Orbital traders no longer sell the Transition techprint.
- Every Harmony patch is guarded, so an error in one never breaks the game's own logic.
- Many interface and performance fixes: cached drawing, background work only with the terminal closed, capped save lists.

## [0.5.0] - 2026-10-06
### Added
- Network terminal as a node map: Core (Heat ring, daily trend, alerts, resources, "Heat and trace" screen with a 7-day graph), Club, Board, Archive, Server room, Studio, Gateway.
- Cyberspace: Gateway with floating external nodes (zoom, drag, discover), turn-based dives with code gates, hidden caches, Black ICE, programs and Hacking Expansion outcomes.
- 75 AI events (10 general, 5 per personality) with choices, limits and a dev action to fire any of them.
- Braindance goggles, raw recordings from strong moments, editing into ordinary and black chips at the network console; exotic traders buy chips.
- Network attacks gated by Heat and the hidden "Black Ledger" mercenary faction with revenge raids.
- Cyberspace architect tab, terminal accent color setting, EN/RU texts throughout.
### Changed
- Comms console renamed to network console; the terminal pauses the game once on opening.
- One AI server per colony with server racks; AIs in a host get only the hediff (no services, no feeding).
- Fixer shop, trace scrubbing and contracts redone in the terminal style.
### Fixed
- Stuck passenger hediff, AI fragment storage in stockpiles made before the mod, black chip icon size.

## [0.4.0] - 2026-10-05
- AI server with racks, coprocessor AI, carried deliveries, AI events, Netsick and the net craving break, braindance chips.

## [0.3.0] and earlier
- Digital Heat, network terminal, fixers, contracts, cyberpod bonuses and ICE outcomes.
