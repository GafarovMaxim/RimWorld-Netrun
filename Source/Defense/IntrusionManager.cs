using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>An attack walking the map toward Home (docs/CONCEPTS_10.md, section 1.2).</summary>
public class IntrusionPacket : IExposable
{
    public int Id;
    public IncidentDef Attack;
    public int SourceFaction = -1;
    public int NodeId;
    public List<int> Path = [];
    public float Progress;
    public int MapId = -1;

    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id");
        Scribe_Defs.Look(ref Attack, "attack");
        Scribe_Values.Look(ref SourceFaction, "source", -1);
        Scribe_Values.Look(ref NodeId, "node");
        Scribe_Collections.Look(ref Path, "path", LookMode.Value);
        Scribe_Values.Look(ref Progress, "progress");
        Scribe_Values.Look(ref MapId, "map", -1);
        Path ??= [];
    }
}

/// <summary>
/// With the cyberspace map, a network attack no longer appears next to Home: it starts at the attacker's ground and
/// walks the backbone, 2 hours a link (half a runner's speed). A runner can intercept it on the way with a short dive;
/// isolation stops it at the wall; otherwise it strikes Home against the node defense.
/// </summary>
public class IntrusionManager : GameComponent, INetrunCleanable
{
    public const int MaxPackets = 2;
    private const float HoursPerLink = 2f;

    public List<IntrusionPacket> Packets = [];
    private int _nextId = 1;

    /// <summary>Set while a packet strikes, so the attack resolves at once instead of launching a new packet.</summary>
    public static bool Striking;

    public IntrusionManager(Game game)
    {
    }

    public static IntrusionManager Get() => GameComp<IntrusionManager>.Get();

    private static NetMapManager Net => NetMapManager.Get();

    public static bool Active => Net is { Ready: true } && !Striking;

    /// <summary>Launches a packet from the source's ground (a corporation's fortress, else the transit). False if it cannot start.</summary>
    public bool Launch(IncidentDef attack, Map map, Faction source = null)
    {
        if (Net is not { Ready: true } || Packets.Count >= MaxPackets || map == null)
            return false;
        CyberMap cm = Net.Map;
        CorpState corp = source != null ? CorpManager.Get()?.StateOf(source) : CorpManager.Get()?.Corps.OrderByDescending(c => c.Alert).FirstOrDefault();
        MapNode start = corp != null ? cm.Nodes.FirstOrDefault(n => n.Fortress && n.OwnerFaction == corp.FactionId) : null;
        start ??= cm.Nodes.Where(n => n.District == District.Transit).RandomElementWithFallback() ?? cm.Nodes.Where(n => n.District != District.Home).RandomElement();
        List<int> path = cm.Route(start.Id, cm.HomeId, out _);
        if (path.Count == 0)
            return false;
        var packet = new IntrusionPacket
        {
            Id = _nextId++, Attack = attack, SourceFaction = corp?.FactionId ?? source?.loadID ?? -1, NodeId = start.Id, Path = path, MapId = map.uniqueID,
        };
        Packets.Add(packet);
        string from = corp?.Faction?.Name ?? (string)"NR_IntrusionUnknown".Translate();
        string text = "NR_Intrusion".Translate(attack.label, from, NetMapManager.NodeLabel(start), (path.Count * HoursPerLink).ToString("0"));
        if (NewsChoices.Get() is { } choices)
            choices.Offer(NewsChoiceKind.Intrusion, text, packet.Id, -1, path.Count * HoursPerLink / 24f);
        else
            Find.LetterStack.ReceiveLetter("NR_IntrusionLabel".Translate(attack.label), text, LetterDefOf.ThreatSmall);
        return true;
    }

