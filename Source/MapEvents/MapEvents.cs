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
    /// <summary>Below this many raid points the hunters bring small arms only (no grenadiers, no launchers) for a young colony.</summary>
    public float courierHuntersExplosivesPoints = 500f;
    /// <summary>Cyberdata in the courier's pack: the colony's if the courier dies.</summary>
    public IntRange courierCyberdata = new(6, 10);
    /// <summary>The courier's pay: threat points times this, worth at least courierSilver and at most courierRewardMax.</summary>
    public int courierSilver = 250;
    public float courierRewardPerPoint = 0.6f;
    public int courierRewardMax = 3000;
    public float courierReputation = 10f;
    public float courierFailHeat = 10f;
    public float courierFailReputation = 5f;
    /// <summary>How long the fixer's request waits in the letter before the courier goes elsewhere.</summary>
    public float courierOfferHours = 12f;

    public float blackMarketDays = 2f;
    public float blackMarketReputation = 30f;
    public float blackMarketCoreChance = 0.3f;

    public float distressWorkHours = 2f;
    public float distressFragmentChance = 0.7f;

    public int mapHackTurrets = 3;
    public float mapHackHours = 12f;
    public float mapHackWinHeat = 5f;
    public int mapHackLoseBreakdowns = 2;

    /// <summary>
    /// Map events come from their own schedule (not the storyteller's Misc pool, where they were picked almost never):
    /// on average one every this many days, divided by the "map events" frequency setting.
    /// </summary>
    public float mapEventMtbDays = 5f;

    /// <summary>Extra cyberdata from the first encrypted crate opened at home within this many days (the newcomer's start).</summary>
    public int starterCrateCyberdata = 22;
    public float starterCrateDays = 15f;

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
    private int _courierReward;
    private int _huntersTick = -1;
    private Map _huntersMap;
    private int _huntersTries;
    /// <summary>The map the hunters are fighting on, until none of them is a threat any more.</summary>
    private Map _huntersFighting;
    /// <summary>The hunters came and were beaten: a healthy courier need not wait out the day.</summary>
    private bool _huntersBeaten;
    /// <summary>A hunter was seen on the map: drop pods still in the air must not count as a beaten raid.</summary>
    private bool _huntersSeen;
    private int _huntersFightTick = -1;
    private int _blackMarketUntilTick = -1;
    public bool BlackMarketCore;
    private List<Thing> _hijacked = [];
    private int _counterUntilTick = -1;
    private Map _counterMap;

    public MapEventsManager(Game game)
    {
    }

    public static MapEventsManager Get() => GameComp<MapEventsManager>.Get();

    // ---- Courier ----

    /// <summary>One courier at a time: a second one would overwrite the first one's hunters (a player's report).</summary>
    public bool CourierBusy => _courier != null || _huntersTick >= 0;

    public void StartCourier(Pawn courier, int huntersInTicks, int reward)
    {
        _courier = courier;
        _courierReward = reward;
        // A new job starts clean: a fight left over from the last courier must not count for this one.
        _huntersBeaten = false;
        _huntersFighting = null;
        _huntersSeen = false;
        _courierUntilTick = Find.TickManager.TicksGame + (int)(Settings.courierDays * GenDate.TicksPerDay);
        _huntersTick = Find.TickManager.TicksGame + huntersInTicks;
        _huntersMap = courier.Map;
        _huntersTries = 0;
        Log.Message($"[Netrun] Courier {courier.LabelShort} arrived; hunters due in {huntersInTicks.ToStringTicksToPeriod()} (tick {_huntersTick}).");
    }

    /// <summary>Dev: the hunters come at the next rare tick, on the current map if no courier is around.</summary>
    public void DevHuntersNow()
    {
        _huntersTick = Find.TickManager.TicksGame;
        _huntersMap ??= Find.CurrentMap;
        _huntersTries = 0;
    }

    /// <summary>The mercenaries after the courier's data: fired directly when their time comes.</summary>
    private void CheckHunters()
    {
        if (_huntersTick < 0 || Find.TickManager.TicksGame < _huntersTick)
            return;
        _huntersTick = -1;
        Map map = _huntersMap != null && Find.Maps.Contains(_huntersMap) ? _huntersMap : _courier?.MapHeld ?? Find.AnyPlayerHomeMap;
        if (map == null)
        {
            Log.Warning("[Netrun] The courier's hunters had no map to come to.");
            _huntersMap = null;
            return;
        }
        IncidentParms raid = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
        raid.forced = true;
        raid.points *= Settings.courierHuntersPoints;
        raid.pawnGroupKind = raid.points < Settings.courierHuntersExplosivesPoints ? NetrunDefOf.NR_CourierHuntLight : NetrunDefOf.NR_CourierHunt;
        bool arrived = false;
        try
        {
            arrived = NetrunDefOf.NR_MercRaid.Worker.TryExecute(raid);
        }
        catch (System.Exception e)
        {
            Log.Error("[Netrun] The courier's hunters threw: " + e);
        }
        if (arrived)
        {
            Log.Message($"[Netrun] The courier's hunters arrived ({raid.points:0} points, {raid.raidStrategy?.defName}, {raid.raidArrivalMode?.defName}).");
            _huntersFighting = map;
            _huntersSeen = false;
            _huntersFightTick = Find.TickManager.TicksGame;
            _huntersMap = null;
            return;
        }
        // A raid that cannot start now (no spawn spot, colonists away) tries again in an hour, three times.
        Log.Warning($"[Netrun] The courier's hunters could not arrive (try {_huntersTries + 1}/3): faction {Mercenaries.Faction?.Name ?? "none"}, "
                    + $"points {raid.points:0}, colonists on map {map.mapPawns.FreeColonistsSpawnedCount}.");
        if (++_huntersTries < 3)
            _huntersTick = Find.TickManager.TicksGame + GenDate.TicksPerHour;
        else
            _huntersMap = null;
    }

    /// <summary>The hunters are beaten once none of them on the map is still an active threat (dead, downed, fled or captured).</summary>
    private void CheckHuntersBeaten()
    {
        if (_huntersFighting == null)
            return;
        Faction mercs = Mercenaries.Faction;
        bool onMap = Find.Maps.Contains(_huntersFighting);
        if (onMap && mercs != null && _huntersFighting.mapPawns.SpawnedPawnsInFaction(mercs).Any(p => GenHostility.IsActiveThreatTo(p, Faction.OfPlayer)))
        {
            _huntersSeen = true;
            return;
        }
        // None seen yet: they may still be landing; after a day without them the raid simply does not count.
        if (!_huntersSeen && onMap && Find.TickManager.TicksGame - _huntersFightTick < GenDate.TicksPerDay)
            return;
        _huntersFighting = null;
        _huntersBeaten = _huntersSeen;
    }

    /// <summary>
    /// An awake enemy still fighting the colony on this map. Not counted (Maxim, 2026-10-23): sleeping mech clusters and
    /// other dormant pawns, insects guarding their hive, shamblers, anything still in the fog.
    /// </summary>
    private static bool OtherThreats(Map map) => map.mapPawns.AllPawnsSpawned.Any(p =>
        p.HostileTo(Faction.OfPlayer) && GenHostility.IsActiveThreatTo(p, Faction.OfPlayer) && !p.Fogged()
        && p.GetComp<CompCanBeDormant>() is not { Awake: false } && p.GetLord()?.LordJob is not LordJob_DefendAndExpandHive && !p.IsShambler);

    private const int DownedGraceDays = 3;

    private void CheckCourier()
    {
        if (_courier == null)
            return;
        // Death, being carried off, taken prisoner or left lying 3 days past his time loses the job: a courier who falls
        // to his wounds can be rescued and still finish it.
        if (_courier.Dead || _courier.MapHeld == null || _courier.IsPrisonerOfColony
            || (_courier.Downed && Find.TickManager.TicksGame - _courierUntilTick > DownedGraceDays * GenDate.TicksPerDay))
        {
            HeatTracker.Get()?.AddHeat(Settings.courierFailHeat, "NR_CourierLostLog".Translate(), HeatSource.Event);
            FixerManager.Get()?.Fixers.RandomElementWithFallback()?.GainReputation(-Settings.courierFailReputation);
            Find.LetterStack.ReceiveLetter("NR_CourierLostLabel".Translate(), "NR_CourierLost".Translate(_courier.LabelShort), LetterDefOf.NegativeEvent, _courier);
            _courier = null;
            return;
        }
        if (_courier.Downed)
            return;
        int left = _courierUntilTick - Find.TickManager.TicksGame;
        // The raid is beaten and the courier is unhurt: no reason to wait out the day (Maxim, 2026-10-23).
        // Not while anything else on the map is still fighting the colony.
        if (left > 0 && _huntersBeaten && _courier.Spawned && !_courier.health.HasHediffsNeedingTend() && !HealthAIUtility.ShouldSeekMedicalRest(_courier)
            && !OtherThreats(_courier.Map))
        {
            _courier.GetLord()?.RemovePawn(_courier);
            left = 0;
        }
        if (left > 0)
        {
            // Downed pawns drop out of their lord: back on his feet (and out of bed), the courier hides again.
            if (_courier.Spawned && !_courier.InBed() && _courier.GetLord()?.LordJob is not LordJob_CourierHide)
                IncidentWorker_DataCourier.Hide(_courier, left);
            return;
        }

        if (_courier.Spawned && _courier.GetLord() == null)
            LordMaker.MakeNewLord(_courier.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk), _courier.Map, [_courier]);
        int reward = _courierReward > 0 ? _courierReward : Settings.courierSilver;
        Fixer fixer = FixerManager.Get()?.Fixers.RandomElementWithFallback();
        fixer?.GainReputation(Settings.courierReputation);
        var letter = (ChoiceLetter_CourierReward)LetterMaker.MakeLetter(NetrunDefOf.NR_CourierReward);
        letter.Label = "NR_CourierDoneLabel".Translate();
        letter.Text = "NR_CourierDone".Translate(_courier.LabelShort, reward, fixer?.Name ?? "-", Settings.courierReputation.ToString("0"));
        letter.Map = _courier.MapHeld ?? Find.AnyPlayerHomeMap;
        letter.Options = CourierReward.Roll(reward);
        Find.LetterStack.ReceiveLetter(letter);
        _courier = null;
        _huntersBeaten = false;
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
        CheckHuntersBeaten();
        CheckCourier();
        CheckHunters();
        CheckCounterHack();
        TryScheduledEvent();
        if (Find.TickManager.TicksGame % EventPacing.CheckInterval == 0)
            EventPacing.Get()?.Check();
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
        // Worker.TryExecute does not record the fire (the storyteller does that), so minRefireDays would never hold.
        if (def.Worker.TryExecute(parms))
            map.StoryState.Notify_IncidentFired(new FiringIncident(def, null, parms));
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
        Scribe_Values.Look(ref _courierReward, "courierReward");
        Scribe_References.Look(ref _huntersFighting, "huntersFighting");
        Scribe_Values.Look(ref _huntersBeaten, "huntersBeaten");
        Scribe_Values.Look(ref _huntersSeen, "huntersSeen");
        Scribe_Values.Look(ref _huntersFightTick, "huntersFightTick", -1);
        Scribe_Values.Look(ref _huntersTick, "huntersTick", -1);
        Scribe_References.Look(ref _huntersMap, "huntersMap");
        Scribe_Values.Look(ref _huntersTries, "huntersTries");
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

