using System.Collections.Generic;
using System.Linq;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

public enum IceTier
{
    None,
    Firewall,
    Ice,
    BlackIce,
}

public enum AttackOutcome
{
    Repelled,
    Intrusion,
    Hit,
}

/// <summary>Numbers for the colony's node defense (Defs/Defense.xml).</summary>
public class DefenseSettingsDef : Def
{
    public int firewallDefense = 2;
    public int maxFirewalls = 2;
    public int jammerDefense = 1;
    public int wardenDefense = 2;
    public int wardenTrustBonus = 1;
    public float wardenTrustThreshold = 60f;
    public int dutyIntellectPerPoint = 4;
    public int repelMargin = 3;
    public int honeypotCyberdataMin = 2;
    public int honeypotCyberdataMax = 4;
    public float honeypotRevealChance = 0.4f;
    public float blackIceRepelHeat = 3f;
    public int upkeepDays = 15;
    public int upkeepFirewall = 1;
    public int upkeepIce = 3;
    public int upkeepBlackIceAdvanced = 1;
}

/// <summary>
/// The colony's own node (docs/CONCEPTS_10.md, section 1): firewalls, the colony's ICE tier, honeypot, netrunners
/// on duty, a Warden AI, HE's jammer at a pod, and full network isolation. Network attacks are resolved here.
/// </summary>
public class NodeDefense : GameComponent
{
    /// <summary>Full isolation: no attacks get through, but the Gateway, dives, contracts, AI services and fixer calls are off.</summary>
    public bool Isolated;
    public IceTier Tier;
    /// <summary>thingIDNumber of the firewall in honeypot mode, -1 for none (one per colony).</summary>
    public int HoneypotId = -1;
    /// <summary>thingIDNumbers of cyberpods in "node watch" mode.</summary>
    private int _nextUpkeepTick = -1;

    public NodeDefense(Game game)
    {
    }

    public static NodeDefense Get() => GameComp<NodeDefense>.Get();

    private static DefenseSettingsDef Settings => NetrunDefOf.NR_Defense;

    // ---- Research and tiers ----

    public static IceTier MaxTier()
    {
        if (NetrunDefOf.NR_Res_BlackICEDefense?.IsFinished == true)
            return IceTier.BlackIce;
        if (NetrunDefOf.NR_Res_ActiveICE?.IsFinished == true)
            return IceTier.Ice;
        if (NetrunDefOf.NR_Res_NetSecurity?.IsFinished == true)
            return IceTier.Firewall;
        return IceTier.None;
    }

    public static int TierDefense(IceTier tier) => (int)tier;

    /// <summary>Cyberdata (regular, advanced) a tier costs every upkeep period.</summary>
    public static (int regular, int advanced) Upkeep(IceTier tier) => tier switch
    {
        IceTier.Firewall => (Settings.upkeepFirewall, 0),
        IceTier.Ice => (Settings.upkeepIce, 0),
        IceTier.BlackIce => (0, Settings.upkeepBlackIceAdvanced),
        _ => (0, 0),
    };

    public void SetTier(IceTier tier, Map map)
    {
        if (tier > MaxTier())
            return;
        if (tier > IceTier.None && !Pay(map, tier))
        {
            Messages.Message("NR_DefenseUpkeepShort".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }
        Tier = tier;
        _nextUpkeepTick = Find.TickManager.TicksGame + Settings.upkeepDays * GenDate.TicksPerDay;
    }

    private static bool Pay(Map map, IceTier tier)
    {
        (int regular, int advanced) = Upkeep(tier);
        if (map == null || ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata) < regular || ColonyGoods.Count(map, NetrunDefOf.USH_AdvancedCyberdata) < advanced)
            return false;
        ColonyGoods.TryTake(map, NetrunDefOf.USH_Cyberdata, regular);
        ColonyGoods.TryTake(map, NetrunDefOf.USH_AdvancedCyberdata, advanced);
        return true;
    }

    public override void GameComponentTick()
    {
        int now = Find.TickManager.TicksGame;
        if (now % GenDate.TicksPerHour != 53)
            return;

        if (Tier > IceTier.None && _nextUpkeepTick >= 0 && now >= _nextUpkeepTick)
        {
            Map map = Find.AnyPlayerHomeMap;
            if (Tier > MaxTier() || !Pay(map, Tier))
            {
                Tier = IceTier.None;
                Find.LetterStack.ReceiveLetter("NR_DefenseUpkeepLostLabel".Translate(), "NR_DefenseUpkeepLost".Translate(), LetterDefOf.NegativeEvent);
            }
            _nextUpkeepTick = now + Settings.upkeepDays * GenDate.TicksPerDay;
        }

        if (Isolated)
        {
            // Cut off from the net, every awake AI loses trust as on an isolated server.
            AIManager manager = AIManager.Get();
            if (manager != null)
                foreach (NetAI ai in manager.AIs.Where(a => a.Powered && !a.Isolated))
                    ai.AddTrust(-NetrunDefOf.NR_AI.isolatedTrustPerDay / 24f);
        }
    }

