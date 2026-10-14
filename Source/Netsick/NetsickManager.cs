using System.Linq;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>Drives Netsick for everyone: growth in cyberpods and from hacks, decay offline, emotional dives.</summary>
public class NetsickManager : GameComponent
{
    private const int TickInterval = 250;

    public NetsickManager(Game game) { }

    public static NetsickManager Get() => GameComp<NetsickManager>.Get();

    private static NetsickSettingsDef Settings => NetrunDefOf.NR_NetsickSettings;
    private static float DayFraction => (float)TickInterval / GenDate.TicksPerDay;

    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % TickInterval != 0)
            return;

        int now = Find.TickManager.TicksGame;

        foreach (Map map in Find.Maps)
        {
            var pods = map.GetComponent<MapComponent_CyberpodManager>()?.Pods;
            if (pods == null)
                continue;

            foreach (Building_Cyberpod pod in pods.ToList())
            {
                if (pod.ContainedThing is not Pawn diver || diver.Faction != Faction.OfPlayer)
                    continue;

                // In a map session Netsick counts the hours in the net (docs/CYBERSPACE_V2.md, section 2).
                float perDay = NetMapManager.Get()?.InSession(diver) == true ? NetrunDefOf.NR_CyberMap.sessionGainPerDay : Settings.podGainPerDay;
                Hediff_Netsick netsick = AddNetsick(diver, perDay * DayFraction * PodFactor(pod));
                ContractManager.Get()?.NotifyPodTicks(TickInterval, diver);
                netsick.LastConnectedTick = now;
                WarnNeeds(diver, pod, now);

                // Resolved and waking: nothing more to do, unless the pod never lets the pawn out (then eject).
                if (netsick.WakeRequestedTick >= 0)
                {
                    if (now - netsick.WakeRequestedTick > 3 * GenDate.TicksPerHour)
                        pod.EjectContents();
                    continue;
                }
                // A craving that brought the pawn here starts its dive now (the break may have ended on the way).
                if (netsick.CravingSinceTick >= 0 && netsick.EmotionalDiveUntilTick < 0)
                    BeginEmotionalDive(diver, netsick);
                if (netsick.EmotionalDiveUntilTick >= 0 && now >= netsick.EmotionalDiveUntilTick)
                {
                    // Once: the outcome, then the pod wakes the netrunner up (Hacking Expansion's own waking, about an hour).
                    ResolveEmotionalDive(diver, pod, netsick);
                    netsick.EmotionalDiveUntilTick = -1;
                    netsick.CravingSinceTick = -1;
                    netsick.WakeRequestedTick = now;
                    StartWaking(pod);
                }
            }
        }

        foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonists)
        {
            if (pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick) is not Hediff_Netsick netsick)
                continue;
            if (pawn.TryGetHoldingCyberpod(out _))
                continue;

            // Left the pod mid-dive: the emotional dive simply ends. A craving that never reached a pod is dropped after a day.
            netsick.EmotionalDiveUntilTick = -1;
            netsick.WakeRequestedTick = -1;
            if (netsick.CravingSinceTick >= 0 && Find.TickManager.TicksGame - netsick.CravingSinceTick > GenDate.TicksPerDay)
                netsick.CravingSinceTick = -1;
            netsick.Severity -= Settings.decayPerDay * DayFraction * NetrunTraits.DecayFactor(pawn)
                                * (LiveLeisure(pawn) ? Settings.liveLeisureDecayFactor : 1f);
        }
    }

    // When each pawn was last told about hunger (even key) or tiredness (odd key) in a pod. Not saved: after a load a
    // warning may simply repeat once.
    private static readonly System.Collections.Generic.Dictionary<int, int> NeedWarned = [];

    /// <summary>
    /// A plain message (not a letter) when a netrunner in a pod gets hungry or tired: without a Vitality coprocessor the
    /// body keeps its needs, and the player should know when to wake them.
    /// </summary>
    private static void WarnNeeds(Pawn pawn, Building_Cyberpod pod, int now)
    {
        if (pod.IsContentsSuspended || pawn.needs == null)
            return;
        if (pawn.needs.food?.CurCategory >= HungerCategory.Hungry)
            Warn(pawn, pod, now, 0, "NR_PodNeedFood");
        if (pawn.needs.rest?.CurCategory >= RestCategory.Tired)
            Warn(pawn, pod, now, 1, "NR_PodNeedRest");
        if (NeedWarned.Count > 64)
            NeedWarned.Clear();
    }

    private static void Warn(Pawn pawn, Building_Cyberpod pod, int now, int kind, string key)
    {
        int id = pawn.thingIDNumber * 2 + kind;
        if (NeedWarned.TryGetValue(id, out int last) && now - last < GenDate.TicksPerDay / 2)
            return;
        NeedWarned[id] = now;
        Messages.Message(key.Translate(pawn.LabelShort), new LookTargets(pod), MessageTypeDefOf.CautionInput, historical: false);
    }

    /// <summary>Busy with live leisure: a talk, a walk, a game, the stars. Screens and drugs do not count.</summary>
    public static bool LiveLeisure(Pawn pawn)
    {
        JoyKindDef kind = pawn.CurJob?.def?.joyKind;
        return kind != null && Settings.liveJoyKinds.Contains(kind);
    }

    private static readonly HarmonyLib.AccessTools.FieldRef<Building_Cyberpod, bool> IsWakingUp =
        HarmonyLib.AccessTools.FieldRefAccess<Building_Cyberpod, bool>("_isWakingUp");

    /// <summary>The emotional dive starts: it lasts emotionalDiveHours, then ends on its own.</summary>
    public static void BeginEmotionalDive(Pawn pawn, Hediff_Netsick netsick)
    {
        netsick.CravingSinceTick = -1;
        if (netsick.EmotionalDiveUntilTick >= 0)
            return;
        netsick.EmotionalDiveUntilTick = Find.TickManager.TicksGame + (int)(Settings.emotionalDiveHours * GenDate.TicksPerHour);
        Messages.Message("NR_DiveStarted".Translate(pawn.LabelShort, Settings.emotionalDiveHours.ToString("0.#")), pawn, MessageTypeDefOf.NeutralEvent, historical: true);
    }

    /// <summary>
    /// Wakes the pawn's cyberpod, if the pawn is in one. A pod with a Vitality coprocessor holds its pawn in stasis,
    /// so a coma or any other condition would not run its course until the pawn is out.
    /// </summary>
    public static void WakeFromPod(Pawn pawn)
    {
        if (pawn != null && pawn.TryGetHoldingCyberpod(out Building_Cyberpod pod))
            StartWaking(pod);
    }

    /// <summary>Starts Hacking Expansion's wake-up of a cyberpod (the pawn leaves it when it finishes).</summary>
    private static void StartWaking(Building_Cyberpod pod)
    {
        try
        {
            IsWakingUp(pod) = true;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Could not wake the cyberpod after a net craving; ejecting instead: " + e.Message, 0x4E52_5741);
            pod.EjectContents();
        }
    }

    /// <summary>Dev: the craving state of everyone with Netsick.</summary>
    public static string DebugState()
    {
        int now = Find.TickManager.TicksGame;
        var lines = new System.Collections.Generic.List<string>();
        foreach (Pawn p in PawnsFinder.AllMaps_FreeColonists)
        {
            if (p.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick) is not Hediff_Netsick n)
                continue;
            bool inPod = p.TryGetHoldingCyberpod(out _);
            lines.Add($"{p.LabelShort}: {n.Severity:P0}, in pod {inPod}, craving since {(n.CravingSinceTick < 0 ? "-" : ((now - n.CravingSinceTick) / 2500f).ToString("0.#") + " h")}, dive ends in {(n.EmotionalDiveUntilTick < 0 ? "-" : ((n.EmotionalDiveUntilTick - now) / 2500f).ToString("0.#") + " h")}, mental state {p.MentalStateDef?.defName ?? "-"}");
        }
        return lines.Count == 0 ? "No Netsick." : string.Join("\n", lines);
    }

    /// <summary>Called once per hack completed by a colonist.</summary>
    public void NotifyHack(Pawn hacker)
    {
        if (hacker.TryGetHoldingCyberpod(out Building_Cyberpod pod))
        {
            AddNetsick(hacker, Settings.podHackGain * PodFactor(pod)).LastConnectedTick = Find.TickManager.TicksGame;
        }
        else if (hacker.IsHacker())
        {
            AddNetsick(hacker, Settings.remoteHackGain).LastConnectedTick = Find.TickManager.TicksGame;
        }
    }

    /// <summary>The severity where Netsick starts to hurt: the first stage with capacity losses or breaks.</summary>
    public static float StrongSeverity
    {
        get
        {
            if (_strong < 0f)
            {
                HediffStage stage = NetrunDefOf.NR_Netsick?.stages?.FirstOrDefault(s => !s.capMods.NullOrEmpty() || !s.mentalStateGivers.NullOrEmpty());
                _strong = stage?.minSeverity ?? 0.6f;
            }
            return _strong;
        }
    }
    private static float _strong = -1f;

    /// <summary>
    /// Hours a pawn in a pod can stay before Netsick turns strong at the current rate (0 when it already is).
    /// </summary>
    public static float SafeHoursInPod(Pawn pawn, Building_Cyberpod pod)
    {
        float severity = pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick)?.Severity ?? 0f;
        float perDay = (NetMapManager.Get()?.InSession(pawn) == true ? NetrunDefOf.NR_CyberMap.sessionGainPerDay : Settings.podGainPerDay)
                       * PodFactor(pod) * NetrunTraits.GainFactor(pawn);
        if (severity >= StrongSeverity)
            return 0f;
        return perDay <= 0f ? float.PositiveInfinity : (StrongSeverity - severity) / perDay * 24f;
    }

    /// <summary>"Safe for ~N h" or "time to wake up" for a pawn in a pod.</summary>
    public static string SafeTimeText(Pawn pawn, Building_Cyberpod pod)
    {
        float hours = SafeHoursInPod(pawn, pod);
        if (hours <= 0f)
            return "NR_PodSafeOver".Translate();
        return float.IsInfinity(hours) ? "NR_PodSafeAlways".Translate() : "NR_PodSafeTime".Translate(hours < 10f ? hours.ToString("0.#") : hours.ToString("0"));
    }

    private static float PodFactor(Building_Cyberpod pod)
        => CyberpodUtility.NetsickFactor(CyberpodUtility.CoprocessorAI(pod));

    /// <summary>Netsick from outside the pod loop (an emergency disconnect, an ICE patrol on the map).</summary>
    public static void AddNetsickFor(Pawn pawn, float amount)
    {
        if (pawn?.health != null && !pawn.Dead)
            AddNetsick(pawn, amount);
    }

    private static Hediff_Netsick AddNetsick(Pawn pawn, float amount)
    {
        if (pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick) is not Hediff_Netsick netsick)
        {
            netsick = (Hediff_Netsick)HediffMaker.MakeHediff(NetrunDefOf.NR_Netsick, pawn);
            netsick.Severity = 0.01f;
            netsick.LastConnectedTick = Find.TickManager.TicksGame;
            pawn.health.AddHediff(netsick);
            if (pawn.IsColonist)
                FirstHints.Show("Netsick", "NR_Help_Heat_Netsick");
        }

        netsick.Severity += amount > 0f ? amount * NetrunTraits.GainFactor(pawn) : amount;
        return netsick;
    }

    /// <summary>
    /// A dive taken during a "net craving" break: usually it goes wrong, sometimes it is legendary.
    /// </summary>
    private static void ResolveEmotionalDive(Pawn diver, Building_Cyberpod pod, Hediff_Netsick netsick)
    {
        netsick.EmotionalDiveUntilTick = -1;
        netsick.LastCravingTick = Find.TickManager.TicksGame;
        float roll = Rand.Value;

        if (roll < Settings.emotionalJackpotChance)
        {
            NetrunSignals.Increment("EmotionalJackpot");
            Thing data = ThingMaker.MakeThing(NetrunDefOf.USH_AdvancedCyberdata);
            data.stackCount = Settings.jackpotAdvancedData.RandomInRange;
            GenPlace.TryPlaceThing(data, pod.InteractionCell, pod.Map, ThingPlaceMode.Near);

            bool fragment = Rand.Chance(AIBalance.FragmentChance(Settings.jackpotFragmentChance));
            if (fragment)
                GenPlace.TryPlaceThing(ThingMaker.MakeThing(NetrunDefOf.NR_AIFragment), pod.InteractionCell, pod.Map, ThingPlaceMode.Near);

            string text = "NR_DiveJackpot".Translate(diver.LabelShort, data.stackCount);
            if (fragment)
                text += "\n\n" + "NR_DiveJackpotFragment".Translate();
            Find.LetterStack.ReceiveLetter("NR_DiveJackpotLabel".Translate(diver.LabelShort), text,
                LetterDefOf.PositiveEvent, new LookTargets(pod));
            return;
        }

        if (roll < Settings.emotionalJackpotChance + Settings.emotionalFailChance)
        {
            HediffDef bad = new[] { USH_DefOf.USH_CyberspaceNausea, USH_DefOf.USH_CyberspaceLockOut, USH_DefOf.USH_CyberspaceComa }.RandomElement();
            diver.health.AddHediff(bad);
            netsick.Severity += Settings.failNetsickGain;
            Find.LetterStack.ReceiveLetter("NR_DiveFailLabel".Translate(diver.LabelShort),
                "NR_DiveFail".Translate(diver.LabelShort, bad.label), LetterDefOf.NegativeEvent, new LookTargets(pod));
            return;
        }

        Find.LetterStack.ReceiveLetter("NR_DiveNeutralLabel".Translate(diver.LabelShort), "NR_DiveNeutral".Translate(diver.LabelShort), LetterDefOf.NeutralEvent, new LookTargets(pod));
    }
}
