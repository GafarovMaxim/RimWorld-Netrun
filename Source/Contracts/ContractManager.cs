using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>Contract offers and accepted contracts. One accepted contract per fixer.</summary>
public class ContractManager : GameComponent, INetrunCleanable
{
    private const int TickInterval = 250;

    private List<Contract> _contracts = [];
    private List<ContractRecord> _history = [];
    private const int HistorySize = 5;
    private int _nextId = 1;

    public ContractManager(Game game) { }

    public int Cleanup(CleanupReason reason)
    {
        int n = _history.RemoveAll(r => r == null);
        if (_history.Count > HistorySize)
        {
            n += _history.Count - HistorySize;
            _history.RemoveRange(HistorySize, _history.Count - HistorySize);
        }
        return n;
    }

    public static ContractManager Get() => GameComp<ContractManager>.Get();

    private static FixerSettingsDef Settings => NetrunDefOf.NR_Fixers;

    public IEnumerable<Contract> Active => _contracts.Where(c => c.Accepted);
    public IEnumerable<Contract> OffersOf(Fixer fixer) => _contracts.Where(c => !c.Accepted && c.FixerId == fixer.Id);
    /// <summary>The last few finished contracts, newest first.</summary>
    public IReadOnlyList<ContractRecord> History => _history;

    private void Record(Contract contract, string fixerName, bool success)
    {
        _history.Insert(0, new ContractRecord { Type = contract.Type, FixerName = fixerName, Success = success, Reward = contract.Reward });
        if (_history.Count > HistorySize)
            _history.RemoveRange(HistorySize, _history.Count - HistorySize);
    }

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
            else if (contract.Type == ContractType.LayLow && (HeatTracker.Get()?.Heat ?? 0f) <= contract.Goal)
            {
                contract.Progress = contract.Goal;
                Complete(contract);
            }
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

    /// <summary>Puts an offer of this type on a fixer's list (a scenario's first contract).</summary>
    public void OfferOfType(Fixer fixer, ContractType type)
    {
        if (fixer != null)
            _contracts.Add(MakeOffer(fixer, type));
    }

    private Contract MakeOffer(Fixer fixer, ContractType? forced = null)
    {
        ContractType type = forced ?? OfferableTypes().RandomElement();
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
            case ContractType.Surveillance:
                contract.Goal = Settings.surveillanceHours.RandomInRange;
                contract.Reward = contract.Goal * Settings.surveillanceRewardPerHour;
                contract.TargetHub = PickWatchHub();
                break;
            case ContractType.LayLow:
            {
                // The Heat to get down to: what it is now, minus a chunk.
                int drop = Settings.layLowDrop.RandomInRange;
                contract.Goal = Mathf.Max(1, Mathf.FloorToInt((HeatTracker.Get()?.Heat ?? 0f) - drop));
                contract.Reward = drop * Settings.layLowRewardPerHeat;
                break;
            }
            case ContractType.Rookie:
                contract.Goal = Settings.rookieDives;
                contract.Reward = Settings.rookieReward;
                break;
            case ContractType.AIOrder:
                contract.Goal = 1;
                contract.Reward = Settings.aiOrderReward;
                break;
            case ContractType.CleanLab:
                contract.Goal = Settings.cleanLabChips.RandomInRange;
                contract.Param = (int)Gen.RandomEnumValue<BraindanceMood>(disallowFirstValue: false);
                contract.Reward = contract.Goal * Settings.cleanLabRewardPerChip;
                break;
        }

        FixerSettingsDef traits = NetrunDefOf.NR_Fixers;
        if (fixer.Specialty == FixerSpecialty.Imperial)
        {
            contract.Favor = ImperialClients.FavorFor(contract.Reward);
            contract.Reward = Mathf.RoundToInt(contract.Reward * traits.imperialSilverFactor);
        }
        // A famous colony is paid more (top 10 +10%, #1 +20%, docs/CONCEPTS_10.md, section 12).
        contract.Reward = Mathf.RoundToInt(contract.Reward * (NetRanking.Get()?.ContractRewardFactor ?? 1f));
        if (fixer.Trait == FixerTrait.Reliable)
            contract.Reward = Mathf.RoundToInt(contract.Reward * traits.reliableRewardFactor);
        else if (fixer.Trait == FixerTrait.Hothead)
        {
            contract.Reward = Mathf.RoundToInt(contract.Reward * traits.hotheadRewardFactor);
            contract.DurationTicks = Mathf.Max(2 * GenDate.TicksPerDay, Mathf.RoundToInt(contract.DurationTicks * traits.hotheadDurationFactor));
        }