    // ---- Isolation ----

    // D5 "port cooldown" (docs/CONCEPTS_D_2026-10-09.md): no new isolation for 6 game hours after it ends, and a short
    // isolation (under 2 hours) costs trace and Netsick when it is lifted, so it is no free shield against one intrusion.
    private const int CooldownTicks = 6 * GenDate.TicksPerHour;
    private const int ShortIsolationTicks = 2 * GenDate.TicksPerHour;
    private const float ShortIsolationHeat = 4f;
    private const float ShortIsolationNetsick = 0.05f;
    private int _isolatedAt = -1;
    private int _liftedAt = -1;

    /// <summary>Ticks of unbroken isolation so far (0 when the network is open).</summary>
    public int IsolatedTicks => Isolated && _isolatedAt >= 0 ? Find.TickManager.TicksGame - _isolatedAt : 0;

    /// <summary>Ticks until the network can be isolated again (0: now).</summary>
    public int IsolationCooldownLeft
        => !NetrunMod.Settings.IsolationCooldown || Isolated || _liftedAt < 0 ? 0 : System.Math.Max(0, _liftedAt + CooldownTicks - Find.TickManager.TicksGame);

    public AcceptanceReport CanToggleIsolation()
    {
        if (!Isolated && GatewayManager.Get()?.Session is { Finished: false })
            return "NR_IsolationDiveActive".Translate();
        if (!Isolated && IsolationCooldownLeft > 0)
            return "NR_IsolationCooldown".Translate(IsolationCooldownLeft.ToStringTicksToPeriod());
        return true;
    }

    public void ToggleIsolation()
    {
        if (!CanToggleIsolation().Accepted)
            return;
        Isolated = !Isolated;
        int now = Find.TickManager.TicksGame;
        if (Isolated)
        {
            _isolatedAt = now;
            // Cut off: every runner on the cyberspace map is pulled back through a clean exit (no jolt; the dive itself
            // cannot be running, isolation waits for it).
            if (NetMapManager.Get() is { } net)
                foreach (NetRunner r in net.Runners.ToList())
                    net.Disconnect(r, emergency: false);
        }
        else
        {
            if (NetrunMod.Settings.IsolationCooldown && _isolatedAt >= 0 && now - _isolatedAt < ShortIsolationTicks)
                ShortIsolationPenalty();
            _liftedAt = now;
        }
        AIBalance.ResetCache();
        Messages.Message((Isolated ? "NR_IsolationOn" : "NR_IsolationOff").Translate(), MessageTypeDefOf.NeutralEvent, historical: true);
        HeatTracker.Get()?.Note((Isolated ? "NR_IsolationOn" : "NR_IsolationOff").Translate(), HeatSource.Other);
    }

    /// <summary>The links dropped and came back too fast: a trace spike and a jolt for the runners in pods.</summary>
    private static void ShortIsolationPenalty()
    {
        HeatTracker.Get()?.AddHeat(ShortIsolationHeat, "NR_IsolationShortHeat".Translate(), HeatSource.Other);
        foreach (Map map in Find.Maps)
            foreach (Building_Cyberpod pod in CyberpodUtility.PlayerPods(map))
                if (pod.ContainedThing is Pawn p && p.Faction == Faction.OfPlayer)
                    NetsickManager.AddNetsickFor(p, ShortIsolationNetsick);
        Messages.Message("NR_IsolationShort".Translate(ShortIsolationHeat.ToString("0")), MessageTypeDefOf.NegativeEvent, historical: true);
    }

    // ---- Defense value ----

    public static IEnumerable<Building> Firewalls(Map map)
        => NetrunDefOf.NR_Firewall == null || map == null
            ? Enumerable.Empty<Building>()
            : map.listerBuildings.AllBuildingsColonistOfDef(NetrunDefOf.NR_Firewall).Where(b => b.GetComp<CompPowerTrader>()?.PowerOn != false);

    /// <summary>The node's defense and what makes it up (label, value) for tooltips.</summary>
    public int Defense(Map map, List<(string label, int value)> parts = null)
    {
        int total = 0;
        void Add(string label, int value)
        {
            if (value == 0)
                return;
            total += value;
            parts?.Add((label, value));
        }

        List<Building> firewalls = Firewalls(map).Take(Settings.maxFirewalls).ToList();
        int working = firewalls.Count(f => f.thingIDNumber != HoneypotId);
        Add("NR_DefenseFirewalls".Translate(working), working * Settings.firewallDefense);
        if (firewalls.Count > 0 && Tier > IceTier.None)
            Add(("NR_IceTier_" + Tier).Translate(), TierDefense(Tier));

        if (map != null && DefDatabase<ThingDef>.GetNamedSilentFail("USH_JammerCoprocessor") is { } jammer
            && CyberpodUtility.PlayerPods(map).Any(p => p.GetComp<CompAffectedByFacilities>()?.LinkedFacilitiesListForReading.Any(f => f.def == jammer) == true))
            Add(jammer.LabelCap, Settings.jammerDefense);

        AIManager manager = AIManager.Get();
        if (manager != null)
        {
            foreach (NetAI ai in manager.AIs.Where(a => a.Personality == AIPersonality.Warden && a.InServer && a.Powered))
                Add(ai.Name, Settings.wardenDefense + (ai.Trust >= Settings.wardenTrustThreshold ? Settings.wardenTrustBonus : 0) + (ai.Drilled ? 1 : 0));
        }

        if (Guard(map) is { } guard)
            Add("NR_DefenseGuard".Translate(guard.LabelShort), IntellectOf(guard) / Settings.dutyIntellectPerPoint);
        return total;
    }

