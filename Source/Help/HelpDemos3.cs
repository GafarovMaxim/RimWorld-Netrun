using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Live blocks for "Getting started" and "Heat": [[checklist]] (your real first steps, ticked from the game),
/// [[terminalmap]] (the terminal's nodes, click to read), [[hackheat]] (the trace of one hack), [[newcomer]] (the
/// protection's state), [[lowerheat]] (the ways down, compared on your numbers).
/// </summary>
public partial class HelpBrowser
{
    // ---- [[checklist]] ----

    private float ChecklistDemo(float y, float w) => Frame("checklist", y, w, NetrunUI.Ok, inner =>
    {
        bool game = Current.Game != null;
        bool console = game && Find.Maps.Any(m => AIAwakening.Console(m) != null);
        bool pod = game && Find.Maps.Any(m => m.GetComponent<USH_HE.MapComponent_CyberpodManager>()?.Pods.Any() == true);
        bool linked = game && PawnsFinder.AllMaps_FreeColonists.Any(p => USH_HE.CyberUtils.IsHacker(p));
        bool hacked = game && (HeatTracker.Get()?.Log.Count ?? 0) > 0;
        bool fixer = game && FixerManager.Get()?.Fixers.Any(f => f.Reputation > 0f) == true;
        bool ai = game && AIManager.Get()?.AIs.Count > 0;
        bool dove = game && GatewayManager.Get()?.Reports.Count > 0;
        (bool done, string key, Texture2D icon)[] steps =
        [
            (console, "Console", ThingDefOf.CommsConsole.uiIcon),
            (pod, "Pod", DefDatabase<ThingDef>.GetNamedSilentFail("USH_Cyberpod")?.uiIcon),
            (linked, "Link", DefDatabase<ThingDef>.GetNamedSilentFail("USH_Cyberlink")?.uiIcon),
            (hacked, "Hack", NetrunTex.CyberIcon("Action_Hack")),
            (fixer, "Fixer", NetrunTex.CallFixer),
            (dove, "Dive", NetrunTex.Dive),
            (ai, "AI", NetrunTex.AIAwaken),
        ];
        int count = steps.Count(s => s.done);
        float t = Result(inner, inner.y, "NR_HelpChecklistDone".Translate(count, steps.Length), count / (float)steps.Length, NetrunUI.Ok);
        foreach ((bool done, string key, Texture2D icon) in steps)
        {
            Text.Font = GameFont.Small;
            string text = ("NR_HelpChecklist_" + key).Translate();
            float h = Mathf.Max(30f, Text.CalcHeight(text, inner.width - 70f) + 6f);
            var row = new Rect(inner.x, t, inner.width, h);
            if (done)
                Widgets.DrawBoxSolid(row, new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.06f));
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x + 4f, row.y, 20f, h), (done ? "✓" : "○").Colorize(done ? NetrunUI.Ok : NetrunUI.Muted));
            if (icon != null)
                GUI.DrawTexture(new Rect(row.x + 26f, row.y + (h - 24f) / 2f, 24f, 24f), icon);
            Widgets.Label(new Rect(row.x + 58f, row.y, row.width - 60f, h), text.Colorize(done ? NetrunUI.Muted : NetrunUI.TextColor));
            Text.Anchor = TextAnchor.UpperLeft;
            t += h + 2f;
        }
        return t;
    });

    // ---- [[terminalmap]] ----

    private static readonly string[] TerminalNodes = ["Core", "Club", "Board", "Archive", "Server", "Studio", "Gateway"];
    private int _demoTerminalNode;

    private float TerminalMapDemo(float y, float w) => Frame("terminalmap", y, w, NetrunUI.Accent, inner =>
    {
        const float tile = 64f, gap = 10f;
        int perRow = Mathf.Max(3, Mathf.FloorToInt((inner.width + gap) / (tile + gap)));
        float rowH = tile + 26f;
        for (int i = 0; i < TerminalNodes.Length; i++)
        {
            var r = new Rect(inner.x + i % perRow * (tile + gap), inner.y + i / perRow * rowH, tile, tile);
            bool sel = i == _demoTerminalNode;
            Color c = sel ? NetrunUI.Accent : NetrunUI.Muted;
            if (sel)
                NetrunUI.Glow(r, c, 0.7f, 8f);
            GUI.color = NetrunUI.BgWindow;
            GUI.DrawTexture(r, NetrunUI.Disc);
            GUI.color = c;
            GUI.DrawTexture(r, NetrunUI.Ring);
            GUI.color = Color.white;
            Texture2D icon = i == 0 ? NetrunTex.HeatIcon(HeatTracker.Get()?.Level ?? HeatLevel.Shadow)
                : ContentFinder<Texture2D>.Get("UI/Netrun/Nodes/Home_" + TerminalNodes[i], reportFailure: false);
            if (icon != null)
                GUI.DrawTexture(r.ContractedBy(14f), icon);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(r.x - gap / 2f, r.yMax + 3f, r.width + gap, 18f), ((string)("NR_HelpTermNode_" + TerminalNodes[i]).Translate()).Colorize(sel ? NetrunUI.Accent : NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (Widgets.ButtonInvisible(r, doMouseoverSound: false))
            {
                _demoTerminalNode = i;
                NetrunSound.Click();
            }
        }
        float top = inner.y + Mathf.CeilToInt(TerminalNodes.Length / (float)perRow) * rowH + 4f;
        return Panel("terminalmap.card", inner, top, NetrunUI.Accent, cr =>
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(cr.x, cr.y, cr.width, 32f), ((string)("NR_HelpTermNode_" + TerminalNodes[_demoTerminalNode]).Translate()).Colorize(NetrunUI.Accent));
            Text.Font = GameFont.Small;
            return Para(cr, cr.y + 34f, ("NR_HelpTermNodeDesc_" + TerminalNodes[_demoTerminalNode]).Translate(), NetrunUI.TextColor);
        });
    });

    // ---- [[hackheat]] ----

    private float _demoDefence = 6000f;
    private float _demoStealthStat = 100f;
    private bool _demoInPod;

    private float HackHeatDemo(float y, float w) => Frame("hackheat", y, w, NetrunUI.Warn, inner => Columns(inner, inner.y, 0.55f,
        (l, top) =>
        {
            top = Slider(l, top, "NR_HelpHackDefence".Translate(Mathf.RoundToInt(_demoDefence)), ref _demoDefence, 0f, 16000f, 500f);
            top = Slider(l, top, "NR_HelpHackStealth".Translate(Mathf.RoundToInt(_demoStealthStat)), ref _demoStealthStat, 25f, 400f, 5f);
            return Toggle(l, top, "NR_HelpHackInPod".Translate(NetrunDefOf.NR_Heat.cyberpodHeatFactor.ToStringPercent()), ref _demoInPod);
        },
        (r, top) =>
        {
            HeatSettingsDef s = NetrunDefOf.NR_Heat;
            float heat = s.hackHeat.ClampToRange(_demoDefence / s.defencePerHeat);
            heat *= s.stealthFactor.ClampToRange(s.baseStealth / Mathf.Max(1f, _demoStealthStat));
            if (_demoInPod)
                heat *= s.cyberpodHeatFactor;
            heat *= NetrunMod.Settings.HeatGainMultiplier;
            top = Result(r, top, "+" + heat.ToString("0.#") + " Heat", heat / s.hackHeat.max, NetrunUI.Warn);
            return Para(r, top, "NR_HelpHackHeatNote".Translate(s.hackHeat.min.ToString("0"), s.hackHeat.max.ToString("0")), NetrunUI.TextColor);
        }));

    // ---- [[newcomer]] ----

    private float NewcomerDemo(float y, float w) => Frame("newcomer", y, w, NetrunUI.Muted, inner =>
    {
        bool on = NetrunMod.Settings.NewcomerProtection;
        bool open = NetworkGate.IsOpen;
        float t = KV(inner, inner.y, "NR_HelpNewcomerSetting".Translate(), on ? "NR_HelpOn".Translate() : "NR_HelpOff".Translate(), on ? NetrunUI.Ok : NetrunUI.Muted);
        if (Current.Game != null)
            t = KV(inner, t, "NR_HelpNewcomerState".Translate(), open ? "NR_HelpNewcomerOpen".Translate() : "NR_HelpNewcomerClosed".Translate(), open ? NetrunUI.Warn : NetrunUI.Ok);
        t = KV(inner, t, "NR_HelpNewcomerBlocks".Translate(), "NR_HelpNewcomerBlocksList".Translate(), NetrunUI.Bad);
        return KV(inner, t, "NR_HelpNewcomerAlways".Translate(), "NR_HelpNewcomerAlwaysList".Translate(), NetrunUI.Ok);
    });

    // ---- [[lowerheat]] ----

    private float _demoLowerHeat = 60f;

    private float LowerHeatDemo(float y, float w) => Frame("lowerheat", y, w, NetrunUI.Ok, inner =>
    {
        float t = Slider(inner, inner.y, "NR_HelpLowerHeatNow".Translate(Mathf.RoundToInt(_demoLowerHeat)), ref _demoLowerHeat, 0f, HeatTracker.MaxHeat);
        float heat = Mathf.Round(_demoLowerHeat);
        // Waiting: day by day at the cooling rate (faster the hotter).
        float low = HeatTracker.BaseDecayAt(0f);
        float h = heat, days = 0f;
        while (h > 0.01f && days < 400f)
        {
            h -= HeatTracker.BaseDecayAt(h);
            days++;
        }
        // Scrub: unit by unit at the level's rate, a fixer with no discount.
        FixerSettingsDef fs = NetrunDefOf.NR_Fixers;
        float hs = heat;
        int units = 0;
        while (hs > 0.01f && units < 500)
        {
            float factor = fs.scrubFactorByLevel.NullOrEmpty() ? 1f : fs.scrubFactorByLevel[Mathf.Clamp((int)HeatLevelUtility.LevelOf(hs), 0, fs.scrubFactorByLevel.Count - 1)];
            hs -= fs.heatPerCyberdata * factor * NetrunMod.Settings.ScrubMultiplier
                / (HeatLevelUtility.LevelOf(hs) >= HeatLevel.RedZone ? fs.redZoneScrubPriceFactor : 1f);
            units++;
        }
        float ghost = NetrunDefOf.NR_AI.ghostHeatScrub;
        t = KV(inner, t + 4f, "NR_HelpLowerWait".Translate(), low <= 0f ? "∞" : (string)"NR_HelpLowerWaitValue".Translate(days.ToString("0")), NetrunUI.TextColor, 170f, NetrunTex.HeatIcon(HeatLevel.Shadow));
        t = KV(inner, t, "NR_HelpLowerScrub".Translate(), "NR_HelpLowerScrubValue".Translate(units), NetrunUI.Ok, 170f, NetrunTex.CallFixer);
        t = KV(inner, t, "NR_HelpLowerGhost".Translate(), "NR_HelpLowerGhostValue".Translate(ghost.ToString("0"), Mathf.CeilToInt(heat / Mathf.Max(1f, ghost)), NetrunDefOf.NR_AI.serviceCooldownDays.ToString("0")),
            NetrunUI.Purple, 170f, NetrunTex.Portrait(AIPersonality.Ghost));
        return KV(inner, t, "NR_HelpLowerContract".Translate(), "NR_HelpLowerContractValue".Translate(fs.layLowRewardPerHeat), NetrunUI.Warn, 170f, NetrunTex.TabIcon(2));
    });
}
