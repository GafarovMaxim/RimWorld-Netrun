# Changelog

All notable changes to Hacking Expansion: Netrun. Format: [Keep a Changelog](https://keepachangelog.com/), versions follow `About/About.xml`.

## [Unreleased]
### Added
- Combat Extended: crew and corp raiders spawn with spare magazines (patch applies only when CE is loaded).

## [1.1.1] - 2026-10-16
### Changed
- Abandoned server site: a bigger compound with the four inside turrets in the corners, out of blast range of the server; six turrets outside on all sides; traps (hunter drones with Odyssey, otherwise IEDs) in a ring around the walls.

## [1.1.0] - 2026-10-16
### Added
- The net rituals (litany of data, trace cleansing) have their own green look: a glow, circuit traces on the floor, falling columns of code and floating digits; and their own ambience (a cyberspace drone with data chirps) instead of the ideoligion music.
- New terminal frame "Braindance studio": a violet monitor over a mixing desk with a scrolling timeline, a chip bay, a LED level meter (Heat and signal), faders, a jog wheel, headphone and mic AUX jacks, a spectrum screen and VHS stickers. Opens with 3 unique braindance recordings.
- A red "Regenerate the cyberspace map" button in the mod settings (Events, under the map options) rebuilds the current game's map with the chosen size and layout, after a confirmation.
- Trace cleansing is now done by up to three netrunners at the comms console, without spectators; their average Intellectual sets the quality.
- The Net Archon is a regular vanilla role: apparel requirements and the usual role editor options work for it.
- New icons for the net rituals (litany of data, trace cleansing) and the Net Archon role, in the style of the vanilla Ideology icons.
- Deck theme "Relic" (opens with the first dive into the Old Net; new achievement "Ghosts of the Old Net"): a battered field unit in cream plastic worn through to rusty metal, the screen in a dark bezel with screws, rubber keys and jewel lamps on a dark top plate, and an instrument column on the right: VOL/SQL knobs, a faded TUNE gauge under cracked glass whose needle shudders with the Heat, SCAN with an ICE lamp, a TRACE knob that turns with the Heat level, JACK and PURGE buttons. At the bottom an orange LCD, stickers, grey square keys, Heat lamps and a slide switch.
- The default deck frame is now the NR-1 Cyberdeck: matte black plastic with chamfered corners, carbon inserts and grip ridges, chiclet keys with glowing legends, a TUNE thumb wheel, light-pipe lamps with ALERT, RF on an OLED, an amber LCD, a light-pipe Heat tray and a NET LINK rocker. The light line round the screen is cyan and turns red, pulsing, from Target.
- Iron Company: a red ALARM lamp over the nameplate pulses once Heat reaches Target and strobes in the Red Zone.
- Deck frames react to high Heat: Nomad's charm swings harder, its RF bars jitter, the TUNE needle drifts with static and the hottest gem flickers; Kurenai's red band breathes, the logo square lights up and the current THREAT INDEX square pulses, flashing in the Red Zone; Retro's pixel flames dance and its LCD flashes "!! ALERT !!"; Street's neon buzzes and drops out in the Red Zone, its graffiti glows pink and its LCD log starts throwing red errors; the classic Heat scale's top segment pulses.
- The game's music fades down while the terminal is open (to 30 %, a slider in Sound settings) and comes back when it closes.
- Settings: hovering a cyberspace layout shows a small preview of its map.
### Changed
- Frames at small scales: the Relic and NR-1 Heat scales shrink instead of vanishing, the NR-1 top lamps slide into any room, the Nomad tally shows as many groups as fit; the Retro HEAT caption sits higher, the Nomad cross pad core is centred.
- Braindance studio at the default window: the spectrum screen shrinks to a small one instead of hiding, the LCD panel narrows, fader captions are short (NET LINK, EMO, SOMA, AUD, VIS) and no longer overlap. Net rituals: the floor circuit traces now light up and fade in turn (brighter, thicker) instead of static motes that did not show.
- Relic HEAT lamps fit the default window (keys pack tighter on narrow windows). Net ritual effects are sparser and spread over a fixed area around the target, through walls, instead of crowding a small room.
- The abandoned server stays after a colonist connects to it (once; the AI fragment drops next to them). The turret hijack event is now called network hijack. Corporate frame key glyphs centred on their rings.
- Hub watch has no button any more: while anyone lies in a powered cyberpod, the sharpest of them keeps watch (+1 hub defense per 4 Intellectual). Several pods do not stack.
- Ideoligion screen: the Net Archon sits in one row with the leader and the moral guide (it had a lower impact, which started a row of its own).
- Cyberspace maps keep more room between hubs (wider districts, a larger gap, and a pass that pushes apart hubs left too close), so links cross less and name plates overlap less. Applies to newly generated nets.
- Rotated terminal parts (Relic TRACE knob and TUNE needle, Iron and Nomad dials, gateway map markers) stay in place with any UI and terminal scale.
- Terminal frames: bold captions on Retro (bottom), Nomad and Kurenai (all keys and lamps), crisp bold on Relic (HELP read as "HHELP" under the UI scale); Iron and Classic lamp captions sit higher.
- Ideology: net rituals always start "any time" (the game could roll them as date rituals with no date, so the button never showed); existing ones are repaired on load.
- Ideology: the net rituals now show in the ideoligion's ritual list (they were hidden), and a net meme added to an existing ideoligion now brings its ritual (litany at the AI server, trace cleansing at the comms console); missing ones are added on load.
- Ideology: rituals and the net role get random names (e.g. "silicon vigil", "purge of the logs", "oracle of the root"; in Russian «бдение кремния», «стирание логов», «оракул корня»). Existing ideoligions keep their names until regenerated.
- The AI server and the abandoned server fill their 3x3 footprint in every facing; server racks (new art with a black stand) sit higher and no longer spill onto the cell below.
- Street: the NET/SICK/POD lamps sit in chrome rings on a black chamfered plate with tape and a bolt sticker.
- Iron Company is wider by default; the terminal window grows with a thicker casing instead of squeezing the screen.

## [0.6.4] - 2026-10-15
### Added
- Deck theme "NR-80 Retro" (opens after a season in Shadow): a cream handheld with a sun badge, sunset bars, cream keys on a dark plate, control strips on the rails, a green LCD, round BACK/CORE/HELP keys, the Heat as pixel flames and a NET LINK slide switch.
- Deck theme "Iron Company" (replaces Milspec, opens after a clean streak of 10): a rugged field tablet in chipped olive paint with rubber bumpers, handles, an antenna, connectors and vents, guarded toggles for CLOSE and NET-LOCK, a TUNE knob that follows the tuner, an amber MIL-RF window and LCD, a THREAT LEVEL bargraph and a NET LINK toggle.
- Deck theme "Kurenai Systems" (replaces Ops pad, opens after 25 contracts): a corporate exec terminal on light printed stock, a worn red band with 紅, hatching, registration marks and microtype, round sensor keys and a THREAT INDEX of the Heat levels.
- Deck theme "Street" (opens after passing Black ICE): a worn yellow panel with cut corners, a neon tube on top that burns with the Heat (cyan, pink from Tracked, red in the Red Zone), keys with neon outlines, a holographic kitsune sticker, 夜の走者 on a sprayed band and graffiti.
- Deck theme "Nomad" (opens after 50 dives): a worn field case with its own silhouette, solar panel, hinges, pouch and charm sticking out of the window, a D-pad, a strap of Heat gems and a NET LINK lever that slides along its slot. The terminal window grows a little to fit it.
- Dev actions: open all deck themes at once or lock them again (they work from the main menu too).
### Changed
- Ideology: net rituals always start "any time" (the game could roll them as date rituals with no date, so the button never showed); existing ones are repaired on load.
- Ideology: the net rituals now show in the ideoligion's ritual list (they were hidden), and a net meme added to an existing ideoligion now brings its ritual (litany at the AI server, trace cleansing at the comms console); missing ones are added on load.
- Ideology: rituals and the net role get random names (e.g. "silicon vigil", "purge of the logs", "oracle of the root"; in Russian «бдение кремния», «стирание логов», «оракул корня»). Existing ideoligions keep their names until regenerated.
- Netsick grows about half as fast: a day in the net or a pod 0.10, a dive 0.02, an ICE hit 0.03. A runner can stay in cyberspace for about three days before the strong stage.
- Netsick fades faster offline: 0.06 a day (was 0.04), twice that with live leisure, so withdrawal after a long run lasts about 2 to 4 days.
- Map panel: the hub's facts (owner, exit, fortress, states with their timer, route) and the "Next 3 days" list are coloured rows with the time on the right; the hub's nodes are listed with their type icons.
- Important nodes on the map (contract target, key, black lab, corporate core, Old Net) pulse with a soft ring in their colour.
- The braindance batch button reads "Edit all: N".
- The dive cost line says when it is paid from the pod's fuel ("Cost: 2 cyberdata · from the pod").
- The map header shows how many hours a runner can still stay in the net before Netsick turns strong, instead of the stage name.
- The Transition starts for a group: the button opens a list of the occupied pods on the map, the ticked colonists upload together. When the mind is gone, the body quietly dies (no death letter, no grief, no Echo). The epilogue (credits or play on) comes as soon as the group is through, like the ship launch. Empty shells from older saves are released on load.
### Fixed
- The AI server is placed facing front by default.
- Map tags (watch, timer) no longer overlap a hub's LOCKED / OPEN tag.
- Network isolation breaks a running Transition (like opening the pod) and blocks starting one.
- The hover tick no longer loops over the terminal's header icons (guide, menu, Cyberpedia).

## [0.6.3] - 2026-10-15
### Fixed
- Keys to protected corporate hubs no longer land in orbital hubs (out of reach without a ship in space), at Home or in the Old Net; keys already there move to a ground hub on load.

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
- Ideology: net rituals always start "any time" (the game could roll them as date rituals with no date, so the button never showed); existing ones are repaired on load.
- Ideology: the net rituals now show in the ideoligion's ritual list (they were hidden), and a net meme added to an existing ideoligion now brings its ritual (litany at the AI server, trace cleansing at the comms console); missing ones are added on load.
- Ideology: rituals and the net role get random names (e.g. "silicon vigil", "purge of the logs", "oracle of the root"; in Russian «бдение кремния», «стирание логов», «оракул корня»). Existing ideoligions keep their names until regenerated.
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
- Ideology: net rituals always start "any time" (the game could roll them as date rituals with no date, so the button never showed); existing ones are repaired on load.
- Ideology: the net rituals now show in the ideoligion's ritual list (they were hidden), and a net meme added to an existing ideoligion now brings its ritual (litany at the AI server, trace cleansing at the comms console); missing ones are added on load.
- Ideology: rituals and the net role get random names (e.g. "silicon vigil", "purge of the logs", "oracle of the root"; in Russian «бдение кремния», «стирание логов», «оракул корня»). Existing ideoligions keep their names until regenerated.
- Comms console renamed to network console; the terminal pauses the game once on opening.
- One AI server per colony with server racks; AIs in a host get only the hediff (no services, no feeding).
- Fixer shop, trace scrubbing and contracts redone in the terminal style.
### Fixed
- Stuck passenger hediff, AI fragment storage in stockpiles made before the mod, black chip icon size.

## [0.4.0] - 2026-10-05
- AI server with racks, coprocessor AI, carried deliveries, AI events, Netsick and the net craving break, braindance chips.

## [0.3.0] and earlier
- Digital Heat, network terminal, fixers, contracts, cyberpod bonuses and ICE outcomes.
