using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Netrun;

/// <summary>
/// The Studio node (braindance, mockup 17): tabs for the summary, raw recordings waiting for editing, the library
/// (unique recordings found and "???" for the rest, chips in storage) and who is hooked. A panel on the right explains
/// how unique recordings drop and shows the one picked.
/// </summary>
public static class StudioTab
{
    private enum Tab
    {
        Summary,
        Raw,
        Library,
        Addiction,
    }

    private const float RowHeight = 40f;
    private const float SectionHeight = 36f;

    private static Tab _tab = Tab.Summary;
    private static ThingDef _picked;
    private static float _tabLineX = -1f, _tabLineW;

    private static readonly Color Gold = NetrunUI.Hex(0xf0b840);
    private static readonly Color StudioColor = new(1f, 0.45f, 0.35f);

    public static void Draw(Rect rect, Map map, ref Vector2 scroll)
    {
        // Gathered about once a second, not on every GUI event.
        (List<Thing> raws, List<Thing> chips, List<Pawn> hooked) = NetrunUI.Cached("studio.data." + map.uniqueID, () => (
            BraindanceEditing.RawRecordings(map).ToList(),
            // Only chip defs, not every haulable thing on the map.
            map.listerThings.ThingsOfDef(NetrunDefOf.NR_BraindanceChip)
                .Concat(map.listerThings.ThingsOfDef(NetrunDefOf.NR_BlackBraindanceChip))
                .Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer)).ToList(),
            map.mapPawns.FreeColonistsSpawned
                .Where(p => p.health.hediffSet.hediffs.Any(h => h is Hediff_Addiction a && a.Chemical == NetrunDefOf.NR_Braindance)
                            || p.health.hediffSet.HasHediff(NetrunDefOf.NR_BraindanceTolerance)).ToList()), 1f);

        GuideRegistry.Area("studio.tabs", new Rect(rect.x, rect.y, rect.width * 0.6f, 32f));
        DrawTabs(new Rect(rect.x, rect.y, rect.width, 32f), raws.Count, hooked.Count);
        rect.yMin += 42f;

        float panelW = Mathf.Min(330f, rect.width * 0.36f);
        var left = new Rect(rect.x, rect.y, rect.width - panelW - 12f, rect.height);
        var right = new Rect(left.xMax + 12f, rect.y, panelW, rect.height);
        switch (_tab)
        {
            case Tab.Summary:
                DrawSummary(left, map, raws, chips, hooked);
                DrawGallery(right, map, chips);
                break;
            case Tab.Raw:
                GuideRegistry.Area("studio.records", left);
                DrawList(left, ref scroll, "NR_StudioRecordings".Translate(raws.Count), raws.Count, "NR_StudioNoRecordings".Translate(),
                    (y, w) => { float yy = y; foreach (Thing raw in raws) yy = DrawRecording(new Rect(0f, yy, w, RowHeight), raw); return yy; });
                DrawInfo(right, "NR_StudioInfoRawTitle", "NR_StudioInfoRaw", StudioColor);
                break;
            case Tab.Library:
                GuideRegistry.Area("studio.library", left);
                DrawLibrary(left, map, chips, ref scroll);
                DrawPanel(right);
                break;
            case Tab.Addiction:
                GuideRegistry.Area("studio.hooked", left);
                DrawList(left, ref scroll, "NR_StudioHooked".Translate(hooked.Count), hooked.Count, "NR_StudioNoneHooked".Translate(),
                    (y, w) => { float yy = y; foreach (Pawn pawn in hooked) yy = DrawHooked(new Rect(0f, yy, w, RowHeight), pawn); return yy; });
                DrawInfo(right, "NR_StudioInfoHookTitle", "NR_StudioInfoHook", NetrunUI.Bad);
                break;
        }
    }

    // ---- Tabs: a sliding line under the active one ----

    private static void DrawTabs(Rect rect, int raws, int hooked)
    {
        int found = UniqueBraindanceManager.Get()?.Found.Count ?? 0;
        int total = UniqueBraindance.All.Count();
        float x = rect.x;
        float activeX = 0f, activeW = 0f;
        foreach (Tab tab in System.Enum.GetValues(typeof(Tab)))
        {
            string label = ("NR_StudioTab_" + tab).Translate();
            string count = tab switch
            {
                Tab.Raw => raws.ToString(),
                Tab.Library => $"{found}/{total}",
                Tab.Addiction => hooked.ToString(),
                _ => null,
            };
            float w = Text.CalcSize(label).x + (count != null ? Text.CalcSize(count).x + 12f : 0f) + 26f;
            var r = new Rect(x, rect.y, w, rect.height);
            bool active = _tab == tab;
            float lit = NetrunUI.HoverFade(r);
            Color c = tab == Tab.Library ? Gold : StudioColor;
            NetrunUI.Box(r, active ? NetrunUI.BgCardAlt : Color.Lerp(NetrunUI.BgCard, NetrunUI.BgCardAlt, lit),
                active ? new Color(c.r, c.g, c.b, 0.8f) : new Color(1f, 1f, 1f, 0.12f + 0.2f * lit));
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(r.x + 12f, r.y, w, r.height), active ? label : label.Colorize(NetrunUI.Muted));
            if (count != null)
                Widgets.Label(new Rect(r.xMax - Text.CalcSize(count).x - 10f, r.y, 60f, r.height), count.Colorize(active ? c : NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            if (active)
            {
                activeX = r.x;
                activeW = r.width;
            }
            if (Widgets.ButtonInvisible(r) && !active)
            {
                NetrunSound.Click();
                _tab = tab;
            }
            x += w + 6f;
        }
        if (_tabLineX < 0f || !NetrunMod.Settings.Animations)
        {
            _tabLineX = activeX;
            _tabLineW = activeW;
        }
        else
        {
            float k = Mathf.Clamp01(Time.unscaledDeltaTime * 14f);
            _tabLineX = Mathf.Lerp(_tabLineX, activeX, k);
            _tabLineW = Mathf.Lerp(_tabLineW, activeW, k);
        }
        Widgets.DrawBoxSolid(new Rect(_tabLineX, rect.yMax - 2f, _tabLineW, 2f), _tab == Tab.Library ? Gold : StudioColor);
    }

    // ---- Summary: four tiles and what to do next ----

    private static void DrawSummary(Rect rect, Map map, List<Thing> raws, List<Thing> chips, List<Pawn> hooked)
    {
        GuideRegistry.Area("studio.summary", rect);
        int goggles = map.listerThings.ThingsOfDef(NetrunDefOf.NR_BraindanceGoggles).Count
                      + map.mapPawns.FreeColonistsSpawned.Count(BraindanceUtility.HasGoggles);
        int black = chips.Where(c => c.def == NetrunDefOf.NR_BlackBraindanceChip).Sum(c => c.stackCount);
        int found = UniqueBraindanceManager.Get()?.Found.Count ?? 0;
        int total = UniqueBraindance.All.Count();

        float y = NetrunUI.Section(new Rect(rect.x, rect.y, rect.width, SectionHeight), "NR_StudioTab_Summary".Translate());
        float tw = (rect.width - 18f) / 4f;
        Tile(new Rect(rect.x, y, tw, 64f), "NR_StudioTileGoggles".Translate(), goggles.ToString(), StudioColor, Tab.Summary);
        Tile(new Rect(rect.x + (tw + 6f), y, tw, 64f), "NR_StudioTileRaw".Translate(), raws.Count.ToString(), NetrunUI.Accent, Tab.Raw);
        Tile(new Rect(rect.x + 2 * (tw + 6f), y, tw, 64f), "NR_StudioTileChips".Translate(), $"{chips.Sum(c => c.stackCount)} ({black})", NetrunUI.Purple, Tab.Library);
        Tile(new Rect(rect.x + 3 * (tw + 6f), y, tw, 64f), "NR_StudioTileUnique".Translate(), $"{found}/{total}", Gold, Tab.Library);
        y += 76f;

        string[] lines =
        [
            raws.Count > 0 ? (string)"NR_StudioHintRaw".Translate(raws.Count) : null,
            hooked.Count > 0 ? (string)"NR_StudioHintHooked".Translate(hooked.Count) : null,
            goggles == 0 ? (string)"NR_StudioHintGoggles".Translate() : null,
            found < total ? (string)"NR_StudioHintUnique".Translate(total - found) : null,
        ];
        foreach (string line in lines.Where(l => l != null))
        {
            float h = Text.CalcHeight(line, rect.width - 24f) + 10f;
            var row = new Rect(rect.x, y, rect.width, h);
            Widgets.DrawBoxSolid(row, NetrunUI.BgCard);
            Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), StudioColor);
            Widgets.Label(new Rect(row.x + 12f, row.y + 5f, row.width - 18f, h), "> " + line);
            y += h + 4f;
        }
    }

    /// <summary>Summary's right panel: the unique recordings as a small gallery and the chips by mood.</summary>
    private static void DrawGallery(Rect rect, Map map, List<Thing> chips)
    {
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(1f, 1f, 1f, 0.12f));
        Rect inner = rect.ContractedBy(12f);
        float y = Header(inner, inner.y, "NR_StudioGallery".Translate());
        UniqueBraindanceManager ub = UniqueBraindanceManager.Get();
        List<ThingDef> all = UniqueBraindance.All.ToList();
        float cell = Mathf.Min(60f, (inner.width - 3 * 8f) / 4f);
        for (int i = 0; i < all.Count; i++)
        {
            var r = new Rect(inner.x + i % 4 * (cell + 8f), y + i / 4 * (cell + 8f), cell, cell);
            bool found = ub?.IsFound(all[i]) == true;
            NetrunUI.Box(r, NetrunUI.BgWindow, found ? Gold : new Color(1f, 1f, 1f, 0.15f), found ? 2 : 1);
            if (found)
            {
                GUI.DrawTexture(r.ContractedBy(6f), all[i].uiIcon);
                NetrunUI.Tip(r, all[i].LabelCap + "\n" + ("NR_UB_Good_" + UniqueBraindance.Ext(all[i]).effect).Translate());
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(r, "?".Colorize(NetrunUI.Muted));
                Text.Anchor = TextAnchor.UpperLeft;
            }
            if (Widgets.ButtonInvisible(r))
            {
                _tab = Tab.Library;
                _picked = found ? all[i] : null;
            }
        }
        y += Mathf.CeilToInt(all.Count / 4f) * (cell + 8f) + 10f;

        y = Header(inner, y, "NR_StudioChipsByMood".Translate());
        Text.Font = GameFont.Tiny;
        var byMood = chips.Select(c => c.TryGetComp<CompBraindanceRecord>()).Where(c => c != null)
            .GroupBy(c => c.Mood).ToDictionary(g => g.Key, g => g.Count());
        int total = Mathf.Max(1, byMood.Values.Sum());
        foreach (BraindanceMood mood in System.Enum.GetValues(typeof(BraindanceMood)))
        {
            int n = byMood.TryGetValue(mood, out int v) ? v : 0;
            Widgets.Label(new Rect(inner.x, y, 90f, 18f), ((string)("NR_BDMood_" + mood).Translate()).CapitalizeFirst().Colorize(MoodColor(mood)));
            NetrunUI.Bar(new Rect(inner.x + 94f, y + 5f, inner.width - 130f, 8f), n / (float)total, MoodColor(mood));
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inner.xMax - 32f, y, 32f, 18f), n.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
            y += 22f;
        }
        Text.Font = GameFont.Small;
    }

    /// <summary>A right-hand panel with a title and a few lines of explanation.</summary>
    private static void DrawInfo(Rect rect, string titleKey, string textKey, Color color)
    {
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(1f, 1f, 1f, 0.12f));
        Rect inner = rect.ContractedBy(12f);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), ((string)titleKey.Translate()).ToUpper().Colorize(color));
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = new Color(color.r, color.g, color.b, 0.6f);
        Widgets.DrawLineHorizontal(inner.x, inner.y + 26f, inner.width);
        GUI.color = Color.white;
        float y = inner.y + 34f;
        Text.Font = GameFont.Small;
        foreach (string line in ((string)textKey.Translate()).Split('\n'))
        {
            if (line.Trim().Length == 0)
            {
                y += 6f;
                continue;
            }
            float h = Text.CalcHeight("• " + line, inner.width);
            Widgets.Label(new Rect(inner.x, y, inner.width, h), ("• " + line).Colorize(NetrunUI.Muted));
            y += h + 4f;
        }
    }

    private static void Tile(Rect r, string label, string value, Color c, Tab go)
    {
        float lit = NetrunUI.HoverFade(r);
        Widgets.DrawBoxSolid(r, Color.Lerp(NetrunUI.BgCard, NetrunUI.BgCardAlt, lit));
        Widgets.DrawBoxSolid(new Rect(r.x, r.yMax - 2f, r.width, 2f), c);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(r.x + 8f, r.y + 6f, r.width - 12f, 16f), label.ToUpper().Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(r.x + 8f, r.y + 24f, r.width - 12f, 32f), value.Colorize(c));
        Text.Font = GameFont.Small;
        if (go != Tab.Summary && Widgets.ButtonInvisible(r))
        {
            NetrunSound.Click();
            _tab = go;
        }
    }

    private static void DrawList(Rect rect, ref Vector2 scroll, string title, int count, string empty, System.Func<float, float, float> rows)
    {
        float width = rect.width - 16f;
        var view = new Rect(0f, 0f, width, SectionHeight + Mathf.Max(1, count) * (RowHeight + 4f) + 10f);
        NetrunUI.BeginScroll(rect, ref scroll, view);
        float y = NetrunUI.Section(new Rect(0f, 0f, width, SectionHeight), title);
        if (count == 0)
            Widgets.Label(new Rect(0f, y, width, RowHeight), NetrunUI.Prompt(empty).Colorize(NetrunUI.Muted));
        else
            rows(y, width);
        Widgets.EndScrollView();
    }

    // ---- Library: unique recordings, then chips in storage ----

    private static void DrawLibrary(Rect rect, Map map, List<Thing> chips, ref Vector2 scroll)
    {
        List<ThingDef> all = UniqueBraindance.All.ToList();
        UniqueBraindanceManager ub = UniqueBraindanceManager.Get();
        float width = rect.width - 16f;
        float height = SectionHeight + all.Count * (78f + 6f) + 16f + SectionHeight + Mathf.Max(1, chips.Count) * (RowHeight + 4f) + 10f;
        var view = new Rect(0f, 0f, width, height);
        NetrunUI.BeginScroll(rect, ref scroll, view);
        float y = 0f;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(0f, y, width, 26f), ((string)"NR_StudioUniqueHeader".Translate(ub?.Found.Count ?? 0, all.Count)).ToUpper().Colorize(Gold));
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = new Color(Gold.r, Gold.g, Gold.b, 0.6f);
        Widgets.DrawLineHorizontal(0f, y + 28f, width);
        GUI.color = Color.white;
        y += 36f;
        foreach (ThingDef def in all)
            y = UniqueRow(new Rect(0f, y, width, 78f), def, ub?.IsFound(def) == true, map) + 6f;

        y = NetrunUI.Section(new Rect(0f, y + 10f, width, SectionHeight), "NR_StudioLibrary".Translate(chips.Sum(c => c.stackCount)));
        if (chips.Count == 0)
            Widgets.Label(new Rect(0f, y, width, RowHeight), NetrunUI.Prompt("NR_StudioNoChips".Translate()).Colorize(NetrunUI.Muted));
        foreach (Thing chip in chips)
            y = DrawChip(new Rect(0f, y, width, RowHeight), chip);
        Widgets.EndScrollView();
    }

    private static float UniqueRow(Rect r, ThingDef def, bool found, Map map)
    {
        UniqueBraindanceExtension ext = UniqueBraindance.Ext(def);
        bool picked = _picked == def;
        float lit = NetrunUI.HoverFade(r, 0.12f);
        Color edge = !found ? NetrunUI.Bad : ext.black ? NetrunUI.Purple : Gold;
        Widgets.DrawBoxSolid(r, Color.Lerp(found ? new Color(0.16f, 0.15f, 0.11f) : NetrunUI.BgCard, NetrunUI.BgCardAlt, lit * 0.5f));
        Widgets.DrawBoxSolid(new Rect(r.x, r.y, 4f, r.height), found ? Gold : new Color(NetrunUI.Bad.r, NetrunUI.Bad.g, NetrunUI.Bad.b, 0.6f));
        GUI.color = new Color(edge.r, edge.g, edge.b, picked ? 0.9f : 0.35f);
        Widgets.DrawBox(r, picked ? 2 : 1);
        GUI.color = Color.white;

        var icon = new Rect(r.x + 14f, r.y + 11f, 56f, 56f);
        NetrunUI.Box(icon, NetrunUI.BgWindow, new Color(edge.r, edge.g, edge.b, found ? 1f : 0.35f), 2);
        if (found)
            GUI.DrawTexture(icon.ContractedBy(5f), def.uiIcon);
        // A gold star on the corner: these are the rare ones.
        Widgets.Label(new Rect(icon.xMax - 10f, icon.y - 8f, 20f, 20f), "★".Colorize(found ? Gold : new Color(Gold.r, Gold.g, Gold.b, 0.4f)));

        float x = icon.xMax + 14f;
        float w = r.xMax - x - 10f;
        if (!found)
        {
            Widgets.Label(new Rect(x, r.y + 12f, w, 24f), ("??? · " + "NR_StudioUniqueMissing".Translate()).Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(x, r.y + 38f, w, 32f), "NR_StudioUniqueWhere".Translate().Colorize(new Color(1f, 1f, 1f, 0.35f)));
            Text.Font = GameFont.Small;
            return r.yMax;
        }

        Widgets.Label(new Rect(x, r.y + 6f, w, 24f), ("«" + def.LabelCap + "»").Colorize(Gold));
        Text.Font = GameFont.Tiny;
        List<Thing> here = map.listerThings.ThingsOfDef(def);
        string viewsText = here.Count > 0
            ? (string)"NR_StudioUniqueViews".Translate(here.Max(t => t.TryGetComp<CompUniqueBraindance>()?.ViewsLeft ?? 0))
            : (string)"NR_StudioUniqueGone".Translate();
        string meta = "NR_StudioUniqueMeta".Translate(("NR_UB_Author_" + ext.effect).Translate(),
            (ext.black ? "NR_StudioUniqueBlack" : "NR_StudioUniqueNormal").Translate(), viewsText);
        Widgets.Label(new Rect(x, r.y + 28f, w, 16f), meta.Truncate(w).Colorize(NetrunUI.Muted));
        Widgets.Label(new Rect(x, r.y + 44f, w, 16f), ("▲ " + ("NR_UB_Good_" + ext.effect).Translate()).Truncate(w).Colorize(NetrunUI.Ok));
        string badKey = "NR_UB_Bad_" + ext.effect;
        if (badKey.CanTranslate())
            Widgets.Label(new Rect(x, r.y + 59f, w, 16f), ("▼ " + badKey.Translate()).Truncate(w).Colorize(NetrunUI.Bad));
        Text.Font = GameFont.Small;
        NetrunUI.Tip(r, HelpPageDef.StripModName(def.description));
        if (Widgets.ButtonInvisible(r))
        {
            NetrunSound.Click();
            _picked = picked ? null : def;
        }
        return r.yMax;
    }

    /// <summary>Right panel: how unique recordings drop, the one picked, the rules.</summary>
    private static void DrawPanel(Rect rect)
    {
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(1f, 1f, 1f, 0.12f));
        Rect inner = rect.ContractedBy(12f);
        float y = Header(inner, inner.y, "NR_StudioHowTheyDrop".Translate());
        UniqueBraindanceSettingsDef s = NetrunDefOf.NR_UniqueBraindance;
        Text.Font = GameFont.Tiny;
        foreach (string line in new[]
                 {
                     (string)"NR_StudioDropBoss".Translate(s.bossChance.ToStringPercent()),
                     (string)"NR_StudioDropDive".Translate(s.diveFindChance.ToStringPercent()),
                     (string)"NR_StudioDropMarket".Translate(s.blackMarketChance.ToStringPercent()),
                     (string)"NR_StudioDropContract".Translate(s.contractChance.ToStringPercent()),
                     (string)"NR_StudioDropOnce".Translate(),
                 })
        {
            float h = Text.CalcHeight("• " + line, inner.width);
            Widgets.Label(new Rect(inner.x, y, inner.width, h), ("• " + line).Colorize(NetrunUI.Muted));
            y += h + 2f;
        }
        Text.Font = GameFont.Small;
        y += 8f;

        y = Header(inner, y, "NR_StudioItem".Translate());
        if (_picked == null || UniqueBraindanceManager.Get()?.IsFound(_picked) != true)
        {
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inner.x, y, inner.width, 34f), NetrunUI.Prompt("NR_StudioPickHint".Translate()).Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
            y += 38f;
        }
        else
        {
            UniqueBraindanceExtension ext = UniqueBraindance.Ext(_picked);
            string badKey = "NR_UB_Bad_" + ext.effect;
            Text.Font = GameFont.Tiny;
            string desc = HelpPageDef.StripModName(_picked.description);
            float dh = Text.CalcHeight(desc, inner.width - 20f);
            string good = ("NR_UB_Good_" + ext.effect).Translate();
            float gh = Text.CalcHeight(good, inner.width - 20f);
            float bh = badKey.CanTranslate() ? Text.CalcHeight(badKey.Translate(), inner.width - 20f) : 0f;
            Text.Font = GameFont.Small;
            var card = new Rect(inner.x, y, inner.width, 8f + 24f + 18f + dh + 6f + gh + bh + 10f);
            NetrunUI.Glow(card, Gold, 0.35f, 8f);
            NetrunUI.Box(card, NetrunUI.BgWindow, Gold);
            Widgets.DrawBoxSolid(new Rect(card.x, card.y, 4f, card.height), Gold);
            float cy = card.y + 8f;
            Widgets.Label(new Rect(card.x + 12f, cy, card.width - 20f, 24f), ("«" + _picked.LabelCap + "»").Colorize(Gold));
            cy += 24f;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(card.x + 12f, cy, card.width - 20f, 16f),
                "NR_StudioUniqueKind".Translate((ext.black ? "NR_StudioUniqueBlack" : "NR_StudioUniqueNormal").Translate()).Colorize(NetrunUI.Muted));
            cy += 18f;
            Widgets.Label(new Rect(card.x + 12f, cy, card.width - 20f, dh), desc);
            cy += dh + 6f;
            Widgets.Label(new Rect(card.x + 12f, cy, card.width - 20f, gh), good.Colorize(NetrunUI.Ok));
            cy += gh;
            if (bh > 0f)
                Widgets.Label(new Rect(card.x + 12f, cy, card.width - 20f, bh), badKey.Translate().Colorize(NetrunUI.Bad));
            Text.Font = GameFont.Small;
            y = card.yMax + 12f;
        }

        y = Header(inner, y, "NR_StudioRules".Translate());
        Text.Font = GameFont.Tiny;
        string rules = "NR_StudioRulesText".Translate();
        Widgets.Label(new Rect(inner.x, y, inner.width, Text.CalcHeight(rules, inner.width)), rules.Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
    }

    private static float Header(Rect inner, float y, string label)
    {
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(inner.x, y, inner.width, 24f), label.ToUpper().Colorize(Gold));
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = new Color(Gold.r, Gold.g, Gold.b, 0.6f);
        Widgets.DrawLineHorizontal(inner.x, y + 26f, inner.width);
        GUI.color = Color.white;
        return y + 34f;
    }

    // ---- Rows (kept from the old Studio) ----

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

        // Right to left: the edit buttons, then the tags, then the text in the room that is left.
        float bx = row.xMax - 8f;
        foreach (ThingDef chip in rec == null ? Enumerable.Empty<ThingDef>() : BraindanceEditing.ChipsFor(rec.Mood))
        {
            string label = "NR_StudioEdit".Translate(chip.label);
            float w = NetrunUI.ButtonWidth(label);
            bx -= w;
            if (NetrunUI.Button(new Rect(bx, row.y + 5f, w, 30f), label, NetrunUI.Accent, true, "NR_StudioEditTip".Translate()))
                BraindanceEditing.OpenEditorMenu(raw, chip);
            bx -= 8f;
        }
        if (rec != null)
        {
            string mood = ((string)("NR_BDMood_" + rec.Mood).Translate()).ToUpper();
            string quality = ((string)("NR_BDQuality_" + rec.QualityTier).Translate()).ToUpper();
            Text.Font = GameFont.Tiny;
            float qw = Text.CalcSize(quality).x + 12f, mw = Text.CalcSize(mood).x + 12f;
            Text.Font = GameFont.Small;
            bx -= qw;
            NetrunUI.Chip(bx, row.y + 11f, quality, NetrunUI.Warn);
            bx -= mw + 6f;
            NetrunUI.Chip(bx, row.y + 11f, mood, color);
            bx -= 8f;
        }
        float x = row.x + 50f;
        Text.Anchor = TextAnchor.MiddleLeft;
        string text = rec == null ? raw.LabelCap : $"{rec.Author}: {rec.Event}";
        Widgets.Label(new Rect(x, row.y, Mathf.Max(0f, bx - x), row.height), text.Truncate(Mathf.Max(0f, bx - x)));
        Text.Anchor = TextAnchor.UpperLeft;
        NetrunUI.Tip(new Rect(x, row.y, Mathf.Max(0f, bx - x), row.height), text);
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
