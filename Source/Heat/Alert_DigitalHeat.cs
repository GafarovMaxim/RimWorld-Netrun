using RimWorld;
using Verse;

namespace Netrun;

public class Alert_DigitalHeat : Alert
{
    public Alert_DigitalHeat()
    {
        defaultPriority = AlertPriority.Medium;
    }

    public override string GetLabel()
    {
        HeatTracker tracker = HeatTracker.Get();
        if (tracker == null)
            return string.Empty;

        return "NR_AlertHeatLabel".Translate(tracker.Level.Label(), tracker.Heat.ToString("0"));
    }

    public override TaggedString GetExplanation()
    {
        HeatTracker tracker = HeatTracker.Get();
        if (tracker == null)
            return string.Empty;

        return "NR_AlertHeatDesc".Translate(
            tracker.Heat.ToString("0"),
            tracker.Level.Label(),
            (HeatTracker.Get()?.DecayPerDay ?? NetrunMod.Settings.HeatDecayPerDay).ToString("0.0"));
    }

    public override AlertReport GetReport()
    {
        HeatTracker tracker = HeatTracker.Get();
        return tracker != null && tracker.Level >= HeatLevel.Noticed;
    }
}
