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
    private int _layoutVersion;

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
        // Layout version 1 (2026-10-15): hubs step back from Home. Version 2 (2026-10-22): crossing links go, bridges
        // are rerouted through a clear pair. Once per older save; hubs never move here.
        // Version 3 (2026-10-23): corporations sewn back into one piece, no hub without a street; version 4: every district.
        // Version 5: streets between the Old Net and the main net (v4 laid some around the Wall) go, joined again.
        // Version 6: every transit hub gets a fast link to a district where one fits.
        if (Ready && _layoutVersion < 6)
        {
            if (_layoutVersion < 1)
                CyberMapGenerator.ClearHome(Map);
            if (_layoutVersion < 2)
            {
                int removed = CyberMapGenerator.Uncross(Map);
                if (Prefs.DevMode && removed > 0)
                    Log.Message($"[Netrun] Map layout v2: {removed} crossing links removed.");
            }
            Map.Links.RemoveAll(l => l.Kind is LinkKind.Normal or LinkKind.Fast
                                     && (Map.Node(l.A)?.District == District.OldNet) != (Map.Node(l.B)?.District == District.OldNet));
            Map.Dirty();
            int joined = CyberMapGenerator.JoinParts(Map);
            if (Prefs.DevMode && joined > 0)
                Log.Message($"[Netrun] Map layout v4: {joined} links added to join districts.");
            CyberMapGenerator.FeedTransit(Map);
            _layoutVersion = 6;
        }
        if (!ModsConfig.OdysseyActive || !Ready || Gateway == null)
            return;
        // Version 2 (2026-10-14): placed on free ground with one clear link; older saves are laid out again once.
        // Version 3 (2026-10-23): no spot left free kept the first candidate, on top of a hub; now the roomiest one.
        // Version 4: beyond the corporation's outer edge, off every link, never nearer a stranger than its own ground.
        if (!_orbitByCorp || _orbitVersion < 4 || !Map.Nodes.Any(n => n.District == District.Orbit))
        {
            PlaceOrbitByCorp();
            _orbitByCorp = true;
            _orbitVersion = 4;
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
            // Rings start past the corporation's farthest hub (Maxim, 2026-10-23: orbital hubs sat inside districts and on links).
            float reach = ground.Max(h => Vector2.Distance(h.Pos, centre));
            Vector2 p = centre + away * (reach + 0.07f);
            bool found = false;
            float roomiest = -1f;
            for (int ring = 0; ring < 6 && !found; ring++)
            {
                float radius = reach + 0.07f + ring * 0.035f;
                for (int k = 0; k < 12 && !found; k++)
                {
                    float turn = (k % 2 == 0 ? 1f : -1f) * (k + 1) / 2 * 25f * Mathf.Deg2Rad;
                    Vector2 dir = new(away.x * Mathf.Cos(turn) - away.y * Mathf.Sin(turn), away.x * Mathf.Sin(turn) + away.y * Mathf.Cos(turn));
                    Vector2 c = centre + dir * radius;
                    if (c.x < 0.04f || c.x > 0.96f || c.y < 0.04f || c.y > 0.66f)
                        continue;
                    System.Collections.Generic.List<MapNode> others = Map.Nodes.Where(o => o != n && (o.District != District.Orbit || orbit.IndexOf(o) < i)).ToList();
                    float room = others.Select(o => Vector2.Distance(o.Pos, c)).DefaultIfEmpty(1f).Min();
                    if (room > roomiest)
                    {
                        roomiest = room;
                        p = c;
                    }
                    if (room < 0.085f || !ground.Contains(others.Where(o => o.District != District.Orbit).MinBy(o => Vector2.Distance(o.Pos, c)))
                        || Map.Links.Any(l => CyberMap.DistanceToSegment(c, Map.Node(l.A).Pos, Map.Node(l.B).Pos) < CyberMap.LinkClearance * 1.5f))
                        continue;
                    // And a clear way down to its own ground.
                    n.X = c.x;
                    n.Y = c.y;
                    Map.Dirty();
                    found = ground.Any(h => !h.Fortress && Map.LinkClear(n.Id, h.Id));
                    if (found)
                        p = c;
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
            // No clear link to the corporation: the nearest hub of any kind that is clear.
            if (linked == 0)
                foreach (MapNode any in Map.Nodes.Where(h => h != n && h.District is not (District.Orbit or District.Dark or District.OldNet)).OrderBy(h => Vector2.Distance(h.Pos, p)))
                    if (Map.TryAddLink(new MapLink { A = n.Id, B = any.Id, Kind = LinkKind.Normal }))
                    {
                        linked++;
                        break;
                    }
            // shortcut: an orbital hub boxed in by links on every side keeps one forced link; a crossing is better than an unreachable hub.
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
        Scribe_Values.Look(ref _layoutVersion, "layoutVersion");
    }
}
