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

    /// <summary>Each personality's own color in the terminal.</summary>
    public static UnityEngine.Color Color(this AIPersonality personality) => personality switch
    {
        AIPersonality.DataHunter => new(0.30f, 0.75f, 1.00f),
        AIPersonality.Ghost => new(0.75f, 0.82f, 0.92f),
        AIPersonality.Muse => new(1.00f, 0.55f, 0.80f),
        AIPersonality.Predator => new(1.00f, 0.30f, 0.30f),
        AIPersonality.Warden => new(0.45f, 0.60f, 1.00f),
        AIPersonality.Oracle => new(0.75f, 0.50f, 1.00f),
        AIPersonality.Forge => new(1.00f, 0.60f, 0.20f),
        AIPersonality.Siren => new(1.00f, 0.35f, 0.75f),
        AIPersonality.Trickster => new(0.85f, 1.00f, 0.30f),
        AIPersonality.Doc => new(0.40f, 1.00f, 0.60f),
        AIPersonality.Archivist => new(0.90f, 0.78f, 0.55f),
        AIPersonality.Seed => new(0.60f, 0.95f, 0.40f),
        AIPersonality.Echo => new(0.55f, 1.00f, 0.95f),
        _ => UnityEngine.Color.white,
    };

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