/// <summary>
/// A fixer asks to shelter a data courier for a day; mercenaries come for the data. The player accepts or refuses in
/// the letter (Maxim, 2026-10-14). Keep the courier standing.
/// </summary>
public class IncidentWorker_DataCourier : IncidentWorker
{
    private static MapEventsSettingsDef Settings => NetrunDefOf.NR_MapEvents;

    private static Faction CourierFaction() => Find.FactionManager.AllFactionsVisible
        .Where(f => !f.IsPlayer && f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer) && !f.defeated && !f.Hidden)
        .RandomElementWithFallback();

    protected override bool CanFireNowSub(IncidentParms parms)
        => parms.target is Map && FixerManager.Get()?.Fixers.Count > 0 && CourierFaction() != null && MapEventsManager.Get() is { CourierBusy: false };

    protected override bool TryExecuteWorker(IncidentParms parms)
    {
        var map = (Map)parms.target;
        if (CourierFaction() == null || MapEventsManager.Get() is not { CourierBusy: false })
            return false;
        var letter = (ChoiceLetter_CourierOffer)LetterMaker.MakeLetter(NetrunDefOf.NR_CourierOffer);
        letter.Label = "NR_CourierOfferLabel".Translate();
        letter.Text = "NR_CourierOfferText".Translate(CourierReward.Value(map), Settings.courierHuntersHours.min, Settings.courierHuntersHours.max);
        letter.Map = map;
        letter.StartTimeout((int)(Settings.courierOfferHours * GenDate.TicksPerHour));
        EventPacing.SendChoice(letter);
        return true;
    }

    /// <summary>
    /// The courier lies low indoors: the dining room or rec room first, else any other room but a prison cell, and never
    /// one too cold or hot for him, like a freezer (Maxim, 2026-10-17); null if there is none.
    /// </summary>
    private static IntVec3? HidingSpot(Map map, Pawn courier)
    {
        RoomRoleDef dining = DefDatabase<RoomRoleDef>.GetNamedSilentFail("DiningRoom");
        RoomRoleDef rec = DefDatabase<RoomRoleDef>.GetNamedSilentFail("RecRoom");
        FloatRange safe = courier.SafeTemperatureRange();
        List<Room> rooms = map.regionGrid.AllRooms.Where(r => !r.PsychologicallyOutdoors && !r.IsPrisonCell && r.CellCount >= 4 && safe.Includes(r.Temperature)).ToList();
        foreach (Room room in rooms.InRandomOrder().OrderByDescending(r => r.Role != null && (r.Role == dining || r.Role == rec)))
        {
            if (room.Cells.Where(c => c.Standable(map) && c.GetFirstBuilding(map) == null).TryRandomElement(out IntVec3 cell)
                && map.reachability.CanReach(courier.Position, cell, PathEndMode.OnCell, TraverseParms.For(courier)))
                return cell;
        }
        return null;
    }

    /// <summary>Puts the courier (back) under the hiding lord for the rest of his stay.</summary>
    public static void Hide(Pawn courier, int ticks)
    {
        courier.GetLord()?.RemovePawn(courier);
        IntVec3 spot = HidingSpot(courier.Map, courier) ?? DropCellFinder.TradeDropSpot(courier.Map);
        LordMaker.MakeNewLord(courier.Faction, new LordJob_CourierHide(spot, ticks), courier.Map, [courier]);
    }

    /// <summary>The player took the job: the courier walks in and the hunters' clock starts.</summary>
    public static void Arrive(Map map)
    {
        if (MapEventsManager.Get() is not { CourierBusy: false })
        {
            Messages.Message("NR_CourierBusy".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }
        Faction faction = CourierFaction();
        if (map == null || faction == null || !RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entry, map, CellFinder.EdgeRoadChance_Friendly))
        {
            Messages.Message("NR_CourierNoWay".Translate(), MessageTypeDefOf.NeutralEvent, historical: false);
            return;
        }

        Pawn courier = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Villager, faction));
        // The hot data rides in the courier's pack: it falls out if the courier falls.
        Thing data = ThingMaker.MakeThing(NetrunDefOf.USH_Cyberdata);
        data.stackCount = Settings.courierCyberdata.RandomInRange;
        courier.inventory?.TryAddItemNotForSale(data);
        GenSpawn.Spawn(courier, entry, map);
        Hide(courier, (int)(Settings.courierDays * GenDate.TicksPerDay));
        // The hunters follow on our own clock, not the storyteller's queue (a queued raid that fails its checks is
        // dropped silently, which is how the mercenaries went missing).
        int reward = CourierReward.Value(map);
        MapEventsManager.Get().StartCourier(courier, Settings.courierHuntersHours.RandomInRange * GenDate.TicksPerHour, reward);

        HeatTracker.Get()?.Note("NR_CourierLabel".Translate(), HeatSource.Event);
        Find.LetterStack.ReceiveLetter("NR_CourierLabel".Translate(), "NR_Courier".Translate(courier.LabelShort, reward, data.stackCount), LetterDefOf.NeutralEvent, courier);
    }
}

