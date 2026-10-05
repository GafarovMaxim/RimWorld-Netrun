using Verse;

namespace Netrun;

public enum HeatLevel
{
    Ghost,
    Noticed,
    Hunted,
    Burned,
}

public static class HeatLevelUtility
{
    public const float NoticedThreshold = 25f;
    public const float HuntedThreshold = 50f;
    public const float BurnedThreshold = 75f;

    public static HeatLevel LevelOf(float heat)
    {
        if (heat >= BurnedThreshold) return HeatLevel.Burned;
        if (heat >= HuntedThreshold) return HeatLevel.Hunted;
        if (heat >= NoticedThreshold) return HeatLevel.Noticed;
        return HeatLevel.Ghost;
    }

    public static string Label(this HeatLevel level) => ("NR_HeatLevel_" + level).Translate();
}
