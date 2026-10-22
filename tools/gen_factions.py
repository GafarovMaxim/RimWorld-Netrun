"""Generates Defs/NetFactions.xml and its Russian DefInjected file: six runner crew emblems and eleven corporation
themes as hidden factions without settlements (docs/CONCEPTS_10.md, sections 6 and 11). The game creates none of them
at world generation; Source/Factions/NetFactions.cs picks three crews and three corporations per game.
Run: python tools/gen_factions.py"""
import os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")

CREWS = [
    # key, label, names, colors (dark, accent)
    ("Spider", "Spiders", ["Chrome Spiders", "Glass Spiders", "Black Widows", "Silk Line"], "(0.12,0.12,0.14)", "(0.75,0.75,0.8)"),
    ("Kitsune", "Kitsune", ["Nine Tails", "Neon Kitsune", "Fox Fire", "Red Foxes"], "(0.2,0.08,0.06)", "(0.95,0.45,0.2)"),
    ("Spades", "Spades", ["Ace of Spades", "Dead Hand", "Black Suit", "High Card"], "(0.08,0.08,0.1)", "(0.9,0.9,0.9)"),
    ("Rat", "Rats", ["Gutter Rats", "Cable Rats", "Rat King", "Sewer Line"], "(0.15,0.13,0.1)", "(0.7,0.6,0.45)"),
    ("Bat", "Bats", ["Night Bats", "Echo Bats", "Blind Wing", "Belfry"], "(0.1,0.05,0.15)", "(0.65,0.35,0.95)"),
    ("Wolf", "Wolves", ["Grey Wolves", "Static Pack", "Lone Howl", "Iron Wolves"], "(0.1,0.12,0.14)", "(0.4,0.75,0.95)"),
]

CORPS = [
    # key, word, label, description, colors, mayRequire
    ("Security", "Security", "security corporation", "A corporation that sells safety: guards, ICE and the hunters behind it. Its nodes are thick with ICE, and its raids hit hard.", "(0.1,0.12,0.18)", "(0.35,0.6,0.95)", None),
    ("Media", "Media", "media corporation", "A corporation of feeds, stars and braindance studios. Its vaults hold recordings, and its rumors fill the news.", "(0.18,0.06,0.14)", "(0.95,0.35,0.7)", None),
    ("Biotech", "Biotech", "biotech corporation", "A corporation that edits life. Its data is dense and valuable, and its attacks come as neural noise.", "(0.06,0.16,0.1)", "(0.35,0.9,0.5)", None),
    ("Neurotech", "Neurotech", "neurotech corporation", "A corporation of implants and minds. It hoards unique recordings and chips, and knows how to make a cyberlink hurt.", "(0.08,0.1,0.18)", "(0.3,0.85,0.95)", None),
    ("Finance", "Capital", "finance corporation", "A corporation of money and ledgers. Its vaults are full of silver, and it takes a toll on every link it owns.", "(0.14,0.12,0.06)", "(0.95,0.8,0.3)", None),
    ("Logistics", "Logistics", "logistics corporation", "A corporation of routes and freight. Its fast backbone lines run everywhere, and it steals data straight from warehouses.", "(0.16,0.1,0.04)", "(0.95,0.6,0.2)", None),
    ("Arms", "Arms", "arms corporation", "A corporation of weapons. Its vaults hold guns, and its raiders come better equipped.", "(0.14,0.06,0.06)", "(0.9,0.3,0.25)", None),
    ("Energy", "Power", "energy corporation", "A corporation of grids and reactors. It attacks with power surges and can switch whole nodes off.", "(0.14,0.14,0.04)", "(0.95,0.9,0.3)", None),
    ("Pharma", "Pharma", "pharma corporation", "A corporation of drugs and cures. Its vaults hold medicine and neurostabilizers.", "(0.06,0.14,0.14)", "(0.4,0.95,0.85)", None),
    ("DataBroker", "Data", "data broker corporation", "A corporation that buys and sells secrets. Its loot is maps and dark links, and it trades leaks in the news.", "(0.1,0.08,0.14)", "(0.7,0.55,0.95)", None),
    ("Orbital", "Orbital", "orbital corporation", "A corporation that lives above the sky. Its nodes and its fortress are in orbit.", "(0.06,0.08,0.12)", "(0.85,0.9,1.0)", "Ludeon.RimWorld.Odyssey"),
]

CORP_WORDS = ["Kobalt", "Sayren", "Arasaka", "Helix", "Vanta", "Orison", "Militech", "Petrova", "Zetatech", "Kiroshi",
              "Halden", "Novak", "Tessier", "Ashgrove", "Meridian", "Obsidian", "Lumen", "Castell", "Wyrm", "Northgate"]
