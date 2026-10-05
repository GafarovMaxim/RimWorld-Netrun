using System.Collections.Generic;
using System.Linq;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// The Gateway: the external nodes on offer (refreshed every few days), the programs picked for the
/// next dive and the dive in progress, if any.
/// </summary>
public class GatewayManager : GameComponent
{
    private static CyberspaceSettingsDef Settings => NetrunDefOf.NR_Cyberspace;

    private List<ExternalNode> _nodes = [];
    private int _nextRefreshTick = -1;
    private int _nextId = 1;
    public List<DiveProgram> SelectedPrograms = [];
    public DiveSession Session;

    public GatewayManager(Game game)
    {
    }

    public static GatewayManager Get() => Current.Game?.GetComponent<GatewayManager>();

    public IReadOnlyList<ExternalNode> Nodes
    {
        get
        {
            if (_nextRefreshTick < 0 || Find.TickManager.TicksGame >= _nextRefreshTick)
                Refresh();
            return _nodes;
        }
    }

    private void Refresh()
    {
        _nextRefreshTick = Find.TickManager.TicksGame + (int)(Settings.refreshDays * GenDate.TicksPerDay);
        // The node being dived into stays until the dive ends.
        _nodes.RemoveAll(n => Session == null || n.Id != Session.Node.Id);
        int count = Settings.nodeCount.RandomInRange;
        for (int i = 0; i < count; i++)
            _nodes.Add(ExternalNode.Generate(_nextId++));
        foreach (ExternalNode node in _nodes.InRandomOrder().Take(Settings.hiddenNodes))
            node.Hidden = true;
        if (ContractManager.Get()?.Active.Any(c => c.Type is ContractType.SystemTap or ContractType.QuietRun) == true
            && _nodes.Where(n => !n.Hidden).TryRandomElement(out ExternalNode marked))
            marked.Contract = true;
    }

    public void RemoveNode(ExternalNode node) => _nodes.RemoveAll(n => n.Id == node.Id);

    public int TicksUntilRefresh => System.Math.Max(0, _nextRefreshTick - Find.TickManager.TicksGame);

    // ---- The netrunner and the pod ----

    /// <summary>A colonist with a cyberlink lying in a powered cyberpod on this map: the only way in.</summary>
    public static Pawn Runner(Map map, out Building_Cyberpod pod)
    {
        pod = null;
        foreach (Building_Cyberpod each in CyberpodUtility.PlayerPods(map))
        {
            if (each.ContainedThing is Pawn pawn && pawn.IsHacker() && each.GetComp<CompPowerTrader>()?.PowerOn != false)
            {
                pod = each;
                return pawn;
            }
        }
        return null;
    }

    public static int ProgramSlots(Building_Cyberpod pod)
        => Settings.programSlots + (pod != null && CyberpodUtility.CoprocessorAI(pod) != null ? 1 : 0);

    public AcceptanceReport CanDive(Map map, ExternalNode node)
    {
        if (node == null)
            return "NR_GatewayPickNode".Translate();
        if (node.Hidden)
            return "NR_GatewayNodeHidden".Translate();
        if (Runner(map, out _) == null)
            return "NR_GatewayNoPod".Translate();
        int cost = DiveCost(map, node, out _);
        if (ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata) < cost)
            return "NR_AINoFood".Translate(cost, NetrunDefOf.USH_Cyberdata.label);
        return true;
    }

    public void Dive(Map map, ExternalNode node)
    {
        Pawn runner = Runner(map, out _);
        if (runner == null)
            return;
        ColonyGoods.TryTake(map, NetrunDefOf.USH_Cyberdata, DiveCost(map, node, out _));
        Session = DiveSession.Start(node, runner, SelectedPrograms);
        SelectedPrograms = [];
    }

    /// <summary>
    /// Cyberdata a dive costs: the node's entry (base + one per two defense levels) plus the programs,
    /// minus discounts for an AI in the pod's coprocessor and an AI riding in the netrunner. Never below zero.
    /// </summary>
    public int DiveCost(Map map, ExternalNode node, out string breakdown)
    {
        int entry = node == null ? 0 : Settings.diveBaseCost + node.Defense / 2;
        int programs = SelectedPrograms.Count * Settings.programCost;
        Pawn runner = Runner(map, out Building_Cyberpod pod);
        int coprocessor = pod != null && CyberpodUtility.CoprocessorAI(pod) != null ? Settings.coprocessorDiscount : 0;
        int passenger = runner != null && AIManager.Get()?.AIs.Any(a => a.Host == runner) == true ? Settings.passengerDiscount : 0;
        int total = System.Math.Max(0, entry + programs - coprocessor - passenger);

        var lines = new List<string> { "NR_DiveCostEntry".Translate(entry), "NR_DiveCostPrograms".Translate(SelectedPrograms.Count, programs) };
        if (coprocessor > 0)
            lines.Add("NR_DiveCostCoprocessor".Translate(coprocessor));
        if (passenger > 0)
            lines.Add("NR_DiveCostPassenger".Translate(passenger));
        lines.Add("NR_DiveCostTotal".Translate(total));
        breakdown = lines.ToLineList();
        return total;
    }

    /// <summary>The dive window was closed after the end: forget it.</summary>
    public void ClearFinished()
    {
        if (Session is { Finished: true })
            Session = null;
    }

    /// <summary>A dive whose netrunner left the pod (or died) cannot go on.</summary>
    public void ValidateSession()
    {
        if (Session == null || Session.Finished)
            return;
        if (Session.Runner == null || Session.Runner.Dead || !Session.Runner.TryGetHoldingCyberpod(out _))
        {
            Messages.Message("NR_DiveAborted".Translate(), MessageTypeDefOf.NegativeEvent, historical: false);
            Session = null;
        }
    }

    /// <summary>The Gateway map has room for this many circles.</summary>
    public const int MaxNodes = 7;

    /// <summary>"Discover new node": reveals a hidden node, or finds a new one when nothing is hidden.</summary>
    public AcceptanceReport CanSearch(Map map)
    {
        if (!_nodes.Any(n => n.Hidden) && _nodes.Count >= MaxNodes)
            return "NR_GatewayFull".Translate();
        return ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata) >= Settings.searchCost
            ? true
            : "NR_AINoFood".Translate(Settings.searchCost, NetrunDefOf.USH_Cyberdata.label);
    }

    public ExternalNode Search(Map map)
    {
        if (!CanSearch(map).Accepted)
            return null;
        ColonyGoods.TryTake(map, NetrunDefOf.USH_Cyberdata, Settings.searchCost);
        ExternalNode found = _nodes.FirstOrDefault(n => n.Hidden);
        if (found == null)
        {
            found = ExternalNode.Generate(_nextId++);
            _nodes.Add(found);
        }
        found.Hidden = false;
        return found;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref _nodes, "nodes", LookMode.Deep);
        Scribe_Values.Look(ref _nextRefreshTick, "nextRefreshTick", -1);
        Scribe_Values.Look(ref _nextId, "nextId", 1);
        Scribe_Collections.Look(ref SelectedPrograms, "selectedPrograms", LookMode.Value);
        Scribe_Deep.Look(ref Session, "session");
        _nodes ??= [];
        SelectedPrograms ??= [];
    }
}
