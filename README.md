# Hacking Expansion: Netrun

<p align="center">
  <img src="About/Preview.png" alt="Hacking Expansion: Netrun Preview" width="680"/>
</p>

<p align="center">
  <a href="https://rimworldgame.com/"><img src="https://img.shields.io/badge/RimWorld-1.6-2ea44f?style=for-the-badge&logo=csharp" alt="RimWorld 1.6"/></a>
  <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=3573344880"><img src="https://img.shields.io/badge/Dependency-Ushanka's%20Hacking%20Expansion-00b4d8?style=for-the-badge" alt="Hacking Expansion Dependency"/></a>
  <a href="https://github.com/pardeike/HarmonyRimWorld"><img src="https://img.shields.io/badge/Requires-Harmony-f77f00?style=for-the-badge" alt="Harmony"/></a>
  <a href="https://github.com/AndroidQuazar/VanillaExpandedFramework"><img src="https://img.shields.io/badge/Requires-Vanilla%20Expanded%20Framework-7209b7?style=for-the-badge" alt="VEF"/></a>
  <a href="https://github.com/GafarovMaxim/RimWorld-Netrun/actions"><img src="https://img.shields.io/badge/CI%20Build-Passing-brightgreen?style=for-the-badge&logo=githubactions" alt="Build Status"/></a>
</p>

<p align="center">
  <b>English</b> | <a href="README_RU.md">Русский</a>
</p>

---

> *"The network remembers your trace. Data is currency, but the netrunner pays in body and sanity."*

