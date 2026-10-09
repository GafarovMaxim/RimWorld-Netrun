using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

public enum NewsChoiceKind
{
    Phishing,
    MapLeak,
    FixerTip,
    CrewChallenge,
    SellOutCrew,
    Intrusion,
    TricksterJoke,
}

public class NewsChoice : IExposable
{
    public int Id;
    public NewsChoiceKind Kind;
    public int ExpiresTick;
    public int A = -1;
    public int B = -1;
    public string Text;

    public bool Important => Kind is NewsChoiceKind.CrewChallenge or NewsChoiceKind.SellOutCrew or NewsChoiceKind.Intrusion or NewsChoiceKind.TricksterJoke;

    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id");
        Scribe_Values.Look(ref Kind, "kind");
        Scribe_Values.Look(ref ExpiresTick, "expires");
        Scribe_Values.Look(ref A, "a", -1);
        Scribe_Values.Look(ref B, "b", -1);
        Scribe_Values.Look(ref Text, "text");
    }
}

/// <summary>
/// Choices in the news (docs/CONCEPTS_10.md, section 13.3): small ones live in the news line (a day to answer, silence is
/// a refusal); important ones come as a letter with options and are also kept in the news line.
/// </summary>
public class NewsChoices : GameComponent, INetrunCleanable
{
    public List<NewsChoice> Pending = [];
    private int _nextId = 1;
    private int _nextPhishingTick = -1;
    private int _nextSmallTick = -1;

    public NewsChoices(Game game)
    {
    }

    public static NewsChoices Get() => GameComp<NewsChoices>.Get();

    private static int Now => Find.TickManager.TicksGame;

    public NewsChoice Offer(NewsChoiceKind kind, string text, int a = -1, int b = -1, float days = 1f)
    {
        var choice = new NewsChoice { Id = _nextId++, Kind = kind, Text = text, A = a, B = b, ExpiresTick = Now + (int)(days * GenDate.TicksPerDay) };
        Pending.Add(choice);
        if (choice.Important && NetrunDefOf.NR_ChoiceLetter != null)
        {
            var letter = (NewsChoiceLetter)LetterMaker.MakeLetter(NetrunDefOf.NR_ChoiceLetter);
            letter.Label = ("NR_ChoiceTitle_" + kind).Translate();
            letter.Text = text;
            letter.ChoiceId = choice.Id;
            letter.StartTimeout((int)(days * GenDate.TicksPerDay));
            Find.LetterStack.ReceiveLetter(letter);
        }
        else
        {
            GatewayManager.Get()?.AddHeadline("NR_ChoiceInLine".Translate(("NR_ChoiceTitle_" + kind).Translate()), 0);
        }
        return choice;
    }

    /// <summary>The answers for a choice: label, what it does, tooltip.</summary>
    public List<(string label, System.Action act, string tip)> Options(NewsChoice c)
    {
        Map map = Find.AnyPlayerHomeMap;
        var list = new List<(string, System.Action, string)>();
        switch (c.Kind)
        {
            case NewsChoiceKind.Phishing:
                list.Add(("NR_ChoicePhishingOpen".Translate(), () => OpenPhishing(map), "NR_ChoicePhishingOpenTip".Translate()));
                list.Add(("NR_ChoiceDelete".Translate(), () => { }, null));
                break;
            case NewsChoiceKind.MapLeak:
                list.Add(("NR_ChoiceLeakBuy".Translate(5), () => BuyLeak(map), "NR_ChoiceLeakBuyTip".Translate()));
                list.Add(("NR_ChoiceIgnore".Translate(), () => { }, null));
                break;
            case NewsChoiceKind.FixerTip:
                list.Add(("NR_ChoiceTipGive".Translate(), TipFixer, "NR_ChoiceTipGiveTip".Translate()));
                list.Add(("NR_ChoiceIgnore".Translate(), () => { }, null));
                break;
            case NewsChoiceKind.CrewChallenge:
                list.Add(("NR_ChoiceAccept".Translate(), () => CrewDeals.Race(Faction(c.A)), "NR_ClubCrewRaceTip".Translate()));
                list.Add(("NR_ChoiceDecline".Translate(), () => NetRanking.Get()?.AddCrewFame(Faction(c.A), 20f), "NR_ChoiceDeclineChallengeTip".Translate()));
                break;
            case NewsChoiceKind.SellOutCrew:
                list.Add(("NR_ChoiceSellOut".Translate(300), () => SellOut(map, c), "NR_ChoiceSellOutTip".Translate()));
                list.Add(("NR_ChoiceDecline".Translate(), () => { }, null));
                break;
            case NewsChoiceKind.TricksterJoke:
                list.Add(("NR_ChoiceJokeGift".Translate(), () => AIQuests.TricksterChose(c.A, 0), null));
                list.Add(("NR_ChoiceJokePrank".Translate(), () => AIQuests.TricksterChose(c.A, 1), null));
                list.Add(("NR_ChoiceJokeBoth".Translate(), () => AIQuests.TricksterChose(c.A, 2), null));
                break;
            case NewsChoiceKind.Intrusion:
                list.Add(("NR_ChoiceIsolate".Translate(), () => { if (!NetworkIsolation.Active) NodeDefense.Get()?.ToggleIsolation(); }, "NR_IsolationGizmoDesc".Translate()));
                list.Add(("NR_ChoiceMeetIt".Translate(), () => { }, "NR_ChoiceMeetItTip".Translate()));
                break;
        }
        return list;
    }

