using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Choose how much cyberdata to hand over to scrub the trace. Styled like the terminal: item icons, exact
/// amounts typed in or set with -/+ and "max", and the Heat before and after.
/// </summary>
public class Dialog_FixerScrub : Window
{
    private const float RowHeight = 44f;

    private readonly Fixer _fixer;
    private readonly Map _map;
    private readonly int _availableData;
    private readonly int _availableAdvanced;

    private int _data;
    private int _advanced;
    private string _dataBuffer;
    private string _advancedBuffer;

    public override Vector2 InitialSize => new(520f, 380f);
    protected override float Margin => 0f;

    public Dialog_FixerScrub(Fixer fixer, Map map)
    {
        _fixer = fixer;
        _map = map;
        _availableData = ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata);
        _availableAdvanced = ColonyGoods.Count(map, NetrunDefOf.USH_AdvancedCyberdata);

        doCloseX = false;
        forcePause = false;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = true;
        draggable = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Widgets.DrawBoxSolid(inRect, NetrunUI.BgWindow);
        Rect r = inRect.ContractedBy(14f);
        float y = r.y;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(r.x, y, r.width, 32f), "NR_ScrubTitle".Translate(_fixer.Name).Colorize(NetrunUI.Accent));
        Text.Font = GameFont.Small;
        y += 34f;
        Widgets.Label(new Rect(r.x, y, r.width, 22f),
            "NR_ScrubRates".Translate(_fixer.HeatPerCyberdata.ToString("0.#"), _fixer.HeatPerAdvancedCyberdata.ToString("0.#")).Colorize(NetrunUI.Muted));
        y += 30f;

        y = AmountRow(new Rect(r.x, y, r.width, RowHeight), NetrunDefOf.USH_Cyberdata, ref _data, ref _dataBuffer, _availableData) + 8f;
        y = AmountRow(new Rect(r.x, y, r.width, RowHeight), NetrunDefOf.USH_AdvancedCyberdata, ref _advanced, ref _advancedBuffer, _availableAdvanced) + 12f;

        // Heat now and after, on one bar.
        float heat = HeatTracker.Get()?.Heat ?? 0f;
        float scrub = Mathf.Min(heat, FixerDeals.ScrubAmount(_fixer, _data, _advanced));
        float after = heat - scrub;
        var bar = new Rect(r.x, y, r.width, 16f);
        NetrunUI.Bar(bar, heat / HeatTracker.MaxHeat, NetrunUI.Bad);
        Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * after / HeatTracker.MaxHeat, bar.height), HeatLevelUtility.LevelOf(after).Color());
        y += 22f;
        Widgets.Label(new Rect(r.x, y, r.width, 24f), "NR_ScrubResult".Translate(scrub.ToString("0"), heat.ToString("0"), after.ToString("0")));

        // Buttons.
        float by = r.yMax - 36f;
        string confirm = "NR_ScrubConfirm".Translate();
        AcceptanceReport can = _data + _advanced > 0 ? true : (AcceptanceReport)"NR_ScrubNothing".Translate();
        float cw = NetrunUI.ButtonWidth(confirm);
        if (NetrunUI.Button(new Rect(r.xMax - cw, by, cw, 34f), confirm, NetrunUI.Accent, can))
        {
            FixerDeals.Scrub(_fixer, _map, _data, _advanced);
            Close();
        }
        string cancel = "CloseButton".Translate();
        float xw = NetrunUI.ButtonWidth(cancel);
        if (NetrunUI.Button(new Rect(r.xMax - cw - 8f - xw, by, xw, 34f), cancel, NetrunUI.Muted, true))
            Close();
    }

    /// <summary>Icon, name, then "min", -, an exact number field, +, and "max".</summary>
    private static float AmountRow(Rect rect, ThingDef def, ref int value, ref string buffer, int available)
    {
        NetrunUI.Card(rect, NetrunUI.Accent);
        GUI.DrawTexture(new Rect(rect.x + 12f, rect.y + 6f, 32f, 32f), def.uiIcon);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(rect.x + 52f, rect.y, 170f, rect.height), def.LabelCap + "  " + ("(" + available + ")").Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;

        float x = rect.xMax - 10f - 60f;
        float cy = rect.y + 7f;
        AcceptanceReport any = available > 0 ? true : (AcceptanceReport)"NR_ScrubNone".Translate();
        if (NetrunUI.Button(new Rect(x, cy, 60f, 30f), "NR_ScrubMax".Translate(), NetrunUI.Ok, any))
        {
            value = available;
            buffer = null;
        }
        x -= 36f;
        if (NetrunUI.Button(new Rect(x, cy, 30f, 30f), "+", NetrunUI.Accent, value < available ? true : (AcceptanceReport)false))
        {
            value++;
            buffer = null;
        }
        x -= 66f;
        Widgets.TextFieldNumeric(new Rect(x, cy + 2f, 60f, 26f), ref value, ref buffer, 0, available);
        x -= 36f;
        if (NetrunUI.Button(new Rect(x, cy, 30f, 30f), "−", NetrunUI.Accent, value > 0 ? true : (AcceptanceReport)false))
        {
            value--;
            buffer = null;
        }
        x -= 66f;
        if (NetrunUI.Button(new Rect(x, cy, 60f, 30f), "NR_ScrubMin".Translate(), NetrunUI.Muted, value > 0 ? true : (AcceptanceReport)false))
        {
            value = 0;
            buffer = null;
        }
        return rect.yMax;
    }
}
