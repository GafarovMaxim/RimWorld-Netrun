using Verse;

namespace Netrun;

public enum HeatLevel
{
    Shadow,
    Noticed,
    Tracked,
    Target,
    RedZone,
}

public static class HeatLevelUtility
{
    public const float NoticedThreshold = 25f;
    public const float TrackedThreshold = 50f;
    public const float TargetThreshold = 75f;
    public const float RedZoneThreshold = 95f;

    public static HeatLevel LevelOf(float heat)
    {
        if (heat >= RedZoneThreshold) return HeatLevel.RedZone;
        if (heat >= TargetThreshold) return HeatLevel.Target;
        if (heat >= TrackedThreshold) return HeatLevel.Tracked;
        if (heat >= NoticedThreshold) return HeatLevel.Noticed;
        return HeatLevel.Shadow;
    }

    public static string Label(this HeatLevel level) => ("NR_HeatLevel_" + level).Translate();
}
