using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using USH_HE;
using Verse;
using Verse.AI;

namespace Netrun;

/// <summary>Who an effect lands on.</summary>
public enum AIEventWho
{
    Target,
    Hacker,
    All,
    Others,
    Random,
}

/// <summary>One building block of an AI event outcome; events in Defs/AIEvents.xml are lists of these.</summary>
public abstract class AIEventEffect
{
    public abstract void Apply(AIEventContext c);

    protected static IEnumerable<Pawn> Pawns(AIEventContext c, AIEventWho who)
    {
        List<Pawn> colonists = c.Map.mapPawns.FreeColonistsSpawned.ToList();
        switch (who)
        {
            case AIEventWho.Target:
                if (c.Target != null)
                    yield return c.Target;
                break;
            case AIEventWho.Hacker:
                if (c.Hacker != null)
                    yield return c.Hacker;
                break;
            case AIEventWho.All:
                foreach (Pawn p in colonists)
                    yield return p;
                break;
            case AIEventWho.Others:
                foreach (Pawn p in colonists.Where(p => p != c.Target))
                    yield return p;
                break;
            case AIEventWho.Random:
                if (colonists.TryRandomElement(out Pawn random))
                    yield return random;
                break;
        }
    }
}

public class AIEffect_Trust : AIEventEffect
{
    public float amount;
    public override void Apply(AIEventContext c) => c.AI.AddTrust(amount);
}

public class AIEffect_Hunger : AIEventEffect
{
    public float amount;
    public override void Apply(AIEventContext c) => c.AI.AddHunger(amount);
}

public class AIEffect_Heat : AIEventEffect
{
    public float amount;

    public override void Apply(AIEventContext c)
    {
        HeatTracker tracker = HeatTracker.Get();
        string reason = "NR_AIEventHeatReason".Translate(c.AI.Name, c.Def.label);
        if (amount > 0f)
            tracker?.AddHeat(amount, reason);
        else if (amount < 0f)
            tracker?.ReduceHeat(-amount, reason);
    }
}

/// <summary>Items appear next to the AI's server or host.</summary>
public class AIEffect_Items : AIEventEffect
{
    public ThingDef thing;
    public IntRange count = IntRange.One;

    public override void Apply(AIEventContext c)
    {
        int left = count.RandomInRange;
        while (left > 0)
        {
            Thing stack = ThingMaker.MakeThing(thing);
            stack.stackCount = Mathf.Min(left, thing.stackLimit);
            left -= stack.stackCount;
            GenPlace.TryPlaceThing(stack, c.Spot, c.Map, ThingPlaceMode.Near);
        }
    }
}

public class AIEffect_Inspiration : AIEventEffect
{
    public AIEventWho who = AIEventWho.Target;
    public InspirationDef def;
    public int count = 1;

    public override void Apply(AIEventContext c)
    {
        foreach (Pawn pawn in Pawns(c, who).Take(count))
        {
            InspirationDef inspiration = def ?? pawn.mindState?.inspirationHandler?.GetRandomAvailableInspirationDef();
            if (inspiration != null)
                pawn.mindState.inspirationHandler.TryStartInspiration(inspiration, "NR_AIEventInspired".Translate(c.AI.Name));
        }
    }
}

public class AIEffect_Thought : AIEventEffect
{
    public ThoughtDef def;
    public AIEventWho who = AIEventWho.Target;
    public float chance = 1f;

    public override void Apply(AIEventContext c)
    {
        foreach (Pawn pawn in Pawns(c, who))
        {
            if (Rand.Chance(chance))
                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(def);
        }
    }
}

public class AIEffect_Hediff : AIEventEffect
{
    public HediffDef def;
    public AIEventWho who = AIEventWho.Target;

    public override void Apply(AIEventContext c)
    {
        foreach (Pawn pawn in Pawns(c, who))
            pawn.health.AddHediff(def);
    }
}

/// <summary>Experience in a skill; no skill means the pawn's best one, randomSkill a random one.</summary>
public class AIEffect_Skill : AIEventEffect
{
    public SkillDef skill;
    public bool randomSkill;
    public float amount = 1000f;
    public AIEventWho who = AIEventWho.Target;

    public override void Apply(AIEventContext c)
    {
        foreach (Pawn pawn in Pawns(c, who))
        {
            if (pawn.skills == null)
                continue;
            SkillDef target = skill
                ?? (randomSkill
                    ? pawn.skills.skills.Where(s => !s.TotallyDisabled).Select(s => s.def).RandomElementWithFallback()
                    : pawn.skills.skills.Where(s => !s.TotallyDisabled).MaxByWithFallback(s => s.Level)?.def);
            if (target != null)
                pawn.skills.Learn(target, amount, direct: true);
        }
    }
}

