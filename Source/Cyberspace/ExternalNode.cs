using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

public enum ExternalNodeType
{
    Market,
    Archive,
    Corp,
    Abandoned,
    DarkExchange,
}

/// <summary>Numbers for the Gateway and dives (docs/CYBERSPACE.md), in Defs/Cyberspace.xml.</summary>
public class CyberspaceSettingsDef : Def
{
    public IntRange nodeCount = new(4, 6);
    public int hiddenNodes = 1;
    public float refreshDays = 5f;
    public int searchCost = 5;
    public int programCost = 1;
    public int programSlots = 2;
    /// <summary>Entry cost of any dive; the node adds one per two defense levels.</summary>
    public int diveBaseCost = 1;
    /// <summary>Discounts on the dive cost: an AI in the pod's coprocessor, an AI riding in the netrunner.</summary>
    public int coprocessorDiscount = 1;
    public int passengerDiscount = 1;
    public int maxTurns = 8;

    public float hackBase = 0.35f;
    public float hackPerSkill = 0.03f;
    public float hackPerStrength = 0.06f;
    public float swordBonus = 0.3f;

    public float tracePerAction = 1f;
    public float stealthTraceFactor = 0.5f;
    public float iceAttackChance = 0.6f;
    public float decoyChance = 0.5f;
    public float retreatLootLoss = 0.5f;
    public float netsickPerDive = 0.03f;
    public float netsickPerIceHit = 0.1f;
    public float successXp = 400f;
}

/// <summary>A node outside the home cluster: something to dive into from a cyberpod.</summary>
public class ExternalNode : IExposable
{
    public int Id;
    public int Number;
    public ExternalNodeType Type;
    public int Defense;
    public bool BlackIce;
    public int IceStrength;
    public bool Hidden;
    public bool Contract;

    public string Name => ("NR_ExtNodeName_" + Type).Translate(Number.ToString("00"));
    public string TypeLabel => ("NR_ExtNodeType_" + Type).Translate();

    public Color Color => Type switch
    {
        ExternalNodeType.Market => NetrunUI.Accent,
        ExternalNodeType.Archive => NetrunUI.Ok,
        ExternalNodeType.Corp => NetrunUI.Bad,
        ExternalNodeType.Abandoned => NetrunUI.Purple,
        _ => NetrunUI.Warn,
    };

    /// <summary>Rough value of the haul, for the circle size and the panel.</summary>
    public float RewardScore => Type switch
    {
        ExternalNodeType.Corp => 1f,
        ExternalNodeType.DarkExchange => 0.8f,
        ExternalNodeType.Abandoned => 0.7f,
        ExternalNodeType.Market => 0.6f,
        _ => 0.45f,
    } + Defense * 0.05f;

    public float HeatPerTrace => Type switch
    {
        ExternalNodeType.Corp => 1.5f,
        ExternalNodeType.DarkExchange => 1.2f,
        _ => 1f,
    };

    public Texture2D Icon => NetrunTex.CyberIcon("Node_" + Type switch
    {
        ExternalNodeType.Corp => "CorpVault",
        ExternalNodeType.Abandoned => "AbandonedServer",
        _ => Type.ToString(),
    });

    /// <summary>0 low (white ICE, light gates), 1 medium (strong gates), 2 high (Black ICE).</summary>
    public int Risk => BlackIce ? 2 : Defense >= 3 ? 1 : 0;

    public Texture2D RiskIcon => NetrunTex.CyberIcon(Risk switch { 2 => "Dive_Lightning_Red", 1 => "Dive_Lightning_Yellow", _ => "Dive_Lightning_Grey" });

    /// <summary>Icons of what the node pays out, for the reward row.</summary>
    public System.Collections.Generic.IEnumerable<Texture2D> RewardIcons
    {
        get
        {
            if (Type == ExternalNodeType.DarkExchange)
                yield return ThingDefOf.Silver.uiIcon;
            yield return NetrunDefOf.USH_Cyberdata.uiIcon;
            if (Type == ExternalNodeType.Corp)
                yield return NetrunDefOf.USH_AdvancedCyberdata.uiIcon;
            if (Type == ExternalNodeType.Abandoned)
                yield return NetrunDefOf.NR_AIFragment.uiIcon;
            if (Type is ExternalNodeType.Corp or ExternalNodeType.DarkExchange)
                yield return NetrunTex.CyberIcon("Reward_Valuables");
        }
    }

    public string RewardText => ("NR_ExtNodeReward_" + Type).Translate();

    public static ExternalNode Generate(int id)
    {
        var type = (ExternalNodeType)Rand.Range(0, 5);
        int defense = Rand.RangeInclusive(1, 5);
        bool blackIce = type == ExternalNodeType.Corp || (defense >= 4 && Rand.Chance(0.5f));
        return new ExternalNode
        {
            Id = id,
            Number = Rand.RangeInclusive(1, 99),
            Type = type,
            Defense = defense,
            BlackIce = blackIce,
            IceStrength = blackIce ? defense + Rand.RangeInclusive(1, 2) : 0,
        };
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id");
        Scribe_Values.Look(ref Number, "number");
        Scribe_Values.Look(ref Type, "type");
        Scribe_Values.Look(ref Defense, "defense");
        Scribe_Values.Look(ref BlackIce, "blackIce");
        Scribe_Values.Look(ref IceStrength, "iceStrength");
        Scribe_Values.Look(ref Hidden, "hidden");
        Scribe_Values.Look(ref Contract, "contract");
    }
}
