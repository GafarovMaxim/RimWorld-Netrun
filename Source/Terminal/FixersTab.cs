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
    private const float CardHeight = 142f;
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

        // The hacker group: favor from cyberspace finds, spent on calling allies.
        GatewayManager gateway = GatewayManager.Get();
        if (gateway != null)
        {
            var groupRect = new Rect(rect.x, rect.y, rect.width, 34f);
            GuideRegistry.Area("club.group", groupRect);
            string favor = "NR_ClubFavor".Translate(gateway.GroupFavor, NetrunDefOf.NR_Cyberspace.groupFavorForAllies);
            Text.Anchor = TextAnchor.MiddleLeft;
            float favorX = groupRect.x;
            if (NetrunTex.GroupFavor is { } favorIcon)
            {
                GUI.DrawTexture(new Rect(favorX, groupRect.center.y - 11f, 22f, 22f), favorIcon);
                favorX += 28f;
            }
            var favorRect = new Rect(favorX, groupRect.y, groupRect.xMax - 220f - favorX, groupRect.height);
            Widgets.Label(favorRect, favor.Colorize(NetrunUI.Purple));
            TooltipHandler.TipRegion(favorRect, "NR_ClubFavorTip".Translate(NetrunDefOf.NR_Cyberspace.groupFavorForAllies, NetrunDefOf.NR_Cyberspace.alliesCooldownDays));
            Text.Anchor = TextAnchor.UpperLeft;
            string call = "NR_ClubCallAllies".Translate();
            Texture2D groupIcon = NetrunTex.HackerGroup;
            float cw = NetrunUI.ButtonWidth(call, groupIcon != null);
            if (NetrunUI.Button(new Rect(groupRect.xMax - cw - 16f, groupRect.y, cw, 30f), call, NetrunUI.Purple, gateway.CanCallAllies(), "NR_ClubCallAlliesTip".Translate(), groupIcon))
                gateway.CallAllies(map);
            rect.yMin += 40f;
        }

        GuideRegistry.Area("club.list", rect);
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

    public static Color SpecialtyColor(FixerSpecialty specialty) => specialty switch
    {
        FixerSpecialty.Broker => NetrunUI.Accent,
        FixerSpecialty.Cleaner => NetrunUI.Ok,
        _ => NetrunUI.Purple,
    };

    public static Color TraitColor(Fixer fixer)
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
        chipX += NetrunUI.Chip(chipX, y + 4f, fixer.TraitLabel.ToUpper(), TraitColor(fixer), fixer.TraitDescription) + 6f;
        if (fixer.IsNew)
            chipX += NetrunUI.Chip(chipX, y + 4f, "NR_FixerNewTag".Translate(), NetrunUI.Accent, "NR_FixerNewTip".Translate()) + 6f;
        if (fixer.Held && !fixer.Pinned)
            NetrunUI.Chip(chipX, y + 4f, "NR_FixerHeldTag".Translate(), NetrunUI.Warn, "NR_FixerHeldTip".Translate());

        // Top right: hold through rotation (unpinned only) and break ties.
        FixerManager manager = FixerManager.Get();
        float tx = inner.xMax;
        string drop = "NR_FixerBreak".Translate();
        float dw = NetrunUI.ButtonWidth(drop);
        tx -= dw;
        if (NetrunUI.Button(new Rect(tx, y - 2f, dw, 26f), drop, NetrunUI.Bad, true, "NR_FixerBreakTip".Translate(NetrunDefOf.NR_Fixers.breakBanDays)))
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("NR_FixerBreakConfirm".Translate(fixer.Name, NetrunDefOf.NR_Fixers.breakBanDays),
                () => manager?.BreakTies(fixer), destructive: true));
        if (!fixer.Pinned && manager != null)
        {
            string hold = (fixer.Held ? "NR_FixerRelease" : "NR_FixerHold").Translate();
            float hw = NetrunUI.ButtonWidth(hold);
            tx -= hw + 6f;
            if (NetrunUI.Button(new Rect(tx, y - 2f, hw, 26f), hold, NetrunUI.Warn, manager.CanHold(fixer), "NR_FixerHoldTip".Translate(NetrunDefOf.NR_Fixers.maxHeld)))
                manager.ToggleHold(fixer);
        }
        y += 28f;

        // Status line.
        Widgets.Label(new Rect(x, y, width, 22f), StatusText(fixer).Colorize(NetrunUI.Muted));
        y += 22f;

        // Reputation: label, bar with the pin mark and the number all centred on one line.
        const float lineHeight = 22f;
        string repLabel = "NR_FixerReputationLabel".Translate();
        float repLabelWidth = Text.CalcSize(repLabel).x + 8f;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(x, y, repLabelWidth, lineHeight), repLabel);
        float pinAt = NetrunDefOf.NR_Fixers.pinnedReputation;
        var barRect = new Rect(x + repLabelWidth, Mathf.Round(y + (lineHeight - 12f) / 2f), Mathf.Min(200f, width - repLabelWidth - 160f), 12f);
        Color repColor = NetrunTex.Gauge(fixer.Reputation);
        NetrunUI.Bar(barRect, fixer.Reputation / 100f, repColor);
        Widgets.DrawBoxSolid(new Rect(Mathf.Round(barRect.x + barRect.width * pinAt / 100f) - 1f, barRect.y - 3f, 2f, barRect.height + 6f), NetrunUI.Warn);
        string repText = fixer.Reputation.ToString("0").Colorize(repColor);
        if (!fixer.Pinned)
            repText += "  " + "NR_FixerToPin".Translate(Mathf.CeilToInt(pinAt - fixer.Reputation)).Colorize(NetrunUI.Muted);
        Widgets.Label(new Rect(barRect.xMax + 8f, y, width - (barRect.xMax + 8f - x), lineHeight), repText);
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(new Rect(x, y, width, 22f), "NR_FixerReputationTip".Translate(pinAt.ToString("0")));
        y += lineHeight + 8f;

        // Buttons right under the reputation line, never overlapping it.
        DrawButtons(new Rect(x, y, width, ButtonHeight), fixer, map);
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

        bool Add(string label, Color color, AcceptanceReport report, string tip, Texture2D icon = null)
        {
            float w = NetrunUI.ButtonWidth(label, icon != null);
            bool clicked = NetrunUI.Button(new Rect(x, rect.y, w, rect.height), label, color, report, tip, icon);
            x += w + 8f;
            return clicked;
        }

        AcceptanceReport silent = fixer.IsSilent ? (AcceptanceReport)StatusText(fixer) : true;

        if (!fixer.ChannelOpen)
        {
            if (Add("NR_FixerCall".Translate(fixer.EffectiveCallCost), NetrunUI.Warn, silent,
                    "NR_FixerCallTip".Translate(NetrunDefOf.NR_Fixers.channelHours), NetrunTex.CallFixer))
                FixerDeals.Call(fixer, map);
        }

        AcceptanceReport needsChannel = !silent.Accepted ? silent : fixer.ChannelOpen ? true : (AcceptanceReport)"NR_FixerAfterCall".Translate();
        if (Add("NR_FixerScrub".Translate(), NetrunUI.Accent, needsChannel, "NR_FixerScrubTip".Translate()))
            Find.WindowStack.Add(new Dialog_FixerScrub(fixer, map));

        bool blackMarket = MapEventsManager.Get()?.BlackMarketStock(fixer).Any() == true;
        if ((fixer.HasShop || blackMarket) && Add(blackMarket ? "NR_FixerShopBlack".Translate() : "NR_FixerShop".Translate(), NetrunUI.Ok, needsChannel, "NR_FixerShopTip".Translate()))
            Find.WindowStack.Add(new Dialog_FixerShop(fixer, map));

        // A favor the fixer owes from a cyberspace find: a free scrub, no channel needed.
        if (fixer.FreeScrubs > 0 && Add("NR_FixerFreeScrub".Translate(fixer.FreeScrubs), NetrunUI.Purple, true,
                "NR_FixerFreeScrubTip".Translate(NetrunDefOf.NR_Cyberspace.freeScrubHeat.ToString("0"))))
        {
            fixer.FreeScrubs--;
            HeatTracker.Get()?.ReduceHeat(NetrunDefOf.NR_Cyberspace.freeScrubHeat, "NR_LogScrub".Translate(fixer.Name));
        }
    }
}
