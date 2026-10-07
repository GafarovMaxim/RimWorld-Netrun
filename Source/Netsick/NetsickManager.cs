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

    public static NetsickManager Get() => Current.Game?.GetComponent<NetsickManager>();

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

                Hediff_Netsick netsick = AddNetsick(diver, Settings.podGainPerDay * DayFraction * PodFactor(pod));
                netsick.LastConnectedTick = now;

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
            netsick.Severity -= Settings.decayPerDay * DayFraction;
        }
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

    private static float PodFactor(Building_Cyberpod pod)
        => CyberpodUtility.NetsickFactor(CyberpodUtility.CoprocessorAI(pod));

    private static Hediff_Netsick AddNetsick(Pawn pawn, float amount)
    {
        if (pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick) is not Hediff_Netsick netsick)
        {
            netsick = (Hediff_Netsick)HediffMaker.MakeHediff(NetrunDefOf.NR_Netsick, pawn);
            netsick.Severity = 0.01f;
            netsick.LastConnectedTick = Find.TickManager.TicksGame;
            pawn.health.AddHediff(netsick);
        }

        netsick.Severity += amount;
        return netsick;
    }

    /// <summary>
    /// A dive taken during a "net craving" break: usually it goes wrong, sometimes it is legendary.
    /// </summary>
    private static void ResolveEmotionalDive(Pawn diver, Building_Cyberpod pod, Hediff_Netsick netsick)
    {
        netsick.EmotionalDiveUntilTick = -1;
        float roll = Rand.Value;

        if (roll < Settings.emotionalJackpotChance)
        {
            Thing data = ThingMaker.MakeThing(NetrunDefOf.USH_AdvancedCyberdata);
            data.stackCount = Settings.jackpotAdvancedData.RandomInRange;
            GenPlace.TryPlaceThing(data, pod.InteractionCell, pod.Map, ThingPlaceMode.Near);

            bool fragment = Rand.Chance(Settings.jackpotFragmentChance);
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
