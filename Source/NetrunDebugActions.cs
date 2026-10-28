using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// Dev-mode actions, grouped by what they touch (Maxim, 2026-10-14: one long list was hard to use). Each group is its
/// own category in the debug actions window; inside a group the order follows displayPriority (higher first).
/// New content gets an "Unlock:" action here when it lands (the ending too: docs/TASKS.md, I2).
/// </summary>
public static class NetrunDebugActions
{
    private const string Unlock = "Netrun: unlock and reset";
    private const string HeatCat = "Netrun: Heat and Netsick";
    private const string Net = "Netrun: cyberspace";
    private const string DiveCat = "Netrun: dives";
    private const string AICat = "Netrun: AIs";
    private const string Fixers = "Netrun: fixers and contracts";
    private const string Events = "Netrun: events";
    private const string Raids = "Netrun: test raids";
    private const string Spawn = "Netrun: spawn";
    private const string Tools = "Netrun: tools and achievements";

    private const AllowedGameStates Playing = AllowedGameStates.Playing;
    private const AllowedGameStates OnMap = AllowedGameStates.PlayingOnMap;

    private static void Done(string what) => Messages.Message("[Netrun dev] " + what, MessageTypeDefOf.NeutralEvent, historical: false);

    // ---- Unlock and reset ----

    [DebugAction(Unlock, "Unlock: everything", allowedGameStates = Playing, displayPriority = 100)]
    private static void UnlockEverything()
    {
        SkipOnboarding();
        UnlockResearch();
        ChartAll();
        OpenWall();
        GrantDaemons();
        if (GatewayManager.Get() is { } gateway)
            gateway.TrainingOffered = gateway.TrainingDone = true;
        Done("everything unlocked: onboarding, research, districts, the Wall, daemons");
    }

    /// <summary>Opens a deck theme without its achievement and puts it on (to check a new casing in game).</summary>
    [DebugAction(Unlock, "Unlock: deck theme...", allowedGameStates = Playing, displayPriority = 96)]
    private static void UnlockDeckTheme()
    {
        Find.WindowStack.Add(new FloatMenu(DeckThemes.All.Select(t => new FloatMenuOption(t.Label, () =>
        {
            OpenTheme(t);
            DeckThemes.Select(t);
            Done("deck theme open and on: " + t.Label);
        })).ToList()));
    }

    /// <summary>Opens every deck theme for good (kept in the mod settings, like an earned one). In a game only: allowedGameStates flags all have to hold, so Entry | Playing would hide it everywhere.</summary>
    [DebugAction(Unlock, "Unlock: all deck themes", allowedGameStates = Playing, displayPriority = 95)]
    private static void UnlockAllDeckThemes()
    {
        foreach (DeckTheme t in DeckThemes.All)
            OpenTheme(t);
        NetrunMod.Settings.Write();
        Done("all deck themes open; pick one in the mod settings or with \"Unlock: deck theme...\"");
    }

    private static void OpenTheme(DeckTheme t)
    {
        if (t.UnlockKey != null && !NetrunMod.Settings.UnlockedThemes.Contains(t.Id))
            NetrunMod.Settings.UnlockedThemes.Add(t.Id);
    }

    [DebugAction(Unlock, "Unlock: skip onboarding (all core nodes)", allowedGameStates = Playing, displayPriority = 90)]
    private static void SkipOnboarding()
    {
        NetworkGate gate = NetworkGate.Get();
        gate?.Open();
        gate?.SkipOnboarding();
    }

    [DebugAction(Unlock, "Unlock: Netrun and HE research", allowedGameStates = Playing, displayPriority = 85)]
    private static void UnlockResearch()
    {
        foreach (ResearchProjectDef p in DefDatabase<ResearchProjectDef>.AllDefs.Where(p => (p.defName.StartsWith("NR_") || p.defName.StartsWith("USH_")) && !p.IsFinished))
            Find.ResearchManager.FinishProject(p, doCompletionDialog: false, researcher: null, doCompletionLetter: false);
    }

    [DebugAction(Unlock, "Unlock: chart all districts", allowedGameStates = Playing, displayPriority = 80)]
    private static void ChartAll() => NetMapManager.Get()?.DevChartAll();

    [DebugAction(Unlock, "Unlock: open the Wall (Old Net)", allowedGameStates = Playing, displayPriority = 75)]
    private static void OpenWall() => NetMapManager.Get()?.OpenWall();

    [DebugAction(Unlock, "Unlock: orbit on / off (no gravship needed)", allowedGameStates = Playing, displayPriority = 70)]
    private static void ToggleOrbit()
    {
        NetMapManager.DevForceOrbit = !NetMapManager.DevForceOrbit;
        Done("orbit forced: " + NetMapManager.DevForceOrbit + (ModsConfig.OdysseyActive ? "" : " (no Odyssey: the map has no orbital hubs)"));
    }

    [DebugAction(Unlock, "Unlock: all daemons", allowedGameStates = Playing, displayPriority = 65)]
    private static void GrantDaemons()
    {
        foreach (DiveDaemon daemon in System.Enum.GetValues(typeof(DiveDaemon)))
            GatewayManager.Get()?.AddDaemon(daemon);
    }

    [DebugAction(Unlock, "Unlock: network gate open (threats on)", allowedGameStates = Playing, displayPriority = 60)]
    private static void GateOpen() => NetworkGate.Get()?.Open();

