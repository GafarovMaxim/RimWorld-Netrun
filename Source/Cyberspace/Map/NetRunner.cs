using System.Collections.Generic;
using Verse;

namespace Netrun;

/// <summary>
/// A netrunner connected to the map from a cyberpod (docs/CYBERSPACE_V2.md, sections 1-3): where they stand, the route
/// they walk in game time, when the session began. Up to three at once.
/// </summary>
public class NetRunner : IExposable
{
    public Pawn Pawn;
    public int NodeId;
    public List<int> Path = [];
    /// <summary>Game hours already walked on the current link.</summary>
    public float Progress;
    public int SessionStartTick;
    /// <summary>Lent to a friendly crew until this tick (cannot walk or dive), -1 free.</summary>
    public int BusyUntil = -1;
    public int BusyCrew = -1;

    public bool Busy => Find.TickManager.TicksGame < BusyUntil;

    public bool Moving => Path.Count > 0;
    public int NextNodeId => Path.Count > 0 ? Path[0] : -1;

    public void ExposeData()
    {
        Scribe_References.Look(ref Pawn, "pawn");
        Scribe_Values.Look(ref NodeId, "node");
        Scribe_Collections.Look(ref Path, "path", LookMode.Value);
        Scribe_Values.Look(ref Progress, "progress");
        Scribe_Values.Look(ref SessionStartTick, "sessionStart");
        Scribe_Values.Look(ref BusyUntil, "busyUntil", -1);
        Scribe_Values.Look(ref BusyCrew, "busyCrew", -1);
        Path ??= [];
    }
}
