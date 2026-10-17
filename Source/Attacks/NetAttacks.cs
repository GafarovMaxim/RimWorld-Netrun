using System.Collections.Generic;
using System.Linq;
using RimWorld;
using USH_HE;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>Per-incident settings: the Heat level it needs before the storyteller may pick it.</summary>
public class NetAttackExtension : DefModExtension
{
    public HeatLevel minLevel = HeatLevel.Noticed;
    /// <summary>Base strength against the node defense; the Heat level (0-4) is added on top.</summary>
    public int strength = 4;
}

/// <summary>
/// Network attacks: storyteller incidents that only fire once the colony is noticed online.
/// A netrunner in a cyberpod and a Warden or Predator AI on the server may stop them.
/// </summary>
public abstract class IncidentWorker_NetAttack : IncidentWorker
{
    private const float PodDefense = 0.4f;
    private const float AIDefense = 0.25f;
    private const float MaxDefense = 0.75f;

    protected HeatLevel MinLevel => def.GetModExtension<NetAttackExtension>()?.minLevel ?? HeatLevel.Noticed;

    protected override bool CanFireNowSub(IncidentParms parms)
    {
        if (parms.target is not Map map || !map.IsPlayerHome)
            return false;
        HeatTracker tracker = HeatTracker.Get();
        // A cut-off network cannot be attacked (docs/CONCEPTS_10.md, section 1).
        return tracker != null && !NetworkIsolation.Active && tracker.Level >= MinLevel && CanHit(map);
    }

    /// <summary>Is there anything to hit at all.</summary>
    protected virtual bool CanHit(Map map) => true;

    protected override bool TryExecuteWorker(IncidentParms parms)
    {
        var map = (Map)parms.target;
        if (!CanHit(map))
            return false;

        // With the cyberspace map the attack walks the backbone first (docs/CONCEPTS_10.md, section 1.2).
        if (IntrusionManager.Active && IntrusionManager.Get()?.Launch(def, map) == true)
            return true;

        if (Repelled(map))
            return true;

        bool hit = Hit(map);
        if (hit)
            HeatTracker.Get()?.Note("NR_LogNetAttack".Translate(def.LabelCap), HeatSource.Raid);
        return hit;
    }

    protected abstract bool Hit(Map map);

    /// <summary>Dev: the hit itself, past Heat, isolation, intrusions and the node defense; false if there is nothing to hit.</summary>
    public bool DevHit(Map map) => CanHit(map) && Hit(map);

    /// <summary>Attack strength: the type's base plus the Heat level (0-4).</summary>
    public int Strength => (def.GetModExtension<NetAttackExtension>()?.strength ?? 4) + (int)(HeatTracker.Get()?.Level ?? HeatLevel.Shadow);

    /// <summary>
    /// Strength against the node defense (docs/CONCEPTS_10.md, section 1): well over it repels, well under lets it
    /// through. Close to it is an intrusion, fought at the node: for now a roll that netrunners in pods, guards and
    /// Warden or Predator AIs improve (intrusions along the cyberspace map come with the map).
    /// </summary>
    private bool Repelled(Map map)
    {
        NodeDefense node = NodeDefense.Get();
        AttackOutcome outcome = node?.Resolve(map, Strength, out int defense) ?? AttackOutcome.Intrusion;
        defense = node?.Defense(map) ?? 0;
        string defender = null;
        bool repelled = outcome switch
        {
            AttackOutcome.Repelled => true,
            AttackOutcome.Hit => false,
            _ => Rand.Chance(Mathf.Clamp01(IntrusionBase + IntrusionPerPoint * (defense - Strength) + DefenseChance(map, out defender)
                + (NetMapManager.Get() is { Ready: true } net && net.RunnerAt(net.Map.HomeId) != null ? HomeRunnerBonus : 0f))),
        };
        if (!repelled)
            return false;

        string by = outcome == AttackOutcome.Repelled || defender.NullOrEmpty() ? "NR_AttackByDefense".Translate(defense) : (TaggedString)defender;
        Messages.Message("NR_AttackBlocked".Translate(def.label, by), MessageTypeDefOf.PositiveEvent, historical: true);
        HeatTracker.Get()?.Note("NR_LogNetAttackBlocked".Translate(def.LabelCap, by), HeatSource.Raid);
        node?.OnRepelled(map, def.label);
        return true;
    }

