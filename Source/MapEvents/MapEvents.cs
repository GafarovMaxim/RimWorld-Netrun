using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Netrun;

/// <summary>Numbers for the map events (docs/PHASE5_MAPEVENTS.md), in Defs/MapEvents.xml.</summary>
public class MapEventsSettingsDef : Def
{
    public float courierDays = 1f;
    public IntRange courierHuntersHours = new(4, 8);
    public float courierHuntersPoints = 0.5f;
    /// <summary>Cyberdata in the courier's pack: the colony's if the courier dies.</summary>
    public IntRange courierCyberdata = new(6, 10);
    public int courierSilver = 250;
    public float courierReputation = 10f;
    public float courierFailHeat = 10f;
    public float courierFailReputation = 5f;

    public float blackMarketDays = 2f;
    public float blackMarketReputation = 30f;
    public float blackMarketCoreChance = 0.3f;

    public float distressWorkHours = 2f;
    public float distressFragmentChance = 0.7f;
    public int distressSteel = 30;

    public int mapHackTurrets = 3;
    public float mapHackHours = 12f;
    public float mapHackWinHeat = 5f;
    public int mapHackLoseBreakdowns = 2;

    /// <summary>
    /// Map events come from their own schedule (not the storyteller's Misc pool, where they were picked almost never):
    /// on average one every this many days, divided by the "map events" frequency setting.
    /// </summary>
    public float mapEventMtbDays = 5f;

}

/// <summary>
/// Keeps what map events leave running: the data courier, the black market window and the counter-hack
/// after a hack on the colony's turrets.
/// </summary>
public class MapEventsManager : GameComponent, INetrunCleanable
{
    private static MapEventsSettingsDef Settings => NetrunDefOf.NR_MapEvents;

    private Pawn _courier;
    private int _courierUntilTick = -1;
    private int _blackMarketUntilTick = -1;
    public bool BlackMarketCore;
    private List<Thing> _hijacked = [];
    private int _counterUntilTick = -1;
    private Map _counterMap;

    public MapEventsManager(Game game)
    {
    }

    public static MapEventsManager Get() => Current.Game?.GetComponent<MapEventsManager>();

    // ---- Courier ----

    public void StartCourier(Pawn courier)
    {
        _courier = courier;
        _courierUntilTick = Find.TickManager.TicksGame + (int)(Settings.courierDays * GenDate.TicksPerDay);
    }

