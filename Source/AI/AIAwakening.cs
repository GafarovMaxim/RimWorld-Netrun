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

    /// <summary>A powered network console: any building with the terminal comp.</summary>
    public static Building Console(Map map)
        => NetTerminalUtility.Consoles(map).FirstOrDefault(b => b.GetComp<CompPowerTrader>()?.PowerOn != false);

    public static AcceptanceReport CanAwaken(Map map)
    {
        if (map == null)
            return false;
        if (!Fragments(map).Any())
            return "NR_AwakenNoFragment".Translate();
        if (FreeServer(map) == null)
            return "NR_AwakenNoServer".Translate();
        // AIs that already lived with the colony come back for free; fresh fragments need the research and cyberdata.
        if (!Fragments(map).Any(f => IsStored(f) || Researched))
            return "NR_AwakenNoResearch".Translate(NetrunDefOf.NR_Res_AIAwakening.LabelCap);
        if (!Fragments(map).Any(f => IsStored(f) || Affordable(map)))
            return "NR_AwakenNoCyberdata".Translate(NetrunDefOf.NR_AI.awakenCyberdataCost);
        return true;
    }

    public static bool Researched => NetrunDefOf.NR_Res_AIAwakening.IsFinished;

    private static bool IsStored(Thing fragment) => fragment.TryGetComp<CompAIFragment>()?.StoredAI != null;

    private static bool Affordable(Map map)
    {
        int cost = NetrunDefOf.NR_AI.awakenCyberdataCost;
        return cost <= 0 || ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata) >= cost;
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
            if (stored == null)
            {
                string label = "NR_AwakenFragmentUnknown".Translate() + " (" + "NR_AwakenCost".Translate(NetrunDefOf.NR_AI.awakenCyberdataCost) + ")";
                if (!Researched)
                    options.Add(new FloatMenuOption(label + ": " + "NR_AwakenNoResearch".Translate(NetrunDefOf.NR_Res_AIAwakening.LabelCap), null));
                else if (!Affordable(map))
                    options.Add(new FloatMenuOption(label + ": " + "NR_AwakenNoCyberdata".Translate(NetrunDefOf.NR_AI.awakenCyberdataCost), null));
                else
                    options.Add(new FloatMenuOption(label, () => OpenWorkerMenu(map, fragment)));
            }
            else
            {
                options.Add(new FloatMenuOption("NR_AwakenFragmentStored".Translate(stored.Name), () => OpenWorkerMenu(map, fragment)));
            }
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

    /// <summary>The colonist carries the fragment to the server and wakes it there.</summary>
    private static void Order(Map map, Pawn pawn, Thing fragment)
    {
        Building server = FreeServer(map);
        if (server == null)
            return;

        Job job = JobMaker.MakeJob(NetrunDefOf.NR_AwakenAI, server, fragment, server);
        job.count = 1;
        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
    }
}
