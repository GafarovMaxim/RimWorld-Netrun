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
    Cyberpunk,
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

    /// <summary>Scales every AI fragment source: 0.5 low, 1 normal, 1.5 high (docs/CONCEPTS_10.md, section 2).</summary>
    public float AIAvailability = 1f;
    private static readonly float[] Availabilities = [0.5f, 1f, 1.5f];

    /// <summary>Cyberspace map size for new maps (docs/CYBERSPACE_V2.md, section 9); asked again at the first entry.</summary>
    public NetSize MapSize = NetSize.Large;

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
    /// <summary>Text size in the mod's windows, 85-125%.</summary>
    public float TextScale = 1f;
    /// <summary>Icon size in the mod's lists and articles, 80-140%.</summary>
    public float IconScale = 1f;
    /// <summary>Use the Tektur font in the mod's windows (when its bundle is present).</summary>
    public bool TerminalFont = true;
    /// <summary>Short screen glitches (button presses, the Red Zone flicker); off keeps the terminal steady.</summary>
    public bool Glitches = true;
    /// <summary>CRT lines over the terminal's screen.</summary>
    public bool Scanlines = true;
    /// <summary>The minimap in the corner of the cyberspace map.</summary>
    public bool MapMinimap = true;
    /// <summary>Pause the game once when a patrol or an intrusion appears on a runner's route.</summary>
    public bool MapAutopause = true;
    /// <summary>The intro tour on the first terminal and one card the first time ICE, Netsick, a fixer or an AI event shows up.</summary>
    public bool FirstHints = true;

    /// <summary>The Newcomer preset is gentler for the first ten days: less trace, weaker ICE.</summary>
    public float NewcomerEase => Difficulty == HeatDifficulty.Easy && Current.Game != null && GenDate.DaysPassed < 10 ? 0.75f : 1f;
    /// <summary>The cyberspace map's lens (MapLens), remembered between games.</summary>
    public int MapLens;

    // ---- Frame (Customization) ----
    /// <summary>The casing theme (DeckThemes), its parts that can be hidden, and the themes opened in any game.</summary>
    public string FrameTheme = "Field";
    public bool FrameLights = true;
    public bool FrameGlass = true;
    public bool FrameLcd = true;
    public List<string> UnlockedThemes = [];

    // ---- Difficulty: finer knobs on top of the Heat preset ----
    /// <summary>ICE and patrols: their strength in dives (1 = as designed).</summary>
    public float IceStrength = 1f;
    /// <summary>How fast awakened AIs get hungry.</summary>
    public float AIHunger = 1f;
    /// <summary>The share of the haul lost on a retreat.</summary>
    public float RetreatLoss = 1f;

    // ---- Sound ----
    /// <summary>The sound pack (NetrunSound.Packs) or "Off".</summary>
    public string SoundPack = "Clean";
    public float UiVolume = 0.6f;
    public float AmbientVolume = 0.6f;
    /// <summary>Each ambient loop on its own, on top of the ambient volume (1 = as the pack made it).</summary>
    public float AmbientTerminal = 1f;
    public float AmbientCyberspace = 1f;
    public float AmbientDive = 1f;
    public float AmbientServer = 1f;

    /// <summary>The volume of one ambient loop ("Ambient_Terminal" and so on).</summary>
    public float AmbientVolumeOf(string id) => AmbientVolume * id switch
    {
        "Ambient_Terminal" => AmbientTerminal,
        "Ambient_Cyberspace" => AmbientCyberspace,
        "Ambient_Dive" => AmbientDive,
        "Ambient_Server" => AmbientServer,
        _ => 1f,
    };

    private static readonly float[] Frequencies = [0f, 0.5f, 1f, 2f];

    /// <summary>Presets: Heat gain, cooling per day, scrub efficiency, ICE strength, AI hunger, retreat loss.</summary>
    private static readonly Dictionary<HeatDifficulty, (float gain, float decay, float scrub, float ice, float hunger, float retreat)> Presets = new()
    {
        [HeatDifficulty.Easy] = (0.75f, 6f, 1.5f, 0.8f, 0.8f, 0.5f),
        [HeatDifficulty.Normal] = (1f, 4f, 1f, 1f, 1f, 1f),
        [HeatDifficulty.Hard] = (1.3f, 3f, 0.75f, 1.2f, 1.2f, 1.25f),
        [HeatDifficulty.Cyberpunk] = (1.6f, 2f, 0.6f, 1.4f, 1.4f, 1.5f),
    };
    /// <summary>The order of the preset chips (Easy is shown as "Newcomer").</summary>
    private static readonly HeatDifficulty[] PresetOrder = [HeatDifficulty.Easy, HeatDifficulty.Normal, HeatDifficulty.Hard, HeatDifficulty.Cyberpunk];

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
        IceStrength = p.ice;
        AIHunger = p.hunger;
        RetreatLoss = p.retreat;
        // The newcomer preset brings the newcomer protection with it.
        if (difficulty == HeatDifficulty.Easy)
            NewcomerProtection = true;
    }

    // ---- Pages drawn inside the terminal's menu (Settings, Difficulty, Customization tabs) ----

    private static readonly Dictionary<string, Vector2> PageScroll = [];
    private static readonly Dictionary<string, float> PageHeight = [];

    /// <summary>
    /// One page of settings with its own scroll (saved when the terminal closes): "settings" (events, interface, support), "difficulty", "frame",
    /// "colors", "sound", "graphics". The terminal's menu calls it; the mod window shows the same parts.
    /// </summary>
    public void DrawPage(Rect outer, string page)
    {
        float height = PageHeight.TryGetValue(page, out float h) ? h : 600f;
        bool scrolls = height > outer.height;
        var view = new Rect(0f, 0f, outer.width - (scrolls ? 16f : 0f), Mathf.Max(height, outer.height));
        Vector2 scroll = PageScroll.TryGetValue(page, out Vector2 s) ? s : Vector2.zero;
        NetrunUI.BeginScroll(outer, ref scroll, view);
        _y = 0f;
        _w = view.width;
        switch (page)
        {
            case "settings":
                DrawEvents();
                Header("NR_SettingsGroupInterface");
                DrawInterface();
                Header("NR_SettingsGroupSupport");
                DrawSupport();
                break;
            case "difficulty":
                DrawDifficulty(true);
                break;
            case "frame":
                DrawFrame();
                break;
            case "sound":
                DrawSound();
                break;
            case "graphics":
                DrawColors();
                DrawGraphics();
                break;
        }
        PageHeight[page] = _y + 10f;
        PageScroll[page] = scroll;
        Widgets.EndScrollView();
    }

    private void Header(string labelKey)
    {
        _y += 6f;
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(12f, _y, _w - 12f, 24f), ((string)labelKey.Translate()).ToUpper().Colorize(NetrunUI.Accent));
        GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.35f);
        Widgets.DrawLineHorizontal(12f, _y + 24f, _w - 12f);
        GUI.color = Color.white;
        _y += 32f;
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
        NetrunUI.BeginScroll(outer, ref _scroll, view);
        _y = 0f;
        _w = view.width;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, _y, _w, 32f), "NR_SettingsTitle".Translate().Colorize(NetrunUI.Accent));
        Text.Font = GameFont.Small;
        _y += 38f;

        if (Group("gameplay", "NR_SettingsGroupGameplay", ResetGameplay))
        {
            DrawDifficulty(false);
            DrawEvents();
        }
        if (Group("interface", "NR_SettingsGroupInterface", ResetInterface))
            DrawInterface();
        // In a game the look and the sound are in the terminal (MENU, Customization); from the main menu they are here.
        if (Current.Game != null)
        {
            Label("NR_SettingsInTerminal".Translate(), NetrunUI.Muted);
            _y += 4f;
        }
        else
        {
            if (Group("customization", "NR_MenuCustomization", ResetCustomization))
            {
                DrawFrame();
                DrawColors();
                DrawGraphics();
            }
            if (Group("sound", "NR_SettingsGroupSound", ResetSound))
                DrawSound();
        }
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
    private bool Group(string id, string labelKey, System.Action reset = null)
    {
        bool open = !Collapsed.Contains(id);
        var row = new Rect(0f, _y, _w, 32f);
        // The group's own reset sits on the right of its bar (and does not fold the group).
        if (reset != null)
        {
            string label = "NR_SettingsResetGroup".Translate();
            float rw = NetrunUI.ButtonWidth(label);
            var rr = new Rect(row.xMax - rw - 6f, row.y + 4f, rw, 24f);
            if (NetrunUI.Button(rr, label, NetrunUI.Bad, true, "NR_SettingsResetGroupTip".Translate(labelKey.Translate())))
                reset();
            row.width -= rw + 12f;
        }
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
            NetrunSound.Click();
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
            NetrunSound.Click();
        }
        _y += 32f;
    }

    private float Slider(string label, float value, float min, float max)
    {
        var frame = new Rect(12f, _y, _w - 12f, 50f);
        NetrunUI.Box(frame, NetrunUI.BgCard, new Color(1f, 1f, 1f, 0.1f));
        Widgets.Label(new Rect(frame.x + 8f, frame.y + 4f, frame.width - 16f, 22f), label);
        float v = NetrunUI.Slider(new Rect(frame.x + 8f, frame.y + 28f, frame.width - 16f, 16f), value, min, max, 0.05f);
        _y += 56f;
        return v;
    }

    /// <summary>Presets (Newcomer, Normal, Hard, Cyberpunk) and the knobs; any knob moved makes it "Custom".</summary>
    private void DrawDifficulty(bool open)
    {
        Label("NR_SettingsDifficulty".Translate(), NetrunUI.Accent, "NR_SettingsDifficultyTip".Translate());
        string[] names = PresetOrder.Select(d => (string)("NR_SettingsDifficultyName_" + d).Translate()).ToArray();
        string[] tips = PresetOrder.Select(d =>
        {
            var p = Presets[d];
            return (string)("NR_SettingsDifficulty_" + d).Translate(p.gain.ToStringPercent(), p.decay.ToString("0.#"), p.scrub.ToStringPercent())
                   + "\n" + (string)"NR_SettingsDifficultyMore".Translate(p.ice.ToStringPercent(), p.hunger.ToStringPercent(), p.retreat.ToStringPercent());
        }).ToArray();
        int current = System.Array.IndexOf(PresetOrder, Difficulty);
        int picked = Chips(names, current, tips);
        if (picked != current && picked >= 0)
            ApplyPreset(PresetOrder[picked]);
        if (Difficulty == HeatDifficulty.Custom)
            Label("NR_SettingsDifficulty_Custom".Translate(), NetrunUI.Warn);
        else if (current >= 0)
            Label(tips[current], NetrunUI.Muted);

        if (!open)
        {
            var adv = new Rect(12f, _y, 240f, 26f);
            if (NetrunUI.Button(adv, (_advancedOpen ? "▼ " : "► ") + "NR_SettingsAdvanced".Translate(), NetrunUI.Muted, true))
                _advancedOpen = !_advancedOpen;
            _y += 32f;
        }
        if (open || _advancedOpen)
        {
            float[] before = [HeatGainMultiplier, HeatDecayPerDay, ScrubMultiplier, IceStrength, AIHunger, RetreatLoss];
            HeatGainMultiplier = Slider("NR_SettingsHeatGain".Translate(HeatGainMultiplier.ToStringPercent()), HeatGainMultiplier, 0.25f, 3f);
            HeatDecayPerDay = Slider("NR_SettingsHeatDecay".Translate(HeatDecayPerDay.ToString("0.0")), HeatDecayPerDay, 0f, 20f);
            IceStrength = Slider("NR_SettingsIceStrength".Translate(IceStrength.ToStringPercent()), IceStrength, 0.5f, 2f);
            AIHunger = Slider("NR_SettingsAIHunger".Translate(AIHunger.ToStringPercent()), AIHunger, 0.25f, 2f);
            ScrubMultiplier = Slider("NR_SettingsScrub".Translate(ScrubMultiplier.ToStringPercent()), ScrubMultiplier, 0.5f, 2f);
            RetreatLoss = Slider("NR_SettingsRetreatLoss".Translate(RetreatLoss.ToStringPercent()), RetreatLoss, 0f, 2f);
            float[] after = [HeatGainMultiplier, HeatDecayPerDay, ScrubMultiplier, IceStrength, AIHunger, RetreatLoss];
            if (before.Where((v, i) => !Mathf.Approximately(v, after[i])).Any())
                Difficulty = HeatDifficulty.Custom;
        }
        _y += 6f;
    }

    /// <summary>Event frequencies, AI availability, map size, newcomer protection.</summary>
    private void DrawEvents()
    {

        Frequency("NR_SettingsFreqAttacks", ref NetAttackFrequency);
        Frequency("NR_SettingsFreqMap", ref MapEventFrequency);
        Frequency("NR_SettingsFreqCyber", ref CyberEventFrequency);
        Frequency("NR_SettingsAIEventFrequency", ref AIEventFrequency);

        Label("NR_SettingsAIAvailability".Translate(), null, "NR_SettingsAIAvailabilityTip".Translate());
        string[] availLabels = ["NR_SettingsAIAvailabilityLow".Translate(), "NR_SettingsAIAvailabilityNormal".Translate(), "NR_SettingsAIAvailabilityHigh".Translate()];
        float avail = AIAvailability;
        int availPicked = Chips(availLabels, System.Array.FindIndex(Availabilities, f => Mathf.Approximately(f, avail)));
        if (availPicked >= 0)
            AIAvailability = Availabilities[availPicked];

        Label("NR_SettingsMapSize".Translate(), null, "NR_SettingsMapSizeTip".Translate());
        string[] sizeLabels = ["NR_NetSize_Small".Translate(), "NR_NetSize_Normal".Translate(), "NR_NetSize_Large".Translate()];
        int sizePicked = Chips(sizeLabels, (int)MapSize);
        if (sizePicked >= 0)
            MapSize = (NetSize)sizePicked;

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
        Toggle("NR_SettingsDiveReport", ref ShowDiveReport, "NR_SettingsDiveReportTip");
        Toggle("NR_SettingsPause", ref PauseOnOpen, "NR_SettingsPauseTip");
        Toggle("NR_SettingsMinimap", ref MapMinimap, "NR_SettingsMinimapTip");
        Toggle("NR_SettingAutopause", ref MapAutopause, "NR_SettingAutopauseTip");
        Toggle("NR_SettingsFirstHints", ref FirstHints, "NR_SettingsFirstHintsTip");
        _y += 6f;
    }

    /// <summary>The terminal's accent colour: swatches.</summary>
    private void DrawColors()
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
    }

    /// <summary>Animations, glitches, CRT lines, text and icon size, the font.</summary>
    private void DrawGraphics()
    {
        Toggle("NR_SettingsAnimations", ref Animations, "NR_SettingsAnimationsTip");
        Toggle("NR_SettingsGlitches", ref Glitches, "NR_SettingsGlitchesTip");
        Toggle("NR_SettingsScanlines", ref Scanlines, "NR_SettingsScanlinesTip");
        TextScale = Slider("NR_SettingsTextScale".Translate(TextScale.ToStringPercent()), TextScale, 0.85f, 1.25f);
        IconScale = Slider("NR_SettingsIconScale".Translate(IconScale.ToStringPercent()), IconScale, 0.8f, 1.4f);
        if (NetrunFont.Available)
            Toggle("NR_SettingsFont", ref TerminalFont, "NR_SettingsFontTip");
        _y += 6f;
    }

    /// <summary>Frame themes as cards (locked ones say which achievement opens them) and the parts that can be hidden.</summary>
    private void DrawFrame()
    {
        Label("NR_CustomFrameThemes".Translate(), NetrunUI.Accent);
        const float gap = 10f;
        int perRow = _w > 760f ? 4 : 2;
        float cw = Mathf.Min(200f, (_w - 12f - gap * (perRow - 1)) / perRow);
        float ch = 150f;
        for (int i = 0; i < DeckThemes.All.Length; i++)
        {
            DeckTheme theme = DeckThemes.All[i];
            var outerCard = new Rect(12f + i % perRow * (cw + gap), _y + i / perRow * (ch + gap), cw, ch);
            // Everything of a card stays inside it (long lock texts and names are cut, never spill over).
            Widgets.BeginGroup(outerCard);
            var card = new Rect(0f, 0f, cw, ch);
            bool open = DeckThemes.Unlocked(theme);
            bool chosen = DeckThemes.Current == theme;
            Color edge = chosen ? NetrunUI.Warn : open ? NetrunUI.Accent : NetrunUI.Muted;
            NetrunUI.Box(card, Mouse.IsOver(card) && open ? NetrunUI.BgCardAlt : NetrunUI.BgCard, new Color(edge.r, edge.g, edge.b, chosen ? 1f : 0.5f), chosen ? 2 : 1);
            // A small casing in the theme's colours around a dark screen.
            var preview = new Rect(card.x + 12f, card.y + 12f, card.width - 24f, 76f);
            for (int b = 0; b < 8; b++)
                Widgets.DrawBoxSolid(new Rect(preview.x, preview.y + preview.height * b / 8f, preview.width, preview.height / 8f + 1f), Color.Lerp(theme.Top, theme.Bottom, b / 7f));
            Widgets.DrawBoxSolid(new Rect(preview.x + 10f, preview.y + 14f, preview.width - 20f, preview.height - 30f), NetrunUI.Hex(0x0b0d10));
            Widgets.DrawBoxSolid(new Rect(preview.x + preview.width / 2f - 12f, preview.yMax - 12f, 24f, 7f), theme.KeyFace);
            if (!open)
            {
                Widgets.DrawBoxSolid(preview, new Color(0f, 0f, 0f, 0.55f));
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(preview.ContractedBy(4f), DeckThemes.LockText(theme).Colorize(NetrunUI.Warn));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
            Widgets.Label(new Rect(card.x + 12f, preview.yMax + 6f, card.width - 24f, 22f), theme.Label.ToUpper().Colorize(open ? (chosen ? NetrunUI.Warn : NetrunUI.TextColor) : NetrunUI.Muted));
            Text.Font = GameFont.Tiny;
            string sub = chosen ? (string)"NR_CustomThemeInUse".Translate() : theme.Description;
            Widgets.Label(new Rect(card.x + 12f, preview.yMax + 28f, card.width - 24f, 30f), sub.Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
            NetrunUI.Tip(card, open ? theme.Description : DeckThemes.LockText(theme));
            if (open && !chosen && Widgets.ButtonInvisible(card))
            {
                DeckThemes.Select(theme);
                NetrunSound.Click();
            }
            Widgets.EndGroup();
        }
        _y += (DeckThemes.All.Length + perRow - 1) / perRow * (ch + gap) + 6f;
        Label("NR_CustomFramePreview".Translate(), NetrunUI.Muted);
        Label("NR_CustomFrameParts".Translate(), NetrunUI.Accent);
        Toggle("NR_CustomLights", ref FrameLights, "NR_CustomLightsTip");
        Toggle("NR_CustomGlass", ref FrameGlass, "NR_CustomGlassTip");
        Toggle("NR_CustomLcd", ref FrameLcd, "NR_CustomLcdTip");
        _y += 6f;
    }

    private void DrawSound()
    {
        Label("NR_SettingsSoundPack".Translate(), NetrunUI.Accent, "NR_SettingsSoundPackTip".Translate());
        string[] ids = [.. NetrunSound.Packs, NetrunSound.Off];
        string[] names = ids.Select(id => id == NetrunSound.Off ? (string)"NR_SettingsOff".Translate() : (string)("NR_SoundPack_" + id).Translate()).ToArray();
        string[] tips = ids.Select(id => id == NetrunSound.Off ? (string)"NR_SoundPackDesc_Off".Translate() : (string)("NR_SoundPackDesc_" + id).Translate()).ToArray();
        int current = System.Array.IndexOf(ids, SoundPack.NullOrEmpty() ? NetrunSound.Packs[0] : SoundPack);
        int picked = Chips(names, current, tips);
        if (picked >= 0 && picked != current)
        {
            SoundPack = ids[picked];
            NetrunSound.SettingsChanged();
            NetrunSound.Play("UI_Confirm");
        }
        if (current >= 0)
            Label(tips[current], NetrunUI.Muted);
        if (SoundPack != NetrunSound.Off)
        {
            float ui = UiVolume, ambient = AmbientVolume;
            UiVolume = Slider("NR_SettingsUiVolume".Translate(UiVolume.ToStringPercent()), UiVolume, 0f, 1f);
            AmbientVolume = Slider("NR_SettingsAmbientVolume".Translate(AmbientVolume.ToStringPercent()), AmbientVolume, 0f, 1f);
            if (AmbientVolume > 0f)
            {
                Label("NR_SettingsAmbientEach".Translate(), NetrunUI.Muted);
                AmbientCyberspace = Slider("NR_SettingsAmbientCyberspace".Translate(AmbientCyberspace.ToStringPercent()), AmbientCyberspace, 0f, 1.5f);
                AmbientServer = Slider("NR_SettingsAmbientServer".Translate(AmbientServer.ToStringPercent()), AmbientServer, 0f, 1.5f);
            }
            if (!Mathf.Approximately(ui, UiVolume))
                NetrunSound.Hover();
        }
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
            GUIUtility.systemCopyBuffer = NetrunDiagnostics.Build();
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
            + $"attacks {NetAttackFrequency}, map {MapEventFrequency}, cyber {CyberEventFrequency}, AI {AIEventFrequency}, AI availability {AIAvailability}, map size {MapSize}, newcomer {NewcomerProtection}");
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
        ResetGameplay();
        ResetInterface();
        ResetCustomization();
        ResetSound();
    }

    private void ResetGameplay()
    {
        ApplyPreset(HeatDifficulty.Normal);
        AIEventFrequency = NetAttackFrequency = MapEventFrequency = CyberEventFrequency = 1f;
        AIAvailability = 1f;
        MapSize = NetSize.Large;
        NewcomerProtection = true;
    }

    private void ResetInterface()
    {
        ShowDiveReport = true;
        PauseOnOpen = true;
        MapMinimap = true;
        MapAutopause = true;
        FirstHints = true;
    }

    private void ResetCustomization()
    {
        TerminalAccent = 0;
        Animations = Glitches = Scanlines = true;
        TextScale = 1f;
        IconScale = 1f;
        TerminalFont = true;
        FrameTheme = "Field";
        FrameLights = FrameGlass = FrameLcd = true;
    }

    private void ResetSound()
    {
        SoundPack = "Clean";
        UiVolume = AmbientVolume = 0.6f;
        AmbientTerminal = AmbientCyberspace = AmbientDive = AmbientServer = 1f;
        NetrunSound.SettingsChanged();
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
        Scribe_Values.Look(ref AIAvailability, "aiAvailability", 1f);
        Scribe_Values.Look(ref MapSize, "mapSize", NetSize.Large);
        Scribe_Values.Look(ref NetAttackFrequency, "netAttackFrequency", 1f);
        Scribe_Values.Look(ref MapEventFrequency, "mapEventFrequency", 1f);
        Scribe_Values.Look(ref CyberEventFrequency, "cyberEventFrequency", 1f);
        Scribe_Values.Look(ref ShowDiveReport, "showDiveReport", true);
        Scribe_Values.Look(ref NewcomerProtection, "newcomerProtection", true);
        Scribe_Values.Look(ref PauseOnOpen, "pauseOnOpen", true);
        Scribe_Values.Look(ref Animations, "animations", true);
        Scribe_Values.Look(ref TextScale, "textScale", 1f);
        Scribe_Values.Look(ref IconScale, "iconScale", 1f);
        Scribe_Values.Look(ref TerminalFont, "terminalFont", true);
        Scribe_Values.Look(ref Glitches, "glitches", true);
        Scribe_Values.Look(ref Scanlines, "scanlines", true);
        Scribe_Values.Look(ref MapMinimap, "mapMinimap", true);
        Scribe_Values.Look(ref MapAutopause, "mapAutopause", true);
        Scribe_Values.Look(ref FirstHints, "firstHints", true);
        Scribe_Values.Look(ref MapLens, "mapLens");
        Scribe_Values.Look(ref FrameTheme, "frameTheme", "Field");
        Scribe_Values.Look(ref FrameLights, "frameLights", true);
        Scribe_Values.Look(ref FrameGlass, "frameGlass", true);
        Scribe_Values.Look(ref FrameLcd, "frameLcd", true);
        Scribe_Collections.Look(ref UnlockedThemes, "unlockedThemes", LookMode.Value);
        Scribe_Values.Look(ref IceStrength, "iceStrength", 1f);
        Scribe_Values.Look(ref AIHunger, "aiHunger", 1f);
        Scribe_Values.Look(ref RetreatLoss, "retreatLoss", 1f);
        UnlockedThemes ??= [];
        Scribe_Values.Look(ref SoundPack, "soundPack", "Clean");
        Scribe_Values.Look(ref UiVolume, "uiVolume", 0.6f);
        Scribe_Values.Look(ref AmbientVolume, "ambientVolume", 0.6f);
        Scribe_Values.Look(ref AmbientTerminal, "ambientTerminal", 1f);
        Scribe_Values.Look(ref AmbientCyberspace, "ambientCyberspace", 1f);
        Scribe_Values.Look(ref AmbientDive, "ambientDive", 1f);
        Scribe_Values.Look(ref AmbientServer, "ambientServer", 1f);
        if (SoundPack != NetrunSound.Off && !NetrunSound.Packs.Contains(SoundPack))
            SoundPack = "Clean";
        if (AIEventFrequency < 0f)
            AIEventFrequency = 1f;
        if (ScrubMultiplier <= 0f)
            ScrubMultiplier = 1f;
        // Settings from before the presets: hand-set sliders count as "Custom".
        if (Scribe.mode == LoadSaveMode.LoadingVars && Presets.TryGetValue(Difficulty, out var p)
            && (!Mathf.Approximately(p.gain, HeatGainMultiplier) || !Mathf.Approximately(p.decay, HeatDecayPerDay) || !Mathf.Approximately(p.scrub, ScrubMultiplier)
                || !Mathf.Approximately(p.ice, IceStrength) || !Mathf.Approximately(p.hunger, AIHunger) || !Mathf.Approximately(p.retreat, RetreatLoss)))
            Difficulty = HeatDifficulty.Custom;
    }
}
