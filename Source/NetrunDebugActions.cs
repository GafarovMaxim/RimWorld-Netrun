using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace Netrun;

public static class NetrunDebugActions
{
    private const string Category = "Netrun";

    [DebugAction(Category, "+0.3 Netsick", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void AddNetsick(Pawn pawn)
    {
        Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick)
            ?? pawn.health.AddHediff(NetrunDefOf.NR_Netsick);
        hediff.Severity += 0.3f;
    }

    [DebugAction(Category, "Start net craving", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void StartNetCraving(Pawn pawn)
        => pawn.mindState?.mentalStateHandler?.TryStartMentalState(DefDatabase<MentalStateDef>.GetNamed("NR_NetCraving"), forced: true);

    [DebugAction(Category, "+10 Heat", allowedGameStates = AllowedGameStates.Playing)]
    private static void AddHeat() => HeatTracker.Get()?.AddHeat(10f, "NR_LogDev".Translate());

    [DebugAction(Category, "-10 Heat", allowedGameStates = AllowedGameStates.Playing)]
    private static void RemoveHeat() => HeatTracker.Get()?.ReduceHeat(10f, "NR_LogDev".Translate());

    [DebugAction(Category, "Reset Heat", allowedGameStates = AllowedGameStates.Playing)]
    private static void ResetHeat() => HeatTracker.Get()?.ReduceHeat(HeatTracker.MaxHeat, "NR_LogDev".Translate());

    /// <summary>Fires any AI event now, ignoring its conditions and limits, on an AI that fits its personality.</summary>
    [DebugAction(Category, "Fire AI event...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void FireAIEvent()
    {
        AIManager manager = AIManager.Get();
        var options = new List<FloatMenuOption>();
        foreach (AIEventDef def in DefDatabase<AIEventDef>.AllDefsListForReading)
        {
            NetAI ai = manager?.AIs.FirstOrDefault(a => a.Housed && (def.General || def.personalities.Contains(a.Personality)));
            if (ai == null)
                continue;
            AIEventDef captured = def;
            options.Add(new FloatMenuOption(def.defName + ": " + def.label + " (" + ai.Name + ")",
                () => AIEventManager.Get()?.Fire(captured, AIEventContext.Make(ai, Find.CurrentMap, captured))));
        }
        if (options.Count == 0)
            options.Add(new FloatMenuOption("No awakened AI fits any event.", null));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    /// <summary>Balance protocol (docs/BALANCE.md): what the last 10 days brought and cost.</summary>
    [DebugAction(Category, "Economy summary (10 days)", allowedGameStates = AllowedGameStates.Playing)]
    private static void EconomySummary()
        => Find.WindowStack.Add(new Dialog_MessageBox(EconomyTracker.Get()?.Summary(10) ?? "-"));

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
