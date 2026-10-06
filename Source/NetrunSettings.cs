using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

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

    public void DoWindowContents(Rect inRect)
    {
        var view = new Rect(0f, 0f, inRect.width - 16f, _viewHeight);
        Widgets.BeginScrollView(inRect, ref _scroll, view);
        var list = new Listing_Standard();
        list.Begin(view);

        if (Group(list, "gameplay", "NR_SettingsGroupGameplay"))
            DrawGameplay(list);
        if (Group(list, "interface", "NR_SettingsGroupInterface"))
            DrawInterface(list);
        if (Group(list, "support", "NR_SettingsGroupSupport"))
            DrawSupport(list);

        list.GapLine();
        if (list.ButtonText("NR_SettingsReset".Translate()))
            ResetAll();

        _viewHeight = list.CurHeight + 20f;
        list.End();
        Widgets.EndScrollView();
    }

    /// <summary>A clickable group header (▼ open / ► closed); true when the group is open.</summary>
    private static bool Group(Listing_Standard list, string id, string labelKey)
    {
        list.Gap(6f);
        bool open = !Collapsed.Contains(id);
        Rect row = list.GetRect(30f);
        Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.06f));
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(row.x + 8f, row.y, row.width - 8f, row.height), (open ? "▼ " : "► ") + labelKey.Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        if (Widgets.ButtonInvisible(row))
        {
            if (open)
                Collapsed.Add(id);
            else
                Collapsed.Remove(id);
        }
        list.Gap(4f);
        return open;
    }

    private void DrawGameplay(Listing_Standard list)
    {
        list.Label("NR_SettingsDifficulty".Translate(), tooltip: "NR_SettingsDifficultyTip".Translate());
        foreach (HeatDifficulty d in new[] { HeatDifficulty.Easy, HeatDifficulty.Normal, HeatDifficulty.Hard })
        {
            var p = Presets[d];
            if (list.RadioButton(("NR_SettingsDifficulty_" + d).Translate(p.gain.ToStringPercent(), p.decay.ToString("0.#"), p.scrub.ToStringPercent()),
                    Difficulty == d))
                ApplyPreset(d);
        }
        if (Difficulty == HeatDifficulty.Custom)
            list.Label("NR_SettingsDifficulty_Custom".Translate().Colorize(ColoredText.SubtleGrayColor));

        if (list.ButtonText((_advancedOpen ? "▼ " : "► ") + "NR_SettingsAdvanced".Translate()))
            _advancedOpen = !_advancedOpen;
        if (_advancedOpen)
        {
            float gain = HeatGainMultiplier, decay = HeatDecayPerDay, scrub = ScrubMultiplier;
            list.Label("NR_SettingsHeatGain".Translate(HeatGainMultiplier.ToStringPercent()));
            HeatGainMultiplier = list.Slider(HeatGainMultiplier, 0.25f, 3f);
            list.Label("NR_SettingsHeatDecay".Translate(HeatDecayPerDay.ToString("0.0")));
            HeatDecayPerDay = list.Slider(HeatDecayPerDay, 0f, 20f);
            list.Label("NR_SettingsScrub".Translate(ScrubMultiplier.ToStringPercent()));
            ScrubMultiplier = list.Slider(ScrubMultiplier, 0.5f, 2f);
            if (!Mathf.Approximately(gain, HeatGainMultiplier) || !Mathf.Approximately(decay, HeatDecayPerDay) || !Mathf.Approximately(scrub, ScrubMultiplier))
                Difficulty = HeatDifficulty.Custom;
        }
        list.Gap();

        Frequency(list, "NR_SettingsFreqAttacks", ref NetAttackFrequency);
        Frequency(list, "NR_SettingsFreqMap", ref MapEventFrequency);
        Frequency(list, "NR_SettingsFreqCyber", ref CyberEventFrequency);
        Frequency(list, "NR_SettingsAIEventFrequency", ref AIEventFrequency);

        list.CheckboxLabeled("NR_SettingsNewcomer".Translate(), ref NewcomerProtection, "NR_SettingsNewcomerTip".Translate());
    }

    /// <summary>Label and a row of four radio buttons: off, 0.5x, 1x, 2x.</summary>
    private static void Frequency(Listing_Standard list, string labelKey, ref float value)
    {
        list.Label(labelKey.Translate(), tooltip: (labelKey + "Tip").Translate());
        Rect row = list.GetRect(26f);
        float w = row.width / Frequencies.Length;
        for (int i = 0; i < Frequencies.Length; i++)
        {
            float f = Frequencies[i];
            string label = f <= 0f ? "NR_SettingsOff".Translate() : "NR_SettingsAIEventFrequencyValue".Translate(f.ToString("0.#"));
            if (Widgets.RadioButtonLabeled(new Rect(row.x + i * w, row.y, w - 12f, row.height), label, Mathf.Approximately(value, f)))
                value = f;
        }
        list.Gap(6f);
    }

    private void DrawInterface(Listing_Standard list)
    {
        list.Label("NR_SettingsAccent".Translate());
        for (int i = 0; i < NetrunTex.Accents.Length; i++)
        {
            if (list.RadioButton(("NR_SettingsAccent_" + i).Translate().Colorize(NetrunTex.Accents[i]), TerminalAccent == i))
                TerminalAccent = i;
        }
        list.Gap();
        list.CheckboxLabeled("NR_SettingsDiveReport".Translate(), ref ShowDiveReport, "NR_SettingsDiveReportTip".Translate());
        list.CheckboxLabeled("NR_SettingsPause".Translate(), ref PauseOnOpen, "NR_SettingsPauseTip".Translate());
        list.CheckboxLabeled("NR_SettingsAnimations".Translate(), ref Animations, "NR_SettingsAnimationsTip".Translate());
    }

    private void DrawSupport(Listing_Standard list)
    {
        list.Label("NR_SettingsReportInfoDesc".Translate().Colorize(ColoredText.SubtleGrayColor));
        if (list.ButtonText("NR_SettingsReportInfo".Translate()))
        {
            GUIUtility.systemCopyBuffer = ReportInfo();
            Messages.Message("NR_SettingsReportCopied".Translate(), MessageTypeDefOf.NeutralEvent, historical: false);
        }
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
