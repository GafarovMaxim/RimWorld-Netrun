using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The cyberspace article's live demos: [[gateway]] (a small Gateway map with a card for the picked node),
/// [[hackchance]] (the hack formula with its bonuses) and [[trace]] (what a dive's trace turns into as Heat).
/// Numbers come straight from the settings, so the article never goes stale. Built from HelpKit's blocks.
/// </summary>
public partial class HelpBrowser
{
    private static CyberspaceSettingsDef Cyber => NetrunDefOf.NR_Cyberspace;

    /// <summary>One sample node per type, placed around the home node (x, y as fractions of the map).</summary>
    private static readonly (ExternalNodeType type, float x, float y, int defense, int risk)[] DemoNodes =
    [
        (ExternalNodeType.Market, 0.18f, 0.26f, 2, 0),
        (ExternalNodeType.Archive, 0.82f, 0.22f, 2, 0),
        (ExternalNodeType.Corp, 0.86f, 0.72f, 4, 1),
        (ExternalNodeType.Abandoned, 0.14f, 0.76f, 3, 1),
        (ExternalNodeType.DarkExchange, 0.5f, 0.88f, 5, 2),
        (ExternalNodeType.CorpCore, 0.5f, 0.12f, 6, 2),
    ];

    private ExternalNodeType _demoNode = ExternalNodeType.Corp;

    private static Color DemoColor(ExternalNodeType t) => t switch
    {
        ExternalNodeType.Market => NetrunUI.Accent,
        ExternalNodeType.Archive => NetrunUI.Ok,
        ExternalNodeType.Corp => NetrunUI.Bad,
        ExternalNodeType.Abandoned => NetrunUI.Purple,
        ExternalNodeType.CorpCore => new Color(0.85f, 0.15f, 0.25f),
        _ => NetrunUI.Warn,
    };

    private static Texture2D DemoIcon(ExternalNodeType t) => NetrunTex.CyberIcon("Node_" + t switch
    {
        ExternalNodeType.Corp => "CorpVault",
        ExternalNodeType.CorpCore => "CorpCore",
        ExternalNodeType.Abandoned => "AbandonedServer",
        _ => t.ToString(),
    });

    private static string TypeName(ExternalNodeType t) => ((string)("NR_ExtNodeType_" + t).Translate()).CapitalizeFirst();

    private static float DemoHeatPerTrace(ExternalNodeType t) => t switch
    {
        ExternalNodeType.CorpCore => 2f,
        ExternalNodeType.Corp => 1.5f,
        ExternalNodeType.DarkExchange => 1.2f,
        _ => 1f,
    };

    private static IEnumerable<Texture2D> DemoRewards(ExternalNodeType t)
    {
        if (t == ExternalNodeType.DarkExchange)
            yield return ThingDefOf.Silver.uiIcon;
        yield return NetrunDefOf.USH_Cyberdata.uiIcon;
        if (t is ExternalNodeType.Corp or ExternalNodeType.CorpCore)
            yield return NetrunDefOf.USH_AdvancedCyberdata.uiIcon;
        if (t == ExternalNodeType.CorpCore)
            yield return NetrunDefOf.NR_AICoreModule.uiIcon;
        if (t == ExternalNodeType.Abandoned)
            yield return NetrunDefOf.NR_AIFragment.uiIcon;
        if (t is ExternalNodeType.Corp or ExternalNodeType.DarkExchange or ExternalNodeType.CorpCore)
            yield return NetrunTex.CyberIcon("Reward_Valuables");
    }

    // ---- [[gateway]] ----

    private float GatewayDemo(float y, float w) => Frame("gateway", y, w, NetrunUI.Accent, inner =>
        Columns(inner, inner.y, 0.55f, (l, top) => DrawDemoMap(l, top), (r, top) => DemoNodeCard(r, top)));

