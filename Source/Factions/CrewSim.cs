using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

public enum CrewFocus
{
    Corps,
    Markets,
    Contracts,
    Hunters,
}

public class CrewState : IExposable
{
    public int FactionId;
    public int Skill = 5;
    public CrewFocus Focus;
    public District Home = District.Market;
    public int SinceTick;
    public string LastAction;

    public Faction Faction => Find.FactionManager.AllFactions.FirstOrDefault(f => f.loadID == FactionId);

    public void ExposeData()
    {
        Scribe_Values.Look(ref FactionId, "faction");
        Scribe_Values.Look(ref Skill, "skill", 5);
        Scribe_Values.Look(ref Focus, "focus");
        Scribe_Values.Look(ref Home, "home", District.Market);
        Scribe_Values.Look(ref SinceTick, "since");
        Scribe_Values.Look(ref LastAction, "lastAction");
    }
}

/// <summary>
/// Rival crews (docs/CONCEPTS_10.md, section 12): no objects on the map; once a day each crew picks one action by its
/// focus and skill, the result lands in the news, the map and the ranking.
/// </summary>
public class CrewSim : GameComponent, INetrunCleanable
{
    public List<CrewState> Crews = [];

    public CrewSim(Game game)
    {
    }

    public static CrewSim Get() => GameComp<CrewSim>.Get();

    private static int Now => Find.TickManager.TicksGame;
    private static GatewayManager Gateway => GatewayManager.Get();
    private static NetMapManager Net => NetMapManager.Get();

    public CrewState StateOf(Faction f) => f == null ? null : Crews.FirstOrDefault(c => c.FactionId == f.loadID);

    public void Sync()
    {
        Crews.RemoveAll(c => c.Faction == null || c.Faction.defeated);
        foreach (Faction f in NetFactions.Crews)
        {
            if (StateOf(f) != null)
                continue;
            Crews.Add(new CrewState
            {
                FactionId = f.loadID,
                Skill = Rand.RangeInclusive(3, 8),
                Focus = (CrewFocus)Rand.Range(0, 4),
                Home = new[] { District.Market, District.Ruins, District.Dump, District.Entertainment }.RandomElement(),
                SinceTick = Now,
            });
        }
    }

    public override void GameComponentTick()
    {
        if (Now % GenDate.TicksPerDay != 4421)
            return;
        try
        {
            NetFactions.Get()?.Ensure();
            Sync();
            if (Net is not { Ready: true })
                return;
            foreach (CrewState c in Crews.ToList())
                Act(c);
        }
        catch (System.Exception e)
        {
            Log.Warning("[Netrun] rival crews' day failed: " + e.Message);
        }
    }

    private void Act(CrewState c)
    {
        Faction f = c.Faction;
        if (f == null)
            return;
        float w(CrewFocus focus) => c.Focus == focus ? 3f : 1f;
        var actions = new List<(System.Func<CrewState, Faction, bool> run, float weight)>
        {
            (BurnBranch, 1.5f * w(CrewFocus.Markets)),
            (HitCorp, 1f * w(CrewFocus.Corps)),
            (StealContract, 0.6f * w(CrewFocus.Contracts)),
            (Hunt, 0.6f * w(CrewFocus.Hunters)),
            (SellData, 0.5f),
            (Quiet, 1.5f),
        };
        // Low skill fails more often.
        actions.Add((Fail, Mathf.Lerp(1.2f, 0.2f, c.Skill / 10f)));
        actions.RandomElementByWeight(a => a.weight).run(c, f);
    }

    private static bool Quiet(CrewState c, Faction f)
    {
        // A friendly crew may throw down a challenge.
        if (!f.HostileTo(Faction.OfPlayer) && Rand.Chance(0.15f) && CrewDeals.CanRace(f).Accepted)
            NewsChoices.Get()?.Offer(NewsChoiceKind.CrewChallenge, "NR_ChoiceChallengeText".Translate(f.Name), f.loadID, -1, 1f);
        return true;
    }

    private bool BurnBranch(CrewState c, Faction f)
    {
        ExternalNode b = Gateway?.AllBranches.Where(x => x.MapNodeId >= 0 && !x.IsBoss && !x.Ancient && !x.Contract && Gateway.Session?.Node?.Id != x.Id
            && Net.NodeOf(x)?.District == c.Home).RandomElementWithFallback()
            ?? Gateway?.AllBranches.Where(x => x.MapNodeId >= 0 && !x.IsBoss && !x.Ancient && !x.Contract && Gateway.Session?.Node?.Id != x.Id).RandomElementWithFallback();
        if (b == null)
            return false;
        b.OfflineUntil = Now + Rand.RangeInclusive(2, 4) * GenDate.TicksPerDay;
        NetRanking.Get()?.AddCrewFame(f, 20f + c.Skill * 5f);
        Report(c, "NR_CrewNewsBurn", f.Name.Named("CREW"), b.Name.Named("BRANCH"));
        return true;
    }

