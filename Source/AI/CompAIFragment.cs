using Verse;

namespace Netrun;

public class CompProperties_AIFragment : CompProperties
{
    public CompProperties_AIFragment() => compClass = typeof(CompAIFragment);
}

/// <summary>A fragment is either a fresh unknown AI or one that fell back from a destroyed server.</summary>
public class CompAIFragment : ThingComp
{
    public int StoredAIId = -1;

    public NetAI StoredAI => StoredAIId >= 0 ? AIManager.Get()?.ById(StoredAIId) : null;

    public override string CompInspectStringExtra()
    {
        NetAI ai = StoredAI;
        return ai == null
            ? "NR_AIFragmentUnknown".Translate()
            : "NR_AIFragmentStored".Translate(ai.Name, ai.Personality.Label());
    }

    public override bool AllowStackWith(Thing other) => false;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref StoredAIId, "storedAIId", -1);
    }
}