    private void CheckCourier()
    {
        if (_courier == null)
            return;
        if (_courier.Dead || _courier.Downed)
        {
            HeatTracker.Get()?.AddHeat(Settings.courierFailHeat, "NR_CourierLostLog".Translate(), HeatSource.Event);
            FixerManager.Get()?.Fixers.RandomElementWithFallback()?.GainReputation(-Settings.courierFailReputation);
            Find.LetterStack.ReceiveLetter("NR_CourierLostLabel".Translate(), "NR_CourierLost".Translate(_courier.LabelShort), LetterDefOf.NegativeEvent, _courier);
            _courier = null;
            return;
        }
        if (Find.TickManager.TicksGame < _courierUntilTick)
            return;

        Map map = _courier.MapHeld ?? Find.AnyPlayerHomeMap;
        if (map != null)
        {
            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = Settings.courierSilver;
            DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, [silver], forbid: false);
        }
        Fixer fixer = FixerManager.Get()?.Fixers.RandomElementWithFallback();
        fixer?.GainReputation(Settings.courierReputation);
        Find.LetterStack.ReceiveLetter("NR_CourierDoneLabel".Translate(),
            "NR_CourierDone".Translate(_courier.LabelShort, Settings.courierSilver, fixer?.Name ?? "-", Settings.courierReputation.ToString("0")),
            LetterDefOf.PositiveEvent);
        _courier = null;
    }

    // ---- Black market ----

    private List<ThingDef> _marketSold = [];

    public void MarkBlackMarketSold(ThingDef def) => _marketSold.Add(def);

    /// <summary>A unique braindance recording on sale at this black market, or null.</summary>
    public ThingDef BlackMarketUnique;

    public int BlackMarketTicksLeft => System.Math.Max(0, _blackMarketUntilTick - Find.TickManager.TicksGame);
    public bool BlackMarketOpen => Find.TickManager.TicksGame < _blackMarketUntilTick;

    public void OpenBlackMarket()
    {
        _blackMarketUntilTick = Find.TickManager.TicksGame + (int)(Settings.blackMarketDays * GenDate.TicksPerDay);
        BlackMarketCore = Rand.Chance(Settings.blackMarketCoreChance);
        _marketSold.Clear();
        BlackMarketUnique = Rand.Chance(NetrunDefOf.NR_UniqueBraindance.blackMarketChance) ? UniqueBraindanceManager.Get()?.RandomUnfound() : null;
    }

    /// <summary>What a fixer sells on top of the usual stock while the black market is open (reputation 30+).</summary>
    public IEnumerable<ThingDef> BlackMarketStock(Fixer fixer)
    {
        if (!BlackMarketOpen || fixer.Reputation < Settings.blackMarketReputation)
            yield break;
        // Each black market item sells once per market.
        if (!_marketSold.Contains(NetrunDefOf.NR_BlackBraindanceChip))
            yield return NetrunDefOf.NR_BlackBraindanceChip;
        if (!_marketSold.Contains(NetrunDefOf.NR_AIFragment))
            yield return NetrunDefOf.NR_AIFragment;
        if (BlackMarketCore && !_marketSold.Contains(NetrunDefOf.NR_AICoreModule))
            yield return NetrunDefOf.NR_AICoreModule;
        // Rarely, one unique recording, at its (high) market value; gone once bought.
        if (BlackMarketUnique != null && !_marketSold.Contains(BlackMarketUnique))
            yield return BlackMarketUnique;
    }

    // ---- Hack on the colony's turrets and the counter-hack ----

    public int Hijack(Map map)
    {
        List<CompFlickable> turrets = map.listerBuildings.allBuildingsColonist
            .Where(b => b.def.building?.IsTurret == true)
            .Select(b => b.GetComp<CompFlickable>())
            .Where(f => f != null && f.SwitchIsOn)
            .InRandomOrder().Take(Settings.mapHackTurrets).ToList();
        foreach (CompFlickable flick in turrets)
            flick.SwitchIsOn = false;
        _hijacked = turrets.Select(f => (Thing)f.parent).ToList();
        _counterUntilTick = Find.TickManager.TicksGame + (int)(Settings.mapHackHours * GenDate.TicksPerHour);
        _counterMap = map;
        return turrets.Count;
    }

    public bool CounterHackRunning => _counterUntilTick >= 0;

    public void CounterHackWon()
    {
        foreach (Thing turret in _hijacked.Where(t => t is { Destroyed: false }))
        {
            CompFlickable flick = turret.TryGetComp<CompFlickable>();
            if (flick != null)
                flick.SwitchIsOn = true;
        }
        HeatTracker.Get()?.ReduceHeat(Settings.mapHackWinHeat, "NR_CounterHackLog".Translate());
        Find.LetterStack.ReceiveLetter("NR_CounterHackWonLabel".Translate(), "NR_CounterHackWon".Translate(), LetterDefOf.PositiveEvent);
        ClearCounterHack();
    }

    private void CheckCounterHack()
    {
        if (_counterUntilTick < 0 || Find.TickManager.TicksGame < _counterUntilTick)
            return;
        Map map = _counterMap ?? Find.AnyPlayerHomeMap;
        if (map != null)
            IncidentWorker_NetBreakdown.BreakSome(map, Settings.mapHackLoseBreakdowns, NetrunDefOf.NR_MapHack);
        ClearCounterHack();
    }

    private void ClearCounterHack()
    {
        _hijacked.Clear();
        _counterUntilTick = -1;
        _counterMap = null;
        GatewayManager.Get()?.RemoveCounterHackNodes();
    }

    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % GenTicks.TickRareInterval != 0)
            return;
        CheckCourier();
        CheckCounterHack();
        TryScheduledEvent();
    }

    // ---- Our own schedule for map events ----

    public static readonly string[] Scheduled = ["NR_DataCourier", "NR_BlackMarket", "NR_DistressSignal", "NR_MapHack"];

    private static void TryScheduledEvent()
    {
        float freq = NetrunMod.Settings.MapEventFrequency;
        if (freq <= 0f)
            return;
        float mtbTicks = Settings.mapEventMtbDays * GenDate.TicksPerDay / freq;
        if (!Rand.Chance(GenTicks.TickRareInterval / mtbTicks))
            return;
        Map map = Find.Maps.Where(m => m.IsPlayerHome).RandomElementWithFallback();
        if (map == null)
            return;
        var options = Candidates(map).Where(c => c.why == null).ToList();
        if (options.Count == 0)
            return;
        (IncidentDef def, IncidentParms parms, string _) = options.RandomElementByWeight(o => o.def.baseChance);
        def.Worker.TryExecute(parms);
    }

    /// <summary>Each scheduled map event with its parms and why it cannot fire now (null if it can).</summary>
    public static IEnumerable<(IncidentDef def, IncidentParms parms, string why)> Candidates(Map map)
    {
        foreach (string name in Scheduled)
        {
            IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(name);
            if (def == null)
                continue;
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
            string why = null;
            if (!NetworkGate.Allows(def))
                why = "newcomer protection";
            else if (map.StoryState.lastFireTicks.TryGetValue(def, out int last) && Find.TickManager.TicksGame - last < def.minRefireDays * GenDate.TicksPerDay)
                why = $"refire: {((def.minRefireDays * GenDate.TicksPerDay - (Find.TickManager.TicksGame - last)) / (float)GenDate.TicksPerDay):0.#} days left";
            else if (!def.Worker.CanFireNow(parms))
                why = "conditions not met";
            yield return (def, parms, why);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref _courier, "courier");
        Scribe_Values.Look(ref _courierUntilTick, "courierUntilTick", -1);
        Scribe_Values.Look(ref _blackMarketUntilTick, "blackMarketUntilTick", -1);
        Scribe_Defs.Look(ref BlackMarketUnique, "blackMarketUnique");
        Scribe_Collections.Look(ref _marketSold, "marketSold", LookMode.Def);
        _marketSold ??= [];
        Scribe_Values.Look(ref BlackMarketCore, "blackMarketCore");
        Scribe_Collections.Look(ref _hijacked, "hijacked", LookMode.Reference);
        Scribe_Values.Look(ref _counterUntilTick, "counterUntilTick", -1);
        Scribe_References.Look(ref _counterMap, "counterMap");
        _hijacked ??= [];
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            _hijacked.RemoveAll(t => t == null);
    }

    public int Cleanup(CleanupReason reason)
    {
        int n = _hijacked.RemoveAll(t => t == null || t.Destroyed);
        if (_courier != null && (_courier.Destroyed || _courier.Discarded))
        {
            _courier = null;
            n++;
        }
        return n;
    }
}

