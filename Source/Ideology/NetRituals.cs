using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// Kept only so older saves load: the net rituals now use the game's own Precept_Ritual, because the
/// ideoligion screen lists rituals of exactly that class (a subclass was hidden there).
/// </summary>
public class Precept_NetRitual : Precept_Ritual
{
}

/// <summary>The Net Archon role. Its name comes from NR_NamerNetArchon.</summary>
public class Precept_NetArchon : Precept_RoleSingle
{

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
                // 5 / 10 / 15 by outcome, as the ritual description says (like the trace cleansing).
                ai.AddTrust(positivity >= 2 ? 15f : positivity == 1 ? 10f : 5f);
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

/// <summary>
/// The game adds a meme's required rituals only when an ideoligion is generated, so a net meme added to an
/// existing ideoligion came without its ritual (no ritual button on the AI server, nothing in the ritual list).
/// Run after loading and once a day by the janitor: adds the missing ones to the player's ideoligions.
/// </summary>
public static class NetRitualSync
{
    public static int EnsureMemeRituals()
    {
        if (!ModsConfig.IdeologyActive || Faction.OfPlayer?.ideos == null)
            return 0;
        int added = 0;
        foreach (Ideo ideo in Faction.OfPlayer.ideos.AllIdeos)
        {
            if (ideo?.memes == null)
                continue;
            // Our rituals have no date: one saved as "by date" never shows its button, so make it "any time".
            foreach (Precept precept in ideo.PreceptsListForReading)
            {
                if (precept is Precept_Ritual r && !r.isAnytime && r.def.defName.StartsWith("NR_"))
                {
                    r.isAnytime = true;
                    added++;
                }
            }
            foreach (MemeDef meme in ideo.memes)
            {
                if (meme.requiredRituals == null || !meme.defName.StartsWith("NR_"))
                    continue;
                foreach (RequiredRitualAndBuilding req in meme.requiredRituals)
                {
                    if (req?.precept == null || req.pattern == null)
                        continue;
                    if (ideo.PreceptsListForReading.Any(p => p is Precept_Ritual r && r.sourcePattern == req.pattern))
                        continue;
                    var ritual = (Precept_Ritual)PreceptMaker.MakePrecept(req.precept);
                    ideo.AddPrecept(ritual, init: true, null, req.pattern);
                    ritual.RegenerateName();
                    added++;
                }
            }
        }
        return added;
    }
}
