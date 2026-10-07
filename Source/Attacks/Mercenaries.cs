using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// The hidden mercenary faction: no settlements, no relations to manage, it only shows up as raids.
/// Saves made before the faction existed get it created on first use.
/// </summary>
public static class Mercenaries
{
    private static readonly FloatRange RevengeDelayDays = new(1f, 3f);

    public static Faction Faction
    {
        get
        {
            FactionDef def = NetrunDefOf.NR_Mercenaries;
            Faction faction = Find.FactionManager.FirstFactionOfDef(def);
            if (faction != null)
                return faction;

            faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def, default, true));
            Find.FactionManager.Add(faction);
            return faction;
        }
    }

    /// <summary>
    /// Someone sold the colony out: with this chance a mercenary raid is queued for later,
    /// and the player is warned now.
    /// </summary>
    public static void TryQueueRevenge(float chance, string reasonKey, params NamedArgument[] args)
    {
        Map map = Find.AnyPlayerHomeMap;
        if (map == null || !Rand.Chance(chance))
            return;

        IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
        parms.forced = true;
        parms.faction = Faction;

        // A paranoid fixer in the pool hears of it first and leaks their plan: they come weaker.
        string tip = string.Empty;
        if (FixerManager.Get()?.Fixers.FirstOrDefault(f => f.Trait == FixerTrait.Paranoid) is { } paranoid)
        {
            parms.points *= NetrunDefOf.NR_Fixers.paranoidRaidFactor;
            paranoid.TraitRevealed = true;
            tip = "\n\n" + "NR_FixerParanoidWarning".Translate(paranoid.Name);
        }

        int delay = (int)(RevengeDelayDays.RandomInRange * GenDate.TicksPerDay);
        Find.Storyteller.incidentQueue.Add(new QueuedIncident(new FiringIncident(NetrunDefOf.NR_MercRaid, null, parms),
            Find.TickManager.TicksGame + delay));

        Find.LetterStack.ReceiveLetter("NR_MercWarningLabel".Translate(),
            reasonKey.Translate(args) + "\n\n" + "NR_MercWarning".Translate(Faction.Name) + tip,
            LetterDefOf.ThreatSmall);
    }
}

/// <summary>A raid by the hidden mercenaries: by the storyteller at Target Heat, or queued as revenge.</summary>
public class IncidentWorker_MercRaid : IncidentWorker_RaidEnemy
{
    protected override bool CanFireNowSub(IncidentParms parms)
    {
        if (AIEventManager.Get()?.MercsBlocked == true)
            return false;
        if (!parms.forced && (HeatTracker.Get()?.Level ?? HeatLevel.Shadow) < HeatLevel.Target)
            return false;
        return base.CanFireNowSub(parms);
    }

    protected override bool TryResolveRaidFaction(IncidentParms parms)
    {
        parms.faction = Mercenaries.Faction;
        return parms.faction != null;
    }

    public override bool FactionCanBeGroupSource(Faction f, IncidentParms parms, bool desperate = false)
        => f?.def == NetrunDefOf.NR_Mercenaries;

    /// <summary>Below this many raid points only grenadiers fit the budget, and the raid comes without guns.</summary>
    private const float MinPoints = 300f;

    protected override bool TryExecuteWorker(IncidentParms parms)
    {
        parms.points = UnityEngine.Mathf.Max(parms.points, MinPoints);
        if (!base.TryExecuteWorker(parms))
            return false;
        HeatTracker.Get()?.Note("NR_LogMercRaid".Translate(def.LabelCap), HeatSource.Raid);
        return true;
    }

    protected override string GetLetterText(IncidentParms parms, List<Pawn> pawns)
        => "NR_MercRaidIntro".Translate() + "\n\n" + base.GetLetterText(parms, pawns);
}
