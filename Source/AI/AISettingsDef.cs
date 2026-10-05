using Verse;

namespace Netrun;

/// <summary>AI balance numbers (Defs/AI.xml).</summary>
public class AISettingsDef : Def
{
    // Server slots: base, maximum with core modules.
    public int baseSlots = 1;
    public int maxSlots = 3;

    // Awakening a fragment at the comms console.
    public int awakenWorkTicks = 5000;
    public float awakenDetectChancePerHeat = 0.006f;
    public float awakenDetectHeat = 10f;

    // Drops on completed hacks, by Hacking Expansion hackset of the target.
    public float fragmentChanceCoreCommandICE = 0.1f;
    public float fragmentChanceBlackICE = 0.25f;
    public float moduleChanceBlackICE = 0.05f;
    public float moduleChanceBlackBox = 0.1f;
}