    // The ending (docs/CONCEPT.md, section 19): every step can be reached without playing the whole chain.

    [DebugAction(Unlock, "Ending: set stage...", allowedGameStates = Playing, displayPriority = 58)]
    private static void EndingStage()
    {
        TransitionManager t = TransitionManager.Get();
        string[] names = ["0 nothing", "1 rumour", "2 insider met", "3 first blueprint", "4 all blueprints", "5 first crossing"];
        Find.WindowStack.Add(new FloatMenu(names.Select((n, i) => new FloatMenuOption(n + (t?.Stage == i ? " (now)" : ""), () => t?.DevSetStage(i))).ToList()));
    }

    [DebugAction(Unlock, "Ending: blueprints...", allowedGameStates = Playing, displayPriority = 57)]
    private static void EndingBlueprints()
    {
        TransitionManager t = TransitionManager.Get();
        Find.WindowStack.Add(new FloatMenu(Enumerable.Range(0, TransitionManager.Blueprints + 1)
            .Select(i => new FloatMenuOption(i + " / " + TransitionManager.Blueprints, () => t?.DevSetBlueprints(i))).ToList()));
    }

    [DebugAction(Unlock, "Ending: meet the insider now", allowedGameStates = Playing, displayPriority = 56)]
    private static void EndingInsider() => TransitionManager.Get()?.MeetContact();

    [DebugAction(Unlock, "Ending: spawn a lab now (free)", allowedGameStates = Playing, displayPriority = 55)]
    private static void EndingLab()
    {
        TransitionManager t = TransitionManager.Get();
        if (t == null)
            return;
        if (!t.ContactMet)
            t.MeetContact();
        t.SpawnLab();
    }

    [DebugAction(Unlock, "Ending: open the research (all blueprints, finished)", allowedGameStates = Playing, displayPriority = 54)]
    private static void EndingResearch()
    {
        TransitionManager t = TransitionManager.Get();
        if (t == null)
            return;
        if (!t.ContactMet)
            t.MeetContact();
        t.DevSetBlueprints(TransitionManager.Blueprints);
        if (TransitionDefOf.NR_Res_Transition is { IsFinished: false } res)
            Find.ResearchManager.FinishProject(res, doCompletionDialog: false, researcher: null, doCompletionLetter: false);
    }

    [DebugAction(Unlock, "Ending: finish running uploads now", allowedGameStates = Playing, displayPriority = 53)]
    private static void EndingFinishUploads() => TransitionManager.Get()?.DevFinishUploads();

    [DebugAction(Unlock, "Ending: show the epilogue", allowedGameStates = Playing, displayPriority = 52)]
    private static void EndingEpilogue()
    {
        TransitionManager t = TransitionManager.Get();
        if (t == null)
            return;
        // Nobody crossed over yet: a preview with the colonists in cyberpods (or everyone), not saved.
        List<TransitionDeparted> preview = [];
        if (t.Departed.Count == 0)
        {
            List<Pawn> who = PawnsFinder.AllMaps_FreeColonists.Where(p => p.TryGetHoldingCyberpod(out _)).ToList();
            if (who.Count == 0)
                who = PawnsFinder.AllMaps_FreeColonists.ToList();
            foreach (Pawn p in who)
                preview.Add(new TransitionDeparted { Name = p.Name?.ToStringFull ?? p.LabelShort, Words = ("NR_TransitionWords_" + Rand.RangeInclusive(0, 11)).Translate(), Tick = Find.TickManager.TicksGame });
            t.Departed.AddRange(preview);
        }
        t.ShowEnding();
        foreach (TransitionDeparted d in preview)
            t.Departed.Remove(d);
    }

    [DebugAction(Unlock, "Ending: reset", allowedGameStates = Playing, displayPriority = 51)]
    private static void EndingReset() => TransitionManager.Get()?.DevReset();

    [DebugAction(Unlock, "Reset: newcomer start (gate, onboarding, guide, training offer, districts)", allowedGameStates = Playing, displayPriority = 40)]
    private static void NewcomerReset()
    {
        NetworkGate.Get()?.Close();
        // A full newcomer test: the guide screens, guide areas, first hints and the intro tour are new again.
        HelpProgress.Get()?.DevResetGuide();
        if (GatewayManager.Get() is { } gateway)
            gateway.TrainingOffered = gateway.TrainingDone = false;
        NetMapManager.Get()?.DevChartReset();
    }

    [DebugAction(Unlock, "Reset: districts back to the start", allowedGameStates = Playing, displayPriority = 35)]
    private static void ChartReset() => NetMapManager.Get()?.DevChartReset();

    [DebugAction(Unlock, "Reset: cooldowns (fixers, allies, AI)", allowedGameStates = Playing, displayPriority = 30)]
    private static void ResetCooldowns()
    {
        foreach (Fixer fixer in FixerManager.Get()?.Fixers ?? [])
            fixer.SilentUntilTick = 0;
        GatewayManager.Get()?.DevResetAllies();
        AIEventManager.Get()?.DevResetGap();
        ReadyAIs();
    }

    /// <summary>Locks every deck theme again and puts the classic deck back (to check the achievement unlock).</summary>
    [DebugAction(Unlock, "Reset: lock deck themes", allowedGameStates = Playing, displayPriority = 25)]
    private static void LockDeckThemes()
    {
        NetrunMod.Settings.UnlockedThemes.Clear();
        NetrunMod.Settings.FrameTheme = DeckThemes.All[0].Id;
        NetrunMod.Settings.Write();
        Done("deck themes locked, classic deck on");
    }

