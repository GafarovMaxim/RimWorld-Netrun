using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Netrun;

/// <summary>
/// The Board (docs/CONCEPTS_6.md, section 2): a left column with the fixers (reputation bars, filter by fixer),
/// difficulty filters and sorting; on the right the active contract as one wide card with its progress, then the offers
/// with worded tags, difficulty squares and the reward beside the button; a folding history line at the bottom.
/// </summary>
[StaticConstructorOnStartup]
public static class ContractsTab
{
    private const float SectionHeight = 36f;
    private const float CardGap = 10f;
    private const float Pad = 12f;
    private const float IconSize = 64f;
    private const float ButtonHeight = 34f;
    private const float ButtonWidth = 130f;
    private const float RightColumn = 150f;
    private const float LeftWidth = 220f;

    private enum DifficultyFilter
    {
        Any,
        Easy,
        Hard,
    }

    private enum Sort
    {
        Reward,
        Duration,
    }

    private static int _fixerId = -1;
    private static DifficultyFilter _difficulty;
    private static Sort _sort;
    private static bool _historyOpen;
    private static Vector2 _leftScroll;

    private static FixerSettingsDef Settings => NetrunDefOf.NR_Fixers;

    public static void Draw(Rect rect, Map map, ref Vector2 scroll, System.Action<string> openHelp = null)
    {
        ContractManager contracts = ContractManager.Get();
        FixerManager fixers = FixerManager.Get();
        if (contracts == null || fixers == null || map == null)
            return;

        float historyHeight = _historyOpen ? 30f + Mathf.Max(1, Mathf.Min(5, contracts.History.Count)) * 22f + 8f : 30f;
        var left = new Rect(rect.x, rect.y, LeftWidth, rect.height - historyHeight - 8f);
        var main = new Rect(left.xMax + 14f, rect.y, rect.width - LeftWidth - 14f, rect.height - historyHeight - 8f);
        GuideRegistry.Area("board.fixers", left);
        DrawLeft(left, fixers, contracts);
        DrawMain(main, map, ref scroll, contracts, fixers, openHelp);
        GuideRegistry.Area("board.history", new Rect(rect.x, rect.yMax - historyHeight, rect.width, historyHeight));
        DrawHistory(new Rect(rect.x, rect.yMax - historyHeight, rect.width, historyHeight), contracts);
    }

    // ---- Left column: fixers, filter, sort ----

    private static void DrawLeft(Rect rect, FixerManager fixers, ContractManager contracts)
    {
        List<Fixer> list = fixers.Fixers.OrderByDescending(f => f.Pinned).ThenByDescending(f => f.Reputation).ToList();
        float height = SectionHeight + (list.Count + 1) * 58f + 2 * SectionHeight + 3 * 34f + 2 * 34f + 20f;
        var view = new Rect(0f, 0f, rect.width - (height > rect.height ? 16f : 0f), height);
        NetrunUI.BeginScroll(rect, ref _leftScroll, view);
        float w = view.width;
        float y = NetrunUI.Section(new Rect(0f, 0f, w, SectionHeight), "NR_BoardFixers".Translate());

        int total = fixers.Fixers.Where(f => !f.IsSilent).Sum(f => contracts.OffersOf(f).Count());
        y = FixerItem(new Rect(0f, y, w, 52f), -1, "NR_BoardAllFixers".Translate(total), "NR_BoardAllFixersSub".Translate(), NetrunUI.Accent, -1f, false) + 6f;
        foreach (Fixer f in list)
        {
            Color c = f.IsSilent ? NetrunUI.Muted : FixersTab.SpecialtyColor(f.Specialty);
            string name = f.Name + (f.Pinned ? " ★" : string.Empty) + (f.IsSilent ? " · " + "NR_BoardSilentShort".Translate() : string.Empty);
            string sub = f.IsSilent
                ? (string)"NR_BoardSilentBack".Translate((f.SilentUntilTick - Find.TickManager.TicksGame).ToStringTicksToPeriod())
                : (string)"NR_BoardFixerSub".Translate(f.Reputation.ToString("0"), contracts.OffersOf(f).Count());
            y = FixerItem(new Rect(0f, y, w, 52f), f.Id, name, sub, c, f.IsSilent ? -1f : f.Reputation / 100f, f.IsSilent) + 6f;
        }

        y = NetrunUI.Section(new Rect(0f, y + 6f, w, SectionHeight), "NR_BoardFilter".Translate());
        foreach (DifficultyFilter d in System.Enum.GetValues(typeof(DifficultyFilter)))
        {
            if (Toggle(new Rect(0f, y, w, 28f), ("NR_BoardFilter_" + d).Translate(), _difficulty == d))
                _difficulty = d;
            y += 34f;
        }
        y = NetrunUI.Section(new Rect(0f, y + 6f, w, SectionHeight), "NR_BoardSort".Translate());
        float half = (w - 6f) / 2f;
        if (Toggle(new Rect(0f, y, half, 28f), "NR_BoardSort_Reward".Translate(), _sort == Sort.Reward))
            _sort = Sort.Reward;
        if (Toggle(new Rect(half + 6f, y, half, 28f), "NR_BoardSort_Duration".Translate(), _sort == Sort.Duration))
            _sort = Sort.Duration;
        Widgets.EndScrollView();
    }