/// <summary>The fixer's request: shelter the courier (mercenaries follow) or refuse. Ignored, it lapses.</summary>
public class ChoiceLetter_CourierOffer : ChoiceLetter
{
    public Map Map;

    public override IEnumerable<DiaOption> Choices
    {
        get
        {
            if (ArchivedOnly)
            {
                yield return Option_Close;
                yield break;
            }
            var accept = new DiaOption("NR_CourierAccept".Translate())
            {
                action = () =>
                {
                    IncidentWorker_DataCourier.Arrive(Map != null && Find.Maps.Contains(Map) ? Map : Find.AnyPlayerHomeMap);
                    Find.LetterStack.RemoveLetter(this);
                },
                resolveTree = true,
            };
            yield return accept;
            yield return new DiaOption("NR_CourierDecline".Translate())
            {
                action = () =>
                {
                    Messages.Message("NR_CourierDeclined".Translate(), MessageTypeDefOf.NeutralEvent, historical: false);
                    Find.LetterStack.RemoveLetter(this);
                },
                resolveTree = true,
            };
            yield return Option_Postpone;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref Map, "map");
    }
}

/// <summary>A corporate team comes for the AI server: a mercenary raid at Target Heat and above.</summary>
public class IncidentWorker_CorpHunt : IncidentWorker_MercRaid
{
    protected override bool CanFireNowSub(IncidentParms parms)
        => (HeatTracker.Get()?.Level ?? HeatLevel.Shadow) >= HeatLevel.Target
           && parms.target is Map map && map.listerBuildings.AllBuildingsColonistOfDef(NetrunDefOf.NR_AIServer).Any()
           && (parms.forced || EventPacing.Get()?.ThreatBlock() == null)
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
           // Only a colony with a netrunner hears the signal.
           && map.mapPawns.FreeColonists.Any(p => USH_HE.CyberUtils.IsHacker(p))
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
        Thing server = GenSpawn.Spawn(ThingMaker.MakeThing(NetrunDefOf.NR_DistressServer), cell, map, Rot4.South);
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

