using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>A net ritual precept: named after its def (no generated name).</summary>
public class Precept_NetRitual : Precept_Ritual
{
    public override string GenerateNameRaw() => def.LabelCap;
}

/// <summary>The Net Archon role: named after its def.</summary>
public class Precept_NetArchon : Precept_RoleSingle
{
    public override string GenerateNameRaw() => def.LabelCap;

    public static bool Holds(Pawn pawn)
        => ModsConfig.IdeologyActive && pawn?.Ideo?.GetRole(pawn)?.def?.defName == "NR_IdeoRole_NetArchon";
}

/// <summary>Shared by the net rituals: remembers the outcome the vanilla worker picked.</summary>
public abstract class RitualOutcomeEffectWorker_Net : RitualOutcomeEffectWorker_FromQuality
{
    protected RitualOutcomePossibility Last;

    protected RitualOutcomeEffectWorker_Net()
    {
    }

    protected RitualOutcomeEffectWorker_Net(RitualOutcomeEffectDef def) : base(def)
    {
    }

    public override RitualOutcomePossibility GetOutcome(float quality, LordJob_Ritual ritual)
    {
        Last = base.GetOutcome(quality, ritual);
        return Last;
    }

    public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
    {
        Last = null;
        base.Apply(progress, totalPresence, jobRitual);
        try
        {
            ApplyNet(Last?.positivityIndex ?? 0);
        }
        catch (System.Exception e)
        {
            Log.Warning("[Netrun] ritual outcome failed: " + e.Message);
        }
    }

    protected abstract void ApplyNet(int positivity);
}

/// <summary>Litany of data: the AIs' trust +5/+10/+15 by outcome; unheard: hunger +10.</summary>
public class RitualOutcomeEffectWorker_Litany : RitualOutcomeEffectWorker_Net
{
    public RitualOutcomeEffectWorker_Litany()
    {
    }

    public RitualOutcomeEffectWorker_Litany(RitualOutcomeEffectDef def) : base(def)
    {
    }

    protected override void ApplyNet(int positivity)
    {
        foreach (NetAI ai in AIManager.Get()?.AIs.Where(a => a.Housed) ?? Enumerable.Empty<NetAI>())
        {
            if (positivity < 0)
                ai.AddHunger(10f);
            else
                ai.AddTrust(positivity >= 2 ? 15f : 10f);
        }
    }
}

/// <summary>Trace cleansing: Heat -5/-10/-15 by outcome, without a fixer.</summary>
public class RitualOutcomeEffectWorker_TraceCleansing : RitualOutcomeEffectWorker_Net
{
    public RitualOutcomeEffectWorker_TraceCleansing()
    {
    }

    public RitualOutcomeEffectWorker_TraceCleansing(RitualOutcomeEffectDef def) : base(def)
    {
    }

    protected override void ApplyNet(int positivity)
    {
        float amount = positivity >= 2 ? 15f : positivity >= 1 ? 10f : 5f;
        HeatTracker.Get()?.ReduceHeat(amount, "NR_RitualCleansingLog".Translate(), HeatSource.Other);
    }
}
