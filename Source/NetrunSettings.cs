using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Netrun;

public enum HeatDifficulty
{
    Easy,
    Normal,
    Hard,
    Custom,
}

/// <summary>
/// Mod settings (docs/CORE_V3.md, "first" list): collapsible groups for gameplay, interface and support.
/// </summary>
public class NetrunSettings : ModSettings
{
    private const float DefaultHeatGainMultiplier = 1f;
    private const float DefaultHeatDecayPerDay = 4f;

    // ---- Gameplay ----
    public HeatDifficulty Difficulty = HeatDifficulty.Normal;
    public float HeatGainMultiplier = DefaultHeatGainMultiplier;
    public float HeatDecayPerDay = DefaultHeatDecayPerDay;
    /// <summary>Scales the Heat a fixer removes per cyberdata: above 1 the scrub is cheaper.</summary>
    public float ScrubMultiplier = 1f;

    /// <summary>Event frequency by group: 0 = off, 0.5x, 1x, 2x.</summary>
    public float AIEventFrequency = 1f;
    public float NetAttackFrequency = 1f;
    public float MapEventFrequency = 1f;
    public float CyberEventFrequency = 1f;

    /// <summary>Netrun threats (Heat, AI, fixers, mercenaries) wait until the colony has touched the net.</summary>
    public bool NewcomerProtection = true;

    // ---- Interface ----
    /// <summary>Index into NetrunTex.Accents: the terminal's highlight color.</summary>
    public int TerminalAccent;
    /// <summary>Open the dive summary after leaving a dive.</summary>
    public bool ShowDiveReport = true;
    public bool PauseOnOpen = true;
    /// <summary>Moving and glowing bits of the terminal (floating Gateway nodes, node glow).</summary>
    public bool Animations = true;
    /// <summary>Use the Tektur font in the mod's windows (when its bundle is present).</summary>
    public bool TerminalFont = true;

    private static readonly float[] Frequencies = [0f, 0.5f, 1f, 2f];

    /// <summary>Presets: Heat gain, cooling per day, scrub efficiency.</summary>
    private static readonly Dictionary<HeatDifficulty, (float gain, float decay, float scrub)> Presets = new()
    {
        [HeatDifficulty.Easy] = (0.75f, 6f, 1.5f),
        [HeatDifficulty.Normal] = (1f, 4f, 1f),
        [HeatDifficulty.Hard] = (1.3f, 3f, 0.75f),
    };

    // Collapsed groups and the scroll position live only for the session.
    private static readonly HashSet<string> Collapsed = [];
    private static bool _advancedOpen;
    private static Vector2 _scroll;
    private static float _viewHeight = 900f;

    /// <summary>A setting used as a divisor (0 = off): a tiny positive number instead of zero.</summary>
    public static float Divisor(float frequency) => frequency > 0f ? frequency : 0.0001f;

    public void ApplyPreset(HeatDifficulty difficulty)
    {
        Difficulty = difficulty;
        if (!Presets.TryGetValue(difficulty, out var p))
            return;
        HeatGainMultiplier = p.gain;
        HeatDecayPerDay = p.decay;
        ScrubMultiplier = p.scrub;
    }

    // ---- Window: drawn by hand in the terminal style (no Listing), with its own scroll ----

    private float _y;
    private float _w;

    public void DoWindowContents(Rect inRect)
    {
        bool font = NetrunFont.Push();
        try
        {
            DrawWindow(inRect);
        }
        finally
        {
            NetrunFont.Pop(font);
        }
    }

