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

/// <summary>"Dive summary": the result, the haul with item icons, costs and consequences.</summary>
public class Dialog_DiveReport : Window
{
    private readonly DiveReport _report;

    public override Vector2 InitialSize => new(520f, Mathf.Min(600f, UI.screenHeight - 40f));
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

    public override void DoWindowContents(Rect inRect)
    {
        Widgets.DrawBoxSolid(inRect, NetrunUI.BgWindow);
        Rect r = inRect.ContractedBy(14f);
        DiveReport d = _report;

        float titleX = r.x;
        if (NetrunTex.DiveReport is { } reportIcon)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, 30f, 30f), reportIcon);
            titleX += 36f;
        }
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(titleX, r.y, r.xMax - titleX, 32f), "NR_ReportTitle".Translate(d.NodeName.ToUpper()).Colorize(NetrunUI.Accent));
        Text.Font = GameFont.Small;
        float y = r.y + 36f;

        (string result, Color color) = d.Success
            ? ("NR_ReportSuccess".Translate(), NetrunUI.Ok)
            : d.Retreated ? ("NR_ReportRetreat".Translate(), NetrunUI.Warn) : ("NR_ReportFail".Translate(), NetrunUI.Bad);
        float x = r.x + NetrunUI.Chip(r.x, y, result.ToUpper(), color) + 8f;
        Widgets.Label(new Rect(x, y, r.xMax - x, 22f), "NR_ReportRunner".Translate(d.RunnerName, d.Turns).Colorize(NetrunUI.Muted));
        y += 30f;

        // The haul, with item icons.
        y = NetrunUI.Section(new Rect(r.x, y, r.width, 36f), "NR_ReportHaul".Translate());
        var haul = new List<(ThingDef def, int count)>();
        if (d.Cyberdata > 0) haul.Add((NetrunDefOf.USH_Cyberdata, d.Cyberdata));
        if (d.Advanced > 0) haul.Add((NetrunDefOf.USH_AdvancedCyberdata, d.Advanced));
        if (d.Silver > 0) haul.Add((ThingDefOf.Silver, d.Silver));
        foreach (IGrouping<ThingDef, ThingDef> g in d.Items.GroupBy(i => i))
            haul.Add((g.Key, g.Count()));
        if (haul.Count == 0)
        {
            Widgets.Label(new Rect(r.x, y, r.width, 22f), "NR_ReportNothing".Translate().Colorize(NetrunUI.Muted));
            y += 26f;
        }
        x = r.x;
        foreach ((ThingDef def, int count) in haul)
        {
            string label = $"{count} × {def.label}";
            float w = 30f + Text.CalcSize(label).x + 12f;
            if (x + w > r.xMax)
            {
                x = r.x;
                y += 30f;
            }
            var cell = new Rect(x, y, w, 26f);
            Widgets.ThingIcon(new Rect(x, y + 1f, 24f, 24f), def);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x + 28f, y, w - 28f, 26f), label);
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(cell, def.LabelCap);
            x += w + 6f;
        }
        if (haul.Count > 0)
            y += 32f;
        if (d.Special)
        {
            Widgets.Label(new Rect(r.x, y, r.width, 22f), "NR_ReportSpecial".Translate().Colorize(NetrunUI.Ok));
            y += 24f;
        }
        if (d.LostCyberdata + d.LostAdvanced + d.LostSilver + d.LostItems > 0)
        {
            string lost = "NR_ReportLost".Translate(d.LostCyberdata, d.LostAdvanced, d.LostSilver, d.LostItems);
            float lh = Text.CalcHeight(lost, r.width);
            Widgets.Label(new Rect(r.x, y, r.width, lh), lost.Colorize(NetrunUI.Bad));
            y += lh + 4f;
        }

        // Costs and consequences.
        y = NetrunUI.Section(new Rect(r.x, y + 4f, r.width, 36f), "NR_ReportCosts".Translate());
        y = Line(r, y, "NR_ReportCost".Translate(), d.CostPaid.ToString(), NetrunUI.Warn);
        float heat = d.HeatAfter - d.HeatBefore;
        y = Line(r, y, "NR_ReportHeat".Translate(), $"{d.HeatBefore:0} → {d.HeatAfter:0} ({(heat >= 0 ? "+" : string.Empty)}{heat:0.#})", heat > 0.05f ? NetrunUI.Bad : NetrunUI.Ok);
        float sick = d.NetsickAfter - d.NetsickBefore;
        y = Line(r, y, "NR_ReportNetsick".Translate(), $"{d.NetsickBefore.ToStringPercent()} → {d.NetsickAfter.ToStringPercent()} (+{sick.ToStringPercent()})", sick > 0.001f ? NetrunUI.Warn : NetrunUI.Muted);
        if (d.Xp > 0f)
            y = Line(r, y, "NR_ReportXp".Translate(), "+" + d.Xp.ToString("0") + " " + SkillDefOf.Intellectual.label, NetrunUI.Ok);
        if (d.HunterFollowed)
            y = Line(r, y, "NR_ReportHunter".Translate(), "NR_ReportHunterValue".Translate(), NetrunUI.Bad);

        if (d.Events.Count > 0)
        {
            y = NetrunUI.Section(new Rect(r.x, y + 4f, r.width, 36f), "NR_ReportEvents".Translate());
            Text.Font = GameFont.Tiny;
            foreach (string e in d.Events)
            {
                float h = Text.CalcHeight("• " + e, r.width);
                Widgets.Label(new Rect(r.x, y, r.width, h), "• " + e);
                y += h + 2f;
            }
            Text.Font = GameFont.Small;
        }

        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(r.x, r.yMax - 30f, r.width - 120f, 30f), "NR_ReportSettingHint".Translate().Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        string close = "CloseButton".Translate();
        float cw = NetrunUI.ButtonWidth(close);
        if (NetrunUI.Button(new Rect(r.xMax - cw, r.yMax - 34f, cw, 34f), close, NetrunUI.Accent, true))
            Close();
    }

    private static float Line(Rect r, float y, string label, string value, Color color)
    {
        Widgets.Label(new Rect(r.x, y, 180f, 22f), label.Colorize(NetrunUI.Muted));
        Widgets.Label(new Rect(r.x + 180f, y, r.width - 180f, 22f), value.Colorize(color));
        return y + 24f;
    }
}
