using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The Studio node (braindance): raw recordings waiting for editing, the chip library in storage,
/// and who is hooked. Chips are sold to traders (exotic goods traders buy them), not here.
/// </summary>
public static class StudioTab
{
    private const float RowHeight = 40f;
    private const float SectionHeight = 36f;

    public static void Draw(Rect rect, Map map, ref Vector2 scroll)
    {
        List<Thing> raws = BraindanceEditing.RawRecordings(map).ToList();
        // Only the two chip defs, not every haulable thing on the map.
        List<Thing> chips = map.listerThings.ThingsOfDef(NetrunDefOf.NR_BraindanceChip)
            .Concat(map.listerThings.ThingsOfDef(NetrunDefOf.NR_BlackBraindanceChip))
            .Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer)).ToList();
        List<Pawn> hooked = map.mapPawns.FreeColonistsSpawned
            .Where(p => p.health.hediffSet.hediffs.Any(h => h is Hediff_Addiction a && a.Chemical == NetrunDefOf.NR_Braindance)
                        || p.health.hediffSet.HasHediff(NetrunDefOf.NR_BraindanceTolerance)).ToList();

        float width = rect.width - 16f;
        float height = 4 * SectionHeight + 60f
                       + (Mathf.Max(1, raws.Count) + Mathf.Max(1, chips.Count) + Mathf.Max(1, hooked.Count)) * (RowHeight + 4f);
        var view = new Rect(0f, 0f, width, height);

        Widgets.BeginScrollView(rect, ref scroll, view);
        float y = 0f;

        // Goggles and the colony's chip count at a glance.
        int goggles = map.listerThings.ThingsOfDef(NetrunDefOf.NR_BraindanceGoggles).Count
                      + map.mapPawns.FreeColonistsSpawned.Count(BraindanceUtility.HasGoggles);
        GuideRegistry.InScroll("studio.summary", rect, scroll, new Rect(0f, 0f, width, 26f));
        Widgets.Label(new Rect(0f, y, width, 24f), "NR_StudioSummary".Translate(goggles, chips.Sum(c => c.stackCount), raws.Count).Colorize(NetrunUI.Muted));
        y += 30f;

        float gRec = y;
        y = NetrunUI.Section(new Rect(0f, y, width, SectionHeight), "NR_StudioRecordings".Translate(raws.Count));
        if (raws.Count == 0)
            y = Empty(width, y, "NR_StudioNoRecordings".Translate());
        foreach (Thing raw in raws)
            y = DrawRecording(new Rect(0f, y, width, RowHeight), raw);

        GuideRegistry.InScroll("studio.records", rect, scroll, new Rect(0f, gRec, width, y - gRec));
        float gLib = y;
        y = NetrunUI.Section(new Rect(0f, y + 8f, width, SectionHeight), "NR_StudioLibrary".Translate(chips.Sum(c => c.stackCount)));
        if (chips.Count == 0)
            y = Empty(width, y, "NR_StudioNoChips".Translate());
        foreach (Thing chip in chips)
            y = DrawChip(new Rect(0f, y, width, RowHeight), chip);

        GuideRegistry.InScroll("studio.library", rect, scroll, new Rect(0f, gLib, width, y - gLib));
        float gHook = y;
        y = NetrunUI.Section(new Rect(0f, y + 8f, width, SectionHeight), "NR_StudioHooked".Translate(hooked.Count));
        if (hooked.Count == 0)
            y = Empty(width, y, "NR_StudioNoneHooked".Translate());
        foreach (Pawn pawn in hooked)
            y = DrawHooked(new Rect(0f, y, width, RowHeight), pawn);
        GuideRegistry.InScroll("studio.hooked", rect, scroll, new Rect(0f, gHook, width, y - gHook));

        Widgets.EndScrollView();
    }

    private static float Empty(float width, float y, string text)
    {
        Widgets.Label(new Rect(0f, y, width, RowHeight), text.Colorize(NetrunUI.Muted));
        return y + RowHeight + 4f;
    }

    private static Color MoodColor(BraindanceMood mood) => mood switch
    {
        BraindanceMood.Bright => NetrunUI.Ok,
        BraindanceMood.Dark => NetrunUI.Purple,
        _ => NetrunUI.Bad,
    };

    private static float DrawRecording(Rect row, Thing raw)
    {
        CompBraindanceRecord rec = raw.TryGetComp<CompBraindanceRecord>();
        Color color = rec != null ? MoodColor(rec.Mood) : NetrunUI.Muted;
        NetrunUI.Card(row, color);
        GUI.DrawTexture(new Rect(row.x + 10f, row.y + 4f, 32f, 32f), raw.def.uiIcon);

        float x = row.x + 50f;
        Text.Anchor = TextAnchor.MiddleLeft;
        string text = rec == null ? raw.LabelCap : $"{rec.Author}: {rec.Event}";
        Widgets.Label(new Rect(x, row.y, row.width * 0.45f, row.height), text.Truncate(row.width * 0.45f));
        Text.Anchor = TextAnchor.UpperLeft;
        if (rec != null)
        {
            float cx = x + row.width * 0.45f + 6f;
            cx += NetrunUI.Chip(cx, row.y + 11f, ((string)("NR_BDMood_" + rec.Mood).Translate()).ToUpper(), color) + 6f;
            NetrunUI.Chip(cx, row.y + 11f, ((string)("NR_BDQuality_" + rec.QualityTier).Translate()).ToUpper(), NetrunUI.Warn);
        }

        float bx = row.xMax - 8f;
        foreach (ThingDef chip in rec == null ? Enumerable.Empty<ThingDef>() : BraindanceEditing.ChipsFor(rec.Mood))
        {
            string label = "NR_StudioEdit".Translate(chip.label);
            float w = NetrunUI.ButtonWidth(label);
            bx -= w;
            if (NetrunUI.Button(new Rect(bx, row.y + 5f, w, 30f), label, NetrunUI.Accent, true, "NR_StudioEditTip".Translate()))
                BraindanceEditing.OpenEditorMenu(raw, chip);
            bx -= 6f;
        }
        return row.yMax + 4f;
    }

    private static float DrawChip(Rect row, Thing chip)
    {
        bool black = chip.def == NetrunDefOf.NR_BlackBraindanceChip;
        NetrunUI.Card(row, black ? NetrunUI.Bad : NetrunUI.Accent);
        GUI.DrawTexture(new Rect(row.x + 10f, row.y + 4f, 32f, 32f), chip.def.uiIcon);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(row.x + 50f, row.y, row.width * 0.55f, row.height), chip.LabelCap.Truncate(row.width * 0.55f));
        Text.Anchor = TextAnchor.UpperLeft;
        NetrunUI.Tip(new Rect(row.x, row.y, row.width * 0.6f, row.height), chip.GetInspectString());
        return row.yMax + 4f;
    }

    private static float DrawHooked(Rect row, Pawn pawn)
    {
        Hediff_Addiction addiction = pawn.health.hediffSet.hediffs.OfType<Hediff_Addiction>()
            .FirstOrDefault(a => a.Chemical == NetrunDefOf.NR_Braindance);
        Hediff tolerance = pawn.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_BraindanceTolerance);
        NetrunUI.Card(row, addiction != null ? NetrunUI.Bad : NetrunUI.Warn);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(row.x + 14f, row.y, 180f, row.height), pawn.LabelShort);
        string state = addiction != null
            ? "NR_StudioAddicted".Translate(addiction.Severity.ToStringPercent()).Colorize(NetrunUI.Bad)
            : "NR_StudioTolerance".Translate((tolerance?.Severity ?? 0f).ToStringPercent()).Colorize(NetrunUI.Warn);
        Widgets.Label(new Rect(row.x + 200f, row.y, row.width - 210f, row.height), state);
        Text.Anchor = TextAnchor.UpperLeft;
        return row.yMax + 4f;
    }
}