// ---- Incidents ----

/// <summary>A fixer's data courier stays a day; mercenaries come for the data. Keep the courier standing.</summary>
public class IncidentWorker_DataCourier : IncidentWorker
{
    private static MapEventsSettingsDef Settings => NetrunDefOf.NR_MapEvents;

    private static Faction CourierFaction() => Find.FactionManager.AllFactionsVisible
        .Where(f => !f.IsPlayer && f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer) && !f.defeated && !f.Hidden)
        .RandomElementWithFallback();

    protected override bool CanFireNowSub(IncidentParms parms)
        => parms.target is Map && FixerManager.Get()?.Fixers.Count > 0 && CourierFaction() != null && MapEventsManager.Get() != null;

    protected override bool TryExecuteWorker(IncidentParms parms)
    {
        var map = (Map)parms.target;
        Faction faction = CourierFaction();
        if (faction == null || !RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entry, map, CellFinder.EdgeRoadChance_Friendly))
            return false;

        Pawn courier = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Villager, faction));
        // The hot data rides in the courier's pack: it falls out if the courier falls.
        Thing data = ThingMaker.MakeThing(NetrunDefOf.USH_Cyberdata);
        data.stackCount = Settings.courierCyberdata.RandomInRange;
        courier.inventory?.TryAddItemNotForSale(data);
        GenSpawn.Spawn(courier, entry, map);
        IntVec3 chill = DropCellFinder.TradeDropSpot(map);
        LordMaker.MakeNewLord(faction, new LordJob_VisitColony(faction, chill, (int)(Settings.courierDays * GenDate.TicksPerDay)), map, [courier]);
        MapEventsManager.Get().StartCourier(courier);

        // The hunters follow.
        IncidentParms raid = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
        raid.forced = true;
        raid.points *= Settings.courierHuntersPoints;
        Find.Storyteller.incidentQueue.Add(new QueuedIncident(new FiringIncident(NetrunDefOf.NR_MercRaid, null, raid),
            Find.TickManager.TicksGame + Settings.courierHuntersHours.RandomInRange * GenDate.TicksPerHour));

        HeatTracker.Get()?.Note("NR_CourierLabel".Translate(), HeatSource.Event);
        SendStandardLetter("NR_CourierLabel".Translate(), "NR_Courier".Translate(courier.LabelShort, Settings.courierSilver, data.stackCount), LetterDefOf.NeutralEvent, parms, courier);
        return true;
    }
}

