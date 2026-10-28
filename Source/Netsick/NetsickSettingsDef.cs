using Verse;

namespace Netrun;

/// <summary>Cyberspace dependency balance numbers (Defs/Netsick.xml).</summary>
public class NetsickSettingsDef : Def
{
    // Growth: per day spent in a cyberpod, per hack from a pod.
    public float podGainPerDay = 0.15f;
    public float podHackGain = 0.02f;

    // Recovery while disconnected.
    public float decayPerDay = 0.04f;

    // "A living day off": while the pawn is busy with live leisure (talk, walks, games, the sky), recovery is faster.
    public float liveLeisureDecayFactor = 2f;
    public System.Collections.Generic.List<RimWorld.JoyKindDef> liveJoyKinds = [];

    // Withdrawal: disconnected for this long while at least this severe.
    public float withdrawalAfterDays = 1f;
    public float withdrawalMinSeverity = 0.3f;

    // "Net craving" break: an emotional dive lasts this long, then the outcome is rolled.
    public float emotionalDiveHours = 2f;
    /// <summary>After an emotional dive, no new net craving for this many days.</summary>
    public float cravingCooldownDays = 3f;
    public float emotionalFailChance = 0.65f;
    public float emotionalJackpotChance = 0.12f;
    public IntRange jackpotAdvancedData = new(3, 5);
    public float jackpotFragmentChance = 0.3f;
    public float failNetsickGain = 0.1f;

    // T1.5 neural load: added by each dive; the Netsick a dive gives is multiplied by the factor of the stage the load had before it.
    public float neuralLoadPerDive = 0.25f;
    public System.Collections.Generic.List<float> neuralLoadNetsickFactors = [1f, 2f, 3f];
    // Hack chance lost at each load stage (plan v1: the third dive of a day hacks worse).
    public System.Collections.Generic.List<float> neuralLoadHackPenalties = [0f, 0.05f, 0.1f];
}