    public void Resolve(NewsChoice c, System.Action act)
    {
        if (!Pending.Remove(c))
            return;
        try
        {
            act?.Invoke();
        }
        catch (System.Exception e)
        {
            Log.Warning("[Netrun] news choice failed: " + e.Message);
        }
        foreach (Letter l in Find.LetterStack.LettersListForReading.ToList())
            if (l is NewsChoiceLetter nl && nl.ChoiceId == c.Id)
                Find.LetterStack.RemoveLetter(l);
    }

    private static Faction Faction(int loadId) => Find.FactionManager.AllFactions.FirstOrDefault(f => f.loadID == loadId);

    // ---- Outcomes ----

    /// <summary>Opening a suspicious file: mostly junk, sometimes a little data, sometimes a virus that breaks a machine.</summary>
    private static void OpenPhishing(Map map)
    {
        float r = Rand.Value;
        if (r < 0.7f)
            Messages.Message("NR_PhishingJunk".Translate(), MessageTypeDefOf.NeutralEvent, historical: false);
        else if (r < 0.9f && map != null)
        {
            Thing data = ThingMaker.MakeThing(NetrunDefOf.USH_Cyberdata);
            data.stackCount = 2;
            DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, [data], forbid: false);
            Messages.Message("NR_PhishingData".Translate(), MessageTypeDefOf.PositiveEvent, historical: true);
        }
        else if (map != null)
        {
            IncidentWorker_NetBreakdown.BreakSome(map, 1, NetrunDefOf.NR_NetAttack_Breakdown);
            Messages.Message("NR_PhishingVirus".Translate(), MessageTypeDefOf.NegativeEvent, historical: true);
        }
    }

    /// <summary>A leaked district map: hidden branches around a random node show, or a dark link is found.</summary>
    private static void BuyLeak(Map map)
    {
        if (!ColonyGoods.TryTake(map, NetrunDefOf.USH_Cyberdata, 5))
        {
            Messages.Message("NR_AINoFood".Translate(5, NetrunDefOf.USH_Cyberdata.label), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }
        NetMapManager net = NetMapManager.Get();
        if (net is not { Ready: true })
            return;
        if (Rand.Chance(0.35f) && net.DarkLinkFound())
            return;
        MapNode n = net.Map.Nodes.Where(x => x.District is not (District.Home or District.OldNet or District.Orbit)).RandomElement();
        int shown = net.RevealAround(n.Id, 2);
        Messages.Message("NR_LeakShown".Translate(shown, NetMapManager.NodeLabel(n)), MessageTypeDefOf.PositiveEvent, historical: false);
    }

    private static void TipFixer()
    {
        Fixer fixer = FixerManager.Get()?.Fixers.RandomElementWithFallback();
        if (fixer == null)
            return;
        fixer.GainReputation(5f);
        HeatTracker.Get()?.AddHeat(2f, "NR_TipHeat".Translate(fixer.Name), HeatSource.Other);
        Messages.Message("NR_TipDone".Translate(fixer.Name), MessageTypeDefOf.PositiveEvent, historical: false);
    }

    /// <summary>Selling a crew out to a corporation: silver, the corporation eases off, the crew turns against the colony.</summary>
    private static void SellOut(Map map, NewsChoice c)
    {
        Faction crew = Faction(c.A);
        CorpState corp = CorpManager.Get()?.Corps.FirstOrDefault(x => x.FactionId == c.B);
        if (map != null)
        {
            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = 300;
            DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, [silver], forbid: false);
        }
        if (corp != null)
            corp.Alert = Mathf.Max(0f, corp.Alert - 10f);
        NetFactions.AffectCrew(crew, -30, "NR_CrewGoodwillSoldOut");
    }

    // ---- Small choices over time ----

    public override void GameComponentTick()
    {
        if (Now % GenDate.TicksPerHour != 177)
            return;
        Pending.RemoveAll(c => Now >= c.ExpiresTick);
        if (NetMapManager.Get() is not { Ready: true } || !NetworkGate.IsOpen)
            return;
        if (_nextPhishingTick < 0)
            _nextPhishingTick = Now + Rand.RangeInclusive(10, 20) * GenDate.TicksPerDay;
        else if (Now >= _nextPhishingTick)
        {
            _nextPhishingTick = Now + Rand.RangeInclusive(10, 20) * GenDate.TicksPerDay;
            string fixer = FixerManager.Get()?.Fixers.RandomElementWithFallback()?.Name ?? "?";
            Offer(NewsChoiceKind.Phishing, "NR_ChoicePhishing".Translate(fixer, PhishingFile()));
        }
        if (_nextSmallTick < 0)
            _nextSmallTick = Now + Rand.RangeInclusive(6, 12) * GenDate.TicksPerDay;
        else if (Now >= _nextSmallTick)
        {
            _nextSmallTick = Now + Rand.RangeInclusive(6, 12) * GenDate.TicksPerDay;
            if (Rand.Bool)
                Offer(NewsChoiceKind.MapLeak, "NR_ChoiceLeak".Translate(5));
            else
                Offer(NewsChoiceKind.FixerTip, "NR_ChoiceTip".Translate());
        }
    }

    private static string PhishingFile() => new[] { "contract_final_2.ice", "payment.pdf.exe", "braindance_free.bd", "fixer_list_LEAKED.dat", "your_heat_report.ice" }.RandomElement();

    public int Cleanup(CleanupReason reason) => Pending.RemoveAll(c => Now >= c.ExpiresTick);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref Pending, "pending", LookMode.Deep);
        Scribe_Values.Look(ref _nextId, "nextId", 1);
        Scribe_Values.Look(ref _nextPhishingTick, "nextPhishing", -1);
        Scribe_Values.Look(ref _nextSmallTick, "nextSmall", -1);
        Pending ??= [];
    }
}

