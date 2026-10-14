using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>The mod's starts (docs/CONCEPTS_10.md, section 4).</summary>
public enum NetrunStart
{
    None,
    LastNode,
    CorpRunaways,
    PirateStudio,
    FlyingNode,
}

/// <summary>
/// The scenario part that marks a Netrun start. The colonists, items and research are vanilla parts in
/// Defs/Scenarios.xml; this part only hands the start to <see cref="NetrunScenario"/>, which sets up the mod's side
/// once the game is running (all GameComponents ready).
/// </summary>
public class ScenPart_NetrunStart : ScenPart
{
    public NetrunStart start;

    public override string Summary(Scenario scen) => ("NR_ScenSummary_" + start).Translate();

    public override void PostGameStart() => NetrunScenario.Get()?.Begin(start);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref start, "start");
    }

    public override int GetHashCode() => base.GetHashCode() ^ (int)start;
}

/// <summary>
/// Sets up a Netrun start: buildings near the colonists, the partner's Echo, the hunting corporation, the fixers.
/// Keeps the hunting corporation's Alert from cooling for the first month (the "Corporate runaways" threat).
/// </summary>
public class NetrunScenario : GameComponent
{
    public NetrunStart Start;
    private bool _applied = true;
    private int _ownerFactionId = -1;
    private int _alertFloorUntil = -1;
    private bool _markFortress;

    public NetrunScenario(Game game)
    {
    }

    public static NetrunScenario Get() => GameComp<NetrunScenario>.Get();

    public void Begin(NetrunStart start)
    {
        Start = start;
        _applied = start == NetrunStart.None;
    }

    public override void GameComponentTick()
    {
        int now = Find.TickManager.TicksGame;
        if (!_applied && now >= 60)
        {
            _applied = true;
            try
            {
                Apply();
            }
            catch (System.Exception e)
            {
                Log.Error("[Netrun] scenario setup failed: " + e);
            }
        }
        if (now % GenDate.TicksPerHour == 1777 && _ownerFactionId >= 0)
            Hourly(now);
    }

    private void Hourly(int now)
    {
        CorpState c = CorpManager.Get()?.Corps.FirstOrDefault(x => x.FactionId == _ownerFactionId);
        if (c == null)
        {
            _ownerFactionId = -1;
            return;
        }
        if (now < _alertFloorUntil && c.Alert < CorpManager.Settings.intrusionAlert)
            c.Alert = CorpManager.Settings.intrusionAlert;
        if (_markFortress && NetMapManager.Get() is { Ready: true } net)
        {
            MapNode fortress = net.Map.Nodes.FirstOrDefault(n => n.Fortress && n.OwnerFaction == _ownerFactionId);
            if (fortress != null)
            {
                net.MarkedNodeId = fortress.Id;
                net.UpdateVisibility();
                _markFortress = false;
            }
        }
        if (now >= _alertFloorUntil && !_markFortress)
            _ownerFactionId = -1;
    }

    private void Apply()
    {
        Map map = Find.AnyPlayerHomeMap;
        if (map == null)
            return;
        IntVec3 center = map.mapPawns.FreeColonistsSpawned.FirstOrDefault()?.Position ?? map.Center;
        int now = Find.TickManager.TicksGame;

        switch (Start)
        {
            case NetrunStart.LastNode:
            {
                DropPacked(DefDatabase<ThingDef>.GetNamedSilentFail("USH_Cyberpod"), map, center);
                DropPacked(NetrunDefOf.NR_AIServer, map, center);
                string partner = ((string)"NR_Scen_PartnerNames".Translate()).Split(',').Select(s => s.Trim()).Where(s => s.Length > 0)
                    .RandomElementWithFallback("Kestrel");
                Thing fragment = AIManager.Get()?.CreateScenarioEcho(partner, map, center);
                // The partner's last recording: the Echo's "Who am I" starts from it.
                Thing raw = ThingMaker.MakeThing(NetrunDefOf.NR_BraindanceRaw);
                CompBraindanceRecord rec = raw.TryGetComp<CompBraindanceRecord>();
                if (rec != null)
                {
                    rec.Author = partner;
                    rec.Event = "NR_Scen_PartnerRecordEvent".Translate();
                    rec.Mood = BraindanceMood.Dark;
                    rec.Strength = 0.7f;
                }
                GenPlace.TryPlaceThing(raw, center, map, ThingPlaceMode.Near);
                Find.LetterStack.ReceiveLetter("NR_Scen_LastNodeLabel".Translate(), "NR_Scen_LastNodeText".Translate(partner),
                    LetterDefOf.NeutralEvent, new LookTargets(fragment ?? raw));
                break;
            }
            case NetrunStart.CorpRunaways:
            {
                HeatTracker.Get()?.DevSetHeat(60f, "NR_Scen_HeatReason".Translate());
                CorpManager corps = CorpManager.Get();
                corps?.Sync();
                CorpState owner = corps?.Corps.Where(c => c.Faction is { defeated: false }).RandomElementWithFallback();
                if (owner != null)
                {
                    owner.Alert = CorpManager.Settings.intrusionAlert;
                    owner.NextIntrusionTick = now + Rand.RangeInclusive(10, 15) * GenDate.TicksPerDay;
                    _ownerFactionId = owner.FactionId;
                    _alertFloorUntil = now + 30 * GenDate.TicksPerDay;
                    _markFortress = true;
                }
                Mercenaries.QueueRaid(Rand.RangeInclusive(20, 30) * GenDate.TicksPerDay);
                FixerManager fixers = FixerManager.Get();
                Fixer cleaner = fixers?.Fixers.FirstOrDefault(f => f.Specialty == FixerSpecialty.Cleaner) ?? fixers?.Fixers.FirstOrDefault();
                if (cleaner != null)
                {
                    cleaner.Specialty = FixerSpecialty.Cleaner;
                    cleaner.Reputation = UnityEngine.Mathf.Max(cleaner.Reputation, 30f);
                }
                Find.LetterStack.ReceiveLetter("NR_Scen_RunawaysLabel".Translate(),
                    "NR_Scen_Runaways".Translate(owner?.Faction?.Name ?? "?", cleaner?.Name ?? "?"), LetterDefOf.ThreatSmall);
                break;
            }
            case NetrunStart.PirateStudio:
            {
                FixerManager fixers = FixerManager.Get();
                foreach (Fixer f in fixers?.Fixers ?? Enumerable.Empty<Fixer>())
                    f.Reputation = UnityEngine.Mathf.Max(f.Reputation, 10f);
                Fixer client = fixers?.Fixers.FirstOrDefault(f => f.Specialty != FixerSpecialty.Imperial) ?? fixers?.Fixers.FirstOrDefault();
                ContractManager.Get()?.OfferOfType(client, ContractType.CleanLab);
                var drops = new List<Thing>();
                foreach (ThingDef unique in UniqueBraindance.All.InRandomOrder().Take(2))
                    drops.Add(ThingMaker.MakeThing(unique));
                foreach (Thing t in drops)
                    GenPlace.TryPlaceThing(t, center, map, ThingPlaceMode.Near);
                Thing box = SpawnBuilding(DefDatabase<ThingDef>.GetNamedSilentFail("USH_BlackBox"), map, center, null, minRadius: 12f);
                Find.LetterStack.ReceiveLetter("NR_Scen_StudioLabel".Translate(), "NR_Scen_Studio".Translate(client?.Name ?? "?"),
                    LetterDefOf.PositiveEvent, new LookTargets(box ?? drops.FirstOrDefault()));
                break;
            }
            case NetrunStart.FlyingNode:
            {
                Thing pod = DropPacked(DefDatabase<ThingDef>.GetNamedSilentFail("USH_Cyberpod"), map, center);
                Find.LetterStack.ReceiveLetter("NR_Scen_FlyingLabel".Translate(), "NR_Scen_Flying".Translate(), LetterDefOf.NeutralEvent,
                    pod != null ? new LookTargets(pod) : null);
                break;
            }
        }
    }