    // ---- Heat and Netsick ----

    [DebugAction(HeatCat, "Heat: set...", allowedGameStates = Playing, displayPriority = 100)]
    private static void SetHeat()
    {
        HeatTracker heat = HeatTracker.Get();
        var options = new List<FloatMenuOption>
        {
            new("+10", () => heat?.AddHeat(10f, "NR_LogDev".Translate())),
            new("-10", () => heat?.ReduceHeat(10f, "NR_LogDev".Translate())),
            new("Reset to 0", () => heat?.ReduceHeat(HeatTracker.MaxHeat, "NR_LogDev".Translate())),
        };
        foreach ((HeatLevel level, float value) in new[] { (HeatLevel.Shadow, 0f), (HeatLevel.Noticed, 30f), (HeatLevel.Tracked, 55f), (HeatLevel.Target, 80f), (HeatLevel.RedZone, 97f) })
            options.Add(new FloatMenuOption($"{level.Label().CapitalizeFirst()} ({value:0})", () => heat?.DevSetHeat(value, "NR_LogDev".Translate())));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    /// <summary>Red Zone in one click (T1.2 test: Cleaner cap 15 and double price, street contracts, entry +1).</summary>
    [DebugAction(HeatCat, "Heat: set 96", allowedGameStates = Playing, displayPriority = 99)]
    private static void SetHeat96() => HeatTracker.Get()?.DevSetHeat(96f, "NR_LogDev".Translate());

    [DebugAction(HeatCat, "Netsick: +0.3", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = OnMap, displayPriority = 90)]
    private static void AddNetsick(Pawn pawn)
    {
        Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick)
            ?? pawn.health.AddHediff(NetrunDefOf.NR_Netsick);
        hediff.Severity += 0.3f;
    }

