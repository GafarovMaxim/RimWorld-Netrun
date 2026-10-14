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
    private int _orbitVersion;

    /// <summary>A player home map is in space right now (checked at most every 250 ticks).</summary>
    /// <summary>Dev: the orbit counts as reached without a gravship in space (not saved).</summary>
    public static bool DevForceOrbit;

    public static bool OrbitOpen
    {
        get
        {
            if (DevForceOrbit)
                return true;
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
        // Version 2 (2026-10-14): placed on free ground with one clear link; older saves are laid out again once.
        if (!_orbitByCorp || _orbitVersion < 2 || !Map.Nodes.Any(n => n.District == District.Orbit))
        {
            PlaceOrbitByCorp();
            _orbitByCorp = true;
            _orbitVersion = 2;
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
            // Candidates on rings around the corporation, the outward side first: the first spot inside the map with
            // room around it (Maxim, 2026-10-14: orbital hubs sat on other hubs and pulled long odd links).
            Vector2 p = centre + away * 0.12f;
            bool found = false;
            for (int ring = 0; ring < 5 && !found; ring++)
            {
                float radius = 0.11f + ring * 0.035f;
                for (int k = 0; k < 12 && !found; k++)
                {
                    float turn = (k % 2 == 0 ? 1f : -1f) * (k + 1) / 2 * 25f * Mathf.Deg2Rad;
                    Vector2 dir = new(away.x * Mathf.Cos(turn) - away.y * Mathf.Sin(turn), away.x * Mathf.Sin(turn) + away.y * Mathf.Cos(turn));
                    Vector2 c = centre + dir * radius;
                    if (c.x < 0.04f || c.x > 0.96f || c.y < 0.04f || c.y > 0.66f)
                        continue;
                    if (Map.Nodes.Any(o => o != n && o.District != District.Orbit && Vector2.Distance(o.Pos, c) < 0.085f)
                        || orbit.Take(i).Any(o => Vector2.Distance(o.Pos, c) < 0.085f))
                        continue;
                    p = c;
                    found = true;
                }
            }
            n.X = p.x;
            n.Y = p.y;
            n.CorpSlot = ground[0].CorpSlot;
            // One clear link down to the nearest hub of the corporation (a second one only if it is short and clear).
            Map.Dirty();
            int linked = 0;
            System.Collections.Generic.List<MapNode> byDistance = ground.Where(h => !h.Fortress).OrderBy(h => Vector2.Distance(h.Pos, p)).ToList();
            foreach (MapNode down in byDistance)
            {
                if (linked >= 2 || (linked == 1 && Vector2.Distance(down.Pos, p) > 0.16f))
                    break;
                if (!Map.LinkClear(n.Id, down.Id))
                    continue;
                Map.Links.Add(new MapLink { A = n.Id, B = down.Id, Kind = LinkKind.Normal });
                Map.Dirty();
                linked++;
            }
            if (linked == 0)
            {
                MapNode nearest = byDistance.FirstOrDefault() ?? ground.OrderBy(h => Vector2.Distance(h.Pos, p)).First();
                Map.Links.Add(new MapLink { A = n.Id, B = nearest.Id, Kind = LinkKind.Normal });
                Map.Dirty();
            }
        }
    }
    private void ExposeOrbit()
    {
        Scribe_Values.Look(ref _orbitWasOpen, "orbitWasOpen");
        Scribe_Values.Look(ref _orbitByCorp, "orbitByCorp");
        Scribe_Values.Look(ref _orbitVersion, "orbitVersion");
    }
}
