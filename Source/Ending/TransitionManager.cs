using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

[DefOf]
public static class TransitionDefOf
{
    public static ResearchProjectDef NR_Res_Transition;
    public static HediffDef NR_DigitalShell;
    public static ThoughtDef NR_WentToNet;

    static TransitionDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(TransitionDefOf));
}

/// <summary>A colonist's mind being moved into the net from a cyberpod (one in-game day).</summary>
public class TransitionUpload : IExposable
{
    public Pawn Pawn;
    public Building_Cyberpod Pod;
    public int StartTick;

    public void ExposeData()
    {
        Scribe_References.Look(ref Pawn, "pawn");
        Scribe_References.Look(ref Pod, "pod");
        Scribe_Values.Look(ref StartTick, "start");
    }
}

/// <summary>Someone who crossed over: kept by name, so the epilogue survives a lost map or a dead body.</summary>
public class TransitionDeparted : IExposable
{
    public string Name;
    public string Words;
    public int Tick;

    public void ExposeData()
    {
        Scribe_Values.Look(ref Name, "name");
        Scribe_Values.Look(ref Words, "words");
        Scribe_Values.Look(ref Tick, "tick");
    }
}

/// <summary>
/// The ending, "The Transition" (docs/CONCEPT.md, section 19; "digital afterlife" in the flavour texts).
/// Stages: 0 nothing, 1 a rumour in the feed, 2 the insider met (a guest card in the Club pool), 3 the first
/// prototype, 4 all five (the research opens), 5 the first crossing. The insider sells coordinates of corporate black
/// labs (Black ICE, an advanced cyberdata at the door, +Heat); the letter names the lab and its hub, and the hub is
/// marked on the map while the lab exists. A clean lab dive may yield a prototype (vanilla techprint of the research,
/// dropped next to the runner's pod), with a pity rule. The research needs all five techprints; where the game has no
/// techprint item for it, our own counter gates the research instead. A colonist with a cyberlink in a powered pod then uploads for a day; the body stays in the pod as an empty
/// shell (frozen, no ticks). When no colonist is left but shells, the epilogue offers the credits or a new start.
/// Balance: a RimWorld year is 60 days; the whole chain is meant to land around day 180-240 (vanilla endings: years 3-4).
/// </summary>
public class TransitionManager : GameComponent
{
    // ---- Balance (see the class summary) ----
    public const int EligibleDay = 60;
    public const int EligibleDives = 10;
    public const float ContactChancePerRotation = 0.35f;
    public const int Blueprints = 5;
    public const float BlueprintChance = 0.4f;
    /// <summary>Clean lab dives without a blueprint before the next one is certain.</summary>
    public const int PityAfter = 2;
    public const int CoordCyberdata = 20;
    public const int CoordSilver = 300;
    public const float LabCooldownDays = 8f;
    public const float LabLifetimeDays = 6f;
    public const float LabExtraHeat = 15f;
    public const float UploadHours = 24f;
    public const float UploadHeatPerHour = 1f;
    public const float PurgeChance = 0.3f;
    public const float InterruptNetsick = 0.5f;
    public const float InterruptDeathChance = 0.35f;

    private const int TickInterval = 250;

    public int Stage;
    public int BlueprintsFound;
    public string ContactName;
    private int _cleanWithoutPrint;
    private int _nextLabTick = -1;
    /// <summary>The hub we marked for the active lab (-1 none): only our own mark is ever cleared.</summary>
    private int _labHub = -1;
    private List<string> _usedCodeNames = [];
    private List<TransitionUpload> _uploads = [];
    public List<TransitionDeparted> Departed = [];
    private bool _endingShown;

    public TransitionManager(Game game) { }

    public static TransitionManager Get() => GameComp<TransitionManager>.Get();

    public IReadOnlyList<TransitionUpload> Uploads => _uploads;
    public bool ContactMet => Stage >= 2;

    public static bool IsShell(Pawn p) => p?.health?.hediffSet != null && TransitionDefOf.NR_DigitalShell != null
                                          && p.health.hediffSet.HasHediff(TransitionDefOf.NR_DigitalShell);

    public bool Uploading(Pawn p) => p != null && _uploads.Any(u => u.Pawn == p);

