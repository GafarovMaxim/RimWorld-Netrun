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

        if (list.ButtonText("NR_SettingsReset".Translate()))
        {
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
    }
}
