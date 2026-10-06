using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The Board node (mockup 6-contracts-board): the active contract on top with progress, reward and penalty,
/// then offers as cards colored by fixer, fixers who are not answering dimmed, and a short history.
/// Descriptions wrap; nothing is cut off.
/// </summary>
[StaticConstructorOnStartup]
public static class ContractsTab
{
    private const float SectionHeight = 36f;
    private const float CardGap = 10f;
    private const float Pad = 12f;
    private const float IconSize = 64f;
    private const float ButtonHeight = 34f;
    private const float RightColumn = 140f;

    private static FixerSettingsDef Settings => NetrunDefOf.NR_Fixers;

    public static void Draw(Rect rect, Map map, ref Vector2 scroll)
    {
        ContractManager contracts = ContractManager.Get();
        FixerManager fixers = FixerManager.Get();
        if (contracts == null || fixers == null || map == null)
            return;

        List<Contract> active = contracts.Active.ToList();
        var offers = fixers.Fixers.Where(f => !f.IsSilent)
            .SelectMany(f => contracts.OffersOf(f).Select(c => (contract: c, fixer: f))).ToList();
        List<Fixer> silent = fixers.Fixers.Where(f => f.IsSilent).ToList();

        float width = rect.width - 16f;
        float height = 3 * SectionHeight + 40f
                       + (active.Count == 0 ? 30f : active.Sum(c => ActiveHeight(c, width) + CardGap))
                       + offers.Sum(o => OfferHeight(o.contract, width) + CardGap)
                       + silent.Count * (60f + CardGap) + (offers.Count + silent.Count == 0 ? 30f : 0f);
        var view = new Rect(0f, 0f, width, height);

        Widgets.BeginScrollView(rect, ref scroll, view);
        float y = 0f;

        float gAct = y;
        y = NetrunUI.Section(new Rect(0f, y, width, SectionHeight), "NR_BoardActive".Translate());
        if (active.Count == 0)
            y = Empty(width, y, "NR_ContractsNoActive".Translate());
        foreach (Contract contract in active)
        {
            float h = ActiveHeight(contract, width);
            DrawActive(new Rect(0f, y, width, h), contract, fixers.ById(contract.FixerId), map, contracts);
            y += h + CardGap;
        }

        GuideRegistry.InScroll("board.active", rect, scroll, new Rect(0f, gAct, width, y - gAct));
        float gOff = y;
        y = NetrunUI.Section(new Rect(0f, y, width, SectionHeight), "NR_BoardOffers".Translate(offers.Count));
        if (offers.Count + silent.Count == 0)
            y = Empty(width, y, "NR_ContractsNoOffers".Translate());
        foreach ((Contract contract, Fixer fixer) in offers)
        {
            float h = OfferHeight(contract, width);
            DrawOffer(new Rect(0f, y, width, h), contract, fixer, contracts);
            y += h + CardGap;
        }
        GuideRegistry.InScroll("board.offers", rect, scroll, new Rect(0f, gOff, width, y - gOff));
        foreach (Fixer fixer in silent)
        {
            DrawSilent(new Rect(0f, y, width, 60f), fixer);
            y += 60f + CardGap;
            GuideRegistry.InScroll("board.offers", rect, scroll, new Rect(0f, gOff, width, y - gOff));
        }

        y = NetrunUI.Section(new Rect(0f, y, width, SectionHeight), "NR_BoardHistory".Translate());
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(0f, y, width, 36f), HistoryLine(contracts));
        Text.Font = GameFont.Small;