    private static int IntellectOf(Pawn p) => p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;

    /// <summary>
    /// The node watch (needs the active ICE research): anyone in a powered pod keeps watch, no button needed (Maxim,
    /// 2026-10-16). Only the sharpest one counts, so several pods never stack the bonus.
    /// </summary>
    public Pawn Guard(Map map)
    {
        if (map == null || NetrunDefOf.NR_Res_ActiveICE?.IsFinished != true)
            return null;
        Pawn best = null;
        foreach (Building_Cyberpod pod in CyberpodUtility.PlayerPods(map))
            if (pod.ContainedThing is Pawn p && pod.GetComp<CompPowerTrader>()?.PowerOn != false && (best == null || IntellectOf(p) > IntellectOf(best)))
                best = p;
        return best;
    }

    public string DefenseTip(Map map)
    {
        var parts = new List<(string label, int value)>();
        int total = Defense(map, parts);
        var lines = new List<string> { "NR_DefenseTotal".Translate(total) };
        lines.AddRange(parts.Select(p => $"  {p.label}: +{p.value}"));
        if (Isolated)
            lines.Add("NR_DefenseIsolated".Translate());
        return lines.ToLineList();
    }

    // ---- Attacks ----

    /// <summary>
    /// Strength of an attack against defense: 3+ over strength repels it, within 2 is an intrusion (decided at the node),
    /// 3+ under lets the attack through.
    /// </summary>
    public AttackOutcome Resolve(Map map, int strength, out int defense)
    {
        defense = Defense(map);
        int margin = defense - strength;
        if (margin >= Settings.repelMargin)
            return AttackOutcome.Repelled;
        if (margin <= -Settings.repelMargin)
            return AttackOutcome.Hit;
        return AttackOutcome.Intrusion;
    }

    /// <summary>After a repelled attack: the honeypot's back-trace and Black ICE's noise.</summary>
    public void OnRepelled(Map map, string attackLabel, bool intercepted = false)
    {
        AIQuests.NotifyRepelled(intercepted);
        NetrunSignals.Increment("IntrusionsRepelled");
        if (intercepted)
            NetrunSignals.Increment("Intercepts");
        if (Tier == IceTier.BlackIce)
            HeatTracker.Get()?.AddHeat(Settings.blackIceRepelHeat, "NR_DefenseBlackIceHeat".Translate(attackLabel), HeatSource.Raid);

        if (HoneypotId >= 0 && Firewalls(map).Any(f => f.thingIDNumber == HoneypotId))
        {
            ExternalNode revealed = Rand.Chance(Settings.honeypotRevealChance) ? GatewayManager.Get()?.RevealHidden() : null;
            if (revealed != null)
            {
                Messages.Message("NR_HoneypotReveal".Translate(revealed.Name), MessageTypeDefOf.PositiveEvent, historical: true);
            }
            else if (map != null)
            {
                int count = Rand.RangeInclusive(Settings.honeypotCyberdataMin, Settings.honeypotCyberdataMax);
                Building pot = Firewalls(map).First(f => f.thingIDNumber == HoneypotId);
                Thing data = ThingMaker.MakeThing(NetrunDefOf.USH_Cyberdata);
                data.stackCount = count;
                GenPlace.TryPlaceThing(data, pot.Position, map, ThingPlaceMode.Near);
                Messages.Message("NR_HoneypotLoot".Translate(count), new LookTargets(pot), MessageTypeDefOf.PositiveEvent);
            }
        }

        if (Guard(map) is { } guard && DefDatabase<TaleDef>.GetNamedSilentFail("NR_Tale_RepelledIntrusion") is { } tale)
            TaleRecorder.RecordTale(tale, guard);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref Isolated, "isolated");
        Scribe_Values.Look(ref Tier, "tier");
        Scribe_Values.Look(ref HoneypotId, "honeypotId", -1);
        Scribe_Values.Look(ref _nextUpkeepTick, "nextUpkeepTick", -1);
        Scribe_Values.Look(ref _isolatedAt, "isolatedAt", -1);
        Scribe_Values.Look(ref _liftedAt, "liftedAt", -1);
    }
}

/// <summary>Shortcut for the isolation switch.</summary>
public static class NetworkIsolation
{
    public static bool Active => NodeDefense.Get()?.Isolated == true;

    /// <summary>For the network features isolation shuts off.</summary>
    public static AcceptanceReport Check() => Active ? "NR_IsolationBlocked".Translate() : true;
}