/// <summary>A corporate team comes for the AI server: a mercenary raid at Target Heat and above.</summary>
public class IncidentWorker_CorpHunt : IncidentWorker_MercRaid
{
    protected override bool CanFireNowSub(IncidentParms parms)
        => (HeatTracker.Get()?.Level ?? HeatLevel.Shadow) >= HeatLevel.Target
           && parms.target is Map map && map.listerBuildings.AllBuildingsColonistOfDef(NetrunDefOf.NR_AIServer).Any()
           && base.CanFireNowSub(parms);

    protected override string GetLetterText(IncidentParms parms, List<Pawn> pawns)
        => "NR_CorpHuntIntro".Translate() + "\n\n" + base.GetLetterText(parms, pawns);
}

/// <summary>The black market opens for a couple of days at fixers who trust the colony.</summary>
public class IncidentWorker_BlackMarket : IncidentWorker
{
    protected override bool CanFireNowSub(IncidentParms parms)
        => MapEventsManager.Get() is { BlackMarketOpen: false } && FixerManager.Get()?.Fixers.Count > 0;

    protected override bool TryExecuteWorker(IncidentParms parms)
    {
        MapEventsManager.Get().OpenBlackMarket();
        float rep = NetrunDefOf.NR_MapEvents.blackMarketReputation;
        List<Fixer> trusted = FixerManager.Get().Fixers.Where(f => f.Reputation >= rep).ToList();
        string who = trusted.Count == 0 ? "NR_BlackMarketNobody".Translate(rep.ToString("0")) : trusted.Select(f => f.Name).ToCommaList();
        HeatTracker.Get()?.Note("NR_BlackMarketLabel".Translate(), HeatSource.Event);
        SendStandardLetter("NR_BlackMarketLabel".Translate(), "NR_BlackMarketLetter".Translate(NetrunDefOf.NR_MapEvents.blackMarketDays.ToString("0"), who),
            LetterDefOf.PositiveEvent, parms, LookTargets.Invalid);
        return true;
    }
}

/// <summary>An abandoned server turns up on the map; a colonist can connect to it.</summary>
/// <summary>
/// The distress signal: an abandoned server somewhere on the world map, a few days away. Send a caravan with a hacker to
/// connect to it before the signal dies out.
/// </summary>
public class IncidentWorker_DistressSignal : IncidentWorker
{
    private static QuestScriptDef Quest => DefDatabase<QuestScriptDef>.GetNamedSilentFail("NR_DistressQuest");

