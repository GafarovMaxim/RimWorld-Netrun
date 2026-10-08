using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// One article of the in-game help (docs/HELP.md). A page without a parent is a topic in the left tree.
/// Body markup, one element per line: "## heading", "- bullet", "[[levels]]" (Heat level tiles),
/// "> page:defName|text" (link), "> open:Screen|text" (open a terminal screen); {tokens} take numbers from Defs.
/// </summary>
public class HelpPageDef : Def
{
    public HelpPageDef parent;
    public int order;
    /// <summary>Topic stripe color: Accent, Ok, Warn, Bad, Purple, Muted, Studio.</summary>
    public string color = "Accent";
    [MustTranslate]
    public string body;
    [MustTranslate]
    public List<string> tips = [];
    public List<HelpPageDef> related = [];
    /// <summary>Terminal screens this page opens on from the "?" button: Core, Heat, Club, Board, Archive, Server, Studio, Gateway, Dive, Onboarding.</summary>
    public List<string> contexts = [];
    /// <summary>A spoiler page stays locked until this flag is set in the game (HelpProgress).</summary>
    public string unlockFlag;
    /// <summary>Header and tree icon: see HelpIcons.Resolve ("Name", "Folder/Name", "thing:Def", "heat:Level", "ai:Personality").</summary>
    public string icon;
    /// <summary>While locked, show "???" instead of the title.</summary>
    public bool lockedTitle;
    /// <summary>Words that link to this page from the text of other pages (first mention per line).</summary>
    [MustTranslate]
    public List<string> keywords = [];
    /// <summary>How to unlock it, shown while locked.</summary>
    [MustTranslate]
    public string lockHint;

    /// <summary>Kept for links, but not listed in the Cyberpedia's tree (the AI archetypes live in one gallery).</summary>
    public bool hideInTree;

    public bool IsTopic => parent == null;

    public IEnumerable<HelpPageDef> Children => DefDatabase<HelpPageDef>.AllDefs.Where(p => p.parent == this && !p.hideInTree).OrderBy(p => p.order);

    public HelpPageDef Topic => parent == null ? this : parent.Topic;

    public Color Color => color switch
    {
        "Ok" => NetrunUI.Ok,
        "Warn" => NetrunUI.Warn,
        "Bad" => NetrunUI.Bad,
        "Purple" => NetrunUI.Purple,
        "Muted" => NetrunUI.Muted,
        "Studio" => new Color(1f, 0.45f, 0.35f),
        _ => NetrunUI.Accent,
    };

    public static IEnumerable<HelpPageDef> Topics => DefDatabase<HelpPageDef>.AllDefs.Where(p => p.IsTopic).OrderBy(p => p.order);

    /// <summary>The page for a terminal screen: a page listing it in contexts, else the first topic.</summary>
    public static HelpPageDef ForContext(string context)
        => DefDatabase<HelpPageDef>.AllDefs.Where(p => p.contexts.Contains(context)).OrderBy(p => p.IsTopic ? 1 : 0).FirstOrDefault()
           ?? Topics.FirstOrDefault();

