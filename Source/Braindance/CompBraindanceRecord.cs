using System.Collections.Generic;
using System.Linq;
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

/// <summary>One recording kept in a stack of chips.</summary>
public class BraindanceRecordData : IExposable
{
    public string Author;
    /// <summary>The recording colonist's ThingID, to find relatives; empty on older and outside recordings.</summary>
    public string AuthorId;
    public string Event;
    public BraindanceMood Mood;
    public float Strength;

    public void ExposeData()
    {
        Scribe_Values.Look(ref Author, "author");
        Scribe_Values.Look(ref AuthorId, "authorId");
        Scribe_Values.Look(ref Event, "event");
        Scribe_Values.Look(ref Mood, "mood");
        Scribe_Values.Look(ref Strength, "strength");
    }
}

/// <summary>
/// Who recorded it, what happened and how strong it was. Raw recordings and chips edited from them
/// carry it; chips from the drug lab have none and play as plain chips.
/// Recorded chips of one kind share a stack like a playlist (Maxim, 2026-10-21): the fields are the recording on top,
/// the one played, sold or split off next; the others wait in _rest, one per chip.
/// </summary>
public class CompBraindanceRecord : ThingComp
{
    public string Author;
    public string AuthorId;
    public string Event;
    public BraindanceMood Mood;
    public float Strength;
    private List<BraindanceRecordData> _rest = [];

    public bool HasRecord => !Author.NullOrEmpty();

    /// <summary>0 ordinary, 1 good, 2 masterful.</summary>
    public int QualityTier => !HasRecord ? 0 : Strength >= 0.85f ? 2 : Strength >= 0.55f ? 1 : 0;

    public void CopyFrom(CompBraindanceRecord other)
    {
        Author = other.Author;
        AuthorId = other.AuthorId;
        Event = other.Event;
        Mood = other.Mood;
        Strength = other.Strength;
    }

    public void CopyFrom(BraindanceRecordData r)
    {
        Author = r.Author;
        AuthorId = r.AuthorId;
        Event = r.Event;
        Mood = r.Mood;
        Strength = r.Strength;
    }

    private BraindanceRecordData Top => new() { Author = Author, AuthorId = AuthorId, Event = Event, Mood = Mood, Strength = Strength };

    /// <summary>The stack's recordings, top first, trimmed or padded with copies of the top to <paramref name="count"/>.</summary>
    private List<BraindanceRecordData> Records(int count)
    {
        var all = new List<BraindanceRecordData> { Top };
        all.AddRange(_rest.Take(count - 1));
        while (all.Count < count)
            all.Add(Top);
        return all;
    }

    private void SetRecords(List<BraindanceRecordData> all)
    {
        Author = all[0].Author;
        AuthorId = all[0].AuthorId;
        Event = all[0].Event;
        Mood = all[0].Mood;
        Strength = all[0].Strength;
        _rest = all.Skip(1).ToList();
    }

    /// <summary>Every recording in the stack, top first.</summary>
    public List<BraindanceRecordData> All => HasRecord ? Records(parent.stackCount) : [];

    public IEnumerable<string> Authors => All.Select(r => r.Author).Distinct();

    public override string TransformLabel(string label)
    {
        if (!HasRecord)
            return label;
        int authors = Authors.Count();
        return label + " (" + (authors > 1 ? (string)"NR_BDAuthors".Translate(authors) : Author) + ")";
    }

    public override bool AllowStackWith(Thing other)
        => (other.TryGetComp<CompBraindanceRecord>()?.HasRecord ?? false) == HasRecord;

    /// <summary>The other stack gives its bottom recordings and keeps its top.</summary>
    public override void PreAbsorbStack(Thing otherStack, int count)
    {
        base.PreAbsorbStack(otherStack, count);
        if (!HasRecord || otherStack.TryGetComp<CompBraindanceRecord>() is not { HasRecord: true } other)
            return;
        List<BraindanceRecordData> mine = Records(parent.stackCount);
        List<BraindanceRecordData> theirs = other.Records(otherStack.stackCount);
        mine.AddRange(theirs.Skip(theirs.Count - count));
        SetRecords(mine);
        if (count < otherStack.stackCount)
            other.SetRecords(theirs.Take(theirs.Count - count).ToList());
    }

