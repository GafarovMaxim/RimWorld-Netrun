using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// The life of the map (docs/CYBERSPACE_V2.md, section 5): every day or two one event changes one to three elements.
/// Permanent changes (a cut cable, a new node) at most once a season; the last path to Home never breaks.
/// Corporations' captures, retreats and collapse come from their own logic (stage 9).
/// </summary>
public partial class NetMapManager
{
    private static int Now => Find.TickManager.TicksGame;
    private static int Days(float d) => (int)(d * GenDate.TicksPerDay);

    private IEnumerable<MapLink> Backbone => Map.Links.Where(l => l.Kind is LinkKind.Normal or LinkKind.Fast);
    private IEnumerable<MapNode> Playable => Map.Nodes.Where(n => n.District is not (District.Home or District.OldNet or District.Dark or District.Orbit));

    private void Headline(string key, int tone, params NamedArgument[] args) => Gateway?.AddHeadline(key.Translate(args), tone);

    public void RunEvent()
    {
        var events = new List<(System.Action run, float weight)>
        {
            (Maintenance, 3f),
            (Overload, 3f),
            (LocalStorm, 1.5f),
            (Outage, 2f),
            (NewBranch, 2f),
            (TenantsLeave, 1.5f),
            (NewTenants, 1.5f),
            (Patrol, 1.5f),
            (TransitWindow, 1f),
            (TollLink, 1f),
            (Backdoor, 0.6f),
            (DarkLinkFoundEvent, 0.6f),
        };
        if (_lastPermanentTick < 0 || Now - _lastPermanentTick >= Days(Settings.permanentChangeDays))
        {
            events.Add((CableCut, 0.5f));
            events.Add((NewMapNode, 0.5f));
        }
        try
        {
            events.RandomElementByWeight(e => e.weight * NetrunMod.Settings.CyberEventFrequency).run();
        }
        catch (System.Exception e)
        {
            Log.Warning("[Netrun] cyberspace map event failed: " + e.Message);
        }
    }

    /// <summary>Breaking this link for a while keeps every node reachable (the last path to Home never breaks).</summary>
    private bool CanBreak(MapLink l) => !l.Broken && Map.SafeToRemove(l);

    private void Maintenance()
    {
        MapNode center = Playable.RandomElementWithFallback();
        if (center == null)
            return;
        int broken = 0;
        foreach (MapLink l in Map.LinksOf(center.Id).Where(l => l.Kind == LinkKind.Normal).InRandomOrder().Take(Rand.RangeInclusive(1, 2)))
        {
            if (!CanBreak(l))
                continue;
            l.BrokenUntil = Now + Days(1f);
            broken++;
        }
        if (broken > 0)
            Headline("NR_MapNewsMaintenance", 0, NodeLabel(center).Named("NODE"));
    }

    private void Overload()
    {
        List<MapLink> links = Backbone.InRandomOrder().Take(Rand.RangeInclusive(2, 3)).ToList();
        foreach (MapLink l in links)
            l.JamUntil = Now + Days(Rand.Range(1f, 2f));
        if (links.Count > 0)
            Headline("NR_MapNewsOverload", 2, NodeLabel(Map.Node(links[0].A)).Named("NODE"));
    }

    private void LocalStorm()
    {
        MapNode center = Playable.RandomElementWithFallback();
        if (center == null)
            return;
        int until = Now + Days(Rand.Range(1f, 2f));
        center.StormUntil = until;
        foreach (MapLink l in Map.LinksOf(center.Id))
            Map.Node(l.Other(center.Id)).StormUntil = until;
        Headline("NR_MapNewsStorm", 0, NodeLabel(center).Named("NODE"));
    }

    private void Outage()
    {
        MapNode n = Playable.Where(x => !Runners.Any(r => r.NodeId == x.Id || r.Path.Contains(x.Id))).RandomElementWithFallback();
        if (n == null)
            return;
        n.OfflineUntil = Now + Days(Rand.Range(1f, 2f));
        Headline("NR_MapNewsOutage", 2, NodeLabel(n).Named("NODE"));
    }

