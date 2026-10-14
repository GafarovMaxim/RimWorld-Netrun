using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// I1, the gradual opening of the map (Maxim, 2026-10-14: simple, no hard limits). At the start only Home and the
/// districts next to it are charted; the rest are drawn dim with a "?" and their branches stay hidden. A runner
/// stepping into a district charts it, and every clean dive charts the nearest uncharted district bordering the
/// charted ground. Nothing is locked: a runner may walk anywhere. Off in the settings: everything is charted.
/// </summary>
public partial class NetMapManager
{
    /// <summary>Charted districts by key (district and corporation slot); null on a save from before charting.</summary>
    private HashSet<int> _charted;

    private static int DistrictKey(MapNode n) => (int)n.District * 16 + n.CorpSlot + 1;

    public bool Charted(MapNode n)
        => n == null || !NetrunMod.Settings.GradualDistricts || _charted == null || n.District == District.Home || _charted.Contains(DistrictKey(n));

    /// <summary>A new map: Home and the districts one link away are charted.</summary>
    private void ChartStart()
    {
        _charted = [DistrictKey(Map.Home)];
        foreach (MapLink l in Map.LinksOf(Map.HomeId))
            _charted.Add(DistrictKey(Map.Node(l.Other(Map.HomeId))));
        HideUncharted();
    }

    /// <summary>Branches on uncharted ground stay hidden until the district is charted.</summary>
    private void HideUncharted()
    {
        if (!Ready || Gateway == null)
            return;
        foreach (ExternalNode b in Gateway.AllBranches)
            if (b.MapNodeId >= 0 && !b.IsBoss && !Charted(Map.Node(b.MapNodeId)))
                b.Hidden = true;
    }

    /// <summary>Charts the district of this hub; true if it was new (with a message saying what lives there).</summary>
    public bool Chart(MapNode n, bool announce = true)
    {
        if (_charted == null || n == null || Charted(n))
            return false;
        _charted.Add(DistrictKey(n));
        UpdateVisibility();
        if (announce)
        {
            string name = n.District == District.Corp && CorpOfSlot(n.CorpSlot) is { } corp ? corp.Name : (string)("NR_District_" + n.District).Translate();
            NetrunSound.Play("UI_Scan");
            Messages.Message("NR_DistrictCharted".Translate(name, ("NR_DistrictHint_" + n.District).Translate()), MessageTypeDefOf.PositiveEvent, historical: true);
        }
        return true;
    }

    /// <summary>After a clean dive: the nearest uncharted district that borders charted ground (by hops from Home).</summary>
    public void ChartNext()
    {
        if (!Ready || _charted == null || !NetrunMod.Settings.GradualDistricts)
            return;
        MapNode best = null;
        int bestHops = int.MaxValue;
        foreach (MapLink l in Map.Links)
        {
            if (!l.Known)
                continue;
            MapNode a = Map.Node(l.A), b = Map.Node(l.B);
            if (a == null || b == null || Charted(a) == Charted(b))
                continue;
            MapNode fresh = Charted(a) ? b : a;
            if (fresh.District is District.Dark or District.OldNet)
                continue;
            int hops = Map.Hops(Map.HomeId, fresh.Id);
            if (hops < bestHops)
            {
                bestHops = hops;
                best = fresh;
            }
        }
        Chart(best);
    }

    /// <summary>Dev: chart every district at once.</summary>
    public void DevChartAll()
    {
        if (!Ready)
            return;
        _charted ??= [];
        foreach (MapNode n in Map.Nodes)
            _charted.Add(DistrictKey(n));
        UpdateVisibility();
    }

    /// <summary>Dev: back to the start (Home and its neighbours).</summary>
    public void DevChartReset()
    {
        if (Ready)
            ChartStart();
    }

    private void ExposeCharting()
    {
        List<int> charted = _charted?.ToList();
        Scribe_Collections.Look(ref charted, "charted", LookMode.Value);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
            _charted = charted != null ? new HashSet<int>(charted) : null;
    }
}
