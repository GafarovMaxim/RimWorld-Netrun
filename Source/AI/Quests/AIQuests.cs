using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace Netrun;

public enum AIQuestKind
{
    BloodOfCorp,
    AnotherName,
    AnotherMemory,
    Retaliation,
    Drills,
    Forecast,
    MissingPart,
    HeartToHeart,
    ChoiceOfJoke,
    Patient,
    MissingVolume,
    FirstStep,
    WhoAmI,
    HungryChannel,
    QuietHour,
    ForeignCode,
}

/// <summary>
/// An AI's quest inside a vanilla quest (docs/CONCEPTS_10.md, section 16): which AI, which quest, which step, the
/// counters and the target. Progress comes from events the mod already raises and a cheap hourly check.
/// </summary>
public class QuestPart_NetAIQuest : QuestPart
{
    public int AIId = -1;
    public AIQuestKind Kind;
    public int Step;
    public int Counter;
    public int Target = -1;
    public int Target2 = -1;
    public int StepStartTick;
    public int DeadlineTick;
    public int Choice = -1;

    public NetAI AI => AIManager.Get()?.ById(AIId);

    public int Steps => AIQuests.StepCount(Kind);

    public override string DescriptionPart
        => "NR_AIQ_Progress".Translate(Step + 1, Steps) + "\n" + AIQuests.StepText(this);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref AIId, "aiId", -1);
        Scribe_Values.Look(ref Kind, "kind");
        Scribe_Values.Look(ref Step, "step");
        Scribe_Values.Look(ref Counter, "counter");
        Scribe_Values.Look(ref Target, "target", -1);
        Scribe_Values.Look(ref Target2, "target2", -1);
        Scribe_Values.Look(ref StepStartTick, "stepStart");
        Scribe_Values.Look(ref DeadlineTick, "deadline");
        Scribe_Values.Look(ref Choice, "choice", -1);
    }
}

/// <summary>Builds the quest from the slate: the part, the name and the description.</summary>
public class QuestNode_NetAIQuest : QuestNode
{
    protected override bool TestRunInt(Slate slate) => slate.Exists("nrAIId");

    protected override void RunInt()
    {
        Slate slate = QuestGen.slate;
        var part = new QuestPart_NetAIQuest
        {
            AIId = slate.Get<int>("nrAIId"),
            Kind = (AIQuestKind)slate.Get<int>("nrKind"),
            Target = slate.Get("nrTarget", -1),
            Target2 = slate.Get("nrTarget2", -1),
        };
        QuestGen.quest.AddPart(part);
        string ai = part.AI?.Name ?? "?";
        QuestGen.AddQuestNameRules(new List<Rule> { new Rule_String("nrQuestName", ("NR_AIQ_Name_" + part.Kind).Translate(ai)) });
        QuestGen.AddQuestDescriptionRules(new List<Rule>
        {
            new Rule_String("nrQuestDesc", ("NR_AIQ_Desc_" + part.Kind).Translate(ai, AIQuests.TargetName(part)) + "\n\n" + "NR_AIQ_Terms".Translate(AIQuests.Days(part.Kind))),
        });
    }
}

/// <summary>Offers, tracks and pays out the AIs' quests; also the AI levels.</summary>
public class AIQuests : GameComponent
{
    public const int MaxColonyQuests = 2;

    public AIQuests(Game game)
    {
    }

    public static AIQuests Get() => GameComp<AIQuests>.Get();

    private static int Now => Find.TickManager.TicksGame;
    private static AIManager Manager => AIManager.Get();

    // ---- Definitions ----

    public static int StepCount(AIQuestKind k) => k switch
    {
        AIQuestKind.AnotherMemory => 3,
        AIQuestKind.HungryChannel or AIQuestKind.QuietHour or AIQuestKind.ForeignCode or AIQuestKind.Forecast or AIQuestKind.MissingVolume => 1,
        _ => 2,
    };

