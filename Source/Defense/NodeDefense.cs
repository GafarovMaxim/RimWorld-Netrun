using Verse;

namespace Netrun;

/// <summary>
/// The colony's own node (docs/CONCEPTS_10.md, section 1): network isolation now; firewalls, ICE tiers, duty and the
/// attack check come in stage 6.
/// </summary>
public class NodeDefense : GameComponent
{
    /// <summary>Full isolation: no attacks get through, but the Gateway, dives, contracts, AI services and fixer calls are off.</summary>
    public bool Isolated;

    public NodeDefense(Game game)
    {
    }

    public static NodeDefense Get() => Current.Game?.GetComponent<NodeDefense>();

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref Isolated, "isolated");
    }
}

/// <summary>Shortcut for the isolation switch.</summary>
public static class NetworkIsolation
{
    public static bool Active => NodeDefense.Get()?.Isolated == true;
}
