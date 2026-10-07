using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>Numbers for the corporations (Defs/Corporations.xml).</summary>
public class CorpSettingsDef : Def
{
    public float startStrength = 50f;
    public float newcomerStrength = 40f;
    public float alertDecayPerDay = 2f;
    public float strengthGrowthPerDay = 1f;
    public float alertPerBranch = 3f;
    public float alertPerFortress = 10f;
    public float alertPerCore = 25f;
    public float alertPerIntercept = 5f;
    public float strengthLossFortress = 10f;
    public float strengthLossCore = 30f;
    public float hardenAlert = 40f;
    public float patrolAlert = 60f;
    public float intrusionAlert = 80f;
    public IntRange intrusionIntervalDays = new(10, 15);
    public float mercChanceAtIntrusion = 0.3f;
    public float captureStrength = 70f;
    public float captureMaxAlert = 30f;
    public float maxCorpShare = 0.45f;
    public float collapseStrength = 15f;
    public int collapseLowDays = 10;
    public int collapseCoreWindowDays = 30;
    public float collapseChance = 0.5f;
    public int collapseCooldownDays = 60;
    public IntRange replacementDays = new(15, 30);
    public int ruinsDays = 15;
    public float goodwillBase = -30f;
    public float goodwillPerAlert = 0.7f;
    public float traceAlert40 = 0.5f;
    public float traceAlert80 = 1f;
}

/// <summary>One corporation's state in the net.</summary>
public class CorpState : IExposable
{
    public int FactionId;
    public int Slot;
    public float Alert;
    public float Strength;
    public int LowSinceTick = -1;
    public List<int> CoreTakenTicks = [];
    public int NextIntrusionTick = -1;
    /// <summary>Was the last blow from the colony (for its fame).</summary>
    public bool PlayerLastBlow;

    public Faction Faction => Find.FactionManager.AllFactions.FirstOrDefault(f => f.loadID == FactionId);

    public string Status
    {
        get
        {
            CorpSettingsDef s = CorpManager.Settings;
            if (GatewayManager.Get()?.LockdownActive == true)
                return "Lockdown";
            if (Strength < s.collapseStrength)
                return "Brink";
            if (Alert >= s.intrusionAlert)
                return "Hunting";
            if (Alert >= s.hardenAlert)
                return "Wary";
            if (Strength >= s.captureStrength)
                return "Expanding";
            return "Calm";
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref FactionId, "faction");
        Scribe_Values.Look(ref Slot, "slot");
        Scribe_Values.Look(ref Alert, "alert");
        Scribe_Values.Look(ref Strength, "strength");
        Scribe_Values.Look(ref LowSinceTick, "lowSince", -1);
        Scribe_Collections.Look(ref CoreTakenTicks, "coreTaken", LookMode.Value);
        Scribe_Values.Look(ref NextIntrusionTick, "nextIntrusion", -1);
        Scribe_Values.Look(ref PlayerLastBlow, "playerLastBlow");
        CoreTakenTicks ??= [];
    }
}

/// <summary>
/// Corporations with logic (docs/CONCEPTS_10.md, section 11): Alert and Strength per corporation, a district on the map,
/// hardening, patrols, intrusions and raids as the Alert climbs, captures when strong and calm, and a rare collapse
/// followed by a newcomer. Everything happens on events and once a day.
/// </summary>
public class CorpManager : GameComponent, INetrunCleanable
{
    public List<CorpState> Corps = [];
    private int _lastCollapseTick = -1;
    /// <summary>Districts waiting for a new corporation: slot and tick.</summary>
    private List<int> _pendingSlot = [];
    private List<int> _pendingTick = [];

    public CorpManager(Game game)
    {
    }

    public static CorpManager Get() => Current.Game?.GetComponent<CorpManager>();

    public static CorpSettingsDef Settings => NetrunDefOf.NR_Corporations;

    public int PendingReplacements => _pendingSlot.Count;

    private static NetMapManager Net => NetMapManager.Get();
    private static int Now => Find.TickManager.TicksGame;

    public CorpState StateOf(Faction f) => f == null ? null : Corps.FirstOrDefault(c => c.FactionId == f.loadID);

    public CorpState StateOfSlot(int slot) => Corps.FirstOrDefault(c => c.Slot == slot && c.Faction is { defeated: false });

    public Faction CorpOfSlot(int slot) => StateOfSlot(slot)?.Faction;

    /// <summary>The corporation owning the node a branch hangs on.</summary>
    public CorpState OwnerOf(ExternalNode branch)
    {
        MapNode n = Net?.NodeOf(branch);
        return n == null || n.OwnerFaction < 0 ? null : Corps.FirstOrDefault(c => c.FactionId == n.OwnerFaction);
    }

    public override void StartedNewGame() => Sync();