    public static int Days(AIQuestKind k) => k switch
    {
        AIQuestKind.BloodOfCorp or AIQuestKind.AnotherMemory => 8,
        AIQuestKind.Retaliation or AIQuestKind.HeartToHeart or AIQuestKind.MissingVolume => 12,
        AIQuestKind.Drills or AIQuestKind.WhoAmI => 15,
        AIQuestKind.Forecast or AIQuestKind.HungryChannel => 3,
        AIQuestKind.ChoiceOfJoke => 5,
        AIQuestKind.QuietHour => 4,
        _ => 10,
    };

    private static AIQuestKind? KindFor(AIPersonality p) => p switch
    {
        AIPersonality.DataHunter => AIQuestKind.BloodOfCorp,
        AIPersonality.Ghost => AIQuestKind.AnotherName,
        AIPersonality.Muse => AIQuestKind.AnotherMemory,
        AIPersonality.Predator => AIQuestKind.Retaliation,
        AIPersonality.Warden => AIQuestKind.Drills,
        AIPersonality.Oracle => AIQuestKind.Forecast,
        AIPersonality.Forge => AIQuestKind.MissingPart,
        AIPersonality.Siren => AIQuestKind.HeartToHeart,
        AIPersonality.Trickster => AIQuestKind.ChoiceOfJoke,
        AIPersonality.Doc => AIQuestKind.Patient,
        AIPersonality.Archivist => AIQuestKind.MissingVolume,
        AIPersonality.Seed => AIQuestKind.FirstStep,
        AIPersonality.Echo => AIQuestKind.WhoAmI,
        _ => null,
    };

    public static string StepText(QuestPart_NetAIQuest q) => ("NR_AIQ_" + q.Kind + "_Step" + (q.Step + 1)).Translate(TargetName(q), q.Counter);

    /// <summary>The quest's target as text: a corporation, a fixer, a pawn, a branch.</summary>
    public static string TargetName(QuestPart_NetAIQuest q)
    {
        switch (q.Kind)
        {
            case AIQuestKind.BloodOfCorp:
            case AIQuestKind.Retaliation:
                return Find.FactionManager.AllFactions.FirstOrDefault(f => f.loadID == q.Target)?.Name ?? "?";
            case AIQuestKind.HeartToHeart:
                return FixerManager.Get()?.ById(q.Target)?.Name ?? "?";
            case AIQuestKind.Patient:
                return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.FirstOrDefault(p => p.thingIDNumber == q.Target)?.LabelShort ?? "?";
            case AIQuestKind.Forecast:
                return GatewayManager.Get()?.AllBranches.FirstOrDefault(b => b.Id == q.Target)?.Name ?? "?";
            case AIQuestKind.AnotherMemory:
                return ("NR_BDMood_" + (BraindanceMood)Mathf.Max(0, q.Target)).Translate();
            default:
                return "";
        }
    }

    // ---- Active quests ----

    public static IEnumerable<QuestPart_NetAIQuest> Active
        => Find.QuestManager?.QuestsListForReading.Where(q => q.State == QuestState.Ongoing)
               .SelectMany(q => q.PartsListForReading.OfType<QuestPart_NetAIQuest>()) ?? Enumerable.Empty<QuestPart_NetAIQuest>();

    private static IEnumerable<QuestPart_NetAIQuest> Offered
        => Find.QuestManager?.QuestsListForReading.Where(q => q.State == QuestState.NotYetAccepted)
               .SelectMany(q => q.PartsListForReading.OfType<QuestPart_NetAIQuest>()) ?? Enumerable.Empty<QuestPart_NetAIQuest>();

    public static QuestPart_NetAIQuest QuestOf(NetAI ai) => Active.Concat(Offered).FirstOrDefault(q => q.AIId == ai.Id);

    // ---- Offering ----