CORP_WORDS = [w for w in CORP_WORDS if w not in ("Arasaka", "Militech", "Zetatech", "Kiroshi", "Tessier")]  # no real brands

RU_CREW = {"Spider": ("команда раннеров", "Пауки"), "Kitsune": ("команда раннеров", "Кицунэ"), "Spades": ("команда раннеров", "Пики"),
           "Rat": ("команда раннеров", "Крысы"), "Bat": ("команда раннеров", "Летучие мыши"), "Wolf": ("команда раннеров", "Волки")}
RU_CORP = {
    "Security": ("охранная корпорация", "Корпорация, которая продаёт безопасность: охрану, ICE и охотников за ним. Её ноды плотно закрыты ICE, а рейды бьют тяжело."),
    "Media": ("медиакорпорация", "Корпорация лент, звёзд и студий брейнданса. В её хранилищах записи, а её слухи заполняют новости."),
    "Biotech": ("биотех-корпорация", "Корпорация, которая переписывает жизнь. Её данные плотные и дорогие, а атаки приходят нейрошумом."),
    "Neurotech": ("нейротех-корпорация", "Корпорация имплантов и разумов. Копит уникальные записи и чипы и умеет сделать кибер-линку больно."),
    "Finance": ("финансовая корпорация", "Корпорация денег и реестров. Хранилища полны серебра, и она берёт пошлину с каждой своей связи."),
    "Logistics": ("логистическая корпорация", "Корпорация маршрутов и грузов. Её скоростные магистрали везде, а данные она крадёт прямо со складов."),
    "Arms": ("оружейная корпорация", "Корпорация оружия. В хранилищах стволы, а её рейдеры приходят лучше снаряжёнными."),
    "Energy": ("энергетическая корпорация", "Корпорация сетей и реакторов. Атакует скачками напряжения и может обесточить целые узлы."),
    "Pharma": ("фармкорпорация", "Корпорация лекарств. В хранилищах медикаменты и нейростабилизаторы."),
    "DataBroker": ("корпорация дата-брокеров", "Корпорация, которая покупает и продаёт секреты. Её добыча — карты и тёмные связи, а сливы она продаёт в новости."),
    "Orbital": ("орбитальная корпорация", "Корпорация, которая живёт над небом. Её ноды и крепость — на орбите."),
}

HEADER = '<?xml version="1.0" encoding="utf-8"?>\n<!-- Generated by tools/gen_factions.py -->\n<Defs>\n\n'


def pawn_kinds():
    return '''  <!-- Pawns of the net factions -->
  <PawnKindDef>
    <defName>NR_CrewRunner</defName>
    <label>runner</label>
    <race>Human</race>
    <combatPower>70</combatPower>
    <isFighter>true</isFighter>
    <defaultFactionDef>NR_Crew_Spider</defaultFactionDef>
    <itemQuality>Normal</itemQuality>
    <gearHealthRange>0.7~2.3</gearHealthRange>
    <apparelMoney>400~1000</apparelMoney>
    <apparelTags>
      <li>IndustrialBasic</li>
      <li>IndustrialAdvanced</li>
      <li>IndustrialMilitaryBasic</li>
    </apparelTags>
    <apparelAllowHeadgearChance>0.5</apparelAllowHeadgearChance>
    <weaponMoney>200~500</weaponMoney>
    <weaponTags>
      <li>SimpleGun</li>
      <li>ShortShots</li>
    </weaponTags>
    <modExtensions>
      <li Class="Netrun.NetPawnKindExtension">
        <brainImplant>USH_InstalledCyberlink</brainImplant>
      </li>
    </modExtensions>
    <initialWillRange>1~3</initialWillRange>
    <initialResistanceRange>10~18</initialResistanceRange>
  </PawnKindDef>

  <PawnKindDef>
    <defName>NR_CrewMuscle</defName>
    <label>crew muscle</label>
    <race>Human</race>
    <combatPower>85</combatPower>
    <isFighter>true</isFighter>
    <defaultFactionDef>NR_Crew_Spider</defaultFactionDef>
    <itemQuality>Normal</itemQuality>
    <gearHealthRange>0.7~2.3</gearHealthRange>
    <apparelMoney>600~1400</apparelMoney>
    <apparelTags>
      <li>IndustrialMilitaryBasic</li>
      <li>IndustrialMilitaryAdvanced</li>
    </apparelTags>
    <weaponMoney>400~800</weaponMoney>
    <weaponTags>
      <li>IndustrialGunAdvanced</li>
      <li>Gun</li>
    </weaponTags>
    <initialWillRange>2~4</initialWillRange>
    <initialResistanceRange>14~24</initialResistanceRange>
  </PawnKindDef>

  <PawnKindDef>
    <defName>NR_CorpGuard</defName>
    <label>corporate guard</label>
    <race>Human</race>
    <combatPower>95</combatPower>
    <isFighter>true</isFighter>
    <defaultFactionDef>NR_Corp_Security</defaultFactionDef>
    <itemQuality>Good</itemQuality>
    <gearHealthRange>1~2.5</gearHealthRange>
    <apparelMoney>1000~2000</apparelMoney>
    <apparelTags>
      <li>IndustrialMilitaryAdvanced</li>
      <li>SpacerMilitary</li>
    </apparelTags>
    <weaponMoney>600~1200</weaponMoney>
    <weaponTags>
      <li>IndustrialGunAdvanced</li>
      <li>SpacerGun</li>
    </weaponTags>
    <initialWillRange>3~5</initialWillRange>
    <initialResistanceRange>18~30</initialResistanceRange>
  </PawnKindDef>

  <PawnKindDef>
    <defName>NR_CorpNetrunner</defName>
    <label>corporate netrunner</label>
    <race>Human</race>
    <combatPower>80</combatPower>
    <isFighter>true</isFighter>
    <defaultFactionDef>NR_Corp_Security</defaultFactionDef>
    <itemQuality>Good</itemQuality>
    <apparelMoney>800~1600</apparelMoney>
    <apparelTags>
      <li>IndustrialAdvanced</li>
      <li>IndustrialMilitaryAdvanced</li>
    </apparelTags>
    <weaponMoney>300~600</weaponMoney>
    <weaponTags>
      <li>ShortShots</li>
      <li>IndustrialGunAdvanced</li>
    </weaponTags>
    <modExtensions>
      <li Class="Netrun.NetPawnKindExtension">
        <brainImplant>USH_InstalledCyberlink</brainImplant>
      </li>
    </modExtensions>
    <initialWillRange>3~5</initialWillRange>
    <initialResistanceRange>18~30</initialResistanceRange>
  </PawnKindDef>

'''


