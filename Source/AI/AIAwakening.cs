using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Netrun;

/// <summary>Finding what an awakening needs and ordering a colonist to do it.</summary>
public static class AIAwakening
{
    public static IEnumerable<Thing> Fragments(Map map)
        => map.listerThings.ThingsOfDef(NetrunDefOf.NR_AIFragment).Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer));

    public static Building FreeServer(Map map)
        => map.listerBuildings.AllBuildingsColonistOfDef(NetrunDefOf.NR_AIServer)
            .FirstOrDefault(b => b.GetComp<CompAIServer>() is { Powered: true, HasFreeSlot: true });

    public static Building Console(Map map)
        => map.listerBuildings.allBuildingsColonist.FirstOrDefault(b => b.TryGetComp<CompNetTerminal>() != null
            && b.GetComp<CompPowerTrader>()?.PowerOn != false);

    public static AcceptanceReport CanAwaken(Map map)
    {
        if (map == null)
            return false;
        if (!Fragments(map).Any())
            return "NR_AwakenNoFragment".Translate();
        if (FreeServer(map) == null)
            return "NR_AwakenNoServer".Translate();
        if (Console(map) == null)
            return "NR_AwakenNoConsole".Translate();
        return true;
    }

    /// <summary>Pick a fragment, then a colonist; the job does the rest.</summary>
    public static void OpenMenu(Map map)
    {
        AcceptanceReport report = CanAwaken(map);
        if (!report.Accepted)
        {
            Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var options = new List<FloatMenuOption>();
        foreach (Thing fragment in Fragments(map))
        {
            NetAI stored = fragment.TryGetComp<CompAIFragment>()?.StoredAI;
            string label = stored == null
                ? "NR_AwakenFragmentUnknown".Translate()
                : "NR_AwakenFragmentStored".Translate(stored.Name);
            options.Add(new FloatMenuOption(label, () => OpenWorkerMenu(map, fragment)));
        }
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void OpenWorkerMenu(Map map, Thing fragment)
    {
        var options = new List<FloatMenuOption>();
        foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
        {
            if (pawn.WorkTagIsDisabled(WorkTags.Intellectual) || pawn.Downed)
                continue;

            int skill = pawn.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
            Pawn worker = pawn;
            options.Add(new FloatMenuOption($"{pawn.LabelShort} ({SkillDefOf.Intellectual.LabelCap}: {skill})", () => Order(map, worker, fragment)));
        }

        if (options.Count == 0)
            options.Add(new FloatMenuOption("NR_AwakenNoWorker".Translate(), null));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void Order(Map map, Pawn pawn, Thing fragment)
    {
        Building console = Console(map);
        if (console != null)
            Order(pawn, fragment, console);
    }

    private static void Order(Pawn pawn, Thing fragment, Building console)
    {
        Building server = FreeServer(pawn.Map);
        if (server == null)
            return;

        Job job = JobMaker.MakeJob(NetrunDefOf.NR_AwakenAI, console, fragment, server);
        job.count = 1;
        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
    }
}
