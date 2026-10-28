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
    private const float IconSize = 52f;
    private const float ButtonHeight = 34f;
    private const float ButtonWidth = 130f;
    private const float RightColumn = 150f;
    private const float LeftWidth = 220f;
    private const float GroupHeight = 24f;

    private enum DifficultyFilter
    {
        Any,
        Easy,
        Medium,
        Hard,
    }

    private enum Sort
    {
        Reward,
        Duration,
        Type,
    }

    private static int _fixerId = -1;
    private static DifficultyFilter _difficulty;
    private static Sort _sort;
    private static bool _historyOpen, _scrollTop;
    private static float _acceptedAt = -10f;
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
        DrawLeft(left, fixers, contracts, map);
        DrawMain(main, map, ref scroll, contracts, fixers, openHelp);
        GuideRegistry.Area("board.history", new Rect(rect.x, rect.yMax - historyHeight, rect.width, historyHeight));
        DrawHistory(new Rect(rect.x, rect.yMax - historyHeight, rect.width, historyHeight), contracts);
    }

    // ---- Left column: fixers, filter, sort ----

    private static void DrawLeft(Rect rect, FixerManager fixers, ContractManager contracts, Map map)
    {
        List<Fixer> list = NetrunUI.Cached("board.fixers", () => fixers.Fixers.OrderByDescending(f => f.Pinned).ThenByDescending(f => f.Reputation).ToList());
        float height = SectionHeight + 38f + list.Count * 58f + SectionHeight + 34f + 20f;
        var view = new Rect(0f, 0f, rect.width - (height > rect.height ? 16f : 0f), height);
        NetrunUI.BeginScroll(rect, ref _leftScroll, view);
        float w = view.width;
        float y = NetrunUI.Section(new Rect(0f, 0f, w, SectionHeight), "NR_BoardFixers".Translate());

        int total = NetrunUI.Cached("board.total", () => fixers.Fixers.Where(f => !f.IsSilent).Sum(f => contracts.OffersOf(f).Count()));
        // "All": one line, no subtitle (UI audit 2026-10-23).
        y = FixerItem(new Rect(0f, y, w, 32f), null, "NR_BoardAllFixers".Translate(total), string.Empty, NetrunUI.Accent, -1f, map, contracts) + 6f;
        foreach (Fixer f in list)
        {
            Color c = f.IsSilent ? NetrunUI.Muted : FixersTab.SpecialtyColor(f.Specialty);
            string name = f.Name + (f.Pinned ? " ★" : string.Empty) + (f.IsSilent ? " · " + "NR_BoardSilentShort".Translate() : string.Empty);
            string sub = f.IsSilent
                ? (string)"NR_BoardSilentBack".Translate((f.SilentUntilTick - Find.TickManager.TicksGame).ToStringTicksToPeriod())
                : (string)"NR_BoardFixerSub".Translate(f.Reputation.ToString("0"), contracts.OffersOf(f).Count());
            y = FixerItem(new Rect(0f, y, w, 52f), f, name, sub, c, f.IsSilent || f.Reputation <= 0f ? -1f : f.Reputation / 100f, map, contracts) + 6f;
        }

        y = NetrunUI.Section(new Rect(0f, y + 6f, w, SectionHeight), "NR_BoardFilter".Translate());
        // One segmented row: "all", then one to three squares like on the cards.
        float quarter = (w - 18f) / 4f;
        foreach (DifficultyFilter d in System.Enum.GetValues(typeof(DifficultyFilter)))
        {
            var r = new Rect((int)d * (quarter + 6f), y, quarter, 28f);
            if (Toggle(r, d == DifficultyFilter.Any ? "NR_BoardFilterAllShort".Translate() : string.Empty, _difficulty == d))
                _difficulty = d;
            if (d != DifficultyFilter.Any)
                Squares(r.center.x - 19.5f, r.center.y - 5.5f, (int)d);
            NetrunUI.Tip(r, ("NR_BoardFilter_" + d).Translate());
        }
        // Sorting is the "▼" label over the offers only (UI audit 2026-10-23: two controls for one thing).
        Widgets.EndScrollView();
    }

    private static float FixerItem(Rect r, Fixer fixer, string name, string sub, Color color, float rep, Map map, ContractManager contracts)
    {
        int id = fixer?.Id ?? -1;
        bool silent = fixer is { IsSilent: true };
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
        // Right of the name: "on line", or a call button for a fixer without a line, so a locked offer is one click away
        // without leaving the Board (Maxim, 2026-10-23). It pulses while the fixer has offers waiting.
        float right = 0f;
        if (fixer != null && !silent)
        {
            Text.Font = GameFont.Tiny;
            if (fixer.ChannelOpen)
            {
                string online = "NR_BoardChannelOpenShort".Translate();
                float cw = Text.CalcSize(online).x;
                Widgets.Label(new Rect(r.xMax - cw - 6f, r.y + 6f, cw + 2f, 18f), online.Colorize(NetrunUI.Ok));
                right = cw + 8f;
            }
            else if (!NetworkIsolation.Active)
            {
                var call = new Rect(r.xMax - 26f, r.y + 4f, 22f, 22f);
                bool waiting = contracts.OffersOf(fixer).Any();
                float pulse = waiting && NetrunMod.Settings.Animations ? 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 4f) : 1f;
                Color tint = Mouse.IsOver(call) ? NetrunUI.Warn : Color.Lerp(NetrunUI.Muted, NetrunUI.Warn, waiting ? pulse : 0f);
                if (NetrunTex.CallFixer != null)
                {
                    GUI.color = tint;
                    GUI.DrawTexture(call.ContractedBy(2f), NetrunTex.CallFixer);
                    GUI.color = Color.white;
                }
                string refusal = fixer.CallRefusal;
                NetrunUI.Tip(call, refusal ?? (string)"NR_BoardCallFirstTip".Translate(fixer.Name, NetrunDefOf.NR_Fixers.channelHours)
                    + "\n" + "NR_FixerCall".Translate(fixer.EffectiveCallCost));
                right = 28f;
                if (refusal == null && Widgets.ButtonInvisible(call))
                    FixerDeals.Call(fixer, map);
            }
            Text.Font = GameFont.Small;
        }
        Widgets.Label(new Rect(r.x + 10f, r.y + 4f, r.width - 14f - right, 22f), (silent ? name.Colorize(NetrunUI.Muted) : name).Truncate(r.width - 14f - right));
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(r.x + 10f, r.y + 24f, r.width - 14f, 16f), sub.Truncate(r.width - 14f).Colorize(NetrunUI.Subtle));
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
        List<ExternalNode> races = Challenges();
        var offers = NetrunUI.Cached("board.offers." + _fixerId + "." + _difficulty + "." + _sort, () =>
        {
            var all = fixers.Fixers.Where(f => !f.IsSilent && (_fixerId < 0 || f.Id == _fixerId))
                .SelectMany(f => contracts.OffersOf(f).Select(c => (contract: c, fixer: f)))
                .Where(o => _difficulty == DifficultyFilter.Any || Difficulty(o.contract) == (int)_difficulty);
            return (_sort switch
            {
                Sort.Reward => all.OrderByDescending(o => o.contract.Reward),
                Sort.Duration => all.OrderBy(o => o.contract.DurationTicks),
                _ => all.OrderBy(o => o.contract.Type).ThenByDescending(o => o.contract.Reward),
            }).ToList();
        });
        int groups = _sort == Sort.Type ? offers.Select(o => o.contract.Type).Distinct().Count() : 0;

        float width = rect.width - 16f;
        float height = 2 * SectionHeight + 20f
                       + (active.Count + races.Count == 0 ? EmptyH : (active.Count + races.Count) * (ActiveHeight(width) + CardGap))
                       + offers.Sum(o => OfferHeight(o.contract, width) + CardGap) + groups * GroupHeight + (offers.Count == 0 ? EmptyH : 0f);
        var view = new Rect(0f, 0f, width, height);
        if (_scrollTop)
        {
            scroll.y = 0f;
            _scrollTop = false;
        }

        NetrunUI.BeginScroll(rect, ref scroll, view);
        float y = 0f;

        float gAct = y;
        y = NetrunUI.Section(new Rect(0f, y, width, SectionHeight), "NR_BoardActive".Translate());
        if (active.Count + races.Count == 0)
            y = Empty(width, y, "NR_ContractsNoActive".Translate());
        foreach (ExternalNode race in races)
        {
            float h = ActiveHeight(width);
            DrawChallenge(new Rect(0f, y, width, h), race);
            y += h + CardGap;
        }
        foreach (Contract contract in active)
        {
            float h = ActiveHeight(width);
            DrawActive(new Rect(0f, y, width, h), contract, fixers.ById(contract.FixerId), map, contracts);
            y += h + CardGap;
        }
        GuideRegistry.InScroll("board.active", rect, scroll, new Rect(0f, gAct, width, y - gAct));

        float gOff = y;
        float yAfter = NetrunUI.Section(new Rect(0f, y, width, SectionHeight), "NR_BoardOffers".Translate(offers.Count));
        // The sort as a real button over the offers (Maxim, 2026-10-23: the grey "reward ▼" was barely visible); a click
        // cycles reward, time, type.
        string sortLabel = "NR_BoardSortButton".Translate(("NR_BoardSort_" + _sort).Translate().ToLower());
        float sw = NetrunUI.ButtonWidth(sortLabel);
        if (NetrunUI.Button(new Rect(width - sw - 4f, y - 1f, sw, 24f), sortLabel, NetrunUI.Accent, true, "NR_BoardSortCycleTip".Translate()))
            _sort = (Sort)(((int)_sort + 1) % 3);
        y = yAfter;
        if (offers.Count == 0)
            y = Empty(width, y, "NR_ContractsNoOffers".Translate());
        for (int i = 0; i < offers.Count; i++)
        {
            if (groups > 0 && (i == 0 || offers[i].contract.Type != offers[i - 1].contract.Type))
            {
                ContractType type = offers[i].contract.Type;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(0f, y + 2f, width, 18f), (offers[i].contract.Title + " · " + offers.Count(o => o.contract.Type == type)).Colorize(NetrunUI.Muted));
                Text.Font = GameFont.Small;
                Widgets.DrawBoxSolid(new Rect(0f, y + GroupHeight - 5f, width, 1f), new Color(1f, 1f, 1f, 0.1f));
                y += GroupHeight;
            }
            float h = OfferHeight(offers[i].contract, width);
            DrawOffer(new Rect(0f, y, width, h), offers[i].contract, offers[i].fixer, contracts, openHelp, i, map);
            y += h + CardGap;
        }
        GuideRegistry.InScroll("board.offers", rect, scroll, new Rect(0f, gOff, width, y - gOff));
        Widgets.EndScrollView();
    }

    private const float EmptyH = 56f;

    // The terminal's shared empty state, as in the other tabs (UI audit 2026-10-23).
    private static float Empty(float width, float y, string text)
    {
        NetrunUI.EmptyState(new Rect(0f, y, width, EmptyH), "EMPTY", text, NetrunUI.Muted, center: false);
        return y + EmptyH;
    }

    // ---- Shared bits ----

    /// <summary>The contract type's icon (also for the cyberspace map's contract cards).</summary>
    public static Texture2D IconOf(ContractType type) => TypeIcon(type);

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
                if (contract.WatchesHub)
                    tags.Add(("NR_BoardTag_InNode".Translate(contract.ParamText), NetrunUI.Accent, "NR_BoardTag_InNodeTip".Translate(contract.ParamText)));
                else
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

        for (int i = 0; i < tags.Count; i++)
        {
            (string text, Color color, string tip) = tags[i];
            bool fixerChip = i == 0 && fixer != null;
            Text.Font = GameFont.Tiny;
            float w = Text.CalcSize(text).x + 12f;
            Text.Font = GameFont.Small;
            if (x + w > maxX)
                break;
            var r = new Rect(x, y, w, 18f);
            if (fixerChip)
            {
                NetrunUI.Chip(x, y, text, color, tip + "\n\n" + "NR_BoardFilterFixerTip".Translate(fixer.Name).Colorize(NetrunUI.Muted));
                if (Widgets.ButtonInvisible(r))
                {
                    NetrunSound.Click();
                    _fixerId = _fixerId == fixer.Id ? -1 : fixer.Id;
                }
                x += w + 6f;
                continue;
            }
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
        float flash = 1f - (Time.realtimeSinceStartup - _acceptedAt) / 1.2f;
        if (flash > 0f)
        {
            GUI.color = new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, flash);
            Widgets.DrawBox(rect, 2);
            GUI.color = Color.white;
        }
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
        // A surveillance contract: one click opens the Gateway on the watched hub.
        if (contract.WatchesHub && GatewayManager.MapReady)
        {
            string show = "NR_MapShow".Translate();
            float sw = NetrunUI.ButtonWidth(show);
            bx -= sw + 8f;
            if (NetrunUI.Button(new Rect(bx, by, sw, 30f), show, NetrunUI.Warn, true, "NR_BoardShowTip".Translate(contract.ParamText)))
                Find.WindowStack.WindowOfType<Dialog_NetTerminal>()?.OpenGatewayAt(contract.TargetHub);
        }
        // A contract with a target on the map: one click opens the Gateway on it.
        else if (contract.Type is ContractType.SystemTap or ContractType.QuietRun && GatewayManager.MapReady
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

    /// <summary>Crew race challenges the player accepted and has not run yet (Maxim, 2026-10-28: shown like contracts).</summary>
    private static List<ExternalNode> Challenges()
        => GatewayManager.Get()?.AllBranches.Where(b => b.ChallengeCrew >= 0 && b.Rival && !b.Hidden).ToList() ?? [];

    private static void DrawChallenge(Rect rect, ExternalNode node)
    {
        Faction crew = Find.FactionManager.AllFactions.FirstOrDefault(f => f.loadID == node.ChallengeCrew);
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(NetrunUI.Bad.r, NetrunUI.Bad.g, NetrunUI.Bad.b, 0.5f));
        NetrunUI.ScanlineOnce(rect, NetrunUI.Bad, NetrunUI.HoverTime(rect), 0.5f);
        var icon = new Rect(rect.x + 4f + Pad, rect.y + Pad + 4f, IconSize, IconSize);
        if (NetrunTex.CyberIcon("Event_Rival") is { } tex)
        {
            GUI.color = NetrunUI.Bad;
            GUI.DrawTexture(icon, tex);
            GUI.color = Color.white;
        }

        float x = icon.xMax + 14f;
        float rightW = 200f;
        float width = rect.xMax - Pad - rightW - 12f - x;
        float y = rect.y + Pad;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(x, y - 2f, width, 30f), ((string)"NR_CrewRaceLabel".Translate(crew?.Name ?? "?")).Truncate(width));
        Text.Font = GameFont.Small;
        y += 30f;
        int left = node.ExpireTick >= 0 ? Mathf.Max(0, node.ExpireTick - Find.TickManager.TicksGame) : 0;
        NetrunUI.Bar(new Rect(x, y, width, 14f), node.ExpireTick >= 0 ? left / (2f * GenDate.TicksPerDay) : 1f, NetrunUI.Bad);
        y += 20f;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(x, y, width, 18f), (node.Name + " · ").Colorize(NetrunUI.Muted)
            + "NR_BoardLeft".Translate(left.ToStringTicksToPeriod()).Colorize(left < GenDate.TicksPerDay ? NetrunUI.Bad : NetrunUI.Muted));
        Text.Font = GameFont.Small;
        NetrunUI.Tip(new Rect(x, rect.y + Pad + 30f, width, 40f), "NR_BoardChallengeTip".Translate(crew?.Name ?? "?", node.Name));

        Text.Anchor = TextAnchor.UpperRight;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(rect.xMax - Pad - rightW, y - 54f, rightW, 30f), "NR_BoardChallengeFame".Translate().Colorize(NetrunUI.Warn));
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        if (node.MapNodeId < 0 || !GatewayManager.MapReady)
            return;
        string show = "NR_MapShow".Translate();
        float sw = NetrunUI.ButtonWidth(show);
        if (NetrunUI.Button(new Rect(rect.xMax - Pad - sw, rect.yMax - Pad - 30f, sw, 30f), show, NetrunUI.Warn, true, "NR_BoardShowTip".Translate(node.Name)))
        {
            GatewayView.FocusNode(node.MapNodeId, node.Id);
            Find.WindowStack.WindowOfType<Dialog_NetTerminal>()?.OpenGatewayAt(node.MapNodeId);
            GatewayView.FocusNode(node.MapNodeId, node.Id);
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
        return Mathf.Max(Mathf.Max(IconSize, content), 46f + ButtonHeight + 20f) + 2 * Pad;
    }

    private static void DrawOffer(Rect rect, Contract contract, Fixer fixer, ContractManager contracts, System.Action<string> openHelp, int index, Map map)
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
        // Grouped by type, the group header names the type: the card names its fixer, the goal is the line under it (Maxim, 2026-10-23).
        Widgets.Label(new Rect(x, y - 2f, width, 30f), (_sort == Sort.Type ? fixer.Name : contract.Title).Truncate(width));
        Text.Font = GameFont.Small;
        y += 28f;
        Text.Font = GameFont.Tiny;
        float desc = HyperText.Height(contract.Description, width, "board." + contract.Id);
        HyperText.Label(new Rect(x, y, width, desc), contract.Description, NetrunUI.Subtle, "board." + contract.Id);
        Text.Font = GameFont.Small;
        y += desc + 6f;

        Tags(x, y, x + width, contract, fixer, openHelp);
        y += 26f;

        int level = Difficulty(contract);
        Squares(x, y + 3f, level);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(x + 48f, y, width - 48f, 18f),
            (DifficultyLabel(level) + " · " + "NR_BoardDuration".Translate(contract.DurationTicks.ToStringTicksToPeriod())).Colorize(NetrunUI.Subtle));
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

        // No channel to the fixer: call them right here instead of a dead Accept (Maxim, 2026-10-23: the player had to go to
        // the Club and remember the name).
        var button = new Rect(rect.xMax - Pad - ButtonWidth, ry + 46f, ButtonWidth, ButtonHeight);
        var reason = new Rect(rx - 40f, button.yMax + 2f, RightColumn + 40f, 18f);
        if (!fixer.ChannelOpen && !NetworkIsolation.Active)
        {
            AcceptanceReport canCall = fixer.CallRefusal is { } refusal ? (AcceptanceReport)refusal : true;
            string callLabel = "NR_FixerCall".Translate(fixer.EffectiveCallCost);
            // The call's label carries its price: as wide as it needs, growing left (UI audit 2026-10-23).
            float cw = Mathf.Max(ButtonWidth, NetrunUI.ButtonWidth(callLabel, NetrunTex.CallFixer != null));
            button = new Rect(rect.xMax - Pad - cw, button.y, cw, ButtonHeight);
            if (NetrunUI.Button(button, callLabel, NetrunUI.Warn, canCall,
                    "NR_BoardCallFirstTip".Translate(fixer.Name, NetrunDefOf.NR_Fixers.channelHours), NetrunTex.CallFixer))
                FixerDeals.Call(fixer, map);
            ReasonLine(reason, "NR_BoardNoChannel".Translate(fixer.Name), NetrunUI.Warn);
            return;
        }
        AcceptanceReport report = contracts.CanAccept(contract, fixer);
        // Blocked for another reason (one active, isolation): the card steps back, the reason stays readable.
        if (!report.Accepted)
            Widgets.DrawBoxSolid(rect, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.45f));
        string label = contracts.HasActive(fixer) ? "NR_BoardOneActive".Translate() : (string)"NR_ContractAccept".Translate();
        int others = contracts.OffersOf(fixer).Count() - 1;
        string tip = others > 0 ? "NR_BoardAcceptDrops".Translate(fixer.Name, others).Colorize(NetrunUI.Warn) : null;
        if (NetrunUI.Button(button, label, NetrunUI.Ok, report, tip))
        {
            contracts.Accept(contract, fixer);
            // The accepted job jumps to the top, where the active card flashes (it used to appear out of view).
            _scrollTop = true;
            _acceptedAt = Time.realtimeSinceStartup;
        }
        if (!report.Accepted && !report.Reason.NullOrEmpty())
            ReasonLine(reason, report.Reason, NetrunUI.Muted);
    }

    private static void ReasonLine(Rect r, string text, Color color)
    {
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(r, text.Truncate(r.width).Colorize(color));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        NetrunUI.Tip(r, text);
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
        // Nothing finished yet: say so in the head, nothing to unfold.
        Widgets.Label(new Rect(head.x + 12f, head.y, head.width - 20f, head.height),
            (count == 0 ? (string)"NR_BoardNoHistory".Translate() : (string)"NR_BoardHistoryHead".Translate(count) + (_historyOpen ? " ▾" : " ▸")).Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
        if (count > 0 && Widgets.ButtonInvisible(head))
        {
            NetrunSound.Click();
            _historyOpen = !_historyOpen;
        }
        if (_historyOpen && count > 0)
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
        NetrunUI.HeaderChip(x, y, "NR_BoardActiveChip".Translate(contracts.Active.Count(), fixers.Fixers.Count), "NR_BoardActiveChipTip".Translate());
    }
}
