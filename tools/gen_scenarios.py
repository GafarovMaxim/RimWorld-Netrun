"""The mod's scenarios and their pawn kinds -> Defs/Scenarios.xml. Run: python tools/gen_scenarios.py"""
import os
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
def kind(dn, label, bs, implant=None, fighter=False, weaponTags=None):
    ext = f'''    <modExtensions>
      <li Class="Netrun.NetPawnKindExtension">
        <brainImplant>{implant}</brainImplant>
      </li>
    </modExtensions>
''' if implant else ''
    return f'''  <PawnKindDef>
    <defName>{dn}</defName>
    <label>{label}</label>
    <race>Human</race>
    <defaultFactionDef>PlayerColony</defaultFactionDef>
    <combatPower>30</combatPower>
    <isFighter>false</isFighter>
    <minGenerationAge>20</minGenerationAge>
    <forceNormalGearQuality>true</forceNormalGearQuality>
    <apparelIgnoreSeasons>true</apparelIgnoreSeasons>
    <chemicalAddictionChance>0</chemicalAddictionChance>
    <backstoryCryptosleepCommonality>0</backstoryCryptosleepCommonality>
    <apparelMoney>350~600</apparelMoney>
    <apparelTags>
      <li>IndustrialBasic</li>
    </apparelTags>
    <fixedAdultBackstories>
      <li>{bs}</li>
    </fixedAdultBackstories>
{ext}  </PawnKindDef>
'''
kinds = [
  ("NR_ScenStreetNetrunner","street netrunner","NR_Bs_StreetNetrunner","USH_InstalledAdvancedCyberlink"),
  ("NR_ScenNetrunner","netrunner","NR_Bs_StreetNetrunner","USH_InstalledCyberlink"),
  ("NR_ScenCorpCoder","corporate coder","NR_Bs_CorpCoder","USH_InstalledCyberlink"),
  ("NR_ScenCorpGuard","corporate guard","NR_Bs_CorpGuard",None),
  ("NR_ScenBraindanceTester","braindance tester","NR_Bs_BraindanceTester",None),
  ("NR_ScenFormerFixer","former fixer","NR_Bs_FormerFixer",None),
  ("NR_ScenDataCourier","data courier","NR_Bs_DataCourier",None),
]
def thing(d, c, scatter=False, stuff=None):
    if scatter:
        return f'''      <li Class="ScenPart_ScatterThingsNearPlayerStart">
        <def>ScatterThingsNearPlayerStart</def>
        <thingDef>{d}</thingDef>
        <count>{c}</count>
      </li>
'''
    st = f'\n        <stuff>{stuff}</stuff>' if stuff else ''
    return f'''      <li Class="ScenPart_StartingThing_Defined">
        <def>StartingThing_Defined</def>
        <thingDef>{d}</thingDef>{st}
        <count>{c}</count>
      </li>
'''
def research(p):
    return f'''      <li Class="ScenPart_StartingResearch">
        <def>StartingResearch</def>
        <project>{p}</project>
      </li>
'''
def scen(dn, label, desc, summary, pawns, choice, arrive, start, items, res, mayreq=''):
    kc=''.join(f'''          <li>
            <kindDef>{k}</kindDef>
            <count>{n}</count>
            <requiredAtStart>true</requiredAtStart>
          </li>
''' for k,n in pawns)
    return f'''  <ScenarioDef{mayreq}>
    <defName>{dn}</defName>
    <label>{label}</label>
    <description>{desc}</description>
    <scenario>
      <summary>{summary}</summary>
      <playerFaction>
        <def>PlayerFaction</def>
        <factionDef>PlayerColony</factionDef>
      </playerFaction>
      <parts>
      <li Class="ScenPart_ConfigPage_ConfigureStartingPawns_KindDefs">
        <def>NR_ConfigurePawnsKinds</def>
        <pawnChoiceCount>{choice}</pawnChoiceCount>
        <kindCounts>
{kc}        </kindCounts>
      </li>
      <li Class="ScenPart_PlayerPawnsArriveMethod">
        <def>PlayerPawnsArriveMethod</def>
        <method>{arrive}</method>
      </li>
      <li Class="Netrun.ScenPart_NetrunStart">
        <def>NR_NetrunStart</def>
        <start>{start}</start>
      </li>
      <li Class="ScenPart_GameStartDialog">
        <def>GameStartDialog</def>
        <textKey>NR_ScenIntro_{start}</textKey>
        <closeSound>GameStartSting</closeSound>
      </li>
{''.join(res)}{''.join(items)}      </parts>
    </scenario>
  </ScenarioDef>
'''
basic = [thing("Silver",500),thing("MealSurvivalPack",20),thing("MedicineIndustrial",10),thing("ComponentIndustrial",10),
         thing("Steel",400,True),thing("WoodLog",300,True)]
