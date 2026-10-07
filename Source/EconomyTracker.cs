using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// Day-by-day totals for balancing (docs/BALANCE.md section 2): cyberdata gained and spent, Heat up and down,
/// dives and AI events. Read by the dev action "Economy summary"; keeps the last 15 days.
/// </summary>
public class EconomyTracker : GameComponent, INetrunCleanable
{
    private const int KeepDays = 15;

    private List<DayStats> _days = [];

    public EconomyTracker(Game game)
    {
    }

    public int Cleanup(CleanupReason reason)
    {
        int day = GenDate.DaysPassed;
        return _days.RemoveAll(d => d == null || d.day < day - KeepDays);
    }

    public static EconomyTracker Get() => Current.Game?.GetComponent<EconomyTracker>();

    private DayStats Today
    {
        get
        {
            int day = GenDate.DaysPassed;
            DayStats stats = _days.Find(d => d.day == day);
            if (stats != null)
                return stats;
            stats = new DayStats { day = day };
            _days.Add(stats);
            _days.RemoveAll(d => d.day < day - KeepDays);
            return stats;
        }
    }

    /// <summary>Records one economic fact; cyberdata counts advanced cyberdata as its regular worth.</summary>
    public void Note(int gained = 0, int spent = 0, float heatUp = 0f, float heatDown = 0f, bool dive = false, bool diveWon = false, bool aiEvent = false)
    {
        DayStats t = Today;
        t.gained += gained;
        t.spent += spent;
        t.heatUp += heatUp;
        t.heatDown += heatDown;
        if (dive)
            t.dives++;
        if (diveWon)
            t.divesWon++;
        if (aiEvent)
            t.aiEvents++;
    }

    public string Summary(int days)
    {
        int from = GenDate.DaysPassed - days + 1;
        List<DayStats> range = _days.Where(d => d.day >= from).ToList();
        return "NR_EconomySummary".Translate(days,
            range.Sum(d => d.gained), range.Sum(d => d.spent),
            range.Sum(d => d.heatUp).ToString("0"), range.Sum(d => d.heatDown).ToString("0"),
            range.Sum(d => d.dives), range.Sum(d => d.divesWon), range.Sum(d => d.aiEvents),
            HeatTracker.Get()?.Heat.ToString("0") ?? "?");
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref _days, "days", LookMode.Deep);
        _days ??= [];
    }

    private class DayStats : IExposable
    {
        public int day;
        public int gained;
        public int spent;
        public float heatUp;
        public float heatDown;
        public int dives;
        public int divesWon;
        public int aiEvents;

        public void ExposeData()
        {
            Scribe_Values.Look(ref day, "day");
            Scribe_Values.Look(ref gained, "gained");
            Scribe_Values.Look(ref spent, "spent");
            Scribe_Values.Look(ref heatUp, "heatUp");
            Scribe_Values.Look(ref heatDown, "heatDown");
            Scribe_Values.Look(ref dives, "dives");
            Scribe_Values.Look(ref divesWon, "divesWon");
            Scribe_Values.Look(ref aiEvents, "aiEvents");
        }
    }
}