    public override void GameComponentTick()
    {
        if (Now % GenDate.TicksPerHour != 911 || Manager == null)
            return;
        try
        {
            foreach (QuestPart_NetAIQuest q in Active.ToList())
                Hourly(q);
            CheckEnded();
            TryOffer();
        }
        catch (System.Exception e)
        {
            Log.Warning("[Netrun] AI quests: " + e.Message);
        }
    }

    /// <summary>Every 10-14 days an AI with trust 30+ and hunger under 50 may ask; one per AI, two per colony.</summary>
    private void TryOffer()
    {
        if (NetrunDefOf.NR_AIQuest == null || Active.Concat(Offered).Count() >= MaxColonyQuests)
            return;
        foreach (NetAI ai in Manager.AIs.Where(a => a.Active && a.Trust >= 30f && a.Hunger < 50f).InRandomOrder())
        {
            if (ai.NextQuestTick < 0)
            {
                ai.NextQuestTick = Now + Rand.RangeInclusive(10, 14) * GenDate.TicksPerDay;
                continue;
            }
            if (Now < ai.NextQuestTick || QuestOf(ai) != null)
                continue;
            ai.NextQuestTick = Now + Rand.RangeInclusive(10, 14) * GenDate.TicksPerDay;
            if (Offer(ai))
                return;
        }
    }

    public bool Offer(NetAI ai, AIQuestKind? forced = null)
    {
        AIQuestKind kind = forced ?? KindFor(ai.Personality) ?? AIQuestKind.HungryChannel;
        // Its own quest when it can happen now, otherwise a common one.
        if (!Setup(kind, out int target, out int target2))
        {
            kind = new[] { AIQuestKind.HungryChannel, AIQuestKind.QuietHour, AIQuestKind.ForeignCode }.RandomElement();
            if (!Setup(kind, out target, out target2))
                kind = AIQuestKind.HungryChannel;
        }
        var slate = new Slate();
        slate.Set("nrAIId", ai.Id);
        slate.Set("nrKind", (int)kind);
        slate.Set("nrTarget", target);
        slate.Set("nrTarget2", target2);
        Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(NetrunDefOf.NR_AIQuest, slate);
        if (quest == null)
            return false;
        quest.acceptanceExpireTick = Now + 2 * GenDate.TicksPerDay;
        QuestUtility.SendLetterQuestAvailable(quest);
        return true;
    }