    private void NewBranch()
    {
        MapNode n = Playable.Where(x => FreeSlots(x) > 0).RandomElementWithFallback();
        if (n == null || Gateway == null)
            return;
        ExternalNode branch = ExternalNode.ForDistrict(Gateway.NextBranchId(), BranchDistrict(n));
        Attach(branch, n);
        branch.Hidden = false;
        Headline("NR_MapNewsNewBranch", 1, branch.Name.Named("BRANCH"), NodeLabel(n).Named("NODE"));
    }

    private void TenantsLeave()
    {
        ExternalNode b = Gateway?.AllBranches.Where(x => x.MapNodeId >= 0 && !x.Deserted && !x.IsBoss && !x.Ancient && !x.CounterHack
            && Gateway.Session?.Node?.Id != x.Id).RandomElementWithFallback();
        if (b == null)
            return;
        b.Deserted = true;
        b.Defense = Mathf.Max(1, b.Defense - 1);
        b.BlackIce = false;
        b.IceStrength = 0;
        b.WildAI |= Rand.Chance(NetrunDefOf.NR_Cyberspace.wildAIChance);
        Headline("NR_MapNewsDeserted", 0, b.Name.Named("BRANCH"));
    }

    private void NewTenants()
    {
        ExternalNode b = Gateway?.AllBranches.Where(x => x.Deserted && Gateway.Session?.Node?.Id != x.Id).RandomElementWithFallback();
        if (b == null)
            return;
        MapNode n = Map.Node(b.MapNodeId);
        ExternalNode fresh = ExternalNode.ForDistrict(b.Id, n?.District ?? b.District);
        // New tenants are often someone else: a market turns into a dark exchange, a deserted server into an outpost.
        if (b.Type == ExternalNodeType.Market && Rand.Chance(0.5f))
            fresh.Type = ExternalNodeType.DarkExchange;
        b.Type = fresh.Type;
        b.Defense = fresh.Defense;
        b.BlackIce = fresh.BlackIce;
        b.IceStrength = fresh.IceStrength;
        b.Deserted = false;
        b.WildAI = false;
        Headline("NR_MapNewsTenants", 0, b.Name.Named("BRANCH"));
    }

    private void Patrol()
    {
        MapNode n = Playable.RandomElementWithFallback();
        if (n == null)
            return;
        n.PatrolUntil = Now + Days(Rand.Range(1f, 3f));
        Headline("NR_MapNewsPatrol", 2, NodeLabel(n).Named("NODE"));
    }

    private void TransitWindow()
    {
        MapNode n = Map.Nodes.Where(x => x.District == District.Transit).RandomElementWithFallback() ?? Playable.RandomElementWithFallback();
        if (n == null)
            return;
        n.TransitUntil = Now + Days(3f);
        Headline("NR_MapNewsTransit", 1, NodeLabel(n).Named("NODE"));
    }

    private void TollLink()
    {
        MapLink l = Backbone.RandomElementWithFallback();
        if (l == null)
            return;
        l.TollUntil = Now + Days(Rand.Range(2f, 4f));
        Headline("NR_MapNewsToll", 2, NodeLabel(Map.Node(l.A)).Named("NODE"));
    }