    [DebugAction(HeatCat, "Netsick: -0.3", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = OnMap, displayPriority = 85)]
    private static void RemoveNetsick(Pawn pawn)
    {
        Hediff netsick = pawn?.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick);
        if (netsick != null)
            netsick.Severity -= 0.3f;
    }

    [DebugAction(HeatCat, "Netsick: start craving", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = OnMap, displayPriority = 80)]
    private static void StartNetCraving(Pawn pawn)
        => pawn.mindState?.mentalStateHandler?.TryStartMentalState(DefDatabase<MentalStateDef>.GetNamed("NR_NetCraving"), forced: true);

    [DebugAction(HeatCat, "Netsick: craving state", allowedGameStates = Playing, displayPriority = 75)]
    private static void CravingState() => Find.WindowStack.Add(new Dialog_MessageBox(NetsickManager.DebugState()));

    // ---- Cyberspace map and the Gateway ----

    /// <summary>Builds the cyberspace map again (for tests): a new random map of the chosen size, layout from the settings.</summary>
    [DebugAction(Net, "Map: regenerate...", allowedGameStates = Playing, displayPriority = 100)]
    private static void RegenerateMap()
    {
        var options = new List<DebugMenuOption>();
        foreach (NetSize size in System.Enum.GetValues(typeof(NetSize)))
            options.Add(new DebugMenuOption(size.ToString(), DebugMenuOptionMode.Action, () => NetMapManager.Get()?.Generate(size, freshSeed: true)));
        Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
    }

    [DebugAction(Net, "Map: event now", allowedGameStates = Playing, displayPriority = 90)]
    private static void MapNetEvent() => NetMapManager.Get()?.RunEvent();

    [DebugAction(Net, "Map: runners arrive now", allowedGameStates = Playing, displayPriority = 85)]
    private static void RunnersArrive() => NetMapManager.Get()?.DevArriveAll();

    [DebugAction(Net, "Gateway: spawn node...", allowedGameStates = Playing, displayPriority = 80)]
    private static void SpawnNode()
    {
        GatewayManager gateway = GatewayManager.Get();
        var options = System.Enum.GetValues(typeof(ExternalNodeType)).Cast<ExternalNodeType>()
            .Select(t => new FloatMenuOption(t.ToString(), () => gateway?.DevAddNode(t))).ToList();
        options.Add(new FloatMenuOption("Boss node (corporate core)", () => gateway?.SpawnBoss()));
        options.Add(new FloatMenuOption("Event nodes (rival, honeypot, wild AI)", () => gateway?.SpawnEventNodes()));
        options.Add(new FloatMenuOption("Counter-hack node (12 h)", () => gateway?.AddCounterHackNode(12f)));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    [DebugAction(Net, "Gateway: world effects...", allowedGameStates = Playing, displayPriority = 70)]
    private static void WorldEffects()
    {
        GatewayManager gateway = GatewayManager.Get();
        Find.WindowStack.Add(new FloatMenu(
        [
            new("Data storm", () => gateway?.StartStorm()),
            new("Headline now", () => gateway?.RollHeadline()),
            new("Corporate lockdown", () => gateway?.CorpLockdown()),
            new("+1 group favor (allies call)", () => { if (gateway != null) gateway.GroupFavor++; }),
        ]));
    }

    // ---- Dives ----

    [DebugAction(DiveCat, "Dive: start training dive", allowedGameStates = OnMap, displayPriority = 100)]
    private static void StartTraining()
    {
        GatewayManager gateway = GatewayManager.Get();
        AcceptanceReport can = gateway?.CanTrain(Find.CurrentMap) ?? false;
        if (!can.Accepted)
        {
            Messages.Message(can.Reason, MessageTypeDefOf.RejectInput, historical: false);
            return;
        }
        gateway.StartTraining(Find.CurrentMap);
        NetTerminalUtility.OpenTerminal(Find.CurrentMap);
    }

    [DebugAction(DiveCat, "Dive: finish as clean exit", allowedGameStates = Playing, displayPriority = 90)]
    private static void FinishDiveWin() => GatewayManager.Get()?.Session?.DevFinish(success: true);

    [DebugAction(DiveCat, "Dive: finish as failure", allowedGameStates = Playing, displayPriority = 85)]
    private static void FinishDiveFail() => GatewayManager.Get()?.Session?.DevFinish(success: false);

    [DebugAction(DiveCat, "Dive: show last summary", allowedGameStates = Playing, displayPriority = 80)]
    private static void ShowLastReport()
    {
        DiveReport report = GatewayManager.Get()?.LastReport;
        if (report != null)
            Find.WindowStack.Add(new Dialog_DiveReport(report));
        else
            Messages.Message("No dive yet", MessageTypeDefOf.RejectInput, historical: false);
    }

    /// <summary>Rolls the main files of every tier 100 times and logs min / average / max, to check the fixed ranges.</summary>
    [DebugAction(DiveCat, "Dive: roll rewards x100", allowedGameStates = Playing, displayPriority = 70)]
    private static void RollRewards()
    {
        var tiers = new (string name, ExternalNode node, bool hard)[]
        {
            ("low (defense 1-2)", new ExternalNode { Defense = 2, Type = ExternalNodeType.Market }, false),
            ("mid easy path", new ExternalNode { Defense = 3, Type = ExternalNodeType.Market }, false),
            ("mid hard path", new ExternalNode { Defense = 3, Type = ExternalNodeType.Market }, true),
            ("corp mid easy", new ExternalNode { Defense = 3, Type = ExternalNodeType.Corp }, false),
            ("corp mid hard", new ExternalNode { Defense = 3, Type = ExternalNodeType.Corp }, true),
            ("Black ICE", new ExternalNode { Defense = 5, BlackIce = true, Type = ExternalNodeType.Archive }, true),
            ("dark exchange (defense 3)", new ExternalNode { Defense = 3, Type = ExternalNodeType.DarkExchange }, false),
            ("corp core (boss)", new ExternalNode { Defense = 5, BlackIce = true, Type = ExternalNodeType.CorpCore }, true),
        };
        var sb = new System.Text.StringBuilder("[Netrun dev] Dive rewards x100 (cyberdata / advanced / silver, min-avg-max)\n");
        foreach ((string name, ExternalNode node, bool hard) in tiers)
        {
            var rolls = Enumerable.Range(0, 100).Select(_ => DiveSession.RollMain(node, hard)).ToList();
            sb.AppendLine($"{name}: data {rolls.Min(r => r.data)}-{rolls.Average(r => r.data):0.0}-{rolls.Max(r => r.data)}"
                + $", advanced {rolls.Min(r => r.advanced)}-{rolls.Average(r => r.advanced):0.0}-{rolls.Max(r => r.advanced)}"
                + $", silver {rolls.Min(r => r.silver)}-{rolls.Average(r => r.silver):0}-{rolls.Max(r => r.silver)}");
        }
        Log.Message(sb.ToString());
        Done("Rewards rolled, see the log");
    }

    /// <summary>Prints the node card forecast and 10 whole dives rolled the way a dive does (random route, caches, memory), flagging any outside the forecast.</summary>
    [DebugAction(DiveCat, "Dive: forecast vs roll x10 (selected node)", allowedGameStates = Playing, displayPriority = 69)]
    private static void ForecastVsRoll()
    {
        ExternalNode node = GatewayView.SelectedNode ?? GatewayManager.Get()?.AllBranches.FirstOrDefault();
        if (node == null)
        {
            Done("No node: open the cyberspace panel and select one");
            return;
        }
        CyberspaceSettingsDef s = NetrunDefOf.NR_Cyberspace;
        var sb = new System.Text.StringBuilder($"[Netrun dev] {node.Name}: defense {node.Defense}, Black ICE {node.BlackIce}, mult x{node.StackMult():0.##} (cap x{s.rewardMultCap:0.#}{(node.IsBoss ? ", core exempt" : "")})\nForecast card:\n");
        foreach ((_, string label, string amount, string tip) in node.RewardLines())
            sb.AppendLine($"  {label}: {amount}" + (tip.NullOrEmpty() ? "" : "  [" + tip.Replace("\n", " ") + "]"));
        var t = node.ForecastTotals();
        int forks = DiveSession.ForksFor(node);
        sb.AppendLine("Rolls (data / advanced / silver):");
        for (int i = 0; i < 10; i++)
        {
            int hardForks = Enumerable.Range(0, forks).Count(_ => Rand.Bool);
            bool hard = hardForks > 0;
            float passage = (forks - hardForks) * s.branchPassageBonus;
            (int data, int adv, int silver) = DiveSession.RollMain(node, hard, passage);
            int caches = hardForks + UnityEngine.Mathf.Max(0, forks - 1) + (forks == 0 && Rand.Chance(0.6f) ? 1 : 0);
            for (int c = 0; c < caches; c++)
                data += DiveSession.Scaled(node, s.cacheReward.RandomInRange, passage);
            if (node.ForgottenCache)
                data += DiveSession.Scaled(node, s.forgottenCacheReward.RandomInRange, passage);
            if (node.HasMemory)
                adv += UnityEngine.Mathf.RoundToInt(s.memoryAdvanced.RandomInRange * node.ExtraMult(false));
            bool ok = data >= t.dataMin && data <= t.dataMax && adv >= t.advMin && adv <= t.advMax && silver >= t.silverMin && silver <= t.silverMax;
            sb.AppendLine($"  {(hard ? "hard" : "easy")} x{hardForks}/{forks}: {data} / {adv} / {silver}" + (ok ? "" : "   <-- OUTSIDE the forecast"));
        }
        Log.Message(sb.ToString());
        Done("Forecast and rolls logged");
    }

    // ---- AIs ----

    /// <summary>Fires any AI event now, ignoring its conditions and limits, on an AI that fits its personality.</summary>
    [DebugAction(AICat, "AIs: fire event...", allowedGameStates = OnMap, displayPriority = 100)]
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

    [DebugAction(AICat, "AIs: offer quest...", allowedGameStates = Playing, displayPriority = 90)]
    private static void OfferAIQuest()
    {
        var options = (AIManager.Get()?.AIs ?? []).Select(ai => new FloatMenuOption(ai.Name + " (" + ai.Personality + ")", () =>
        {
            if (AIQuests.QuestOf(ai) != null || AIQuests.Get()?.Offer(ai) != true)
                Messages.Message(ai.Name + ": already has a quest or none can be offered", MessageTypeDefOf.RejectInput, historical: false);
        })).ToList();
        if (options.Count > 0)
            Find.WindowStack.Add(new FloatMenu(options));
    }

    [DebugAction(AICat, "AIs: trust and hunger...", allowedGameStates = Playing, displayPriority = 80)]
    private static void TrustHunger()
    {
        var options = new List<FloatMenuOption>();
        foreach (float d in new[] { 20f, -20f })
        {
            float delta = d;
            options.Add(new FloatMenuOption($"Trust {delta:+0;-0} (all)", () => { foreach (NetAI ai in AIManager.Get()?.AIs ?? []) ai.AddTrust(delta); }));
            options.Add(new FloatMenuOption($"Hunger {delta:+0;-0} (all)", () => { foreach (NetAI ai in AIManager.Get()?.AIs ?? []) ai.AddHunger(delta); }));
        }
        options.Add(new FloatMenuOption("Starve: trust 15, hunger 90 (escape test)", () =>
        {
            foreach (NetAI ai in AIManager.Get()?.AIs ?? [])
            {
                ai.Trust = 15f;
                ai.Hunger = 90f;
            }
        }));
        options.Add(new FloatMenuOption("Trust 5 (betrayal test)", () => { foreach (NetAI ai in AIManager.Get()?.AIs ?? []) ai.Trust = 5f; }));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    [DebugAction(AICat, "AIs: ready services and requests", allowedGameStates = Playing, displayPriority = 70)]
    private static void ReadyAIs()
    {
        foreach (NetAI ai in AIManager.Get()?.AIs ?? [])
        {
            ai.ServiceReadyTick = 0;
            ai.NextRequestTick = Find.TickManager.TicksGame;
        }
    }

    // ---- Fixers and contracts ----

    [DebugAction(Fixers, "Fixers: rotate", allowedGameStates = Playing, displayPriority = 100)]
    private static void RotateFixers() => FixerManager.Get()?.Rotate(notify: true);

    [DebugAction(Fixers, "Fixers: new pool", allowedGameStates = Playing, displayPriority = 90)]
    private static void NewFixerPool() => FixerManager.Get()?.DevNewPool();

    [DebugAction(Fixers, "Fixers: +20 reputation (all)", allowedGameStates = Playing, displayPriority = 80)]
    private static void AddReputation()
    {
        foreach (Fixer fixer in FixerManager.Get()?.Fixers ?? [])
            fixer.GainReputation(20f);
    }

    [DebugAction(Fixers, "Fixer: give AI fragment (shard offer)", allowedGameStates = Playing, displayPriority = 75)]
    private static void GiveShard()
    {
        Map map = Find.CurrentMap;
        GenPlace.TryPlaceThing(ThingMaker.MakeThing(NetrunDefOf.NR_AIFragment), UI.MouseCell().InBounds(map) ? UI.MouseCell() : map.Center, map, ThingPlaceMode.Near);
    }

    [DebugAction(Fixers, "Contracts: give accepted...", allowedGameStates = Playing, displayPriority = 70)]
    private static void GiveContract()
    {
        var options = ((ContractType[])System.Enum.GetValues(typeof(ContractType)))
            .Select(t => new FloatMenuOption(t.ToString(), () =>
            {
                if (ContractManager.Get()?.DevGive(t) != true)
                    Messages.Message("No fixer to give it", MessageTypeDefOf.RejectInput, historical: false);
            })).ToList();
        Find.WindowStack.Add(new FloatMenu(options));
    }

    [DebugAction(Fixers, "Contracts: complete active", allowedGameStates = Playing, displayPriority = 60)]
    private static void CompleteContracts() => ContractManager.Get()?.DevCompleteActive();

    // ---- Events ----

    [DebugAction(Events, "Colony events: fire...", allowedGameStates = OnMap, displayPriority = 100)]
    private static void FireMapEvent()
    {
        string[] names = ["NR_DataCourier", "NR_CorpHunt", "NR_BlackMarket", "NR_DistressSignal", "NR_MapHack"];
        var options = names.Select(n => DefDatabase<IncidentDef>.GetNamedSilentFail(n)).Where(d => d != null)
            .Select(d => new FloatMenuOption(d.LabelCap + " (" + d.defName + ")", () => Fire(d))).ToList();
        Find.WindowStack.Add(new FloatMenu(options));
    }

    [DebugAction(Events, "Colony events: fire a random one", allowedGameStates = OnMap, displayPriority = 90)]
    private static void MapEventNow()
    {
        var options = MapEventsManager.Candidates(Find.CurrentMap).Where(c => c.why == null).ToList();
        if (options.Count == 0)
        {
            Messages.Message("No map event can fire now: see \"Colony events: why not?\"", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }
        var pick = options.RandomElementByWeight(o => o.def.baseChance);
        pick.def.Worker.TryExecute(pick.parms);
    }

    /// <summary>Why each map event can or cannot come right now on this map.</summary>
    [DebugAction(Events, "Colony events: why not?", allowedGameStates = OnMap, displayPriority = 80)]
    private static void MapEventsWhy()
    {
        var lines = MapEventsManager.Candidates(Find.CurrentMap)
            .Select(c => $"{c.def.defName}: {c.why ?? "can fire"} (weight {c.def.baseChance})").ToList();
        lines.Insert(0, $"Schedule: one every {NetrunDefOf.NR_MapEvents.mapEventMtbDays} days / frequency {NetrunMod.Settings.MapEventFrequency}");
        Log.Message("[Netrun] " + string.Join("\n", lines));
        Messages.Message(string.Join("; ", lines), MessageTypeDefOf.NeutralEvent, historical: false);
    }

    [DebugAction(Events, "Incidents: fire any Netrun incident...", allowedGameStates = OnMap, displayPriority = 70)]
    private static void FireIncident()
    {
        var options = DefDatabase<IncidentDef>.AllDefs.Where(d => d.defName.StartsWith("NR_")).OrderBy(d => d.defName)
            .Select(d => new FloatMenuOption(d.defName, () => Fire(d))).ToList();
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void Fire(IncidentDef d)
    {
        IncidentParms parms = StorytellerUtility.DefaultParmsNow(d.category, Find.CurrentMap);
        parms.forced = true;
        if (!d.Worker.TryExecute(parms))
            Messages.Message(d.defName + ": could not fire here (conditions)", MessageTypeDefOf.RejectInput, historical: false);
    }

    // ---- Test raids: no Heat, alert or hostility conditions ----

    [DebugAction(Raids, "Raid: corporation...", allowedGameStates = OnMap, displayPriority = 100)]
    private static void RaidCorp() => PickFaction(NetFactions.Corps, DefDatabase<IncidentDef>.GetNamedSilentFail("NR_CorpRaid"));

    [DebugAction(Raids, "Raid: runner crew...", allowedGameStates = OnMap, displayPriority = 90)]
    private static void RaidCrew() => PickFaction(NetFactions.Crews, DefDatabase<IncidentDef>.GetNamedSilentFail("NR_CrewRaid"));

    [DebugAction(Raids, "Raid: mercenaries...", allowedGameStates = OnMap, displayPriority = 80)]
    private static void RaidMercs() => PickPoints(NetrunDefOf.NR_MercRaid, null);

    [DebugAction(Raids, "Raid: courier's hunters now", allowedGameStates = OnMap, displayPriority = 75)]
    private static void RaidCourierHunters() => MapEventsManager.Get()?.DevHuntersNow();

    [DebugAction(Raids, "Net attack: hit now (no Heat, defense or intrusion)...", allowedGameStates = OnMap, displayPriority = 65)]
    private static void NetAttackHit() => PickNetAttack(d => ((IncidentWorker_NetAttack)d.Worker).DevHit(Find.CurrentMap));

    [DebugAction(Raids, "Net attack: through the defenses (no Heat)...", allowedGameStates = OnMap, displayPriority = 64)]
    private static void NetAttackDefended() => PickNetAttack(d =>
    {
        IncidentParms parms = StorytellerUtility.DefaultParmsNow(d.category, Find.CurrentMap);
        parms.forced = true;
        return d.Worker.TryExecute(parms);
    });

    private static void PickNetAttack(System.Func<IncidentDef, bool> fire)
    {
        var options = DefDatabase<IncidentDef>.AllDefs.Where(d => d.Worker is IncidentWorker_NetAttack).OrderBy(d => d.defName)
            .Select(d => new FloatMenuOption(d.LabelCap + " (" + d.defName + ")", () =>
            {
                if (!fire(d))
                    Messages.Message(d.defName + ": nothing to hit here", MessageTypeDefOf.RejectInput, historical: false);
            })).ToList();
        Find.WindowStack.Add(new FloatMenu(options));
    }

    [DebugAction(Raids, "Raid: corporate hunt for the AI...", allowedGameStates = OnMap, displayPriority = 70)]
    private static void RaidCorpHunt() => PickPoints(DefDatabase<IncidentDef>.GetNamedSilentFail("NR_CorpHunt"), null);

    /// <summary>To the log: every hostile pawn on this map with its kind, faction, weapon, the weapon's tags and the kind's tags.</summary>
    [DebugAction(Raids, "Raid: weapons report (log)", allowedGameStates = OnMap, displayPriority = 60)]
    private static void RaidWeaponsReport()
    {
        var lines = Find.CurrentMap.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Humanlike && p.HostileTo(Faction.OfPlayer))
            .GroupBy(p => (p.kindDef, p.Faction, weapon: p.equipment?.Primary?.def))
            .Select(g => $"{g.Count()}x {g.Key.kindDef.defName} [{g.Key.Faction?.def.defName}] money {g.Key.kindDef.weaponMoney} tags {string.Join(",", g.Key.kindDef.weaponTags ?? [])}"
                         + $" -> {g.Key.weapon?.defName ?? "none"} ({g.Key.weapon?.BaseMarketValue:0}) tags {string.Join(",", g.Key.weapon?.weaponTags ?? [])}")
            .ToList();
        Log.Message("[Netrun] Raid weapons:\n" + string.Join("\n", lines));
        Done(lines.Count + " groups written to the log");
    }

    private static void PickFaction(IEnumerable<Faction> factions, IncidentDef def)
    {
        var options = factions.Select(f => new FloatMenuOption(f.Name + (f.HostileTo(Faction.OfPlayer) ? "" : " (will turn hostile)"), () => PickPoints(def, f))).ToList();
        if (options.Count == 0)
        {
            Messages.Message("[Netrun dev] No such faction in this game.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void PickPoints(IncidentDef def, Faction faction)
    {
        if (def == null)
            return;
        var options = new[] { 0f, 300f, 800f, 1500f, 3000f }
            .Select(p => new FloatMenuOption(p <= 0f ? "Storyteller points" : p + " points", () => TestRaid(def, faction, p))).ToList();
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void TestRaid(IncidentDef def, Faction faction, float points)
    {
        if (faction != null && !faction.HostileTo(Faction.OfPlayer))
            Faction.OfPlayer.TryAffectGoodwillWith(faction, -200, canSendMessage: false, canSendHostilityLetter: false);
        IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, Find.CurrentMap);
        parms.forced = true;
        parms.faction = faction;
        if (points > 0f)
            parms.points = points;
        if (!def.Worker.TryExecute(parms))
        {
            string why = def == NetrunDefOf.NR_MercRaid || def.defName == "NR_CorpHunt" ? (Mercenaries.Faction == null ? "no mercenary faction" : "the raid did not start") : "the raid did not start";
            Log.Warning($"[Netrun] Test raid {def.defName}: {why}.");
            Messages.Message($"[Netrun dev] {def.defName}: {why}", MessageTypeDefOf.RejectInput, historical: false);
        }
    }

    // ---- Spawn ----

    /// <summary>Click on the map: an encrypted crate to hack, a cyberlink to install and some cyberdata.</summary>
    [DebugAction(Spawn, "Spawn: hacking test kit", actionType = DebugActionType.ToolMap, allowedGameStates = OnMap, displayPriority = 100)]
    private static void SpawnHackingTestKit()
    {
        Map map = Find.CurrentMap;
        IntVec3 cell = UI.MouseCell();
        if (!cell.InBounds(map))
            return;
        Give("USH_EncryptedCrate", 1);
        Give("USH_Cyberlink", 1);
        Give("USH_Cyberdata", 10);

        void Give(string defName, int count)
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

    [DebugAction(Spawn, "Spawn: Netrun items...", actionType = DebugActionType.ToolMap, allowedGameStates = OnMap, displayPriority = 90)]
    private static void GiveResources()
    {
        IntVec3 cell = UI.MouseCell();
        Map map = Find.CurrentMap;
        void Give(ThingDef def, int count)
        {
            if (def == null || !cell.InBounds(map))
                return;
            while (count > 0)
            {
                Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
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
            new("AI socket", () => Give(DefDatabase<ThingDef>.GetNamedSilentFail("NR_AISocket"), 1)),
            new("3 braindance chips", () => Give(NetrunDefOf.NR_BraindanceChip, 3)),
            new("3 black braindance chips", () => Give(NetrunDefOf.NR_BlackBraindanceChip, 3)),
            new("3 raw recordings", () => Give(NetrunDefOf.NR_BraindanceRaw, 3)),
            new("Unique braindance (random)", () => Give(UniqueBraindance.All.RandomElementWithFallback(), 1)),
            new("500 silver", () => Give(ThingDefOf.Silver, 500)),
        };
        Find.WindowStack.Add(new FloatMenu(options));
    }

    // ---- Tools and achievements ----

    [DebugAction(Tools, "Terminal: open", allowedGameStates = Playing, displayPriority = 100)]
    private static void OpenTerminal() => NetTerminalUtility.OpenTerminal(Find.CurrentMap);

    /// <summary>Moves the clock ahead (timed achievements, hourly checks, contract deadlines); the hourly checks catch up on their next run.</summary>
    [DebugAction(Tools, "Time: skip ahead...", allowedGameStates = Playing, displayPriority = 90)]
    private static void SkipTime()
    {
        var options = new List<FloatMenuOption>();
        foreach (int h in new[] { 1, 6, 24, 72 })
        {
            int hours = h;
            options.Add(new FloatMenuOption(hours + " h", () => Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame + hours * GenDate.TicksPerHour)));
        }
        Find.WindowStack.Add(new FloatMenu(options));
    }

    /// <summary>Balance protocol: what the last 10 days brought and cost.</summary>
    [DebugAction(Tools, "Economy: summary (10 days)", allowedGameStates = Playing, displayPriority = 80)]
    private static void EconomySummary()
        => Find.WindowStack.Add(new Dialog_MessageBox(EconomyTracker.Get()?.Summary(10) ?? "-"));

    /// <summary>Runs the data janitor now and shows what it cleaned (docs/TECH_DETAILS.md, section 2.5).</summary>
    [DebugAction(Tools, "Janitor: cleanup report", allowedGameStates = Playing, displayPriority = 70)]
    private static void CleanupReport()
    {
        List<string> lines = NetrunJanitor.Get()?.Run(CleanupReason.Manual) ?? [];
        Done("cleanup: " + (lines.Count > 0 ? string.Join(", ", lines) : "nothing to clean"));
    }

    /// <summary>Save migration state: save version, current version, one line per step (BALANCE_PLAN 20.4).</summary>
    [DebugAction(Tools, "Migration: report", allowedGameStates = Playing, displayPriority = 68)]
    private static void MigrationReport() => ShowMigration("report", NetrunMigration.Get()?.Report());

    /// <summary>Sets the save version back and reruns the steps from there (steps are idempotent).</summary>
    [DebugAction(Tools, "Migration: rerun from version N...", allowedGameStates = Playing, displayPriority = 67)]
    private static void MigrationRerun()
    {
        var options = new List<FloatMenuOption>();
        for (int v = 1; v <= NetrunMigration.CurrentVersion; v++)
        {
            int from = v;
            options.Add(new FloatMenuOption("from v" + from, () => ShowMigration("rerun from v" + from, NetrunMigration.Get()?.RerunFrom(from))));
        }
        Find.WindowStack.Add(new FloatMenu(options));
    }

    /// <summary>Pretends the save is version 0 and runs every step on the current game.</summary>
    [DebugAction(Tools, "Migration: simulate old save", allowedGameStates = Playing, displayPriority = 66)]
    private static void MigrationSimulate() => ShowMigration("simulate old save", NetrunMigration.Get()?.RerunFrom(1));

    private static void ShowMigration(string what, List<string> lines)
    {
        string text = $"[Netrun] migration {what}:\n" + (lines is { Count: > 0 } ? string.Join("\n", lines) : "nothing ran");
        Log.Message(text);
        Find.WindowStack.Add(new Dialog_MessageBox(text));
    }

    /// <summary>Opens one achievement (or all) through its counter, the same signal the game sends, not around it.</summary>
    [DebugAction(Tools, "Achievements: unlock...", allowedGameStates = Playing, displayPriority = 60)]
    private static void UnlockAchievement()
    {
        var options = new List<FloatMenuOption>
        {
            new("ALL", () =>
            {
                foreach (NetrunAchievementDef a in DefDatabase<NetrunAchievementDef>.AllDefs)
                    NetrunSignals.Max(a.key, a.count);
            }),
        };
        foreach (NetrunAchievementDef a in DefDatabase<NetrunAchievementDef>.AllDefs.OrderBy(a => a.order))
        {
            NetrunAchievementDef ach = a;
            string done = NetrunSignals.Value(a.key) >= a.count ? " ✓" : $" ({NetrunSignals.Value(a.key)}/{a.count})";
            options.Add(new FloatMenuOption(a.LabelCap + done, () => NetrunSignals.Max(ach.key, ach.count)));
        }
        Find.WindowStack.Add(new FloatMenu(options));
    }

    [DebugAction(Tools, "Achievements: set counter...", allowedGameStates = Playing, displayPriority = 55)]
    private static void SetCounter()
    {
        var options = new List<FloatMenuOption>();
        foreach (NetrunAchievementDef a in DefDatabase<NetrunAchievementDef>.AllDefs.OrderBy(a => a.order))
        {
            NetrunAchievementDef ach = a;
            options.Add(new FloatMenuOption($"{a.key} = {NetrunSignals.Value(a.key)} (goal {a.count})", () =>
            {
                var values = new List<FloatMenuOption>();
                foreach (int v in new[] { 0, 1, ach.count / 2, ach.count - 1, ach.count }.Distinct().Where(v => v >= 0))
                {
                    int value = v;
                    values.Add(new FloatMenuOption(value.ToString(), () => NetrunCounters.Get()?.DevSet(ach.key, value)));
                }
                Find.WindowStack.Add(new FloatMenu(values));
            }));
        }
        Find.WindowStack.Add(new FloatMenu(options));
    }

    [DebugAction(Tools, "Achievements: show counters", allowedGameStates = Playing, displayPriority = 50)]
    private static void AchievementCounters() => Find.WindowStack.Add(new Dialog_MessageBox(NetrunCounters.Get()?.DebugState() ?? "-"));

    /// <summary>Counters back to zero (the mod's own list of achievements; an achievement VAE already gave stays given).</summary>
    [DebugAction(Tools, "Achievements: reset counters", allowedGameStates = Playing, displayPriority = 45)]
    private static void ResetCounters() => NetrunCounters.Get()?.DevReset();

    /// <summary>Which terminal icons the tint finds and recolours (for the "icons stay white" reports on 1.7).</summary>
    [DebugAction(Tools, "UI: icon tint report", allowedGameStates = Playing, displayPriority = 40)]
    private static void TintReport() => Find.WindowStack.Add(new Dialog_MessageBox(NetrunTint.DebugReport()));
}