    private static float FixerItem(Rect r, int id, string name, string sub, Color color, float rep, bool silent)
    {
        bool on = _fixerId == id;
        float lit = NetrunUI.HoverFade(r, 0.12f);
        Widgets.DrawBoxSolid(r, on ? NetrunUI.BgCardAlt : Color.Lerp(NetrunUI.BgCard, NetrunUI.BgCardAlt, lit));
        Widgets.DrawBoxSolid(new Rect(r.x, r.y, 3f, r.height), color);
        if (on)
        {
            GUI.color = new Color(color.r, color.g, color.b, 0.8f);
            Widgets.DrawBox(r);
            GUI.color = Color.white;
        }
        Widgets.Label(new Rect(r.x + 10f, r.y + 4f, r.width - 14f, 22f), (silent ? name.Colorize(NetrunUI.Muted) : name).Truncate(r.width - 14f));
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(r.x + 10f, r.y + 24f, r.width - 14f, 16f), sub.Truncate(r.width - 14f).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        if (rep >= 0f)
            NetrunUI.Bar(new Rect(r.x + 10f, r.yMax - 9f, r.width - 20f, 5f), rep, color);
        if (Widgets.ButtonInvisible(r))
        {
            NetrunSound.Click();
            _fixerId = on ? -1 : id;
        }
        return r.yMax;
    }

    private static bool Toggle(Rect r, string label, bool on)
    {
        float lit = NetrunUI.HoverFade(r, 0.1f);
        NetrunUI.Box(r, on ? new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.15f) : NetrunUI.BgCard,
            on ? NetrunUI.Accent : new Color(1f, 1f, 1f, 0.15f + 0.25f * lit));
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(r, on ? label.Colorize(NetrunUI.Accent) : label.Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
        if (!on && Widgets.ButtonInvisible(r))
        {
            NetrunSound.Click();
            return true;
        }
        return false;
    }

    // ---- Main: active contract, then offers ----

