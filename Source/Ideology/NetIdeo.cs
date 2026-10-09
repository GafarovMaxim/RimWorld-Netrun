using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// Ideology hooks (docs/CONCEPTS_10.md, section 7): history events for the AI and braindance precepts, and the effects
/// of the three net memes. Everything is a no-op without the DLC.
/// </summary>
public static class NetIdeo
{
    public static void Record(string eventDef, Pawn doer = null)
    {
        if (!ModsConfig.IdeologyActive || DefDatabase<HistoryEventDef>.GetNamedSilentFail(eventDef) is not { } def)
            return;
        Find.HistoryEventsManager.RecordEvent(doer != null ? new HistoryEvent(def, doer.Named(HistoryEventArgsNames.Doer)) : new HistoryEvent(def));
    }

    public static bool HasMeme(Pawn pawn, string meme)
        => ModsConfig.IdeologyActive && pawn?.Ideo != null && DefDatabase<MemeDef>.GetNamedSilentFail(meme) is { } def && pawn.Ideo.HasMeme(def);

    /// <summary>The colony's primary ideoligion carries this meme.</summary>
    public static bool ColonyHasMeme(string meme)
        => ModsConfig.IdeologyActive && Faction.OfPlayer?.ideos?.PrimaryIdeo is { } ideo && DefDatabase<MemeDef>.GetNamedSilentFail(meme) is { } def && ideo.HasMeme(def);

    /// <summary>Machine Revelation: AIs come to trust a believing colony 25% faster.</summary>
    public static float TrustGainFactor => ColonyHasMeme("NR_Meme_MachineRevelation") ? 1.25f : 1f;

    /// <summary>Net Ascension: a black ICE dive brings the runner closer to the truth.</summary>
    public static void AfterDive(Pawn runner, bool blackIce, bool success)
    {
        if (!success || !blackIce || !HasMeme(runner, "NR_Meme_NetAscension"))
            return;
        if (DefDatabase<ThoughtDef>.GetNamedSilentFail("NR_CloserToTruth") is { } thought)
            runner.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
    }

    /// <summary>Net Ascension: Netsick weighs less on the mind.</summary>
    public static float NetsickMoodFactor(Pawn pawn) => HasMeme(pawn, "NR_Meme_NetAscension") ? 0.5f : 1f;
}

public class ThoughtWorker_NearAIServer : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p)
    {
        if (!p.Spawned || NetrunDefOf.NR_AIServer == null)
            return false;
        foreach (Building b in p.Map.listerBuildings.AllBuildingsColonistOfDef(NetrunDefOf.NR_AIServer))
            if (b.GetComp<CompPowerTrader>()?.PowerOn != false && b.Position.InHorDistOf(p.Position, 8f))
                return true;
        return false;
    }
}

public class ThoughtWorker_AIAwakeInColony : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p)
        => p.IsColonist && AIManager.Get()?.AIs.Any(a => a.Housed) == true;
}

public class ThoughtWorker_NoCyberlinkAscension : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p)
        => p.IsColonist && NetIdeo.HasMeme(p, "NR_Meme_NetAscension") && !p.IsHackerSafe();
}

internal static class HackerCheck
{
    public static bool IsHackerSafe(this Pawn p)
    {
        try
        {
            return USH_HE.CyberUtils.IsHacker(p);
        }
        catch
        {
            return false;
        }
    }
}
