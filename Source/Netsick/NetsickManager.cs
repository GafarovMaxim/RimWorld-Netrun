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

                if (netsick.EmotionalDiveUntilTick >= 0 && now >= netsick.EmotionalDiveUntilTick)
                    ResolveEmotionalDive(diver, pod, netsick);
            }
        }

        foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonists)
        {
            if (pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick) is not Hediff_Netsick netsick)
                continue;
            if (pawn.TryGetHoldingCyberpod(out _))
                continue;

            // Left the pod mid-dive: the emotional dive simply ends.
            netsick.EmotionalDiveUntilTick = -1;
            netsick.Severity -= Settings.decayPerDay * DayFraction;
        }
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
        => CyberpodUtility.HasNetrunCoprocessor(pod) ? NetrunDefOf.NR_Heat.coprocessorNetsickFactor : 1f;

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

        Messages.Message("NR_DiveNeutral".Translate(diver.LabelShort), pod, MessageTypeDefOf.NeutralEvent, historical: true);
    }
}
