using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

public static class ContractsTab
{
    private const float SectionHeight = 28f;
    private const float RowHeight = 76f;
    private const float RowGap = 6f;
    private const float ButtonWidth = 160f;
    private const float ButtonHeight = 28f;

    public static void Draw(Rect rect, Map map, ref Vector2 scroll)
    {
        ContractManager contracts = ContractManager.Get();
        FixerManager fixers = FixerManager.Get();
        if (contracts == null || fixers == null || map == null)
            return;

        List<Contract> active = contracts.Active.ToList();
        var offers = fixers.Fixers.SelectMany(f => contracts.OffersOf(f).Select(c => (contract: c, fixer: f))).ToList();

        float height = 2 * SectionHeight + (Mathf.Max(1, active.Count) + Mathf.Max(1, offers.Count)) * (RowHeight + RowGap);
        var viewRect = new Rect(0f, 0f, rect.width - 16f, height);

        Widgets.BeginScrollView(rect, ref scroll, viewRect);
        float y = 0f;

        y = DrawSectionHeader(viewRect, y, "NR_ContractsActive".Translate());
        if (active.Count == 0)
            y = DrawEmpty(viewRect, y, "NR_ContractsNoActive".Translate());
        foreach (Contract contract in active)
        {
            DrawActive(new Rect(0f, y, viewRect.width, RowHeight), contract, fixers.ById(contract.FixerId), map, contracts);
            y += RowHeight + RowGap;
        }

        y = DrawSectionHeader(viewRect, y, "NR_ContractsOffers".Translate());
        if (offers.Count == 0)
            DrawEmpty(viewRect, y, "NR_ContractsNoOffers".Translate());
        foreach ((Contract contract, Fixer fixer) in offers)
        {
            DrawOffer(new Rect(0f, y, viewRect.width, RowHeight), contract, fixer, contracts);
            y += RowHeight + RowGap;
        }

        Widgets.EndScrollView();
    }

    private static float DrawSectionHeader(Rect viewRect, float y, string label)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, y, viewRect.width, SectionHeight), label);
        Text.Font = GameFont.Small;
        return y + SectionHeight;
    }

    private static float DrawEmpty(Rect viewRect, float y, string label)
    {
        GUI.color = Color.gray;
        Widgets.Label(new Rect(8f, y, viewRect.width, 24f), label);
        GUI.color = Color.white;
        return y + RowHeight + RowGap;
    }

    private static Rect DrawCard(Rect rect, Contract contract, string fixerName, out float textWidth)
    {
        Widgets.DrawOptionBackground(rect, selected: false);
        Rect inner = rect.ContractedBy(6f);
        textWidth = inner.width - ButtonWidth - 10f;

        Widgets.Label(new Rect(inner.x, inner.y, textWidth, 22f),
            "NR_ContractHeader".Translate(contract.Title, fixerName, contract.Reward));
        GUI.color = Color.gray;
        Widgets.Label(new Rect(inner.x, inner.y + 22f, textWidth, 22f), contract.Description);
        GUI.color = Color.white;
        return inner;
    }

    private static void DrawActive(Rect rect, Contract contract, Fixer fixer, Map map, ContractManager contracts)
    {
        Rect inner = DrawCard(rect, contract, fixer?.Name ?? "?", out float textWidth);

        var barRect = new Rect(inner.x, inner.y + 46f, textWidth * 0.55f, 18f);
        Widgets.FillableBar(barRect, (float)contract.Progress / contract.Goal, BaseContent.WhiteTex, BaseContent.BlackTex, doBorder: true);
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = Color.black;
        Widgets.Label(barRect, $"{contract.Progress} / {contract.Goal}");
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;

        Widgets.Label(new Rect(barRect.xMax + 10f, barRect.y - 2f, textWidth - barRect.width - 10f, 22f),
            "NR_ContractTimeLeft".Translate(contract.TicksLeft.ToStringTicksToPeriod()));

        float x = inner.xMax - ButtonWidth;
        if (contract.Type == ContractType.DataDelivery
            && Widgets.ButtonText(new Rect(x, inner.y, ButtonWidth, ButtonHeight), "NR_ContractDeliver".Translate()))
        {
            contracts.Deliver(contract, map);
        }

        if (Widgets.ButtonText(new Rect(x, inner.y + ButtonHeight + 6f, ButtonWidth, ButtonHeight), "NR_ContractAbandon".Translate()))
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "NR_ContractAbandonConfirm".Translate(contract.Title),
                () => contracts.Abandon(contract),
                destructive: true));
        }
    }

    private static void DrawOffer(Rect rect, Contract contract, Fixer fixer, ContractManager contracts)
    {
        Rect inner = DrawCard(rect, contract, fixer.Name, out float textWidth);

        Widgets.Label(new Rect(inner.x, inner.y + 46f, textWidth, 22f),
            "NR_ContractDuration".Translate(contract.DurationTicks.ToStringTicksToPeriod()));

        var buttonRect = new Rect(inner.xMax - ButtonWidth, inner.y, ButtonWidth, ButtonHeight);
        AcceptanceReport report = contracts.CanAccept(contract, fixer);
        if (!report.Accepted)
        {
            Widgets.ButtonText(buttonRect, "NR_ContractAccept".Translate(), active: false);
            TooltipHandler.TipRegion(buttonRect, report.Reason);
            return;
        }

        if (Widgets.ButtonText(buttonRect, "NR_ContractAccept".Translate()))
            contracts.Accept(contract, fixer);
    }
}
