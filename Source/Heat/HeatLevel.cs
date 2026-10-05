using UnityEngine;
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

[StaticConstructorOnStartup]
public static class HeatLevelUtility
{
    public const float NoticedThreshold = 25f;
    public const float TrackedThreshold = 50f;
    public const float TargetThreshold = 75f;
    public const float RedZoneThreshold = 95f;

    private static readonly Color[] Colors =
    [
        new(0.55f, 0.70f, 0.80f), // Shadow
        new(0.95f, 0.85f, 0.30f), // Noticed
        new(1.00f, 0.60f, 0.20f), // Tracked
        new(1.00f, 0.35f, 0.15f), // Target
        new(0.95f, 0.10f, 0.10f), // Red zone
    ];

    private static readonly Texture2D[] BarTextures = new Texture2D[Colors.Length];

    static HeatLevelUtility()
    {
        for (int i = 0; i < Colors.Length; i++)
            BarTextures[i] = SolidColorMaterials.NewSolidColorTexture(Colors[i]);
    }

    public static HeatLevel LevelOf(float heat)
    {
        if (heat >= RedZoneThreshold) return HeatLevel.RedZone;
        if (heat >= TargetThreshold) return HeatLevel.Target;
        if (heat >= TrackedThreshold) return HeatLevel.Tracked;
        if (heat >= NoticedThreshold) return HeatLevel.Noticed;
        return HeatLevel.Shadow;
    }

    public static float MinHeat(this HeatLevel level) => level switch
    {
        HeatLevel.Noticed => NoticedThreshold,
        HeatLevel.Tracked => TrackedThreshold,
        HeatLevel.Target => TargetThreshold,
        HeatLevel.RedZone => RedZoneThreshold,
        _ => 0f,
    };

    public static string Label(this HeatLevel level) => ("NR_HeatLevel_" + level).Translate();
    public static string Description(this HeatLevel level) => ("NR_HeatLevelDesc_" + level).Translate();
    public static Color Color(this HeatLevel level) => Colors[(int)level];
    public static Texture2D BarTexture(this HeatLevel level) => BarTextures[(int)level];
}
