using UnityEngine;
using Verse;

namespace Netrun;

public class NetrunSettings : ModSettings
{
    private const float DefaultHeatGainMultiplier = 1f;
    private const float DefaultHeatDecayPerDay = 4f;

    public float HeatGainMultiplier = DefaultHeatGainMultiplier;
    public float HeatDecayPerDay = DefaultHeatDecayPerDay;

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

        if (list.ButtonText("NR_SettingsReset".Translate()))
        {
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
    }
}
