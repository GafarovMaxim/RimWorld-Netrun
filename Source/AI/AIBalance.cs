using System.Linq;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// AI balance (docs/CONCEPTS_10.md, section 2): how often fragments turn up (the "AI availability" setting scales
/// every source) and the network noise awake AIs make (a floor under the colony's Heat).
/// </summary>
public static class AIBalance
{
    private static int _noiseTick = -1;
    private static float _noiseFloor;

    /// <summary>A fragment chance after the "AI availability" setting.</summary>
    public static float FragmentChance(float baseChance) => Mathf.Clamp01(baseChance * NetrunMod.Settings.AIAvailability);

    /// <summary>How loud one awake AI is: Ghost half as loud, Predator twice.</summary>
    public static float NoiseWeight(NetAI ai) => ai.Personality switch
    {
        AIPersonality.Ghost => 0.5f,
        AIPersonality.Predator => 2f,
        _ => 1f,
    };

    /// <summary>Active AIs that make noise: awake, powered and not isolated. A cut-off network silences all of them.</summary>
    public static System.Collections.Generic.IEnumerable<NetAI> NoisyAIs()
        => NetworkIsolation.Active ? Enumerable.Empty<NetAI>() : AIManager.Get()?.AIs.Where(a => a.Active) ?? Enumerable.Empty<NetAI>();

    /// <summary>
    /// Heat never cools (or gets scrubbed) below this: noisePerAI for every awake AI after the first, weighted by
    /// personality. Cached for 250 ticks.
    /// </summary>
    public static float NoiseFloor
    {
        get
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (_noiseTick >= 0 && now - _noiseTick < 250 && now >= _noiseTick)
                return _noiseFloor;
            _noiseTick = now;
            float[] weights = NoisyAIs().Select(NoiseWeight).OrderByDescending(w => w).ToArray();
            // The loudest AI is free: the floor starts with the second one.
            float sum = weights.Skip(1).Sum();
            _noiseFloor = Mathf.Min(HeatTracker.MaxHeat * 0.5f, sum * NetrunDefOf.NR_AI.noisePerAI);
            return _noiseFloor;
        }
    }

    public static void ResetCache() => _noiseTick = -1;
}
