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

        // Built again only when the level or the rounded Heat changes (the alert list asks every frame).
        int heat = UnityEngine.Mathf.RoundToInt(tracker.Heat);
        if (_label == null || heat != _labelHeat || tracker.Level != _labelLevel)
        {
            _labelHeat = heat;
            _labelLevel = tracker.Level;
            _label = "NR_AlertHeatLabel".Translate(tracker.Level.Label(), heat.ToString());
        }
        return _label;
    }

    private string _label;
    private int _labelHeat = -1;
    private HeatLevel _labelLevel;

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
