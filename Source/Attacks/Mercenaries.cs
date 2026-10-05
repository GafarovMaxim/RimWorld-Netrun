using System.Collections.Generic;
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

        int delay = (int)(RevengeDelayDays.RandomInRange * GenDate.TicksPerDay);
        Find.Storyteller.incidentQueue.Add(new QueuedIncident(new FiringIncident(NetrunDefOf.NR_MercRaid, null, parms),
            Find.TickManager.TicksGame + delay));

        Find.LetterStack.ReceiveLetter("NR_MercWarningLabel".Translate(),
            reasonKey.Translate(args) + "\n\n" + "NR_MercWarning".Translate(Faction.Name),
            LetterDefOf.ThreatSmall);
    }
}

/// <summary>A raid by the hidden mercenaries: by the storyteller at Target Heat, or queued as revenge.</summary>
public class IncidentWorker_MercRaid : IncidentWorker_RaidEnemy
{
    protected override bool CanFireNowSub(IncidentParms parms)
    {
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

    protected override string GetLetterText(IncidentParms parms, List<Pawn> pawns)
        => "NR_MercRaidIntro".Translate() + "\n\n" + base.GetLetterText(parms, pawns);
}
