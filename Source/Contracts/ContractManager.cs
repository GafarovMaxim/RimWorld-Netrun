using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>Contract offers and accepted contracts. One accepted contract per fixer.</summary>
public class ContractManager : GameComponent
{
    private const int TickInterval = 250;

    private List<Contract> _contracts = [];
    private int _nextId = 1;

    public ContractManager(Game game) { }

    public static ContractManager Get() => Current.Game?.GetComponent<ContractManager>();

    private static FixerSettingsDef Settings => NetrunDefOf.NR_Fixers;

    public IEnumerable<Contract> Active => _contracts.Where(c => c.Accepted);
    public IEnumerable<Contract> OffersOf(Fixer fixer) => _contracts.Where(c => !c.Accepted && c.FixerId == fixer.Id);
    public bool HasActive(Fixer fixer) => _contracts.Any(c => c.Accepted && c.FixerId == fixer.Id);

    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % TickInterval != 0)
            return;

        FixerManager fixers = FixerManager.Get();
        if (fixers == null)
            return;

        // Every fixer without an active contract keeps at least one offer.
        foreach (Fixer fixer in fixers.Fixers)
        {
            if (!fixer.IsSilent && !HasActive(fixer) && !OffersOf(fixer).Any())
                GenerateOffers(fixer);
        }

        // Offers of fixers who left the pool disappear.
        _contracts.RemoveAll(c => !c.Accepted && fixers.ById(c.FixerId) == null);

        foreach (Contract contract in Active.ToList())
        {
            if (contract.Type == ContractType.QuietRun && HeatTracker.Get()?.Level > HeatLevel.Tracked)
                Fail(contract, "NR_ContractFailedLoud");
            else if (Find.TickManager.TicksGame >= contract.DeadlineTick)
                Fail(contract, "NR_ContractFailedExpired");
        }
    }

    public void RegenerateOffers(Fixer fixer)
    {
        _contracts.RemoveAll(c => !c.Accepted && c.FixerId == fixer.Id);
        GenerateOffers(fixer);
    }

    private void GenerateOffers(Fixer fixer)
    {
        int count = Settings.offersPerFixer.RandomInRange;
        for (int i = 0; i < count; i++)
            _contracts.Add(MakeOffer(fixer));
    }

    private Contract MakeOffer(Fixer fixer)
    {
        var type = Gen.RandomEnumValue<ContractType>(disallowFirstValue: false);
        var contract = new Contract
        {
            Id = _nextId++,
            FixerId = fixer.Id,
            Type = type,
            DurationTicks = Settings.contractDays.RandomInRange * GenDate.TicksPerDay,
        };

        switch (type)
        {
            case ContractType.DataDelivery:
                contract.Goal = Settings.dataDeliveryGoal.RandomInRange;
                float bonus = fixer.Specialty == FixerSpecialty.Broker ? Settings.brokerDataBonus : 1f;
                contract.Reward = Mathf.RoundToInt(contract.Goal * Settings.dataDeliveryRewardPerItem * bonus);
                break;
            case ContractType.SystemTap:
                contract.Goal = Settings.systemTapGoal.RandomInRange;
                contract.Reward = contract.Goal * Settings.systemTapRewardPerHack;
                break;
            case ContractType.QuietRun:
                contract.Goal = Settings.quietRunGoal.RandomInRange;
                contract.Reward = contract.Goal * Settings.quietRunRewardPerHack;
                break;
        }

        return contract;
    }

    public AcceptanceReport CanAccept(Contract contract, Fixer fixer)
    {
        if (fixer.IsSilent)
            return "NR_FixerSilent".Translate(fixer.Name);
        if (!fixer.ChannelOpen)
            return "NR_ContractNeedsChannel".Translate();
        if (HasActive(fixer))
            return "NR_ContractAlreadyActive".Translate(fixer.Name);
        if (contract.Type == ContractType.QuietRun && HeatTracker.Get()?.Level > HeatLevel.Tracked)
            return "NR_ContractTooLoud".Translate();
        return true;
    }

    public void Accept(Contract contract, Fixer fixer)
    {
        AcceptanceReport report = CanAccept(contract, fixer);
        if (!report.Accepted)
        {
            Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        contract.DeadlineTick = Find.TickManager.TicksGame + contract.DurationTicks;
        _contracts.RemoveAll(c => !c.Accepted && c.FixerId == fixer.Id);
        Messages.Message("NR_ContractAccepted".Translate(contract.Title, fixer.Name), MessageTypeDefOf.NeutralEvent, historical: true);
    }

    /// <summary>Called once per hack completed by a colonist.</summary>
    public void NotifyHackCompleted()
    {
        foreach (Contract contract in Active.ToList())
        {
            // The hack's own Heat is already applied, so a hack that breaks a quiet run never counts for it.
            if (contract.Type == ContractType.QuietRun && HeatTracker.Get()?.Level > HeatLevel.Tracked)
                Fail(contract, "NR_ContractFailedLoud");
            else if (contract.Type is ContractType.SystemTap or ContractType.QuietRun)
                AddProgress(contract, 1);
        }
    }

    /// <summary>Hands over cyberdata from storage. Advanced cyberdata counts for several items.</summary>
    public void Deliver(Contract contract, Map map)
    {
        int need = contract.Goal - contract.Progress;
        int worth = Settings.advancedCyberdataWorth;

        int advanced = Mathf.Min(need / worth, ColonyGoods.Count(map, NetrunDefOf.USH_AdvancedCyberdata));
        ColonyGoods.TryTake(map, NetrunDefOf.USH_AdvancedCyberdata, advanced);
        need -= advanced * worth;

        int regular = Mathf.Min(need, ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata));
        ColonyGoods.TryTake(map, NetrunDefOf.USH_Cyberdata, regular);

        int delivered = advanced * worth + regular;
        if (delivered <= 0)
        {
            Messages.Message("NR_NotEnoughCyberdata".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        AddProgress(contract, delivered);

        if (!contract.Done)
        {
            Messages.Message("NR_ContractDelivered".Translate(delivered, contract.Progress, contract.Goal, contract.Goal - contract.Progress),
                MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }

    public void Abandon(Contract contract) => Fail(contract, "NR_ContractFailedAbandoned");

    private void AddProgress(Contract contract, int amount)
    {
        contract.Progress = Mathf.Min(contract.Goal, contract.Progress + amount);
        if (contract.Done)
            Complete(contract);
    }

    private void Complete(Contract contract)
    {
        _contracts.Remove(contract);
        Fixer fixer = FixerManager.Get()?.ById(contract.FixerId);
        Map map = Find.AnyPlayerHomeMap;

        if (map != null)
        {
            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = contract.Reward;
            DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, [silver], forbid: false);
        }

        string fixerName = fixer?.Name ?? "?";
        if (fixer != null)
        {
            fixer.GainReputation(Settings.contractReputation);
            fixer.AfterDeal();
        }

        Find.LetterStack.ReceiveLetter(
            "NR_ContractCompletedLabel".Translate(),
            "NR_ContractCompleted".Translate(contract.Title, fixerName, contract.Reward),
            LetterDefOf.PositiveEvent);
    }

    private void Fail(Contract contract, string reasonKey)
    {
        _contracts.Remove(contract);
        Fixer fixer = FixerManager.Get()?.ById(contract.FixerId);
        string fixerName = fixer?.Name ?? "?";

        if (fixer != null)
        {
            fixer.GainReputation(-Settings.failReputationLoss);
            fixer.SilentUntilTick = Find.TickManager.TicksGame + Settings.failSilentDays * GenDate.TicksPerDay;
            fixer.ChannelOpenUntilTick = 0;

            if (fixer.Trait == FixerTrait.Rat && Rand.Chance(Settings.ratFailLeakChance))
            {
                fixer.TraitRevealed = true;
                HeatTracker.Get()?.AddHeat(Settings.ratLeakHeat, "NR_FixerRatLeak".Translate(fixer.Name));
            }
        }

        Find.LetterStack.ReceiveLetter(
            "NR_ContractFailedLabel".Translate(),
            "NR_ContractFailed".Translate(contract.Title, fixerName, reasonKey.Translate(), Settings.failSilentDays),
            LetterDefOf.NegativeEvent);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref _contracts, "contracts", LookMode.Deep);
        Scribe_Values.Look(ref _nextId, "nextId", 1);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            _contracts ??= [];
    }
}
