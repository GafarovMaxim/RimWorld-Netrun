using RimWorld;
using Verse;

namespace Netrun;

public class CompProperties_AIFragment : CompProperties
{
    public CompProperties_AIFragment() => compClass = typeof(CompAIFragment);
}

/// <summary>
/// A fragment is either a fresh unknown AI or one that fell back from a destroyed server.
/// A fresh fragment slowly falls apart away from a powered AI server (docs/CONCEPTS_10.md, section 2);
/// a known AI waits in its fragment without decaying.
/// </summary>
public class CompAIFragment : ThingComp
{
    public int StoredAIId = -1;
    private int _decayTicks;

    public NetAI StoredAI => StoredAIId >= 0 ? AIManager.Get()?.ById(StoredAIId) : null;

    private static int DecayLimit => (int)(NetrunDefOf.NR_AI.fragmentDecayDays * GenDate.TicksPerDay);

    private bool Decays => StoredAIId < 0 && NetrunDefOf.NR_AI.fragmentDecayDays > 0f;

    public override void CompTickRare()
    {
        base.CompTickRare();
        if (!Decays || NearPoweredServer())
            return;
        _decayTicks += GenTicks.TickRareInterval;
        if (_decayTicks < DecayLimit)
            return;

        Map map = parent.MapHeld;
        if (map != null)
            Messages.Message("NR_AIFragmentCrumbled".Translate(), new TargetInfo(parent.PositionHeld, map), MessageTypeDefOf.NegativeEvent);
        parent.Destroy();
    }

    /// <summary>On the same map, within a few cells of a powered AI server or server rack.</summary>
    public bool NearPoweredServer()
    {
        Map map = parent.MapHeld;
        if (map == null)
            return false;
        IntVec3 pos = parent.PositionHeld;
        float radiusSq = NetrunDefOf.NR_AI.fragmentSafeRadius * NetrunDefOf.NR_AI.fragmentSafeRadius;
        return Near(map, NetrunDefOf.NR_AIServer, pos, radiusSq) || Near(map, NetrunDefOf.NR_AIServerRack, pos, radiusSq);
    }

    private static bool Near(Map map, ThingDef def, IntVec3 pos, float radiusSq)
    {
        foreach (Building b in map.listerBuildings.AllBuildingsColonistOfDef(def))
        {
            if (b.GetComp<CompPowerTrader>()?.PowerOn == true && (b.Position - pos).LengthHorizontalSquared <= radiusSq)
                return true;
        }
        return false;
    }

    public override string CompInspectStringExtra()
    {
        NetAI ai = StoredAI;
        if (ai != null)
            return "NR_AIFragmentStored".Translate(ai.Name, ai.Personality.Label());
        string text = "NR_AIFragmentUnknown".Translate();
        if (!Decays)
            return text;
        float daysLeft = (DecayLimit - _decayTicks) / (float)GenDate.TicksPerDay;
        text += "\n" + (NearPoweredServer()
            ? "NR_AIFragmentStable".Translate(daysLeft.ToString("0.#"))
            : "NR_AIFragmentDecaying".Translate(daysLeft.ToString("0.#")));
        return text;
    }

    public override bool AllowStackWith(Thing other) => false;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref StoredAIId, "storedAIId", -1);
        Scribe_Values.Look(ref _decayTicks, "decayTicks");
    }
}
