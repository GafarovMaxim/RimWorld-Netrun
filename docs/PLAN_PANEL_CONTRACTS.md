# UX: Gateway side panel + Contracts board (ArchitectUX, 2026-10-23)

Line numbers are from the working tree on 2026-10-23. GatewayMapView.cs, GatewayView.cs and GatewayMapFuture.cs have
uncommitted edits from another session, so trust the method names first and the line numbers second.
Builds on uxarchitect.md section 4 (structure) and uidesigner.md section 5 (look). Both are kept as phase 2.

---------------------------------------------------------------------------------------------------------------------
## A. Gateway side panel

### What is wrong (from 180-184.webp + code)
1. **Three contexts in one column.** `GatewayView.DrawPanel` (GatewayView.cs:625) stacks: training button -> `DrawMapPanel`
   (GatewayMapView.cs:852): runners + 3 button rows + hub + node list -> node block (GatewayView.cs:665-730) -> programs
   (734) -> daemons -> cost -> DIVE (796) -> footer. On 180 the DIVE button sits about 900 px below the fold. The reader
   cannot tell which block belongs to the runner, which to the hub and which to the node.
2. **Every header looks the same.** RUNNERS, MARKET QUARTER-22 and MARKET-31 all use `NetrunUI.Section` at 36 px with
   "//". The hub and the node have near-identical names ("Market quarter-22" next to "Market-31").
3. **"Pick a node to read it" plus programs (183).** `FocusNode` (GatewayMapV4.cs:53) sets `_selectedId = branchId`,
   which is -1 for every caller without a branch (Show buttons, OpenGatewayAt). A map click instead picks the first
   visible node (GatewayMapView.cs:752-753). With node == null, DrawPanel prints the hint (GatewayView.cs:662) and then
   still draws PROGRAMS, daemons and a disabled DIVE.
4. **Runner buttons have no owner.** Stop/Exit + Emergency (GatewayMapView.cs:865-881), Go and exit (896) and Home (902)
   are 3 full-width rows of the same weight. The red Emergency button is as large as Stop and sits right beside it.
5. **Truncated runner card.** "4 h in the" (180): `RunnerCard` draws `NR_MapSessionHours` ("{0} h in the net") into a
   56 px rect (GatewayMapPanel.cs:55).
6. **Rival is invisible.** It shows only as a Tiny Warn chip "⚔ rival" below RISK (GatewayView.cs:716-725). The font has
   no ⚔ glyph, so it renders as "x rival" (182). It is not on the map at all: `ImportantColor` (GatewayMapView.cs:627)
   skips Rival, the hub badge (GatewayMapView.cs:~611, `DrawNodeBadge` 708) knows only contract/key, and the panel node
   rows (GatewayMapView.cs:1072-1080) show contract/key badges but no rival. The ⚔ also shows in the list view
   (GatewayView.cs:576).