    private static void DrawMain(Rect rect, Map map, ref Vector2 scroll, ContractManager contracts, FixerManager fixers, System.Action<string> openHelp)
    {
        List<Contract> active = contracts.Active.ToList();
        var offers = fixers.Fixers.Where(f => !f.IsSilent && (_fixerId < 0 || f.Id == _fixerId))
            .SelectMany(f => contracts.OffersOf(f).Select(c => (contract: c, fixer: f)))
            .Where(o => _difficulty == DifficultyFilter.Any || (_difficulty == DifficultyFilter.Easy ? Difficulty(o.contract) == 1 : Difficulty(o.contract) == 3))
            .ToList();
        offers = _sort == Sort.Reward
            ? offers.OrderByDescending(o => o.contract.Reward).ToList()
            : offers.OrderBy(o => o.contract.DurationTicks).ToList();

        float width = rect.width - 16f;
        float height = 2 * SectionHeight + 20f
                       + (active.Count == 0 ? 30f : active.Sum(c => ActiveHeight(width) + CardGap))
                       + offers.Sum(o => OfferHeight(o.contract, width) + CardGap) + (offers.Count == 0 ? 30f : 0f);
        var view = new Rect(0f, 0f, width, height);

        NetrunUI.BeginScroll(rect, ref scroll, view);
        float y = 0f;

        float gAct = y;
        y = NetrunUI.Section(new Rect(0f, y, width, SectionHeight), "NR_BoardActive".Translate());
        if (active.Count == 0)
            y = Empty(width, y, "NR_ContractsNoActive".Translate());
        foreach (Contract contract in active)
        {
            float h = ActiveHeight(width);
            DrawActive(new Rect(0f, y, width, h), contract, fixers.ById(contract.FixerId), map, contracts);
            y += h + CardGap;
        }
        GuideRegistry.InScroll("board.active", rect, scroll, new Rect(0f, gAct, width, y - gAct));

        float gOff = y;
        float yAfter = NetrunUI.Section(new Rect(0f, y, width, SectionHeight), "NR_BoardOffers".Translate(offers.Count));
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(new Rect(width - 200f, y, 200f, 26f), (("NR_BoardSort_" + _sort).Translate().ToLower() + " ▼").Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        y = yAfter;
        if (offers.Count == 0)
            y = Empty(width, y, "NR_ContractsNoOffers".Translate());
        for (int i = 0; i < offers.Count; i++)
        {
            float h = OfferHeight(offers[i].contract, width);
            DrawOffer(new Rect(0f, y, width, h), offers[i].contract, offers[i].fixer, contracts, openHelp, i);
            y += h + CardGap;
        }
        GuideRegistry.InScroll("board.offers", rect, scroll, new Rect(0f, gOff, width, y - gOff));
        Widgets.EndScrollView();
    }

    private static float Empty(float width, float y, string text)
    {
        Widgets.Label(new Rect(0f, y, width, 26f), NetrunUI.Prompt(text).Colorize(NetrunUI.Muted));
        return y + 30f;
    }

    // ---- Shared bits ----

    private static Texture2D TypeIcon(ContractType type) => type switch
    {
        ContractType.DataDelivery => NetrunTex.TabIcon(2),
        ContractType.SystemTap => NetrunTex.Cyberspace,
        ContractType.Surveillance => NetrunTex.CyberIcon("Action_Scan") ?? NetrunTex.Cyberspace,
        ContractType.Rookie => NetrunTex.CyberIcon("Icon_Xp") ?? NetrunTex.Cyberspace,
        ContractType.AIOrder => NetrunTex.AIUse,
        ContractType.CleanLab => NetrunTex.TabIconByName("Studio") ?? NetrunTex.Cyberspace,
        _ => NetrunTex.HeatIcon(HeatLevel.Shadow),
    };

    /// <summary>1-3 squares: where the goal sits in its type's range.</summary>
    private static int Difficulty(Contract contract)
    {
        IntRange range = contract.Type switch
        {
            ContractType.DataDelivery => Settings.dataDeliveryGoal,
            ContractType.SystemTap => Settings.systemTapGoal,
            ContractType.Surveillance => Settings.surveillanceHours,
            ContractType.CleanLab => Settings.cleanLabChips,
            ContractType.QuietRun => Settings.quietRunGoal,
            _ => new IntRange(1, 1),
        };
        float t = range.max > range.min ? (float)(contract.Goal - range.min) / (range.max - range.min) : 0.5f;
        return 1 + Mathf.Clamp(Mathf.RoundToInt(t * 2f), 0, 2);
    }

    private static void DrawIcon(Rect card, ContractType type, Color color)
    {
        var box = new Rect(card.x + 4f + Pad, card.y + Pad, IconSize, IconSize);
        NetrunUI.Box(box, NetrunUI.BgWindow, color, 2);
        GUI.DrawTexture(box.ContractedBy(12f), NetrunTint.Of(TypeIcon(type)));
    }

    /// <summary>Tags in words: the fixer, what the job asks (green easy, red risky), a known bad trait. A click opens the article.</summary>
    private static float Tags(float x, float y, float maxX, Contract contract, Fixer fixer, System.Action<string> openHelp)
    {
        var tags = new List<(string text, Color color, string tip)>();
        if (fixer != null)
            tags.Add((fixer.Name, FixersTab.SpecialtyColor(fixer.Specialty), fixer.SpecialtyLabel));
        switch (contract.Type)
        {
            case ContractType.DataDelivery:
                tags.Add(("NR_BoardTag_Delivery".Translate(), NetrunUI.Accent, "NR_BoardTag_DeliveryTip".Translate(Settings.advancedCyberdataWorth)));
                break;
            case ContractType.SystemTap:
                tags.Add(("NR_BoardTag_AnyHack".Translate(), NetrunUI.Ok, "NR_BoardTag_AnyHackTip".Translate()));
                break;
            case ContractType.QuietRun:
                tags.Add(("NR_BoardTag_Quiet".Translate(HeatLevel.Tracked.Label()), NetrunUI.Bad, "NR_BoardTag_QuietTip".Translate(HeatLevel.Tracked.Label())));
                break;
            case ContractType.Surveillance:
                tags.Add(("NR_BoardTag_Pod".Translate(), NetrunUI.Accent, "NR_BoardTag_PodTip".Translate()));
                break;
            case ContractType.LayLow:
                tags.Add(("NR_BoardTag_LayLow".Translate(contract.Goal), NetrunUI.Ok, "NR_BoardTag_LayLowTip".Translate(contract.Goal)));
                break;
            case ContractType.Rookie:
                tags.Add(("NR_BoardTag_Rookie".Translate(Settings.rookieMaxSkill), NetrunUI.Warn, "NR_BoardTag_RookieTip".Translate(Settings.rookieMaxSkill)));
                break;
            case ContractType.AIOrder:
                tags.Add(("NR_BoardTag_Fragment".Translate(), NetrunUI.Purple, "NR_BoardTag_FragmentTip".Translate()));
                break;
            case ContractType.CleanLab:
                tags.Add(("NR_BoardTag_Mood".Translate(contract.ParamText), NetrunUI.Accent, "NR_BoardTag_MoodTip".Translate(contract.ParamText)));
                break;
        }
        if (contract.Favor > 0)
            tags.Add(("NR_BoardTag_Favor".Translate(contract.Favor), NetrunUI.Warn, "NR_BoardTag_FavorTip".Translate(contract.Favor)));
        if (fixer != null && fixer.TraitKnown)
            tags.Add((fixer.TraitLabel, FixersTab.TraitColor(fixer), fixer.TraitDescription));

        foreach ((string text, Color color, string tip) in tags)
        {
            Text.Font = GameFont.Tiny;
            float w = Text.CalcSize(text).x + 12f;
            Text.Font = GameFont.Small;
            if (x + w > maxX)
                break;
            var r = new Rect(x, y, w, 18f);
            NetrunUI.Chip(x, y, text, color, openHelp != null ? tip + "\n\n" + "NR_BoardTagArticle".Translate().Colorize(NetrunUI.Muted) : tip);
            if (openHelp != null && Widgets.ButtonInvisible(r))
                openHelp("NR_Help_Fixers_Contracts");
            x += w + 6f;
        }
        return x;
    }

    private static void Squares(float x, float y, int level)
    {
        Color c = level == 1 ? NetrunUI.Ok : level == 2 ? NetrunUI.Warn : NetrunUI.Bad;
        for (int i = 0; i < 3; i++)
            Widgets.DrawBoxSolid(new Rect(x + i * 14f, y, 11f, 11f), i < level ? c : new Color(1f, 1f, 1f, 0.12f));
    }

    private static string DifficultyLabel(int level) => ("NR_BoardDifficulty_" + level).Translate();

    // ---- Active: one wide card ----

    private static float ActiveHeight(float width) => IconSize + 2 * Pad + 8f;

    private static void DrawActive(Rect rect, Contract contract, Fixer fixer, Map map, ContractManager contracts)
    {
        Color color = fixer != null ? FixersTab.SpecialtyColor(fixer.Specialty) : NetrunUI.Ok;
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.5f));
        NetrunUI.ScanlineOnce(rect, NetrunUI.Accent, NetrunUI.HoverTime(rect), 0.5f);
        DrawIcon(rect, contract.Type, color);