/// <summary>The AI's service is unavailable for a while.</summary>
public class AIEffect_ServiceBlock : AIEventEffect
{
    public float days = 1f;

    public override void Apply(AIEventContext c)
        => c.AI.ServiceReadyTick = Mathf.Max(c.AI.ServiceReadyTick, Find.TickManager.TicksGame + (int)(days * GenDate.TicksPerDay));
}

public class AIEventBranch
{
    public float weight = 1f;
    [MustTranslate] public string result;
    public List<AIEventEffect> effects = [];
}

/// <summary>One of several outcomes by weight; its result text is shown as a message.</summary>
public class AIEffect_Chance : AIEventEffect
{
    public List<AIEventBranch> branches = [];

    public override void Apply(AIEventContext c)
    {
        if (!branches.TryRandomElementByWeight(b => b.weight, out AIEventBranch branch))
            return;
        foreach (AIEventEffect effect in branch.effects)
            effect.Apply(c);
        if (!branch.result.NullOrEmpty())
            Messages.Message(c.Format(branch.result), MessageTypeDefOf.NeutralEvent, historical: true);
    }
}

/// <summary>Effects that land later; the key finds them in the def again after a save.</summary>
public class AIEffect_Delayed : AIEventEffect
{
    public string key;
    public float days = 1f;
    [MustTranslate] public string result;
    public List<AIEventEffect> effects = [];

    public override void Apply(AIEventContext c) => AIEventManager.Get()?.AddPending(this, c);

    public static AIEffect_Delayed Find(AIEventDef def, string key)
    {
        IEnumerable<AIEventEffect> all = def.effects.Concat(def.options.SelectMany(o => o.effects));
        return Search(all, key);
    }

    private static AIEffect_Delayed Search(IEnumerable<AIEventEffect> effects, string key)
    {
        foreach (AIEventEffect effect in effects)
        {
            switch (effect)
            {
                case AIEffect_Delayed d when d.key == key:
                    return d;
                case AIEffect_Delayed d when Search(d.effects, key) is { } inner:
                    return inner;
                case AIEffect_Chance chance when Search(chance.branches.SelectMany(b => b.effects), key) is { } inner:
                    return inner;
            }
        }
        return null;
    }
}

/// <summary>Fires an incident on the map, now or after a delay.</summary>
public class AIEffect_Incident : AIEventEffect
{
    public IncidentDef def;
    public float delayHours;

    public override void Apply(AIEventContext c)
    {
        IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, c.Map);
        parms.forced = true;
        if (delayHours <= 0f)
        {
            if (def.Worker.CanFireNow(parms))
                def.Worker.TryExecute(parms);
            return;
        }
        Find.Storyteller.incidentQueue.Add(new QueuedIncident(new FiringIncident(def, null, parms),
            Find.TickManager.TicksGame + (int)(delayHours * GenDate.TicksPerHour)));
    }
}

/// <summary>A spark at the server.</summary>
public class AIEffect_Fire : AIEventEffect
{
    public float chance = 1f;

    public override void Apply(AIEventContext c)
    {
        if (!Rand.Chance(chance) || c.Anchor is not { Spawned: true } anchor)
            return;
        IntVec3 cell = GenAdj.CellsAdjacent8Way(anchor).Where(x => x.InBounds(c.Map) && x.Walkable(c.Map)).RandomElementWithFallback(anchor.Position);
        FireUtility.TryStartFireIn(cell, c.Map, 0.5f, null);
    }
}

/// <summary>Network attacks are stopped more often for a while.</summary>
public class AIEffect_Defense : AIEventEffect
{
    public float amount = 0.3f;
    public float days = 1f;
    public override void Apply(AIEventContext c) => AIEventManager.Get()?.AddDefense(amount, days);
}

/// <summary>No mercenary raids for a while.</summary>
public class AIEffect_MercDelay : AIEventEffect
{
    public float days = 3f;
    public override void Apply(AIEventContext c) => AIEventManager.Get()?.BlockMercs(days);
}

public class AIEffect_Netsick : AIEventEffect
{
    public float amount = -0.2f;
    public AIEventWho who = AIEventWho.Hacker;

    public override void Apply(AIEventContext c)
    {
        foreach (Pawn pawn in Pawns(c, who))
        {
            Hediff netsick = pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick);
            if (netsick != null)
                netsick.Severity = Mathf.Max(0.001f, netsick.Severity + amount);
        }
    }
}

