using UnityEngine;
using Verse;

namespace Netrun;

public enum ContractType
{
    DataDelivery,
    SystemTap,
    QuietRun,
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

    public bool Accepted => DeadlineTick >= 0;
    public bool Done => Progress >= Goal;
    public int TicksLeft => Mathf.Max(0, DeadlineTick - Find.TickManager.TicksGame);

    public string Title => ("NR_Contract_" + Type).Translate();
    public string Description => ("NR_ContractDesc_" + Type).Translate(Goal);

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