    public override void LoadedGame()
    {
        NetMapManager.CorpTraceHook = CorpTrace;
        Sync();
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        NetMapManager.CorpTraceHook = CorpTrace;
    }

    /// <summary>Corporations get a state and a district; their district's nodes become theirs.</summary>
    public void Sync()
    {
        NetMapManager.CorpTraceHook = CorpTrace;
        NetFactions.Get()?.Ensure();
        Corps.RemoveAll(c => c.Faction == null);
        List<int> slots = Net is { Ready: true } ? Net.Map.Nodes.Where(n => n.District == District.Corp).Select(n => n.CorpSlot).Distinct().OrderBy(s => s).ToList() : [0, 1, 2];
        foreach (Faction f in NetFactions.Corps.OrderBy(f => f.loadID))
        {
            if (StateOf(f) != null)
                continue;
            int slot = slots.Where(s => StateOfSlot(s) == null && !_pendingSlot.Contains(s)).DefaultIfEmpty(-1).First();
            if (slot < 0)
                slot = slots.Where(s => StateOfSlot(s) == null).DefaultIfEmpty(-1).First();
            if (slot < 0)
                continue;
            _pendingSlot.Remove(slot);
            Corps.Add(new CorpState { FactionId = f.loadID, Slot = slot, Strength = Settings.startStrength, NextIntrusionTick = -1 });
        }
        ClaimDistricts();
    }

    private void ClaimDistricts()
    {
        if (Net is not { Ready: true })
            return;
        foreach (MapNode n in Net.Map.Nodes.Where(n => n.District == District.Corp))
        {
            CorpState c = StateOfSlot(n.CorpSlot);
            if (c != null && n.OwnerFaction < 0)
                n.OwnerFaction = c.FactionId;
        }
    }

    // ---- Events from the colony ----

    /// <summary>A clean dive into a corporate branch: Alert up, Strength down for the big ones.</summary>
    public void NotifyDive(ExternalNode branch, bool success)
    {
        if (!success)
            return;
        CorpState c = OwnerOf(branch);
        if (branch.IsBoss)
            c ??= Corps.Where(x => x.Faction is { defeated: false }).RandomElementWithFallback();
        if (c == null)
            return;
        MapNode node = Net?.NodeOf(branch);
        if (branch.IsBoss)
        {
            c.Alert += Settings.alertPerCore;
            c.Strength -= Settings.strengthLossCore;
            c.CoreTakenTicks.Add(Now);
            c.PlayerLastBlow = true;
            LoseBorderNodes(c, Rand.RangeInclusive(1, 2));
        }
        else if (node is { Fortress: true })
        {
            c.Alert += Settings.alertPerFortress;
            c.Strength -= Settings.strengthLossFortress;
            c.PlayerLastBlow = true;
        }
        else
        {
            c.Alert += Settings.alertPerBranch;
        }
        Clamp(c);
    }

    public void NotifyIntercept(int factionId)
    {
        CorpState c = Corps.FirstOrDefault(x => x.FactionId == factionId);
        if (c == null)
            return;
        c.Alert += Settings.alertPerIntercept;
        Clamp(c);
    }

    /// <summary>A rival crew hit this corporation: the corporation turns to them for a while.</summary>
    public void NotifyRivalHit(CorpState c)
    {
        c.Strength -= 5f;
        c.PlayerLastBlow = false;
        if (Rand.Chance(0.4f))
            c.Alert -= 5f;
        Clamp(c);
    }

    private static void Clamp(CorpState c)
    {
        c.Alert = Mathf.Clamp(c.Alert, 0f, 100f);
        c.Strength = Mathf.Clamp(c.Strength, 0f, 100f);
    }

    /// <summary>Trace for a hop through a corporation's ground (docs/CYBERSPACE_V2.md, section 3).</summary>
    private float CorpTrace(MapNode node)
    {
        if (node.OwnerFaction < 0)
            return 0f;
        CorpState c = Corps.FirstOrDefault(x => x.FactionId == node.OwnerFaction);
        if (c == null)
            return 0f;
        return c.Alert >= Settings.intrusionAlert ? Settings.traceAlert80 : c.Alert >= Settings.hardenAlert ? Settings.traceAlert40 : 0f;
    }

    // ---- Daily life ----

    public override void GameComponentTick()
    {
        if (Now % GenDate.TicksPerDay != 2213)
            return;
        try
        {
            Daily();
        }
        catch (System.Exception e)
        {
            Log.Warning("[Netrun] corporations' day failed: " + e.Message);
        }
    }