/// <summary>The AI is erased (an Echo asking to be let go).</summary>
public class AIEffect_Erase : AIEventEffect
{
    public override void Apply(AIEventContext c) => AIManager.Get()?.Erase(c.AI);
}

/// <summary>The AI leaves for good, quietly.</summary>
public class AIEffect_Leave : AIEventEffect
{
    public override void Apply(AIEventContext c) => AIManager.Get()?.LeaveQuietly(c.AI);
}

/// <summary>Items are lost from storage, as many as there are up to the count.</summary>
public class AIEffect_TakeItems : AIEventEffect
{
    public ThingDef thing;
    public int count = 1;

    public override void Apply(AIEventContext c)
        => ColonyGoods.TryTake(c.Map, thing, Mathf.Min(count, ColonyGoods.Count(c.Map, thing)));
}

/// <summary>Every other AI of the colony changes its trust (jealousy, solidarity).</summary>
public class AIEffect_OtherAIsTrust : AIEventEffect
{
    public float amount;

    public override void Apply(AIEventContext c)
    {
        foreach (NetAI other in AIManager.Get()?.AIs.Where(a => a != c.AI) ?? Enumerable.Empty<NetAI>())
            other.AddTrust(amount);
    }
}

/// <summary>A weapon or apparel in storage goes up one quality level, up to a cap.</summary>
public class AIEffect_Quality : AIEventEffect
{
    public QualityCategory max = QualityCategory.Excellent;

    public override void Apply(AIEventContext c)
    {
        CompQuality comp = c.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
            .Where(t => (t.def.IsWeapon || t.def.IsApparel) && t.IsInAnyStorage())
            .Select(t => t.TryGetComp<CompQuality>())
            .Where(q => q != null && q.Quality < max)
            .RandomElementWithFallback();
        if (comp == null)
            return;
        comp.SetQuality(comp.Quality + 1, ArtGenerationContext.Colony);
        Messages.Message("NR_AIEventQualityUp".Translate(comp.parent.LabelCap), comp.parent, MessageTypeDefOf.PositiveEvent, historical: true);
    }
}

/// <summary>Batteries lose part of their charge.</summary>
public class AIEffect_BatteryDrain : AIEventEffect
{
    public float fraction = 0.4f;

    public override void Apply(AIEventContext c)
    {
        foreach (CompPowerBattery battery in c.Map.listerBuildings.allBuildingsColonist.Select(b => b.GetComp<CompPowerBattery>()).Where(b => b != null))
            battery.DrawPower(battery.StoredEnergy * fraction);
    }
}

/// <summary>A few switchable powered buildings are turned off.</summary>
public class AIEffect_PowerOff : AIEventEffect
{
    public int count = 3;

    public override void Apply(AIEventContext c)
    {
        IEnumerable<CompFlickable> switches = c.Map.listerBuildings.allBuildingsColonist
            .Where(b => b.GetComp<CompPowerTrader>() != null && b.def != NetrunDefOf.NR_AIServer)
            .Select(b => b.GetComp<CompFlickable>())
            .Where(f => f != null && f.SwitchIsOn)
            .InRandomOrder().Take(count);
        foreach (CompFlickable flick in switches)
            flick.SwitchIsOn = false;
    }
}

/// <summary>A colonist goes to the AI and spends some hours with it.</summary>
public class AIEffect_Session : AIEventEffect
{
    public float hours = 2f;
    public AIEventWho who = AIEventWho.Hacker;

    public override void Apply(AIEventContext c)
    {
        Thing anchor = c.Anchor is { Spawned: true } a && a is not Pawn ? a : null;
        foreach (Pawn pawn in Pawns(c, who).Where(p => !p.Downed && p != c.AI.Host))
        {
            Job job = anchor != null
                ? JobMaker.MakeJob(NetrunDefOf.NR_AIEventSession, anchor)
                : JobMaker.MakeJob(NetrunDefOf.NR_AIEventSession, pawn.Position);
            job.expiryInterval = (int)(hours * GenDate.TicksPerHour);
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }
}

public class AIEffect_PrisonerResistance : AIEventEffect
{
    public float amount = -10f;

    public override void Apply(AIEventContext c)
    {
        if (c.Map.mapPawns.PrisonersOfColonySpawned.TryRandomElement(out Pawn prisoner) && prisoner.guest != null)
            prisoner.guest.resistance = Mathf.Max(0f, prisoner.guest.resistance + amount);
    }
}

public class AIEffect_FixerReputation : AIEventEffect
{
    public FloatRange amount = new(-5f, 5f);

