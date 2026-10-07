using System.Collections.Generic;
using System.Linq;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// What Netrun remembers about a netrunner between dives (docs/CONCEPTS_10.md, section 14): the clean-dive streak for
/// "ghost hand", the last dive for "alien in flesh", and the hours spent in a pod in one go (atrophy, a missing spouse).
/// Keyed by the pawn's thingIDNumber, never by reference (docs/TECH_DETAILS.md); the janitor drops the dead.
/// </summary>
public class PawnNetState : GameComponent, INetrunCleanable
{
    private const int TickInterval = GenDate.TicksPerHour;
    private const int MaxEntries = 200;

    private class Entry : IExposable
    {
        public int CleanStreak;
        public int LastDiveTick = -1;
        public int PodHours;

        public void ExposeData()
        {
            Scribe_Values.Look(ref CleanStreak, "cleanStreak");
            Scribe_Values.Look(ref LastDiveTick, "lastDiveTick", -1);
            Scribe_Values.Look(ref PodHours, "podHours");
        }
    }

    private Dictionary<int, Entry> _entries = new();
    private List<int> _keys;
    private List<Entry> _values;

    public PawnNetState(Game game)
    {
    }

    public static PawnNetState Get() => Current.Game?.GetComponent<PawnNetState>();

    private Entry For(Pawn pawn, bool create)
    {
        if (pawn == null)
            return null;
        if (_entries.TryGetValue(pawn.thingIDNumber, out Entry e) || !create)
            return e;
        e = new Entry();
        _entries[pawn.thingIDNumber] = e;
        return e;
    }

    public int CleanStreak(Pawn pawn) => For(pawn, false)?.CleanStreak ?? 0;

    public int LastDiveTick(Pawn pawn) => For(pawn, false)?.LastDiveTick ?? -1;

    /// <summary>Called when a dive ends: the streak grows on a dive without an ICE hit and breaks on one with a hit.</summary>
    public void NotifyDive(Pawn pawn, int iceHits)
    {
        Entry e = For(pawn, true);
        if (e == null)
            return;
        e.CleanStreak = iceHits == 0 ? e.CleanStreak + 1 : 0;
        e.LastDiveTick = Find.TickManager.TicksGame;
    }

    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % TickInterval != 37)
            return;

        var inPod = new HashSet<int>();
        foreach (Map map in Find.Maps)
        {
            foreach (Building_Cyberpod pod in CyberpodUtility.PlayerPods(map))
            {
                if (pod.ContainedThing is not Pawn pawn || pawn.Faction != Faction.OfPlayer)
                    continue;
                inPod.Add(pawn.thingIDNumber);
                For(pawn, true).PodHours++;
            }
        }

        // Out of the pod since the last check: the long stay leaves its marks.
        foreach (KeyValuePair<int, Entry> pair in _entries)
        {
            if (pair.Value.PodHours <= 0 || inPod.Contains(pair.Key))
                continue;
            int hours = pair.Value.PodHours;
            pair.Value.PodHours = 0;
            Pawn pawn = FindPawn(pair.Key);
            if (pawn != null)
                AfterLongStay(pawn, hours);
        }
    }

    private static Pawn FindPawn(int id)
        => PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction.FirstOrDefault(p => p.thingIDNumber == id);

    private static void AfterLongStay(Pawn pawn, int hours)
    {
        PawnNetSettings s = NetrunDefOf.NR_Cyberspace.pawnNet;
        if (hours >= s.atrophyHours && DefDatabase<HediffDef>.GetNamedSilentFail("NR_PodAtrophy") is { } atrophy && pawn.health != null)
        {
            if (pawn.health.hediffSet.GetFirstHediffOfDef(atrophy) is { } old)
                pawn.health.RemoveHediff(old);
            pawn.health.AddHediff(atrophy);
        }
        if (hours >= s.missedSpouseHours && DefDatabase<ThoughtDef>.GetNamedSilentFail("NR_MissedSpouse") is { } missed && pawn.relations != null)
        {
            foreach (DirectPawnRelation rel in pawn.relations.DirectRelations)
            {
                if (rel.def != PawnRelationDefOf.Spouse && rel.def != PawnRelationDefOf.Lover && rel.def != PawnRelationDefOf.Fiance)
                    continue;
                if (rel.otherPawn is { Dead: false, Spawned: true } other)
                    other.needs?.mood?.thoughts?.memories?.TryGainMemory(missed, pawn);
            }
        }
    }

    public int Cleanup(CleanupReason reason)
    {
        if (reason == CleanupReason.Loaded)
            return 0;
        var alive = new HashSet<int>(PawnsFinder.AllMapsWorldAndTemporary_Alive.Select(p => p.thingIDNumber));
        int removed = 0;
        foreach (int id in _entries.Keys.ToList())
        {
            if (alive.Contains(id))
                continue;
            _entries.Remove(id);
            removed++;
        }
        // Cap: drop the ones that dived longest ago.
        if (_entries.Count > MaxEntries)
        {
            foreach (int id in _entries.OrderBy(p => p.Value.LastDiveTick).Take(_entries.Count - MaxEntries).Select(p => p.Key).ToList())
            {
                _entries.Remove(id);
                removed++;
            }
        }
        return removed;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref _entries, "entries", LookMode.Value, LookMode.Deep, ref _keys, ref _values);
        _entries ??= new Dictionary<int, Entry>();
    }
}

/// <summary>Numbers for the pawn side of dives, inside the cyberspace settings def.</summary>
public class PawnNetSettings
{
    public int atrophyHours = 72;
    public int missedSpouseHours = 24;
    public int veteranCleanExits = 60;
    public int ghostHandStreak = 30;
    public int burntCount = 12;
    public int alienDives = 150;
    public int alienOfflineDays = 3;
    public float veteranHackBonus = 0.10f;
    public float veteranDecayFactor = 0.8f;
    public float ghostHandTraceFactor = 0.85f;
    public float storyWeight = 0.04f;
    public float formerFixerReputation = 10f;
    public float burnedTesterNetsick = 0.1f;
    public float naturalNetrunnerBackstoryChance = 0.25f;
    public int forgeBandwidth = 2;
    public float forgeBandwidthTrust = 60f;
    public float mechHackTraceFactor = 0.5f;
}
