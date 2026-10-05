using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

[StaticConstructorOnStartup]
public static class FixersTab
{
    private const float HeaderHeight = 56f;
    private const float RowHeight = 132f;
    private const float RowGap = 8f;
    private const float PortraitSize = 96f;
    private const float ButtonWidth = 210f;
    private const float ButtonHeight = 32f;

    private static readonly Color ReputationColor = new(0.85f, 0.70f, 0.25f);
    private static readonly Texture2D ReputationTex = SolidColorMaterials.NewSolidColorTexture(ReputationColor);

    public static void Draw(Rect rect, Map map, ref Vector2 scroll)
    {
        FixerManager manager = FixerManager.Get();
        if (manager == null || map == null)
            return;

        DrawHeader(new Rect(rect.x, rect.y, rect.width, HeaderHeight), map, manager);

        var listRect = new Rect(rect.x, rect.y + HeaderHeight, rect.width, rect.height - HeaderHeight);
        IReadOnlyList<Fixer> fixers = manager.Fixers;
        var viewRect = new Rect(0f, 0f, listRect.width - 16f, fixers.Count * (RowHeight + RowGap));

        Widgets.BeginScrollView(listRect, ref scroll, viewRect);
        float y = 0f;
        foreach (Fixer fixer in fixers)
        {
            DrawFixer(new Rect(0f, y, viewRect.width, RowHeight), fixer, map);
            y += RowHeight + RowGap;
        }
        Widgets.EndScrollView();
    }

    private static void DrawHeader(Rect rect, Map map, FixerManager manager)
    {
        int silver = ColonyGoods.Count(map, ThingDefOf.Silver);
        int data = ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata);
        int advanced = ColonyGoods.Count(map, NetrunDefOf.USH_AdvancedCyberdata);

        Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f),
            "NR_FixersResources".Translate(silver, data, advanced));
        GUI.color = NetrunTex.DimText;
        Widgets.Label(new Rect(rect.x, rect.y + 26f, rect.width, 24f),
            "NR_FixersRotation".Translate(manager.TicksUntilRotation.ToStringTicksToPeriod()));
        GUI.color = Color.white;
    }

    private static void DrawFixer(Rect rect, Fixer fixer, Map map)
    {
        Widgets.DrawOptionBackground(rect, selected: false);
        Rect inner = rect.ContractedBy(10f);

        // Portrait by specialty.
        var portraitRect = new Rect(inner.x, inner.y + (inner.height - PortraitSize) / 2f, PortraitSize, PortraitSize);
        GUI.DrawTexture(portraitRect, NetrunTex.Portrait(fixer.Specialty));

        float textX = portraitRect.xMax + 12f;
        float textWidth = inner.xMax - ButtonWidth - 12f - textX;

        // Name and pin.
        Text.Font = GameFont.Medium;
        string name = fixer.Name.Colorize(NetrunTex.Accent);
        Widgets.Label(new Rect(textX, inner.y, textWidth, 32f), name);
        if (fixer.Pinned)
        {
            // A gold pin right after the name instead of a [Pinned] tag.
            float nameWidth = Mathf.Min(Text.CalcSize(fixer.Name).x, textWidth - 30f);
            var pinRect = new Rect(textX + nameWidth + 6f, inner.y + 3f, 24f, 24f);
            GUI.color = ColorLibrary.Gold;
            GUI.DrawTexture(pinRect, NetrunTex.Pinned);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(pinRect, "NR_FixerPinnedTag".Translate());
        }
        Text.Font = GameFont.Small;

        // Specialty and trait with tooltips.
        var specialtyRect = new Rect(textX, inner.y + 32f, textWidth, 22f);
        Widgets.Label(specialtyRect, "NR_FixerSpecialtyLine".Translate(fixer.SpecialtyLabel));
        Widgets.DrawHighlightIfMouseover(specialtyRect);
        TooltipHandler.TipRegion(specialtyRect, fixer.SpecialtyDescription);

        var traitRect = new Rect(textX, inner.y + 54f, textWidth, 22f);
        Widgets.Label(traitRect, "NR_FixerTraitLine".Translate(fixer.TraitLabel));
        Widgets.DrawHighlightIfMouseover(traitRect);
        TooltipHandler.TipRegion(traitRect, fixer.TraitDescription);

        // Reputation: readable label next to the bar, the bar marks the pin threshold.
        var repLabelRect = new Rect(textX, inner.y + 78f, 170f, 22f);
        Widgets.Label(repLabelRect, "NR_FixerReputation".Translate(fixer.Reputation.ToString("0").Colorize(NetrunTex.Gauge(fixer.Reputation))));
        var barRect = new Rect(repLabelRect.xMax + 6f, inner.y + 82f, textWidth - repLabelRect.width - 6f, 14f);
        Widgets.FillableBar(barRect, fixer.Reputation / 100f, ReputationTex, BaseContent.BlackTex, doBorder: true);
        float pinX = barRect.x + barRect.width * NetrunDefOf.NR_Fixers.pinnedReputation / 100f;
        Widgets.DrawLineVertical(pinX, barRect.y - 3f, barRect.height + 6f);
        var repTipRect = new Rect(repLabelRect.x, repLabelRect.y, textWidth, 22f);
        TooltipHandler.TipRegion(repTipRect, "NR_FixerReputationTip".Translate(NetrunDefOf.NR_Fixers.pinnedReputation.ToString("0")));

        // Status line.
        GUI.color = NetrunTex.DimText;
        Widgets.Label(new Rect(textX, inner.y + 100f, textWidth, 22f), StatusText(fixer));
        GUI.color = Color.white;

        DrawButtons(new Rect(inner.xMax - ButtonWidth, inner.y, ButtonWidth, inner.height), fixer, map);
    }

    private static string StatusText(Fixer fixer)
    {
        if (fixer.IsSilent)
            return "NR_FixerStatusSilent".Translate((fixer.SilentUntilTick - Find.TickManager.TicksGame).ToStringTicksToPeriod());
        if (fixer.ChannelOpen)
            return "NR_FixerStatusOpen".Translate(fixer.ChannelTicksLeft.ToStringTicksToPeriod());
        return "NR_FixerStatusClosed".Translate();
    }

    private static void DrawButtons(Rect rect, Fixer fixer, Map map)
    {
        float y = rect.y;

        if (fixer.IsSilent)
            return;

        if (!fixer.ChannelOpen)
        {
            var callRect = new Rect(rect.x, y, rect.width, ButtonHeight);
            if (Widgets.ButtonText(callRect, "NR_FixerCall".Translate(fixer.EffectiveCallCost)))
                FixerDeals.Call(fixer, map);
            TooltipHandler.TipRegion(callRect, "NR_FixerCallTip".Translate(NetrunDefOf.NR_Fixers.channelHours));
            return;
        }

        var scrubRect = new Rect(rect.x, y, rect.width, ButtonHeight);
        if (Widgets.ButtonText(scrubRect, "NR_FixerScrub".Translate()))
            Find.WindowStack.Add(new Dialog_FixerScrub(fixer, map));
        TooltipHandler.TipRegion(scrubRect, "NR_FixerScrubTip".Translate());
        y += ButtonHeight + 6f;

        if (fixer.HasShop)
        {
            var shopRect = new Rect(rect.x, y, rect.width, ButtonHeight);
            if (Widgets.ButtonText(shopRect, "NR_FixerShop".Translate()))
                Find.WindowStack.Add(new Dialog_FixerShop(fixer, map));
            TooltipHandler.TipRegion(shopRect, "NR_FixerShopTip".Translate());
        }
    }
}