    protected override bool CanFireNowSub(IncidentParms parms)
        => parms.target is Map map && Quest != null && DefDatabase<SitePartDef>.GetNamedSilentFail("NR_DistressSite") != null
           && RimWorld.Planet.TileFinder.TryFindNewSiteTile(out RimWorld.Planet.PlanetTile _, map.Tile, 2, 10);

    /// <summary>A vanilla quest: it shows in the Quests tab, has its own letter, timeout and site.</summary>
    protected override bool TryExecuteWorker(IncidentParms parms)
    {
        var map = (Map)parms.target;
        var slate = new RimWorld.QuestGen.Slate();
        slate.Set("points", parms.points);
        slate.Set("map", map);
        Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(Quest, slate);
        if (quest == null)
            return false;
        QuestUtility.SendLetterQuestAvailable(quest);
        HeatTracker.Get()?.Note("NR_DistressLabel".Translate(), HeatSource.Event);
        return true;
    }
}

/// <summary>On the distress site's map: the abandoned server near the centre with a couple of dead racks around it.</summary>
public class GenStep_DistressServer : GenStep
{
    public override int SeedPart => 0x4E52_4453;

    public override void Generate(Map map, GenStepParams parms)
    {
        if (!RCellFinder.TryFindRandomCellNearTheCenterOfTheMapWith(c => GenAdj.OccupiedRect(c, Rot4.North, NetrunDefOf.NR_DistressServer.size).All(x => x.InBounds(map) && x.Standable(map) && x.GetFirstBuilding(map) == null), map, out IntVec3 cell))
            cell = map.Center;
        foreach (IntVec3 c in GenAdj.OccupiedRect(cell, Rot4.North, NetrunDefOf.NR_DistressServer.size).ExpandedBy(1))
            if (c.InBounds(map))
                foreach (Thing t in c.GetThingList(map).Where(t => t.def.destroyable && (t.def.category == ThingCategory.Building || t.def.category == ThingCategory.Plant)).ToList())
                    t.Destroy();
        Thing server = GenSpawn.Spawn(ThingMaker.MakeThing(NetrunDefOf.NR_DistressServer), cell, map);
        BuildCompound(map, server);
        // A couple of dead racks around it.
        ThingDef rackDef = DefDatabase<ThingDef>.GetNamedSilentFail("NR_DistressRack");
        if (rackDef != null)
        {
            var around = GenAdj.OccupiedRect(cell, Rot4.North, NetrunDefOf.NR_DistressServer.size).ExpandedBy(1).EdgeCells
                .Where(c => c.InBounds(map) && c.Standable(map) && c.GetFirstBuilding(map) == null).InRandomOrder().Take(2).ToList();
            foreach (IntVec3 c in around)
                GenSpawn.Spawn(ThingMaker.MakeThing(rackDef), c, map);
        }
        // The way in is in sight when the caravan arrives; the inside stays dark until someone opens the door.
        foreach (IntVec3 c in GenRadial.RadialCellsAround(server.Position + new IntVec3(0, 0, -CompoundHalfZ - 3), 6f, true))
            if (c.InBounds(map))
                map.fogGrid.Unfog(c);
    }

    private const int CompoundHalfX = 6;
    private const int CompoundHalfZ = 5;

