using Verse;

namespace Netrun;

/// <summary>One line of the terminal journal. The text is stored already translated.</summary>
/// <summary>Where a Heat change came from: colors the dots on the Heat graph.</summary>
public enum HeatSource
{
    Other,
    Hack,
    Dive,
}

public class HeatLogEntry : IExposable
{
    public int Tick;
    public float Amount;
    public string Text;
    public HeatSource Source;

    public HeatLogEntry() { }

    public HeatLogEntry(int tick, float amount, string text)
    {
        Tick = tick;
        Amount = amount;
        Text = text;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref Tick, "tick");
        Scribe_Values.Look(ref Amount, "amount");
        Scribe_Values.Look(ref Text, "text");
        Scribe_Values.Look(ref Source, "source");
    }
}
