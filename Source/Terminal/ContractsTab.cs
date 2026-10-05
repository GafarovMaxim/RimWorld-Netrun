using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

[StaticConstructorOnStartup]
public static class ContractsTab
{
    private const float SectionHeight = 32f;
    private const float RowGap = 8f;
    private const float Padding = 8f;
    private const float LineHeight = 24f;
    private const float ButtonWidth = 170f;
    private const float ButtonHeight = 30f;

    private static readonly Texture2D ProgressTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.25f, 0.75f, 0.85f));

    public static void Draw(Rect rect, Map map, ref Vector2 scroll)
    {
        ContractManager contracts = ContractManager.Get();
        FixerManager fixers = FixerManager.Get();
        if (contracts == null || fixers == null || map == null)
            return;

        List<Contract> active = contracts.Active.ToList();
        var offers = fixers.Fixers.SelectMany(f => contracts.OffersOf(f).Select(c => (contract: c, fixer: f))).ToList();

        float width = rect.width - 16f;
        float height = 2 * SectionHeight
            + (active.Count == 0 ? LineHeight + RowGap : active.Sum(c => CardHeight(c, width, isActive: true) + RowGap))
            + (offers.Count == 0 ? LineHeight + RowGap : offers.Sum(o => CardHeight(o.contract, width, isActive: false) + RowGap));
        var viewRect = new Rect(0f, 0f, width, height);

        Widgets.BeginScrollView(rect, ref scroll, viewRect);
        float y = 0f;

        y = DrawSectionHeader(width, y, "NR_ContractsActive".Translate());
        if (active.Count == 0)
            y = DrawEmpty(width, y, "NR_ContractsNoActive".Translate());
        foreach (Contract contract in active)
        {
            float h = CardHeight(contract, width, isActive: true);
            DrawActive(new Rect(0f, y, width, h), contract, fixers.ById(contract.FixerId), map, contracts);
            y += h + RowGap;
        }

        y = DrawSectionHeader(width, y, "NR_ContractsOffers".Translate());
        if (offers.Count == 0)
            DrawEmpty(width, y, "NR_ContractsNoOffers".Translate());
        foreach ((Contract contract, Fixer fixer) in offers)
        {
            float h = CardHeight(contract, width, isActive: false);
            DrawOffer(new Rect(0f, y, width, h), contract, fixer, contracts);
            y += h + RowGap;
        }

        Widgets.EndScrollView();
    }

    /// <summary>Title line, wrapped description, one status line, never shorter than the buttons.</summary>
    private static float CardHeight(Contract contract, float width, bool isActive)
    {
        float textWidth = width - 2 * Padding - ButtonWidth - 10f;
        float description = Text.CalcHeight(contract.Description, textWidth);
        float content = LineHeight + description + 4f + LineHeight;
        float buttons = isActive ? 2 * ButtonHeight + 6f : ButtonHeight + 36f;
        return Mathf.Max(content, buttons) + 2 * Padding;
    }

    private static float DrawSectionHeader(float width, float y, string label)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, y, width, SectionHeight), label);
        Text.Font = GameFont.Small;
        return y + SectionHeight;
    }

    private static float DrawEmpty(float width, float y, string label)
    {
        GUI.color = NetrunTex.DimText;
        Widgets.Label(new Rect(8f, y, width, LineHeight), label);
        GUI.color = Color.white;
        return y + LineHeight + RowGap;
    }

    /// <summary>Draws title and description; returns the y below them and the text column width.</summary>
    private static float DrawCard(Rect rect, Contract contract, string fixerName, out Rect inner, out float textWidth)
    {
        Widgets.DrawOptionBackground(rect, selected: false);
        inner = rect.ContractedBy(Padding);
        textWidth = inner.width - ButtonWidth - 10f;

        Widgets.Label(new Rect(inner.x, inner.y, textWidth, LineHeight),
            "NR_ContractHeader".Translate(contract.Title, fixerName, contract.Reward));

        float descHeight = Text.CalcHeight(contract.Description, textWidth);
        GUI.color = NetrunTex.DimText;
        Widgets.Label(new Rect(inner.x, inner.y + LineHeight, textWidth, descHeight), contract.Description);
        GUI.color = Color.white;

        return inner.y + LineHeight + descHeight + 4f;
    }

    private static void DrawActive(Rect rect, Contract contract, Fixer fixer, Map map, ContractManager contracts)
    {
        float y = DrawCard(rect, contract, fixer?.Name ?? "?", out Rect inner, out float textWidth);

        // Readable progress text next to the bar, then the time left.
        string progress = "NR_ContractProgress".Translate(contract.Progress, contract.Goal, contract.Goal - contract.Progress);
        float progressWidth = Text.CalcSize(progress).x + 10f;
        Widgets.Label(new Rect(inner.x, y, progressWidth, LineHeight), progress);

        var barRect = new Rect(inner.x + progressWidth, y + 5f, Mathf.Max(60f, textWidth * 0.3f), 14f);
        Widgets.FillableBar(barRect, (float)contract.Progress / contract.Goal, ProgressTex, BaseContent.BlackTex, doBorder: true);

        GUI.color = NetrunTex.DimText;
        Widgets.Label(new Rect(barRect.xMax + 12f, y, inner.x + textWidth - barRect.xMax - 12f, LineHeight),
            "NR_ContractTimeLeft".Translate(contract.TicksLeft.ToStringTicksToPeriod()));
        GUI.color = Color.white;

        float x = inner.xMax - ButtonWidth;
        if (contract.Type == ContractType.DataDelivery)
        {
            var deliverRect = new Rect(x, inner.y, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(deliverRect, "NR_ContractDeliver".Translate()))
                contracts.Deliver(contract, map);
            TooltipHandler.TipRegion(deliverRect, "NR_ContractDeliverTip".Translate(NetrunDefOf.NR_Fixers.advancedCyberdataWorth));
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
        float y = DrawCard(rect, contract, fixer.Name, out Rect inner, out float textWidth);

        GUI.color = NetrunTex.DimText;
        Widgets.Label(new Rect(inner.x, y, textWidth, LineHeight),
            "NR_ContractDuration".Translate(contract.DurationTicks.ToStringTicksToPeriod()));
        GUI.color = Color.white;

        var buttonRect = new Rect(inner.xMax - ButtonWidth, inner.y, ButtonWidth, ButtonHeight);
        AcceptanceReport report = contracts.CanAccept(contract, fixer);
        if (!report.Accepted)
        {
            Widgets.ButtonText(buttonRect, "NR_ContractAccept".Translate(), active: false);
            TooltipHandler.TipRegion(buttonRect, report.Reason);

            // Say it inline as well: tooltips are easy to miss.
            GUI.color = ColorLibrary.RedReadable;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(buttonRect.x, buttonRect.yMax + 2f, ButtonWidth, 32f), report.Reason);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            return;
        }

        if (Widgets.ButtonText(buttonRect, "NR_ContractAccept".Translate()))
            contracts.Accept(contract, fixer);
    }
}