**Hacking Expansion: Netrun** is a deep cyberpunk expansion layer for RimWorld 1.6, designed as a modular companion to **[Ushanka's Hacking Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3573344880)**.

Intrusions are no longer isolated events: every breach leaves a digital trace across remote nodes, attracts underworld fixers, awakens autonomous network AIs, and provokes severe corporate counter-intrusion protocols.

---

## ⚡ Overview

**Hacking Expansion: Netrun** transforms hacking in RimWorld into a high-stakes cat-and-mouse dynamic:

**Infiltration → Cyberdata Harvest → Digital Heat → Network Retaliation (ICE, Net Attacks, Mercenaries)**

* **The Network Remembers:** The more telemetry you generate, the deadlier the network's response.
* **Data as Currency:** Trade stolen archives for hardware, hire fixers, and scrub your trace clean.
* **The Cost of Cybernetics:** Long-term cyberspace immersion causes neural degradation (Netsick) and addiction.

---

## 🚀 Key Features

### 0. Network Terminal & Cyberspace Dives
The comms console becomes a **network console**; a colonist opens the terminal there (a cyberpod has its own button).
* **Node map:** Core (Heat ring with daily trend, alerts and resources), Club (fixers), Board (contracts), Archive (trace journal), Server room (AIs), Studio (braindance), Gateway (dives).
* **Gateway:** external nodes float as circles (size = haul, color = type, dashed = hidden). Pick programs (Sword, Shield, Decoy, Stealth, Killer) and dive from a cyberpod.
* **Turn-based dives:** move through code gates, scan hidden caches, hack Black ICE, download data and leave through the I/O port. Black ICE answers with Hacking Expansion outcomes: nausea, lockout, coma.

### 1. Digital Heat & Network Trace
Every intrusion or data siphon generates measurable telemetry for the colony (0–100%).
* **Shadow (0–24%):** Undetected ghost presence. Networks remain passive.
* **Noticed (25–49%):** Security alerts tripped. Slight neuro-noise and data theft risks.
* **Tracked (50–74%):** Active triangulation. Viral machine breakdowns, battery power grid spikes.
* **Target (75–94%):** High-priority threat. Corporate ransomware, mercenary strike deployment.
* **Red Zone (95–100%):** Maximum alert. Lethal black-ops raids, rogue AI flight or hijacking risks.

Trace passively cools down over time or can be actively expunged through underworld fixers using Cyberdata.

### 2. Underworld Fixers & Rogue Contracts
Access the Network Terminal via the comms console to tap into an ever-rotating roster of fixers:
* **Specializations:**
  * **Broker** — Buys bulk data archives at a premium payout (x1.5).
  * **Cleaner** — Quickly and cheaply purges colony Heat from remote tracking nodes.
  * **Fence** — Delivers rare cybernetic equipment and modules via orbital drop-pods.
* **Personality Traits:** Fixers can be *Reliable*, *Greedy*, or hidden *Rats* (who may sell out your coordinates upon failed contracts).
* **Contracts:** Time-sensitive missions for system penetrations, secure data deliveries, and stealth ops without tripping trace thresholds.

### 3. Autonomous Network AIs (13 Personalities)
Dormant AI fragments can be salvaged from protected systems and awakened at the network console:
* **Infrastructure:**
  * **AI Server (2×2):** Dedicated computing facility running core services (requires 300W power).
  * **Server Racks:** Modular server extensions expanding colony capacity up to 3 active AI cores.
  * **AI Coprocessor:** Cyberpod attachment that houses an AI to dampen Heat buildup and slow Netsick.
  * **AI Socket:** Neural cranial implant allowing direct embodiment of an AI inside a colonist's brain.
* **Personalities:**
  * *Ghost* — Dampens Heat and provides early warning telemetry.
  * *Data Hunter* — Siphons bonus Cyberdata from network runs.
  * *Muse* — Inspires colonists and accelerates research progress.
  * *Predator* — Neutralizes network attacks and boosts host combat prowess.
  * *Warden* — Autonomous firewall guarding against ICE feedback shocks.
  * *Doc* — Accelerated nervous system recovery from cybernetic trauma.
  * *Oracle* — Forecasts upcoming events and reveals fixer hidden traits.
  * *Trickster*, *Siren*, *Echo (Fallen Hacker)*, *Seed*, *Forge*, *Archivist*.
* **Trust & Hunger:** Each core has unique desires. Starved or mistreated AIs can leak coordinates, defect, or trigger host mental overrides.

### 4. Braindance (BD Recording & Chips)
Complete closed-loop synthesis of virtual neural recordings:
* **Braindance Goggles:** Worn on the eyes. Passively captures raw emotional peaks (combat victories, joy, trauma) into a `Raw BD Recording`.
* **Console Editing:** Raw recordings are edited at the network console into finished BD chips of varying quality (*Normal, Excellent, Masterwork*).
* **Playback:** Requires BD goggles or an installed Cyberlink neural interface. Grants intense mood elevation but carries real addictive danger.

### 5. Network Incidents & "The Black Book"
* **Network Attacks:** Hacker neuro-noise, remote cyberdata theft, machine sabotage, power surges, and ransomware lockouts.
* **The Black Book:** A covert mercenary syndicate without static map bases. Deploys elite hit-squads tracing hot telemetry signals or retaliating for botched contracts.

### 6. Cyberspace Sickness (Netsick)
Excessive pod immersion leads to chronic cyberspace dependency. Severe withdrawal can trigger erratic binges where netrunners recklessly dive into the net: 65% chance of brain trauma or coma, but 12% chance of a legendary breakthrough with rare data and AI fragments.

---

## 📦 Requirements & Load Order

1. **RimWorld 1.6**
2. **Harmony**
3. **Vanilla Expanded Framework**
4. **Ushanka's Hacking Expansion**
5. **Hacking Expansion: Netrun** *(must load after Hacking Expansion)*

---

## 🛠️ Building from Source

Requires .NET SDK targeting `.NET Framework 4.8`:

```cmd
# Quick build:
build.bat

# Or via dotnet CLI:
dotnet build Source/Netrun.csproj -c Release
```

Compiled assembly outputs directly to `Assemblies/HackingExpansionNetrun.dll`.

Verify localization key consistency:
```cmd
python tools/check_keys.py
```

---

## 📄 License

[MIT](LICENSE) © 2026 Raul Moreau. The Tektur font (Fonts/, AssetBundles/netrun_fonts_*) is licensed separately under the [SIL Open Font License 1.1](Fonts/OFL.txt).

## 📜 Authors & Credits

* **Author:** Raul Moreau (GafarovMaxim)
* **Font:** [Tektur](https://github.com/hyvyys/Tektur) © 2023 The Tektur Project Authors, SIL OFL 1.1.
* **Special Thanks:** BlueEagle421 & the Ushanka Modding Team for the incredible foundation of Hacking Expansion.