    private void Backdoor()
    {
        List<MapNode> pool = Playable.ToList();
        if (pool.Count < 4)
            return;
        // A shortcut between hubs at least 3 hops apart, only where it crosses no link (Maxim, 2026-10-22: the farthest
        // hub always made a long crossing line). A few tries, else no event.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            MapNode a = pool.RandomElement();
            foreach (MapNode b in pool.Where(x => x != a && Map.Hops(a.Id, x.Id, 3) >= 3).OrderByDescending(x => Vector2.Distance(a.Pos, x.Pos)))
            {
                if (!Map.TryAddLink(new MapLink { A = a.Id, B = b.Id, Kind = LinkKind.Backdoor, ExpiresTick = Now + Days(Rand.Range(3f, 5f)) }))
                    continue;
                Headline("NR_MapNewsBackdoor", 1, NodeLabel(a).Named("NODE"), NodeLabel(b).Named("OTHER"));
                return;
            }
        }
    }

    /// <summary>A hidden dark link becomes known (also sold by data brokers).</summary>
    public bool DarkLinkFound()
    {
        MapLink l = Map.Links.Where(x => x.Kind == LinkKind.Dark && !x.Known).RandomElementWithFallback();
        if (l == null)
            return false;
        l.Known = true;
        Map.Dirty();
        Headline("NR_MapNewsDarkLink", 1, NodeLabel(Map.Node(l.A)).Named("NODE"));
        return true;
    }

    private void DarkLinkFoundEvent() => DarkLinkFound();

    private void CableCut()
    {
        MapLink cut = Map.Links.Where(l => l.Kind == LinkKind.Normal && Map.SafeToRemove(l)).RandomElementWithFallback();
        if (cut == null)
            return;
        Map.Links.Remove(cut);
        Map.Dirty();
        // The net rebuilds around the break: a new link to the nearest neighbour it can reach without crossing a link.
        MapNode a = Map.Node(cut.A);
        foreach (MapNode b in Playable.Where(x => x.Id != cut.A && x.Id != cut.B).OrderBy(x => Vector2.Distance(a.Pos, x.Pos)).Take(6))
            if (Map.TryAddLink(new MapLink { A = a.Id, B = b.Id, Kind = LinkKind.Normal }))
                break;
        _lastPermanentTick = Now;
        Headline("NR_MapNewsCableCut", 0, NodeLabel(a).Named("NODE"));
    }

    private void NewMapNode()
    {
        MapNode anchor = Map.Nodes.Where(n => n.District is District.Outskirts or District.Market or District.Ruins).RandomElementWithFallback();
        if (anchor == null)
            return;
        Vector2 pos = anchor.Pos;
        for (int i = 0; i < 60; i++)
        {
            Vector2 p = anchor.Pos + Rand.InsideUnitCircle * 0.12f;
            // As much room as the generator keeps, and off every link (2026-10-22: new hubs sat on top of links).
            if (p.x > 0.05f && p.x < 0.95f && p.y > 0.04f && p.y < 0.63f && Map.Nodes.All(n => Vector2.Distance(n.Pos, p) > 0.07f)
                && Map.Links.All(l => CyberMap.DistanceToSegment(p, Map.Node(l.A).Pos, Map.Node(l.B).Pos) > CyberMap.LinkClearance))
            {
                pos = p;
                break;
            }
        }
        if (pos == anchor.Pos)
            return;
        var node = new MapNode { Id = Map.NextNodeId++, District = anchor.District, X = pos.x, Y = pos.y, Slots = Rand.RangeInclusive(1, 2) };
        Map.Nodes.Add(node);
        Map.Dirty();
        // Linked to the nearest hubs it can reach without crossing a link; none: the node does not appear.
        int links = 0;
        foreach (MapNode near in Playable.Where(x => x.Id != node.Id).OrderBy(x => Vector2.Distance(x.Pos, pos)).Take(4))
            if ((links == 0 || Rand.Bool) && Map.TryAddLink(new MapLink { A = near.Id, B = node.Id, Kind = LinkKind.Normal }) && ++links == 2)
                break;
        if (links == 0)
        {
            Map.Nodes.Remove(node);
            Map.Dirty();
            return;
        }
        if (Gateway != null)
            Attach(ExternalNode.ForDistrict(Gateway.NextBranchId(), node.District), node);
        _lastPermanentTick = Now;
        Headline("NR_MapNewsNewNode", 1, NodeLabel(node).Named("NODE"));
    }
}
