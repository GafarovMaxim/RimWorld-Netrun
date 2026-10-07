using RimWorld;
using Verse;

namespace Netrun;

/// <summary>Ideology's "net culture" (Defs/NetCulture.xml): a dive is a history event; the trace weighs on the wary and the abhorrent.</summary>
public static class NetCulture
{
    /// <summary>Tells the ideoligions that this pawn dove into the net.</summary>
    public static void RecordDive(Pawn pawn)
    {
        if (!ModsConfig.IdeologyActive || pawn == null || DefDatabase<HistoryEventDef>.GetNamedSilentFail("NR_DoveIntoNet") is not { } def)
            return;
        Find.HistoryEventsManager.RecordEvent(new HistoryEvent(def, pawn.Named(HistoryEventArgsNames.Doer)));
    }

    public static bool Holds(Pawn pawn, string precept)
        => ModsConfig.IdeologyActive && pawn?.Ideo != null && DefDatabase<PreceptDef>.GetNamedSilentFail(precept) is { } def && pawn.Ideo.HasPrecept(def);

    public static HeatLevel Level => HeatTracker.Get()?.Level ?? HeatLevel.Shadow;
}

/// <summary>Wary: unhappy while the colony's Heat is at Target or higher.</summary>
public class ThoughtWorker_NetTraceWary : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p)
        => NetCulture.Holds(p, "NR_NetCulture_Wary") && NetCulture.Level >= HeatLevel.Target;
}

/// <summary>Abhorrent: unhappy whenever the colony has been noticed at all.</summary>
public class ThoughtWorker_NetTraceAbhorrent : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p)
        => NetCulture.Holds(p, "NR_NetCulture_Abhorrent") && NetCulture.Level >= HeatLevel.Noticed;
}