    private bool HitCorp(CrewState c, Faction f)
    {
        CorpState corp = CorpManager.Get()?.Corps.Where(x => x.Faction is { defeated: false }).RandomElementWithFallback();
        if (corp == null)
            return false;
        CorpManager.Get().NotifyRivalHit(corp);
        // Now and then the corporation offers silver for the crew's names.
        if (corp.Alert >= 40f && Rand.Chance(0.3f))
            NewsChoices.Get()?.Offer(NewsChoiceKind.SellOutCrew, "NR_ChoiceSellOutText".Translate(corp.Faction.Name, f.Name), f.loadID, corp.FactionId, 2f);
        // After the hit the fortress's outer gates are weaker for two days.
        MapNode fortress = Net.Map.Nodes.FirstOrDefault(n => n.Fortress && n.OwnerFaction == corp.FactionId);
        if (fortress != null)
            foreach (ExternalNode b in Net.BranchesAt(fortress.Id))
                b.OpenDoorUntil = Now + 2 * GenDate.TicksPerDay;
        NetRanking.Get()?.AddCrewFame(f, 40f + c.Skill * 8f);
        Report(c, "NR_CrewNewsHitCorp", f.Name.Named("CREW"), corp.Faction.Name.Named("CORP"));
        return true;
    }

    /// <summary>A crew goes for the colony's contract target: the colony has a day or two to dive first.</summary>
    private bool StealContract(CrewState c, Faction f)
    {
        ExternalNode target = Gateway?.AllBranches.FirstOrDefault(x => x.Contract && !x.Rival && Gateway.Session?.Node?.Id != x.Id);
        if (target == null)
            return false;
        target.Rival = true;
        target.ExpireTick = Now + (int)(Rand.Range(1f, 2f) * GenDate.TicksPerDay);
        Find.LetterStack.ReceiveLetter("NR_CrewStealLabel".Translate(f.Name), "NR_CrewSteal".Translate(f.Name, target.Name), LetterDefOf.ThreatSmall);
        Report(c, "NR_CrewNewsSteal", f.Name.Named("CREW"), target.Name.Named("BRANCH"));
        return true;
    }

    private bool Hunt(CrewState c, Faction f)
    {
        MapNode n = Net.Map.Nodes.Where(x => x.District is District.Transit or District.Market or District.Outskirts).RandomElementWithFallback();
        if (n == null)
            return false;
        n.RivalTrapUntil = Mathf.Max(n.RivalTrapUntil, Now + Rand.RangeInclusive(1, 2) * GenDate.TicksPerDay);
        c.LastAction = "NR_CrewActionHunt".Translate(NetMapManager.NodeLabel(n));
        return true;
    }

    private bool SellData(CrewState c, Faction f)
    {
        if (Gateway == null || Gateway.GlutActive)
            return false;
        Gateway.StartGlut();
        Report(c, "NR_CrewNewsSell", f.Name.Named("CREW"));
        return true;
    }

    private bool Fail(CrewState c, Faction f)
    {
        NetRanking ranking = NetRanking.Get();
        ranking?.AddCrewFame(f, -(60f + Rand.Range(0f, 80f)));
        Report(c, "NR_CrewNewsFail", f.Name.Named("CREW"));
        // A big failure breaks the crew; a new one takes its place.
        float fame = ranking?.CrewEntry(f)?.Fame ?? 1000f;
        if (fame < 300f && Rand.Chance(0.25f) && Now - c.SinceTick > 30 * GenDate.TicksPerDay)
        {
            f.defeated = true;
            Crews.Remove(c);
            Gateway?.AddHeadline("NR_CrewNewsDisband".Translate(f.Name), 0);
            NetFactions.Get()?.Ensure();
            Sync();
        }
        return true;
    }

    private static void Report(CrewState c, string key, params NamedArgument[] args)
    {
        string text = key.Translate(args);
        c.LastAction = text;
        Gateway?.AddHeadline(text, 0);
    }

    /// <summary>A dive raced a rival crew: whoever got the files takes fame, and relations shift.</summary>
    public void NotifyRace(DiveSession dive, bool colonyWon)
    {
        Faction crew = NetFactions.Crews.RandomElementWithFallback();
        if (crew == null || dive?.Node == null || !dive.Node.Rival)
            return;
        NetRanking ranking = NetRanking.Get();
        if (colonyWon)
        {
            ranking?.AddColonyFame(dive.Runner, NetRanking.Settings.fameRace, "race");
            ranking?.AddCrewFame(crew, -NetRanking.Settings.fameRace);
            NetFactions.AffectCrew(crew, -5, "NR_CrewGoodwillRace");
        }
        else
        {
            ranking?.AddCrewFame(crew, NetRanking.Settings.fameRace);
        }
    }