    private void Daily()
    {
        Sync();
        GatewayManager gateway = GatewayManager.Get();
        int rank = NetRanking.Get()?.BestColonyPlace ?? 999;
        float weeklyFame = rank == 1 ? 10f : rank <= 3 ? 5f : 0f;

        foreach (CorpState c in Corps.ToList())
        {
            Faction f = c.Faction;
            if (f == null || f.defeated)
                continue;
            c.Alert -= Settings.alertDecayPerDay;
            c.Alert += weeklyFame / 7f;
            if (c.Strength < Settings.captureStrength)
                c.Strength += Settings.strengthGrowthPerDay;
            Clamp(c);

            SyncGoodwill(c, f);
            Harden(c);
            if (c.Alert >= Settings.patrolAlert)
                PatrolOwnGround(c);
            if (c.Alert >= Settings.intrusionAlert)
                MaybeIntrude(c, f);
            if (c.Strength >= Settings.captureStrength && c.Alert < Settings.captureMaxAlert)
                TryCapture(c, f, gateway);

            if (c.Strength < Settings.collapseStrength)
            {
                if (c.LowSinceTick < 0)
                    c.LowSinceTick = Now;
            }
            else
            {
                c.LowSinceTick = -1;
            }
            c.CoreTakenTicks.RemoveAll(t => Now - t > Settings.collapseCoreWindowDays * GenDate.TicksPerDay);
            TryCollapse(c, f, gateway);
        }

        // A newcomer moves into a free district.
        for (int i = _pendingTick.Count - 1; i >= 0; i--)
        {
            if (Now < _pendingTick[i])
                continue;
            int slot = _pendingSlot[i];
            _pendingTick.RemoveAt(i);
            _pendingSlot.RemoveAt(i);
            NetFactions.Get()?.Ensure();
            Sync();
            CorpState fresh = StateOfSlot(slot);
            if (fresh != null)
            {
                fresh.Strength = Settings.newcomerStrength;
                fresh.Alert = 0f;
                gateway?.AddHeadline("NR_CorpNewsNewcomer".Translate(fresh.Faction.Name), 0);
            }
        }
    }

    /// <summary>The corporation's goodwill follows its Alert: at high Alert it turns hostile and may raid.</summary>
    private static void SyncGoodwill(CorpState c, Faction f)
    {
        if (Faction.OfPlayer == null)
            return;
        int target = Mathf.RoundToInt(Settings.goodwillBase - c.Alert * Settings.goodwillPerAlert);
        int delta = target - f.PlayerGoodwill;
        if (delta != 0)
            f.TryAffectGoodwillWith(Faction.OfPlayer, delta, canSendMessage: false, canSendHostilityLetter: delta < 0);
    }

    private void Harden(CorpState c)
    {
        bool should = c.Alert >= Settings.hardenAlert;
        bool changed = false;
        foreach (ExternalNode b in GatewayManager.Get()?.AllBranches.Where(b => OwnerOf(b) == c).ToList() ?? [])
        {
            if (should && !b.AlertHardened)
            {
                b.AlertHardened = true;
                b.Defense++;
                changed = true;
            }
            else if (!should && b.AlertHardened)
            {
                b.AlertHardened = false;
                b.Defense = Mathf.Max(1, b.Defense - 1);
            }
        }
        if (changed)
            GatewayManager.Get()?.AddHeadline("NR_CorpNewsHarden".Translate(c.Faction.Name), 2);
    }

    private void PatrolOwnGround(CorpState c)
    {
        MapNode n = Net?.Map?.Nodes.Where(x => x.OwnerFaction == c.FactionId).RandomElementWithFallback();
        if (n != null)
            n.PatrolUntil = Mathf.Max(n.PatrolUntil, Now + GenDate.TicksPerDay);
    }

    private void MaybeIntrude(CorpState c, Faction f)
    {
        if (c.NextIntrusionTick < 0)
        {
            c.NextIntrusionTick = Now + Settings.intrusionIntervalDays.RandomInRange * GenDate.TicksPerDay;
            return;
        }
        if (Now < c.NextIntrusionTick)
            return;
        c.NextIntrusionTick = Now + Settings.intrusionIntervalDays.RandomInRange * GenDate.TicksPerDay;
        Map map = Find.AnyPlayerHomeMap;
        if (map == null || NetworkIsolation.Active)
            return;
        IncidentDef attack = DefDatabase<IncidentDef>.AllDefs.Where(d => d.Worker is IncidentWorker_NetAttack).RandomElementWithFallback();
        if (attack != null)
            IntrusionManager.Get()?.Launch(attack, map, f);
        if (Rand.Chance(Settings.mercChanceAtIntrusion))
            Mercenaries.TryQueueRevenge(1f, "NR_MercReasonCorp", f.Name);
    }