    private float DrawDemoMap(Rect area, float top)
    {
        float mapH = Mathf.Clamp(area.width * 0.72f, 200f, 300f);
        var map = new Rect(area.x, top, area.width, mapH);
        Widgets.DrawBoxSolid(map, NetrunUI.BgWindow);
        GUI.color = new Color(1f, 1f, 1f, 0.06f);
        Widgets.DrawBox(map);
        GUI.color = Color.white;

        Vector2 home = map.center;
        float r = Mathf.Clamp(map.height * 0.11f, 20f, 30f);
        foreach (var n in DemoNodes)
        {
            Vector2 p = new(map.x + map.width * n.x, map.y + map.height * n.y);
            bool sel = n.type == _demoNode;
            Widgets.DrawLine(home, p, sel ? DemoColor(n.type) : new Color(1f, 1f, 1f, 0.15f), sel ? 2f : 1f);
        }
        var homeRect = new Rect(home.x - r * 1.15f, home.y - r * 1.15f, r * 2.3f, r * 2.3f);
        GUI.color = NetrunUI.BgWindow;
        GUI.DrawTexture(homeRect, NetrunUI.Disc);
        GUI.color = NetrunUI.Accent;
        GUI.DrawTexture(homeRect, NetrunUI.Ring);
        GUI.color = Color.white;
        if (NetrunTex.CyberIcon("Node_NeutralZone") is { } homeIcon)
            GUI.DrawTexture(homeRect.ContractedBy(r * 0.45f), homeIcon);
        NetrunUI.Tip(homeRect, "NR_HelpGwHome".Translate());

        foreach (var n in DemoNodes)
        {
            Vector2 p = new(map.x + map.width * n.x, map.y + map.height * n.y);
            var rect = new Rect(p.x - r, p.y - r, r * 2f, r * 2f);
            Color c = DemoColor(n.type);
            bool sel = n.type == _demoNode;
            if (sel || Mouse.IsOver(rect))
                NetrunUI.Glow(rect, c, sel ? 0.9f : 0.5f, 8f);
            GUI.color = new Color(c.r * 0.2f, c.g * 0.2f, c.b * 0.2f, 1f);
            GUI.DrawTexture(rect, NetrunUI.Disc);
            GUI.color = c;
            GUI.DrawTexture(rect, NetrunUI.Ring);
            GUI.color = Color.white;
            if (DemoIcon(n.type) is { } icon)
                GUI.DrawTexture(rect.ContractedBy(r * 0.38f), icon);
            NetrunUI.Tip(rect, TypeName(n.type));
            if (Widgets.ButtonInvisible(rect, doMouseoverSound: false))
            {
                _demoNode = n.type;
                NetrunSound.Click();
            }
        }
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(map.x + 6f, map.yMax - 18f, map.width - 12f, 18f), "NR_HelpGwHint".Translate().Colorize(NetrunUI.Warn));
        Text.Font = GameFont.Small;
        return map.yMax;
    }

    private float DemoNodeCard(Rect area, float top)
    {
        var n = DemoNodes.First(d => d.type == _demoNode);
        Color c = DemoColor(n.type);
        return Panel("gateway.card", area, top, c, cr =>
        {
            float y = cr.y;
            if (DemoIcon(n.type) is { } icon)
                GUI.DrawTexture(new Rect(cr.x, y, 30f, 30f), icon);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(cr.x + 38f, y - 2f, cr.width - 38f, 34f), TypeName(n.type).Colorize(c));
            Text.Font = GameFont.Small;
            y += 38f;
            // Defense as pips.
            float pipsY = y;
            y = KV(cr, y, "NR_GatewayRowDefense".Translate(), string.Empty);
            float lw = Mathf.Min(130f, cr.width * 0.4f);
            for (int i = 0; i < 7; i++)
                Widgets.DrawBoxSolid(new Rect(cr.x + lw + 8f + i * 14f, pipsY + 8f, 11f, 8f), i < n.defense ? c : NetrunUI.BgCardAlt);
            y = KV(cr, y, "NR_GatewayRowRisk".Translate(), ("NR_GatewayRisk_" + n.risk).Translate(), n.risk == 2 ? NetrunUI.Bad : n.risk == 1 ? NetrunUI.Warn : NetrunUI.Ok,
                icon: NetrunTex.CyberIcon(n.risk switch { 2 => "Dive_Lightning_Red", 1 => "Dive_Lightning_Yellow", _ => "Dive_Lightning_Grey" }));
            y = KV(cr, y, "NR_GatewayRowTrace".Translate(), "×" + DemoHeatPerTrace(n.type).ToString("0.0"));
            float rewardY = y;
            y = KV(cr, y, "NR_GatewayRowReward".Translate(), string.Empty);
            float x = cr.x + lw + 8f;
            foreach (Texture2D tex in DemoRewards(n.type))
            {
                if (tex == null)
                    continue;
                GUI.DrawTexture(new Rect(x, rewardY + 2f, 20f, 20f), tex);
                x += 24f;
            }
            return Para(cr, y + 4f, ("NR_HelpGwNote_" + n.type).Translate(), NetrunUI.TextColor);
        });
    }

    // ---- [[hackchance]] ----

    private float _demoSkill = 8f;
    private float _demoStrength = 3f;
    private bool _demoSword, _demoFlow, _demoNatural, _demoBurned;

    private float HackChanceDemo(float y, float w) => Frame("hack", y, w, NetrunUI.Warn, inner => Columns(inner, inner.y, 0.55f,
        (l, top) =>
        {
            top = Slider(l, top, "NR_HelpHackSkill".Translate(Mathf.RoundToInt(_demoSkill)), ref _demoSkill, 0f, 20f);
            top = Slider(l, top, "NR_HelpHackStrength".Translate(Mathf.RoundToInt(_demoStrength)), ref _demoStrength, 1f, 7f);
            top = Toggle(l, top, "NR_HelpHackSword".Translate(Cyber.swordBonus.ToStringPercent()), ref _demoSword);
            top = Toggle(l, top, "NR_HelpHackFlow".Translate(Cyber.effectFlowHackBonus.ToStringPercent()), ref _demoFlow);
            top = Toggle(l, top, "NR_HelpHackNatural".Translate(), ref _demoNatural);
            return Toggle(l, top, "NR_HelpHackBurned".Translate(Cyber.effectBurnedHackPenalty.ToStringPercent()), ref _demoBurned);
        },
        (r, top) =>
        {
            int skill = Mathf.RoundToInt(_demoSkill), strength = Mathf.RoundToInt(_demoStrength);
            float raw = Cyber.hackBase + skill * Cyber.hackPerSkill - strength * Cyber.hackPerStrength
                        + (_demoSword ? Cyber.swordBonus : 0f) + (_demoFlow ? Cyber.effectFlowHackBonus : 0f)
                        + (_demoNatural ? 0.05f : 0f) - (_demoBurned ? Cyber.effectBurnedHackPenalty : 0f);
            float chance = Mathf.Clamp(raw, 0.05f, 0.95f);
            Color cc = chance >= 0.6f ? NetrunUI.Ok : chance >= 0.35f ? NetrunUI.Warn : NetrunUI.Bad;
            top = Result(r, top, chance.ToStringPercent(), chance, cc);
            top = Para(r, top, "NR_HelpHackFormula".Translate(Cyber.hackBase.ToStringPercent(), skill, Cyber.hackPerSkill.ToStringPercent(), strength,
                Cyber.hackPerStrength.ToStringPercent(), raw.ToStringPercent()), NetrunUI.TextColor);
            return Para(r, top, "NR_HelpHackClamp".Translate(), NetrunUI.Muted, GameFont.Tiny);
        }));

    // ---- [[trace]] ----

    private float _demoTrace = 6f;
    private ExternalNodeType _demoTraceNode = ExternalNodeType.Corp;
    private bool _demoQuiet, _demoStealth;

    private float TraceDemo(float y, float w) => Frame("trace", y, w, NetrunUI.Bad, inner =>
    {
        float top = Choices(inner, inner.y, DemoNodes.Select(n => n.type), TypeName, t => t == _demoTraceNode, t => _demoTraceNode = t,
            DemoColor, DemoIcon);
        return Columns(inner, top, 0.55f,
            (l, t) =>
            {
                t = Slider(l, t, "NR_HelpTraceAmount".Translate(Mathf.RoundToInt(_demoTrace)), ref _demoTrace, 0f, 16f);
                t = Toggle(l, t, "NR_HelpTraceQuiet".Translate(Cyber.newsQuietTrace.ToStringPercent()), ref _demoQuiet);
                return Toggle(l, t, "NR_HelpTraceStealth".Translate(Cyber.stealthTraceFactor.ToStringPercent()), ref _demoStealth);
            },
            (r, t) =>
            {
                float heat = Mathf.RoundToInt(_demoTrace) * (_demoStealth ? Cyber.stealthTraceFactor : 1f) * DemoHeatPerTrace(_demoTraceNode)
                             * (_demoQuiet ? Cyber.newsQuietTrace : 1f) * NetrunMod.Settings.HeatGainMultiplier;
                t = Result(r, t, "+" + heat.ToString("0") + " Heat", heat / 32f, NetrunUI.Warn);
                return Para(r, t, "NR_HelpTraceNote".Translate(DemoHeatPerTrace(_demoTraceNode).ToString("0.0")), NetrunUI.TextColor);
            });
    });
}