    /// <summary>A lent runner comes back: silver and goodwill, now and then hurt by the job.</summary>
    public void FinishHire(NetRunner runner)
    {
        Faction crew = Find.FactionManager.AllFactions.FirstOrDefault(f => f.loadID == runner.BusyCrew);
        runner.BusyCrew = -1;
        if (crew == null || runner.Pawn == null)
            return;
        Map map = runner.Pawn.MapHeld;
        int silver = Rand.RangeInclusive(150, 300);
        if (map != null)
        {
            Thing s = ThingMaker.MakeThing(ThingDefOf.Silver);
            s.stackCount = silver;
            GenPlace.TryPlaceThing(s, runner.Pawn.PositionHeld, map, ThingPlaceMode.Near);
        }
        NetFactions.AffectCrew(crew, 5);
        bool hurt = Rand.Chance(0.15f);
        if (hurt)
            NetsickManager.AddNetsickFor(runner.Pawn, 0.1f);
        Messages.Message((hurt ? "NR_CrewHireBackHurt" : "NR_CrewHireBack").Translate(runner.Pawn.LabelShort, crew.Name, silver), runner.Pawn,
            hurt ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.PositiveEvent);
    }

    public int Cleanup(CleanupReason reason) => Crews.RemoveAll(c => c.Faction == null);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref Crews, "crews", LookMode.Deep);
        Crews ??= [];
    }
}

/// <summary>What the colony can do with a crew from the Club (docs/CONCEPTS_10.md, section 12).</summary>
public static class CrewDeals
{
    public const int HireHours = 12;
    public const int OrderSilver = 300;
    public const int RaceGoodwill = 0;

    /// <summary>A friendly crew borrows a connected runner for half a day: silver and goodwill, a small risk.</summary>
    public static AcceptanceReport CanHire(Faction crew, NetRunner runner)
    {
        if (crew == null || crew.PlayerGoodwill < 25)
            return "NR_CrewNeedFriends".Translate();
        if (runner == null)
            return "NR_MapNoRunner".Translate();
        if (runner.Busy || runner.Moving)
            return "NR_CrewRunnerBusy".Translate();
        return true;
    }

    public static void Hire(Faction crew, NetRunner runner)
    {
        if (!CanHire(crew, runner).Accepted)
            return;
        runner.BusyUntil = Find.TickManager.TicksGame + HireHours * GenDate.TicksPerHour;
        runner.BusyCrew = crew.loadID;
        Messages.Message("NR_CrewHired".Translate(runner.Pawn.LabelShort, crew.Name, HireHours), MessageTypeDefOf.NeutralEvent, historical: false);
    }

    /// <summary>A race: the crew posts a node; get there and take it first.</summary>
    public static AcceptanceReport CanRace(Faction crew)
    {
        if (crew == null || crew.HostileTo(Faction.OfPlayer))
            return "NR_CrewHostile".Translate();
        if (GatewayManager.Get()?.AllBranches.Any(b => b.Rival && b.ExpireTick >= 0) == true)
            return "NR_CrewRaceRunning".Translate();
        return NetMapManager.Get() is { Ready: true } ? true : (AcceptanceReport)"NR_MapNotReady".Translate();
    }

    public static void Race(Faction crew)
    {
        GatewayManager gateway = GatewayManager.Get();
        if (gateway == null || !CanRace(crew).Accepted)
            return;
        ExternalNode node = ExternalNode.ForDistrict(gateway.NextBranchId(), District.Market);
        node.Rival = true;
        node.Hidden = false;
        node.ExpireTick = Find.TickManager.TicksGame + 2 * GenDate.TicksPerDay;
        gateway.AddBranch(node, District.Market, District.Ruins, District.Entertainment);
        Find.LetterStack.ReceiveLetter("NR_CrewRaceLabel".Translate(crew.Name), "NR_CrewRace".Translate(crew.Name, node.Name), LetterDefOf.NeutralEvent);
    }

    /// <summary>Order a hit on a crew through a fixer: they lose fame; sometimes they learn who paid.</summary>
    public static AcceptanceReport CanOrder(Map map, Faction crew)
        => ColonyGoods.Count(map, ThingDefOf.Silver) >= OrderSilver ? true : (AcceptanceReport)"NR_NotEnoughSilver".Translate(OrderSilver);

    public static void Order(Map map, Faction crew)
    {
        if (!CanOrder(map, crew).Accepted || !ColonyGoods.TryTake(map, ThingDefOf.Silver, OrderSilver))
            return;
        NetRanking.Get()?.AddCrewFame(crew, -200f);
        GatewayManager.Get()?.AddHeadline("NR_CrewNewsOrdered".Translate(crew.Name), 1);
        if (Rand.Chance(0.3f))
            NetFactions.AffectCrew(crew, -10, "NR_CrewGoodwillOrder");
    }
}