    /// <summary>A strong, calm corporation takes a neighboring neutral node (never Outskirts, Transit, Old Net), up to 45% of the map.</summary>
    private void TryCapture(CorpState c, Faction f, GatewayManager gateway)
    {
        CyberMap map = Net?.Map;
        if (map == null || !Rand.Chance(0.3f))
            return;
        var main = map.Nodes.Where(n => n.District is not (District.Home or District.Dark or District.OldNet)).ToList();
        if (main.Count(n => n.OwnerFaction >= 0) + 1 > main.Count * Settings.maxCorpShare)
            return;
        MapNode target = main.Where(n => n.OwnerFaction < 0 && n.District is not (District.Outskirts or District.Transit)
                && map.LinksOf(n.Id).Any(l => map.Node(l.Other(n.Id))?.OwnerFaction == c.FactionId)).RandomElementWithFallback();
        if (target == null)
            return;
        target.OwnerFaction = c.FactionId;
        gateway?.AddHeadline("NR_CorpNewsCapture".Translate(f.Name, NetMapManager.NodeLabel(target)), 2);
    }

    private void LoseBorderNodes(CorpState c, int count)
    {
        CyberMap map = Net?.Map;
        if (map == null)
            return;
        foreach (MapNode n in map.Nodes.Where(n => n.OwnerFaction == c.FactionId && !n.Fortress && n.District != District.Corp).Take(count).ToList())
            n.OwnerFaction = -1;
    }

    private void TryCollapse(CorpState c, Faction f, GatewayManager gateway)
    {
        bool weak = c.LowSinceTick >= 0 && Now - c.LowSinceTick > Settings.collapseLowDays * GenDate.TicksPerDay;
        bool broken = c.CoreTakenTicks.Count >= 2;
        if (!weak && !broken)
            return;
        if (_lastCollapseTick >= 0 && Now - _lastCollapseTick < GenDate.TicksPerYear)
            return;
        if (!Rand.Chance(Settings.collapseChance))
        {
            // Spared this time: it limps on.
            c.LowSinceTick = Now;
            c.CoreTakenTicks.Clear();
            gateway?.AddHeadline("NR_CorpNewsAssets".Translate(f.Name), 1);
            return;
        }
        Collapse(c, f, gateway);
    }

    public void Collapse(CorpState c, Faction f, GatewayManager gateway)
    {
        _lastCollapseTick = Now;
        gateway?.AddHeadline("NR_CorpNewsCollapse".Translate(f.Name), 1);
        Find.LetterStack.ReceiveLetter("NR_CorpCollapseLabel".Translate(f.Name), "NR_CorpCollapse".Translate(f.Name, Settings.ruinsDays), LetterDefOf.PositiveEvent);
        CyberMap map = Net?.Map;
        if (map != null)
        {
            foreach (MapNode n in map.Nodes.Where(n => n.OwnerFaction == c.FactionId))
            {
                n.OwnerFaction = -1;
                if (!n.Fortress)
                    continue;
                // The fortress becomes ruins for a while: deserted branches, richer loot, rivals flock in.
                foreach (ExternalNode b in Net.BranchesAt(n.Id).ToList())
                {
                    b.Deserted = true;
                    b.BlackIce = false;
                    b.IceStrength = 0;
                    b.Defense = Mathf.Max(2, b.Defense - 1);
                    b.Rival = true;
                }
            }
        }
        if (c.PlayerLastBlow)
            NetRanking.Get()?.AddColonyFame(null, NetRanking.Settings.fameCollapse, "NR_FameCollapse".Translate(f.Name));
        f.defeated = true;
        Corps.Remove(c);
        _pendingSlot.Add(c.Slot);
        _pendingTick.Add(Now + Settings.replacementDays.RandomInRange * GenDate.TicksPerDay);
    }

    public int Cleanup(CleanupReason reason) => Corps.RemoveAll(c => c.Faction == null);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref Corps, "corps", LookMode.Deep);
        Scribe_Values.Look(ref _lastCollapseTick, "lastCollapse", -1);
        Scribe_Collections.Look(ref _pendingSlot, "pendingSlot", LookMode.Value);
        Scribe_Collections.Look(ref _pendingTick, "pendingTick", LookMode.Value);
        Corps ??= [];
        _pendingSlot ??= [];
        _pendingTick ??= [];
    }
}

/// <summary>A hostile corporation sends its own guards (Alert 80+).</summary>
public class IncidentWorker_CorpRaid : IncidentWorker_RaidEnemy
{
    private static Faction Pick() => CorpManager.Get()?.Corps.Where(c => c.Alert >= CorpManager.Settings.intrusionAlert && c.Faction is { defeated: false } f && f.HostileTo(Faction.OfPlayer))
        .Select(c => c.Faction).RandomElementWithFallback();

    protected override bool CanFireNowSub(IncidentParms parms) => Pick() != null && base.CanFireNowSub(parms);

    protected override bool TryResolveRaidFaction(IncidentParms parms)
    {
        parms.faction ??= Pick();
        return parms.faction != null;
    }
}
