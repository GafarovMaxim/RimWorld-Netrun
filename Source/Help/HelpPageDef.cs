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

    public bool IsTopic => parent == null;

    public IEnumerable<HelpPageDef> Children => DefDatabase<HelpPageDef>.AllDefs.Where(p => p.parent == this).OrderBy(p => p.order);

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
            ["dive.search"] = cyber.searchCost.ToString(),
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
        };
        foreach (KeyValuePair<string, string> kv in values)
            text = text.Replace("{" + kv.Key + "}", kv.Value);
        return text;
    }
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

    public bool Unlocked(HelpPageDef page) => page.unlockFlag.NullOrEmpty() || _flags.Contains(page.unlockFlag) || _flags.Contains("all");
    public bool IsNew(HelpPageDef page) => !page.unlockFlag.NullOrEmpty() && Unlocked(page) && !_seen.Contains(page.defName);
    public void MarkSeen(HelpPageDef page) => _seen.Add(page.defName);
    public void UnlockAll() => _flags.Add("all");

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