    /// <summary>
    /// A sealed steel compound around the server: walls, one door on the south side, a metal floor, turrets inside
    /// and by the door (Odyssey's armored turrets when it is there, otherwise mechanoid auto mini-turrets), all hostile;
    /// with Odyssey, a few dormant hunter drones hidden outside.
    /// </summary>
    private static void BuildCompound(Map map, Thing server)
    {
        IntVec3 c = server.Position;
        var rect = new CellRect(c.x - CompoundHalfX, c.z - CompoundHalfZ, CompoundHalfX * 2 + 1, CompoundHalfZ * 2 + 1);
        if (!rect.ExpandedBy(4).InBounds(map))
            return;
        Faction hostile = Faction.OfAncientsHostile ?? Faction.OfMechanoids;

        // Clear the ground (keep the server and its racks).
        foreach (IntVec3 cell in rect.ExpandedBy(1))
            foreach (Thing t in cell.GetThingList(map).Where(t => t != server && t.def.defName != "NR_DistressRack" && t.def.destroyable
                         && (t.def.category is ThingCategory.Building or ThingCategory.Plant or ThingCategory.Item)).ToList())
                t.Destroy();
        TerrainDef floor = DefDatabase<TerrainDef>.GetNamedSilentFail("MetalTile") ?? TerrainDefOf.Concrete;
        foreach (IntVec3 cell in rect)
            if (map.terrainGrid.TerrainAt(cell).affordances?.Contains(TerrainAffordanceDefOf.Heavy) == true || map.terrainGrid.TerrainAt(cell).passability != Traversability.Impassable)
                map.terrainGrid.SetTerrain(cell, floor);

        IntVec3 door = new(c.x, 0, rect.minZ);
        foreach (IntVec3 cell in rect.EdgeCells)
        {
            if (cell == door)
                continue;
            Thing wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Steel);
            GenSpawn.Spawn(wall, cell, map);
        }
        Thing d = ThingMaker.MakeThing(ThingDefOf.Door, ThingDefOf.Steel);
        GenSpawn.Spawn(d, door, map);
        d.SetFaction(hostile);

        // Turrets: two inside the far corners, two outside by the door.
        ThingDef turret = DefDatabase<ThingDef>.GetNamedSilentFail("Turret_AncientArmoredTurret") ?? DefDatabase<ThingDef>.GetNamedSilentFail("Turret_AutoMiniTurret");
        if (turret != null)
        {
            IntVec3[] spots =
            [
                new(rect.minX + 2, 0, rect.maxZ - 2), new(rect.maxX - 2, 0, rect.maxZ - 2),
                new(door.x - 3, 0, door.z - 3), new(door.x + 3, 0, door.z - 3),
            ];
            foreach (IntVec3 spot in spots)
            {
                if (!spot.InBounds(map) || !spot.Standable(map) || spot.GetFirstBuilding(map) != null)
                    continue;
                Thing t = ThingMaker.MakeThing(turret, turret.MadeFromStuff ? GenStuff.DefaultStuffFor(turret) : null);
                t.SetFaction(hostile);
                GenSpawn.Spawn(t, spot, map);
            }
        }

        // Odyssey: dormant hunter drones in the approach, never right next to each other.
        if (ModsConfig.OdysseyActive && DefDatabase<ThingDef>.GetNamedSilentFail("HunterDroneTrap") is { } trap)
        {
            var placed = new List<IntVec3>();
            foreach (IntVec3 spot in GenRadial.RadialCellsAround(door, 9f, false).Where(x => x.z < door.z - 1).InRandomOrder())
            {
                if (placed.Count >= 3)
                    break;
                if (!spot.InBounds(map) || !spot.Standable(map) || spot.GetFirstBuilding(map) != null || placed.Any(p => p.DistanceTo(spot) < 3f))
                    continue;
                Thing t = ThingMaker.MakeThing(trap, trap.MadeFromStuff ? GenStuff.DefaultStuffFor(trap) : null);
                t.SetFaction(hostile);
                GenSpawn.Spawn(t, spot, map);
                placed.Add(spot);
            }
        }
    }
}

/// <summary>Removes Hacking Expansion's random data center from the distress map: one AI find per event.</summary>
public class GenStep_DistressCleanup : GenStep
{
    public override int SeedPart => 0x4E52_4443;

    public override void Generate(Map map, GenStepParams parms)
    {
        foreach (Thing t in map.listerThings.AllThings.Where(t => t.def.defName is "USH_DataCenter" or "USH_RewardDataCenter").ToList())
            t.Destroy();
    }
}

/// <summary>Hackers take over the colony's turrets; a counter-hack node appears in the Gateway.</summary>
public class IncidentWorker_MapHack : IncidentWorker
{
    protected override bool CanFireNowSub(IncidentParms parms)
        => parms.target is Map map
           && (HeatTracker.Get()?.Level ?? HeatLevel.Shadow) >= HeatLevel.Tracked
           && MapEventsManager.Get() is { CounterHackRunning: false }
           && GatewayManager.Runner(map, out _) != null
           && map.listerBuildings.allBuildingsColonist.Any(b => b.def.building?.IsTurret == true && b.GetComp<CompFlickable>() != null);

