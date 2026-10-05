using Verse;

namespace Netrun;

/// <summary>One line of the terminal journal. The text is stored already translated.</summary>
public class HeatLogEntry : IExposable
{
    public int Tick;
    public float Amount;
    public string Text;

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
    }
}
