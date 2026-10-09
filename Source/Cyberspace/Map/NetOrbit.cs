using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Odyssey (docs/CONCEPTS_10.md, section 9): orbital uplinks, offline unless a colony map is in space (a gravship in
/// orbit with a pod on board); loot there is richer (x1.5) and the ICE harder. Since 2026-10-09 (Maxim): one uplink per
/// corporation, set beside its ground and tied only to its own hubs (the old belt along the top drew long lines across
/// the map).
/// </summary>
public partial class NetMapManager
{
    private static int _orbitCheckTick = -1;
    private static bool _orbitOpen;
    private bool _orbitWasOpen;
    private bool _orbitByCorp;

    /// <summary>A player home map is in space right now (checked at most every 250 ticks).</summary>
    public static bool OrbitOpen
    {
        get
        {
            if (!ModsConfig.OdysseyActive || Current.Game == null)
                return false;
            int now = Find.TickManager.TicksGame;
            if (_orbitCheckTick < 0 || now - _orbitCheckTick >= 250 || now < _orbitCheckTick)
            {
                _orbitCheckTick = now;
                _orbitOpen = Find.Maps.Any(m => m.IsPlayerHome && m.Tile.LayerDef is { isSpace: true });
            }
            return _orbitOpen;
        }
    }

    /// <summary>Adds the orbital nodes once (Odyssey only) and announces when the orbit opens or closes.</summary>
    private void EnsureOrbit()
    {
        if (!ModsConfig.OdysseyActive || !Ready || Gateway == null)
            return;
        if (!_orbitByCorp || !Map.Nodes.Any(n => n.District == District.Orbit))
        {
            PlaceOrbitByCorp();
            _orbitByCorp = true;
            Map.Dirty();
        }
        bool open = OrbitOpen;
        if (open != _orbitWasOpen)
        {
            _orbitWasOpen = open;
            Gateway.AddHeadline((open ? "NR_HeadlineOrbitOpen" : "NR_HeadlineOrbitClosed").Translate(), open ? 1 : 0);
        }
    }

    /// <summary>
    /// One orbital uplink per corporation: beside its ground, pushed away from the map's middle and from other hubs,
    /// linked to its two nearest hubs of that corporation. Existing orbital hubs are reused (runners and branches stay),
    /// missing ones are added, extra ones keep a place next to the nearest corporation.
    /// </summary>
    private void PlaceOrbitByCorp()
    {
        var corps = Map.Nodes.Where(n => n.District == District.Corp && CorpOfSlot(n.CorpSlot) != null)
            .GroupBy(n => n.CorpSlot).Select(g => g.ToList()).ToList();
        if (corps.Count == 0)
            return;
        var orbit = Map.Nodes.Where(n => n.District == District.Orbit).OrderBy(n => n.Id).ToList();
        while (orbit.Count < corps.Count)
        {
            var n = new MapNode { Id = Map.NextNodeId++, District = District.Orbit, Slots = 2 };
            Map.Nodes.Add(n);
            for (int s = 0; s < n.Slots; s++)
                Attach(ExternalNode.ForDistrict(Gateway.NextBranchId(), District.Orbit), n);
            orbit.Add(n);
        }
        var ids = new System.Collections.Generic.HashSet<int>(orbit.Select(n => n.Id));
        Map.Links.RemoveAll(l => ids.Contains(l.A) || ids.Contains(l.B));
        var middle = new Vector2(0.5f, 0.35f);
        for (int i = 0; i < orbit.Count; i++)
        {
            MapNode n = orbit[i];
            System.Collections.Generic.List<MapNode> ground = corps[i % corps.Count];
            Vector2 centre = ground.Aggregate(Vector2.zero, (s, h) => s + h.Pos) / ground.Count;
            Vector2 away = (centre - middle).sqrMagnitude < 0.0001f ? Vector2.down : (centre - middle).normalized;
            Vector2 p = centre + away * (0.1f + 0.03f * (i / corps.Count));
            // Step further out until no other hub is close.
            for (int step = 0; step < 8 && Map.Nodes.Any(o => o != n && Vector2.Distance(o.Pos, p) < 0.06f); step++)
                p += away * 0.03f + new Vector2(away.y, -away.x) * 0.02f;
            p.x = Mathf.Clamp(p.x, 0.03f, 0.97f);
            p.y = Mathf.Clamp(p.y, 0.03f, 0.66f);
            n.X = p.x;
            n.Y = p.y;
            n.CorpSlot = ground[0].CorpSlot;
            foreach (MapNode down in ground.OrderBy(h => Vector2.Distance(h.Pos, p)).Take(2))
                Map.Links.Add(new MapLink { A = n.Id, B = down.Id, Kind = LinkKind.Normal });
        }
    }

    private void ExposeOrbit()
    {
        Scribe_Values.Look(ref _orbitWasOpen, "orbitWasOpen");
        Scribe_Values.Look(ref _orbitByCorp, "orbitByCorp");
    }
}