        return contract;
    }

    /// <summary>Debug: an accepted contract of this type from the first fixer.</summary>
    public bool DevGive(ContractType type)
    {
        Fixer fixer = FixerManager.Get()?.Fixers.FirstOrDefault();
        if (fixer == null)
            return false;
        Contract contract = MakeOffer(fixer, type);
        if (type == ContractType.Surveillance && contract.TargetHub < 0)
            contract.TargetHub = PickWatchHub();
        contract.DeadlineTick = Find.TickManager.TicksGame + contract.DurationTicks;
        _contracts.Add(contract);
        return true;
    }

    /// <summary>Debug: every accepted contract reaches its goal.</summary>
    public void DevCompleteActive()
    {
        foreach (Contract contract in Active.ToList())
            if (!contract.Done)
                AddProgress(contract, contract.Goal - contract.Progress);
    }

    /// <summary>Kinds of work a fixer can offer now: laying low only makes sense when the trace is hot, training needs a novice.</summary>
    private static List<ContractType> OfferableTypes()
    {
        var types = new List<ContractType>
        {
            ContractType.DataDelivery, ContractType.SystemTap, ContractType.QuietRun,
            ContractType.Surveillance, ContractType.AIOrder, ContractType.CleanLab,
        };
        if ((HeatTracker.Get()?.Heat ?? 0f) >= Settings.layLowMinHeat)
            types.Add(ContractType.LayLow);
        if (PawnsFinder.AllMaps_FreeColonists.Any(IsRookie))
            types.Add(ContractType.Rookie);
        return types;
    }

    private static bool IsRookie(Pawn pawn)
        => pawn.skills?.GetSkill(SkillDefOf.Intellectual) is { TotallyDisabled: false } skill && skill.Level <= Settings.rookieMaxSkill;

    /// <summary>
    /// D3: a hub to watch, random among the known, open ones (not Home, an exit, the dark or orbit), or -1 while the map
    /// is not ready or the option is off.
    /// </summary>
    public static int PickWatchHub()
    {
        NetMapManager net = NetMapManager.Get();
        if (!NetrunMod.Settings.SurveillanceInNode || net is not { Ready: true })
            return -1;
        CyberMap map = net.Map;
        List<MapNode> pool = map.Nodes.Where(n => !n.Exit && n.District is not (District.Home or District.Dark or District.Orbit) && !n.Offline
            && map.LinksOf(n.Id).Any(l => l.Known)).ToList();
        return pool.Count == 0 ? -1 : pool.RandomElement().Id;
    }

    // Credits this check, per contract (several pods in the hub count, at most two: docs/CONCEPTS_D_2026-10-09.md, D3).
    private readonly Dictionary<int, int> _watchCredits = [];
    private int _watchTick = -1;

    /// <summary>
    /// Netrunner time in a pod (called per pod and check): every full hour counts for surveillance. With a watched hub
    /// only a runner standing in that hub counts.
    /// </summary>
    public void NotifyPodTicks(int ticks, Pawn diver = null)
    {
        int now = Find.TickManager.TicksGame;
        if (now != _watchTick)
        {
            _watchTick = now;
            _watchCredits.Clear();
        }
        foreach (Contract contract in Active.Where(c => c.Type == ContractType.Surveillance).ToList())
        {
            if (contract.Accepted && contract.TargetHub < 0 && NetrunMod.Settings.SurveillanceInNode)
                contract.TargetHub = PickWatchHub();
            if (contract.WatchesHub)
            {
                NetMapManager net = NetMapManager.Get();
                if (net?.Map?.Node(contract.TargetHub) == null)
                {
                    contract.TargetHub = PickWatchHub();
                    continue;
                }
                NetRunner runner = diver == null ? null : net.RunnerOf(diver);
                if (runner == null || runner.Moving || runner.NodeId != contract.TargetHub)
                    continue;
                _watchCredits.TryGetValue(contract.Id, out int credits);
                if (credits >= 2)
                    continue;
                _watchCredits[contract.Id] = credits + 1;
            }
            contract.Param += ticks;
            int hours = contract.Param / GenDate.TicksPerHour;
            if (hours <= 0)
                continue;
            contract.Param -= hours * GenDate.TicksPerHour;
            AddProgress(contract, hours);
        }
    }

    /// <summary>A dive ended: a rookie's clean dive counts, a failed one starts the training over.</summary>
    public void NotifyDive(Pawn runner, bool success)
    {
        if (runner == null || !IsRookie(runner))
            return;
        foreach (Contract contract in Active.Where(c => c.Type == ContractType.Rookie).ToList())
        {
            if (success)
                AddProgress(contract, 1);
            else if (contract.Progress > 0)
            {
                contract.Progress = 0;
                Messages.Message("NR_ContractRookieReset".Translate(runner.LabelShort), MessageTypeDefOf.NegativeEvent, historical: true);
            }
        }
    }

    /// <summary>A braindance chip was edited: one of the wanted mood counts for a clean lab job.</summary>
    public void NotifyChipEdited(BraindanceMood mood)
    {
        foreach (Contract contract in Active.Where(c => c.Type == ContractType.CleanLab && c.Param == (int)mood).ToList())
            AddProgress(contract, 1);
    }

    /// <summary>Hands an AI fragment from storage to the fixer.</summary>
    public void DeliverFragment(Contract contract, Map map)
    {
        // Only a blank fragment: one that holds a sleeping AI of the colony is not for sale.
        Thing fragment = EmptyFragments(map).FirstOrDefault();
        if (fragment == null)
        {
            Messages.Message("NR_ContractNoFragment".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }
        fragment.SplitOff(1).Destroy();
        AddProgress(contract, 1);
    }

    public static IEnumerable<Thing> EmptyFragments(Map map)
        => ColonyGoods.Available(map, NetrunDefOf.NR_AIFragment).Where(t => (t.TryGetComp<CompAIFragment>()?.StoredAIId ?? -1) < 0);

    public AcceptanceReport CanAccept(Contract contract, Fixer fixer)
    {
        if (NetworkIsolation.Active)
            return NetworkIsolation.Check();
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
        NetrunSound.Play("UI_Confirm");
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
        EconomyTracker.Get()?.Note(spent: delivered);
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

    /// <summary>Ties broken: offers vanish, an active contract ends as failed in the history, without a reputation penalty.</summary>
    public void DropFixer(Fixer fixer)
    {
        foreach (Contract contract in _contracts.Where(c => c.FixerId == fixer.Id).ToList())
        {
            _contracts.Remove(contract);
            if (contract.Accepted)
                Record(contract, fixer.Name, success: false);
        }
    }

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
        AIQuests.NotifyContract(contract.FixerId);
        NetrunSignals.Increment("ContractsDone");
        Map map = Find.AnyPlayerHomeMap;

        // A gambler rolls the pay when the job is done.
        if (fixer?.Trait == FixerTrait.Gambler)
        {
            fixer.TraitRevealed = true;
            contract.Reward = Mathf.Max(1, Mathf.RoundToInt(contract.Reward * NetrunDefOf.NR_Fixers.gamblerRewardFactor.RandomInRange));
        }

        if (map != null)
        {
            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = contract.Reward;
            DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, [silver], forbid: false);
        }

        string fixerName = fixer?.Name ?? "?";
        // Now and then a fixer adds a unique braindance recording to the pay.
        if (map != null && Rand.Chance(NetrunDefOf.NR_UniqueBraindance.contractChance) && UniqueBraindanceManager.Get()?.RandomUnfound() is { } unique)
        {
            DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, [ThingMaker.MakeThing(unique)], forbid: false);
            Find.LetterStack.ReceiveLetter("NR_UB_ContractBonusLabel".Translate(), "NR_UB_ContractBonus".Translate(fixerName, unique.label), LetterDefOf.PositiveEvent);
        }
        Record(contract, fixerName, success: true);
        ImperialClients.GiveFavor(contract.Favor, fixerName);
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

    private const float MercRevengeChance = 0.25f;

    private void Fail(Contract contract, string reasonKey)
    {
        _contracts.Remove(contract);
        Fixer fixer = FixerManager.Get()?.ById(contract.FixerId);
        string fixerName = fixer?.Name ?? "?";
        Record(contract, fixerName, success: false);

        bool reliable = fixer?.Trait == FixerTrait.Reliable;
        if (fixer != null)
        {
            // A reliable fixer forgives half of it.
            float forgive = reliable ? 0.5f : 1f;
            fixer.GainReputation(-Settings.failReputationLoss * forgive);
            fixer.SilentUntilTick = Find.TickManager.TicksGame + Mathf.RoundToInt(Settings.failSilentDays * GenDate.TicksPerDay * forgive);
            fixer.ChannelOpenUntilTick = 0;

            if (fixer.Trait == FixerTrait.Hothead)
            {
                fixer.TraitRevealed = true;
                HeatTracker.Get()?.AddHeat(NetrunDefOf.NR_Fixers.hotheadFailHeat, "NR_FixerHotheadFail".Translate(fixer.Name), HeatSource.Event);
            }

            if (fixer.Trait == FixerTrait.Rat && Rand.Chance(Settings.ratFailLeakChance))
            {
                fixer.TraitRevealed = true;
                HeatTracker.Get()?.AddHeat(Settings.ratLeakHeat, "NR_FixerRatLeak".Translate(fixer.Name), HeatSource.Event);
            }
        }

        // Whoever lost money on the job may send someone to collect.
        if (!reliable)
            Mercenaries.TryQueueRevenge(MercRevengeChance, "NR_MercReasonContract", fixerName);

        Find.LetterStack.ReceiveLetter(
            "NR_ContractFailedLabel".Translate(),
            "NR_ContractFailed".Translate(contract.Title, fixerName, reasonKey.Translate(), Settings.failSilentDays),
            LetterDefOf.NegativeEvent);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref _contracts, "contracts", LookMode.Deep);
        Scribe_Collections.Look(ref _history, "history", LookMode.Deep);
        _history ??= [];
        Scribe_Values.Look(ref _nextId, "nextId", 1);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            _contracts ??= [];
    }
}
