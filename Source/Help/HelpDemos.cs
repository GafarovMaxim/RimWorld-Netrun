using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The cyberspace article's live demos: [[gateway]] (a small Gateway map with a card for the picked node),
/// [[hackchance]] (the hack formula with its bonuses) and [[trace]] (what a dive's trace turns into as Heat).
/// Numbers come straight from the settings, so the article never goes stale.
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

    private static Rect DemoBox(float y, float w, float h, Color accent)
    {
        var box = new Rect(0f, y, w, h);
        Widgets.DrawBoxSolid(box, NetrunUI.BgCard);
        NetrunUI.Brackets(box, new Color(accent.r, accent.g, accent.b, 0.8f), 2f, 16f);
        return box.ContractedBy(10f);
    }

    // ---- [[gateway]] ----

    private float GatewayDemo(float y, float w)
    {
        bool side = w >= 560f;
        float mapW = side ? w * 0.56f : w - 20f;
        float mapH = Mathf.Min(260f, mapW * 0.7f);
        float cardH = 196f;
        Rect inner = DemoBox(y, w, (side ? Mathf.Max(mapH, cardH) : mapH + cardH + 10f) + 20f, NetrunUI.Accent);
        var map = new Rect(inner.x, inner.y, mapW, mapH);
        Widgets.DrawBoxSolid(map, NetrunUI.BgWindow);
        GUI.color = new Color(1f, 1f, 1f, 0.06f);
        Widgets.DrawBox(map);
        GUI.color = Color.white;

        Vector2 home = new(map.x + map.width * 0.5f, map.y + map.height * 0.5f);
        float r = Mathf.Clamp(map.height * 0.11f, 20f, 30f);
        foreach (var n in DemoNodes)
        {
            Vector2 p = new(map.x + map.width * n.x, map.y + map.height * n.y);
            Color c = DemoColor(n.type);
            bool sel = n.type == _demoNode;
            Widgets.DrawLine(home, p, sel ? c : new Color(1f, 1f, 1f, 0.15f), sel ? 2f : 1f);
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
            bool hover = Mouse.IsOver(rect);
            if (sel || hover)
                NetrunUI.Glow(rect, c, sel ? 0.9f : 0.5f, 8f);
            GUI.color = new Color(c.r * 0.2f, c.g * 0.2f, c.b * 0.2f, 1f);
            GUI.DrawTexture(rect, NetrunUI.Disc);
            GUI.color = c;
            GUI.DrawTexture(rect, NetrunUI.Ring);
            GUI.color = Color.white;
            if (DemoIcon(n.type) is { } icon)
                GUI.DrawTexture(rect.ContractedBy(r * 0.38f), icon);
            NetrunUI.Tip(rect, ("NR_ExtNodeType_" + n.type).Translate());
            if (Widgets.ButtonInvisible(rect))
            {
                _demoNode = n.type;
                NetrunSound.Click();
            }
        }
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(map.x + 6f, map.yMax - 18f, map.width - 12f, 18f), "NR_HelpGwHint".Translate().Colorize(NetrunUI.Warn));
        Text.Font = GameFont.Small;

        // The card of the picked node.
        var card = side ? new Rect(map.xMax + 12f, inner.y, inner.xMax - map.xMax - 12f, cardH) : new Rect(inner.x, map.yMax + 10f, inner.width, cardH);
        DemoNodeCard(card, DemoNodes.First(n => n.type == _demoNode));
        return inner.yMax + 20f;
    }

    private static void DemoNodeCard(Rect card, (ExternalNodeType type, float x, float y, int defense, int risk) n)
    {
        Color c = DemoColor(n.type);
        Widgets.DrawBoxSolid(card, NetrunUI.BgWindow);
        Widgets.DrawBoxSolid(new Rect(card.x, card.y, 3f, card.height), c);
        Rect r = card.ContractedBy(8f);
        if (DemoIcon(n.type) is { } icon)
            GUI.DrawTexture(new Rect(r.x, r.y, 28f, 28f), icon);
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(r.x + 34f, r.y - 2f, r.width - 34f, 30f), ((string)("NR_ExtNodeType_" + n.type).Translate()).Colorize(c));
        Text.Font = GameFont.Small;
        float y = r.y + 34f;

        float Line(string label, System.Action<Rect> value)
        {
            var row = new Rect(r.x, y, r.width, 24f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(row.x, row.y, 96f, row.height), label.Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
            value(new Rect(row.x + 100f, row.y, row.width - 100f, row.height));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = new Color(1f, 1f, 1f, 0.07f);
            Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
            GUI.color = Color.white;
            return y += 26f;
        }

        Line("NR_GatewayRowDefense".Translate(), v =>
        {
            for (int i = 0; i < 7; i++)
                Widgets.DrawBoxSolid(new Rect(v.x + i * 14f, v.center.y - 4f, 11f, 8f), i < n.defense ? c : NetrunUI.BgCardAlt);
        });
        Line("NR_GatewayRowRisk".Translate(), v =>
        {
            if (NetrunTex.CyberIcon(n.risk switch { 2 => "Dive_Lightning_Red", 1 => "Dive_Lightning_Yellow", _ => "Dive_Lightning_Grey" }) is { } risk)
                GUI.DrawTexture(new Rect(v.x, v.center.y - 9f, 18f, 18f), risk);
            Widgets.Label(new Rect(v.x + 22f, v.y, v.width - 22f, v.height), ("NR_GatewayRisk_" + n.risk).Translate());
        });
        Line("NR_GatewayRowTrace".Translate(), v => Widgets.Label(v, "×" + DemoHeatPerTrace(n.type).ToString("0.0")));
        Line("NR_GatewayRowReward".Translate(), v =>
        {
            float x = v.x;
            foreach (Texture2D tex in DemoRewards(n.type))
            {
                if (tex == null)
                    continue;
                GUI.DrawTexture(new Rect(x, v.center.y - 10f, 20f, 20f), tex);
                x += 24f;
            }
        });
        Text.Font = GameFont.Tiny;
        string note = ("NR_HelpGwNote_" + n.type).Translate();
        Widgets.Label(new Rect(r.x, y + 2f, r.width, r.yMax - y), note.Colorize(NetrunUI.TextColor));
        Text.Font = GameFont.Small;
    }

    // ---- [[hackchance]] ----

    private int _demoSkill = 8;
    private int _demoStrength = 3;
    private bool _demoSword, _demoFlow, _demoNatural, _demoBurned;

    private float HackChanceDemo(float y, float w)
    {
        Rect inner = DemoBox(y, w, 176f, NetrunUI.Warn);
        float half = inner.width * 0.5f - 8f;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(inner.x, inner.y, half, 18f), "NR_HelpHackSkill".Translate(_demoSkill).Colorize(NetrunUI.Muted));
        _demoSkill = Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(inner.x, inner.y + 18f, half, 16f), _demoSkill, 0f, 20f, roundTo: 1f));
        Widgets.Label(new Rect(inner.x, inner.y + 40f, half, 18f), "NR_HelpHackStrength".Translate(_demoStrength).Colorize(NetrunUI.Muted));
        _demoStrength = Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(inner.x, inner.y + 58f, half, 16f), _demoStrength, 1f, 7f, roundTo: 1f));
        float cy = inner.y + 82f;
        Text.Font = GameFont.Small;
        Widgets.CheckboxLabeled(new Rect(inner.x, cy, half, 22f), "NR_HelpHackSword".Translate(Cyber.swordBonus.ToStringPercent()), ref _demoSword);
        Widgets.CheckboxLabeled(new Rect(inner.x, cy + 22f, half, 22f), "NR_HelpHackFlow".Translate(Cyber.effectFlowHackBonus.ToStringPercent()), ref _demoFlow);
        Widgets.CheckboxLabeled(new Rect(inner.x, cy + 44f, half, 22f), "NR_HelpHackNatural".Translate(), ref _demoNatural);
        Widgets.CheckboxLabeled(new Rect(inner.x, cy + 66f, half, 22f), "NR_HelpHackBurned".Translate(Cyber.effectBurnedHackPenalty.ToStringPercent()), ref _demoBurned);

        float raw = Cyber.hackBase + _demoSkill * Cyber.hackPerSkill - _demoStrength * Cyber.hackPerStrength
                    + (_demoSword ? Cyber.swordBonus : 0f) + (_demoFlow ? Cyber.effectFlowHackBonus : 0f)
                    + (_demoNatural ? 0.05f : 0f) - (_demoBurned ? Cyber.effectBurnedHackPenalty : 0f);
        float chance = Mathf.Clamp(raw, 0.05f, 0.95f);
        Color cc = chance >= 0.6f ? NetrunUI.Ok : chance >= 0.35f ? NetrunUI.Warn : NetrunUI.Bad;
        float rx = inner.x + half + 16f;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(rx, inner.y, half, 34f), chance.ToStringPercent().Colorize(cc));
        Text.Font = GameFont.Small;
        NetrunUI.Bar(new Rect(rx, inner.y + 38f, half, 12f), chance, cc);
        Text.Font = GameFont.Tiny;
        string formula = "NR_HelpHackFormula".Translate(Cyber.hackBase.ToStringPercent(), _demoSkill, Cyber.hackPerSkill.ToStringPercent(), _demoStrength,
            Cyber.hackPerStrength.ToStringPercent(), raw.ToStringPercent());
        Widgets.Label(new Rect(rx, inner.y + 56f, half, 60f), formula.Colorize(NetrunUI.Muted));
        Widgets.Label(new Rect(rx, inner.y + 112f, half, 40f), "NR_HelpHackClamp".Translate().Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        return inner.yMax + 20f;
    }

    // ---- [[trace]] ----

    private int _demoTrace = 6;
    private ExternalNodeType _demoTraceNode = ExternalNodeType.Corp;
    private bool _demoQuiet, _demoStealth;

    private float TraceDemo(float y, float w)
    {
        Rect inner = DemoBox(y, w, 150f, NetrunUI.Bad);
        float x = inner.x;
        foreach (var n in DemoNodes)
        {
            string label = ("NR_ExtNodeType_" + n.type).Translate();
            float bw = NetrunUI.ButtonWidth(label, icon: true);
            if (x + bw > inner.xMax)
                break;
            bool on = n.type == _demoTraceNode;
            if (NetrunUI.Button(new Rect(x, inner.y, bw, 26f), label, on ? DemoColor(n.type) : NetrunUI.Muted, true, null, DemoIcon(n.type)))
                _demoTraceNode = n.type;
            x += bw + 6f;
        }
        float half = inner.width * 0.5f - 8f;
        float top = inner.y + 36f;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(inner.x, top, half, 18f), "NR_HelpTraceAmount".Translate(_demoTrace).Colorize(NetrunUI.Muted));
        _demoTrace = Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(inner.x, top + 18f, half, 16f), _demoTrace, 0f, 16f, roundTo: 1f));
        Text.Font = GameFont.Small;
        Widgets.CheckboxLabeled(new Rect(inner.x, top + 42f, half, 22f), "NR_HelpTraceQuiet".Translate(Cyber.newsQuietTrace.ToStringPercent()), ref _demoQuiet);
        Widgets.CheckboxLabeled(new Rect(inner.x, top + 64f, half, 22f), "NR_HelpTraceStealth".Translate(Cyber.stealthTraceFactor.ToStringPercent()), ref _demoStealth);

        float heat = _demoTrace * (_demoStealth ? Cyber.stealthTraceFactor : 1f) * DemoHeatPerTrace(_demoTraceNode) * (_demoQuiet ? Cyber.newsQuietTrace : 1f)
                     * NetrunMod.Settings.HeatGainMultiplier;
        float rx = inner.x + half + 16f;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(rx, top - 4f, half, 34f), ("+" + heat.ToString("0") + " Heat").Colorize(NetrunUI.Warn));
        Text.Font = GameFont.Small;
        NetrunUI.Bar(new Rect(rx, top + 32f, half, 12f), Mathf.Clamp01(heat / 32f), NetrunUI.Warn);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(rx, top + 50f, half, 50f), "NR_HelpTraceNote".Translate(DemoHeatPerTrace(_demoTraceNode).ToString("0.0")).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        return inner.yMax + 20f;
    }
}
