using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Odyssey (docs/CONCEPTS_10.md, section 9): an "Orbit" of 2-3 backbone nodes along the top of the map, linked to the
/// transit. They are offline unless a colony map is in space (a gravship in orbit with a pod on board); loot there is
/// richer (x1.5) and the ICE harder.
/// </summary>
public partial class NetMapManager
{
    private static int _orbitCheckTick = -1;
    private static bool _orbitOpen;
    private bool _orbitWasOpen;

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
        if (!Map.Nodes.Any(n => n.District == District.Orbit))
        {
            var transit = Map.Nodes.Where(n => n.District == District.Transit).ToList();
            if (transit.Count == 0)
                return;
            int count = Settings.orbitNodes.RandomInRange;
            MapNode prev = null;
            for (int i = 0; i < count; i++)
            {
                var n = new MapNode
                {
                    Id = Map.NextNodeId++,
                    District = District.Orbit,
                    X = Mathf.Lerp(0.35f, 0.8f, count == 1 ? 0.5f : i / (float)(count - 1)),
                    Y = 0.03f,
                    Slots = 2,
                };
                Map.Nodes.Add(n);
                MapNode down = transit.OrderBy(t => Vector2.Distance(t.Pos, n.Pos)).First();
                Map.Links.Add(new MapLink { A = n.Id, B = down.Id, Kind = LinkKind.Normal });
                if (prev != null)
                    Map.Links.Add(new MapLink { A = prev.Id, B = n.Id, Kind = LinkKind.Normal });
                prev = n;
                for (int s = 0; s < n.Slots; s++)
                {
                    ExternalNode branch = ExternalNode.ForDistrict(Gateway.NextBranchId(), District.Orbit);
                    Attach(branch, n);
                }
            }
            Map.Dirty();
        }
        bool open = OrbitOpen;
        if (open != _orbitWasOpen)
        {
            _orbitWasOpen = open;
            Gateway.AddHeadline((open ? "NR_HeadlineOrbitOpen" : "NR_HeadlineOrbitClosed").Translate(), open ? 1 : 0);
        }
    }

    private void ExposeOrbit() => Scribe_Values.Look(ref _orbitWasOpen, "orbitWasOpen");
}