    private void DrawWindow(Rect inRect)
    {
        Widgets.DrawBoxSolid(inRect, NetrunUI.BgWindow);
        Rect outer = inRect.ContractedBy(10f);
        bool scrolls = _viewHeight > outer.height;
        var view = new Rect(0f, 0f, outer.width - (scrolls ? 16f : 0f), Mathf.Max(_viewHeight, outer.height));
        Widgets.BeginScrollView(outer, ref _scroll, view);
        _y = 0f;
        _w = view.width;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, _y, _w, 32f), "NR_SettingsTitle".Translate().Colorize(NetrunUI.Accent));
        Text.Font = GameFont.Small;
        _y += 38f;

        if (Group("gameplay", "NR_SettingsGroupGameplay"))
            DrawGameplay();
        if (Group("interface", "NR_SettingsGroupInterface"))
            DrawInterface();
        if (Group("support", "NR_SettingsGroupSupport"))
            DrawSupport();

        _y += 10f;
        if (NetrunUI.Button(new Rect(0f, _y, 220f, 30f), "NR_SettingsReset".Translate(), NetrunUI.Bad, true))
            ResetAll();
        _y += 40f;

        _viewHeight = _y;
        Widgets.EndScrollView();
    }

    /// <summary>A clickable group bar (▼ open / ► closed); true when open.</summary>
    private bool Group(string id, string labelKey)
    {
        bool open = !Collapsed.Contains(id);
        var row = new Rect(0f, _y, _w, 32f);
        NetrunUI.Box(row, Mouse.IsOver(row) ? NetrunUI.BgCardAlt : NetrunUI.BgCard, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.5f));
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(row.x + 10f, row.y, row.width - 12f, row.height), ((open ? "▼  " : "►  ") + ((string)labelKey.Translate()).ToUpper()).Colorize(NetrunUI.Accent));
        Text.Anchor = TextAnchor.UpperLeft;
        if (Widgets.ButtonInvisible(row))
        {
            if (open)
                Collapsed.Add(id);
            else
                Collapsed.Remove(id);
            SoundDefOf.Click.PlayOneShotOnCamera();
        }
        _y += 38f;
        return open;
    }

    private void Label(string text, Color? color = null, string tip = null)
    {
        float h = Text.CalcHeight(text, _w - 12f);
        var r = new Rect(12f, _y, _w - 12f, h);
        Widgets.Label(r, color.HasValue ? text.Colorize(color.Value) : text);
        if (tip != null)
            NetrunUI.Tip(r, tip);
        _y += h + 4f;
    }

    /// <summary>Options as a row of chips; returns the picked index.</summary>
    private int Chips(string[] labels, int selected, string[] tips = null)
    {
        float x = 12f;
        int picked = selected;
        for (int i = 0; i < labels.Length; i++)
        {
            float w = NetrunUI.ButtonWidth(labels[i]);
            if (x + w > _w)
            {
                x = 12f;
                _y += 34f;
            }
            bool on = i == selected;
            var r = new Rect(x, _y, w, 28f);
            if (on)
                Widgets.DrawBoxSolid(r, new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.18f));
            if (NetrunUI.Button(r, (on ? "● " : string.Empty) + labels[i], on ? NetrunUI.Ok : NetrunUI.Muted, true, tips?[i]))
                picked = i;
            x += w + 6f;
        }
        _y += 36f;
        return picked;
    }

    private void Toggle(string labelKey, ref bool value, string tipKey)
    {
        var r = new Rect(12f, _y, _w - 12f, 28f);
        if (Mouse.IsOver(r))
            Widgets.DrawBoxSolid(r, new Color(1f, 1f, 1f, 0.04f));
        var box = new Rect(r.x, r.y + 5f, 18f, 18f);
        NetrunUI.Box(box, NetrunUI.BgCard, value ? NetrunUI.Ok : NetrunUI.Muted);
        if (value)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(box, "✓".Colorize(NetrunUI.Ok));
        }
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(r.x + 28f, r.y, r.width - 28f, r.height), labelKey.Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        NetrunUI.Tip(r, tipKey.Translate());
        if (Widgets.ButtonInvisible(r))
        {
            value = !value;
            SoundDefOf.Click.PlayOneShotOnCamera();
        }
        _y += 32f;
    }

    private float Slider(string label, float value, float min, float max)
    {
        var frame = new Rect(12f, _y, _w - 12f, 50f);
        NetrunUI.Box(frame, NetrunUI.BgCard, new Color(1f, 1f, 1f, 0.1f));
        Widgets.Label(new Rect(frame.x + 8f, frame.y + 4f, frame.width - 16f, 22f), label);
        float v = Widgets.HorizontalSlider(new Rect(frame.x + 8f, frame.y + 28f, frame.width - 16f, 16f), value, min, max, roundTo: 0.05f);
        _y += 56f;
        return v;
    }

    private void DrawGameplay()
    {
        Label("NR_SettingsDifficulty".Translate(), NetrunUI.Accent, "NR_SettingsDifficultyTip".Translate());
        HeatDifficulty[] presets = [HeatDifficulty.Easy, HeatDifficulty.Normal, HeatDifficulty.Hard];
        string[] names = presets.Select(d => (string)("NR_SettingsDifficultyName_" + d).Translate()).ToArray();
        string[] tips = presets.Select(d =>
        {
            var p = Presets[d];
            return (string)("NR_SettingsDifficulty_" + d).Translate(p.gain.ToStringPercent(), p.decay.ToString("0.#"), p.scrub.ToStringPercent());
        }).ToArray();
        int current = System.Array.IndexOf(presets, Difficulty);
        int picked = Chips(names, current, tips);
        if (picked != current && picked >= 0)
            ApplyPreset(presets[picked]);
        if (Difficulty == HeatDifficulty.Custom)
            Label("NR_SettingsDifficulty_Custom".Translate(), NetrunUI.Warn);
        else if (current >= 0)
            Label(tips[current], NetrunUI.Muted);

        var adv = new Rect(12f, _y, 240f, 26f);
        if (NetrunUI.Button(adv, (_advancedOpen ? "▼ " : "► ") + "NR_SettingsAdvanced".Translate(), NetrunUI.Muted, true))
            _advancedOpen = !_advancedOpen;
        _y += 32f;
        if (_advancedOpen)
        {
            float gain = HeatGainMultiplier, decay = HeatDecayPerDay, scrub = ScrubMultiplier;
            HeatGainMultiplier = Slider("NR_SettingsHeatGain".Translate(HeatGainMultiplier.ToStringPercent()), HeatGainMultiplier, 0.25f, 3f);
            HeatDecayPerDay = Slider("NR_SettingsHeatDecay".Translate(HeatDecayPerDay.ToString("0.0")), HeatDecayPerDay, 0f, 20f);
            ScrubMultiplier = Slider("NR_SettingsScrub".Translate(ScrubMultiplier.ToStringPercent()), ScrubMultiplier, 0.5f, 2f);
            if (!Mathf.Approximately(gain, HeatGainMultiplier) || !Mathf.Approximately(decay, HeatDecayPerDay) || !Mathf.Approximately(scrub, ScrubMultiplier))
                Difficulty = HeatDifficulty.Custom;
        }
        _y += 6f;

        Frequency("NR_SettingsFreqAttacks", ref NetAttackFrequency);
        Frequency("NR_SettingsFreqMap", ref MapEventFrequency);
        Frequency("NR_SettingsFreqCyber", ref CyberEventFrequency);
        Frequency("NR_SettingsAIEventFrequency", ref AIEventFrequency);

        Toggle("NR_SettingsNewcomer", ref NewcomerProtection, "NR_SettingsNewcomerTip");
        _y += 6f;
    }

    /// <summary>Label and four chips: off, 0.5x, 1x, 2x.</summary>
    private void Frequency(string labelKey, ref float value)
    {
        Label(labelKey.Translate(), null, (labelKey + "Tip").Translate());
        string[] labels = Frequencies.Select(f => f <= 0f ? (string)"NR_SettingsOff".Translate() : (string)"NR_SettingsAIEventFrequencyValue".Translate(f.ToString("0.#"))).ToArray();
        float v = value;
        int current = System.Array.FindIndex(Frequencies, f => Mathf.Approximately(f, v));
        int picked = Chips(labels, current);
        if (picked >= 0)
            value = Frequencies[picked];
    }

    private void DrawInterface()
    {
        Label("NR_SettingsAccent".Translate(), NetrunUI.Accent);
        float x = 12f;
        for (int i = 0; i < NetrunTex.Accents.Length; i++)
        {
            string name = ("NR_SettingsAccent_" + i).Translate();
            float w = NetrunUI.ButtonWidth(name);
            if (x + w > _w)
            {
                x = 12f;
                _y += 34f;
            }
            var r = new Rect(x, _y, w, 28f);
            if (TerminalAccent == i)
                Widgets.DrawBoxSolid(r, new Color(NetrunTex.Accents[i].r, NetrunTex.Accents[i].g, NetrunTex.Accents[i].b, 0.2f));
            if (NetrunUI.Button(r, (TerminalAccent == i ? "● " : string.Empty) + name, NetrunTex.Accents[i], true))
                TerminalAccent = i;
            x += w + 6f;
        }
        _y += 40f;
        Toggle("NR_SettingsDiveReport", ref ShowDiveReport, "NR_SettingsDiveReportTip");
        Toggle("NR_SettingsPause", ref PauseOnOpen, "NR_SettingsPauseTip");
        Toggle("NR_SettingsAnimations", ref Animations, "NR_SettingsAnimationsTip");
        if (NetrunFont.Available)
            Toggle("NR_SettingsFont", ref TerminalFont, "NR_SettingsFontTip");
        _y += 6f;
    }

    private void DrawSupport()
    {
        if (Current.Game != null)
        {
            if (NetrunUI.Button(new Rect(12f, _y, 300f, 30f), "NR_SettingsUnlockHelp".Translate(), NetrunUI.Ok, true, "NR_SettingsUnlockHelpTip".Translate()))
            {
                HelpProgress.Get()?.UnlockAll();
                Messages.Message("NR_SettingsUnlockHelpDone".Translate(), MessageTypeDefOf.NeutralEvent, historical: false);
            }
            _y += 38f;
        }
        Label("NR_SettingsReportInfoDesc".Translate(), NetrunUI.Muted);
        if (NetrunUI.Button(new Rect(12f, _y, 300f, 30f), "NR_SettingsReportInfo".Translate(), NetrunUI.Accent, true))
        {
            GUIUtility.systemCopyBuffer = ReportInfo();
            Messages.Message("NR_SettingsReportCopied".Translate(), MessageTypeDefOf.NeutralEvent, historical: false);
        }
        _y += 40f;
    }

    /// <summary>Versions, mod list and the settings that matter for a bug report.</summary>
    private string ReportInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Hacking Expansion: Netrun bug report info");
        sb.AppendLine("RimWorld: " + VersionControl.CurrentVersionStringWithRev);
        sb.AppendLine("Netrun: " + Version("GafarovMaxim.HackingExpansionNetrun"));
        sb.AppendLine("Hacking Expansion: " + Version("Ushanka.HackingExpansion"));
        sb.AppendLine("Harmony: " + Version("brrainz.harmony"));
        sb.AppendLine($"Settings: difficulty {Difficulty}, gain {HeatGainMultiplier:0.##}, decay {HeatDecayPerDay:0.#}, scrub {ScrubMultiplier:0.##}, "
            + $"attacks {NetAttackFrequency}, map {MapEventFrequency}, cyber {CyberEventFrequency}, AI {AIEventFrequency}, newcomer {NewcomerProtection}");
        if (Current.Game != null)
            sb.AppendLine($"Game: Heat {HeatTracker.Get()?.Heat ?? 0f:0}, network open {NetworkGate.Get()?.Opened}, AIs {AIManager.Get()?.AIs.Count ?? 0}");
        sb.AppendLine("Active mods (load order):");
        foreach (ModMetaData mod in ModsConfig.ActiveModsInLoadOrder)
            sb.AppendLine($"  {mod.Name} [{mod.PackageIdPlayerFacing}] {mod.ModVersion}");
        return sb.ToString();
    }

    private static string Version(string packageId)
    {
        ModMetaData mod = ModsConfig.ActiveModsInLoadOrder.FirstOrDefault(m => m.SamePackageId(packageId, ignorePostfix: true));
        return mod == null ? "not active" : mod.ModVersion.NullOrEmpty() ? "active" : mod.ModVersion;
    }

    private void ResetAll()
    {
        ApplyPreset(HeatDifficulty.Normal);
        AIEventFrequency = NetAttackFrequency = MapEventFrequency = CyberEventFrequency = 1f;
        NewcomerProtection = true;
        TerminalAccent = 0;
        ShowDiveReport = true;
        PauseOnOpen = true;
        Animations = true;
        TerminalFont = true;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref Difficulty, "difficulty", HeatDifficulty.Normal);
        Scribe_Values.Look(ref HeatGainMultiplier, "heatGainMultiplier", DefaultHeatGainMultiplier);
        Scribe_Values.Look(ref HeatDecayPerDay, "heatDecayPerDay", DefaultHeatDecayPerDay);
        Scribe_Values.Look(ref ScrubMultiplier, "scrubMultiplier", 1f);
        Scribe_Values.Look(ref TerminalAccent, "terminalAccent");
        Scribe_Values.Look(ref AIEventFrequency, "aiEventFrequency", 1f);
        Scribe_Values.Look(ref NetAttackFrequency, "netAttackFrequency", 1f);
        Scribe_Values.Look(ref MapEventFrequency, "mapEventFrequency", 1f);
        Scribe_Values.Look(ref CyberEventFrequency, "cyberEventFrequency", 1f);
        Scribe_Values.Look(ref ShowDiveReport, "showDiveReport", true);
        Scribe_Values.Look(ref NewcomerProtection, "newcomerProtection", true);
        Scribe_Values.Look(ref PauseOnOpen, "pauseOnOpen", true);
        Scribe_Values.Look(ref Animations, "animations", true);
        Scribe_Values.Look(ref TerminalFont, "terminalFont", true);
        if (AIEventFrequency < 0f)
            AIEventFrequency = 1f;
        if (ScrubMultiplier <= 0f)
            ScrubMultiplier = 1f;
        // Settings from before the presets: hand-set sliders count as "Custom".
        if (Scribe.mode == LoadSaveMode.LoadingVars && Presets.TryGetValue(Difficulty, out var p)
            && (!Mathf.Approximately(p.gain, HeatGainMultiplier) || !Mathf.Approximately(p.decay, HeatDecayPerDay) || !Mathf.Approximately(p.scrub, ScrubMultiplier)))
            Difficulty = HeatDifficulty.Custom;
    }
}
