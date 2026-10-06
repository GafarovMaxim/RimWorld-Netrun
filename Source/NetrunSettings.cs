using UnityEngine;
using Verse;

namespace Netrun;

public class NetrunSettings : ModSettings
{
    private const float DefaultHeatGainMultiplier = 1f;
    private const float DefaultHeatDecayPerDay = 4f;

    public float HeatGainMultiplier = DefaultHeatGainMultiplier;
    public float HeatDecayPerDay = DefaultHeatDecayPerDay;

    /// <summary>Index into NetrunTex.Accents: the terminal's highlight color.</summary>
    public int TerminalAccent;

    /// <summary>Scales how often AI events come: 0.5x, 1x or 2x.</summary>
    public float AIEventFrequency = 1f;
    private static readonly float[] Frequencies = [0.5f, 1f, 2f];

    /// <summary>Open the dive summary after leaving a dive.</summary>
    public bool ShowDiveReport = true;

    /// <summary>Netrun threats (Heat, AI, fixers, mercenaries) wait until the colony has touched the net.</summary>
    public bool NewcomerProtection = true;

    public void DoWindowContents(Rect inRect)
    {
        var list = new Listing_Standard();
        list.Begin(inRect);

        list.Label("NR_SettingsHeatGain".Translate(HeatGainMultiplier.ToStringPercent()));
        HeatGainMultiplier = list.Slider(HeatGainMultiplier, 0.25f, 3f);
        list.Gap();

        list.Label("NR_SettingsHeatDecay".Translate(HeatDecayPerDay.ToString("0.0")));
        HeatDecayPerDay = list.Slider(HeatDecayPerDay, 0f, 20f);
        list.Gap();

        list.Label("NR_SettingsAccent".Translate());
        for (int i = 0; i < NetrunTex.Accents.Length; i++)
        {
            if (list.RadioButton(("NR_SettingsAccent_" + i).Translate().Colorize(NetrunTex.Accents[i]), TerminalAccent == i))
                TerminalAccent = i;
        }
        list.Gap();

        list.Label("NR_SettingsAIEventFrequency".Translate());
        foreach (float f in Frequencies)
        {
            if (list.RadioButton("NR_SettingsAIEventFrequencyValue".Translate(f.ToString("0.#")), Mathf.Approximately(AIEventFrequency, f)))
                AIEventFrequency = f;
        }
        list.Gap();

        list.CheckboxLabeled("NR_SettingsDiveReport".Translate(), ref ShowDiveReport, "NR_SettingsDiveReportTip".Translate());
        list.CheckboxLabeled("NR_SettingsNewcomer".Translate(), ref NewcomerProtection, "NR_SettingsNewcomerTip".Translate());
        list.Gap();

        if (list.ButtonText("NR_SettingsReset".Translate()))
        {
            ShowDiveReport = true;
            NewcomerProtection = true;
            AIEventFrequency = 1f;
            TerminalAccent = 0;
            HeatGainMultiplier = DefaultHeatGainMultiplier;
            HeatDecayPerDay = DefaultHeatDecayPerDay;
        }

        list.End();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref HeatGainMultiplier, "heatGainMultiplier", DefaultHeatGainMultiplier);
        Scribe_Values.Look(ref HeatDecayPerDay, "heatDecayPerDay", DefaultHeatDecayPerDay);
        Scribe_Values.Look(ref TerminalAccent, "terminalAccent");
        Scribe_Values.Look(ref AIEventFrequency, "aiEventFrequency", 1f);
        Scribe_Values.Look(ref ShowDiveReport, "showDiveReport", true);
        Scribe_Values.Look(ref NewcomerProtection, "newcomerProtection", true);
        if (AIEventFrequency <= 0f)
            AIEventFrequency = 1f;
    }
}
