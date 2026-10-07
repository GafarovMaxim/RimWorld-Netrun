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

    /// <summary>Why each map event can or cannot come right now on this map.</summary>
    [DebugAction(Category, "Map events: why not?", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void MapEventsWhy()
    {
        var lines = MapEventsManager.Candidates(Find.CurrentMap)
            .Select(c => $"{c.def.defName}: {c.why ?? "can fire"} (weight {c.def.baseChance})").ToList();
        lines.Insert(0, $"Schedule: one every {NetrunDefOf.NR_MapEvents.mapEventMtbDays} days / frequency {NetrunMod.Settings.MapEventFrequency}");
        Log.Message("[Netrun] " + string.Join("\n", lines));
        Messages.Message(string.Join("; ", lines), MessageTypeDefOf.NeutralEvent, historical: false);
    }

    [DebugAction(Category, "Map event: fire a random one now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void MapEventNow()
    {
        var options = MapEventsManager.Candidates(Find.CurrentMap).Where(c => c.why == null).ToList();
        if (options.Count == 0)
        {
            Messages.Message("No map event can fire now: see \"Map events: why not?\"", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }
        var pick = options.RandomElementByWeight(o => o.def.baseChance);
        pick.def.Worker.TryExecute(pick.parms);
    }

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

    [DebugAction(Category, "Spawn boss node", allowedGameStates = AllowedGameStates.Playing)]
    private static void SpawnBossNode() => GatewayManager.Get()?.SpawnBoss();

    [DebugAction(Category, "Spawn event nodes", allowedGameStates = AllowedGameStates.Playing)]
    private static void SpawnEventNodes() => GatewayManager.Get()?.SpawnEventNodes();

    [DebugAction(Category, "Start data storm", allowedGameStates = AllowedGameStates.Playing)]
    private static void StartStorm() => GatewayManager.Get()?.StartStorm();

    [DebugAction(Category, "Grant all daemons", allowedGameStates = AllowedGameStates.Playing)]
    private static void GrantDaemons()
    {
        foreach (DiveDaemon daemon in System.Enum.GetValues(typeof(DiveDaemon)))
            GatewayManager.Get()?.AddDaemon(daemon);
    }

    [DebugAction(Category, "+1 hacker group favor", allowedGameStates = AllowedGameStates.Playing)]
    private static void AddFavor()
    {
        if (GatewayManager.Get() is { } gateway)
            gateway.GroupFavor++;
    }

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

    // ---- Added 2026-10-06: more test buttons ----

    [DebugAction(Category, "-0.3 Netsick", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void RemoveNetsick(Pawn pawn)
    {
        Hediff netsick = pawn?.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick);
        if (netsick != null)
            netsick.Severity -= 0.3f;
    }

    [DebugAction(Category, "Set Heat level...", allowedGameStates = AllowedGameStates.Playing)]
    private static void SetHeatLevel()
    {
        var levels = new List<(HeatLevel level, float value)>
        {
            (HeatLevel.Shadow, 0f), (HeatLevel.Noticed, 30f), (HeatLevel.Tracked, 55f), (HeatLevel.Target, 80f), (HeatLevel.RedZone, 97f),
        };
        Find.WindowStack.Add(new FloatMenu(levels.Select(l => new FloatMenuOption($"{l.level.Label().CapitalizeFirst()} ({l.value:0})",
            () => HeatTracker.Get()?.DevSetHeat(l.value, "NR_LogDev".Translate()))).ToList()));
    }

    [DebugAction(Category, "Fire Netrun incident...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void FireIncident()
    {
        var options = DefDatabase<IncidentDef>.AllDefs.Where(d => d.defName.StartsWith("NR_")).OrderBy(d => d.defName)
            .Select(d => new FloatMenuOption(d.defName, () =>
            {
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(d.category, Find.CurrentMap);
                parms.forced = true;
                if (!d.Worker.TryExecute(parms))
                    Messages.Message(d.defName + ": could not fire here", MessageTypeDefOf.RejectInput, historical: false);
            })).ToList();
        Find.WindowStack.Add(new FloatMenu(options));
    }

    [DebugAction(Category, "Spawn Gateway node...", allowedGameStates = AllowedGameStates.Playing)]
    private static void SpawnNode()
    {
        var options = System.Enum.GetValues(typeof(ExternalNodeType)).Cast<ExternalNodeType>()
            .Select(t => new FloatMenuOption(t.ToString(), () => GatewayManager.Get()?.DevAddNode(t))).ToList();
        options.Add(new FloatMenuOption("Event nodes (rival, honeypot, wild AI)", () => GatewayManager.Get()?.SpawnEventNodes()));
        options.Add(new FloatMenuOption("Counter-hack node (12 h)", () => GatewayManager.Get()?.AddCounterHackNode(12f)));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    [DebugAction(Category, "Dive: finish as clean exit", allowedGameStates = AllowedGameStates.Playing)]
    private static void FinishDiveWin() => GatewayManager.Get()?.Session?.DevFinish(success: true);

    [DebugAction(Category, "Dive: finish as failure", allowedGameStates = AllowedGameStates.Playing)]
    private static void FinishDiveFail() => GatewayManager.Get()?.Session?.DevFinish(success: false);

    [DebugAction(Category, "Show last dive summary", allowedGameStates = AllowedGameStates.Playing)]
    private static void ShowLastReport()
    {
        DiveReport report = GatewayManager.Get()?.LastReport;
        if (report != null)
            Find.WindowStack.Add(new Dialog_DiveReport(report));
        else
            Messages.Message("No dive yet", MessageTypeDefOf.RejectInput, historical: false);
    }

    [DebugAction(Category, "Give resources...", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void GiveResources()
    {
        IntVec3 cell = UI.MouseCell();
        Map map = Find.CurrentMap;
        void Give(ThingDef def, int count)
        {
            while (count > 0)
            {
                Thing thing = ThingMaker.MakeThing(def);
                thing.stackCount = System.Math.Min(count, def.stackLimit);
                count -= thing.stackCount;
                GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
            }
        }
        var options = new List<FloatMenuOption>
        {
            new("20 cyberdata", () => Give(NetrunDefOf.USH_Cyberdata, 20)),
            new("5 advanced cyberdata", () => Give(NetrunDefOf.USH_AdvancedCyberdata, 5)),
            new("AI fragment", () => Give(NetrunDefOf.NR_AIFragment, 1)),
            new("AI core module", () => Give(NetrunDefOf.NR_AICoreModule, 1)),
            new("3 braindance chips", () => Give(NetrunDefOf.NR_BraindanceChip, 3)),
            new("3 black braindance chips", () => Give(NetrunDefOf.NR_BlackBraindanceChip, 3)),
            new("3 raw recordings", () => Give(NetrunDefOf.NR_BraindanceRaw, 3)),
            new("500 silver", () => Give(ThingDefOf.Silver, 500)),
        };
        Find.WindowStack.Add(new FloatMenu(options));
    }

    [DebugAction(Category, "Reset cooldowns (fixers, group, AI)", allowedGameStates = AllowedGameStates.Playing)]
    private static void ResetCooldowns()
    {
        foreach (Fixer fixer in FixerManager.Get()?.Fixers ?? [])
            fixer.SilentUntilTick = 0;
        GatewayManager.Get()?.DevResetAllies();
        AIEventManager.Get()?.DevResetGap();
        ReadyAIs();
    }

    [DebugAction(Category, "Fixers: new pool", allowedGameStates = AllowedGameStates.Playing)]
    private static void NewFixerPool() => FixerManager.Get()?.DevNewPool();

    [DebugAction(Category, "AIs: +20 trust", allowedGameStates = AllowedGameStates.Playing)]
    private static void TrustUp()
    {
        foreach (NetAI ai in AIManager.Get()?.AIs ?? [])
            ai.AddTrust(20f);
    }

    [DebugAction(Category, "AIs: -20 trust", allowedGameStates = AllowedGameStates.Playing)]
    private static void TrustDown()
    {
        foreach (NetAI ai in AIManager.Get()?.AIs ?? [])
            ai.AddTrust(-20f);
    }

    [DebugAction(Category, "AIs: +20 hunger", allowedGameStates = AllowedGameStates.Playing)]
    private static void HungerUp()
    {
        foreach (NetAI ai in AIManager.Get()?.AIs ?? [])
            ai.AddHunger(20f);
    }

    [DebugAction(Category, "AIs: -20 hunger", allowedGameStates = AllowedGameStates.Playing)]
    private static void HungerDown()
    {
        foreach (NetAI ai in AIManager.Get()?.AIs ?? [])
            ai.AddHunger(-20f);
    }

    [DebugAction(Category, "Network gate: open", allowedGameStates = AllowedGameStates.Playing)]
    private static void GateOpen() => NetworkGate.Get()?.Open();

    [DebugAction(Category, "Network gate: close (newcomer test)", allowedGameStates = AllowedGameStates.Playing)]
    private static void GateClose() => NetworkGate.Get()?.Close();

    [DebugAction(Category, "Netsick: craving state", allowedGameStates = AllowedGameStates.Playing)]
    private static void CravingState() => Find.WindowStack.Add(new Dialog_MessageBox(NetsickManager.DebugState()));

    [DebugAction(Category, "Map events...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void FireMapEvent()
    {
        string[] names = ["NR_DataCourier", "NR_CorpHunt", "NR_BlackMarket", "NR_DistressSignal", "NR_MapHack"];
        var options = names.Select(n => DefDatabase<IncidentDef>.GetNamedSilentFail(n)).Where(d => d != null)
            .Select(d => new FloatMenuOption(d.LabelCap + " (" + d.defName + ")", () =>
            {
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(d.category, Find.CurrentMap);
                parms.forced = true;
                if (!d.Worker.TryExecute(parms))
                    Messages.Message(d.defName + ": could not fire here (conditions)", MessageTypeDefOf.RejectInput, historical: false);
            })).ToList();
        Find.WindowStack.Add(new FloatMenu(options));
    }
}
