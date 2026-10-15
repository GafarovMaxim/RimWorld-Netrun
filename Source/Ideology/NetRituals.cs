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

/// <summary>
/// Kept only so older saves load: the Net Archon is now a plain vanilla Precept_RoleSingle (apparel, abilities and the
/// role editor work as for the game's roles). Holds() checks the def, so it works for both.
/// </summary>
public class Precept_NetArchon : Precept_RoleSingle
{

    public static bool Holds(Pawn pawn)
        => ModsConfig.IdeologyActive && pawn?.Ideo?.GetRole(pawn)?.def?.defName == "NR_IdeoRole_NetArchon";
}

/// <summary>Trace cleansing has no spectators: only the netrunner roles take part.</summary>
public class RitualSpectatorFilter_None : RitualSpectatorFilter
{
    public override bool Allowed(Pawn p) => false;
}

/// <summary>Ritual quality from the average skill of the pawns in several roles (the trace cleansing netrunners).</summary>
public class RitualOutcomeComp_RolesSkill : RitualOutcomeComp_Quality
{
    public SkillDef skillDef;
    public List<string> roleIds = [];

    private float Average(IEnumerable<Pawn> pawns)
    {
        int n = 0;
        float sum = 0f;
        foreach (Pawn p in pawns)
        {
            if (p?.skills == null)
                continue;
            sum += p.skills.GetSkill(skillDef).Level;
            n++;
        }
        return n == 0 ? 0f : sum / n;
    }

    // Roles with a mergeId are merged into one in the assignments, so the ids of the others are not found there
    // (GetRole returns null and AssignedPawns throws): match the roles by id or mergeId instead.
    private IEnumerable<Pawn> Pawns(RitualRoleAssignments assignments)
    {
        if (assignments?.AllRolesForReading == null)
            yield break;
        foreach (RitualRole role in assignments.AllRolesForReading)
        {
            if (role == null || !roleIds.Contains(role.id) && (role.mergeId == null || !roleIds.Contains(role.mergeId)))
                continue;
            foreach (Pawn p in assignments.AssignedPawns(role))
                yield return p;
        }
    }

    public override float Count(LordJob_Ritual ritual, RitualOutcomeComp_Data data)
        => Average(Pawns(ritual?.assignments));

    public override string GetDesc(LordJob_Ritual ritual = null, RitualOutcomeComp_Data data = null)
    {
        if (ritual == null)
            return labelAbstract;
        float x = Count(ritual, data);
        float q = curve.Evaluate(x);
        return label.CapitalizeFirst() + " " + x.ToString("0.#") + ": " + "OutcomeBonusDesc_QualitySingleOffset".Translate(q.ToStringWithSign("0.#%")) + ".";
    }

    public override QualityFactor GetQualityFactor(Precept_Ritual ritual, TargetInfo ritualTarget, RitualObligation obligation, RitualRoleAssignments assignments, RitualOutcomeComp_Data data)
    {
        float x = Average(Pawns(assignments));
        float q = curve.Evaluate(x);
        return new QualityFactor
        {
            label = label.CapitalizeFirst(),
            count = x.ToString("0.#"),
            qualityChange = "OutcomeBonusDesc_QualitySingleOffset".Translate(q.ToStringWithSign("0.#%")).Resolve(),
            positive = q > 0f,
            quality = q,
            priority = 0f,
        };
    }
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
    private static readonly HarmonyLib.AccessTools.FieldRef<Precept_Ritual, UnityEngine.Texture2D> RitualIcon =
        HarmonyLib.AccessTools.FieldRefAccess<Precept_Ritual, UnityEngine.Texture2D>("icon");

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
            // The icon path is copied into the save when a ritual is made, so a new icon needs it refreshed here.
            foreach (Precept precept in ideo.PreceptsListForReading)
            {
                if (precept is not Precept_Ritual r || !r.def.defName.StartsWith("NR_"))
                    continue;
                if (!r.isAnytime)
                {
                    r.isAnytime = true;
                    added++;
                }
                // The behaviour is saved with the ritual too: a pattern that changed its behaviour (trace cleansing with
                // three netrunners and no spectators) would keep the old one in an old save.
                RitualBehaviorDef behavior = r.sourcePattern?.ritualBehavior;
                if (behavior != null && r.behavior?.def != behavior)
                {
                    r.behavior = behavior.GetInstance();
                    added++;
                }
                string icon = r.sourcePattern?.iconPathOverride ?? r.def.iconPath;
                if (icon != null && r.iconPathOverride != icon)
                {
                    r.iconPathOverride = icon;
                    RitualIcon(r) = null;
                    added++;
                }
            }
            // A Net Archon made before the role became vanilla has no apparel requirements: give it the usual ones.
            foreach (Precept_Role role in ideo.RolesListForReading)
            {
                if (role.def.defName == "NR_IdeoRole_NetArchon" && role.apparelRequirements == null && !ideo.classicMode && !ideo.classicExtraMode)
                {
                    role.ApparelRequirements = role.GenerateNewApparelRequirements();
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
