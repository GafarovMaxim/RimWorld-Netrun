using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Netrun;

public enum CleanupReason
{
    Loaded,
    Daily,
    Manual,
}

/// <summary>
/// Anything that keeps Netrun data in the save and can tidy it: drop expired entries, enforce list caps,
/// replace links to pawns and things the game has removed (docs/TECH_DETAILS.md, section 2.5).
/// </summary>
public interface INetrunCleanable
{
    /// <summary>Returns how many entries were removed or repaired, for the report.</summary>
    int Cleanup(CleanupReason reason);
}

/// <summary>
/// The data janitor: one pass after loading a save, then once a game day, plus a dev action with a report.
/// It never removes pawns, never patches the world pawn GC and never forces a garbage collection.
/// </summary>
public class NetrunJanitor : GameComponent
{
    // Offset from midnight so the daily pass does not land on the same tick as everything else.
    private const int DailyOffset = 1371;

    private bool _loadedPassDone;

    public NetrunJanitor(Game game)
    {
    }

    public static NetrunJanitor Get() => Current.Game?.GetComponent<NetrunJanitor>();

    public override void LoadedGame()
    {
        base.LoadedGame();
        _loadedPassDone = false;
    }

    public override void GameComponentTick()
    {
        int now = Find.TickManager.TicksGame;
        if (!_loadedPassDone)
        {
            // The first tick after loading: every component and map is ready by now.
            _loadedPassDone = true;
            Run(CleanupReason.Loaded);
            return;
        }
        if (now % GenDate.TicksPerDay == DailyOffset)
            Run(CleanupReason.Daily);
    }

    /// <summary>Runs every cleanable component; returns the report lines (component: count) of those that changed something.</summary>
    public List<string> Run(CleanupReason reason)
    {
        var lines = new List<string>();
        if (Current.Game == null)
            return lines;
        foreach (GameComponent component in Current.Game.components)
        {
            if (component is not INetrunCleanable cleanable)
                continue;
            try
            {
                int n = cleanable.Cleanup(reason);
                if (n > 0)
                    lines.Add($"{component.GetType().Name} -{n}");
            }
            catch (System.Exception e)
            {
                Log.Warning($"[Netrun] cleanup of {component.GetType().Name} failed: {e.Message}");
            }
        }
        if (Prefs.DevMode && (lines.Count > 0 || reason == CleanupReason.Manual))
            Log.Message($"[Netrun] cleanup ({reason}): " + (lines.Count > 0 ? string.Join(", ", lines) : "nothing to clean"));
        return lines;
    }

    /// <summary>Every AI fragment the colony has: on maps (spawned or carried) and in caravans.</summary>
    public static HashSet<int> FragmentIds()
    {
        var ids = new HashSet<int>();
        void Take(Thing t)
        {
            if (t?.def == NetrunDefOf.NR_AIFragment && t.TryGetComp<CompAIFragment>() is { StoredAIId: >= 0 } c)
                ids.Add(c.StoredAIId);
        }
        foreach (Map map in Find.Maps)
        {
            foreach (Thing t in map.listerThings.ThingsOfDef(NetrunDefOf.NR_AIFragment))
                Take(t);
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                Take(p.carryTracker?.CarriedThing);
                if (p.inventory != null)
                    foreach (Thing t in p.inventory.innerContainer)
                        Take(t);
            }
            // Fragments inside containers (pods, crates, transporters) on this map.
            var held = new List<Thing>();
            ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForDef(NetrunDefOf.NR_AIFragment), held, allowUnreal: true);
            foreach (Thing t in held)
                Take(t);
        }
        foreach (Caravan caravan in Find.WorldObjects.Caravans)
        {
            foreach (Thing t in CaravanInventoryUtility.AllInventoryItems(caravan))
                Take(t);
        }
        return ids;
    }
}