    public ExternalNode ActiveLab => GatewayManager.Get()?.AllBranches.FirstOrDefault(n => n.Lab);

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        ApplyResearchText();
    }

    public override void GameComponentTick()
    {
        int now = Find.TickManager.TicksGame;
        if (now % TickInterval != 0)
            return;
        if (Stage == 0 && now % 2500 == 0)
            CheckRumour();
        if (_uploads.Count > 0)
            TickUploads(now);
        if (now % 2500 == 0)
        {
            if (_labHub >= 0)
                CheckLabMark();
            if (Departed.Count > 0 && !_endingShown)
                CheckEnding();
        }
    }

    // ---- Stages ----

    private void SetStage(int stage)
    {
        if (stage <= Stage)
            return;
        Stage = stage;
        ApplyResearchText();
    }

    /// <summary>Stage 1: once the colony is an established crew, a rumour about the labs reaches the feed.</summary>
    private void CheckRumour()
    {
        if (NetworkGate.Get()?.OnboardingDone != true || GenDate.DaysPassed < EligibleDay || NetrunSignals.Value("Dives") < EligibleDives)
            return;
        SetStage(1);
        GatewayManager.Get()?.AddHeadline("NR_TransitionRumour".Translate(), 2);
        Messages.Message("NR_TransitionRumourMsg".Translate(), MessageTypeDefOf.NeutralEvent, historical: true);
    }

    /// <summary>Called by the fixer rotation: at stage 1 the insider may get in touch through the pool.</summary>
    public void OnFixersRotated()
    {
        if (Stage == 1 && Rand.Chance(ContactChancePerRotation))
            MeetContact();
    }

    public void MeetContact()
    {
        if (ContactMet)
            return;
        ContactName = ((string)"NR_InsiderNames".Translate()).Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).RandomElementWithFallback("Vesper");
        SetStage(2);
        Find.LetterStack.ReceiveLetter("NR_TransitionContactLabel".Translate(), "NR_TransitionContact".Translate(ContactName, CoordCyberdata, CoordSilver),
            LetterDefOf.PositiveEvent);
    }

    // ---- The insider and the labs ----

    public int TicksUntilNextLab => System.Math.Max(0, _nextLabTick - Find.TickManager.TicksGame);

    public AcceptanceReport CanBuyCoordinates(Map map)
    {
        if (!ContactMet)
            return false;
        if (BlueprintsFound >= Blueprints)
            return "NR_InsiderNoMore".Translate();
        if (ActiveLab != null)
            return "NR_InsiderLabActive".Translate(ActiveLab.Name, LabHubLabel(ActiveLab));
        if (TicksUntilNextLab > 0)
            return "NR_InsiderCooldown".Translate(TicksUntilNextLab.ToStringTicksToPeriod());
        if (map == null || ColonyGoods.CountCached(map, NetrunDefOf.USH_Cyberdata) < CoordCyberdata || ColonyGoods.CountCached(map, ThingDefOf.Silver) < CoordSilver)
            return "NR_InsiderCantPay".Translate(CoordCyberdata, CoordSilver);
        return true;
    }

    public void BuyCoordinates(Map map)
    {
        if (!CanBuyCoordinates(map).Accepted)
            return;
        ColonyGoods.TryTake(map, NetrunDefOf.USH_Cyberdata, CoordCyberdata);
        ColonyGoods.TryTake(map, ThingDefOf.Silver, CoordSilver);
        SpawnLab();
    }

    /// <summary>A lab on the corporate ground with a code name not used yet in this game.</summary>
    public ExternalNode SpawnLab()
    {
        GatewayManager gateway = GatewayManager.Get();
        if (gateway == null)
            return null;
        List<string> names = ((string)"NR_LabCodeNames".Translate()).Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        string code = names.Where(n => !_usedCodeNames.Contains(n)).RandomElementWithFallback(names.RandomElementWithFallback("Lazarus"));
        _usedCodeNames.Add(code);
        if (_usedCodeNames.Count > 40)
            _usedCodeNames.RemoveAt(0);
        ExternalNode lab = ExternalNode.GenerateLab(gateway.NextBranchId(), code, LabLifetimeDays);
        gateway.AddBranch(lab, District.Corp);
        _nextLabTick = Find.TickManager.TicksGame + (int)(LabCooldownDays * GenDate.TicksPerDay);
        MarkLabHub(lab);
        Find.LetterStack.ReceiveLetter("NR_LabFoundLabel".Translate(),
            "NR_LabFound".Translate(ContactName ?? "?", lab.Name, LabHubLabel(lab), LabLifetimeDays.ToString("0")), LetterDefOf.PositiveEvent);
        return lab;
    }

    /// <summary>"Obsidian 14": the hub the lab hangs on (a letter cannot point at a map hub, so the text names it).</summary>
    public static string LabHubLabel(ExternalNode lab)
    {
        NetMapManager net = NetMapManager.Get();
        return lab != null && lab.MapNodeId >= 0 && net?.Map != null ? NetMapManager.NodeLabel(net.NodeOf(lab)) : "NR_LabHubFallback".Translate();
    }

    /// <summary>The map highlights the marked hub; we mark the lab's and remember that the mark is ours.</summary>
    private void MarkLabHub(ExternalNode lab)
    {
        NetMapManager net = NetMapManager.Get();
        if (net == null || lab == null || lab.MapNodeId < 0)
            return;
        net.MarkedNodeId = lab.MapNodeId;
        _labHub = lab.MapNodeId;
    }

    /// <summary>Every 2500 ticks: the lab is gone (expired, finished), so our mark goes too; a scenario's mark is left alone.</summary>
    private void CheckLabMark()
    {
        if (ActiveLab == null)
            ClearLabMark();
    }

    private void ClearLabMark()
    {
        NetMapManager net = NetMapManager.Get();
        if (net != null && net.MarkedNodeId == _labHub)
            net.MarkedNodeId = -1;
        _labHub = -1;
    }

    /// <summary>From the end of a real dive into a lab: Heat, and on a clean exit maybe a blueprint (certain after PityAfter misses).</summary>
    public void OnLabDive(ExternalNode lab, bool success)
    {
        HeatTracker.Get()?.AddHeat(LabExtraHeat, "NR_LabHeat".Translate(lab.Name), HeatSource.Dive);
        if (!success)
            return;
        bool print = _cleanWithoutPrint >= PityAfter || Rand.Chance(BlueprintChance);
        if (!print)
        {
            _cleanWithoutPrint++;
            Messages.Message("NR_LabNoPrint".Translate(lab.Name), MessageTypeDefOf.NeutralEvent, historical: true);
            return;
        }
        _cleanWithoutPrint = 0;
        AddBlueprint(lab.Name);
    }

    /// <summary>
    /// A prototype found: counted, and (when the game has the techprint item of the research) the techprint is delivered next
    /// to the runner's pod, or by drop pod to the colony.
    /// </summary>
    public void AddBlueprint(string source)
    {
        if (BlueprintsFound >= Blueprints)
            return;
        BlueprintsFound++;
        SetStage(3);
        Thing print = TechprintMode ? DeliverTechprint() : null;
        // Safety net: an item that could not be delivered is applied outright, so the set is never short.
        if (print == null && TechprintMode)
            Find.ResearchManager.AddTechprints(TransitionDefOf.NR_Res_Transition, 1);
        string where = print != null ? "\n\n" + "NR_TechprintDelivered".Translate() : "";
        if (BlueprintsFound >= Blueprints)
        {
            SetStage(4);
            Find.LetterStack.ReceiveLetter("NR_BlueprintsAllLabel".Translate(), (print != null ? "NR_BlueprintsAllPrints" : "NR_BlueprintsAll").Translate() + where,
                LetterDefOf.PositiveEvent, print);
        }
        else
        {
            Find.LetterStack.ReceiveLetter("NR_BlueprintLabel".Translate(BlueprintsFound, Blueprints),
                ("NR_BlueprintNote_" + BlueprintsFound).Translate(source) + "\n\n" + "NR_BlueprintCount".Translate(BlueprintsFound, Blueprints) + where,
                LetterDefOf.PositiveEvent, print);
        }
        ApplyResearchText();
    }

    /// <summary>Dev actions: applied techprints are topped up to the count of prototypes set.</summary>
    private void SyncTechprints()
    {
        ResearchProjectDef def = TransitionDefOf.NR_Res_Transition;
        if (!TechprintMode || def == null)
            return;
        int missing = BlueprintsFound - Find.ResearchManager.GetTechprints(def);
        if (missing > 0)
            Find.ResearchManager.AddTechprints(def, missing);
    }

    /// <summary>The vanilla techprint of the research, beside the runner (their pod), else by drop pod to the colony.</summary>
    private static Thing DeliverTechprint()
    {
        try
        {
            ThingDef def = TransitionDefOf.NR_Res_Transition?.Techprint;
            if (def == null)
                return null;
            Thing print = ThingMaker.MakeThing(def);
            Pawn runner = GatewayManager.Get()?.Session?.Runner;
            Map map = runner?.MapHeld;
            if (map != null && runner.PositionHeld.IsValid && GenPlace.TryPlaceThing(print, runner.PositionHeld, map, ThingPlaceMode.Near))
                return print;
            map = Find.AnyPlayerHomeMap;
            if (map == null)
                return null;
            DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, [print], forbid: false);
            return print;
        }
        catch (System.Exception e)
        {
            Log.Warning("[Netrun] Transition techprint delivery failed: " + e.Message);
            return null;
        }
    }

    // ---- The research: hidden until the insider, vague until all prototypes are found ----

    private static string _realLabel, _realDesc, _realPrintLabel, _realPrintDesc;
    private static bool _modeKnown, _techprintMode;

    /// <summary>
    /// True when the game has the vanilla techprint item of the research (techprints need the game's techprint support; the
    /// check is on the item itself, not on a DLC flag). Without it the research is gated by our own counter instead.
    /// </summary>
    public static bool TechprintMode
    {
        get
        {
            if (!_modeKnown)
                InitMode();
            return _techprintMode;
        }
    }

    private static void InitMode()
    {
        _modeKnown = true;
        ResearchProjectDef def = TransitionDefOf.NR_Res_Transition;
        _techprintMode = def != null && def.techprintCount > 0 && def.Techprint != null;
        // No techprint item to find: do not leave a requirement nobody can ever meet.
        if (def != null && !_techprintMode)
            def.techprintCount = 0;
    }

    /// <summary>
    /// The research's name and text follow the story: "???" before the insider, a vague prototype until all five are found,
    /// then its real name. The techprint item is renamed the same way (its generated label carries the research name).
    /// </summary>
    public void ApplyResearchText()
    {
        ResearchProjectDef def = TransitionDefOf.NR_Res_Transition;
        if (def == null)
            return;
        _realLabel ??= def.label;
        _realDesc ??= def.description;
        string prints = "NR_TransitionResearchPrints".Translate(BlueprintsFound, Blueprints);
        if (Stage >= 4)
        {
            def.label = _realLabel;
            def.description = _realDesc + "\n\n" + prints;
        }
        else if (Stage >= 2)
        {
            def.label = "NR_TransitionResearchPrototype".Translate();
            def.description = "NR_TransitionResearchVague".Translate() + "\n\n" + prints;
        }
        else
        {
            def.label = "???";
            def.description = "NR_TransitionResearchHidden".Translate();
        }
        Traverse.Create(def).Field("cachedLabelCap").SetValue(default(TaggedString));

        ThingDef print = TechprintMode ? def.Techprint : null;
        if (print != null)
        {
            _realPrintLabel ??= print.label;
            _realPrintDesc ??= print.description;
            print.label = Stage >= 4 ? _realPrintLabel : "NR_PrototypeLabel".Translate();
            print.description = Stage >= 4 ? _realPrintDesc : "NR_PrototypeDesc".Translate();
            Traverse.Create(print).Field("cachedLabelCap").SetValue(default(TaggedString));
        }
    }

    /// <summary>Without a techprint item: the research opens when all prototypes are counted.</summary>
    public bool ResearchOpen => BlueprintsFound >= Blueprints;

    // ---- The upload ----

    public AcceptanceReport CanBegin(Building_Cyberpod pod)
    {
        if (TransitionDefOf.NR_Res_Transition?.IsFinished != true)
            return false;
        if (pod.ContainedThing is not Pawn p || IsShell(p))
            return "NR_UploadNoPawn".Translate();
        if (!p.IsHacker())
            return "NR_UploadNoLink".Translate(p.LabelShort);
        if (Uploading(p))
            return "NR_UploadRunning".Translate();
        if (NetMapManager.InRun(p))
            return "NR_UploadInRun".Translate(p.LabelShort);
        if (pod.GetComp<CompPowerTrader>()?.PowerOn == false)
            return "NR_UploadNoPower".Translate();
        return true;
    }

    public void Begin(Building_Cyberpod pod)
    {
        if (!CanBegin(pod).Accepted || pod.ContainedThing is not Pawn p)
            return;
        _uploads.Add(new TransitionUpload { Pawn = p, Pod = pod, StartTick = Find.TickManager.TicksGame });
        NetrunSound.Play("Dive_Start");
        Messages.Message("NR_UploadStarted".Translate(p.LabelShort, UploadHours.ToString("0")), pod, MessageTypeDefOf.NeutralEvent, historical: true);
        // The corporations notice a mind leaving through their wires: maybe a purge team.
        if (Rand.Chance(PurgeChance))
            Mercenaries.QueueRaid(Rand.RangeInclusive(6, 18) * GenDate.TicksPerHour);
    }

    private void TickUploads(int now)
    {
        for (int i = _uploads.Count - 1; i >= 0; i--)
        {
            TransitionUpload u = _uploads[i];
            if (u.Pawn == null || u.Pawn.Dead || u.Pod == null || u.Pod.Destroyed || u.Pod.ContainedThing != u.Pawn)
            {
                _uploads.RemoveAt(i);
                if (u.Pawn is { Dead: false })
                    Interrupt(u.Pawn);
                continue;
            }
            if ((now - u.StartTick) % GenDate.TicksPerHour < TickInterval)
                HeatTracker.Get()?.AddHeat(UploadHeatPerHour, "NR_UploadHeat".Translate(u.Pawn.LabelShort), HeatSource.Dive);
            if (now - u.StartTick >= UploadHours * GenDate.TicksPerHour)
            {
                _uploads.RemoveAt(i);
                Complete(u);
            }
        }
    }

    public float Progress(Pawn p)
    {
        TransitionUpload u = _uploads.FirstOrDefault(x => x.Pawn == p);
        return u == null ? 0f : UnityEngine.Mathf.Clamp01((Find.TickManager.TicksGame - u.StartTick) / (UploadHours * GenDate.TicksPerHour));
    }

    /// <summary>The pod opened (or lost) mid-upload: a heavy Netsick, maybe death.</summary>
    public void OnEject(Pawn p)
    {
        int at = _uploads.FindIndex(u => u.Pawn == p);
        if (at < 0)
            return;
        _uploads.RemoveAt(at);
        Interrupt(p);
    }

    private static void Interrupt(Pawn p)
    {
        NetsickManager.AddNetsickFor(p, InterruptNetsick);
        bool dies = Rand.Chance(InterruptDeathChance);
        Find.LetterStack.ReceiveLetter("NR_UploadBrokenLabel".Translate(p.LabelShort),
            (dies ? "NR_UploadBrokenDeath" : "NR_UploadBroken").Translate(p.LabelShort), LetterDefOf.NegativeEvent, p);
        if (dies)
            p.Kill(null);
    }

    private void Complete(TransitionUpload u)
    {
        Pawn p = u.Pawn;
        p.health.AddHediff(TransitionDefOf.NR_DigitalShell, p.health.hediffSet.GetBrain());
        string words = ("NR_TransitionWords_" + Rand.RangeInclusive(0, 11)).Translate();
        Departed.Add(new TransitionDeparted { Name = p.Name?.ToStringFull ?? p.LabelShort, Words = words, Tick = Find.TickManager.TicksGame });
        foreach (Pawn other in PawnsFinder.AllMaps_FreeColonists)
            if (other != p && !IsShell(other) && other.needs?.mood?.thoughts?.memories != null)
                other.needs.mood.thoughts.memories.TryGainMemory(TransitionDefOf.NR_WentToNet, p);
        SetStage(5);
        NetrunSound.Play("Dive_Success");
        Find.LetterStack.ReceiveLetter("NR_UploadDoneLabel".Translate(p.LabelShort), "NR_UploadDone".Translate(p.LabelShort, words),
            LetterDefOf.PositiveEvent, u.Pod);
        CheckEnding();
    }

    // ---- The ending ----

    /// <summary>No colonist left but shells (and someone crossed over): the epilogue.</summary>
    public void CheckEnding()
    {
        if (_endingShown || Departed.Count == 0 || _uploads.Count > 0)
            return;
        if (PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Any(p => !IsShell(p)))
            return;
        ShowEnding();
    }

    public void ShowEnding()
    {
        _endingShown = true;
        string list = string.Join("\n", Departed.Select(d => "NR_EpilogueLine".Translate(d.Name, d.Words).ToString()));
        string text = "NR_Epilogue".Translate(list);
        Find.LetterStack.ReceiveLetter("NR_EpilogueLabel".Translate(), text, LetterDefOf.PositiveEvent);
        Find.WindowStack.Add(new Dialog_MessageBox(text, "NR_EpilogueEnd".Translate(),
            () => GameVictoryUtility.ShowCredits(text, SongDefOf.EndCreditsSong),
            "NR_EpilogueContinue".Translate(), ContinueWithNewPeople, "NR_EpilogueLabel".Translate()));
    }

    /// <summary>Playing on: someone new wanders in to the empty colony.</summary>
    private static void ContinueWithNewPeople()
    {
        Map map = Find.AnyPlayerHomeMap;
        if (map == null)
            return;
        IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentDefOf.WandererJoin.category, map);
        parms.forced = true;
        IncidentDefOf.WandererJoin.Worker.TryExecute(parms);
    }

    // ---- Dev ----

    public void DevSetStage(int stage)
    {
        Stage = stage;
        if (stage >= 2 && ContactName.NullOrEmpty())
            ContactName = "Vesper";
        ApplyResearchText();
    }

    public void DevSetBlueprints(int count)
    {
        BlueprintsFound = UnityEngine.Mathf.Clamp(count, 0, Blueprints);
        if (BlueprintsFound > 0)
            SetStage(3);
        if (BlueprintsFound >= Blueprints)
            SetStage(4);
        SyncTechprints();
        ApplyResearchText();
    }

    public void DevResetLabCooldown() => _nextLabTick = -1;

    public void DevFinishUploads()
    {
        foreach (TransitionUpload u in _uploads)
            u.StartTick = Find.TickManager.TicksGame - (int)(UploadHours * GenDate.TicksPerHour);
    }

    public void DevReset()
    {
        Stage = 0;
        BlueprintsFound = 0;
        ContactName = null;
        _cleanWithoutPrint = 0;
        _nextLabTick = -1;
        ClearLabMark();
        _uploads.Clear();
        _endingShown = false;
        ApplyResearchText();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref Stage, "stage");
        Scribe_Values.Look(ref BlueprintsFound, "blueprints");
        Scribe_Values.Look(ref ContactName, "contact");
        Scribe_Values.Look(ref _cleanWithoutPrint, "cleanWithoutPrint");
        Scribe_Values.Look(ref _nextLabTick, "nextLabTick", -1);
        Scribe_Values.Look(ref _labHub, "labHub", -1);
        Scribe_Values.Look(ref _endingShown, "endingShown");
        Scribe_Collections.Look(ref _usedCodeNames, "usedCodeNames", LookMode.Value);
        Scribe_Collections.Look(ref _uploads, "uploads", LookMode.Deep);
        Scribe_Collections.Look(ref Departed, "departed", LookMode.Deep);
        _usedCodeNames ??= [];
        _uploads ??= [];
        Departed ??= [];
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            _uploads.RemoveAll(u => u.Pawn == null || u.Pod == null);
    }
}