        float x = rect.x + 4f + Pad + IconSize + 14f;
        float rightW = 200f;
        float width = rect.xMax - Pad - rightW - 12f - x;
        float y = rect.y + Pad;

        Text.Font = GameFont.Medium;
        string title = contract.Title + (fixer != null ? " · " + fixer.Name : string.Empty);
        Widgets.Label(new Rect(x, y - 2f, width, 30f), title.Truncate(width));
        Text.Font = GameFont.Small;
        y += 30f;

        var bar = new Rect(x, y, width, 14f);
        NetrunUI.Bar(bar, (float)contract.Progress / contract.Goal, NetrunUI.Ok);
        y += 20f;
        Text.Font = GameFont.Tiny;
        Color due = contract.TicksLeft < GenDate.TicksPerDay ? NetrunUI.Bad : NetrunUI.Muted;
        Widgets.Label(new Rect(x, y, width, 18f), ("NR_BoardActiveLine".Translate(contract.Progress, contract.Goal) + " · ").Colorize(NetrunUI.Muted)
            + "NR_BoardLeft".Translate(contract.TicksLeft.ToStringTicksToPeriod()).Colorize(due));
        Text.Font = GameFont.Small;
        NetrunUI.Tip(new Rect(x, rect.y + Pad + 30f, width, 40f), contract.Description + "\n\n" +
            "NR_BoardFail".Translate(Settings.failReputationLoss.ToString("0"), Settings.failSilentDays).Colorize(NetrunUI.Bad));

