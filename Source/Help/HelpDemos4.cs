using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Live blocks for the AI articles: [[aihomes]] (server, coprocessor, host: what each one gives), [[aihunger]]
/// (hunger and trust over the days), [[aitraits]] (every trait with its plus and minus), [[aiawaken]] (where
/// fragments come from and what waking costs).
/// </summary>
public partial class HelpBrowser
{
    private static AISettingsDef AIS => NetrunDefOf.NR_AI;

    // ---- [[aihomes]] ----

    private int _demoHome;
    private static readonly string[] Homes = ["Server", "Coprocessor", "Host"];

    private static Texture2D HomeIcon(int i) => i switch
    {
        0 => NetrunDefOf.NR_AIServer?.uiIcon,
        1 => NetrunDefOf.NR_NetrunCoprocessor?.uiIcon,
        _ => NetrunTex.AIEmbody,
    };

    private float AIHomesDemo(float y, float w) => Frame("aihomes", y, w, NetrunUI.Purple, inner =>
    {
        float tileW = (inner.width - 2 * 10f) / 3f;
        for (int i = 0; i < Homes.Length; i++)
        {
            var r = new Rect(inner.x + i * (tileW + 10f), inner.y, tileW, 64f);
            bool sel = i == _demoHome;
            if (sel)
                NetrunUI.Glow(r, NetrunUI.Purple, 0.7f, 8f);
            NetrunUI.Box(r, NetrunUI.BgWindow, sel ? NetrunUI.Purple : new Color(1f, 1f, 1f, 0.15f), sel ? 2 : 1);
            if (HomeIcon(i) is { } icon)
                GUI.DrawTexture(new Rect(r.x + 10f, r.y + 8f, 48f, 48f), icon);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(r.x + 66f, r.y, r.width - 70f, r.height), ((string)("NR_HelpAIHome_" + Homes[i]).Translate()).Colorize(sel ? NetrunUI.Purple : NetrunUI.TextColor));
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(r, doMouseoverSound: false))
            {
                _demoHome = i;
                NetrunSound.Click();
            }
        }
        string h = Homes[_demoHome];
        return Panel("aihomes.card", inner, inner.y + 74f, NetrunUI.Purple, cr =>
        {
            float t = KV(cr, cr.y, "NR_HelpAIHomeService".Translate(), ("NR_HelpAIHomeService_" + h).Translate());
            t = KV(cr, t, "NR_HelpAIHomeHunger".Translate(), ("NR_HelpAIHomeHunger_" + h).Translate(AIS.hostHungerReliefPerDay.ToString("0"), AIS.hungerPerDay.ToString("0")));
            return KV(cr, t, "NR_HelpAIHomeGives".Translate(), ("NR_HelpAIHomeGives_" + h).Translate());
        });
    });

    // ---- [[aihunger]] ----

    private float _demoDays = 4f;
    private bool _demoFed, _demoIsolated, _demoHoarder;

    private float AIHungerDemo(float y, float w) => Frame("aihunger", y, w, NetrunUI.Warn, inner => Columns(inner, inner.y, 0.5f,
        (l, top) =>
        {
            top = Slider(l, top, "NR_HelpAIHungerDays".Translate(Mathf.RoundToInt(_demoDays)), ref _demoDays, 0f, 10f);
            top = Toggle(l, top, "NR_HelpAIHungerFed".Translate(AIS.feedHungerRelief.ToString("0"), AIS.feedTrust.ToString("0")), ref _demoFed);
            top = Toggle(l, top, "NR_HelpAIHungerIsolated".Translate(AIS.isolatedHungerFactor.ToString("0.#")), ref _demoIsolated);
            return Toggle(l, top, "NR_HelpAIHungerHoarder".Translate(AIS.hoarderHungerFactor.ToStringPercent()), ref _demoHoarder);
        },
        (r, top) =>
        {
            // Day by day: hunger grows; above the starving mark trust drains; isolation also costs trust.
            float hunger = 0f, trust = AIS.startTrust;
            float perDay = AIS.hungerPerDay * (_demoIsolated ? AIS.isolatedHungerFactor : 1f) * (_demoHoarder ? AIS.hoarderHungerFactor : 1f);
            int days = Mathf.RoundToInt(_demoDays);
            for (int d = 0; d < days; d++)
            {
                hunger = Mathf.Min(100f, hunger + perDay);
                if (hunger >= AIS.starvingHunger)
                    trust -= AIS.starvingTrustPerDay;
                if (_demoIsolated)
                    trust -= AIS.isolatedTrustPerDay;
            }
            if (_demoFed)
            {
                hunger = Mathf.Max(0f, hunger - AIS.feedHungerRelief);
                trust += AIS.feedTrust;
            }
            trust = Mathf.Clamp(trust, 0f, 100f);
            Color hc = hunger >= AIS.starvingHunger ? NetrunUI.Bad : hunger >= 40f ? NetrunUI.Warn : NetrunUI.Ok;
            top = Result(r, top, "NR_HelpAIHungerValue".Translate(hunger.ToString("0")), hunger / 100f, hc);
            top = KV(r, top, "NR_HelpAIHungerTrust".Translate(), trust.ToString("0") + " / 100", trust < AIS.escapeTrust ? NetrunUI.Bad : NetrunUI.Ok);
            string state = hunger >= AIS.starvingHunger
                ? (trust < AIS.escapeTrust && hunger > AIS.escapeHunger ? "NR_HelpAIHungerRun" : "NR_HelpAIHungerStarving").Translate()
                : "NR_HelpAIHungerFine".Translate();
            return Para(r, top + 2f, state, hunger >= AIS.starvingHunger ? NetrunUI.Bad : NetrunUI.TextColor);
        }));

    // ---- [[aitraits]] ----

    private AITrait _demoTrait = AITrait.Curious;

    private float AITraitsDemo(float y, float w) => Frame("aitraits", y, w, NetrunUI.Purple, inner =>
    {
        float t = Choices(inner, inner.y, (AITrait[])System.Enum.GetValues(typeof(AITrait)), tr => tr.Label(), tr => tr == _demoTrait, tr => _demoTrait = tr, _ => NetrunUI.Purple);
        return Panel("aitraits.card", inner, t, NetrunUI.Purple, cr =>
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(cr.x, cr.y, cr.width, 32f), _demoTrait.Label().Colorize(NetrunUI.Purple));
            Text.Font = GameFont.Small;
            string[] parts = _demoTrait.Description().Split('\n');
            float u = cr.y + 36f;
            foreach (string part in parts.Where(p => !p.NullOrEmpty()))
                u = Para(cr, u, part, part.TrimStart().StartsWith("+") ? NetrunUI.Ok : NetrunUI.Bad);
            return Para(cr, u + 4f, "NR_HelpAITraitsReveal".Translate(AIS.traitRevealTrustFirst.ToString("0"), AIS.traitRevealTrustSecond.ToString("0")), NetrunUI.Muted, GameFont.Tiny);
        });
    });

    // ---- [[market]] ----

    private float _demoShopRep = 20f;
    private bool _demoShopBlack;

    /// <summary>A fence's price list from the real settings and the real price code, with reputation and the black market.</summary>
    private float MarketDemo(float y, float w) => Frame("market", y, w, NetrunUI.Ok, inner =>
    {
        FixerSettingsDef s = NetrunDefOf.NR_Fixers;
        float t = Slider(inner, inner.y, "NR_HelpFixerRep".Translate(Mathf.RoundToInt(_demoShopRep)), ref _demoShopRep, 0f, 100f);
        t = Toggle(inner, t, "NR_HelpMarketBlack".Translate(s.blackMarketMarkup.ToStringPercent()), ref _demoShopBlack);
        var fixer = new Fixer { Specialty = FixerSpecialty.Fence, Trait = FixerTrait.Reliable, Reputation = _demoShopRep };
        var items = s.shopItems.Take(6).ToList();
        fixer.Stock = _demoShopBlack ? [] : items.ToList();
        bool pinned = _demoShopRep >= s.pinnedReputation;
        if (pinned)
            items.AddRange(s.pinnedShopItems.Where(d => !items.Contains(d)).Take(3));
        foreach (ThingDef def in items)
        {
            bool rare = s.pinnedShopItems.Contains(def);
            Text.Font = GameFont.Small;
            var row = new Rect(inner.x, t, inner.width, 32f);
            if (Mouse.IsOver(row))
                Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.04f));
            Widgets.ThingIcon(new Rect(row.x + 2f, row.y + 3f, 26f, 26f), def);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x + 36f, row.y, row.width - 160f, row.height), def.LabelCap.ToString().Truncate(row.width - 160f).Colorize(rare ? NetrunUI.Warn : NetrunUI.TextColor));
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(row.xMax - 140f, row.y, 136f, row.height), "NR_HelpContractFlat".Translate(fixer.PriceOf(def)).Colorize(NetrunUI.Ok));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = new Color(1f, 1f, 1f, 0.07f);
            Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
            GUI.color = Color.white;
            t = row.yMax + 1f;
        }
        return Para(inner, t + 6f, pinned ? "NR_HelpFixerRareOn".Translate() : "NR_HelpFixerRareOff".Translate(), pinned ? NetrunUI.Warn : NetrunUI.Muted, GameFont.Tiny);
    });

    // ---- [[aiawaken]] ----

    private static readonly string[] FragmentSources = ["Dive", "Memory", "WildAI", "Distress", "Market", "Hack"];

    private float AIAwakenDemo(float y, float w) => Frame("aiawaken", y, w, NetrunUI.Purple, inner =>
    {
        float t = Para(inner, inner.y, "NR_HelpAwakenSources".Translate(), NetrunUI.Muted, GameFont.Tiny);
        Texture2D SourceIcon(string s) => s switch
        {
            "Dive" => NetrunTex.Dive,
            "Memory" => NetrunTex.CyberIcon("Dive_MEMORY"),
            "WildAI" => NetrunTex.CyberIcon("Event_WildAI"),
            "Distress" => NetrunTex.CyberIcon("Event_DistressSignal"),
            "Market" => NetrunTex.CyberIcon("Event_BlackMarket"),
            _ => NetrunTex.CyberIcon("Action_Hack"),
        };
        foreach (string s in FragmentSources)
            t = KV(inner, t, ("NR_HelpAwakenSrc_" + s).Translate(), ("NR_HelpAwakenSrcDesc_" + s).Translate(), null, 150f, SourceIcon(s));
        float ticks = AIS.awakenWorkTicks;
        return Panel("aiawaken.card", inner, t + 8f, NetrunUI.Purple, cr =>
        {
            float u = KV(cr, cr.y, "NR_HelpAwakenWork".Translate(), "NR_HelpAwakenWorkValue".Translate((ticks / GenDate.TicksPerHour).ToString("0.#")), NetrunUI.TextColor, 150f, NetrunDefOf.NR_AIFragment?.uiIcon);
            u = KV(cr, u, "NR_HelpAwakenNoise".Translate(), "NR_HelpAwakenNoiseValue".Translate((AIS.awakenDetectChancePerHeat * 50f).ToStringPercent(), AIS.awakenDetectHeat.ToString("0")), NetrunUI.Warn, 150f, NetrunTex.HeatRise);
            return KV(cr, u, "NR_HelpAwakenTraits".Translate(), "NR_HelpAwakenTraitsValue".Translate(AIS.traitNoneChance.ToStringPercent()), NetrunUI.Purple, 150f, NetrunTex.AIAwaken);
        });
    });
}