    private const float IntrusionBase = 0.3f;
    /// <summary>A runner standing at Home when the intrusion arrives fights it at the node.</summary>
    private const float HomeRunnerBonus = 0.4f;
    private const float IntrusionPerPoint = 0.1f;

    public static float DefenseChance(Map map, out string defender)
    {
        float chance = 0f;
        var names = new List<string>();

        Pawn runner = CyberpodUtility.PlayerPods(map).Select(p => p.ContainedThing as Pawn).FirstOrDefault(p => p != null);
        if (runner != null)
        {
            chance += PodDefense;
            names.Add(runner.LabelShort);
        }

        AIManager manager = AIManager.Get();
        if (manager != null)
        {
            // Guards and hunters, and any paranoid AI watching the lines.
            foreach (NetAI ai in manager.AIs.Where(a => a.InMainServer && a.Active
                         && (a.Personality == AIPersonality.Warden || a.Personality == AIPersonality.Predator || a.Has(AITrait.Paranoid))))
            {
                chance += AIDefense;
                names.Add(ai.Name);
            }
        }

        float eventBonus = AIEventManager.Get()?.DefenseBonus ?? 0f;
        if (eventBonus > 0f)
        {
            chance += eventBonus;
            names.Add("NR_AIEventDefenseName".Translate());
        }

        defender = names.ToCommaList();
        return System.Math.Min(chance, MaxDefense);
    }

    protected void Letter(Map map, string text, LookTargets look = null)
        => Find.LetterStack.ReceiveLetter(def.LabelCap, text, LetterDefOf.NegativeEvent, look ?? LookTargets.Invalid);
}

/// <summary>Malware trips the breakers: a few powered machines break down.</summary>
public class IncidentWorker_NetBreakdown : IncidentWorker_NetAttack
{
    public static List<CompBreakdownable> Targets(Map map)
        => map.listerBuildings.allBuildingsColonist
            .Select(b => b.GetComp<CompBreakdownable>())
            .Where(c => c != null && !c.BrokenDown && c.parent.GetComp<CompPowerTrader>()?.PowerOn == true)
            .ToList();

    protected override bool CanHit(Map map) => Targets(map).Count > 0;

    protected override bool Hit(Map map) => BreakSome(map, Rand.RangeInclusive(1, 3), def);

    public static bool BreakSome(Map map, int count, IncidentDef def)
    {
        List<CompBreakdownable> broken = Targets(map).InRandomOrder().Take(count).ToList();
        if (broken.Count == 0)
            return false;
        foreach (CompBreakdownable comp in broken)
            comp.DoBreakdown();

        string list = broken.Select(c => c.parent.LabelShort).ToLineList("  - ");
        Find.LetterStack.ReceiveLetter(def.LabelCap, "NR_AttackBreakdown".Translate(list), LetterDefOf.NegativeEvent,
            new LookTargets(broken.Select(c => c.parent)));
        return true;
    }
}

/// <summary>Neural noise floods every cyberlink in the colony.</summary>
public class IncidentWorker_NetNeuralNoise : IncidentWorker_NetAttack
{
    private static List<Pawn> Victims(Map map) => map.mapPawns.FreeColonistsSpawned.Where(p => p.IsHacker()).ToList();

    protected override bool CanHit(Map map) => Victims(map).Count > 0;

    protected override bool Hit(Map map)
    {
        List<Pawn> victims = Victims(map);
        foreach (Pawn pawn in victims)
            pawn.health.AddHediff(NetrunDefOf.NR_NeuralNoise, pawn.health.hediffSet.GetBrain());

        Letter(map, "NR_AttackNeuralNoise".Translate(victims.Select(p => p.LabelShort).ToCommaList()), new LookTargets(victims));
        return true;
    }
}

