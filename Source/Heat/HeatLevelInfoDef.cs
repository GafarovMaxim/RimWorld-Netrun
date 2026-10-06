using System.Collections.Generic;
using Verse;

namespace Netrun;

/// <summary>How serious a level effect is: the mark and color on the Heat screen.</summary>
public enum HeatEffectSeverity
{
    Info,
    Warn,
    Bad,
}

public class HeatEffect
{
    public HeatEffectSeverity severity;
    [MustTranslate]
    public string text;
}

/// <summary>What one Heat level brings, for the "Level effects" block of the Heat screen (docs/HEAT_V2.md).</summary>
public class HeatLevelInfoDef : Def
{
    public HeatLevel level;
    public List<HeatEffect> effects = [];

    public static HeatLevelInfoDef For(HeatLevel level) => DefDatabase<HeatLevelInfoDef>.AllDefs.FirstOrFallback(d => d.level == level);
}
