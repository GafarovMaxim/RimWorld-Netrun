using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>What one dive brought and cost; filled when the dive ends, shown when the player leaves it.</summary>
public class DiveReport : IExposable
{
    public string NodeName;
    public string RunnerName;
    public bool Success;
    public bool Retreated;
    public int Turns;
    public int CostPaid;
    public int Cyberdata;
    public int Advanced;
    public int Silver;
    public List<ThingDef> Items = [];
    public int LostCyberdata;
    public int LostAdvanced;
    public int LostSilver;
    public int LostItems;
    public float HeatBefore;
    public float HeatAfter;
    public float NetsickBefore;
    public float NetsickAfter;
    public float Xp;
    public bool Special;
    public bool HunterFollowed;
    public List<string> Events = [];

    public void ExposeData()
    {
        Scribe_Values.Look(ref NodeName, "node");
        Scribe_Values.Look(ref RunnerName, "runner");
        Scribe_Values.Look(ref Success, "success");
        Scribe_Values.Look(ref Retreated, "retreated");
        Scribe_Values.Look(ref Turns, "turns");
        Scribe_Values.Look(ref CostPaid, "cost");
        Scribe_Values.Look(ref Cyberdata, "cyberdata");
        Scribe_Values.Look(ref Advanced, "advanced");
        Scribe_Values.Look(ref Silver, "silver");
        Scribe_Collections.Look(ref Items, "items", LookMode.Def);
        Scribe_Values.Look(ref LostCyberdata, "lostCyberdata");
        Scribe_Values.Look(ref LostAdvanced, "lostAdvanced");
        Scribe_Values.Look(ref LostSilver, "lostSilver");
        Scribe_Values.Look(ref LostItems, "lostItems");
        Scribe_Values.Look(ref HeatBefore, "heatBefore");
        Scribe_Values.Look(ref HeatAfter, "heatAfter");
        Scribe_Values.Look(ref NetsickBefore, "netsickBefore");
        Scribe_Values.Look(ref NetsickAfter, "netsickAfter");
        Scribe_Values.Look(ref Xp, "xp");
        Scribe_Values.Look(ref Special, "special");
        Scribe_Values.Look(ref HunterFollowed, "hunterFollowed");
        Scribe_Collections.Look(ref Events, "events", LookMode.Value);
        Items ??= [];
        Events ??= [];
    }

    public static float NetsickOf(Pawn pawn)
        => pawn?.health?.hediffSet?.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick)?.Severity ?? 0f;
}

/// <summary>"Dive summary": result, the haul as item tiles, costs and consequences, events; cards in the terminal style.</summary>
public class Dialog_DiveReport : Window
{
    private readonly DiveReport _report;
    /// <summary>Rows type out in order the first time the window shows.</summary>
    private static int _row;
    private readonly string _stamp = System.Guid.NewGuid().ToString("N");

    private string Typed(string text) => NetrunUI.Typed(_stamp + _row, text, 120f, 0.12f * _row++);

    public override Vector2 InitialSize => new(Mathf.Min(780f, UI.screenWidth - 40f), Mathf.Min(560f, UI.screenHeight - 40f));
    protected override float Margin => 0f;

    public Dialog_DiveReport(DiveReport report)
    {
        _report = report;
        doCloseX = false;
        forcePause = false;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = true;
        draggable = true;
    }

    /// <summary>Open the summary if the player wants it (mod setting).</summary>
    public static void Show(DiveReport report)
    {
        if (report != null && NetrunMod.Settings.ShowDiveReport)
            Find.WindowStack.Add(new Dialog_DiveReport(report));
    }

    private static Texture2D Icon(string name) => NetrunTex.CyberIcon(name);

    public override void DoWindowContents(Rect inRect)
    {
        _row = 0;
        Widgets.DrawBoxSolid(inRect, NetrunUI.BgWindow);
        GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.5f);
        Widgets.DrawBox(inRect);
        GUI.color = Color.white;
        Rect r = inRect.ContractedBy(14f);
        DiveReport d = _report;

