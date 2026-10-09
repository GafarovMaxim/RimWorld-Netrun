using System.Collections.Generic;
using System.Linq;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>Moving AIs between servers and colonists with an AI socket.</summary>
public static class AIHosting
{
    public static HediffDef PassengerHediff(AIPersonality personality)
        => PassengerHediffs.TryGetValue(personality, out HediffDef def)
            ? def
            : PassengerHediffs[personality] = DefDatabase<HediffDef>.GetNamedSilentFail("NR_HostedAI_" + personality);

    private static readonly Dictionary<AIPersonality, HediffDef> PassengerHediffs = [];

    public static bool HasEmptySocket(Pawn pawn)
        => pawn.health?.hediffSet?.GetFirstHediffOfDef(NetrunDefOf.NR_AISocketInstalled) is Hediff_AISocket socket
           && socket.Passenger == null
           && pawn.IsHacker();

    public static IEnumerable<Pawn> Candidates(Map map)
        // Colonists held on the map count too: a netrunner lying in a cyberpod is not "spawned".
        => map.mapPawns.FreeColonists.Where(p => p.MapHeld == map && HasEmptySocket(p));

    public static AcceptanceReport CanUpload(NetAI ai, Map map)
    {
        if (!ai.InServer || !ai.Powered)
            return "NR_AIUploadNeedsServer".Translate();
        if (!Candidates(map).Any())
            return "NR_AIUploadNoHost".Translate();
        return true;
    }

    public static void OpenUploadMenu(NetAI ai, Map map)
    {
        var options = Candidates(map)
            .Select(p => new FloatMenuOption(p.LabelShort, () => Upload(ai, p)))
            .ToList();
        if (options.Count == 0)
            options.Add(new FloatMenuOption("NR_AIUploadNoHost".Translate(), null));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    /// <summary>Network transfer from the server into the socket.</summary>
    public static void Upload(NetAI ai, Pawn host)
    {
        if (!HasEmptySocket(host))
            return;

        ai.Server = null;
        ai.Host = host;
        AddPassengerHediff(ai, host);
        Messages.Message("NR_AIUploaded".Translate(ai.Name, host.LabelShort), host, MessageTypeDefOf.PositiveEvent, historical: true);
    }

    public static AcceptanceReport CanDownload(NetAI ai, Map map)
    {
        if (!ai.InHost && !ai.InCoprocessor)
            return false;
        if (AIAwakening.FreeServer(map) == null)
            return "NR_AwakenNoServer".Translate();
        return true;
    }

    /// <summary>Back to the main server, from a host or from a cyberpod coprocessor.</summary>
    public static void Download(NetAI ai, Map map)
    {
        Building server = AIAwakening.FreeServer(map);
        if (server == null || (!ai.InHost && !ai.InCoprocessor))
            return;

        if (ai.InHost)
            RemovePassengerHediff(ai, ai.Host);
        ai.Host = null;
        ai.Server = server;
        Messages.Message("NR_AIDownloaded".Translate(ai.Name, server.LabelCap), server, MessageTypeDefOf.NeutralEvent, historical: true);
    }

    public static Building FreeCoprocessor(Map map)
        => map.listerBuildings.AllBuildingsColonistOfDef(NetrunDefOf.NR_NetrunCoprocessor)
            .FirstOrDefault(b => b.GetComp<CompAIServer>() is { Powered: true, HasFreeSlot: true });

    public static AcceptanceReport CanMoveToCoprocessor(NetAI ai, Map map)
    {
        if (!ai.InMainServer || !ai.Powered)
            return "NR_AIUploadNeedsServer".Translate();
        if (FreeCoprocessor(map) == null)
            return "NR_AINoCoprocessor".Translate();
        return true;
    }

    /// <summary>The AI moves into a cyberpod coprocessor and works for the pod instead of the colony.</summary>
    public static void MoveToCoprocessor(NetAI ai, Map map)
    {
        Building coprocessor = FreeCoprocessor(map);
        if (coprocessor == null)
            return;

        ai.Server = coprocessor;
        Messages.Message("NR_AIMovedToCoprocessor".Translate(ai.Name), coprocessor, MessageTypeDefOf.NeutralEvent, historical: true);
    }

    /// <summary>Powered coprocessors with a free slot other than the AI's own one.</summary>
    public static List<Building> OtherCoprocessors(NetAI ai, Map map)
        => map.listerBuildings.AllBuildingsColonistOfDef(NetrunDefOf.NR_NetrunCoprocessor)
            .Where(b => b != ai.Server && b.GetComp<CompAIServer>() is { Powered: true, HasFreeSlot: true }).ToList();

    public static AcceptanceReport CanMoveBetweenCoprocessors(NetAI ai, Map map)
    {
        if (!ai.InCoprocessor)
            return false;
        return OtherCoprocessors(ai, map).Count > 0 ? true : (AcceptanceReport)"NR_AINoCoprocessor".Translate();
    }

    /// <summary>C8: a menu of the other free coprocessors (by the pod's occupant); the AI moves over directly.</summary>
    public static void OpenCoprocessorMenu(NetAI ai, Map map)
    {
        var options = new List<FloatMenuOption>();
        foreach (Building c in OtherCoprocessors(ai, map))
        {
            Building target = c;
            string where = c.LabelCap + (c.GetComp<CompFacility>()?.LinkedBuildings.OfType<Building_Cyberpod>().FirstOrDefault()?.ContainedThing is Pawn p ? " (" + p.LabelShort + ")" : string.Empty);
            options.Add(new FloatMenuOption(where, () =>
            {
                ai.Server = target;
                Messages.Message("NR_AIMovedToCoprocessor".Translate(ai.Name), target, MessageTypeDefOf.NeutralEvent, historical: true);
            }));
        }
        if (options.Count > 0)
            Find.WindowStack.Add(new FloatMenu(options));
    }

    /// <summary>
    /// Safety net: every colonist carries exactly the passenger hediff of the AI riding in them, and nothing else.
    /// </summary>
    public static void ReconcilePassengerHediffs(IEnumerable<NetAI> ais)
    {
        foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists)
        {
            NetAI rider = ais.FirstOrDefault(a => a.Host == pawn);
            HediffDef keep = rider == null ? null : PassengerHediff(rider.Personality);

            foreach (Hediff hediff in pawn.health.hediffSet.hediffs
                         .Where(h => h.def.defName.StartsWith("NR_HostedAI_") && h.def != keep).ToList())
                pawn.health.RemoveHediff(hediff);

            if (rider != null)
                AddPassengerHediff(rider, pawn);
        }
    }

    public static void AddPassengerHediff(NetAI ai, Pawn host)
    {
        HediffDef def = PassengerHediff(ai.Personality);
        BodyPartRecord brain = host.health.hediffSet.GetBrain();
        if (def != null && brain != null && !host.health.hediffSet.HasHediff(def))
            host.health.AddHediff(def, brain);
    }

    public static void RemovePassengerHediff(NetAI ai, Pawn host)
    {
        HediffDef def = PassengerHediff(ai.Personality);
        Hediff hediff = def == null ? null : host?.health?.hediffSet?.GetFirstHediffOfDef(def);
        if (hediff != null)
            host.health.RemoveHediff(hediff);
    }
}
