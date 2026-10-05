using LudeonTK;
using Verse;

namespace Netrun;

public static class NetrunDebugActions
{
    private const string Category = "Netrun";

    [DebugAction(Category, "+10 Heat", allowedGameStates = AllowedGameStates.Playing)]
    private static void AddHeat() => HeatTracker.Get()?.AddHeat(10f, "NR_LogDev".Translate());

    [DebugAction(Category, "-10 Heat", allowedGameStates = AllowedGameStates.Playing)]
    private static void RemoveHeat() => HeatTracker.Get()?.ReduceHeat(10f, "NR_LogDev".Translate());

    [DebugAction(Category, "Reset Heat", allowedGameStates = AllowedGameStates.Playing)]
    private static void ResetHeat() => HeatTracker.Get()?.ReduceHeat(HeatTracker.MaxHeat, "NR_LogDev".Translate());

    [DebugAction(Category, "Open network terminal", allowedGameStates = AllowedGameStates.Playing)]
    private static void OpenTerminal() => NetTerminalUtility.OpenTerminal(Find.CurrentMap);

    [DebugAction(Category, "Rotate fixers", allowedGameStates = AllowedGameStates.Playing)]
    private static void RotateFixers() => FixerManager.Get()?.Rotate(notify: true);

    [DebugAction(Category, "+20 reputation (all fixers)", allowedGameStates = AllowedGameStates.Playing)]
    private static void AddReputation()
    {
        foreach (Fixer fixer in FixerManager.Get()?.Fixers ?? [])
            fixer.GainReputation(20f);
    }
}