def faction(defname, label, desc, singular, plural, name_maker, icon, c1, c2, groups, extra="", may=None):
    attr = f' MayRequire="{may}"' if may else ""
    return f'''  <FactionDef ParentName="NR_NetFactionBase"{attr}>
    <defName>{defname}</defName>
    <label>{label}</label>
    <description>{desc}</description>
    <pawnSingular>{singular}</pawnSingular>
    <pawnsPlural>{plural}</pawnsPlural>
    <factionNameMaker>{name_maker}</factionNameMaker>
    <factionIconPath>UI/Netrun/Factions/{icon}</factionIconPath>
    <colorSpectrum>
      <li>{c1}</li>
      <li>{c2}</li>
    </colorSpectrum>{extra}
    <pawnGroupMakers>
{groups}
    </pawnGroupMakers>
  </FactionDef>

'''


BASE = '''  <FactionDef Name="NR_NetFactionBase" ParentName="FactionBase" Abstract="True">
    <hidden>true</hidden>
    <requiredCountAtGameStart>0</requiredCountAtGameStart>
    <maxConfigurableAtWorldCreation>0</maxConfigurableAtWorldCreation>
    <canMakeRandomly>false</canMakeRandomly>
    <settlementGenerationWeight>0</settlementGenerationWeight>
    <humanlikeFaction>true</humanlikeFaction>
    <canSiege>false</canSiege>
    <canStageAttacks>true</canStageAttacks>
    <techLevel>Spacer</techLevel>
    <allowedCultures><li>Astropolitan</li></allowedCultures>
    <backstoryFilters>
      <li>
        <categories>
          <li>NR_Netrun</li>
          <li>Offworld</li>
        </categories>
      </li>
    </backstoryFilters>
    <allowedArrivalTemperatureRange>-40~45</allowedArrivalTemperatureRange>
    <raidLootMaker>PirateRaidLootMaker</raidLootMaker>
    <maxPawnCostPerTotalPointsCurve>
      <points>
        <li>(0,35)</li>
        <li>(70, 50)</li>
        <li>(700, 100)</li>
        <li>(1300, 150)</li>
        <li>(100000, 10000)</li>
      </points>
    </maxPawnCostPerTotalPointsCurve>
  </FactionDef>

'''

CREW_GROUPS = '''      <li>
        <kindDef>Combat</kindDef>
        <commonality>100</commonality>
        <options>
          <NR_CrewRunner>10</NR_CrewRunner>
          <NR_CrewMuscle>6</NR_CrewMuscle>
          <Mercenary_Gunner>3</Mercenary_Gunner>
        </options>
      </li>'''

