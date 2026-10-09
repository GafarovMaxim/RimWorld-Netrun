using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// The signal achievements listen to (docs/TECH_DETAILS.md, section 3.4). The main mod does not know about Vanilla
/// Achievements Expanded: the optional NetrunVAE assembly hooks <see cref="Counter"/> with Harmony.
/// </summary>
public static class NetrunSignals
{
    /// <summary>Called whenever a colony counter changes. Does nothing by itself.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Counter(string key, int value)
    {
    }

    /// <summary>The current value of a counter (for "unlock on startup" in old saves).</summary>
    public static int Value(string key) => NetrunCounters.Get()?.Value(key) ?? 0;

    public static void Increment(string key, int by = 1) => NetrunCounters.Get()?.Add(key, by);

    /// <summary>Keeps the highest value seen.</summary>
    public static void Max(string key, int value) => NetrunCounters.Get()?.SetMax(key, value);
}

/// <summary>
/// Colony counters for achievements: they survive colonists' deaths, unlike pawn records. A few checks once an hour
/// (Red Zone time, Shadow season, trust 100, pinned fixers, the Echo meeting its killer, the second year without AIs).
/// </summary>
public class NetrunCounters : GameComponent
{
    private Dictionary<string, int> _values = new();
    private List<string> _keys;
    private List<int> _vals;
    private HashSet<AIPersonality> _met = [];
    private int _redSinceTick = -1;
    private int _leftRedTick = -1;
    private int _shadowSinceTick = -1;
    private int _shadowHacks;

    public NetrunCounters(Game game)
    {
    }

    public static NetrunCounters Get() => GameComp<NetrunCounters>.Get();

    public int Value(string key) => _values.TryGetValue(key, out int v) ? v : 0;

    public void Add(string key, int by)
    {
        int v = Value(key) + by;
        _values[key] = v;
        Signal(key, v);
    }

    /// <summary>Debug: sets a counter and sends the signal (an achievement at its goal opens).</summary>
    public void DevSet(string key, int value)
    {
        _values[key] = value;
        Signal(key, value);
    }

    /// <summary>Debug: every counter and streak back to zero.</summary>
    public void DevReset()
    {
        _values.Clear();
        _met.Clear();
        _redSinceTick = _leftRedTick = _shadowSinceTick = -1;
        _shadowHacks = 0;
    }

    /// <summary>Sets a working value without a signal (a streak in progress).</summary>
    public void Set(string key, int value) => _values[key] = value;

    public void SetMax(string key, int value)
    {
        if (value <= Value(key))
            return;
        _values[key] = value;
        Signal(key, value);
    }

    private static void Signal(string key, int value)
    {
        try
        {
            NetrunSignals.Counter(key, value);
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] achievement signal failed: " + e.Message, 0x4E520040);
        }
    }

    public string DebugState()
        => _values.Count == 0 ? "-" : string.Join("\n", _values.OrderBy(kv => kv.Key).Select(kv => kv.Key + ": " + kv.Value));

    /// <summary>A new personality met (awakened or grown): counts toward "all thirteen".</summary>
    public void MetPersonality(AIPersonality p)
    {
        if (_met.Add(p))
            SetMax("Personalities", _met.Count);
    }

    /// <summary>A hack from the colony (for the Shadow season).</summary>
    public void NotifyHack() => _shadowHacks++;

    public override void GameComponentTick()
    {
        int now = Find.TickManager.TicksGame;
        if (now % GenDate.TicksPerHour != 1201)
            return;
        HeatLevel level = HeatTracker.Get()?.Level ?? HeatLevel.Shadow;

        // Red Zone: hours in a row; leaving it into the Shadow within a day.
        if (level == HeatLevel.RedZone)
        {
            if (_redSinceTick < 0)
                _redSinceTick = now;
            SetMax("RedZoneHours", (now - _redSinceTick) / GenDate.TicksPerHour);
            _leftRedTick = -1;
        }
        else
        {
            if (_redSinceTick >= 0)
                _leftRedTick = now;
            _redSinceTick = -1;
            if (_leftRedTick >= 0 && level == HeatLevel.Shadow)
            {
                if (now - _leftRedTick <= GenDate.TicksPerDay)
                    Add("RedToShadow", 1);
                _leftRedTick = -1;
            }
        }

        // A season (15 days) in the Shadow with 20+ hacks.
        if (level == HeatLevel.Shadow)
        {
            if (_shadowSinceTick < 0)
            {
                _shadowSinceTick = now;
                _shadowHacks = 0;
            }
            if (now - _shadowSinceTick >= 15 * GenDate.TicksPerDay && _shadowHacks >= 20)
            {
                Add("ShadowSeason", 1);
                _shadowSinceTick = now;
                _shadowHacks = 0;
            }
        }
        else
        {
            _shadowSinceTick = -1;
        }

        AIManager ai = AIManager.Get();
        if (ai != null)
        {
            if (ai.AIs.Any(a => a.Trust >= 99.5f))
                SetMax("Trust100", 1);
            foreach (NetAI a in ai.AIs.Where(a => a.Personality == AIPersonality.Echo && a.EchoKillerId >= 0))
            {
                Pawn killer = Find.Maps.SelectMany(m => m.mapPawns.AllPawnsSpawned).FirstOrDefault(p => p.thingIDNumber == a.EchoKillerId);
                if (killer != null && a.Housed && killer.MapHeld == a.Anchor?.MapHeld)
                {
                    Add("EchoMeetsKiller", 1);
                    a.EchoKillerId = -1;
                }
            }
        }
        SetMax("PinnedFixers", FixerManager.Get()?.Fixers.Count(f => f.Pinned) ?? 0);
        SetMax("UniqueRecords", UniqueBraindanceManager.Get()?.Found.Count ?? 0);
        if (NetRanking.Get()?.BestColonyPlace == 1)
            SetMax("NetNumberOne", 1);

        // The second year with a cyberpod and not one AI awakened.
        if (Find.TickManager.TicksGame >= GenDate.TicksPerYear && Value("AIsAwakened") == 0 && Value("NoAIYear2") == 0
            && Find.Maps.Any(m => CyberpodUtility.PlayerPods(m).Any()))
            Add("NoAIYear2", 1);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref _values, "values", LookMode.Value, LookMode.Value, ref _keys, ref _vals);
        Scribe_Collections.Look(ref _met, "met", LookMode.Value);
        Scribe_Values.Look(ref _redSinceTick, "redSince", -1);
        Scribe_Values.Look(ref _leftRedTick, "leftRed", -1);
        Scribe_Values.Look(ref _shadowSinceTick, "shadowSince", -1);
        Scribe_Values.Look(ref _shadowHacks, "shadowHacks");
        _values ??= new Dictionary<string, int>();
        _met ??= [];
    }
}
