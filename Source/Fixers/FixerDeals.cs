using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>Everything the colony can do with a fixer. All deals report back through messages.</summary>
public static class FixerDeals
{
    private static FixerSettingsDef Settings => NetrunDefOf.NR_Fixers;

    public static void Call(Fixer fixer, Map map)
    {
        FirstHints.Show("Fixer", "NR_Help_Fixers");
        NetworkGate.Unlock("Board");
        if (NetworkIsolation.Active)
        {
            Reject(NetworkIsolation.Check().Reason);
            return;
        }
        if (fixer.IsSilent)
        {
            Reject("NR_FixerSilent".Translate(fixer.Name));
            return;
        }

        if (fixer.CallRefusal is { } refusal)
        {
            Reject(refusal);
            return;
        }

        int cost = fixer.EffectiveCallCost;
        if (!ColonyGoods.TryTake(map, ThingDefOf.Silver, cost))
        {
            Reject("NR_NotEnoughSilver".Translate(cost));
            return;
        }

        fixer.ChannelOpenUntilTick = Find.TickManager.TicksGame + Settings.channelHours * GenDate.TicksPerHour;
        fixer.GainReputation(Settings.reputationPerCall);
        NetrunSound.Play("UI_Confirm");
        Messages.Message("NR_FixerCalled".Translate(fixer.Name, cost), MessageTypeDefOf.NeutralEvent, historical: false);
        fixer.AfterDeal();
    }

    /// <summary>Heat removed for the given amount of data at this fixer's rates.</summary>
    /// <summary>
    /// Heat a scrub removes, unit by unit: each unit is priced at the level the Heat stands at when it is spent
    /// (advanced first). One big scrub and many small ones now cost the same.
    /// </summary>
    public static float ScrubAmount(Fixer fixer, int cyberdata, int advanced)
    {
        float start = HeatTracker.Get()?.Heat ?? 0f;
        float heat = Spend(fixer, start, advanced, true);
        heat = Spend(fixer, heat, cyberdata, false);
        return Mathf.Max(0f, start - heat);
    }

    // Awake AIs keep a floor under the Heat: a scrub stops there and data past it is not counted.
    private static float Spend(Fixer fixer, float heat, int units, bool advanced)
    {
        float floor = AIBalance.NoiseFloor;
        for (int i = 0; i < units && heat > floor; i++)
            heat = Mathf.Max(floor, heat - fixer.HeatPerUnitAt(heat, advanced));
        return heat;
    }

    /// <summary>How many units of one kind clear the Heat to zero, given the other kind already chosen (capped).</summary>
    public static int UnitsToClear(Fixer fixer, bool advanced, int otherUnits, int cap)
    {
        float heat = HeatTracker.Get()?.Heat ?? 0f;
        heat = advanced ? Spend(fixer, heat, otherUnits, false) : Spend(fixer, heat, otherUnits, true);
        float floor = AIBalance.NoiseFloor;
        int n = 0;
        while (heat > floor + 0.01f && n < cap)
        {
            heat = Mathf.Max(floor, heat - fixer.HeatPerUnitAt(heat, advanced));
            n++;
        }
        return n;
    }

    public static void Scrub(Fixer fixer, Map map, int cyberdata, int advanced)
    {
        // Only what the Heat above the AI floor needs is taken (advanced first, as ScrubAmount spends it).
        advanced = Mathf.Min(advanced, UnitsToClear(fixer, true, 0, advanced));
        cyberdata = Mathf.Min(cyberdata, UnitsToClear(fixer, false, advanced, cyberdata));
        if (cyberdata <= 0 && advanced <= 0)
            return;

        if (ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata) < cyberdata
            || ColonyGoods.Count(map, NetrunDefOf.USH_AdvancedCyberdata) < advanced)
        {
            Reject("NR_NotEnoughCyberdata".Translate());
            return;
        }

        ColonyGoods.TryTake(map, NetrunDefOf.USH_Cyberdata, cyberdata);
        ColonyGoods.TryTake(map, NetrunDefOf.USH_AdvancedCyberdata, advanced);
        EconomyTracker.Get()?.Note(spent: cyberdata + advanced * Settings.advancedCyberdataWorth);

        float amount = ScrubAmount(fixer, cyberdata, advanced);
        HeatTracker.Get()?.ReduceHeat(amount, "NR_LogScrub".Translate(fixer.Name), HeatSource.Scrub);
        fixer.GainReputation(Settings.reputationPerScrub);
        NetrunSound.Play("UI_Confirm");
        Messages.Message("NR_FixerScrubbed".Translate(fixer.Name, amount.ToString("0")), MessageTypeDefOf.PositiveEvent, historical: false);
        fixer.AfterDeal();
    }

    public static void Buy(Fixer fixer, Map map, ThingDef def)
    {
        int price = fixer.PriceOf(def);
        if (!ColonyGoods.TryTake(map, ThingDefOf.Silver, price))
        {
            Reject("NR_NotEnoughSilver".Translate(price));
            return;
        }

        Thing thing = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
        thing.stackCount = Fixer.ShopCount(def);
        IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
        DropPodUtility.DropThingsNear(dropSpot, map, [thing], forbid: false);

        // Cyberdata packs never sell out; anything else leaves the stock (or the black market's list).
        bool always = def == NetrunDefOf.USH_Cyberdata && fixer.Stock.Contains(def);
        if (!always && !fixer.Stock.Remove(def))
            MapEventsManager.Get()?.MarkBlackMarketSold(def);
        fixer.GainReputation(Settings.reputationPerPurchase);
        NetrunSound.Play("UI_Confirm");
        Messages.Message("NR_FixerDelivery".Translate(fixer.Name, thing.stackCount > 1 ? def.label + " x" + thing.stackCount : def.label),
            new LookTargets(dropSpot, map), MessageTypeDefOf.PositiveEvent, historical: true);
        fixer.AfterDeal();
    }

    /// <summary>Fresh fragments only: one that holds a known AI is the colony's own and is never sold.</summary>
    public static IEnumerable<Thing> SellableShards(Map map)
        => ColonyGoods.Available(map, NetrunDefOf.NR_AIFragment).Where(t => t.TryGetComp<CompAIFragment>()?.StoredAIId < 0);

    /// <summary>The best shard price among the fences the colony knows (never below the floor): what a wild AI sold in a dive pays.</summary>
    public static int BestShardPrice()
    {
        int best = Settings.shardBuyMin;
        foreach (Fixer f in FixerManager.Get()?.Fixers ?? [])
            if (f.HasShop && f.ShardBuyPrice > best)
                best = f.ShardBuyPrice;
        return best;
    }

    public static void SellShard(Fixer fixer, Map map)
    {
        Thing shard = SellableShards(map).FirstOrDefault();
        if (shard == null)
        {
            Reject("NR_ShopShardNone".Translate());
            return;
        }

        int price = fixer.ShardBuyPrice;
        shard.Destroy();
        Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
        silver.stackCount = price;
        DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, [silver], forbid: false);
        fixer.GainReputation(Settings.reputationPerPurchase);
        NetrunSound.Play("UI_Confirm");
        Messages.Message("NR_FixerShardSold".Translate(fixer.Name, price), MessageTypeDefOf.PositiveEvent, historical: false);
        fixer.AfterDeal();
    }

    private static void Reject(string text)
    {
        NetrunSound.Play("UI_Deny");
        Messages.Message(text, MessageTypeDefOf.RejectInput, historical: false);
    }
}