    protected override bool TryExecuteWorker(IncidentParms parms)
    {
        var map = (Map)parms.target;
        int count = MapEventsManager.Get().Hijack(map);
        if (count == 0)
            return false;
        GatewayManager.Get()?.AddCounterHackNode(NetrunDefOf.NR_MapEvents.mapHackHours);
        HeatTracker.Get()?.Note("NR_MapHackLabel".Translate(), HeatSource.Raid);
        SendStandardLetter("NR_MapHackLabel".Translate(), "NR_MapHackLetter".Translate(count, NetrunDefOf.NR_MapEvents.mapHackHours.ToString("0")),
            LetterDefOf.ThreatSmall, parms, LookTargets.Invalid);
        return true;
    }
}

// ---- Buildings ----

public class CompProperties_DistressServer : CompProperties
{
    public CompProperties_DistressServer() => compClass = typeof(CompDistressServer);
}

/// <summary>The abandoned server from a distress signal: a colonist connects to it once.</summary>
public class CompDistressServer : ThingComp
{
    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        string label = "NR_DistressConnect".Translate();
        if (selPawn.WorkTagIsDisabled(WorkTags.Intellectual))
        {
            yield return new FloatMenuOption(label + " (" + "NR_AwakenNoWorker".Translate() + ")", null);
            yield break;
        }
        if (!selPawn.CanReserveAndReach(parent, PathEndMode.Touch, Danger.Some))
        {
            yield return new FloatMenuOption(label + " (" + "NoPath".Translate() + ")", null);
            yield break;
        }
        yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label,
            () => selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(NetrunDefOf.NR_ConnectDistressServer, parent), JobTag.Misc)), selPawn, parent);
    }

    /// <summary>The colonist finished: a fragment or an ICE backlash, then the server falls apart.</summary>
    public void Resolve(Pawn pawn)
    {
        MapEventsSettingsDef s = NetrunDefOf.NR_MapEvents;
        Map map = parent.Map;
        IntVec3 pos = parent.Position;
        if (Rand.Chance(s.distressFragmentChance))
        {
            Thing fragment = GenSpawn.Spawn(ThingMaker.MakeThing(NetrunDefOf.NR_AIFragment), pos, map);
            Messages.Message("NR_DistressFragment".Translate(pawn.LabelShort), fragment, MessageTypeDefOf.PositiveEvent, historical: true);
        }
        else
        {
            HediffDef nausea = DefDatabase<HediffDef>.GetNamedSilentFail("USH_CyberspaceNausea");
            if (nausea != null)
                pawn.health.AddHediff(nausea, pawn.health.hediffSet.GetBrain());
            Messages.Message("NR_DistressBacklash".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NegativeEvent, historical: true);
        }
        parent.Destroy();
        Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
        steel.stackCount = s.distressSteel;
        GenPlace.TryPlaceThing(steel, pos, map, ThingPlaceMode.Near);
    }
}

/// <summary>A: the abandoned server. Walk there and work for a couple of hours.</summary>
public class JobDriver_ConnectDistressServer : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
        Toil work = Toils_General.Wait((int)(NetrunDefOf.NR_MapEvents.distressWorkHours * GenDate.TicksPerHour), TargetIndex.A);
        work.WithProgressBarToilDelay(TargetIndex.A);
        work.WithEffect(EffecterDefOf.Hacking, TargetIndex.A);
        work.tickAction = () => pawn.skills?.Learn(SkillDefOf.Intellectual, 0.05f);
        work.activeSkill = () => SkillDefOf.Intellectual;
        yield return work;
        Toil finish = ToilMaker.MakeToil("ConnectDistressServer");
        finish.initAction = () => job.targetA.Thing?.TryGetComp<CompDistressServer>()?.Resolve(pawn);
        finish.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return finish;
    }
}
