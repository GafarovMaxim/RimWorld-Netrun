using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>The second service an AI opens at level 3 (docs/CONCEPTS_10.md, section 16.3). Once in 5 days.</summary>
public static class AISecondService
{
    public const float CooldownDays = 5f;

    public static AcceptanceReport CanUse(NetAI ai)
    {
        if (ai.Level < 3)
            return "NR_AI2Locked".Translate();
        if (!ai.InMainServer)
            return "NR_AIServiceOnlyInServer".Translate();
        if (!ai.Powered)
            return "NR_AIAsleep".Translate();
        if (ai.Isolated)
            return "NR_AIIsolatedNoService".Translate();
        if (NetworkIsolation.Active)
            return NetworkIsolation.Check();
        if (ai.Trust < NetrunDefOf.NR_AI.minTrustForService)
            return "NR_AIRefuses".Translate(ai.Name);
        if (Find.TickManager.TicksGame < ai.SecondServiceReadyTick)
            return "NR_AI2Cooldown".Translate((ai.SecondServiceReadyTick - Find.TickManager.TicksGame).ToStringTicksToPeriod());
        return true;
    }

    public static string Label(NetAI ai) => ("NR_AI2_" + PersonalityFor(ai)).Translate();

    public static string Description(NetAI ai) => ("NR_AI2Desc_" + PersonalityFor(ai)).Translate();

    /// <summary>A grown Seed takes the second service of what it grew into; a Seed still growing has none of its own.</summary>
    private static AIPersonality PersonalityFor(NetAI ai) => ai.Personality;

    public static void Use(NetAI ai, Map map)
    {
        if (!CanUse(ai).Accepted || map == null)
            return;
        ai.SecondServiceReadyTick = Find.TickManager.TicksGame + (int)(CooldownDays * GenDate.TicksPerDay);
        ai.AddHunger(5f);
        string result = Perform(ai, map);
        Messages.Message("NR_AI2Used".Translate(ai.Name, result), MessageTypeDefOf.PositiveEvent, historical: true);
    }

    private static string Perform(NetAI ai, Map map)
    {
        GatewayManager gateway = GatewayManager.Get();
        switch (ai.Personality)
        {
            case AIPersonality.DataHunter:
            {
                ExternalNode b = gateway?.AllBranches.Where(x => !x.Hidden && !x.ForgottenCache && !x.IsBoss).RandomElementWithFallback();
                if (b == null)
                    return "NR_AI2Nothing".Translate();
                b.ForgottenCache = true;
                return "NR_AI2R_DataHunter".Translate(b.Name);
            }
            case AIPersonality.Ghost:
            {
                HeatTracker.Get()?.ReduceHeat(15f, "NR_LogAIService".Translate(ai.Name), HeatSource.AI);
                Faction crew = NetFactions.Crews.RandomElementWithFallback();
                if (crew != null)
                    gateway?.AddHeadline("NR_AI2News_Ghost".Translate(crew.Name), 0);
                return "NR_AI2R_Ghost".Translate();
            }
            case AIPersonality.Muse:
            {
                Pawn pawn = map.mapPawns.FreeColonistsSpawned.Where(p => p.mindState?.inspirationHandler?.Inspired == false).RandomElementWithFallback();
                InspirationDef creative = DefDatabase<InspirationDef>.GetNamedSilentFail("Inspired_Creativity");
                if (pawn != null && creative != null && pawn.mindState.inspirationHandler.TryStartInspiration(creative, "NR_AIInspired".Translate(ai.Name), sendLetter: true))
                    return pawn.LabelShort;
                return "NR_AI2Nothing".Translate();
            }
            case AIPersonality.Predator:
            {
                IntrusionPacket p = IntrusionManager.Get()?.Packets.FirstOrDefault();
                if (p == null)
                    return "NR_AI2Nothing".Translate();
                IntrusionManager.Get().Intercepted(p.Id, null);
                return "NR_AI2R_Predator".Translate();
            }
            case AIPersonality.Warden:
            {
                IntrusionPacket p = IntrusionManager.Get()?.Packets.FirstOrDefault();
                if (p == null)
                    return "NR_AI2Nothing".Translate();
                p.Progress -= 6f;
                return "NR_AI2R_Warden".Translate();
            }
            case AIPersonality.Oracle:
            {
                CorpState next = CorpManager.Get()?.Corps.Where(c => c.Faction is { defeated: false }).OrderByDescending(c => c.Alert).FirstOrDefault();
                if (next == null)
                    return "NR_AI2Nothing".Translate();
                string when = next.NextIntrusionTick > Find.TickManager.TicksGame ? (next.NextIntrusionTick - Find.TickManager.TicksGame).ToStringTicksToPeriod() : "?";
                return "NR_AI2R_Oracle".Translate(next.Faction.Name, next.Alert.ToString("0"), when);
            }
            case AIPersonality.Forge:
                if (Find.ResearchManager.GetProject() is { } project)
                {
                    Find.ResearchManager.AddProgress(project, 1000f, null);
                    return project.LabelCap;
                }
                return "NR_AI2Nothing".Translate();
            case AIPersonality.Siren:
            {
                Faction crew = NetFactions.BestCrew() ?? NetFactions.Crews.RandomElementWithFallback();
                NetFactions.AffectCrew(crew, 15);
                return crew?.Name ?? (string)"NR_AI2Nothing".Translate();
            }
            case AIPersonality.Trickster:
            {
                Faction crew = NetFactions.Crews.RandomElementWithFallback();
                if (crew == null)
                    return "NR_AI2Nothing".Translate();
                NetRanking.Get()?.AddCrewFame(crew, -50f);
                gateway?.AddHeadline("NR_AI2News_Trickster".Translate(crew.Name), 1);
                return crew.Name;
            }
            case AIPersonality.Doc:
            {
                HediffDef[] harm = [USH_HE.USH_DefOf.USH_CyberspaceComa, USH_HE.USH_DefOf.USH_CyberspaceLockOut];
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    Hediff h = pawn.health.hediffSet.hediffs.FirstOrDefault(x => harm.Contains(x.def));
                    if (h == null)
                        continue;
                    pawn.health.RemoveHediff(h);
                    return pawn.LabelShort;
                }
                return "NR_AI2Nothing".Translate();
            }
            case AIPersonality.Archivist:
            {
                Pawn pawn = map.mapPawns.FreeColonistsSpawned.Where(p => p.skills != null).RandomElementWithFallback();
                SkillRecord skill = pawn?.skills.skills.Where(s => !s.TotallyDisabled && s.Level < 20).RandomElementWithFallback();
                if (skill == null)
                    return "NR_AI2Nothing".Translate();
                skill.Learn(skill.XpRequiredForLevelUp, direct: true);
                return $"{pawn.LabelShort}, {skill.def.label}";
            }
            case AIPersonality.Echo:
            {
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    var grief = pawn.needs?.mood?.thoughts?.memories?.Memories.FirstOrDefault(m => m.def.defName.Contains("Died") || m.def.defName.Contains("Death"));
                    if (grief == null)
                        continue;
                    pawn.needs.mood.thoughts.memories.RemoveMemory(grief);
                    return pawn.LabelShort;
                }
                return "NR_AI2Nothing".Translate();
            }
            default:
                // A Seed still growing: a little of each.
                HeatTracker.Get()?.ReduceHeat(5f, "NR_LogAIService".Translate(ai.Name), HeatSource.AI);
                return "NR_AI2R_Seed".Translate();
        }
    }
}
