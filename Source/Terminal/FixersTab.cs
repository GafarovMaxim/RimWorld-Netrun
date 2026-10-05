using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

public static class FixersTab
{
    private const float HeaderHeight = 52f;
    private const float RowHeight = 118f;
    private const float RowGap = 8f;
    private const float ButtonWidth = 200f;
    private const float ButtonHeight = 30f;

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
        GUI.color = Color.gray;
        Widgets.Label(new Rect(rect.x, rect.y + 24f, rect.width, 24f),
            "NR_FixersRotation".Translate(manager.TicksUntilRotation.ToStringTicksToPeriod()));
        GUI.color = Color.white;
    }

    private static void DrawFixer(Rect rect, Fixer fixer, Map map)
    {
        Widgets.DrawOptionBackground(rect, selected: false);
        Rect inner = rect.ContractedBy(8f);
        float textWidth = inner.width - ButtonWidth - 12f;

        // Name and pin.
        Text.Font = GameFont.Medium;
        string name = fixer.Name;
        if (fixer.Pinned)
            name += " " + "NR_FixerPinnedTag".Translate().Colorize(ColorLibrary.Gold);
        Widgets.Label(new Rect(inner.x, inner.y, textWidth, 30f), name);
        Text.Font = GameFont.Small;

        // Specialty and trait with tooltips.
        var specialtyRect = new Rect(inner.x, inner.y + 30f, textWidth / 2f, 22f);
        Widgets.Label(specialtyRect, "NR_FixerSpecialtyLine".Translate(fixer.SpecialtyLabel));
        TooltipHandler.TipRegion(specialtyRect, fixer.SpecialtyDescription);

        var traitRect = new Rect(inner.x + textWidth / 2f, inner.y + 30f, textWidth / 2f, 22f);
        Widgets.Label(traitRect, "NR_FixerTraitLine".Translate(fixer.TraitLabel));
        TooltipHandler.TipRegion(traitRect, fixer.TraitDescription);

        // Reputation bar.
        var barRect = new Rect(inner.x, inner.y + 56f, textWidth, 18f);
        Widgets.FillableBar(barRect, fixer.Reputation / 100f, BaseContent.WhiteTex, BaseContent.BlackTex, doBorder: true);
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = Color.black;
        Widgets.Label(barRect, "NR_FixerReputation".Translate(fixer.Reputation.ToString("0")));
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(barRect, "NR_FixerReputationTip".Translate(NetrunDefOf.NR_Fixers.pinnedReputation.ToString("0")));

        // Status line.
        GUI.color = Color.gray;
        Widgets.Label(new Rect(inner.x, inner.y + 78f, textWidth, 22f), StatusText(fixer));
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
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, ButtonHeight), "NR_FixerCall".Translate(fixer.EffectiveCallCost)))
                FixerDeals.Call(fixer, map);
            return;
        }

        if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, ButtonHeight), "NR_FixerScrub".Translate()))
            Find.WindowStack.Add(new Dialog_FixerScrub(fixer, map));
        y += ButtonHeight + 6f;

        if (fixer.HasShop && Widgets.ButtonText(new Rect(rect.x, y, rect.width, ButtonHeight), "NR_FixerShop".Translate()))
            Find.WindowStack.Add(new Dialog_FixerShop(fixer, map));
    }
}