    /// <summary>
    /// A player building handed over packed, next to the colonists: the player picks the spot and installs it.
    /// Falls back to the built thing when the def cannot be minified. Null if the def is missing.
    /// </summary>
    private static Thing DropPacked(ThingDef def, Map map, IntVec3 center)
    {
        if (def == null)
            return null;
        Thing building = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
        building.SetFaction(Faction.OfPlayer);
        Thing item = def.Minifiable ? building.MakeMinified() : building;
        if (GenPlace.TryPlaceThing(item, center, map, ThingPlaceMode.Near, out Thing placed))
            return placed;
        Log.Warning($"[Netrun] scenario: no room for {def.defName} near the start.");
        return null;
    }

    /// <summary>A finished unowned building (the black box wreck) on a clear spot at least minRadius away. Null if no spot.</summary>
    private static Thing SpawnBuilding(ThingDef def, Map map, IntVec3 center, Faction faction, float minRadius = 3f)
    {
        if (def == null)
            return null;
        Rot4 rot = def.rotatable ? Rot4.South : Rot4.North;
        IntVec3 spot = GenRadial.RadialCellsAround(center, minRadius, 30f)
            .FirstOrFallback(c => c.InBounds(map) && Clear(def, map, c, rot), IntVec3.Invalid);
        if (!spot.IsValid)
        {
            Log.Warning($"[Netrun] scenario: no room for {def.defName} near the start.");
            return null;
        }
        Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
        if (faction != null)
            thing.SetFaction(faction);
        return GenSpawn.Spawn(thing, spot, map, rot);
    }

    private static bool Clear(ThingDef def, Map map, IntVec3 at, Rot4 rot)
    {
        CellRect rect = GenAdj.OccupiedRect(at, rot, def.Size);
        if (!rect.InBounds(map))
            return false;
        foreach (IntVec3 c in rect.ExpandedBy(1).ClipInsideMap(map))
        {
            if (c.Fogged(map) || c.GetEdifice(map) != null || c.GetFirstPawn(map) != null)
                return false;
        }
        foreach (IntVec3 c in rect)
        {
            if (!c.Standable(map) || c.GetFirstItem(map) != null)
                return false;
            TerrainDef terrain = c.GetTerrain(map);
            if (def.terrainAffordanceNeeded != null && !terrain.affordances.Contains(def.terrainAffordanceNeeded))
                return false;
        }
        if (def.hasInteractionCell)
        {
            IntVec3 ic = ThingUtility.InteractionCellWhenAt(def, at, rot, map);
            if (!ic.InBounds(map) || !ic.Standable(map) || rect.Contains(ic))
                return false;
        }
        return true;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref Start, "start");
        Scribe_Values.Look(ref _applied, "applied", true);
        Scribe_Values.Look(ref _ownerFactionId, "ownerFaction", -1);
        Scribe_Values.Look(ref _alertFloorUntil, "alertFloorUntil", -1);
        Scribe_Values.Look(ref _markFortress, "markFortress");
    }
}