### Phase 1: cheap wins (each about 1-15 lines, no layout rewrite)
| # | Change | Where |
|---|---|---|
| A1 | A focused hub always has a node: in `FocusNode`, if `branchId < 0`, use the first visible branch of `hubId` (same rule as the click, GatewayMapView.cs:752). | GatewayMapV4.cs:56 |
| A2 | With node == null or hidden: show the hint, then go **straight to the footer**. No programs, no daemons, no cost, no DIVE. | GatewayView.cs:660-664 (early `DrawPanelFooter` + return, as at 646) |
| A3 | Runner card time: measure the label with `Text.CalcSize` (or a short key "{0} h"); give the location label the rest of the width. | GatewayMapPanel.cs:53-55 |
| A4 | **Rival in the panel**: remove the chip at GatewayView.cs:722. Under the node's Section header (line 666), draw a full-width warning strip: 24 px, Warn 0.12 fill, 2 px Warn left edge, `NetrunTex.CyberIcon("Event_Rival")` 18 px, text `NR_DiveRivalActive`(rivalTurns) "Rival netrunner: N turns left", tip `NR_GatewayRivalTip`. It is the same strip the dive shows (DiveView.cs:657/857), so it means the same thing in both places. Lift `DiveView.Warning` (DiveView.cs:832) to `NetrunUI` and use it in both. | GatewayView.cs:666, 716-725 |
| A5 | **Rival on the map (one-badge rule kept)**: in `ImportantColor` add `if (b.Rival) return NetrunUI.Bad;`. The rival node then sorts first among the plate chips (GatewayMapFuture.cs:577) and pulses like the other important nodes. Badge priority at GatewayMapView.cs:~611: contract > **rival** > key. The rival badge is the `Event_Rival` icon, 12 px, Bad tint, on a dark rim like the contract badge (`DrawNodeBadge(Rect, kind)`). Add one legend row (GatewayMapLegend.cs:~360). | GatewayMapView.cs:611, 627, 708; GatewayMapLegend.cs |
| A6 | Rival in the panel node rows: a 12 px Event_Rival icon just left of the defense pips (`right - 16`). Add "Rival: N turns" to the row tip. List view: replace " · ⚔" with the word key `NR_GatewayRival` without the glyph (drop ⚔ from the EN/RU value). | GatewayMapView.cs:1072-1086; GatewayView.cs:576; Keyed Cyberspace.xml:247 |
| A7 | Calm the runner buttons: Emergency becomes a 28x28 icon button (Bad border, no fill, keeps the confirm dialog) at the right of the Stop/Exit row. Stop/Exit takes the remaining width. Go and exit + Home share one row, half width each. That saves 32 px and one row of red. | GatewayMapView.cs:865-905 |
| A8 | Make the hub and node headers different. Hub: keep the Section header. Node: a Tiny caps "NODE" kicker above a Medium name with the type icon, no "//" rule. Both pass a `level` arg to a new 3-line `NetrunUI.SubSection` (or inline). | GatewayView.cs:666 |
| A9 | When a hub is picked, the DIVE block must be visible without scrolling. In DrawPanel, set `_panelScroll.y` to the node block's y the frame `_selectedId` changes (store `_lastPanelNode`). It is a cheap stand-in for the sticky footer. | GatewayView.cs:625-640 |

Phase 1 needs no new textures; Event_Rival already exists. New keys: the legend row only (EN+RU).

### Phase 2: structure (from uxarchitect.md section 4, cut down)
- **Sticky action bar** at the bottom of the panel, outside the scroll. Compute its height first, then
  `outer.height -= barH`. Contents follow the deepest context: node -> cost line + DIVE 40 px; hub without a node ->
  GO (route h); runner moving -> STOP; runner at an exit -> DISCONNECT. Secondary icon row 28 px: Go home, Go & exit,
  Jump, Intercept, Emergency (red border, last). This moves the buttons at GatewayMapView.cs:865-1025 and
  GatewayView.cs:780-799 into one `DrawActionBar(Rect, ctx)`.
- **Breadcrumb header**, fixed at 28 px: `Leo › Market quarter-22 › Market-31`. Each crumb selects its level; [x]
  clears the hub. It replaces the three 36 px Section headers.
- **Runner strip**: runner cards shrink to 28 px chips (initial, state dot, ETA) in one row, with "+" for a pod pawn
  that is not connected (replaces the Connect buttons at GatewayMapView.cs:908-920). The selected runner's
  route line sits under it.
- **Node detail in two columns**: TYPE / DEFENSE / ICE / TRACE / RISK as a key/value grid (key Tiny, col 84 px).
  Today these are 5 rows of 52 px (GatewayView.cs:667-700), about 260 -> 120 px. Reward items stay one per line.
- **Programs**: hide them behind a "Programs 1/2 ▸" fold when nothing is picked. They are a pre-dive setting, not
  information.
