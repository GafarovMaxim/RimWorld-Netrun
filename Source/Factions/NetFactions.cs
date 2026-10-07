using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// The net's own factions (docs/CONCEPTS_10.md, sections 6 and 11): three runner crews and three corporations, all
/// hidden factions without settlements. The game makes none of them at world generation; this component picks three
/// crew emblems and three corporation themes per game and creates them (also for older saves, on load).
/// </summary>
public class NetFactions : GameComponent
{
    public const int CrewCount = 3;
    public const int CorpCount = 3;
    private const string CrewPrefix = "NR_Crew_";
    private const string CorpPrefix = "NR_Corp_";
    /// <summary>Corporations start wary of the colony; their Alert moves it (stage 9).</summary>
    private const int CorpStartGoodwill = -30;

    public NetFactions(Game game)
    {
    }

    public static NetFactions Get() => Current.Game?.GetComponent<NetFactions>();

    public static bool IsCrewDef(FactionDef def) => def != null && def.defName.StartsWith(CrewPrefix);
    public static bool IsCorpDef(FactionDef def) => def != null && def.defName.StartsWith(CorpPrefix);

    /// <summary>The theme key of a corporation ("Security", "Media", ...).</summary>
    public static string CorpTheme(Faction f) => f != null && IsCorpDef(f.def) ? f.def.defName.Substring(CorpPrefix.Length) : null;

    public static IEnumerable<Faction> Crews
        => Find.FactionManager?.AllFactions.Where(f => IsCrewDef(f.def) && !f.defeated) ?? Enumerable.Empty<Faction>();

    public static IEnumerable<Faction> Corps
        => Find.FactionManager?.AllFactions.Where(f => IsCorpDef(f.def) && !f.defeated) ?? Enumerable.Empty<Faction>();

    public override void StartedNewGame()
    {
        base.StartedNewGame();
        Ensure();
    }

    public override void LoadedGame()
    {
        base.LoadedGame();
        Ensure();
    }

    /// <summary>Creates crews and corporations up to three each, with themes not already playing.</summary>
    public void Ensure()
    {
        try
        {
            if (Find.FactionManager == null || Find.World == null)
                return;
            int crews = Crews.Count();
            foreach (FactionDef def in UnusedDefs(IsCrewDef).Take(System.Math.Max(0, CrewCount - crews)))
                Create(def, 0);
            int corps = Corps.Count();
            foreach (FactionDef def in UnusedDefs(IsCorpDef).Take(System.Math.Max(0, CorpCount - corps)))
                Create(def, CorpStartGoodwill);
        }
        catch (System.Exception e)
        {
            Log.Warning("[Netrun] could not create the net factions: " + e.Message);
        }
    }

    /// <summary>Defs of this kind no faction uses yet (alive or defeated), shuffled.</summary>
    public static List<FactionDef> UnusedDefs(System.Func<FactionDef, bool> kind)
    {
        var used = new HashSet<FactionDef>(Find.FactionManager.AllFactions.Select(f => f.def));
        return DefDatabase<FactionDef>.AllDefs.Where(d => kind(d) && !used.Contains(d)).InRandomOrder().ToList();
    }

    /// <summary>The vanilla way to add a faction in the middle of a game.</summary>
    public static Faction Create(FactionDef def, int goodwill)
    {
        Faction faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def, default, true));
        Find.FactionManager.Add(faction);
        if (goodwill != 0 && Faction.OfPlayer != null)
            faction.TryAffectGoodwillWith(Faction.OfPlayer, goodwill - faction.PlayerGoodwill, canSendMessage: false, canSendHostilityLetter: false);
        return faction;
    }

    /// <summary>The crew the colony gets on best with (the one that answers a call for help).</summary>
    public static Faction BestCrew()
        => Crews.Where(f => !f.HostileTo(Faction.OfPlayer)).OrderByDescending(f => f.PlayerGoodwill).FirstOrDefault();

    /// <summary>Goodwill with a crew from net events (a find, a race, a helping hand).</summary>
    public static void AffectCrew(Faction crew, int amount, string reasonKey = null)
    {
        if (crew == null || amount == 0 || Faction.OfPlayer == null)
            return;
        crew.TryAffectGoodwillWith(Faction.OfPlayer, amount, canSendMessage: false, canSendHostilityLetter: true);
        if (reasonKey != null)
            Messages.Message(reasonKey.Translate(crew.Name, amount.ToStringWithSign()), MessageTypeDefOf.NeutralEvent, historical: true);
    }
}

/// <summary>A friendly runner crew answers a call for help: armed runners arrive and fight on the colony's side.</summary>
public class IncidentWorker_CrewHelp : IncidentWorker_RaidFriendly
{
    protected override bool CanFireNowSub(IncidentParms parms) => parms.forced;

    protected override bool TryResolveRaidFaction(IncidentParms parms)
    {
        parms.faction ??= NetFactions.BestCrew();
        return parms.faction != null && !parms.faction.HostileTo(Faction.OfPlayer);
    }
}

/// <summary>A hostile runner crew comes for the colony in person (only below the hostility line).</summary>
public class IncidentWorker_CrewRaid : IncidentWorker_RaidEnemy
{
    protected override bool CanFireNowSub(IncidentParms parms)
    {
        if (!NetFactions.Crews.Any(f => f.HostileTo(Faction.OfPlayer)))
            return false;
        return base.CanFireNowSub(parms);
    }

    protected override bool TryResolveRaidFaction(IncidentParms parms)
    {
        if (parms.faction != null && NetFactions.IsCrewDef(parms.faction.def))
            return true;
        parms.faction = NetFactions.Crews.Where(f => f.HostileTo(Faction.OfPlayer)).RandomElementWithFallback();
        return parms.faction != null;
    }
}
