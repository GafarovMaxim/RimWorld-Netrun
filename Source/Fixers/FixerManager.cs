using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Grammar;

namespace Netrun;

/// <summary>The rotating pool of fixers. Fixers with enough reputation stay for good.</summary>
public class FixerManager : GameComponent
{
    private const int TickInterval = 2500;

    private List<Fixer> _fixers = [];
    private int _nextRotationTick = -1;
    private int _nextId = 1;

    public FixerManager(Game game) { }

    public static FixerManager Get() => Current.Game?.GetComponent<FixerManager>();

    public IReadOnlyList<Fixer> Fixers => _fixers;
    public Fixer ById(int id) => _fixers.Find(f => f.Id == id);
    public int TicksUntilRotation => Math.Max(0, _nextRotationTick - Find.TickManager.TicksGame);

    private static FixerSettingsDef Settings => NetrunDefOf.NR_Fixers;

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        if (_fixers.Count == 0)
            Rotate(notify: false);
    }

    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % TickInterval != 0)
            return;

        if (Find.TickManager.TicksGame >= _nextRotationTick)
            Rotate(notify: true);
    }

    /// <summary>
    /// Unpinned fixers leave (unless a contract with them is running), new ones fill the free slots
    /// (always at least one newcomer). Fixers who stay get fresh contract offers.
    /// </summary>
    public void Rotate(bool notify)
    {
        ContractManager contracts = ContractManager.Get();
        _fixers.RemoveAll(f => !f.Pinned && contracts?.HasActive(f) != true);

        foreach (Fixer fixer in _fixers)
        {
            if (contracts?.HasActive(fixer) == false && !fixer.IsSilent)
                contracts.RegenerateOffers(fixer);

            if (fixer.HasShop)
                fixer.Stock = RollStock(fixer);
        }

        int newcomers = Math.Max(1, Settings.poolSize - _fixers.Count);
        for (int i = 0; i < newcomers; i++)
            _fixers.Add(Generate());

        _nextRotationTick = Find.TickManager.TicksGame + Settings.rotationDays.RandomInRange * GenDate.TicksPerDay;

        if (notify)
            Messages.Message("NR_FixersRotated".Translate(), MessageTypeDefOf.NeutralEvent, historical: true);
    }

    private Fixer Generate()
    {
        var fixer = new Fixer
        {
            Id = _nextId++,
            Name = GenerateName(),
            Specialty = Gen.RandomEnumValue<FixerSpecialty>(disallowFirstValue: false),
            Trait = Gen.RandomEnumValue<FixerTrait>(disallowFirstValue: false),
            CallCost = Settings.callCost.RandomInRange,
        };

        if (fixer.HasShop)
            fixer.Stock = RollStock(fixer);

        return fixer;
    }

    /// <summary>Random stock; fixers who trust the colony also offer their rare goods.</summary>
    private static List<ThingDef> RollStock(Fixer fixer)
    {
        List<ThingDef> stock = Settings.shopItems.InRandomOrder().Take(Settings.shopStockSize).ToList();
        if (fixer.Pinned)
            stock.AddRange(Settings.pinnedShopItems);
        return stock;
    }

    private string GenerateName()
    {
        for (int i = 0; i < 20; i++)
        {
            string name = GrammarResolver.Resolve("r_name", new GrammarRequest { Includes = { NetrunDefOf.NR_NamerFixer } });
            if (_fixers.All(f => f.Name != name))
                return name;
        }
        return "Fixer " + _nextId;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref _fixers, "fixers", LookMode.Deep);
        Scribe_Values.Look(ref _nextRotationTick, "nextRotationTick", -1);
        Scribe_Values.Look(ref _nextId, "nextId", 1);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            _fixers ??= [];
    }
}
