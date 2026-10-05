using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>Choose how much cyberdata to hand over to scrub the trace.</summary>
public class Dialog_FixerScrub : Window
{
    private readonly Fixer _fixer;
    private readonly Map _map;
    private readonly int _availableData;
    private readonly int _availableAdvanced;

    private float _data;
    private float _advanced;

    public override Vector2 InitialSize => new(460f, 330f);

    public Dialog_FixerScrub(Fixer fixer, Map map)
    {
        _fixer = fixer;
        _map = map;
        _availableData = ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata);
        _availableAdvanced = ColonyGoods.Count(map, NetrunDefOf.USH_AdvancedCyberdata);

        doCloseX = true;
        forcePause = true;
        absorbInputAroundWindow = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        var list = new Listing_Standard();
        list.Begin(inRect);

        Text.Font = GameFont.Medium;
        list.Label("NR_ScrubTitle".Translate(_fixer.Name));
        Text.Font = GameFont.Small;
        list.Label("NR_ScrubRates".Translate(
            _fixer.HeatPerCyberdata.ToString("0.#"),
            _fixer.HeatPerAdvancedCyberdata.ToString("0.#")));
        list.GapLine();

        int data = Mathf.RoundToInt(_data);
        int advanced = Mathf.RoundToInt(_advanced);

        list.Label("NR_ScrubCyberdata".Translate(NetrunDefOf.USH_Cyberdata.LabelCap, data, _availableData));
        if (_availableData > 0)
            _data = list.Slider(_data, 0f, _availableData);

        list.Label("NR_ScrubCyberdata".Translate(NetrunDefOf.USH_AdvancedCyberdata.LabelCap, advanced, _availableAdvanced));
        if (_availableAdvanced > 0)
            _advanced = list.Slider(_advanced, 0f, _availableAdvanced);

        list.GapLine();

        float heat = HeatTracker.Get()?.Heat ?? 0f;
        float scrub = Mathf.Min(heat, FixerDeals.ScrubAmount(_fixer, data, advanced));
        list.Label("NR_ScrubResult".Translate(scrub.ToString("0"), heat.ToString("0"), (heat - scrub).ToString("0")));

        list.Gap();
        if (list.ButtonText("NR_ScrubConfirm".Translate()) && (data > 0 || advanced > 0))
        {
            FixerDeals.Scrub(_fixer, _map, data, advanced);
            Close();
        }

        list.End();
    }
}
