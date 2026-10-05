using LudeonTK;
using RimWorld;
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

    /// <summary>Click on the map: an encrypted crate to hack, a cyberlink to install and some cyberdata.</summary>
    [DebugAction(Category, "Spawn hacking test kit", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnHackingTestKit()
    {
        Map map = Find.CurrentMap;
        IntVec3 cell = UI.MouseCell();
        if (!cell.InBounds(map))
            return;

        Spawn("USH_EncryptedCrate", 1);
        Spawn("USH_Cyberlink", 1);
        Spawn("USH_Cyberdata", 10);

        void Spawn(string defName, int count)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null)
                return;

            Thing thing = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
            thing.stackCount = count;

            if (def.category == ThingCategory.Building)
                GenSpawn.Spawn(thing, CellFinder.StandableCellNear(cell, map, 5f), map);
            else
                GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
        }
    }

    [DebugAction(Category, "Spawn AI fragment", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnAIFragment() => SpawnAtMouse(NetrunDefOf.NR_AIFragment);

    [DebugAction(Category, "Spawn AI core module", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnCoreModule() => SpawnAtMouse(NetrunDefOf.NR_AICoreModule);

    [DebugAction(Category, "Spawn AI socket", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnAISocket() => SpawnAtMouse(DefDatabase<ThingDef>.GetNamed("NR_AISocket"));

    [DebugAction(Category, "AIs: starve (escape test)", allowedGameStates = AllowedGameStates.Playing)]
    private static void StarveAIs()
    {
        foreach (NetAI ai in AIManager.Get()?.AIs ?? [])
        {
            ai.Trust = 15f;
            ai.Hunger = 90f;
        }
    }

    [DebugAction(Category, "AIs: trust 5 (betrayal test)", allowedGameStates = AllowedGameStates.Playing)]
    private static void BetrayAIs()
    {
        foreach (NetAI ai in AIManager.Get()?.AIs ?? [])
            ai.Trust = 5f;
    }

    [DebugAction(Category, "AIs: ready services and requests", allowedGameStates = AllowedGameStates.Playing)]
    private static void ReadyAIs()
    {
        foreach (NetAI ai in AIManager.Get()?.AIs ?? [])
        {
            ai.ServiceReadyTick = 0;
            ai.NextRequestTick = Find.TickManager.TicksGame;
        }
    }

    private static void SpawnAtMouse(ThingDef def)
    {
        IntVec3 cell = UI.MouseCell();
        if (cell.InBounds(Find.CurrentMap))
            GenPlace.TryPlaceThing(ThingMaker.MakeThing(def), cell, Find.CurrentMap, ThingPlaceMode.Near);
    }

    [DebugAction(Category, "Rotate fixers", allowedGameStates = AllowedGameStates.Playing)]
    private static void RotateFixers() => FixerManager.Get()?.Rotate(notify: true);

    [DebugAction(Category, "+20 reputation (all fixers)", allowedGameStates = AllowedGameStates.Playing)]
    private static void AddReputation()
    {
        foreach (Fixer fixer in FixerManager.Get()?.Fixers ?? [])
            fixer.GainReputation(20f);
    }
}