- Width: 270 -> 296 (GatewayView.cs:18). The map loses 26 px, which is fine at 1080p. Do this only together with the
  two-column grid.
Order: action bar first (it fixes the scroll problem for good), then the breadcrumb, then the runner strip.

---------------------------------------------------------------------------------------------------------------------
## B. Contracts board (Terminal/ContractsTab.cs)

### How availability works today
- `ContractManager.CanAccept` (Contracts/ContractManager.cs:317): blocked by isolation, a silent fixer,
  **`!fixer.ChannelOpen` -> "Contact the fixer first."**, one active contract per fixer, and QuietRun when Heat > Tracked.
- The channel is a paid call: `FixerDeals.Call` (Fixers/FixerDeals.cs:12) takes `EffectiveCallCost` silver and opens it
  for `channelHours` (`Fixer.ChannelOpen`, Fixers/Fixer.cs:68). The Club card has the Call button
  (FixersTab.cs:284-289).
- The board's **active** card already has an inline Call button (ContractsTab.cs:383-393). **Offer cards do not**: the
  Accept button just goes disabled (ContractsTab.cs:475-478), and the reason shows only in a tooltip. That is why the
  player walks to the Club and has to remember a name.
- Hidden trap: `Accept` deletes every other offer from that fixer (ContractManager.cs:339,
  `_contracts.RemoveAll(c => !c.Accepted && c.FixerId == fixer.Id)`). The UI never says so.
- Fixer chip click opens a help article (ContractsTab.cs:294-296), not the fixer.

### Recommendation: call in place, plus a jump
The real need is "accept this offer", not "visit the fixer". Phase 1 fixes this on the card; the jump is the secondary
route for everything else (scrub, shop, trait).

### Phase 1 (priority order)
| # | Change | Where |
|---|---|---|
| B1 | **Call on the offer card.** When `!fixer.ChannelOpen` (and the fixer is not silent), the button slot shows `Call · {cost}` (Warn, `NetrunTex.CallFixer`, same AcceptanceReport as ContractsTab.cs:388-390) instead of a disabled Accept. After the call, the same slot turns into Accept. Two clicks, no tab change. Move the call-button code into a `static bool CallButton(Rect, Fixer, Map)` used by both DrawActive and DrawOffer. DrawOffer needs `map` (pass it down from DrawMain:193). | ContractsTab.cs:383-393, 475-478 |
| B2 | **Reason under the button, not only in a tip.** A Tiny Muted line under the button, right-aligned, 2 lines max: report.Reason, or with an open channel "channel 5h" (Ok). | ContractsTab.cs:478 (ry + 50 + 34 + 2) |
| B3 | **Jump to the fixer.** The fixer chip (first tag) becomes a link: click -> `Dialog_NetTerminal.OpenFixer(fixer.Id)`, which sets `ClubTabs._tab = Fixers` and `FixersTab.FocusId = id`, then `OpenNode(View.Club)` (pattern: `OpenGatewayAt`, Dialog_NetTerminal.cs:485). In `FixersTab.Draw` (FixersTab.cs:83), when `FocusId >= 0`, set `scroll.y` to that card's y once, flash its border for 1.5 s (`NetrunUI.ScanlineOnce`), then reset. Chip tip: "Open in the Club". The other tags keep the article link. Also add a small "↗" button on the fixer item in the left column (ContractsTab.cs:104) next to its filter click. | ContractsTab.cs:249-250, 294-296; Dialog_NetTerminal.cs (~485); ClubTabs.cs:15-23 (make Tab settable); FixersTab.cs:83-140 |
| B4 | **Medium filter.** `DifficultyFilter { Any, Easy, Medium, Hard }` with values 0..3, filter `(int)_difficulty == Difficulty(c)` (ContractsTab.cs:153). The filter is a static, not saved, so it is safe. Draw it as **one segmented row** of 4 (All, ■, ■■, ■■■ in Ok/Warn/Bad) instead of 4 stacked 28 px toggles. That frees ~70 px of the left column. Keys: `NR_BoardFilter_Medium` (EN/RU); keep the Easy/Hard keys as tips. | ContractsTab.cs:27-32, 88-94, 153 |
| B5 | **Sort by type.** `Sort { Reward, Duration, Type }`; Type = `OrderBy(c.Type).ThenByDescending(c.Reward)`. With Type sort, draw a 20 px Tiny caps divider when the type changes (title + count). This is what the owner means by sorting by type: grouping. Left column: 3 toggles in one row (thirds). The "reward ▼" label at ContractsTab.cs:184 becomes a click that cycles the sort. | ContractsTab.cs:34-38, 95-100, 155-157, 184, 190-195 |
| B6 | **Warn before Accept wipes offers.** When the fixer has more offers, add to the Accept tip: "His other {n} offers will be withdrawn." Do not add a confirm dialog (it is one click, and the offers come back on rotation). | ContractsTab.cs:476-478; new key `NR_BoardAcceptDrops` |
| B7 | **Channel state in the fixer column.** Sub line: `rep 4 · 2 offers · ● open 5h` (Ok dot) or `· ○ closed` (Muted). The player sees whose offers he can take before reading the cards. | ContractsTab.cs:82-84 |
| B8 | **Dim locked offers.** When report is not accepted and the reason is not the channel (one active per fixer, too loud), draw the card at 0.55 alpha. Channel-closed cards stay at full brightness, because B1 makes them actionable. | ContractsTab.cs:431-435 |
| B9 | Perf (TECH_DETAILS: no per-frame LINQ in GUI): `DrawLeft` sorts and sums (69, 76) and `DrawMain` rebuilds `offers` (151-157) on every GUI event. Wrap both in `NetrunUI.Cached("board.offers", ..., 0.5f)`, keyed by filter/sort/fixer. | ContractsTab.cs:69-76, 150-157 |

