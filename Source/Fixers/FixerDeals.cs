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
        NetworkGate.Unlock("Board");
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
        return start - heat;
    }

    private static float Spend(Fixer fixer, float heat, int units, bool advanced)
    {
        for (int i = 0; i < units && heat > 0f; i++)
            heat = Mathf.Max(0f, heat - fixer.HeatPerUnitAt(heat, advanced));
        return heat;
    }

    /// <summary>How many units of one kind clear the Heat to zero, given the other kind already chosen (capped).</summary>
    public static int UnitsToClear(Fixer fixer, bool advanced, int otherUnits, int cap)
    {
        float heat = HeatTracker.Get()?.Heat ?? 0f;
        heat = advanced ? Spend(fixer, heat, otherUnits, false) : Spend(fixer, heat, otherUnits, true);
        int n = 0;
        while (heat > 0.01f && n < cap)
        {
            heat = Mathf.Max(0f, heat - fixer.HeatPerUnitAt(heat, advanced));
            n++;
        }
        return n;
    }

    public static void Scrub(Fixer fixer, Map map, int cyberdata, int advanced)
    {
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
        IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
        DropPodUtility.DropThingsNear(dropSpot, map, [thing], forbid: false);

        if (!fixer.Stock.Remove(def))
            MapEventsManager.Get()?.MarkBlackMarketSold(def);
        fixer.GainReputation(Settings.reputationPerPurchase);
        Messages.Message("NR_FixerDelivery".Translate(fixer.Name, def.label),
            new LookTargets(dropSpot, map), MessageTypeDefOf.PositiveEvent, historical: true);
        fixer.AfterDeal();
    }

    private static void Reject(string text) => Messages.Message(text, MessageTypeDefOf.RejectInput, historical: false);
}