/// <summary>A power surge empties the batteries and locks the grid down: no electricity for 1.5 days (Maxim, 2026-10-17).</summary>
public class IncidentWorker_NetPowerSurge : IncidentWorker_NetAttack
{
    private const float LockdownDays = 1.5f;

    private static List<CompPowerBattery> Batteries(Map map)
        => map.listerBuildings.allBuildingsColonist.Select(b => b.GetComp<CompPowerBattery>())
            .Where(b => b != null && b.StoredEnergy > 1f).ToList();

    protected override bool CanHit(Map map) => map.powerNetManager.AllNetsListForReading.Count > 0;

    protected override bool Hit(Map map)
    {
        List<CompPowerBattery> batteries = Batteries(map);
        foreach (CompPowerBattery battery in batteries)
            battery.DrawPower(battery.StoredEnergy);

        int ticks = (int)(LockdownDays * GenDate.TicksPerDay);
        if (map.gameConditionManager.GetActiveCondition(NetrunDefOf.NR_GridLockdown) is { } active)
            active.TicksLeft = System.Math.Max(active.TicksLeft, ticks);
        else
            map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(NetrunDefOf.NR_GridLockdown, ticks));

        Letter(map, "NR_AttackPowerSurge".Translate(batteries.Count, LockdownDays.ToString("0.#")), new LookTargets(batteries.Select(b => b.parent)));
        return true;
    }
}

/// <summary>Someone copies the colony's data off its own storage.</summary>
public class IncidentWorker_NetDataTheft : IncidentWorker_NetAttack
{
    private static readonly FloatRange StolenShare = new(0.2f, 0.5f);

    protected override bool CanHit(Map map)
        => ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata) + ColonyGoods.Count(map, NetrunDefOf.USH_AdvancedCyberdata) > 0;

    protected override bool Hit(Map map)
    {
        float share = StolenShare.RandomInRange;
        int basic = Steal(map, NetrunDefOf.USH_Cyberdata, share);
        int advanced = Steal(map, NetrunDefOf.USH_AdvancedCyberdata, share);
        if (basic + advanced == 0)
            return false;

        Letter(map, "NR_AttackDataTheft".Translate(basic, advanced));
        return true;
    }

    private static int Steal(Map map, ThingDef def, float share)
    {
        int count = ColonyGoods.Count(map, def);
        int stolen = count == 0 ? 0 : System.Math.Max(1, (int)(count * share));
        return stolen > 0 && ColonyGoods.TryTake(map, def, stolen) ? stolen : 0;
    }
}

/// <summary>Ransomware: pay silver now or lose machines.</summary>
public class IncidentWorker_NetRansom : IncidentWorker_NetAttack
{
    private const int SilverPerLevel = 150;
    private const int BreakdownsOnRefusal = 4;

    protected override bool CanHit(Map map) => IncidentWorker_NetBreakdown.Targets(map).Count > 0;

    protected override bool Hit(Map map)
    {
        HeatLevel level = HeatTracker.Get()?.Level ?? HeatLevel.Target;
        int price = SilverPerLevel * ((int)level + 1);
        bool canPay = ColonyGoods.Count(map, ThingDefOf.Silver) >= price;

        var node = new DiaNode("NR_AttackRansom".Translate(price));
        var pay = new DiaOption("NR_AttackRansomPay".Translate(price))
        {
            action = () =>
            {
                ColonyGoods.TryTake(map, ThingDefOf.Silver, price);
                Messages.Message("NR_AttackRansomPaid".Translate(price), MessageTypeDefOf.NeutralEvent, historical: true);
            },
            resolveTree = true,
        };
        if (!canPay)
            pay.Disable("NR_AttackRansomNoSilver".Translate());
        node.options.Add(pay);
        node.options.Add(new DiaOption("NR_AttackRansomRefuse".Translate())
        {
            action = () => IncidentWorker_NetBreakdown.BreakSome(map, BreakdownsOnRefusal, def),
            resolveTree = true,
        });

        Find.WindowStack.Add(new Dialog_NodeTree(node, delayInteractivity: true, title: def.LabelCap));
        return true;
    }
}