    /// <summary>Picks the quest's target; false when the quest cannot happen now.</summary>
    private static bool Setup(AIQuestKind kind, out int target, out int target2)
    {
        target = target2 = -1;
        switch (kind)
        {
            case AIQuestKind.BloodOfCorp:
            {
                CorpState c = CorpManager.Get()?.Corps.Where(x => x.Faction is { defeated: false }).OrderByDescending(x => x.Strength).FirstOrDefault();
                target = c?.FactionId ?? -1;
                return c != null && NetMapManager.Get() is { Ready: true };
            }
            case AIQuestKind.Retaliation:
            {
                CorpState c = CorpManager.Get()?.Corps.Where(x => x.Faction is { defeated: false }).OrderByDescending(x => x.Alert).FirstOrDefault();
                target = c?.FactionId ?? -1;
                return c != null;
            }
            case AIQuestKind.AnotherName:
            case AIQuestKind.ForeignCode:
                return NetMapManager.Get() is { Ready: true };
            case AIQuestKind.AnotherMemory:
                target = Rand.Range(0, 3);
                return DefDatabase<ThingDef>.GetNamedSilentFail("NR_BraindanceRaw") != null;
            case AIQuestKind.HeartToHeart:
            {
                Fixer worst = FixerManager.Get()?.Fixers.Where(f => f.Reputation < 40f && !f.SirenDiscount)
                    .OrderByDescending(f => f.Trait is FixerTrait.Greedy or FixerTrait.Rat or FixerTrait.Paranoid or FixerTrait.Hothead).FirstOrDefault();
                target = worst?.Id ?? -1;
                return worst != null;
            }
            case AIQuestKind.Patient:
            {
                Pawn p = PawnsFinder.AllMaps_FreeColonists.Where(x => x.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick) is { Severity: >= 0.5f })
                    .OrderByDescending(x => x.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick).Severity).FirstOrDefault();
                target = p?.thingIDNumber ?? -1;
                return p != null;
            }
            case AIQuestKind.Forecast:
            {
                ExternalNode b = GatewayManager.Get()?.AllBranches.Where(x => !x.Hidden && !x.IsBoss && !x.Ancient && x.InterceptPacketId < 0).RandomElementWithFallback();
                target = b?.Id ?? -1;
                return b != null;
            }
            case AIQuestKind.MissingPart:
                target = CountRacksAndCoprocessors();
                return true;
            case AIQuestKind.FirstStep:
                return true;
            default:
                return true;
        }
    }

    private static int CountRacksAndCoprocessors()
    {
        int n = 0;
        foreach (Map map in Find.Maps)
        {
            if (NetrunDefOf.NR_AIServerRack != null)
                n += map.listerBuildings.AllBuildingsColonistOfDef(NetrunDefOf.NR_AIServerRack).Count();
            ThingDef cop = DefDatabase<ThingDef>.GetNamedSilentFail("NR_NetrunCoprocessor");
            if (cop != null)
                n += map.listerBuildings.AllBuildingsColonistOfDef(cop).Count();
        }
        return n;
    }

    // ---- Progress ----

    private static void Advance(QuestPart_NetAIQuest q, string message = null)
    {
        q.Step++;
        q.Counter = 0;
        q.StepStartTick = Now;
        if (message != null)
            Messages.Message(message, MessageTypeDefOf.PositiveEvent, historical: false);
        if (q.Step >= q.Steps)
            Complete(q);
        else
            Messages.Message("NR_AIQ_StepDone".Translate(q.AI?.Name ?? "?", StepText(q)), MessageTypeDefOf.NeutralEvent, historical: false);
    }

    private static void Hourly(QuestPart_NetAIQuest q)
    {
        if (q.DeadlineTick <= 0)
        {
            q.DeadlineTick = Now + Days(q.Kind) * GenDate.TicksPerDay;
            q.StepStartTick = Now;
            OnAccepted(q);
        }
        if (q.AI == null)
        {
            q.quest.End(QuestEndOutcome.Fail, sendLetter: false);
            return;
        }
        if (Now > q.DeadlineTick)
        {
            Fail(q);
            return;
        }
        Map map = Find.AnyPlayerHomeMap;
        switch (q.Kind)
        {
            case AIQuestKind.AnotherName when q.Step == 0:
                // Five days in the Shadow, in a row.
                q.Counter = HeatTracker.Get()?.Level == HeatLevel.Shadow ? q.Counter + 1 : 0;
                if (q.Counter >= 5 * 24)
                    Advance(q);
                break;
            case AIQuestKind.Drills when q.Step == 0:
                if ((NodeDefense.Get()?.Defense(map) ?? 0) >= 6)
                    Advance(q);
                break;
            case AIQuestKind.MissingPart when q.Step == 0:
                if (CountRacksAndCoprocessors() > q.Target)
                    Advance(q);
                break;
            case AIQuestKind.MissingPart when q.Step == 1:
                if (ColonyGoods.TryTake(map, ThingDefOf.ComponentSpacer, 5))
                    Advance(q, "NR_AIQ_PartsTaken".Translate(q.AI.Name));
                break;
            case AIQuestKind.HeartToHeart when q.Step == 0:
                if (FixerManager.Get()?.ById(q.Target) is { Reputation: >= 40f })
                    Advance(q);
                break;
            case AIQuestKind.Patient:
            {
                Pawn p = PawnsFinder.AllMaps_FreeColonists.FirstOrDefault(x => x.thingIDNumber == q.Target);
                if (p == null)
                {
                    Fail(q);
                    break;
                }
                if (q.Step == 0 && (p.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick)?.Severity ?? 0f) < 0.25f)
                    Advance(q);
                else if (q.Step == 1)
                {
                    int last = PawnNetState.Get()?.LastDiveTick(p) ?? -1;
                    if (last > q.StepStartTick)
                        q.StepStartTick = Now;
                    else if (Now - q.StepStartTick >= 3 * GenDate.TicksPerDay)
                        Advance(q);
                }
                break;
            }
            case AIQuestKind.FirstStep when q.Step == 0:
            {
                NetAI ai = q.AI;
                int[] now = [ai.SeedTowardDataHunter, ai.SeedTowardMuse, ai.SeedTowardPredator, ai.SeedTowardGhost];
                // Baseline at acceptance: then one path fed three more times (or five feedings in all).
                if (q.Target < 0)
                {
                    q.Target = now.Sum();
                    q.Target2 = now.Max();
                    break;
                }
                if (now.Max() - q.Target2 >= 3 || now.Sum() - q.Target >= 5)
                    Advance(q);
                break;
            }
            case AIQuestKind.WhoAmI when q.Step == 0:
            {
                string name = q.AI.Name;
                bool found = Find.Maps.Any(m => m.listerThings.AllThings.Any(t => t.TryGetComp<CompBraindanceRecord>() is { HasRecord: true } r && name.Contains(r.Author ?? "\u0001")));
                if (found)
                    Advance(q);
                break;
            }
            case AIQuestKind.HungryChannel:
                if (ColonyGoods.TryTake(map, NetrunDefOf.USH_Cyberdata, 10))
                    Advance(q, "NR_AIQ_Fed".Translate(q.AI.Name));
                break;
            case AIQuestKind.QuietHour:
                if (Now - q.StepStartTick >= 2 * GenDate.TicksPerDay && (Manager.LastHackTick < q.StepStartTick))
                    Advance(q);
                else if (Manager.LastHackTick >= q.StepStartTick)
                    q.StepStartTick = Now;
                break;
        }
    }

    /// <summary>Things that happen once when the player accepts.</summary>
    private static void OnAccepted(QuestPart_NetAIQuest q)
    {
        if (q.Kind == AIQuestKind.ChoiceOfJoke)
            NewsChoices.Get()?.Offer(NewsChoiceKind.TricksterJoke, "NR_AIQ_JokeText".Translate(q.AI?.Name ?? "?"), q.AIId, -1, 1f);
        if (q.Kind == AIQuestKind.Forecast)
            Messages.Message("NR_AIQ_ForecastNamed".Translate(q.AI?.Name ?? "?", TargetName(q)), MessageTypeDefOf.NeutralEvent, historical: true);
    }

    /// <summary>The Trickster's choice came back: 0 gift, 1 prank, 2 both.</summary>
    public static void TricksterChose(int aiId, int choice)
    {
        QuestPart_NetAIQuest q = Active.FirstOrDefault(x => x.AIId == aiId && x.Kind == AIQuestKind.ChoiceOfJoke);
        if (q == null)
            return;
        q.Choice = choice;
        if (choice >= 1)
        {
            // The prank: harmless and annoying.
            HeatTracker.Get()?.AddHeat(5f, "NR_AIQ_Prank".Translate(q.AI?.Name ?? "?"), HeatSource.AI);
            Messages.Message("NR_AIQ_PrankDone".Translate(q.AI?.Name ?? "?"), MessageTypeDefOf.NeutralEvent, historical: true);
        }
        Advance(q);
        if (choice == 1)
        {
            // Only a prank: survived it, that is the quest.
            Advance(q);
        }
    }

    // ---- Events from the mod ----

    public static void NotifyDive(DiveSession dive, bool success)
    {
        if (!success || dive?.Node == null)
            return;
        ExternalNode node = dive.Node;
        NetMapManager net = NetMapManager.Get();
        foreach (QuestPart_NetAIQuest q in Active.ToList())
        {
            switch (q.Kind)
            {
                case AIQuestKind.BloodOfCorp:
                {
                    CorpState owner = CorpManager.Get()?.OwnerOf(node);
                    if (owner?.FactionId != q.Target)
                        break;
                    if (q.Step == 0 && ++q.Counter >= 2)
                        Advance(q);
                    else if (q.Step == 1 && net?.NodeOf(node) is { Fortress: true })
                        Advance(q);
                    break;
                }
                case AIQuestKind.AnotherName when q.Step == 1:
                    if (dive.IceHits == 0 && (node.Deserted || node.Rival))
                        Advance(q);
                    break;
                case AIQuestKind.Retaliation when q.Step == 1:
                    if (net?.NodeOf(node) is { Fortress: true } f && f.OwnerFaction == q.Target)
                        Advance(q);
                    break;
                case AIQuestKind.Forecast:
                    if (node.Id == q.Target && Now - q.StepStartTick <= 2 * GenDate.TicksPerDay)
                        Advance(q);
                    break;
                case AIQuestKind.MissingVolume:
                    if (node.Type == ExternalNodeType.Archive && dive.SpecialFound)
                        Advance(q);
                    break;
                case AIQuestKind.FirstStep when q.Step == 1:
                    if (CyberpodUtility.InCyberpod(dive.Runner, out NetAI cop) && cop?.Id == q.AIId)
                        Advance(q);
                    break;
                case AIQuestKind.ChoiceOfJoke when q.Step == 1 && q.Choice != 1:
                    // The gift waits in the next branch.
                    Advance(q, "NR_AIQ_GiftFound".Translate(q.AI?.Name ?? "?"));
                    break;
                case AIQuestKind.ForeignCode:
                    if (node.WildAI)
                        Advance(q);
                    break;
            }
        }
    }

    public static void NotifyRepelled(bool intercepted)
    {
        foreach (QuestPart_NetAIQuest q in Active.ToList())
        {
            if (q.Kind == AIQuestKind.Retaliation && q.Step == 0 && q.AI?.Active == true)
                Advance(q);
            else if (q.Kind == AIQuestKind.Drills && q.Step == 1 && intercepted)
                Advance(q);
        }
    }

    public static void NotifyRecorded(BraindanceMood mood)
    {
        foreach (QuestPart_NetAIQuest q in Active.Where(x => x.Kind == AIQuestKind.AnotherMemory && x.Step == 0).ToList())
            if ((int)mood == q.Target)
                Advance(q);
    }

    public static void NotifyEdited(BraindanceMood mood, int quality)
    {
        foreach (QuestPart_NetAIQuest q in Active.Where(x => x.Kind == AIQuestKind.AnotherMemory && x.Step == 1).ToList())
            if ((int)mood == q.Target && quality >= 1)
                Advance(q);
    }

    public static void NotifyWatched(Pawn viewer, CompBraindanceRecord record)
    {
        foreach (QuestPart_NetAIQuest q in Active.ToList())
        {
            if (q.Kind == AIQuestKind.AnotherMemory && q.Step == 2 && record != null && (int)record.Mood == q.Target && ++q.Counter >= 3)
                Advance(q);
            else if (q.Kind == AIQuestKind.WhoAmI && q.Step == 1 && record?.Author != null && q.AI?.Name.Contains(record.Author) == true)
                Advance(q);
        }
    }

    public static void NotifyContract(int fixerId)
    {
        foreach (QuestPart_NetAIQuest q in Active.Where(x => x.Kind == AIQuestKind.HeartToHeart && x.Step == 1 && x.Target == fixerId).ToList())
            Advance(q);
    }

    // ---- Ending ----

    private static void Complete(QuestPart_NetAIQuest q)
    {
        NetAI ai = q.AI;
        if (ai != null)
        {
            GiveReward(q, ai);
            ai.QuestsDone++;
            int level = ai.QuestsDone >= 3 ? 3 : ai.QuestsDone >= 1 ? 2 : 1;
            if (level > ai.Level)
            {
                ai.Level = level;
                if (level >= 2)
                    ai.TraitsRevealed = Mathf.Max(ai.TraitsRevealed, ai.Traits.Count);
                Find.LetterStack.ReceiveLetter("NR_AIQ_LevelLabel".Translate(ai.Name, level), ("NR_AIQ_Level_" + level).Translate(ai.Name), LetterDefOf.PositiveEvent);
            }
        }
        q.quest.End(QuestEndOutcome.Success, sendLetter: true);
    }

    private static void Fail(QuestPart_NetAIQuest q)
    {
        NetAI ai = q.AI;
        if (ai != null)
        {
            ai.AddTrust(-15f);
            ai.ServiceReadyTick = Mathf.Max(ai.ServiceReadyTick, Now + 3 * GenDate.TicksPerDay);
        }
        q.quest.End(QuestEndOutcome.Fail, sendLetter: true);
    }

    /// <summary>Declined (the offer ran out): a small trust loss.</summary>
    private static void CheckEnded()
    {
        foreach (Quest quest in Find.QuestManager.QuestsListForReading.Where(q => q.State == QuestState.EndedOfferExpired))
        {
            foreach (QuestPart_NetAIQuest q in quest.PartsListForReading.OfType<QuestPart_NetAIQuest>())
            {
                if (q.Counter == -99)
                    continue;
                q.Counter = -99;
                q.AI?.AddTrust(-5f);
            }
        }
    }

    private static void GiveReward(QuestPart_NetAIQuest q, NetAI ai)
    {
        Map map = Find.AnyPlayerHomeMap;
        void Drop(ThingDef def, int count)
        {
            if (map == null || def == null || count <= 0)
                return;
            Thing t = ThingMaker.MakeThing(def);
            t.stackCount = count;
            DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, [t], forbid: false);
        }
        switch (q.Kind)
        {
            case AIQuestKind.BloodOfCorp:
                Drop(NetrunDefOf.USH_AdvancedCyberdata, 4);
                break;
            case AIQuestKind.AnotherName:
                HeatTracker.Get()?.ReduceHeat(20f, "NR_AIQ_RewardLog".Translate(ai.Name), HeatSource.AI);
                AITrait extra = ((AITrait[])System.Enum.GetValues(typeof(AITrait))).Where(t => t != AITrait.Remembering && !ai.Traits.Contains(t)).RandomElement();
                if (ai.Traits.Count < 3)
                {
                    ai.Traits.Add(extra);
                    ai.TraitsRevealed = Mathf.Max(ai.TraitsRevealed, ai.Traits.Count);
                }
                break;
            case AIQuestKind.AnotherMemory:
                if (UniqueBraindanceManager.Get()?.GiveRandomUnique(map) != true)
                    Drop(DefDatabase<ThingDef>.GetNamedSilentFail("NR_BraindanceChip"), 2);
                break;
            case AIQuestKind.Retaliation:
                foreach (Pawn hacker in map?.mapPawns.FreeColonistsSpawned.Where(p => USH_HE.CyberUtils.IsHacker(p)) ?? Enumerable.Empty<Pawn>())
                    if (DefDatabase<HediffDef>.GetNamedSilentFail("NR_PredatorEdge") is { } edge)
                        hacker.health.AddHediff(edge);
                break;
            case AIQuestKind.Drills:
                ai.Drilled = true;
                break;
            case AIQuestKind.Forecast:
                DelayMercs(3);
                break;
            case AIQuestKind.MissingPart:
                if (Find.ResearchManager.GetProject() is { } project)
                    Find.ResearchManager.AddProgress(project, 1500f, null);
                else
                    Drop(NetrunDefOf.USH_AdvancedCyberdata, 3);
                break;
            case AIQuestKind.HeartToHeart:
                if (FixerManager.Get()?.ById(q.Target) is { } fixer)
                    fixer.SirenDiscount = true;
                break;
            case AIQuestKind.ChoiceOfJoke:
                if (Rand.Chance(0.15f) && UniqueBraindanceManager.Get()?.GiveRandomUnique(map) == true)
                    break;
                Drop(NetrunDefOf.USH_Cyberdata, Rand.RangeInclusive(3, 8));
                break;
            case AIQuestKind.Patient:
                ai.DocBoost = true;
                break;
            case AIQuestKind.MissingVolume:
                PickColonistForSkill(map);
                break;
            case AIQuestKind.FirstStep:
                AIPersonality toward = new[] { (ai.SeedTowardDataHunter, AIPersonality.DataHunter), (ai.SeedTowardMuse, AIPersonality.Muse),
                    (ai.SeedTowardPredator, AIPersonality.Predator), (ai.SeedTowardGhost, AIPersonality.Ghost) }.MaxBy(x => x.Item1).Item2;
                Manager.GrowSeed(ai, toward);
                Manager.GrowSeed(ai, toward);
                break;
            case AIQuestKind.WhoAmI:
                if (!ai.Traits.Contains(AITrait.Remembering))
                    ai.Traits.Add(AITrait.Remembering);
                ai.TraitsRevealed = Mathf.Max(ai.TraitsRevealed, ai.Traits.Count);
                Find.LetterStack.ReceiveLetter("NR_AIQ_LegendLabel".Translate(ai.Name), "NR_AIQ_Legend".Translate(ai.Name), LetterDefOf.PositiveEvent);
                break;
            case AIQuestKind.HungryChannel:
                ai.AddHunger(-30f);
                ai.AddTrust(5f);
                break;
            case AIQuestKind.QuietHour:
            case AIQuestKind.ForeignCode:
                ai.AddTrust(10f);
                break;
        }
    }

    /// <summary>The Oracle's forecast pays off: the next mercenary raid comes 3 days later, and the colony knows when.</summary>
    private static void DelayMercs(int days)
    {
        var queued = new List<QueuedIncident>();
        foreach (object o in Find.Storyteller.incidentQueue)
            if (o is QueuedIncident qi)
                queued.Add(qi);
        QueuedIncident next = queued.Where(q => q.FiringIncident?.def == NetrunDefOf.NR_MercRaid).OrderBy(q => q.FireTick).FirstOrDefault();
        if (next == null)
        {
            Messages.Message("NR_AIQ_NoMercs".Translate(), MessageTypeDefOf.NeutralEvent, historical: true);
            return;
        }
        HarmonyLib.Traverse.Create(next).Field("fireTick").SetValue(next.FireTick + days * GenDate.TicksPerDay);
        Messages.Message("NR_AIQ_MercsDelayed".Translate((next.FireTick - Now).ToStringTicksToPeriod()), MessageTypeDefOf.PositiveEvent, historical: true);
    }

    private static void PickColonistForSkill(Map map)
    {
        List<Pawn> pawns = map?.mapPawns.FreeColonistsSpawned.Where(p => p.skills != null).ToList() ?? [];
        if (pawns.Count == 0)
            return;
        var options = pawns.Select(p => new FloatMenuOption(p.LabelShort, () =>
        {
            SkillRecord best = p.skills.skills.Where(s => !s.TotallyDisabled).MaxBy(s => s.Level);
            if (best == null || best.Level >= 20)
                return;
            best.Level++;
            Messages.Message("NR_AIQ_SkillUp".Translate(p.LabelShort, best.def.label), p, MessageTypeDefOf.PositiveEvent);
        })).ToList();
        Find.WindowStack.Add(new FloatMenu(options, "NR_AIQ_PickColonist".Translate()));
    }
}
