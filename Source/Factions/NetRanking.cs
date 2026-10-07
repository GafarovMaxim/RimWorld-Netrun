using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>Numbers for the top 100 (Defs/Corporations.xml).</summary>
public class RankingSettingsDef : Def
{
    public float famePerDefense = 10f;
    public float hardBranchBonus = 0.5f;
    public float fameFortress = 100f;
    public float fameCore = 200f;
    public float fameRace = 50f;
    public float fameIntrusion = 30f;
    public float fameCollapse = 200f;
    public float decayPerDay = 0.01f;
    public int dailyChanges = 5;
    public float retireChance = 0.03f;
    public float topFameMin = 150f;
    public float topFameMax = 5000f;
}

public enum RankKind
{
    Solo,
    Crew,
    Colony,
}

public class RankEntry : IExposable
{
    public string Callsign;
    public float Fame;
    public float FameWeekAgo;
    public RankKind Kind;
    /// <summary>Faction loadID for a crew, pawn thingIDNumber for a colony runner.</summary>
    public int RefId = -1;

    public float Trend => Fame - FameWeekAgo;

    public void ExposeData()
    {
        Scribe_Values.Look(ref Callsign, "callsign");
        Scribe_Values.Look(ref Fame, "fame");
        Scribe_Values.Look(ref FameWeekAgo, "fameWeekAgo");
        Scribe_Values.Look(ref Kind, "kind");
        Scribe_Values.Look(ref RefId, "ref", -1);
    }
}

/// <summary>
/// The net's top 100 (docs/CONCEPTS_10.md, section 12): three crews, the colony's runners and about 95 solo runners with
/// callsigns. Fame grows with deeds and slowly cools; the list re-sorts only when fame changes; five solos move a day.
/// </summary>
public class NetRanking : GameComponent, INetrunCleanable
{
    public const int Size = 100;
    private static readonly string[] First = ["Null", "Ghost", "Static", "Chrome", "Neon", "Zero", "Byte", "Silk", "Echo", "Glitch", "Rust", "Hex", "Ash",
        "Cipher", "Volt", "Pixel", "Mirror", "Sable", "Night", "Wire", "Spark", "Frost", "Shard", "Lotus", "Raven", "Vector", "Drift", "Phantom", "Socket", "Kilo"];
    private static readonly string[] Second = ["Saint", "Fox", "Jack", "Queen", "Monk", "Viper", "Hound", "Moth", "Wolf", "Kid", "Witch", "Prophet", "Rat",
        "Angel", "Bishop", "Ronin", "Siren", "Crane", "Mantis", "Pilot", "Dancer", "Thief", "Doll", "Saint", "Lynx", "Owl", "Mule", "Ferret", "Spider", "Djinn"];

    public List<RankEntry> Entries = [];
    private bool _sorted;
    private int _weekTick = -1;

    public NetRanking(Game game)
    {
    }

    public static NetRanking Get() => Current.Game?.GetComponent<NetRanking>();

    public static RankingSettingsDef Settings => NetrunDefOf.NR_Ranking;

    private static int Now => Find.TickManager.TicksGame;

    public List<RankEntry> Sorted
    {
        get
        {
            EnsureFilled();
            if (!_sorted)
            {
                Entries.Sort((a, b) => b.Fame.CompareTo(a.Fame));
                _sorted = true;
            }
            return Entries;
        }
    }

    public int PlaceOf(RankEntry e) => Sorted.IndexOf(e) + 1;

    /// <summary>The best place among the colony's runners (999 if none is in the list).</summary>
    public int BestColonyPlace
    {
        get
        {
            List<RankEntry> list = Sorted;
            int i = list.FindIndex(e => e.Kind == RankKind.Colony);
            return i < 0 ? 999 : i + 1;
        }
    }

    public RankEntry ColonyEntry(Pawn pawn) => pawn == null ? null : Entries.FirstOrDefault(e => e.Kind == RankKind.Colony && e.RefId == pawn.thingIDNumber);

    public RankEntry CrewEntry(Faction crew) => crew == null ? null : Entries.FirstOrDefault(e => e.Kind == RankKind.Crew && e.RefId == crew.loadID);

    public static string Title(int place) => place switch
    {
        <= 0 => "",
        1 => "NR_RankTitle_1".Translate(),
        <= 3 => "NR_RankTitle_3".Translate(),
        <= 10 => "NR_RankTitle_10".Translate(),
        <= 50 => "NR_RankTitle_50".Translate(),
        <= 100 => "NR_RankTitle_100".Translate(),
        _ => "",
    };

    /// <summary>Contract reward factor from the colony's best place: top 10 +10%, #1 +20%.</summary>
    public float ContractRewardFactor
    {
        get
        {
            int p = BestColonyPlace;
            return p == 1 ? 1.2f : p <= 10 ? 1.1f : 1f;
        }
    }

    private static string Callsign() => First.RandomElement() + " " + Second.RandomElement();