    /// <summary>The piece takes the top recordings: a chip split off to be watched plays the top one.</summary>
    public override void PostSplitOff(Thing piece)
    {
        base.PostSplitOff(piece);
        if (piece == parent || !HasRecord || piece.TryGetComp<CompBraindanceRecord>() is not { } target)
            return;
        List<BraindanceRecordData> all = Records(parent.stackCount + piece.stackCount);
        target.SetRecords(all.Take(piece.stackCount).ToList());
        SetRecords(all.Skip(piece.stackCount).ToList());
    }

    private static int Tier(float strength) => strength >= 0.85f ? 2 : strength >= 0.55f ? 1 : 0;

    private static string Line(BraindanceRecordData r)
        => "NR_BDRecordInspect".Translate(r.Author, r.Event, ("NR_BDMood_" + r.Mood).Translate(), ("NR_BDQuality_" + Tier(r.Strength)).Translate());

    public override string CompInspectStringExtra()
    {
        if (!HasRecord)
            return null;
        List<BraindanceRecordData> all = All;
        if (all.Count == 1)
            return Line(all[0]);
        return "NR_BDRecordsNext".Translate(Line(all[0])) + "\n" + "NR_BDRecordsRest".Translate(all.Count - 1) + "\n"
               + all.Skip(1).Select(Line).ToLineList("  - ");
    }

    /// <summary>Moves one recording to the top: it is the next watched, sold or taken.</summary>
    public void PutOnTop(int index)
    {
        List<BraindanceRecordData> all = All;
        if (index <= 0 || index >= all.Count)
            return;
        BraindanceRecordData pick = all[index];
        all.RemoveAt(index);
        all.Insert(0, pick);
        SetRecords(all);
    }

    public static string Line(BraindanceRecordData r, Pawn viewer)
    {
        string tag = viewer != null ? BraindanceUtility.RelationTag(viewer, r) : null;
        return (tag != null ? "[" + tag + "] " : "") + Line(r);
    }

    /// <summary>Pick which recording of the stack is watched, sold or taken next.</summary>
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo g in base.CompGetGizmosExtra())
            yield return g;
        if (!HasRecord || parent.stackCount < 2)
            yield break;
        yield return new Command_Action
        {
            defaultLabel = "NR_BDPickNext".Translate(),
            defaultDesc = "NR_BDPickNextDesc".Translate(),
            icon = parent.def.uiIcon,
            action = () =>
            {
                List<BraindanceRecordData> all = All;
                var options = new List<FloatMenuOption>();
                for (int i = 1; i < all.Count; i++)
                {
                    int index = i;
                    options.Add(new FloatMenuOption(Line(all[i]), () => PutOnTop(index)));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            },
        };
    }

    /// <summary>A good recording leaves something behind: an extra memory scaled by quality.</summary>
    public override void PostIngested(Pawn ingester)
    {
        base.PostIngested(ingester);
        NetIdeo.Record("NR_WatchedBraindance", ingester);
        AIQuests.NotifyWatched(ingester, this);
        BraindanceUtility.RelativeMemory(ingester, Top);
        if (QualityTier == 0 || ingester?.needs?.mood == null)
            return;

        var memory = (Thought_Memory)ThoughtMaker.MakeThought(NetrunDefOf.NR_BraindanceGoodRecording, QualityTier - 1);
        ingester.needs.mood.thoughts.memories.TryGainMemory(memory);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref Author, "author");
        Scribe_Values.Look(ref AuthorId, "authorId");
        Scribe_Values.Look(ref Event, "event");
        Scribe_Values.Look(ref Mood, "mood");
        Scribe_Values.Look(ref Strength, "strength");
        Scribe_Collections.Look(ref _rest, "rest", LookMode.Deep);
        _rest ??= [];
    }
}
