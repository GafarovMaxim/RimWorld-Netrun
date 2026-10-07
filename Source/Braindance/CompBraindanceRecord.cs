using RimWorld;
using Verse;

namespace Netrun;

/// <summary>What a raw recording captured; decides which chips it can be edited into.</summary>
public enum BraindanceMood
{
    Bright,
    Dark,
    Combat,
}

public class CompProperties_BraindanceRecord : CompProperties
{
    public CompProperties_BraindanceRecord() => compClass = typeof(CompBraindanceRecord);
}

/// <summary>
/// Who recorded it, what happened and how strong it was. Raw recordings and chips edited from them
/// carry it; chips from the drug lab have none and play as plain chips.
/// </summary>
public class CompBraindanceRecord : ThingComp
{
    public string Author;
    public string Event;
    public BraindanceMood Mood;
    public float Strength;

    public bool HasRecord => !Author.NullOrEmpty();

    /// <summary>0 ordinary, 1 good, 2 masterful.</summary>
    public int QualityTier => !HasRecord ? 0 : Strength >= 0.85f ? 2 : Strength >= 0.55f ? 1 : 0;

    public void CopyFrom(CompBraindanceRecord other)
    {
        Author = other.Author;
        Event = other.Event;
        Mood = other.Mood;
        Strength = other.Strength;
    }

    public override string TransformLabel(string label)
        => HasRecord ? label + " (" + Author + ")" : label;

    public override bool AllowStackWith(Thing other)
    {
        CompBraindanceRecord rec = other.TryGetComp<CompBraindanceRecord>();
        if (rec == null)
            return !HasRecord;
        return Author == rec.Author && Event == rec.Event && Mood == rec.Mood && QualityTier == rec.QualityTier;
    }

    public override void PostSplitOff(Thing piece)
    {
        base.PostSplitOff(piece);
        piece.TryGetComp<CompBraindanceRecord>()?.CopyFrom(this);
    }

    public override string CompInspectStringExtra()
    {
        if (!HasRecord)
            return null;
        return "NR_BDRecordInspect".Translate(Author, Event, ("NR_BDMood_" + Mood).Translate(), ("NR_BDQuality_" + QualityTier).Translate());
    }

    /// <summary>A good recording leaves something behind: an extra memory scaled by quality.</summary>
    public override void PostIngested(Pawn ingester)
    {
        base.PostIngested(ingester);
        NetIdeo.Record("NR_WatchedBraindance", ingester);
        if (QualityTier == 0 || ingester?.needs?.mood == null)
            return;

        var memory = (Thought_Memory)ThoughtMaker.MakeThought(NetrunDefOf.NR_BraindanceGoodRecording, QualityTier - 1);
        ingester.needs.mood.thoughts.memories.TryGainMemory(memory);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref Author, "author");
        Scribe_Values.Look(ref Event, "event");
        Scribe_Values.Look(ref Mood, "mood");
        Scribe_Values.Look(ref Strength, "strength");
    }
}