        // Right: reward, reputation, the buttons.
        float rx = rect.xMax - Pad - rightW;
        float ry = rect.y + Pad;
        Text.Anchor = TextAnchor.UpperRight;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(rx, ry - 4f, rightW, 30f), "NR_BoardSilver".Translate(contract.Reward).Colorize(NetrunUI.Ok));
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(rx, ry + 24f, rightW, 18f), "NR_BoardRep".Translate(Settings.contractReputation.ToString("0")).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        float by = rect.yMax - Pad - 30f;
        float bx = rect.xMax - Pad;
        string abandon = "NR_ContractAbandon".Translate();
        float aw = NetrunUI.ButtonWidth(abandon);
        bx -= aw;
        if (NetrunUI.Button(new Rect(bx, by, aw, 30f), abandon, NetrunUI.Bad, true))
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("NR_ContractAbandonConfirm".Translate(contract.Title), () => contracts.Abandon(contract), destructive: true));
        if (contract.Type == ContractType.DataDelivery)
        {
            int have = ColonyGoods.CountCached(map, NetrunDefOf.USH_Cyberdata) + ColonyGoods.CountCached(map, NetrunDefOf.USH_AdvancedCyberdata) * Settings.advancedCyberdataWorth;
            string deliver = "NR_BoardDeliverShort".Translate(have);
            float dw = NetrunUI.ButtonWidth(deliver);
            bx -= dw + 8f;
            AcceptanceReport canDeliver = have > 0 ? true : (AcceptanceReport)"NR_NotEnoughCyberdata".Translate();
            if (NetrunUI.Button(new Rect(bx, by, dw, 30f), deliver, NetrunUI.Ok, canDeliver, "NR_ContractDeliverTip".Translate(Settings.advancedCyberdataWorth)))
                contracts.Deliver(contract, map);
        }
        else if (contract.Type == ContractType.AIOrder)
        {
            int have = ContractManager.EmptyFragments(map).Sum(t => t.stackCount);
            string deliver = "NR_BoardDeliverFragment".Translate(have);
            float dw = NetrunUI.ButtonWidth(deliver);
            bx -= dw + 8f;
            AcceptanceReport canDeliver = have > 0 ? true : (AcceptanceReport)"NR_ContractNoFragment".Translate();
            if (NetrunUI.Button(new Rect(bx, by, dw, 30f), deliver, NetrunUI.Ok, canDeliver, "NR_BoardTag_FragmentTip".Translate()))
                contracts.DeliverFragment(contract, map);
        }
        // The contract's fixer can be called from here, without going to the Club.
        if (fixer != null && !fixer.ChannelOpen)
        {
            string callLabel = "NR_FixerCall".Translate(fixer.EffectiveCallCost);
            float cw = NetrunUI.ButtonWidth(callLabel, NetrunTex.CallFixer != null);
            bx -= cw + 8f;
            AcceptanceReport canCall = NetworkIsolation.Active ? NetworkIsolation.Check()
                : fixer.IsSilent ? (AcceptanceReport)"NR_FixerSilent".Translate(fixer.Name)
                : fixer.CallRefusal is { } refusal ? (AcceptanceReport)refusal : true;
            if (NetrunUI.Button(new Rect(bx, by, cw, 30f), callLabel, NetrunUI.Warn, canCall,
                    "NR_FixerCallTip".Translate(NetrunDefOf.NR_Fixers.channelHours), NetrunTex.CallFixer))
                FixerDeals.Call(fixer, map);
        }
        // A contract with a target on the map: one click opens the Gateway on it.
        if (contract.Type is ContractType.SystemTap or ContractType.QuietRun && GatewayManager.MapReady
            && GatewayManager.Get()?.AllBranches.FirstOrDefault(b => b.Contract && !b.Hidden && b.MapNodeId >= 0) is { } target)
        {
            string show = "NR_MapShow".Translate();
            float sw = NetrunUI.ButtonWidth(show);
            bx -= sw + 8f;
            if (NetrunUI.Button(new Rect(bx, by, sw, 30f), show, NetrunUI.Warn, true, "NR_BoardShowTip".Translate(target.Name)))
            {
                GatewayView.FocusNode(target.MapNodeId, target.Id);
                Find.WindowStack.WindowOfType<Dialog_NetTerminal>()?.OpenGatewayAt(target.MapNodeId);
                GatewayView.FocusNode(target.MapNodeId, target.Id);
            }
        }
    }

    // ---- Offers ----

    private static float TextWidth(float width) => width - (4f + Pad + IconSize + 14f) - Pad - RightColumn - 12f;

    private static float OfferHeight(Contract contract, float width)
    {
        Text.Font = GameFont.Tiny;
        float desc = HyperText.Height(contract.Description, TextWidth(width), "board." + contract.Id);
        Text.Font = GameFont.Small;
        float content = 28f + desc + 6f + 22f + 4f + 18f;
        return Mathf.Max(Mathf.Max(IconSize, content), 30f + 20f + ButtonHeight + 8f) + 2 * Pad;
    }

    private static void DrawOffer(Rect rect, Contract contract, Fixer fixer, ContractManager contracts, System.Action<string> openHelp, int index)
    {
        // Cards slide in one after another.
        float k = NetrunMod.Settings.Animations ? Mathf.Clamp01((Time.realtimeSinceStartup - NetrunUI.CascadeStart("board") - index * 0.04f) / 0.25f) : 1f;
        rect.x += 16f * (1f - k) * (1f - k);
        Color color = FixersTab.SpecialtyColor(fixer.Specialty);
        NetrunUI.Card(rect, color);
        DrawIcon(rect, contract.Type, color);

        float x = rect.x + 4f + Pad + IconSize + 14f;
        float width = TextWidth(rect.width);
        float y = rect.y + Pad;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(x, y - 2f, width, 30f), contract.Title.Truncate(width));
        Text.Font = GameFont.Small;
        y += 28f;
        Text.Font = GameFont.Tiny;
        float desc = HyperText.Height(contract.Description, width, "board." + contract.Id);
        HyperText.Label(new Rect(x, y, width, desc), contract.Description, NetrunUI.Muted, "board." + contract.Id);
        Text.Font = GameFont.Small;
        y += desc + 6f;

        Tags(x, y, x + width, contract, fixer, openHelp);
        y += 26f;

        int level = Difficulty(contract);
        Squares(x, y + 3f, level);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(x + 48f, y, width - 48f, 18f),
            (DifficultyLabel(level) + " · " + "NR_BoardDuration".Translate(contract.DurationTicks.ToStringTicksToPeriod())).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;

        // Right column: reward and reputation over the button (fixed width, never wraps).
        float rx = rect.xMax - Pad - RightColumn;
        float ry = rect.y + Pad;
        Text.Anchor = TextAnchor.UpperRight;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(rx - 40f, ry - 4f, RightColumn + 40f, 30f), "NR_BoardSilver".Translate(contract.Reward).Colorize(NetrunUI.Ok));
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(rx, ry + 26f, RightColumn, 18f), "NR_BoardRep".Translate(Settings.contractReputation.ToString("0")).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        AcceptanceReport report = contracts.CanAccept(contract, fixer);
        string label = contracts.HasActive(fixer) ? "NR_BoardOneActive".Translate() : (string)"NR_ContractAccept".Translate();
        if (NetrunUI.Button(new Rect(rect.xMax - Pad - ButtonWidth, ry + 50f, ButtonWidth, ButtonHeight), label, NetrunUI.Ok, report))
            contracts.Accept(contract, fixer);
    }

    // ---- History: a folding line at the bottom ----

    private static void DrawHistory(Rect rect, ContractManager contracts)
    {
        var head = new Rect(rect.x, rect.y, rect.width, 30f);
        Widgets.DrawBoxSolid(head, Color.Lerp(NetrunUI.BgCard, NetrunUI.BgCardAlt, NetrunUI.HoverFade(head)));
        Widgets.DrawBoxSolid(new Rect(head.x, head.y, 3f, head.height), NetrunUI.Muted);
        int count = Mathf.Min(5, contracts.History.Count);
        Text.Anchor = TextAnchor.MiddleLeft;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(head.x + 12f, head.y, head.width - 20f, head.height),
            ("NR_BoardHistoryHead".Translate(count) + (_historyOpen ? " ▾" : " ▸")).Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
        if (Widgets.ButtonInvisible(head))
        {
            NetrunSound.Click();
            _historyOpen = !_historyOpen;
        }
        if (_historyOpen)
        {
            float y = head.yMax + 4f;
            if (count == 0)
                Widgets.Label(new Rect(rect.x + 12f, y, rect.width - 20f, 20f), "NR_BoardNoHistory".Translate().Colorize(NetrunUI.Muted));
            foreach (ContractRecord r in contracts.History.Reverse().Take(5))
            {
                string title = ("NR_Contract_" + r.Type).Translate();
                string line = r.Success
                    ? "✔ ".Colorize(NetrunUI.Ok) + "NR_BoardHistoryDone".Translate(title, r.FixerName, r.Reward)
                    : "✘ ".Colorize(NetrunUI.Bad) + "NR_BoardHistoryFailed".Translate(title, r.FixerName);
                Widgets.Label(new Rect(rect.x + 12f, y, rect.width - 20f, 20f), line);
                y += 22f;
            }
        }
        Text.Font = GameFont.Small;
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
