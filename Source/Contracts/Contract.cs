using UnityEngine;
using Verse;

namespace Netrun;

public enum ContractType
{
    DataDelivery,
    SystemTap,
    QuietRun,
    // New types are appended so old saves keep their values.
    Surveillance,
    LayLow,
    Rookie,
    AIOrder,
    CleanLab,
}

public class Contract : IExposable
{
    public int Id;
    public int FixerId;
    public ContractType Type;
    public int Goal;
    public int Progress;
    public int Reward;
    public int DurationTicks;
    public int DeadlineTick = -1;
    /// <summary>Per type: the wanted braindance mood (CleanLab), ticks toward the next hour (Surveillance).</summary>
    public int Param;
    /// <summary>Royal favor on top of the silver (an imperial client's contract).</summary>
    public int Favor;
    /// <summary>D3: the cyberspace hub a surveillance contract watches (-1: any pod counts, as before).</summary>
    public int TargetHub = -1;

    public bool Accepted => DeadlineTick >= 0;
    public bool Done => Progress >= Goal;
    public int TicksLeft => Mathf.Max(0, DeadlineTick - Find.TickManager.TicksGame);

    public string Title => ("NR_Contract_" + Type).Translate();
    public string Description => Type == ContractType.Surveillance && WatchesHub
        ? "NR_ContractDesc_SurveillanceNode".Translate(Goal, ParamText)
        : ("NR_ContractDesc_" + Type).Translate(Goal, ParamText, NetrunDefOf.NR_Fixers.rookieMaxSkill);

    /// <summary>A surveillance contract that needs a runner in its hub (the option is on and the hub is set).</summary>
    public bool WatchesHub => Type == ContractType.Surveillance && TargetHub >= 0 && NetrunMod.Settings.SurveillanceInNode;

    /// <summary>The type's extra detail in words (the wanted mood of a CleanLab, the watched hub of a surveillance).</summary>
    public string ParamText => Type switch
    {
        ContractType.CleanLab => ("NR_BDMood_" + (BraindanceMood)Param).Translate().ToString(),
        ContractType.Surveillance when TargetHub >= 0 => NetMapManager.NodeLabel(NetMapManager.Get()?.Map?.Node(TargetHub)),
        _ => string.Empty,
    };

    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id");
        Scribe_Values.Look(ref FixerId, "fixerId");
        Scribe_Values.Look(ref Type, "type");
        Scribe_Values.Look(ref Goal, "goal");
        Scribe_Values.Look(ref Progress, "progress");
        Scribe_Values.Look(ref Reward, "reward");
        Scribe_Values.Look(ref DurationTicks, "durationTicks");
        Scribe_Values.Look(ref DeadlineTick, "deadlineTick", -1);
        Scribe_Values.Look(ref Param, "param");
        Scribe_Values.Look(ref Favor, "favor");
        Scribe_Values.Look(ref TargetHub, "targetHub", -1);
    }
}

/// <summary>A finished contract for the Board's history line.</summary>
public class ContractRecord : IExposable
{
    public ContractType Type;
    public string FixerName;
    public bool Success;
    public int Reward;

    public void ExposeData()
    {
        Scribe_Values.Look(ref Type, "type");
        Scribe_Values.Look(ref FixerName, "fixerName");
        Scribe_Values.Look(ref Success, "success");
        Scribe_Values.Look(ref Reward, "reward");
    }
}