    public override void GameComponentTick()
    {
        if (Packets.Count == 0 || Find.TickManager.TicksGame % 250 != 0)
            return;
        foreach (IntrusionPacket p in Packets.ToList())
        {
            if (NetworkIsolation.Active)
            {
                Packets.Remove(p);
                GatewayManager.Get()?.AddHeadline("NR_IntrusionWall".Translate(p.Attack?.label ?? "?"), 1);
                continue;
            }
            p.Progress += 250f / GenDate.TicksPerHour;
            if (p.Progress < HoursPerLink)
                continue;
            p.Progress = 0f;
            if (p.Path.Count > 0)
            {
                p.NodeId = p.Path[0];
                p.Path.RemoveAt(0);
            }
            if (p.NodeId == Net?.Map?.HomeId || p.Path.Count == 0)
                Strike(p);
        }
    }

    private void Strike(IntrusionPacket p)
    {
        Map map = Find.Maps.FirstOrDefault(m => m.uniqueID == p.MapId) ?? Find.AnyPlayerHomeMap;
        // T1.4: no strike into a fight on the map; the packet waits at Home and tries again in HoursPerLink (Progress is 0).
        if (map != null && map.dangerWatcher.DangerRating != StoryDanger.None)
            return;
        Packets.Remove(p);
        if (map == null || p.Attack?.Worker is not IncidentWorker_NetAttack)
            return;
        IncidentParms parms = StorytellerUtility.DefaultParmsNow(p.Attack.category, map);
        parms.forced = true;
        Striking = true;
        try
        {
            p.Attack.Worker.TryExecute(parms);
        }
        finally
        {
            Striking = false;
        }
    }

    /// <summary>The packet at or next to this node, if any (for the intercept button).</summary>
    public IntrusionPacket At(int nodeId) => Packets.FirstOrDefault(p => p.NodeId == nodeId || (p.Path.Count > 0 && p.Path[0] == nodeId));

    /// <summary>A short dive at the packet's spot: one gate and the attack's spearhead.</summary>
    public ExternalNode OpenIntercept(IntrusionPacket p, int nodeId)
    {
        GatewayManager gateway = GatewayManager.Get();
        if (gateway == null || gateway.AllBranches.Any(b => b.InterceptPacketId == p.Id))
            return gateway?.AllBranches.FirstOrDefault(b => b.InterceptPacketId == p.Id);
        int strength = (p.Attack?.GetModExtension<NetAttackExtension>()?.strength ?? 4) + (int)(HeatTracker.Get()?.Level ?? HeatLevel.Shadow);
        ExternalNode branch = ExternalNode.Generate(gateway.NextBranchId());
        branch.Type = ExternalNodeType.Market;
        branch.Defense = Mathf.Clamp(strength / 2, 1, 5);
        branch.BlackIce = true;
        branch.IceStrength = Mathf.Clamp(strength - 1, 2, 8);
        branch.Hidden = branch.Rival = branch.Honeypot = branch.WildAI = false;
        branch.InterceptPacketId = p.Id;
        branch.ExpireTick = Find.TickManager.TicksGame + 3 * GenDate.TicksPerHour;
        branch.MapNodeId = nodeId;
        branch.District = Net.Map.Node(nodeId)?.District ?? District.Transit;
        gateway.AddBranchRaw(branch);
        return branch;
    }

    /// <summary>The intercept dive succeeded: the attack is stopped on the way.</summary>
    public void Intercepted(int packetId, Pawn runner)
    {
        IntrusionPacket p = Packets.FirstOrDefault(x => x.Id == packetId);
        if (p == null)
            return;
        Packets.Remove(p);
        Map map = Find.Maps.FirstOrDefault(m => m.uniqueID == p.MapId) ?? Find.AnyPlayerHomeMap;
        NodeDefense.Get()?.OnRepelled(map, p.Attack?.label ?? "?", intercepted: true);
        CorpManager.Get()?.NotifyIntercept(p.SourceFaction);
        NetRanking.Get()?.AddColonyFame(runner, NetRanking.Settings.fameIntrusion, "intercept");
        Messages.Message("NR_IntrusionStopped".Translate(p.Attack?.label ?? "?"), MessageTypeDefOf.PositiveEvent, historical: true);
    }

    public int Cleanup(CleanupReason reason) => Packets.RemoveAll(p => p.Attack == null);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref Packets, "packets", LookMode.Deep);
        Scribe_Values.Look(ref _nextId, "nextId", 1);
        Packets ??= [];
    }
}
