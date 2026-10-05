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
        => DefDatabase<HediffDef>.GetNamedSilentFail("NR_HostedAI_" + personality);

    public static bool HasEmptySocket(Pawn pawn)
        => pawn.health?.hediffSet?.GetFirstHediffOfDef(NetrunDefOf.NR_AISocketInstalled) is Hediff_AISocket socket
           && socket.Passenger == null
           && pawn.IsHacker();

    public static IEnumerable<Pawn> Candidates(Map map)
        => map.mapPawns.FreeColonistsSpawned.Where(HasEmptySocket);

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
        if (!ai.InHost)
            return false;
        if (AIAwakening.FreeServer(map) == null)
            return "NR_AwakenNoServer".Translate();
        return true;
    }

    public static void Download(NetAI ai, Map map)
    {
        Building server = AIAwakening.FreeServer(map);
        if (server == null || !ai.InHost)
            return;

        Pawn host = ai.Host;
        RemovePassengerHediff(ai, host);
        ai.Host = null;
        ai.Server = server;
        Messages.Message("NR_AIDownloaded".Translate(ai.Name, server.LabelCap), server, MessageTypeDefOf.NeutralEvent, historical: true);
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
