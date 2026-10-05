using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>The Club node (mockup 4-club-contacts): pinned contacts first, then the rotating pool.</summary>
[StaticConstructorOnStartup]
public static class FixersTab
{
    private const float CardHeight = 124f;
    private const float CardGap = 12f;
    private const float PortraitSize = 96f;
    private const float ButtonHeight = 34f;
    private const float SectionHeight = 36f;

    public static void Draw(Rect rect, Map map, ref Vector2 scroll)
    {
        FixerManager manager = FixerManager.Get();
        if (manager == null || map == null)
            return;

        List<Fixer> pinned = manager.Fixers.Where(f => f.Pinned).ToList();
        List<Fixer> pool = manager.Fixers.Where(f => !f.Pinned).ToList();

        float width = rect.width - 16f;
        float height = (pinned.Count > 0 ? SectionHeight : 0f) + SectionHeight + manager.Fixers.Count * (CardHeight + CardGap);
        var viewRect = new Rect(0f, 0f, width, height);

        Widgets.BeginScrollView(rect, ref scroll, viewRect);
        float y = 0f;
        if (pinned.Count > 0)
        {
            y = NetrunUI.Section(new Rect(0f, y, width, SectionHeight), "NR_ClubPinned".Translate(pinned.Count));
            foreach (Fixer fixer in pinned)
            {
                DrawFixer(new Rect(0f, y, width, CardHeight), fixer, map);
                y += CardHeight + CardGap;
            }
        }

        y = NetrunUI.Section(new Rect(0f, y, width, SectionHeight),
            "NR_ClubPool".Translate(pool.Count, manager.TicksUntilRotation.ToStringTicksToPeriod()));
        foreach (Fixer fixer in pool)
        {
            DrawFixer(new Rect(0f, y, width, CardHeight), fixer, map);
            y += CardHeight + CardGap;
        }
        Widgets.EndScrollView();
    }

    private static Color SpecialtyColor(FixerSpecialty specialty) => specialty switch
    {
        FixerSpecialty.Broker => NetrunUI.Accent,
        FixerSpecialty.Cleaner => NetrunUI.Ok,
        _ => NetrunUI.Purple,
    };

    private static Color TraitColor(Fixer fixer)
    {
        if (!fixer.TraitKnown)
            return NetrunUI.Muted;
        return fixer.Trait == FixerTrait.Reliable ? NetrunUI.Ok : NetrunUI.Bad;
    }

