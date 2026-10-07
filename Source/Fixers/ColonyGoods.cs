using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>Counting and spending colony items on a map: unforbidden, in storage or in the home area.</summary>
public static class ColonyGoods
{
    public static int Count(Map map, ThingDef def) => Available(map, def).Sum(t => t.stackCount);

    public static bool TryTake(Map map, ThingDef def, int count)
    {
        if (count <= 0)
            return true;

        List<Thing> things = Available(map, def).ToList();
        if (things.Sum(t => t.stackCount) < count)
            return false;

        int left = count;
        foreach (Thing thing in things)
        {
            int take = System.Math.Min(left, thing.stackCount);
            thing.SplitOff(take).Destroy();
            left -= take;

            if (left <= 0)
                break;
        }
        return true;
    }

    public static IEnumerable<Thing> Available(Map map, ThingDef def)
    {
        if (map == null || def == null)
            yield break;

        foreach (Thing thing in map.listerThings.ThingsOfDef(def))
        {
            if (!thing.Spawned || thing.IsForbidden(Faction.OfPlayer))
                continue;

            if (thing.IsInAnyStorage() || map.areaManager.Home[thing.Position])
                yield return thing;
        }
    }
}
