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
    /// <summary>Fixers dropped by the player: name and the tick until which they stay away.</summary>
    private Dictionary<string, int> _banned = new();

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
        _fixers.RemoveAll(f => !f.Pinned && !f.Held && contracts?.HasActive(f) != true);

        foreach (Fixer fixer in _fixers)
        {
            if (contracts?.HasActive(fixer) == false && !fixer.IsSilent)
                contracts.RegenerateOffers(fixer);

            if (fixer.HasShop)
                fixer.Stock = RollStock(fixer);
        }

        int newcomers = Math.Max(1, Settings.poolSize - _fixers.Count);
        for (int i = 0; i < newcomers; i++)
            _fixers.Add(Generate(MissingSpecialty()));

        _nextRotationTick = Find.TickManager.TicksGame + Settings.rotationDays.RandomInRange * GenDate.TicksPerDay;

        if (notify && NetworkGate.IsOpen)
            Messages.Message("NR_FixersRotated".Translate(), MessageTypeDefOf.NeutralEvent, historical: true);
    }

    /// <summary>A specialty the pool lacks while it has fewer than minSpecialties different ones.</summary>
    private FixerSpecialty? MissingSpecialty()
    {
        var have = _fixers.Select(f => f.Specialty).Distinct().ToList();
        if (have.Count >= Settings.minSpecialties)
            return null;
        var missing = Enum.GetValues(typeof(FixerSpecialty)).Cast<FixerSpecialty>().Where(s => !have.Contains(s)).ToList();
        return missing.Count == 0 ? null : missing.RandomElement();
    }

    public int HeldCount => _fixers.Count(f => f.Held && !f.Pinned);

    public AcceptanceReport CanHold(Fixer fixer)
        => fixer.Held || HeldCount < Settings.maxHeld ? true : (AcceptanceReport)"NR_FixerHoldLimit".Translate(Settings.maxHeld);

    public void ToggleHold(Fixer fixer)
    {
        if (CanHold(fixer).Accepted)
            fixer.Held = !fixer.Held;
    }

    /// <summary>
    /// Break ties: the fixer leaves the pool and stays away for breakBanDays; their contracts are dropped without a
    /// reputation penalty (the relationship is gone anyway); a newcomer takes the free place at once.
    /// </summary>
    public void BreakTies(Fixer fixer)
    {
        if (!_fixers.Contains(fixer))
            return;
        ContractManager.Get()?.DropFixer(fixer);
        _fixers.Remove(fixer);
        _banned[fixer.Name] = Find.TickManager.TicksGame + Settings.breakBanDays * GenDate.TicksPerDay;
        Fixer newcomer = Generate(MissingSpecialty());
        _fixers.Add(newcomer);
        ContractManager.Get()?.RegenerateOffers(newcomer);
        Messages.Message("NR_FixerTiesBroken".Translate(fixer.Name, newcomer.Name), MessageTypeDefOf.NeutralEvent, historical: true);
    }

    private bool Banned(string name)
        => _banned.TryGetValue(name, out int until) && Find.TickManager.TicksGame < until;

    private Fixer Generate(FixerSpecialty? specialty = null)
    {
        var fixer = new Fixer
        {
            Id = _nextId++,
            Name = GenerateName(),
            JoinedTick = Find.TickManager.TicksGame,
            Specialty = specialty ?? Gen.RandomEnumValue<FixerSpecialty>(disallowFirstValue: false),
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
            if (_fixers.All(f => f.Name != name) && !Banned(name))
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
        Scribe_Collections.Look(ref _banned, "banned", LookMode.Value, LookMode.Value);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            _fixers ??= [];
            _banned ??= [];
    }
}