    private static void DrawFixer(Rect rect, Fixer fixer, Map map)
    {
        Color stripe = fixer.Pinned ? NetrunUI.Warn : SpecialtyColor(fixer.Specialty);
        NetrunUI.Card(rect, stripe);
        Rect inner = new Rect(rect.x + 4f, rect.y, rect.width - 4f, rect.height).ContractedBy(12f);

        // Portrait in a frame; a gold pin on the corner of a pinned contact.
        var portraitRect = new Rect(inner.x, inner.y + (inner.height - PortraitSize) / 2f, PortraitSize, PortraitSize);
        NetrunUI.Box(portraitRect, NetrunUI.BgWindow, stripe, 2);
        GUI.DrawTexture(portraitRect.ContractedBy(4f), NetrunTex.Portrait(fixer.Specialty));
        if (fixer.Pinned)
        {
            var pinRect = new Rect(portraitRect.xMax - 14f, portraitRect.y - 8f, 22f, 22f);
            GUI.color = NetrunUI.Warn;
            GUI.DrawTexture(pinRect, NetrunTex.Pinned);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(pinRect, "NR_FixerPinnedTag".Translate());
        }

        float x = portraitRect.xMax + 14f;
        float width = inner.xMax - x;
        float y = inner.y;

        // Name, then specialty and trait as chips.
        Text.Font = GameFont.Medium;
        float nameWidth = Mathf.Min(Text.CalcSize(fixer.Name).x, width * 0.5f);
        Widgets.Label(new Rect(x, y - 2f, nameWidth + 4f, 30f), fixer.Name);
        Text.Font = GameFont.Small;
        float chipX = x + nameWidth + 10f;
        chipX += NetrunUI.Chip(chipX, y + 4f, fixer.SpecialtyLabel.ToUpper(), SpecialtyColor(fixer.Specialty), fixer.SpecialtyDescription) + 6f;
        NetrunUI.Chip(chipX, y + 4f, fixer.TraitLabel.ToUpper(), TraitColor(fixer), fixer.TraitDescription);
        y += 28f;

        // Status line.
        Widgets.Label(new Rect(x, y, width, 22f), StatusText(fixer).Colorize(NetrunUI.Muted));
        y += 22f;

        // Reputation: bar with the pin mark, the number, and how far to pinning.
        string repLabel = "NR_FixerReputationLabel".Translate();
        float repLabelWidth = Text.CalcSize(repLabel).x + 8f;
        Widgets.Label(new Rect(x, y, repLabelWidth, 22f), repLabel);
        float pinAt = NetrunDefOf.NR_Fixers.pinnedReputation;
        var barRect = new Rect(x + repLabelWidth, y + 5f, Mathf.Min(200f, width - repLabelWidth - 160f), 12f);
        Color repColor = NetrunTex.Gauge(fixer.Reputation);
        NetrunUI.Bar(barRect, fixer.Reputation / 100f, repColor);
        GUI.color = NetrunUI.Warn;
        Widgets.DrawLineVertical(barRect.x + barRect.width * pinAt / 100f, barRect.y - 3f, barRect.height + 6f);
        GUI.color = Color.white;
        string repText = fixer.Reputation.ToString("0").Colorize(repColor);
        if (!fixer.Pinned)
            repText += "  " + "NR_FixerToPin".Translate(Mathf.CeilToInt(pinAt - fixer.Reputation)).Colorize(NetrunUI.Muted);
        Widgets.Label(new Rect(barRect.xMax + 8f, y, width - (barRect.xMax + 8f - x), 22f), repText);
        TooltipHandler.TipRegion(new Rect(x, y, width, 22f), "NR_FixerReputationTip".Translate(pinAt.ToString("0")));
        y += 28f;

        DrawButtons(new Rect(x, inner.yMax - ButtonHeight, width, ButtonHeight), fixer, map);
    }

    private static string StatusText(Fixer fixer)
    {
        if (fixer.IsSilent)
            return "NR_FixerStatusSilent".Translate((fixer.SilentUntilTick - Find.TickManager.TicksGame).ToStringTicksToPeriod());
        if (fixer.ChannelOpen)
            return "NR_FixerStatusOpen".Translate(fixer.ChannelTicksLeft.ToStringTicksToPeriod());
        return "NR_FixerStatusClosed".Translate();
    }

    /// <summary>A row of buttons: call, scrub the trace, shop. Unavailable ones stay visible and explain why.</summary>
    private static void DrawButtons(Rect rect, Fixer fixer, Map map)
    {
        float x = rect.x;

        bool Add(string label, Color color, AcceptanceReport report, string tip)
        {
            float w = NetrunUI.ButtonWidth(label);
            bool clicked = NetrunUI.Button(new Rect(x, rect.y, w, rect.height), label, color, report, tip);
            x += w + 8f;
            return clicked;
        }

        AcceptanceReport silent = fixer.IsSilent ? (AcceptanceReport)StatusText(fixer) : true;

        if (!fixer.ChannelOpen)
        {
            if (Add("NR_FixerCall".Translate(fixer.EffectiveCallCost), NetrunUI.Warn, silent,
                    "NR_FixerCallTip".Translate(NetrunDefOf.NR_Fixers.channelHours)))
                FixerDeals.Call(fixer, map);
        }

        AcceptanceReport needsChannel = !silent.Accepted ? silent : fixer.ChannelOpen ? true : (AcceptanceReport)"NR_FixerAfterCall".Translate();
        if (Add("NR_FixerScrub".Translate(), NetrunUI.Accent, needsChannel, "NR_FixerScrubTip".Translate()))
            Find.WindowStack.Add(new Dialog_FixerScrub(fixer, map));

        if (fixer.HasShop && Add("NR_FixerShop".Translate(), NetrunUI.Ok, needsChannel, "NR_FixerShopTip".Translate()))
            Find.WindowStack.Add(new Dialog_FixerShop(fixer, map));
    }
}
