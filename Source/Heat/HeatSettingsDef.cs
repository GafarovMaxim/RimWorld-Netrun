using Verse;

namespace Netrun;

/// <summary>Heat and cyberpod balance numbers (Defs/Heat.xml).</summary>
public class HeatSettingsDef : Def
{
    // Heat from a completed hack: target defence / defencePerHeat, clamped.
    public float defencePerHeat = 1000f;
    public FloatRange hackHeat = new(2f, 15f);
    /// <summary>Royalty permit "imperial trace scrub": Heat taken off per use.</summary>
    public float imperialScrubAmount = 20f;

    // Hacker stealth: heat is multiplied by baseStealth / stealth, clamped.
    public float baseStealth = 100f;
    public FloatRange stealthFactor = new(0.25f, 2f);

    // Hacking from a Hacking Expansion cyberpod.
    public float cyberpodHeatFactor = 0.6f;
    public float cyberpodStealthFactor = 1.3f;
    public float cyberpodBonusDataChance = 0.25f;

    // An AI in a Netrun coprocessor linked to the cyberpod: Heat factor, extra data chance and Netsick factor.
    // Ghost hides better, Data Hunter finds more, Doc and Warden protect the diver's mind.
    public float coprocessorHeatFactor = 0.8f;
    public float coprocessorGhostHeatFactor = 0.5f;
    public float coprocessorBonusDataChance = 0.1f;
    public float coprocessorDataHunterBonusDataChance = 0.3f;
    public float coprocessorNetsickFactor = 0.75f;
    public float coprocessorDocNetsickFactor = 0.4f;

    // Ripping data with Hacking Expansion.
    public float heatPerRippedItem = 1f;

    // ICE outcome "Trace lock".
    public float traceLockHeat = 8f;
    /// <summary>Passive cooling a day = decayBase + decayPerHeat x Heat, times the cooling setting.</summary>
    public float decayBase = 3f;
    public float decayPerHeat = 0.04f;
    /// <summary>Points multiplier of vanilla enemy raids on a player home at Tracked, Target and the Red Zone.</summary>
    public float raidPointsTracked = 1.1f;
    public float raidPointsTarget = 1.2f;
    public float raidPointsRedZone = 1.25f;
    /// <summary>After this many hours of unbroken network isolation, cooling is multiplied by the factor.</summary>
    public float isolationCoolingHours = 12f;
    public float isolationCoolingFactor = 2f;
    /// <summary>Red Zone: a dive's entry costs this much more cyberdata.</summary>
    public int redZoneDiveExtraCost = 1;
}
