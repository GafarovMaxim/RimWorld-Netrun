using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// Stockpiles and shelves made before the mod was added keep their old list of allowed items, so the mod's
/// items (AI fragment, core module, braindance recordings and chips, goggles) stayed out of storage.
/// Once per save, every storage that accepts the vanilla items next to ours in the same category accepts ours too.
/// </summary>
public class StorageMigration : GameComponent
{
    private const int Version = 1;
    private int _doneVersion;

    public StorageMigration(Game game)
    {
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        if (_doneVersion >= Version)
            return;
        _doneVersion = Version;

        List<(ThingDef ours, ThingDef neighbour)> pairs = OurStorableDefs()
            .Select(d => (d, Neighbour(d)))
            .Where(p => p.Item2 != null)
            .ToList();
        if (pairs.Count == 0)
            return;

        int fixedCount = 0;
        foreach (Map map in Find.Maps)
        {
            IEnumerable<ThingFilter> filters = map.zoneManager.AllZones.OfType<Zone_Stockpile>().Select(z => z.settings?.filter)
                .Concat(map.listerBuildings.allBuildingsColonist.OfType<Building_Storage>().Select(b => b.GetStoreSettings()?.filter));
            foreach (ThingFilter filter in filters.Where(f => f != null))
            {
                foreach ((ThingDef ours, ThingDef neighbour) in pairs)
                {
                    if (!filter.Allows(ours) && filter.Allows(neighbour))
                    {
                        filter.SetAllow(ours, true);
                        fixedCount++;
                    }
                }
            }
        }

        if (fixedCount > 0)
            Log.Message($"[Netrun] Allowed the mod's items in {fixedCount} existing storage filters.");
    }

    private static IEnumerable<ThingDef> OurStorableDefs()
        => DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.defName.StartsWith("NR_") && d.EverStorable(false));

    /// <summary>A vanilla item from the same category, to read the player's intent from.</summary>
    private static ThingDef Neighbour(ThingDef def)
        => def.thingCategories?.SelectMany(c => c.childThingDefs)
            .FirstOrDefault(d => d != def && !d.defName.StartsWith("NR_") && d.EverStorable(false));

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref _doneVersion, "doneVersion");
    }
}
