# Changelog

All notable changes to Hacking Expansion: Netrun. Format: [Keep a Changelog](https://keepachangelog.com/), versions follow `About/About.xml`.

## [Unreleased]
- The node card now lists everything a dive can give (files, caches, forgotten cache, advanced cyberdata and when, silver, special find chance); all reward bonuses (storm, guarded hub, lure, mirror, news, Orbit, hidden passages) stack to at most x2 (the corporate core is exempt), and non-corp Black ICE nodes add 1 advanced cyberdata.
- Dive rewards are fixed by node tier instead of the entry cost: 2-3 / 3-5 / 4-6 (hard path) / 5-8 / core 10-14 cyberdata, caches 1, forgotten cache 2-4, corp advanced files only on the hard path, dark exchange 120-250 silver.
- Fixed: opening the AI server window and closing the terminal no longer play the game's window chime on top of the sound pack.
- Fixed: "Edit all" in the studio logged "Invalid count: -1" when the editor picked up a raw chip stack.
- Fixed: a fixer's scrub no longer takes cyberdata past what brings the Heat down to the AI minimum; the amount fields stop there.
- AI auto-feed: when the carrier drops the delivery (downed, drafted, interrupted), the next order goes out within the hour instead of after 6 hours.
- Offline hubs and nodes on the map keep their shape, drawn in grey.
- Uncharted hubs no longer show a question mark; they are already dimmed.
- Every awake AI after the first raises the minimum Heat: Heat now climbs to that minimum on its own, a fixer's scrub stops there and the scrub window says so; waking a second AI asks first; the Cyberpedia Heat page explains it.
- An accepted crew challenge shows among active contracts (time left, a Show button) and its hub pulses on the map like a contract target.
- Studio: raw recording rows show how many chips the stack holds, and the raw count is in chips, not stacks.
- Studio: "Edit all" no longer teleports the chips; the best editor gathers every raw stack, carries them to the network console and edits them there.
- Settings: "Cyberpods need fuel" (on by default). Off, cyberpods hack without fuel and are never refuelled; a dive still costs cyberdata from storage.
- Fixed: a gravship could not land on the distress signal site.
- Fixed: the fame of a challenge race went to a random crew instead of the one that threw it.

## [1.2.2] - 2026-10-23
- The data courier leaves at once when his hunters are beaten, he is unhurt and no other enemy on the map still fights (sleeping mechs, hive insects and shamblers do not count).
- AI: an Auto-feed button for an AI in a server or coprocessor; it is fed from storage on its own when hungry, a free hauler carries the food.
- Ghost's hunger per hack halved: 7.5 instead of 15.
- The courier's reward options fit the letter: exec chips of one kind are named once.

## [1.2.1] - 2026-10-23
- Map generator: each district keeps its own ground, so districts no longer mix or tear, also on big maps; on big maps hubs stand closer instead of spilling into neighbours.
- The transit no longer grows with the hub count, and every transit hub leads to a district.
- Transit hubs are lime triangles on the map, so they no longer look like the campus.
- Orbital hubs stand one short step out from their corporation's outermost hub, away from links and other districts.
- The Market quarter is now called Trade Row, the Entertainment quarter Vice.
- A hub's description lists every running event with its time left, including a rival's trap.
- Links of an offline hub are drawn shut, with their own tooltip.
- Existing saves: districts are sewn back together by their own streets, transit hubs get links, orbital hubs are placed again (once). Mixed districts of an old map are fixed only by a new map.
- Fixed: the Old Net could be reached around the Wall (street repair and cable cuts); an Old Net hub left with no link now gets its own Wall link.
- Fixed: an orbital hub could appear on top of another hub or on a link.

## [1.2.0] - 2026-10-23
- The training dive offer opens in the terminal's style; the training button lives on the Home hub only, always. Node rows show defense as muted red pips further left; the second line of a wrapped hub name is centred and matches the first; the Board's sort button no longer covers the divider.
- UI tails: Board cards are lower with a smaller type icon, empty Board lists use the terminal's empty state; "Now in the net" reads in the larger font with fewer rows; the search box never runs under the lens bar; DIVE is 40 px and Go 28 px like the other panel buttons.
- Gateway: a wrapped hub name keeps its state icons on the first line and centres the second; the panel no longer scrolls on its own; the hub's node list has a proper section header, a rule before the picked node, and shows rival, key, known honeypot and contract marks.
- UI pass 7: first steps for a new player in the Gateway panel (ticked as done); the map's hotkeys in the legend header's tooltip; the route lights up after Go; the Board's active chip reads "Active N of M" and explains the one-per-fixer rule.
- Cyberspace map: district names on hover only when zoomed out; long hub and district names wrap onto two lines; event waves are slower and stop once the hub is clicked; nodes of an uncharted hub or of the walled Old Net are no longer listed; corporations are sewn back into one piece and no hub is left without a street (new maps and, once, existing saves). Board: the sort is a visible button.
- Performance: the cyberspace map's hub hover card is built only when drawn and cached for half a second.
- UI pass 6: Outskirts, Corp and Old Net district colours no longer match the danger red or the OK green; hub plates switch to the larger font from 180 % zoom.
- UI pass 5: Board: the left sort block is gone (the label over the offers cycles the sort); a one-line "All" item; no empty reputation bar; grouped by type, cards are titled by their fixer; an accepted contract scrolls to the top and its card flashes.
- UI pass 4: the Gateway panel widens with the window (270 to 340 px); programs in a two-column grid with a used/slots counter; the runner count chip left the window header (it is in the panel).
- UI pass 3: the node card in the Gateway panel is compact: one-line fact rows, the type under the node's name, the risk in one word (details in the tooltip), the daemon hint in one line; DIVE fits without scrolling more often.
- UI pass 2: hub plates, watch and timer tags no longer hide under the map's buttons; the zoom buttons sit on a dark backing, the zoom percent moved into the reset button's tooltip; off-screen runner tags stay below the lens bar.
- UI pass 1: the dive cost moved into the DIVE button, the reason it is off shows under it; readable small text is lighter; no loot bars under uncharted hubs; Board tooltips in the terminal style; an empty contract history says so and does not unfold; × in multipliers, hops in route info, Reward sort renamed.
- Cyberspace map: hub plates show node chips on a second row under the name; up to three state icons left of the name (key, exit, ICE patrol, known honeypot, lock, rival, storm, offline, lab, corp core); a hub under contract surveillance pulses amber; hovering a district fades its name in at its centre.
- Board: the fixer chip filters the offers by that fixer; a fixer without a line gets a call button in the left list (pulses while they have offers); a reason line under a blocked button; blocked offers dimmed; Accept warns that the fixer's other offers are withdrawn; difficulty filter as one row of squares; the type sort groups offers with headers; the sort label cycles the sort.
- Gateway side panel: showing a hub picks its first node; with no node the panel skips programs and DIVE; the node gets its own header and the panel scrolls to it; the emergency exit is a small icon button, Go and exit and Home share a row.
- Cyberspace map: a rival netrunner shows as an icon left of the hub name, like the key.
- Cyberspace map: the contract target is an amber pulsing ring on every lens and the first, framed chip in the hub's plate.
- Cyberspace map: a new path is drawn only where it leaves the active route, with a notch where it turns off.
- Cyberspace map: exit and key icons now sit left of the hub name, like the lock; district names are gone from the map.
- Contracts: a Medium filter, sorting by type, and a Call button right on an offer whose fixer has no open channel.
- Fixed: the grid animation on the cyberspace map slid over the map while panning.
- Fixed: some generated maps left corporate hubs cut off from the net.
- Fixed: batch braindance editing used only the top recording of each stack.
- The rival netrunner on a node is now a red strip in the side panel and the first chip on the map.
- Cyberspace map: districts show as soft colour patches; district names lost their frames.
- Cyberspace map: a hub's nodes are now chips in its name plate instead of icons around it; a shut hub shows a lock in the plate instead of the LOCKED tag.
- Cyberspace map: clearer links: straight, four kinds (street, highway, darknet, shut), the route crawls toward its end, and the key line shows only on hover or selection.
- Cyberspace map: links never cross any more, also after map events; older saves get their crossing links rerouted once.
- Russian: a full copy-edit of the Russian texts: gender forms, grammar after numbers, consistent terms (Stealth is now «Невидимка», Black ICE everywhere) and several meaning errors.
- Russian: a glossary of terms and an automatic check of Russian texts; fixed a missing AI name in a quest step and «хаб» → «узел».
### Fixed
- The key diamond on the cyberspace map no longer has a black square behind it.
### Added
- Russian translation of Hacking Expansion itself (items, implants, hediffs, abilities, research, messages and settings).
### Changed
- Russian: cyberdata is now «киберданные», exec data «дискета со скриптом», execs «скрипты».
- Netsick no longer grows from hacks on the map; only time and hacks in a cyberpod cause it.

## [1.1.13] - 2026-10-22
### Changed
- The data courier's payment grows with the colony's threat points (250 to 3000 silver worth); on completion the player picks silver, gold, a prosthetic (from any mod), exec chips or cyberdata.
- Recorded braindance chips of one kind share a stack, like a playlist: the inspect pane lists every recording, and a button chooses which one is watched, sold or taken next.
- Each AI archetype page in the Cyberpedia now lists what the AI gives a cyberpod from a coprocessor.
- The AI archetype gallery card switches between the server, the coprocessor and the socket to show what the AI gives in each.
- Right-clicking a stack of recorded braindance chips offers "Watch...": a list of every recording in it, with the colonist's own and relatives' recordings marked and listed first.
- The Studio library lists every recording on the map with a search line and a "Show to..." button that sends a chosen colonist to watch it; unique recordings moved to their own "Rare" tab.
- Watching one's own recording or a loved one's gives a mood bonus, stronger if that person is dead.

## [1.1.12] - 2026-10-20
### Changed
- The braindance storage category is now a subcategory of Drugs.
### Fixed
- The braindance category showed a missing-texture icon in the categorized resource readout.

## [1.1.11] - 2026-10-19
### Added
- Comms consoles from other mods, such as the compact comms terminal from Vanilla Gravship Expanded - Chapter 1, serve as network terminals: the terminal itself, AI awakening, the onboarding step and console rituals.
### Changed
- New icon for the "net culture" ideoligion issue.

## [1.1.10] - 2026-10-17
### Changed
- The data courier hides in any room (not a prison cell) when there is no dining room or rec room, and fights back against enemies who get into his room or come within 5 cells; he still never runs out to fight.
- The courier's hunters bring small arms only while the raid is under 500 points; grenadiers and launchers join bigger hunts.
### Fixed
- A courier who falls to his wounds no longer counts as lost: rescue him and he hides again and finishes the job. Death, being carried off, being taken prisoner or lying downed 3 days past his time still fails it.
- The courier never hides in a freezer or another room too cold or hot for him.

## [1.1.9] - 2026-10-17
### Changed
- The "power surge" net attack also locks the colony grid down: no electricity on the map for 1.5 days (like a solar flare). It can now hit a colony without batteries.
- The data courier hides indoors, in the dining room or rec room, stays in that room and keeps out of the colony's fights, then leaves when his day is up.
- One data courier at a time: no new offer while a courier or his hunters are still pending.
- The courier's hunters have their own team, mostly gunners and elites with a few grenadiers and heavies, since they came for the courier, not to level the base.
### Added
- Debug actions "Net attack: hit now" (no Heat, defense or intrusion) and "Net attack: through the defenses" in Netrun: test raids.
### Fixed
- The courier's hunters that cannot arrive (no spawn spot, colonists away) try again in an hour, up to three times, instead of being lost; a second courier no longer cancels the first one's hunters. The log now says when they are due, when they arrive and why they could not.

## [1.1.8] - 2026-10-17
### Changed
- Ending the story after the Transition shows the credits and then returns to the main menu; the credits no longer repeat the "end or play on" line.
### Added
- A cyberpod that loses power throws out a colonist who is on a net run, in a dive or hacking a target: the dive fails, the emergency jolt applies (Netsick, nausea, a lockout chance) plus a 15% chance of a net coma. No death.
- The "Net found" window at the first entry into cyberspace is restyled as a terminal screen and lets you pick the layout template (Random, Ring, Spine, Archipelago, Metro) as well as the size; the preview lights up hub by hub from Home.
- A cyberpod that loses power mid-Transition sends a letter and holds on reserve for 3-4 hours; restore power in time or the upload breaks and the pod throws the colonist out into a net coma with a cyberspace lockout. Finishing on reserve has a 40% chance to end the same way.
- A progress bar in the terminal accent colour over every cyberpod with a colonist making the Transition, with the percent and time left in a framed label when zoomed in.

## [1.1.7] - 2026-10-17
### Changed
- Braindance goggles can be worn with open helmets and hats (simple, advanced and the like), but not with helmets that close the whole head (marine, cataphract, recon, phoenix and so on).

## [1.1.6] - 2026-10-17
### Fixed
- Mercenaries (the courier's hunters, mercenary raids, the corporate hunt for an AI) attack the colonists instead of breaching or sapping through the walls.
- Mercenaries scale with the colony: the raid follows the storyteller's points instead of a 300-point floor, so a young colony that takes the courier meets one or two gunners, not a strike team.

## [1.1.5] - 2026-10-17
### Changed
- The AI server and the abandoned server are narrower from the side (front and back unchanged).

## [1.1.4] - 2026-10-16
### Fixed
- An uninstalled (minified) Hacking Expansion cyberpod no longer throws an error every tick.

## [1.1.3] - 2026-10-16
### Fixed
- Our factions (runner crews, corporations, mercenaries) no longer get picked for vanilla quests: they broke the Anomaly distress call and Ideology work sites.
- Colony events (courier, black market, distress signal, network hijack) now respect their refire delay: they could repeat every few days.

### Changed
- The distress signal only comes to a colony that has a netrunner (a colonist with a cyberlink).

## [1.1.2] - 2026-10-16
### Added
- Dev mode: a "Netrun: test raids" group fires a corporation, runner crew, mercenary or corporate hunt raid with no conditions, at a chosen strength, and writes the raiders' weapons to the log.
- Combat Extended: crew and corp raiders spawn with spare magazines (patch applies only when CE is loaded).

### Changed
- Mercenary raids and the corporate hunt for an AI bring gunners: small raids are no longer all grenadiers, and the heavy team has riflemen too.

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