### Offer card, phase 1 target layout (same grid as today, IconSize 64, RightColumn 150)
```
| [icon]  Quiet run                                         660 silver |
|         Complete 3 hacks without your trace rising ...      +10 rep. |
|         [IronByte ↗] [Heat ≤ Tracked]                 [ Call · 40 ]  |  <- or [ Accept ]
|         ■■□ medium · Time: 7 days                    channel closed  |  <- Tiny reason line (B2)
```
- The left stripe stays in the fixer's specialty colour (it already matches the fixer chip).
- The active card title drops " · ColdHalo" (ContractsTab.cs:328) and gets the same fixer chip with ↗, so the two
  card types read the same way.

### Phase 2 (only if the board still feels crowded)
- Move filter/sort out of the left column into a 32 px toolbar above AVAILABLE (difficulty segment, sort segment,
  count). The left column then holds only fixers, and its card height drops 52 -> 44.
- Per-type filter chips in that toolbar (Delivery, Hack, Watch, ...), toggled as a set.
- Recheck the "One active per fixer" button label (ContractsTab.cs:476): name the blocking contract in the reason
  line instead.

### Keys to add (EN + RU, run tools/check_keys.py)
`NR_BoardFilter_Medium`, `NR_BoardSort_Type`, `NR_BoardOpenFixer` (chip tip), `NR_BoardAcceptDrops`,
`NR_BoardChannelOpenShort`, `NR_BoardChannelClosedShort`, legend row for the rival badge. Reuse: `NR_FixerCall`,
`NR_FixerCallTip`, `NR_DiveRivalActive`, `NR_GatewayRivalTip`.

### Not checked
- I could not see whether `NetrunUI.Button` already prints the disabled reason anywhere but the tip. B2 assumes it
  does not.
- Russian string widths in the 3-way sort row (Награда / Срок / Тип should fit at a third of 220).
- Whether the font really lacks ⚔. The "x rival" in 182 suggests it does. A6 drops the glyph either way.