/// <summary>Fallback without a techprint item: the Transition cannot start before all five prototypes are found.</summary>
[HarmonyPatch(typeof(ResearchProjectDef), nameof(ResearchProjectDef.CanStartNow), MethodType.Getter)]
public static class Patch_Research_TransitionLock
{
    static void Postfix(ResearchProjectDef __instance, ref bool __result)
    {
        try
        {
            // With the techprint item the vanilla requirement does the gating; this is the fallback without it.
            if (__result && __instance == TransitionDefOf.NR_Res_Transition && !TransitionManager.TechprintMode && TransitionManager.Get()?.ResearchOpen != true)
                __result = false;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Patch_Research_TransitionLock: " + e.Message, 0x4E52540A);
        }
    }
}

/// <summary>An empty shell sleeps in its pod like in a cryptosleep casket: the contents are suspended (no ticks).</summary>
[HarmonyPatch(typeof(Building_Cyberpod), nameof(Building_Cyberpod.IsContentsSuspended), MethodType.Getter)]
public static class Patch_Cyberpod_ShellSuspended
{
    static void Postfix(Building_Cyberpod __instance, ref bool __result)
    {
        try
        {
            if (!__result && __instance.ContainedThing is Pawn p && TransitionManager.IsShell(p))
                __result = true;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Patch_Cyberpod_ShellSuspended: " + e.Message, 0x4E52540B);
        }
    }
}