    // Big enough that the inside turrets stand past their own blast radius (3.9) from the server.
    private const int CompoundHalfX = 8;
    private const int CompoundHalfZ = 6;

    /// <summary>
    /// A sealed steel compound around the server: walls, one door on the south side, a metal floor, turrets inside
    /// and around the outside (Odyssey's armored turrets when it is there, otherwise mechanoid auto mini-turrets), all hostile;
    /// traps outside on every side (Odyssey's dormant hunter drones, otherwise IEDs).
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

        // Turrets: four in the inside corners, far enough that a destroyed one does not blow up the server;
        // six outside, two by the door and one or two on each other side.
        ThingDef turret = DefDatabase<ThingDef>.GetNamedSilentFail("Turret_AncientArmoredTurret") ?? DefDatabase<ThingDef>.GetNamedSilentFail("Turret_AutoMiniTurret");
        if (turret != null)
        {
            IntVec3[] spots =
            [
                new(rect.minX + 1, 0, rect.maxZ - 1), new(rect.maxX - 1, 0, rect.maxZ - 1),
                new(rect.minX + 1, 0, rect.minZ + 1), new(rect.maxX - 1, 0, rect.minZ + 1),
                new(door.x - 3, 0, door.z - 3), new(door.x + 3, 0, door.z - 3),
                new(rect.minX - 3, 0, c.z), new(rect.maxX + 3, 0, c.z),
                new(c.x - 4, 0, rect.maxZ + 3), new(c.x + 4, 0, rect.maxZ + 3),
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

        // Traps in a ring outside the walls, never right next to each other or to a wall.
        ThingDef trap = (ModsConfig.OdysseyActive ? DefDatabase<ThingDef>.GetNamedSilentFail("HunterDroneTrap") : null)
                        ?? DefDatabase<ThingDef>.GetNamedSilentFail("TrapIED_HighExplosive");
        if (trap != null)
        {
            CellRect inner = rect.ExpandedBy(2), outer = rect.ExpandedBy(8);
            var placed = new List<IntVec3>();
            foreach (IntVec3 spot in outer.Cells.Where(x => !inner.Contains(x)).InRandomOrder())
            {
                if (placed.Count >= 7)
                    break;
                if (!spot.InBounds(map) || !spot.Standable(map) || spot.GetFirstBuilding(map) != null || placed.Any(p => p.DistanceTo(spot) < 4f))
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

/// <summary>The abandoned server from a distress signal: a colonist connects to it once; the building stays (Maxim, 2026-10-16).</summary>
public class CompDistressServer : ThingComp
{
    private bool _drained;

    public override void PostExposeData() => Scribe_Values.Look(ref _drained, "drained");

    public override string CompInspectStringExtra() => _drained ? "NR_DistressDrained".Translate() : null;

    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        string label = "NR_DistressConnect".Translate();
        if (_drained)
        {
            yield return new FloatMenuOption(label + " (" + "NR_DistressDrained".Translate() + ")", null);
            yield break;
        }
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

    /// <summary>The colonist finished: a fragment next to the server or an ICE backlash; the server goes dark but stays.</summary>
    public void Resolve(Pawn pawn)
    {
        if (_drained)
            return;
        _drained = true;
        MapEventsSettingsDef s = NetrunDefOf.NR_MapEvents;
        if (Rand.Chance(AIBalance.FragmentChance(s.distressFragmentChance)))
        {
            Thing fragment = ThingMaker.MakeThing(NetrunDefOf.NR_AIFragment);
            GenPlace.TryPlaceThing(fragment, pawn.Position, parent.Map, ThingPlaceMode.Near);
            Messages.Message("NR_DistressFragment".Translate(pawn.LabelShort), fragment, MessageTypeDefOf.PositiveEvent, historical: true);
        }
        else
        {
            HediffDef nausea = DefDatabase<HediffDef>.GetNamedSilentFail("USH_CyberspaceNausea");
            if (nausea != null)
                pawn.health.AddHediff(nausea, pawn.health.hediffSet.GetBrain());
            Messages.Message("NR_DistressBacklash".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NegativeEvent, historical: true);
        }
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