    /// <summary>Numbers from Defs and settings, so the help stays true after a rebalance.</summary>
    public static string Tokens(string text)
    {
        if (text.NullOrEmpty() || !text.Contains("{"))
            return text;
        var fixers = NetrunDefOf.NR_Fixers;
        var cyber = NetrunDefOf.NR_Cyberspace;
        var ai = NetrunDefOf.NR_AI;
        float decay = NetrunMod.Settings.HeatDecayPerDay;
        var values = new Dictionary<string, string>
        {
            ["heat.decay"] = decay.ToString("0.#"),
            ["heat.decayHigh"] = (decay * NetrunDefOf.NR_Heat.highHeatDecayFactor).ToString("0.#"),
            ["heat.scrubBest"] = (fixers.heatPerCyberdata * fixers.scrubFactorByLevel.FirstOrDefault() * NetrunMod.Settings.ScrubMultiplier).ToString("0.#"),
            ["heat.scrubWorst"] = (fixers.heatPerCyberdata * fixers.scrubFactorByLevel.LastOrDefault() * NetrunMod.Settings.ScrubMultiplier).ToString("0.#"),
            ["fixers.pool"] = fixers.poolSize.ToString(),
            ["fixers.rotation"] = fixers.rotationDays.ToString(),
            ["fixers.pin"] = fixers.pinnedReputation.ToString("0"),
            ["fixers.held"] = fixers.maxHeld.ToString(),
            ["fixers.ban"] = fixers.breakBanDays.ToString(),
            ["fixers.channel"] = fixers.channelHours.ToString(),
            ["dive.turns"] = cyber.maxTurns.ToString(),
            ["dive.hardReward"] = cyber.branchHardReward.ToString("0.0#"),
            ["dive.easyReward"] = cyber.branchEasyReward.ToString("0.0#"),
            ["dive.hardTrace"] = cyber.branchHardTrace.ToString("0"),
            ["dive.easyTrace"] = cyber.branchEasyTrace.ToString("0"),
            ["dive.program"] = cyber.programCost.ToString(),
            ["dive.slots"] = cyber.programSlots.ToString(),
            ["dive.refresh"] = cyber.refreshDays.ToString("0.#"),
            ["dive.boss"] = cyber.bossIntervalDays.ToString("0"),
            ["dive.favor"] = cyber.groupFavorForAllies.ToString(),
            ["dive.allies"] = cyber.alliesCooldownDays.ToString("0"),
            ["dive.wake"] = (cyber.podWakeUpTicks / (float)GenDate.TicksPerHour).ToString("0.#"),
            ["netsick.dive"] = cyber.netsickPerDive.ToStringPercent(),
            ["ai.minTrust"] = ai.minTrustForService.ToString("0"),
            ["ai.serviceTrust"] = ai.serviceTrustCost.ToString("0"),
            ["ai.serviceHunger"] = ai.serviceHungerCost.ToString("0"),
            ["ai.cooldown"] = ai.serviceCooldownDays.ToString("0.#"),
            ["ai.starving"] = ai.starvingHunger.ToString("0"),
        };
        foreach (KeyValuePair<string, string> kv in values)
            text = text.Replace("{" + kv.Key + "}", kv.Value);
        // AI archetypes: {aiDesc:Ghost}, {aiService:Ghost}, {aiServiceDesc:Ghost}, {aiFood:Ghost}, {aiHost:Ghost}.
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\{ai(Desc|Service|ServiceDesc|Food|Host):(\w+)\}", m =>
        {
            if (!System.Enum.TryParse(m.Groups[2].Value, out AIPersonality p))
                return m.Value;
            return m.Groups[1].Value switch
            {
                "Desc" => p.Description(),
                "Service" => AIBehavior.ServiceLabel(p),
                "ServiceDesc" => (string)("NR_AIServiceDesc_" + p).Translate(),
                "Food" => AIBehavior.FoodText(p),
                _ => StripModName(DefDatabase<HediffDef>.GetNamedSilentFail("NR_HostedAI_" + p)?.description ?? "-"),
            };
        });
        return text;
    }

    /// <summary>The game adds the mod's name in italics to the end of def descriptions; the Cyberpedia does not need it.</summary>
    public static string StripModName(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text ?? string.Empty, @"\s*<i>.*?</i>\s*$", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
}

/// <summary>What the player has met in the game (unlocks spoiler pages) and which pages they have read.</summary>
public class HelpProgress : GameComponent
{
    private HashSet<string> _flags = [];
    private HashSet<string> _seen = [];
    private bool _helpOpened;

    public HelpProgress(Game game) { }

    public static HelpProgress Get() => Current.Game?.GetComponent<HelpProgress>();

    /// <summary>The "?" button pulses until the help has been opened once.</summary>
    public bool HelpOpened
    {
        get => _helpOpened;
        set => _helpOpened = value;
    }

    /// <summary>Has the player met this (an "ai:Ghost", "group", ...)?</summary>
    public bool Has(string flag) => _flags.Contains(flag) || _flags.Contains("all");

    public bool Unlocked(HelpPageDef page) => page.unlockFlag.NullOrEmpty() || _flags.Contains(page.unlockFlag) || _flags.Contains("all");
    public bool IsNew(HelpPageDef page) => !page.unlockFlag.NullOrEmpty() && Unlocked(page) && !_seen.Contains(page.defName);
    public void MarkSeen(HelpPageDef page) => _seen.Add(page.defName);
    public void UnlockAll() => _flags.Add("all");

    // Guide mode: screens opened in the guide and areas looked at.
    public void MarkGuideScreen(string screen) => _seen.Add("guide:" + screen);
    public bool GuideScreenSeen(string screen) => _seen.Contains("guide:" + screen);
    public void MarkGuideArea(GuideAreaDef area) => _seen.Add("area:" + area.defName);
    public bool GuideAreaSeen(GuideAreaDef area) => _seen.Contains("area:" + area.defName);

    /// <summary>Checks what the colony has met (called when the help opens).</summary>
    public void Refresh(Map map)
    {
        GatewayManager gateway = GatewayManager.Get();
        if (gateway?.Boss != null || gateway?.LastReport?.Events.Count > 0)
            _flags.Add("boss");
        if (gateway?.GroupFavor > 0)
            _flags.Add("group");
        if (AIManager.Get()?.AIs.Count > 0)
            _flags.Add("ai");
        foreach (NetAI ai in AIManager.Get()?.AIs ?? [])
            _flags.Add("ai:" + ai.Personality);
        if (map != null && ColonyGoods.Count(map, NetrunDefOf.NR_BlackBraindanceChip) > 0)
            _flags.Add("blackchip");
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref _flags, "flags", LookMode.Value);
        Scribe_Collections.Look(ref _seen, "seen", LookMode.Value);
        Scribe_Values.Look(ref _helpOpened, "helpOpened");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            _flags ??= [];
            _seen ??= [];
        }
    }
}
