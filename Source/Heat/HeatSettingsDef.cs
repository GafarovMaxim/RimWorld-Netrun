using Verse;

namespace Netrun;

/// <summary>Heat and cyberpod balance numbers (Defs/Heat.xml).</summary>
public class HeatSettingsDef : Def
{
    // Heat from a completed hack: target defence / defencePerHeat, clamped.
    public float defencePerHeat = 1000f;
    public FloatRange hackHeat = new(2f, 15f);

    // Hacker stealth: heat is multiplied by baseStealth / stealth, clamped.
    public float baseStealth = 100f;
    public FloatRange stealthFactor = new(0.25f, 2f);

    // Hacking from a Hacking Expansion cyberpod.
    public float cyberpodHeatFactor = 0.6f;
    public float cyberpodStealthFactor = 1.3f;
    public float cyberpodBonusDataChance = 0.25f;

    // Ripping data with Hacking Expansion.
    public float heatPerRippedItem = 1f;

    // ICE outcome "Trace lock".
    public float traceLockHeat = 8f;
}