        Widgets.EndScrollView();
    }

    private static float Empty(float width, float y, string text)
    {
        Widgets.Label(new Rect(0f, y, width, 26f), text.Colorize(NetrunUI.Muted));
        return y + 30f;
    }

    // ---- Shared bits ----

    private static Texture2D TypeIcon(ContractType type) => type switch
    {
        ContractType.DataDelivery => NetrunTex.TabIcon(2),
        ContractType.SystemTap => NetrunTex.Cyberspace,
        _ => NetrunTex.HeatIcon(HeatLevel.Shadow),
    };

    /// <summary>1-3 squares: where the goal sits in its type's range.</summary>
    private static int Difficulty(Contract contract)
    {
        IntRange range = contract.Type switch
        {
            ContractType.DataDelivery => Settings.dataDeliveryGoal,
            ContractType.SystemTap => Settings.systemTapGoal,
            _ => Settings.quietRunGoal,
        };
        float t = range.max > range.min ? (float)(contract.Goal - range.min) / (range.max - range.min) : 0.5f;
        return 1 + Mathf.Clamp(Mathf.RoundToInt(t * 2f), 0, 2);
    }

    private static void DrawIcon(Rect card, ContractType type, Color color)
    {
        var box = new Rect(card.x + 4f + Pad, card.y + Pad, IconSize, IconSize);
        NetrunUI.Box(box, NetrunUI.BgWindow, color, 2);
        GUI.DrawTexture(box.ContractedBy(10f), TypeIcon(type));
    }

    /// <summary>Title, then the fixer as a tag, then extra tags; returns the x after the last tag.</summary>
    private static float TitleLine(float x, float y, Contract contract, Fixer fixer, bool dim = false)
    {
        Text.Font = GameFont.Medium;
        float w = Text.CalcSize(contract.Title).x;
        Widgets.Label(new Rect(x, y - 2f, w + 4f, 30f), dim ? contract.Title.Colorize(NetrunUI.Muted) : contract.Title);
        Text.Font = GameFont.Small;
        x += w + 10f;
        if (fixer != null)
        {
            Color c = dim ? NetrunUI.Muted : FixersTab.SpecialtyColor(fixer.Specialty);
            x += NetrunUI.Chip(x, y + 4f, fixer.Name, c, fixer.SpecialtyLabel) + 6f;
            if (fixer.TraitKnown && fixer.Trait != FixerTrait.Reliable && !dim)
                x += NetrunUI.Chip(x, y + 4f, fixer.TraitLabel, NetrunUI.Bad, fixer.TraitDescription) + 6f;
        }
        return x;
    }

    // ---- Active ----

    private static float TextWidth(float width, bool rightColumn) => width - (4f + Pad + IconSize + 14f) - Pad - (rightColumn ? RightColumn + 12f : 0f);

    private static float ActiveHeight(Contract contract, float width)
    {
        float desc = Text.CalcHeight(contract.Description, TextWidth(width, false));
        float content = 28f + desc + 6f + 22f + 6f + 24f + 8f + ButtonHeight;
        return Mathf.Max(IconSize, content) + 2 * Pad;
    }

    private static void DrawActive(Rect rect, Contract contract, Fixer fixer, Map map, ContractManager contracts)
    {
        Color color = NetrunUI.Ok;
        NetrunUI.Card(rect, color);
        DrawIcon(rect, contract.Type, color);

        float x = rect.x + 4f + Pad + IconSize + 14f;
        float width = TextWidth(rect.width, false);
        float y = rect.y + Pad;

        float after = TitleLine(x, y, contract, fixer);
        Color due = contract.TicksLeft < GenDate.TicksPerDay ? NetrunUI.Bad : NetrunUI.Warn;
        NetrunUI.Chip(after, y + 4f, "NR_BoardDue".Translate(contract.TicksLeft.ToStringTicksToPeriod()), due);
        y += 28f;

        float desc = Text.CalcHeight(contract.Description, width);
        Widgets.Label(new Rect(x, y, width, desc), contract.Description.Colorize(NetrunUI.Muted));
        y += desc + 6f;

        // Progress bar with the count written on it.
        var bar = new Rect(x, y, width, 22f);
        NetrunUI.Bar(bar, (float)contract.Progress / contract.Goal, NetrunUI.Ok);
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(bar, "NR_BoardProgress".Translate(contract.Progress, contract.Goal));
        Text.Anchor = TextAnchor.UpperLeft;
        y += 28f;

        string reward = "NR_BoardReward".Translate(contract.Reward, Settings.contractReputation.ToString("0"));
        string fail = "NR_BoardFail".Translate(Settings.failReputationLoss.ToString("0"), Settings.failSilentDays);
        float rw = Text.CalcSize(reward).x + 16f;
        Widgets.Label(new Rect(x, y, rw, 24f), reward.Colorize(NetrunUI.Ok));
        Widgets.Label(new Rect(x + rw, y, width - rw, 24f), fail.Colorize(NetrunUI.Bad));
        y += 32f;

        float bx = x;
        if (contract.Type == ContractType.DataDelivery)
        {
            int have = ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata) + ColonyGoods.Count(map, NetrunDefOf.USH_AdvancedCyberdata) * Settings.advancedCyberdataWorth;
            string deliver = "NR_BoardDeliver".Translate(have);
            float w = NetrunUI.ButtonWidth(deliver);
            AcceptanceReport canDeliver = have > 0 ? true : (AcceptanceReport)"NR_NotEnoughCyberdata".Translate();
            if (NetrunUI.Button(new Rect(bx, y, w, ButtonHeight), deliver, NetrunUI.Ok, canDeliver,
                    "NR_ContractDeliverTip".Translate(Settings.advancedCyberdataWorth)))
                contracts.Deliver(contract, map);
            bx += w + 8f;
        }

        string abandon = "NR_ContractAbandon".Translate();
        if (NetrunUI.Button(new Rect(bx, y, NetrunUI.ButtonWidth(abandon), ButtonHeight), abandon, NetrunUI.Bad, true))
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "NR_ContractAbandonConfirm".Translate(contract.Title), () => contracts.Abandon(contract), destructive: true));
        }
    }

    // ---- Offers ----

    private static float OfferHeight(Contract contract, float width)
    {
        float desc = Text.CalcHeight(contract.Description, TextWidth(width, true));
        float content = 28f + desc + 4f + 22f;
        return Mathf.Max(Mathf.Max(IconSize, content), 24f + 20f + ButtonHeight + 4f) + 2 * Pad;
    }

    private static void DrawOffer(Rect rect, Contract contract, Fixer fixer, ContractManager contracts)
    {
        Color color = FixersTab.SpecialtyColor(fixer.Specialty);
        NetrunUI.Card(rect, color);
        DrawIcon(rect, contract.Type, color);

        float x = rect.x + 4f + Pad + IconSize + 14f;
        float width = TextWidth(rect.width, true);
        float y = rect.y + Pad;

        TitleLine(x, y, contract, fixer);
        y += 28f;
        float desc = Text.CalcHeight(contract.Description, width);
        Widgets.Label(new Rect(x, y, width, desc), contract.Description.Colorize(NetrunUI.Muted));
        y += desc + 4f;

        // Difficulty squares and duration.
        string difficulty = "NR_BoardDifficulty".Translate();
        float dw = Text.CalcSize(difficulty).x + 6f;
        Widgets.Label(new Rect(x, y, dw, 22f), difficulty);
        int level = Difficulty(contract);
        Color squares = level == 1 ? NetrunUI.Ok : level == 2 ? NetrunUI.Warn : NetrunUI.Bad;
        for (int i = 0; i < 3; i++)
            Widgets.DrawBoxSolid(new Rect(x + dw + i * 14f, y + 5f, 11f, 11f), i < level ? squares : new Color(1f, 1f, 1f, 0.12f));
        Widgets.Label(new Rect(x + dw + 52f, y, width - dw - 52f, 22f), "NR_BoardDuration".Translate(contract.DurationTicks.ToStringTicksToPeriod()));

        // Right column: reward, reputation, take.
        float rx = rect.xMax - Pad - RightColumn;
        float ry = rect.y + Pad;
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(new Rect(rx, ry, RightColumn, 24f), "NR_BoardSilver".Translate(contract.Reward).Colorize(NetrunUI.Ok));
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(rx, ry + 24f, RightColumn, 18f), "NR_BoardRep".Translate(Settings.contractReputation.ToString("0")).Colorize(NetrunUI.Ok));
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        AcceptanceReport report = contracts.CanAccept(contract, fixer);
        string label = contracts.HasActive(fixer) ? "NR_BoardOneActive".Translate() : (string)"NR_ContractAccept".Translate();
        float w = NetrunUI.ButtonWidth(label);
        if (NetrunUI.Button(new Rect(rect.xMax - Pad - w, ry + 46f, w, ButtonHeight), label, NetrunUI.Ok, report))
            contracts.Accept(contract, fixer);
    }

    private static void DrawSilent(Rect rect, Fixer fixer)
    {
        NetrunUI.Card(rect, new Color(0.6f, 0.5f, 0.2f));
        Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.25f));
        float x = rect.x + 4f + Pad;
        NetrunUI.Chip(x, rect.y + 10f, fixer.Name, NetrunUI.Muted);
        Widgets.Label(new Rect(x, rect.y + 32f, rect.width - 200f, 22f), "NR_BoardSilentText".Translate().Colorize(NetrunUI.Muted));
        string left = "NR_BoardSilentFor".Translate((fixer.SilentUntilTick - Find.TickManager.TicksGame).ToStringTicksToPeriod());
        Text.Font = GameFont.Tiny;
        float w = Text.CalcSize(left).x + 12f;
        Text.Font = GameFont.Small;
        NetrunUI.Chip(rect.xMax - Pad - w, rect.y + 20f, left, NetrunUI.Bad);
    }

    private static string HistoryLine(ContractManager contracts)
    {
        if (contracts.History.Count == 0)
            return "NR_BoardNoHistory".Translate().Colorize(NetrunUI.Muted);
        return string.Join("  ·  ", contracts.History.Select(r =>
        {
            string title = ("NR_Contract_" + r.Type).Translate();
            return r.Success
                ? "✔ ".Colorize(NetrunUI.Ok) + "NR_BoardHistoryDone".Translate(title, r.FixerName, r.Reward)
                : "✘ ".Colorize(NetrunUI.Bad) + "NR_BoardHistoryFailed".Translate(title, r.FixerName);
        }));
    }

    /// <summary>Title-bar chips for the Board: active contracts and the active fixer's reputation.</summary>
    public static void HeaderChips(float xMax, float y)
    {
        ContractManager contracts = ContractManager.Get();
        FixerManager fixers = FixerManager.Get();
        if (contracts == null || fixers == null)
            return;
        Contract active = contracts.Active.FirstOrDefault();
        float x = xMax;
        Fixer fixer = active == null ? null : fixers.ById(active.FixerId);
        if (fixer != null)
            x = NetrunUI.HeaderChip(x, y, "NR_BoardRepChip".Translate(fixer.Name, fixer.Reputation.ToString("0"))) - 8f;
        NetrunUI.HeaderChip(x, y, "NR_BoardActiveChip".Translate(contracts.Active.Count(), fixers.Fixers.Count));
    }
}
