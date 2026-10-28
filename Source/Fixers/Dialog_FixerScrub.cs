using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The three ways down at a fixer (T1.2): the instant scrub (a Cleaner only: item icons, exact amounts typed in or set
/// with -/+ and "max", the Heat before and after), slow cooling (any fixer) and the isolation state.
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

    // The instant scrub rows show only where the scrub is possible.
    private readonly bool _instant;

    public override Vector2 InitialSize => new(520f, _instant ? 590f : 400f);
    protected override float Margin => 0f;

    public Dialog_FixerScrub(Fixer fixer, Map map)
    {
        _fixer = fixer;
        _map = map;
        _availableData = ColonyGoods.Count(map, NetrunDefOf.USH_Cyberdata);
        _availableAdvanced = ColonyGoods.Count(map, NetrunDefOf.USH_AdvancedCyberdata);
        _instant = fixer.CanScrubNow.Accepted;

        doCloseX = false;
        forcePause = false;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = true;
        preventCameraMotion = false;
        draggable = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        bool font = NetrunFont.Push();
        try
        {
            Rect screen = NetrunUI.Bezel(inRect, 8f);
            DrawContents(screen);
            NetrunUI.Scanlines(screen);
        }
        finally
        {
            NetrunFont.Pop(font);
        }
    }

    private void DrawContents(Rect inRect)
    {
        Widgets.DrawBoxSolid(inRect, NetrunUI.BgWindow);
        Rect r = inRect.ContractedBy(14f);
        float y = r.y;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(r.x, y, r.width, 32f), "NR_ScrubTitle".Translate(_fixer.Name).Colorize(NetrunUI.Accent));
        Text.Font = GameFont.Small;
        y += 34f;
        FixerSettingsDef settings = NetrunDefOf.NR_Fixers;
        float heat = HeatTracker.Get()?.Heat ?? 0f;

        // 1. Instant scrub: a Cleaner only, capped, once per cooldown.
        y = Header(r, y, "NR_ScrubChannelInstant".Translate());
        Text.Font = GameFont.Tiny;
        // The AI noise floor, shown with every channel (plan v1, section 8 p.4).
        if (AIBalance.NoiseFloor > 0f)
            y = Line(r, y, "NR_ScrubFloor".Translate(AIBalance.NoiseFloor.ToString("0")), NetrunUI.Warn);
        if (!_instant)
        {
            y = Line(r, y, _fixer.CanScrubNow.Reason, NetrunUI.Muted);
        }
        else
        {
            y = Line(r, y, "NR_ScrubRates".Translate(_fixer.HeatPerCyberdata.ToString("0.#"), _fixer.HeatPerAdvancedCyberdata.ToString("0.#")), NetrunUI.Muted);
            y = Line(r, y, "NR_ScrubCapLine".Translate(Fixer.ScrubCap.ToString("0"), settings.cleanerScrubCooldownDays), NetrunUI.Muted);
            // r4: the Veteran trait changes the rates above; say so (once the trait is known).
            if (_fixer.Trait == FixerTrait.Veteran && _fixer.TraitKnown)
                y = Line(r, y, "NR_ScrubVeteran".Translate(settings.veteranScrubFactor.ToString("0.##")), NetrunUI.Muted);
            Text.Font = GameFont.Small;
            y += 4f;
            int needData = FixerDeals.UnitsToClear(_fixer, false, _advanced, _availableData);
            int needAdvanced = FixerDeals.UnitsToClear(_fixer, true, _data, _availableAdvanced);
            y = AmountRow(new Rect(r.x, y, r.width, RowHeight), NetrunDefOf.USH_Cyberdata, ref _data, ref _dataBuffer, _availableData, needData) + 8f;
            y = AmountRow(new Rect(r.x, y, r.width, RowHeight), NetrunDefOf.USH_AdvancedCyberdata, ref _advanced, ref _advancedBuffer, _availableAdvanced, needAdvanced) + 10f;

            // Heat now and after, on one bar.
            float scrub = Mathf.Min(heat, FixerDeals.ScrubAmount(_fixer, _data, _advanced));
            float after = heat - scrub;
            var bar = new Rect(r.x, y, r.width, 16f);
            NetrunUI.Bar(bar, heat / HeatTracker.MaxHeat, NetrunUI.Bad);
            Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * after / HeatTracker.MaxHeat, bar.height), HeatLevelUtility.LevelOf(after).Color());
            y += 20f;
            Widgets.Label(new Rect(r.x, y, r.width, 24f), "NR_ScrubResult".Translate(scrub.ToString("0"), heat.ToString("0"), after.ToString("0")));
            y += 24f;
            Text.Font = GameFont.Tiny;
            if (ContractManager.Get()?.Active.Any(c => c.Type == ContractType.LayLow && !c.OldRules) == true)
                y = Line(r, y, "NR_ScrubFailsLayLow".Translate(), NetrunUI.Bad);
        }
        Text.Font = GameFont.Small;
        y += 6f;

        // 2. Slow cooling: any fixer, one deal at a time.
        y = Header(r, y, "NR_ScrubChannelCooling".Translate());
        Text.Font = GameFont.Tiny;
        HeatTracker tracker = HeatTracker.Get();
        string cooling = tracker is { CoolingActive: true }
            ? "NR_CoolingActive".Translate(tracker.CoolingTicksLeft.ToStringTicksToPeriod())
            : "NR_CoolingLine".Translate(_fixer.CoolingCost, settings.coolingPerDay.ToString("0.#"), settings.coolingDays,
                (settings.coolingPerDay * settings.coolingDays).ToString("0"));
        string coolLabel = "NR_CoolingButton".Translate(_fixer.CoolingCost);
        float coolW = NetrunUI.ButtonWidth(coolLabel);
        float ch = Mathf.Max(30f, Text.CalcHeight(cooling, r.width - coolW - 10f));
        Widgets.Label(new Rect(r.x, y, r.width - coolW - 10f, ch), cooling.Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        string refusal = FixerDeals.CoolingRefusal(_fixer, _map);
        if (NetrunUI.Button(new Rect(r.xMax - coolW, y, coolW, 30f), coolLabel, NetrunUI.Ok, refusal == null ? true : (AcceptanceReport)refusal,
                "NR_CoolingTip".Translate()))
        {
            FixerDeals.Cool(_fixer, _map);
            Close();
        }
        y += ch + 8f;
        // r3: paid cooling is not natural cooling either.
        if (ContractManager.Get()?.Active.Any(c => c.Type == ContractType.LayLow && !c.OldRules) == true)
        {
            Text.Font = GameFont.Tiny;
            y = Line(r, y - 4f, "NR_CoolingFailsLayLow".Translate(), NetrunUI.Bad) + 4f;
            Text.Font = GameFont.Small;
        }

        // 3. Isolation: information only (the switch is on the Defense screen).
        y = Header(r, y, "NR_ScrubChannelIsolation".Translate());
        Text.Font = GameFont.Tiny;
        float need = NetrunDefOf.NR_Heat.isolationCoolingHours;
        string iso = HeatTracker.IsolationCooling ? "NR_IsolationCoolingOn".Translate(NetrunDefOf.NR_Heat.isolationCoolingFactor.ToString("0.#"))
            : NetworkIsolation.Active ? "NR_IsolationCoolingWait".Translate(HeatTracker.IsolationHours.ToString("0"), need.ToString("0"))
            : "NR_IsolationCoolingOff".Translate(need.ToString("0"), NetrunDefOf.NR_Heat.isolationCoolingFactor.ToString("0.#"));
        Line(r, y, iso, HeatTracker.IsolationCooling ? NetrunUI.Ok : NetrunUI.Muted);
        Text.Font = GameFont.Small;

        // Buttons.
        float by = r.yMax - 36f;
        string confirm = "NR_ScrubConfirm".Translate();
        AcceptanceReport can = _data + _advanced > 0 ? true : (AcceptanceReport)"NR_ScrubNothing".Translate();
        float cw = _instant ? NetrunUI.ButtonWidth(confirm) : -8f;
        if (_instant && NetrunUI.Button(new Rect(r.xMax - cw, by, cw, 34f), confirm, NetrunUI.Accent, can))
        {
            FixerDeals.Scrub(_fixer, _map, _data, _advanced);
            Close();
        }
        string cancel = "CloseButton".Translate();
        float xw = NetrunUI.ButtonWidth(cancel);
        if (NetrunUI.Button(new Rect(r.xMax - cw - 8f - xw, by, xw, 34f), cancel, NetrunUI.Muted, true))
            Close();
    }

    private static float Header(Rect r, float y, string text)
    {
        Widgets.Label(new Rect(r.x, y, r.width, 22f), text.Colorize(NetrunUI.Accent));
        return y + 22f;
    }

    /// <summary>One wrapped line in the current font.</summary>
    private static float Line(Rect r, float y, string text, Color color)
    {
        float h = Text.CalcHeight(text, r.width);
        Widgets.Label(new Rect(r.x, y, r.width, h), text.Colorize(color));
        return y + h + 2f;
    }

    /// <summary>Icon, name, then "min", -, an exact number field, +, and "max".</summary>
    private static float AmountRow(Rect rect, ThingDef def, ref int value, ref string buffer, int available, int needed)
    {
        NetrunUI.Card(rect, NetrunUI.Accent);
        GUI.DrawTexture(new Rect(rect.x + 12f, rect.y + 6f, 32f, 32f), def.uiIcon);
        // The name in the room left of the buttons (min starts 274 px from the right); smaller and wrapped when long.
        float nameW = rect.width - 274f - 52f - 8f;
        string name = def.LabelCap + "  " + ("(" + available + ")").Colorize(NetrunUI.Muted);
        Text.Font = Text.CalcSize(name.StripTags()).x > nameW ? GameFont.Tiny : GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(rect.x + 52f, rect.y, nameW, rect.height), name);
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        // No more than brings the Heat to its floor: data past it would be spent for nothing.
        int limit = Mathf.Min(available, Mathf.Max(needed, 0));
        if (value > limit)
        {
            value = limit;
            buffer = null;
        }

        float x = rect.xMax - 10f - 60f;
        float cy = rect.y + 7f;
        AcceptanceReport any = available > 0 ? true : (AcceptanceReport)"NR_ScrubNone".Translate();
        if (NetrunUI.Button(new Rect(x, cy, 60f, 30f), "NR_ScrubMax".Translate(), NetrunUI.Ok, any, "NR_ScrubMaxTip".Translate()))
        {
            // Just enough to bring the Heat to zero (or all there is).
            value = limit;
            buffer = null;
        }
        x -= 36f;
        if (NetrunUI.Button(new Rect(x, cy, 30f, 30f), "+", NetrunUI.Accent, value < limit ? true : (AcceptanceReport)false))
        {
            value++;
            buffer = null;
        }
        x -= 66f;
        Widgets.TextFieldNumeric(new Rect(x, cy + 2f, 60f, 26f), ref value, ref buffer, 0, limit);
        x -= 36f;
        if (NetrunUI.Button(new Rect(x, cy, 30f, 30f), "−", NetrunUI.Accent, value > 0 ? true : (AcceptanceReport)false))
        {
            value--;
            buffer = null;
        }
        x -= 66f;
        if (NetrunUI.Button(new Rect(x, cy, 60f, 30f), "NR_ScrubMin".Translate(), NetrunUI.TextColor, value > 0 ? true : (AcceptanceReport)false))
        {
            value = 0;
            buffer = null;
        }
        return rect.yMax;
    }
}