    private void EnsureFilled()
    {
        foreach (Faction crew in NetFactions.Crews)
        {
            if (CrewEntry(crew) == null)
            {
                Entries.Add(new RankEntry { Callsign = crew.Name, Kind = RankKind.Crew, RefId = crew.loadID, Fame = Rand.Range(800f, 2500f) });
                _sorted = false;
            }
        }
        int solos = Entries.Count(e => e.Kind == RankKind.Solo);
        int want = Size - Entries.Count(e => e.Kind != RankKind.Solo);
        for (int i = solos; i < want; i++)
        {
            // Fame spread: a long tail from about 150 at #100 to about 5000 at #1.
            float t = Mathf.Pow(Rand.Value, 2.2f);
            Entries.Add(new RankEntry { Callsign = Callsign(), Kind = RankKind.Solo, Fame = Mathf.Lerp(Settings.topFameMin, Settings.topFameMax, t) });
            _sorted = false;
        }
        if (Entries.Count > Size)
        {
            // Drop the weakest solos to stay at a hundred.
            foreach (RankEntry e in Entries.Where(e => e.Kind == RankKind.Solo).OrderBy(e => e.Fame).Take(Entries.Count - Size).ToList())
                Entries.Remove(e);
        }
    }

    /// <summary>Fame for the colony: to the runner if given, otherwise to the colony's best runner (or a new entry).</summary>
    public void AddColonyFame(Pawn pawn, float amount, string reason)
    {
        if (amount <= 0f)
            return;
        RankEntry e = ColonyEntry(pawn) ?? (pawn == null ? Entries.Where(x => x.Kind == RankKind.Colony).OrderByDescending(x => x.Fame).FirstOrDefault() : null);
        if (e == null && pawn != null)
        {
            e = new RankEntry { Callsign = pawn.LabelShort, Kind = RankKind.Colony, RefId = pawn.thingIDNumber, Fame = 0f };
            Entries.Add(e);
        }
        if (e == null)
            return;
        int before = PlaceOf(e);
        e.Fame += amount;
        _sorted = false;
        int after = PlaceOf(e);
        if (after <= Size && (before > Size || before == 0 || after < before && (after <= 10 || before - after >= 10)))
            GatewayManager.Get()?.AddHeadline("NR_RankNewsClimb".Translate(e.Callsign, after), 1);
        if (reason != null && Prefs.DevMode)
            Log.Message($"[Netrun] fame +{amount:0} {e.Callsign}: {reason}");
    }

    public void AddCrewFame(Faction crew, float amount)
    {
        RankEntry e = CrewEntry(crew);
        if (e == null)
            return;
        e.Fame = Mathf.Max(0f, e.Fame + amount);
        _sorted = false;
    }

    /// <summary>Fame for a clean dive (docs/CONCEPTS_10.md, section 12).</summary>
    public void NotifyDive(DiveSession dive, bool success)
    {
        if (!success || dive?.Runner == null)
            return;
        ExternalNode n = dive.Node;
        float fame = n.Defense * Settings.famePerDefense;
        if (dive.ForkChoice.Contains(1))
            fame *= 1f + Settings.hardBranchBonus;
        if (n.IsBoss)
            fame += Settings.fameCore;
        else if (NetMapManager.Get()?.NodeOf(n) is { Fortress: true })
            fame += Settings.fameFortress;
        AddColonyFame(dive.Runner, fame, "dive");
    }

    public override void GameComponentTick()
    {
        if (Now % GenDate.TicksPerDay != 3319)
            return;
        EnsureFilled();
        if (_weekTick < 0 || Now - _weekTick >= GenDate.TicksPerDay * 7)
        {
            _weekTick = Now;
            foreach (RankEntry e in Entries)
                e.FameWeekAgo = e.Fame;
        }
        foreach (RankEntry e in Entries.Where(e => e.Kind == RankKind.Colony))
            e.Fame *= 1f - Settings.decayPerDay;
        foreach (RankEntry e in Entries.Where(e => e.Kind == RankKind.Solo).InRandomOrder().Take(Settings.dailyChanges).ToList())
        {
            if (Rand.Chance(Settings.retireChance))
            {
                if (PlaceOf(e) <= 30)
                    GatewayManager.Get()?.AddHeadline("NR_RankNewsRetired".Translate(e.Callsign), 0);
                e.Callsign = Callsign();
                e.Fame = Rand.Range(Settings.topFameMin, Settings.topFameMin * 2f);
                e.FameWeekAgo = e.Fame;
                continue;
            }
            e.Fame = Mathf.Max(50f, e.Fame * Rand.Range(0.92f, 1.12f));
        }
        _sorted = false;
    }

    public int Cleanup(CleanupReason reason)
    {
        int n = Entries.RemoveAll(e => e == null || e.Kind == RankKind.Crew && !NetFactions.Crews.Any(f => f.loadID == e.RefId));
        var colonists = new HashSet<int>(PawnsFinder.AllMapsWorldAndTemporary_Alive.Select(p => p.thingIDNumber));
        // Dead runners keep their names in the list for a while: they leave when their fame cools under the bottom.
        n += Entries.RemoveAll(e => e.Kind == RankKind.Colony && !colonists.Contains(e.RefId) && e.Fame < Settings.topFameMin);
        if (n > 0)
            _sorted = false;
        return n;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref Entries, "entries", LookMode.Deep);
        Scribe_Values.Look(ref _weekTick, "weekTick", -1);
        Entries ??= [];
        _sorted = false;
    }
}