out=['<?xml version="1.0" encoding="utf-8"?>','<!-- The mod\'s starts (docs/CONCEPTS_10.md, section 4). Pawn kinds give the starting colonists their backstories','     and cyberlinks; Netrun.ScenPart_NetrunStart sets up the mod\'s side once the game runs. -->','<Defs>','',
'''  <!-- The vanilla "pawns by kind" page under our own part def (its def name in vanilla is not checked from here). -->
  <ScenPartDef>
    <defName>NR_ConfigurePawnsKinds</defName>
    <label>start with specific colonists</label>
    <category>Fixed</category>
    <scenPartClass>ScenPart_ConfigPage_ConfigureStartingPawns_KindDefs</scenPartClass>
    <pageClass>Page_ConfigureStartingPawns</pageClass>
    <canBeRandomlyAdded>false</canBeRandomlyAdded>
    <maxUses>1</maxUses>
  </ScenPartDef>

  <ScenPartDef>
    <defName>NR_NetrunStart</defName>
    <label>netrun start</label>
    <category>Fixed</category>
    <scenPartClass>Netrun.ScenPart_NetrunStart</scenPartClass>
    <canBeRandomlyAdded>false</canBeRandomlyAdded>
    <maxUses>1</maxUses>
  </ScenPartDef>
''']
out += [kind(*k) for k in kinds]
out.append(scen("NR_Scen_LastNode","The last node",
 "A lone street netrunner with an advanced cyberlink holes up next to an abandoned AI server. In the fragment by the pod sleeps the imprint of a partner who did not make it out. Hard: one colonist, one ghost.",
 "One netrunner and the ghost of a partner. Hard.",
 [("NR_ScenStreetNetrunner",1)],6,"Standing","LastNode",
 [thing("USH_Cyberdata",20),thing("Silver",300),thing("MealSurvivalPack",15),thing("MedicineIndustrial",8),thing("ComponentIndustrial",8),thing("Gun_Autopistol",1),thing("Steel",300,True),thing("WoodLog",200,True)],
 [research("USH_BasicCybertech"),research("USH_CybertechMachinery")]))
out.append(scen("NR_Scen_CorpRunaways","Corporate runaways",
 "A corporate coder, a guard and a burned braindance tester walked out of a corporation with its secrets. The corporation wants them back: its first strike through the net comes in two weeks, mercenaries a little later. Lay low first, then take revenge: its fortress is marked on the cyberspace map.",
 "Three runaways with a corporation on their trail. Tense.",
 [("NR_ScenCorpCoder",1),("NR_ScenCorpGuard",1),("NR_ScenBraindanceTester",1)],8,"DropPods","CorpRunaways",
 [thing("USH_AdvancedCyberdata",5),thing("ComponentSpacer",4),thing("Gun_AssaultRifle",1),thing("Gun_Autopistol",1),thing("NR_BraindanceGoggles",1)]+basic,
 [research("USH_BasicCybertech"),research("NR_Res_NetSecurity")]))
out.append(scen("NR_Scen_PirateStudio","Pirate studio",
 "A former fixer and a braindance tester set up a pirate studio with two friends who never touched the net. Nobody knows them yet, every fixer gives them a chance, and the first client already wants a batch of clean chips. A satellite black box fell nearby.",
 "Four colonists, a braindance studio and the fixers' trust. Economy first, dives later.",
 [("NR_ScenFormerFixer",1),("NR_ScenBraindanceTester",1),("Colonist",2)],8,"Standing","PirateStudio",
 [thing("NR_BraindanceGoggles",2),thing("NR_BraindanceChip",3),thing("Silver",700),thing("Gun_Revolver",1)]+basic[1:],
 [research("NR_Res_Neurorecording")]))
out.append(scen("NR_Scen_FlyingNode","Flying node",
 "A netrunner crew lives on a gravship with a cyberpod on board. While the ship is in orbit, the orbital backbone nodes of cyberspace are in reach: richer data, harder ICE. (Odyssey)",
 "A gravship with a cyberpod on board. Orbital nodes from the start. (Odyssey)",
 [("NR_ScenNetrunner",1),("NR_ScenDataCourier",1),("Colonist",1)],8,"Gravship","FlyingNode",
 [thing("USH_Cyberdata",15)]+basic,
 [research("USH_BasicCybertech"),research("USH_CybertechMachinery")],' MayRequire="Ludeon.RimWorld.Odyssey"'))
out.append('</Defs>\n')
open(os.path.join(ROOT,'Defs/Scenarios.xml'),'w').write('\n'.join(out))