CORP_GROUPS = '''      <li>
        <kindDef>Combat</kindDef>
        <commonality>100</commonality>
        <options>
          <NR_CorpGuard>10</NR_CorpGuard>
          <NR_CorpNetrunner>3</NR_CorpNetrunner>
          <Mercenary_Elite>3</Mercenary_Elite>
        </options>
      </li>
      <li>
        <kindDef>Combat</kindDef>
        <commonality>40</commonality>
        <options>
          <NR_CorpGuard>6</NR_CorpGuard>
          <Mercenary_Heavy>4</Mercenary_Heavy>
          <Mercenary_Sniper>3</Mercenary_Sniper>
        </options>
      </li>'''


def rulepack(defname, rules):
    lines = "\n".join(f"        <li>{r}</li>" for r in rules)
    return f'''  <RulePackDef>
    <defName>{defname}</defName>
    <rulePack>
      <rulesStrings>
{lines}
      </rulesStrings>
    </rulePack>
  </RulePackDef>

'''


def main():
    out = [HEADER, pawn_kinds(), BASE]
    for key, label, names, c1, c2 in CREWS:
        out.append(rulepack(f"NR_NamerCrew_{key}", [f"r_name->{n}" for n in names]))
        out.append(faction(f"NR_Crew_{key}", "runner crew",
                           "A crew of netrunners who live in the net and answer to no one. With good relations they send help; with bad ones they get in the way, in the net and at your door.",
                           "runner", "runners", f"NR_NamerCrew_{key}", f"Crew_{key}", c1, c2, CREW_GROUPS))
    out.append(rulepack("NR_NamerCorp", [f"corp->{w}" for w in CORP_WORDS]))
    for key, word, label, desc, c1, c2, may in CORPS:
        out.append(rulepack(f"NR_NamerCorp_{key}", [f"r_name->[corp] {word}"] + [f"corp->{w}" for w in CORP_WORDS]))
        out.append(faction(f"NR_Corp_{key}", label, desc, "corporate guard", "corporate guards", f"NR_NamerCorp_{key}",
                           f"Corp_{key}", c1, c2, CORP_GROUPS, may=may))
    out.append("</Defs>\n")
    open(os.path.join(ROOT, "Defs", "NetFactions.xml"), "w", encoding="utf-8").write("".join(out))

    ru = ['<?xml version="1.0" encoding="utf-8"?>\n<!-- Generated by tools/gen_factions.py -->\n<LanguageData>\n\n']
    for key, *_ in CREWS:
        ru.append(f"  <NR_Crew_{key}.label>команда раннеров</NR_Crew_{key}.label>\n")
        ru.append(f"  <NR_Crew_{key}.description>Команда нетраннеров, которая живёт в сети и никому не подчиняется. При хороших отношениях присылает помощь, при плохих мешает, в сети и у ваших дверей.</NR_Crew_{key}.description>\n")
        ru.append(f"  <NR_Crew_{key}.pawnSingular>раннер</NR_Crew_{key}.pawnSingular>\n  <NR_Crew_{key}.pawnsPlural>раннеры</NR_Crew_{key}.pawnsPlural>\n")
    for key, *_ in CORPS:
        label, desc = RU_CORP[key]
        ru.append(f"  <NR_Corp_{key}.label>{label}</NR_Corp_{key}.label>\n  <NR_Corp_{key}.description>{desc}</NR_Corp_{key}.description>\n")
        ru.append(f"  <NR_Corp_{key}.pawnSingular>корп-охранник</NR_Corp_{key}.pawnSingular>\n  <NR_Corp_{key}.pawnsPlural>корп-охрана</NR_Corp_{key}.pawnsPlural>\n")
    ru.append("\n</LanguageData>\n")
    path = os.path.join(ROOT, "Languages", "Russian (Русский)", "DefInjected", "FactionDef", "NetFactions.xml")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, "w", encoding="utf-8").write("".join(ru))

    kinds = ['<?xml version="1.0" encoding="utf-8"?>\n<LanguageData>\n',
             "  <NR_CrewRunner.label>раннер</NR_CrewRunner.label>\n",
             "  <NR_CrewMuscle.label>боец команды</NR_CrewMuscle.label>\n",
             "  <NR_CorpGuard.label>корп-охранник</NR_CorpGuard.label>\n",
             "  <NR_CorpNetrunner.label>корп-нетраннер</NR_CorpNetrunner.label>\n",
             "</LanguageData>\n"]
    path = os.path.join(ROOT, "Languages", "Russian (Русский)", "DefInjected", "PawnKindDef", "NetFactions.xml")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, "w", encoding="utf-8").write("".join(kinds))
    print("ok")


if __name__ == "__main__":
    main()
