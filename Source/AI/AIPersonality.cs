using Verse;

namespace Netrun;

public enum AIPersonality
{
    DataHunter,
    Ghost,
    Muse,
    Predator,
    Warden,
    Oracle,
    Forge,
    Siren,
    Trickster,

    /// <summary>Only from a dead colonist with a cyberlink, never from a fragment.</summary>
    Echo,
}

public static class AIPersonalityUtility
{
    public static string Label(this AIPersonality personality) => ("NR_AIPersonality_" + personality).Translate();
    public static string Description(this AIPersonality personality) => ("NR_AIPersonalityDesc_" + personality).Translate();

    /// <summary>Any personality a fragment can wake up as.</summary>
    public static AIPersonality RandomFromFragment()
    {
        int count = (int)AIPersonality.Echo;
        return (AIPersonality)Rand.Range(0, count);
    }
}