        // Title: icon, name, "?".
        float titleX = r.x;
        if (NetrunTex.DiveReport is { } reportIcon)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, 30f, 30f), reportIcon);
            titleX += 38f;
        }
        if (NetrunUI.Button(new Rect(r.xMax - 28f, r.y, 28f, 28f), "?", NetrunUI.Accent, true, "NR_HelpButtonTip".Translate()))
        {
            Close();
            Dialog_NetTerminal.OpenCyberpedia(Find.CurrentMap, "DiveReport");
        }
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(titleX, r.y, r.xMax - titleX - 34f, 32f), "NR_ReportTitle".Translate(d.NodeName.ToUpper()).Colorize(NetrunUI.Accent));
        Text.Font = GameFont.Small;
        float y = r.y + 38f;

        // Result strip: status chip with icon, runner and turns.
        (string result, Color color, string icon) = d.Success
            ? ((string)"NR_ReportSuccess".Translate(), NetrunUI.Ok, "Action_Exit")
            : d.Retreated ? ((string)"NR_ReportRetreat".Translate(), NetrunUI.Warn, "Action_Move") : ((string)"NR_ReportFail".Translate(), NetrunUI.Bad, "Dive_BLACKICE");
        var strip = new Rect(r.x, y, r.width, 34f);
        Widgets.DrawBoxSolid(strip, new Color(color.r, color.g, color.b, 0.12f));
        Widgets.DrawBoxSolid(new Rect(strip.x, strip.y, 3f, strip.height), color);
        if (Icon(icon) is { } statusIcon)
            GUI.DrawTexture(new Rect(strip.x + 10f, strip.y + 5f, 24f, 24f), statusIcon);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(strip.x + 42f, strip.y, 200f, strip.height), result.ToUpper().Colorize(color));
        Widgets.Label(new Rect(strip.x + 240f, strip.y, strip.width - 250f, strip.height), "NR_ReportRunner".Translate(d.RunnerName, d.Turns).Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
        y = strip.yMax + 10f;

        // Two cards: the haul, and costs and consequences; events below across the width.
        float bottom = r.yMax - 40f;
        float eventsH = d.Events.Count > 0 ? Mathf.Min(30f + d.Events.Count * 28f, 120f) : 0f;
        float cardsH = bottom - y - (eventsH > 0f ? eventsH + 10f : 0f);
        float half = (r.width - 10f) / 2f;
        DrawHaul(new Rect(r.x, y, half, cardsH), d);
        DrawCosts(new Rect(r.x + half + 10f, y, half, cardsH), d);
        if (eventsH > 0f)
            DrawEvents(new Rect(r.x, y + cardsH + 10f, r.width, eventsH), d);

        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(r.x, r.yMax - 26f, r.width - 140f, 24f), "NR_ReportSettingHint".Translate().Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        string close = "CloseButton".Translate();
        float cw = NetrunUI.ButtonWidth(close);
        if (NetrunUI.Button(new Rect(r.xMax - cw, r.yMax - 32f, cw, 32f), close, NetrunUI.Accent, true))
            Close();
    }

    private static Rect Card(Rect rect, string title, Texture2D icon)
    {
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(1f, 1f, 1f, 0.1f));
        Rect inner = rect.ContractedBy(10f);
        float x = inner.x;
        if (icon != null)
        {
            GUI.DrawTexture(new Rect(inner.x, inner.y + 2f, 22f, 22f), icon);
            x += 28f;
        }
        float y = NetrunUI.Section(new Rect(x, inner.y - 2f, inner.xMax - x, 30f), title);
        return new Rect(inner.x, y - 4f, inner.width, inner.yMax - y + 4f);
    }

    private void DrawHaul(Rect rect, DiveReport d)
    {
        Rect r = Card(rect, "NR_ReportHaul".Translate(), Icon("Reward_Valuables"));
        var haul = new List<(ThingDef def, int count)>();
        if (d.Cyberdata > 0) haul.Add((NetrunDefOf.USH_Cyberdata, d.Cyberdata));
        if (d.Advanced > 0) haul.Add((NetrunDefOf.USH_AdvancedCyberdata, d.Advanced));
        if (d.Silver > 0) haul.Add((ThingDefOf.Silver, d.Silver));
        foreach (IGrouping<ThingDef, ThingDef> g in d.Items.GroupBy(i => i))
            haul.Add((g.Key, g.Count()));

        float y = r.y;
        if (haul.Count == 0)
        {
            Widgets.Label(new Rect(r.x, y, r.width, 22f), "NR_ReportNothing".Translate().Colorize(NetrunUI.Muted));
            y += 26f;
        }
        // Tiles: a big item icon, the count and the name.
        float tw = (r.width - 6f) / 2f;
        for (int i = 0; i < haul.Count; i++)
        {
            (ThingDef def, int count) = haul[i];
            var tile = new Rect(r.x + i % 2 * (tw + 6f), y + i / 2 * 48f, tw, 42f);
            Widgets.DrawBoxSolid(tile, NetrunUI.BgWindow);
            Widgets.ThingIcon(new Rect(tile.x + 5f, tile.y + 5f, 32f, 32f), def);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(tile.x + 42f, tile.y, tile.width - 44f, 26f), count.ToString().Colorize(NetrunUI.Ok));
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(tile.x + 42f, tile.y + 22f, tile.width - 44f, 18f), def.LabelCap.ToString().Truncate(tile.width - 46f).Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            NetrunUI.Tip(tile, def.LabelCap + " ×" + count);
        }
        y += (haul.Count + 1) / 2 * 48f + 4f;

        if (d.Special)
            y = Line(r, y, Icon("Reward_DaemonProgram"), "NR_ReportSpecial".Translate(), NetrunUI.Ok);
        if (d.LostCyberdata + d.LostAdvanced + d.LostSilver + d.LostItems > 0)
            Line(r, y, Icon("Dive_Lightning_Red"), "NR_ReportLost".Translate(d.LostCyberdata, d.LostAdvanced, d.LostSilver, d.LostItems), NetrunUI.Bad);
    }

    private void DrawCosts(Rect rect, DiveReport d)
    {
        Rect r = Card(rect, "NR_ReportCosts".Translate(), Icon("Dive_Heat"));
        float y = r.y;
        y = Row(r, y, NetrunDefOf.USH_Cyberdata.uiIcon, "NR_ReportCost".Translate(), "−" + d.CostPaid, NetrunUI.Warn);
        float heat = d.HeatAfter - d.HeatBefore;
        HeatLevel after = HeatLevelUtility.LevelOf(d.HeatAfter);
        y = Row(r, y, Icon("Dive_Heat") ?? NetrunTex.HeatIcon(after), "NR_ReportHeat".Translate(), $"{d.HeatBefore:0} → {d.HeatAfter:0}  ({(heat >= 0 ? "+" : string.Empty)}{heat:0.#})",
            heat > 0.05f ? NetrunUI.Bad : NetrunUI.Ok);
        float sick = d.NetsickAfter - d.NetsickBefore;
        y = Row(r, y, Icon("Icon_Netsick") ?? NetrunTex.AIUse, "NR_ReportNetsick".Translate(), $"{d.NetsickBefore.ToStringPercent()} → {d.NetsickAfter.ToStringPercent()}  (+{sick.ToStringPercent()})",
            sick > 0.001f ? NetrunUI.Warn : NetrunUI.Muted);
        if (d.Xp > 0f)
            y = Row(r, y, Icon("Icon_Xp") ?? Icon("Action_Hack"), "NR_ReportXp".Translate(), "+" + d.Xp.ToString("0") + " " + SkillDefOf.Intellectual.label, NetrunUI.Ok);
        if (d.HunterFollowed)
            Row(r, y, Icon("Event_HunterICE"), "NR_ReportHunter".Translate(), "NR_ReportHunterValue".Translate(), NetrunUI.Bad);
    }

    private static void DrawEvents(Rect rect, DiveReport d)
    {
        Rect r = Card(rect, "NR_ReportEvents".Translate(), Icon("Event_DataStorm"));
        float y = r.y;
        float colW = (r.width - 10f) / 2f;
        for (int i = 0; i < d.Events.Count; i++)
        {
            string e = d.Events[i];
            int bar = e.IndexOf('|');
            Texture2D icon = bar > 0 ? Icon(e.Substring(0, bar)) : null;
            string text = bar > 0 ? e.Substring(bar + 1) : e;
            var cell = new Rect(r.x + i % 2 * (colW + 10f), y + i / 2 * 26f, colW, 24f);
            if (icon != null)
                GUI.DrawTexture(new Rect(cell.x, cell.y + 1f, 22f, 22f), icon);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(cell.x + 28f, cell.y, cell.width - 30f, cell.height), text.Truncate(cell.width - 30f));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            NetrunUI.Tip(cell, text);
        }
    }

    private float Row(Rect r, float y, Texture2D icon, string label, string value, Color color)
    {
        var row = new Rect(r.x, y, r.width, 30f);
        Widgets.DrawBoxSolid(row, NetrunUI.BgWindow);
        if (icon != null)
            GUI.DrawTexture(new Rect(row.x + 5f, row.y + 4f, 22f, 22f), icon);
        Text.Anchor = TextAnchor.MiddleLeft;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(row.x + 32f, row.y, 130f, row.height), label.Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(new Rect(row.x + 160f, row.y, row.width - 166f, row.height), Typed(value).Colorize(color));
        Text.Anchor = TextAnchor.UpperLeft;
        return y + 34f;
    }

    private float Line(Rect r, float y, Texture2D icon, string text, Color color)
    {
        float h = Mathf.Max(24f, Text.CalcHeight(text, r.width - 30f));
        if (icon != null)
            GUI.DrawTexture(new Rect(r.x, y + 1f, 22f, 22f), icon);
        Widgets.Label(new Rect(r.x + 28f, y + 2f, r.width - 30f, h), Typed(text).Colorize(color));
        return y + h + 4f;
    }
}