    public override void Apply(AIEventContext c)
    {
        if (FixerManager.Get()?.Fixers.TryRandomElement(out Fixer fixer) == true)
            fixer.GainReputation(amount.RandomInRange);
    }
}

/// <summary>An active contract's deadline moves.</summary>
public class AIEffect_ContractShift : AIEventEffect
{
    public float days = -1f;

    public override void Apply(AIEventContext c)
    {
        if (ContractManager.Get()?.Active.TryRandomElement(out Contract contract) == true)
            contract.DeadlineTick = Mathf.Max(Find.TickManager.TicksGame + GenDate.TicksPerHour, contract.DeadlineTick + (int)(days * GenDate.TicksPerDay));
    }
}

/// <summary>A Seed leans further its current way (or back, with a negative step).</summary>
public class AIEffect_SeedGrow : AIEventEffect
{
    public int step = 1;

    public override void Apply(AIEventContext c)
    {
        NetAI ai = c.AI;
        if (ai.Personality != AIPersonality.Seed)
            return;
        var counts = new[] { ai.SeedTowardDataHunter, ai.SeedTowardMuse, ai.SeedTowardPredator, ai.SeedTowardGhost };
        int best = System.Array.IndexOf(counts, counts.Max());
        if (step > 0)
        {
            AIManager.Get()?.GrowSeed(ai, best switch
            {
                0 => AIPersonality.DataHunter,
                1 => AIPersonality.Muse,
                2 => AIPersonality.Predator,
                _ => AIPersonality.Ghost,
            });
            return;
        }
        switch (best)
        {
            case 0: ai.SeedTowardDataHunter = Mathf.Max(0, ai.SeedTowardDataHunter + step); break;
            case 1: ai.SeedTowardMuse = Mathf.Max(0, ai.SeedTowardMuse + step); break;
            case 2: ai.SeedTowardPredator = Mathf.Max(0, ai.SeedTowardPredator + step); break;
            default: ai.SeedTowardGhost = Mathf.Max(0, ai.SeedTowardGhost + step); break;
        }
    }
}

/// <summary>The AI performs its service for free, cooldown untouched.</summary>
public class AIEffect_FreeService : AIEventEffect
{
    public override void Apply(AIEventContext c) => AIBehavior.PerformOnItsOwn(c.AI, c.Map);
}

/// <summary>Progress on the current research project.</summary>
public class AIEffect_Research : AIEventEffect
{
    public float amount = 500f;

    public override void Apply(AIEventContext c)
    {
        ResearchProjectDef project = Find.ResearchManager.GetProject();
        if (project != null)
            Find.ResearchManager.AddProgress(project, amount, null);
    }
}

/// <summary>Cures one chronic condition of the target, if any.</summary>
public class AIEffect_CureChronic : AIEventEffect
{
    public AIEventWho who = AIEventWho.Target;

    public override void Apply(AIEventContext c)
    {
        foreach (Pawn pawn in Pawns(c, who))
        {
            Hediff chronic = pawn.health.hediffSet.hediffs.Where(h => h.def.chronic).RandomElementWithFallback();
            if (chronic != null)
                pawn.health.RemoveHediff(chronic);
        }
    }
}

/// <summary>An art piece from storage is sold online for silver.</summary>
public class AIEffect_SellArt : AIEventEffect
{
    public float valueFactor = 0.8f;

    public static Thing FindArt(Map map)
        => map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
            .FirstOrDefault(t => t.TryGetComp<CompArt>() is { Active: true } && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer));

    public override void Apply(AIEventContext c)
    {
        Thing art = FindArt(c.Map);
        if (art == null)
            return;
        int silver = Mathf.Max(1, (int)(art.MarketValue * art.stackCount * valueFactor));
        string label = art.LabelCap;
        art.Destroy();
        new AIEffect_Items { thing = ThingDefOf.Silver, count = new IntRange(silver, silver) }.Apply(c);
        Messages.Message("NR_AIEventArtSold".Translate(label, silver), MessageTypeDefOf.PositiveEvent, historical: true);
    }
}

/// <summary>A colonist walks to the AI's server (or stays put) and spends the job's expiry with it.</summary>
public class JobDriver_AIEventSession : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        if (job.GetTarget(TargetIndex.A).HasThing)
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

        Toil wait = ToilMaker.MakeToil("AIEventSession");
        wait.defaultCompleteMode = ToilCompleteMode.Never;
        wait.handlingFacing = true;
        wait.tickAction = () =>
        {
            if (job.GetTarget(TargetIndex.A).HasThing)
                pawn.rotationTracker.FaceTarget(job.GetTarget(TargetIndex.A));
        };
        yield return wait;
    }
}