/// <summary>An important news choice as a letter with options (also kept in the news line).</summary>
public class NewsChoiceLetter : ChoiceLetter
{
    public int ChoiceId = -1;

    public override IEnumerable<DiaOption> Choices
    {
        get
        {
            NewsChoices manager = NewsChoices.Get();
            NewsChoice choice = manager?.Pending.FirstOrDefault(c => c.Id == ChoiceId);
            if (choice == null)
            {
                yield return Option_Close;
                yield break;
            }
            foreach ((string label, System.Action act, string _) in manager.Options(choice))
            {
                System.Action run = act;
                yield return new DiaOption(label)
                {
                    action = () => manager.Resolve(choice, run),
                    resolveTree = true,
                };
            }
            yield return Option_Postpone;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref ChoiceId, "choiceId", -1);
    }
}

/// <summary>The pending choices from the news line.</summary>
public class Dialog_NewsChoices : Window
{
    public Dialog_NewsChoices()
    {
        doCloseX = true;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
    }

    public override Vector2 InitialSize => new(560f, 420f);

    public override void DoWindowContents(Rect inRect)
    {
        NewsChoices manager = NewsChoices.Get();
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "NR_ChoicesTitle".Translate().Colorize(NetrunUI.Accent));
        Text.Font = GameFont.Small;
        float y = inRect.y + 40f;
        if (manager == null || manager.Pending.Count == 0)
        {
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 30f), "NR_ChoicesNone".Translate().Colorize(NetrunUI.Muted));
            return;
        }
        foreach (NewsChoice c in manager.Pending.ToList())
        {
            string title = ("NR_ChoiceTitle_" + c.Kind).Translate() + "  " + ((string)"NR_ChoiceLeft".Translate((c.ExpiresTick - Find.TickManager.TicksGame).ToStringTicksToPeriod())).Colorize(NetrunUI.Muted);
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f), title.Colorize(c.Important ? NetrunUI.Warn : NetrunUI.TextColor));
            y += 22f;
            Text.Font = GameFont.Tiny;
            float th = Text.CalcHeight(c.Text, inRect.width);
            Widgets.Label(new Rect(inRect.x, y, inRect.width, th), c.Text.Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
            y += th + 4f;
            float x = inRect.x;
            foreach ((string label, System.Action act, string tip) in manager.Options(c))
            {
                float w = NetrunUI.ButtonWidth(label);
                if (NetrunUI.Button(new Rect(x, y, w, 28f), label, NetrunUI.Accent, true, tip))
                    manager.Resolve(c, act);
                x += w + 6f;
            }
            y += 38f;
        }
    }
}
