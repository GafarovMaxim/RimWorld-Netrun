using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>What a unique recording does when watched (docs/CONCEPTS_6.md, section 4).</summary>
public enum UniqueBraindanceEffect
{
    Concert,
    ZeroRun,
    Morton,
    NightMarket,
    Confession,
    StreetRunner,
    EchoLullaby,
    Oracle,
}

/// <summary>Marks a unique recording and says what it does; texts live in Keyed (NR_UB_Author_*, NR_UB_Good_*, NR_UB_Bad_*).</summary>
public class UniqueBraindanceExtension : DefModExtension
{
    public UniqueBraindanceEffect effect;
    public bool black;
}

public class CompProperties_UniqueBraindance : CompProperties
{
    /// <summary>How many times the recording can be watched before it burns out.</summary>
    public int views = 1;

    public CompProperties_UniqueBraindance() => compClass = typeof(CompUniqueBraindance);
}

/// <summary>Views left on a unique recording; the first time one appears in the world it counts as found.</summary>
public class CompUniqueBraindance : ThingComp
{
    public int ViewsLeft = -1;

    private CompProperties_UniqueBraindance Props => (CompProperties_UniqueBraindance)props;

    public override void Initialize(CompProperties props)
    {
        base.Initialize(props);
        if (ViewsLeft < 0)
            ViewsLeft = Props.views;
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (!respawningAfterLoad)
            UniqueBraindanceManager.Get()?.MarkFound(parent.def);
    }

    public override string CompInspectStringExtra() => "NR_UB_ViewsLeft".Translate(ViewsLeft, Props.views);

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref ViewsLeft, "viewsLeft", -1);
    }
}

