using System.Collections.Generic;
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
    Doc,
    Archivist,

    /// <summary>Starts as nothing; grows into another personality depending on how it is fed.</summary>
    Seed,

    /// <summary>Only from a dead colonist with a cyberlink, never from a fragment.</summary>
    Echo,
}

public static class AIPersonalityUtility
{
    public static string Label(this AIPersonality personality) => ("NR_AIPersonality_" + personality).Translate();
    public static string Description(this AIPersonality personality) => ("NR_AIPersonalityDesc_" + personality).Translate();

    /// <summary>Any personality a fragment can wake up as (everything but Echo).</summary>
    public static AIPersonality RandomFromFragment()
    {
        int count = (int)AIPersonality.Echo;
        return (AIPersonality)Rand.Range(0, count);
    }

    /// <summary>Services a Trickster can borrow: everyone with a fixed, self-contained service.</summary>
    public static readonly List<AIPersonality> Borrowable =
    [
        AIPersonality.DataHunter, AIPersonality.Ghost, AIPersonality.Muse, AIPersonality.Predator,
        AIPersonality.Warden, AIPersonality.Oracle, AIPersonality.Forge, AIPersonality.Siren,
        AIPersonality.Doc, AIPersonality.Archivist,
    ];
}
