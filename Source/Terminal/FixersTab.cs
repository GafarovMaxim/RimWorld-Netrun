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

    /// <summary>What the insider's guest card shows; rebuilt twice a second (NetrunUI.Cached), not every frame.</summary>
    private sealed class InsiderView
    {
        public string Name, Status, Buy, BuyTip, Prints;
        public AcceptanceReport Can;
    }

    private static InsiderView BuildInsider(TransitionManager t, Map map)
    {
        ExternalNode lab = t.ActiveLab;
        return new InsiderView
        {
            Name = t.ContactName ?? "?",
            Prints = "NR_InsiderPrints".Translate(t.BlueprintsFound, TransitionManager.Blueprints),
            Status = t.BlueprintsFound >= TransitionManager.Blueprints ? ((string)"NR_InsiderNoMore".Translate()).Colorize(NetrunUI.TextColor)
                : lab != null ? ((string)"NR_InsiderLabLine".Translate(lab.Name, TransitionManager.LabHubLabel(lab))).Colorize(NetrunUI.Accent)
                : t.TicksUntilNextLab > 0 ? ((string)"NR_InsiderCooldown".Translate(t.TicksUntilNextLab.ToStringTicksToPeriod())).Colorize(NetrunUI.Muted)
                : ((string)"NR_InsiderReady".Translate()).Colorize(NetrunUI.TextColor),
            Buy = "NR_InsiderBuy".Translate(TransitionManager.CoordCyberdata, TransitionManager.CoordSilver),
            BuyTip = "NR_InsiderBuyTip".Translate(TransitionManager.LabLifetimeDays.ToString("0")),
            Can = t.CanBuyCoordinates(map),
        };
    }

    /// <summary>
    /// The ending's insider (docs/CONCEPT.md, section 19) as a GUEST card at the top of the pool: a pool card's look (portrait,
    /// name, chips), the corporate fixer portrait, a vague text, the lab on the map and the buy button.
    /// </summary>
    private static void DrawInsider(Rect rect, TransitionManager t, Map map)
    {
        Color stripe = NetrunUI.Purple;
        NetrunUI.Card(rect, stripe);
        Rect inner = new Rect(rect.x + 4f, rect.y, rect.width - 4f, rect.height).ContractedBy(12f);
        var portraitRect = new Rect(inner.x, inner.y + (inner.height - PortraitSize) / 2f, PortraitSize, PortraitSize);
        NetrunUI.Box(portraitRect, NetrunUI.BgWindow, stripe, 2);
        GUI.DrawTexture(portraitRect.ContractedBy(4f), NetrunTex.Insider);

        InsiderView v = NetrunUI.Cached("club.insider", () => BuildInsider(t, map));
        float x = portraitRect.xMax + 14f;
        float width = inner.xMax - x;
        float y = inner.y;
        float nameWidth = NetrunUI.NamePlate(x, y - 3f, v.Name, stripe, GameFont.Medium, width * 0.5f);
        float chipX = x + nameWidth + 10f;
        chipX += NetrunUI.Chip(chipX, y + 4f, NetrunUI.T("NR_InsiderGuestTag"), stripe, NetrunUI.T("NR_InsiderGuestTip")) + 6f;
        chipX += NetrunUI.Chip(chipX, y + 4f, NetrunUI.T("NR_InsiderRole"), NetrunUI.Warn, NetrunUI.T("NR_InsiderRoleTip")) + 6f;
        NetrunUI.Chip(chipX, y + 4f, v.Prints, NetrunUI.Muted, NetrunUI.T("NR_InsiderPrintsTip"));
        y += 28f;

        Text.Font = GameFont.Tiny;
        GUI.color = NetrunUI.Subtle;
        Widgets.Label(new Rect(x, y, width, 34f), NetrunUI.T("NR_InsiderDesc"));
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        y += 34f;
        Widgets.Label(new Rect(x, y, width, 22f), v.Status);

        float bw = NetrunUI.ButtonWidth(v.Buy);
        if (NetrunUI.Button(new Rect(x, inner.yMax - ButtonHeight, bw, ButtonHeight), v.Buy, stripe, v.Can, v.BuyTip))
        {
            t.BuyCoordinates(map);
            NetrunUI.Cached("club.insider", () => BuildInsider(t, map), 0f);
        }
    }

    public static void Draw(Rect rect, Map map, ref Vector2 scroll)
    {
        FixerManager manager = FixerManager.Get();
        if (manager == null || map == null)
            return;

        List<Fixer> pinned = manager.Fixers.Where(f => f.Pinned).ToList();
        List<Fixer> pool = manager.Fixers.Where(f => !f.Pinned).ToList();

        float width = rect.width - 16f;
        TransitionManager transition = TransitionManager.Get();
        bool insider = transition is { ContactMet: true };
        float height = (pinned.Count > 0 ? SectionHeight : 0f) + SectionHeight + (manager.Fixers.Count + (insider ? 1 : 0)) * (CardHeight + CardGap);
        var viewRect = new Rect(0f, 0f, width, height);

        NetrunUI.BeginScroll(rect, ref scroll, viewRect);
        float y = 0f;
        float gPin = y;
        if (pinned.Count > 0)
        {
            y = NetrunUI.Section(new Rect(0f, y, width, SectionHeight), "NR_ClubPinned".Translate(pinned.Count));
            foreach (Fixer fixer in pinned)
            {
                DrawFixer(new Rect(0f, y, width, CardHeight), fixer, map);
                y += CardHeight + CardGap;
            }
        }

        if (pinned.Count > 0)
            GuideRegistry.InScroll("club.pinned", rect, scroll, new Rect(0f, gPin, width, y - gPin));
        float gPool = y;
        float poolY = y;
        y = NetrunUI.Section(new Rect(0f, y, width, SectionHeight),
            "NR_ClubPool".Translate(pool.Count, manager.TicksUntilRotation.ToStringTicksToPeriod()));
        // Not waiting for the rotation: a new pool now, for cyberdata.
        int cost = NetrunDefOf.NR_Fixers.rotateNowCost;
        string rotate = "NR_ClubRotateNow".Translate(cost);
        float rw = NetrunUI.ButtonWidth(rotate, icon: true);
        AcceptanceReport can = ColonyGoods.CountCached(map, NetrunDefOf.USH_Cyberdata) >= cost ? true : (AcceptanceReport)"NR_NotEnoughCyberdata".Translate();
        if (NetrunUI.Button(new Rect(width - rw - 24f, poolY + 1f, rw, 24f), rotate, NetrunUI.Warn, can, "NR_ClubRotateNowTip".Translate(cost), NetrunTex.Refresh))
        {
            if (ColonyGoods.TryTake(map, NetrunDefOf.USH_Cyberdata, cost))
                manager.Rotate(notify: true);
        }
        GuideRegistry.InScroll("club.pool", rect, scroll, new Rect(0f, gPool, width, SectionHeight));
        // The insider is a guest of the pool: the first card in it.
        if (insider)
        {
            DrawInsider(new Rect(0f, y, width, CardHeight), transition, map);
            y += CardHeight + CardGap;
        }
        bool first = true;
        foreach (Fixer fixer in pool)
        {
            if (first)
                GuideRegistry.InScroll("club.card", rect, scroll, new Rect(0f, y, width, CardHeight));
            first = false;
            DrawFixer(new Rect(0f, y, width, CardHeight), fixer, map);
            y += CardHeight + CardGap;
        }
        Widgets.EndScrollView();
    }

    public static Color SpecialtyColor(FixerSpecialty specialty) => specialty switch
    {
        FixerSpecialty.Broker => NetrunUI.Accent,
        FixerSpecialty.Cleaner => NetrunUI.Ok,
        FixerSpecialty.Imperial => NetrunUI.Warn,
        _ => NetrunUI.Purple,
    };

    public static Color TraitColor(Fixer fixer)
    {
        if (!fixer.TraitKnown)
            return NetrunUI.Muted;
        // Every trait helps and hurts; the colour hints which side weighs more.
        return fixer.Trait switch
        {
            FixerTrait.Reliable or FixerTrait.Loyal or FixerTrait.Veteran => NetrunUI.Ok,
            FixerTrait.Rat or FixerTrait.Hothead => NetrunUI.Bad,
            _ => NetrunUI.Warn,
        };
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
            float ps = NetrunUI.Icon(28f);
            var pinRect = new Rect(portraitRect.xMax - ps * 0.65f, portraitRect.y - ps * 0.4f, ps, ps);
            GUI.color = NetrunUI.Warn;
            GUI.DrawTexture(pinRect, NetrunTex.Star ?? NetrunTex.Pinned);
            GUI.color = Color.white;
            NetrunUI.Tip(pinRect, "NR_FixerPinnedTag".Translate());
        }

        // Right column: hold through rotation (unpinned only) above, break ties below; big, one under the other.
        const float sideWidth = 170f;
        FixerManager manager = FixerManager.Get();
        var side = new Rect(inner.xMax - sideWidth, inner.y, sideWidth, ButtonHeight);
        if (!fixer.Pinned && manager != null)
        {
            string hold = fixer.Held ? (string)"NR_FixerRelease".Translate() : (string)"NR_FixerHoldPaid".Translate(NetrunDefOf.NR_Fixers.holdCost);
            // An isolated network reaches nobody: no holds, no breaks, no calls.
            AcceptanceReport canHold = NetworkIsolation.Active ? NetworkIsolation.Check() : manager.CanHold(fixer);
            if (NetrunUI.Button(side, hold, NetrunUI.Warn, canHold, "NR_FixerHoldTip".Translate(NetrunDefOf.NR_Fixers.maxHeld, NetrunDefOf.NR_Fixers.holdCost), NetrunTex.PinVertical))
                manager.ToggleHold(fixer);
            side.y += ButtonHeight + 8f;
        }
        string drop = "NR_FixerBreak".Translate();
        if (NetrunUI.Button(side, drop, NetrunUI.Bad, NetworkIsolation.Check(), "NR_FixerBreakTip".Translate(NetrunDefOf.NR_Fixers.breakBanDays), NetrunTex.Disconnect))
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("NR_FixerBreakConfirm".Translate(fixer.Name, NetrunDefOf.NR_Fixers.breakBanDays),
                () => manager?.BreakTies(fixer), destructive: true));

        float x = portraitRect.xMax + 14f;
        float width = inner.xMax - sideWidth - 14f - x;
        float y = inner.y;

        // Name, then specialty and trait as chips.
        float nameWidth = NetrunUI.NamePlate(x, y - 3f, fixer.Name, stripe, GameFont.Medium, width * 0.5f);
        float chipX = x + nameWidth + 10f;
        chipX += NetrunUI.Chip(chipX, y + 4f, fixer.SpecialtyLabel.ToUpper(), SpecialtyColor(fixer.Specialty), fixer.SpecialtyDescription) + 6f;
        chipX += NetrunUI.Chip(chipX, y + 4f, fixer.TraitLabel.ToUpper(), TraitColor(fixer), fixer.TraitDescription) + 6f;
        if (fixer.IsNew)
            chipX += NetrunUI.Chip(chipX, y + 4f, "NR_FixerNewTag".Translate(), NetrunUI.Accent, "NR_FixerNewTip".Translate()) + 6f;
        if (fixer.Held && !fixer.Pinned)
        {
            float hs = NetrunUI.Icon(28f);
            var heldRect = new Rect(portraitRect.xMax - hs * 0.65f, portraitRect.y - hs * 0.4f, hs, hs);
            GUI.color = NetrunUI.Warn;
            GUI.DrawTexture(heldRect, NetrunTex.Pinned);
            GUI.color = Color.white;
            NetrunUI.Tip(heldRect, "NR_FixerHeldTip".Translate());
            NetrunUI.Chip(chipX, y + 4f, "NR_FixerHeldTag".Translate(), NetrunUI.Warn, "NR_FixerHeldTip".Translate());
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
            repText += "  " + (fixer.WaitsForPin
                ? "NR_FixerPinFull".Translate(NetrunDefOf.NR_Fixers.maxPinned)
                : "NR_FixerToPin".Translate(Mathf.CeilToInt(pinAt - fixer.Reputation))).Colorize(NetrunUI.Muted);
        Widgets.Label(new Rect(barRect.xMax + 8f, y, width - (barRect.xMax + 8f - x), lineHeight), repText);
        Text.Anchor = TextAnchor.UpperLeft;
        NetrunUI.Tip(new Rect(x, y, width, 22f), "NR_FixerReputationTip".Translate(pinAt.ToString("0")));
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

        AcceptanceReport silent = NetworkIsolation.Active ? NetworkIsolation.Check()
            : fixer.IsSilent ? (AcceptanceReport)StatusText(fixer)
            : fixer.CallRefusal is { } refusal ? (AcceptanceReport)refusal : true;

        if (!fixer.ChannelOpen)
        {
            if (Add("NR_FixerCall".Translate(fixer.EffectiveCallCost), NetrunUI.Warn, silent,
                    "NR_FixerCallTip".Translate(NetrunDefOf.NR_Fixers.channelHours), NetrunTex.CallFixer))
                FixerDeals.Call(fixer, map);
        }

        AcceptanceReport needsChannel = !silent.Accepted ? silent : fixer.ChannelOpen ? true : (AcceptanceReport)"NR_FixerAfterCall".Translate();
        if (Add("NR_FixerScrub".Translate(), NetrunUI.Accent, needsChannel, "NR_FixerScrubTip".Translate(), NetrunTex.HeatReduction))
            Find.WindowStack.Add(new Dialog_FixerScrub(fixer, map));

        bool blackMarket = MapEventsManager.Get()?.BlackMarketStock(fixer).Any() == true;
        if ((fixer.HasShop || blackMarket) && Add(blackMarket ? "NR_FixerShopBlack".Translate() : "NR_FixerShop".Translate(), NetrunUI.Ok, needsChannel, "NR_FixerShopTip".Translate(), NetrunTex.Shop))
            Find.WindowStack.Add(new Dialog_FixerShop(fixer, map));

        // A favor the fixer owes from a cyberspace find: a free scrub, no channel needed.
        if (fixer.FreeScrubs > 0 && Add("NR_FixerFreeScrub".Translate(fixer.FreeScrubs), NetrunUI.Purple, true,
                "NR_FixerFreeScrubTip".Translate(NetrunDefOf.NR_Cyberspace.freeScrubHeat.ToString("0"))))
        {
            fixer.FreeScrubs--;
            HeatTracker.Get()?.ReduceHeat(NetrunDefOf.NR_Cyberspace.freeScrubHeat, "NR_LogScrub".Translate(fixer.Name), HeatSource.Scrub);
        }
    }
}