/// <summary>
/// Watching a unique recording: its own effect, then, if views are left, the chip comes back with one view less
/// (a drug is consumed when taken, so the "same" chip is made anew next to the viewer).
/// </summary>
public class IngestionOutcomeDoer_UniqueBraindance : IngestionOutcomeDoer
{
    protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
    {
        UniqueBraindanceExtension ext = ingested.def.GetModExtension<UniqueBraindanceExtension>();
        if (ext != null)
            UniqueBraindance.Apply(pawn, ext.effect);

        int left = (ingested.TryGetComp<CompUniqueBraindance>()?.ViewsLeft ?? 1) - 1;
        if (left > 0 && pawn.MapHeld != null)
        {
            Thing again = ThingMaker.MakeThing(ingested.def);
            if (again.TryGetComp<CompUniqueBraindance>() is { } comp)
                comp.ViewsLeft = left;
            GenPlace.TryPlaceThing(again, pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near);
        }
        else
        {
            Messages.Message("NR_UB_BurnedOut".Translate(ingested.def.label), pawn, MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }
}

public static class UniqueBraindance
{
    public static IEnumerable<ThingDef> All => DefDatabase<ThingDef>.AllDefs.Where(d => d.HasModExtension<UniqueBraindanceExtension>()).OrderBy(d => d.index);

    public static UniqueBraindanceExtension Ext(ThingDef def) => def.GetModExtension<UniqueBraindanceExtension>();

    private static HediffDef Hediff(string name) => DefDatabase<HediffDef>.GetNamedSilentFail(name);

    public static void Apply(Pawn pawn, UniqueBraindanceEffect effect)
    {
        UniqueBraindanceManager manager = UniqueBraindanceManager.Get();
        switch (effect)
        {
            case UniqueBraindanceEffect.Concert:
                if (DefDatabase<ThoughtDef>.GetNamedSilentFail("NR_UBT_Concert") is { } concert)
                    pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(concert);
                if (pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick) is { } sick)
                    pawn.health.RemoveHediff(sick);
                break;
            case UniqueBraindanceEffect.ZeroRun:
                Give(pawn, "NR_UBH_ZeroRun");
                break;
            case UniqueBraindanceEffect.Morton:
                // Old wounds of the mind fade: bad memories are let go, and the hands want work.
                List<Thought_Memory> bad = pawn.needs?.mood?.thoughts?.memories?.Memories.Where(m => m.MoodOffset() < 0f).ToList() ?? [];
                foreach (Thought_Memory m in bad)
                    pawn.needs.mood.thoughts.memories.RemoveMemory(m);
                if (DefDatabase<InspirationDef>.GetNamedSilentFail("Frenzy_Work") is { } frenzy)
                    pawn.mindState?.inspirationHandler?.TryStartInspiration(frenzy);
                break;
            case UniqueBraindanceEffect.NightMarket:
                manager?.StartMarketDiscount();
                break;
            case UniqueBraindanceEffect.Confession:
                if (manager != null)
                    manager.RevealNextDive = true;
                HeatTracker.Get()?.AddHeat(NetrunDefOf.NR_UniqueBraindance.confessionHeat, "NR_UB_ConfessionLog".Translate(pawn.LabelShort), HeatSource.Event);
                break;
            case UniqueBraindanceEffect.StreetRunner:
                Give(pawn, "NR_UBH_StreetRunner");
                break;
            case UniqueBraindanceEffect.EchoLullaby:
                if (pawn.needs?.rest != null)
                    pawn.needs.rest.CurLevel = pawn.needs.rest.MaxLevel;
                foreach (string name in new[] { "USH_CyberspaceNausea", "Hangover" })
                    if (Hediff(name) is { } def && pawn.health.hediffSet.GetFirstHediffOfDef(def) is { } h)
                        pawn.health.RemoveHediff(h);
                break;
            case UniqueBraindanceEffect.Oracle:
                Give(pawn, "NR_UBH_Oracle");
                if (manager != null)
                    manager.OracleWatching = true;
                break;
        }
        Messages.Message("NR_UB_Watched".Translate(pawn.LabelShort, ("NR_UB_Good_" + effect).Translate()), pawn, MessageTypeDefOf.PositiveEvent, historical: true);
    }

    private static void Give(Pawn pawn, string hediff)
    {
        if (Hediff(hediff) is { } def)
            pawn.health.AddHediff(def);
    }
}

/// <summary>Numbers for unique recordings (Defs/UniqueBraindance.xml).</summary>
public class UniqueBraindanceSettingsDef : Def
{
    public float diveFindChance = 0.5f;
    public float bossChance = 0.5f;
    public float blackMarketChance = 0.3f;
    public float contractChance = 0.1f;
    public float marketDiscount = 0.3f;
    public float marketDiscountDays = 3f;
    public float confessionHeat = 8f;
    public float oracleWarningDays = 1f;
    public float zeroRunHackBonus = 0.1f;
}

/// <summary>Which unique recordings the library has seen, and their lasting effects.</summary>
public class UniqueBraindanceManager : GameComponent
{
    private List<ThingDef> _found = [];
    private int _discountUntil = -1;
    public bool RevealNextDive;
    /// <summary>The Oracle's vision: the next raid on the colony is seen a day ahead (and comes a day later).</summary>
    public bool OracleWatching;

    public UniqueBraindanceManager(Game game) { }

    public static UniqueBraindanceManager Get() => Current.Game?.GetComponent<UniqueBraindanceManager>();

    public IReadOnlyList<ThingDef> Found => _found;

    public bool IsFound(ThingDef def) => _found.Contains(def);

    public void MarkFound(ThingDef def)
    {
        if (!_found.Contains(def))
            _found.Add(def);
    }

    /// <summary>
    /// A recording to drop. Any of them can come again; ones the library has not seen yet are three times as likely.
    /// </summary>
    public ThingDef RandomUnfound() => UniqueBraindance.All.RandomElementByWeightWithFallback(d => _found.Contains(d) ? 1f : 3f);

    public bool MarketDiscountActive => Find.TickManager.TicksGame < _discountUntil;

    public int MarketDiscountTicksLeft => Mathf.Max(0, _discountUntil - Find.TickManager.TicksGame);

    public void StartMarketDiscount() =>
        _discountUntil = Find.TickManager.TicksGame + (int)(NetrunDefOf.NR_UniqueBraindance.marketDiscountDays * GenDate.TicksPerDay);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref _found, "found", LookMode.Def);
        Scribe_Values.Look(ref _discountUntil, "discountUntil", -1);
        Scribe_Values.Look(ref RevealNextDive, "revealNextDive");
        Scribe_Values.Look(ref OracleWatching, "oracleWatching");
        _found ??= [];
        _found.RemoveAll(d => d == null);
    }
}
