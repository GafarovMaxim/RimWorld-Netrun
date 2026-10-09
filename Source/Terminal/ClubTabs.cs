using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Club v2 (docs/CONCEPTS_10.md, section 12b; mockup 29-club-v2): the social node of the terminal. Tabs: fixers (as
/// before), corporations, runner crews, and the net's top 100.
/// </summary>
public static class ClubTabs
{
    private enum Tab
    {
        Fixers,
        Corps,
        Crews,
        Top,
    }

    private static Tab _tab;
    private static Vector2 _scroll;
    private static int _topFilter;

    public static void Draw(Rect rect, Map map, ref Vector2 fixersScroll)
    {
        float x = rect.x;
        foreach (Tab t in System.Enum.GetValues(typeof(Tab)))
        {
            string label = ("NR_ClubTab_" + t).Translate();
            float w = NetrunUI.ButtonWidth(label);
            if (NetrunUI.Button(new Rect(x, rect.y, w, 28f), label, t == _tab ? NetrunUI.Accent : NetrunUI.Muted, true))
                _tab = t;
            if (t == _tab)
                Widgets.DrawBoxSolid(new Rect(x, rect.y + 28f, w, 2f), NetrunUI.Accent);
            x += w + 6f;
        }
        var content = new Rect(rect.x, rect.y + 36f, rect.width, rect.height - 36f);
        switch (_tab)
        {
            case Tab.Fixers:
                FixersTab.Draw(content, map, ref fixersScroll);
                break;
            case Tab.Corps:
                DrawCorps(content);
                break;
            case Tab.Crews:
                DrawCrews(content, map);
                break;
            case Tab.Top:
                DrawTop(content);
                break;
        }
    }

    private static Texture2D IconOf(Faction f) => f?.def.FactionIcon;

    /// <summary>The faction's own hue, lifted so dark colours still read on the terminal's black.</summary>
    private static Color Lifted(Faction f)
    {
        Color.RGBToHSV(f.Color, out float h, out float s, out float v);
        return Color.HSVToRGB(h, Mathf.Min(s, 0.85f), Mathf.Max(v, 0.9f));
    }

    /// <summary>C7: the emblem in a framed tile with a soft glow in the faction's colour.</summary>
    private static void Emblem(Rect r, Faction f)
    {
        Color c = Lifted(f);
        NetrunUI.Glow(r, c, 0.25f, 10f);
        Widgets.DrawBoxSolid(r, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.95f));
        GUI.color = new Color(c.r, c.g, c.b, 0.6f);
        Widgets.DrawBox(r);
        if (IconOf(f) is { } icon)
        {
            GUI.color = c;
            GUI.DrawTexture(r.ContractedBy(8f), icon);
        }
        GUI.color = Color.white;
    }

    // ---- One card layout for corporations and crews (C7) ----

    private const float CardH = 150f, EmblemSize = 72f, BarsW = 230f;

    /// <summary>Header of a card: emblem, name in the faction's colour, a muted subtitle, chips under it. Returns the text column's x.</summary>
    private static float CardHead(Rect card, Faction f, string subtitle, List<(string text, Color color, string tip)> chips)
    {
        Color c = Lifted(f);
        NetrunUI.Card(card, c);
        Emblem(new Rect(card.x + 14f, card.y + 14f, EmblemSize, EmblemSize), f);
        float tx = card.x + 14f + EmblemSize + 14f;
        float tw = card.xMax - BarsW - 24f - tx;
        Text.Font = GameFont.Medium;
        GUI.color = c;
        Widgets.Label(new Rect(tx, card.y + 10f, tw, 30f), f.Name.Truncate(tw));
        GUI.color = NetrunUI.Muted;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(tx, card.y + 38f, tw, 18f), subtitle.Truncate(tw));
        GUI.color = Color.white;
        float cx = tx, cy = card.y + 60f;
        foreach ((string text, Color color, string tip) in chips)
        {
            Text.Font = GameFont.Tiny;
            float w = NetrunUI.Width(text) + 12f;
            if (cx + w > tx + tw)
            {
                cx = tx;
                cy += 22f;
            }
            cx += NetrunUI.Chip(cx, cy, text, color, tip) + 6f;
        }
        Text.Font = GameFont.Small;
        return tx;
    }

    /// <summary>A labelled bar in the card's right block: the label left, the value right, the bar under them.</summary>
    private static void CardBar(Rect card, int index, string label, string value, float fill, Color color, string tip)
    {
        var r = new Rect(card.xMax - BarsW - 14f, card.y + 14f + index * 34f, BarsW, 30f);
        Text.Font = GameFont.Tiny;
        GUI.color = NetrunUI.Muted;
        Widgets.Label(new Rect(r.x, r.y, r.width, 16f), NetrunUI.Upper(label));
        Text.Anchor = TextAnchor.UpperRight;
        GUI.color = color;
        Widgets.Label(new Rect(r.x, r.y, r.width, 16f), value);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        // Ten segments, like the deck's scales.
        const int segs = 10;
        float sw = r.width / segs;
        for (int i = 0; i < segs; i++)
        {
            bool on = fill * segs > i + 0.25f;
            Widgets.DrawBoxSolid(new Rect(r.x + i * sw + 1f, r.y + 19f, sw - 2f, 7f), on ? color : new Color(color.r, color.g, color.b, 0.12f));
        }
        if (tip != null)
            NetrunUI.Tip(r, tip);
    }

    // ---- Corporations ----

    private static void DrawCorps(Rect rect)
    {
        CorpManager manager = CorpManager.Get();
        List<CorpState> corps = manager?.Corps.Where(c => c.Faction is { defeated: false }).ToList() ?? [];
        float viewH = corps.Count * (CardH + 10f) + 80f;
        var view = new Rect(0f, 0f, rect.width - 16f, viewH);
        NetrunUI.BeginScroll(rect, ref _scroll, view);
        float y = 0f;
        if (corps.Count == 0)
            y += NetrunUI.EmptyState(new Rect(0f, y, view.width, 40f), NetrunUI.T("NR_StateQuiet"), NetrunUI.T("NR_ClubNoCorps"), NetrunUI.Warn, center: false);
        NetMapManager net = NetMapManager.Get();
        foreach (CorpState c in corps)
        {
            Faction f = c.Faction;
            var card = new Rect(0f, y, view.width, CardH);
            int owned = net?.Map?.Nodes.Count(n => n.OwnerFaction == c.FactionId) ?? 0;
            Color status = c.Alert >= 80f ? NetrunUI.Bad : c.Alert >= 40f ? NetrunUI.Warn : NetrunUI.Ok;
            float tx = CardHead(card, f, f.def.LabelCap, [
                (("NR_CorpStatus_" + c.Status).Translate(), status, null),
                ("NR_ClubCorpTerritory".Translate(owned), Lifted(f), null),
            ]);
            CardBar(card, 0, "NR_ClubCorpAlertLabel".Translate(), c.Alert.ToString("0"), c.Alert / 100f, status, "NR_ClubCorpBarsTip".Translate());
            CardBar(card, 1, "NR_ClubCorpStrengthLabel".Translate(), c.Strength.ToString("0"), c.Strength / 100f, NetrunUI.Accent, "NR_ClubCorpBarsTip".Translate());

            MapNode fortress = net?.Map?.Nodes.FirstOrDefault(n => n.Fortress && n.OwnerFaction == c.FactionId);
            if (NetrunUI.Button(new Rect(tx, card.yMax - 40f, 140f, 30f), "NR_ClubOnMap".Translate(), NetrunUI.Accent,
                    fortress != null ? true : (AcceptanceReport)"NR_MapNotReady".Translate()))
                Find.WindowStack.WindowOfType<Dialog_NetTerminal>()?.OpenGatewayAt(fortress.Id);
            IntrusionPacket packet = IntrusionManager.Get()?.Packets.FirstOrDefault(p => p.SourceFaction == c.FactionId);
            if (packet != null)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = NetrunUI.Bad;
                Widgets.Label(new Rect(tx + 150f, card.yMax - 34f, card.xMax - tx - 160f, 18f),
                    "NR_ClubCorpIntrusion".Translate(packet.Attack?.label ?? "?", (packet.Path.Count * 2f).ToString("0")));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
            y += CardH + 10f;
        }
        Text.Font = GameFont.Tiny;
        GUI.color = NetrunUI.Muted;
        Widgets.Label(new Rect(0f, y, view.width, 60f), "NR_ClubCorpsHelp".Translate());
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        Widgets.EndScrollView();
    }

    // ---- Crews ----

    private static void DrawCrews(Rect rect, Map map)
    {
        CrewSim sim = CrewSim.Get();
        sim?.Sync();
        List<Faction> crews = NetFactions.Crews.ToList();
        NetRanking ranking = NetRanking.Get();
        var view = new Rect(0f, 0f, rect.width - 16f, crews.Count * (CardH + 10f) + 60f);
        NetrunUI.BeginScroll(rect, ref _scroll, view);
        float y = 0f;
        GatewayManager gateway = GatewayManager.Get();
        NetMapManager net = NetMapManager.Get();
        NetRunner runner = net?.Runners.FirstOrDefault(r => !r.Busy && !r.Moving);
        float maxFame = ranking?.Sorted.Count > 0 ? ranking.Sorted[0].Fame : 1f;
        foreach (Faction f in crews)
        {
            CrewState s = sim?.StateOf(f);
            var card = new Rect(0f, y, view.width, CardH);
            RankEntry entry = ranking?.CrewEntry(f);
            int place = entry == null ? 0 : ranking.PlaceOf(entry);
            bool friendly = !f.HostileTo(Faction.OfPlayer);
            string subtitle = s?.LastAction != null ? (string)("NR_ClubCrewLast".Translate() + " " + s.LastAction) : (string)f.def.LabelCap;
            float tx = CardHead(card, f, subtitle, [
                (("NR_CrewFocus_" + (s?.Focus ?? CrewFocus.Markets)).Translate(), Lifted(f), null),
                ("NR_ClubCrewSkill".Translate(s?.Skill ?? 0), NetrunUI.Accent, null),
                (place > 0 ? "#" + place : "—", place is > 0 and <= 10 ? NetrunUI.Warn : NetrunUI.Muted, NetRanking.Title(place)),
            ]);
            int goodwill = f.PlayerGoodwill;
            CardBar(card, 0, "NR_ClubCrewRelationLabel".Translate(), goodwill.ToStringWithSign(), (goodwill + 100f) / 200f,
                goodwill >= 0 ? NetrunUI.Ok : NetrunUI.Bad, null);
            CardBar(card, 1, "NR_ClubCrewFameLabel".Translate(), (entry?.Fame ?? 0f).ToString("0"), Mathf.Clamp01((entry?.Fame ?? 0f) / Mathf.Max(1f, maxFame)),
                NetrunUI.Purple, null);

            float bx = tx, by = card.yMax - 40f;
            if (friendly)
            {
                string call = "NR_ClubCrewCall".Translate();
                float w = NetrunUI.ButtonWidth(call);
                AcceptanceReport can = NetFactions.BestCrew() == f ? gateway?.CanCallAllies() ?? false : (AcceptanceReport)"NR_ClubCrewNotBest".Translate();
                if (NetrunUI.Button(new Rect(bx, by, w, 30f), call, NetrunUI.Purple, can, "NR_ClubCallAlliesTip".Translate(NetrunDefOf.NR_Cyberspace.groupFavorForAllies, NetrunDefOf.NR_Cyberspace.alliesCooldownDays)))
                    gateway.CallAllies(map);
                bx += w + 6f;
                string hire = "NR_ClubCrewHire".Translate();
                w = NetrunUI.ButtonWidth(hire);
                if (NetrunUI.Button(new Rect(bx, by, w, 30f), hire, NetrunUI.Accent, CrewDeals.CanHire(f, runner), "NR_ClubCrewHireTip".Translate(CrewDeals.HireHours)))
                    CrewDeals.Hire(f, runner);
                bx += w + 6f;
                string race = "NR_ClubCrewRace".Translate();
                w = NetrunUI.ButtonWidth(race);
                if (NetrunUI.Button(new Rect(bx, by, w, 30f), race, NetrunUI.Warn, CrewDeals.CanRace(f), "NR_ClubCrewRaceTip".Translate()))
                    CrewDeals.Race(f);
                bx += w + 6f;
            }
            string order = "NR_ClubCrewOrder".Translate(CrewDeals.OrderSilver);
            float ow = NetrunUI.ButtonWidth(order);
            if (NetrunUI.Button(new Rect(bx, by, ow, 30f), order, NetrunUI.Bad, CrewDeals.CanOrder(map, f), "NR_ClubCrewOrderTip".Translate()))
                CrewDeals.Order(map, f);
            y += CardH + 10f;
        }
        Widgets.EndScrollView();
    }

    // ---- Top 100 ----

    private static readonly Color Gold = NetrunUI.Hex(0xffd34a), Silver = NetrunUI.Hex(0xc9d1d9), Bronze = NetrunUI.Hex(0xd08a4a);
    private const string TopRowsKey = "club.topRows";

    private static void DrawTop(Rect rect)
    {
        NetRanking ranking = NetRanking.Get();
        if (ranking == null)
            return;
        List<RankEntry> all = ranking.Sorted;
        string[] filters = ["NR_ClubTopAll".Translate(), "NR_ClubTopCrews".Translate(), "NR_ClubTopYours".Translate()];
        float fx = rect.x;
        for (int i = 0; i < filters.Length; i++)
        {
            float w = NetrunUI.ButtonWidth(filters[i]);
            if (NetrunUI.Button(new Rect(fx, rect.y, w, 26f), filters[i], i == _topFilter ? NetrunUI.Ok : NetrunUI.Muted, true))
                _topFilter = i;
            fx += w + 6f;
        }
        int best = ranking.BestColonyPlace;
        string goal = best <= 1 ? (string)"NR_ClubTopFirst".Translate()
            : best > NetRanking.Size ? (string)"NR_ClubTopNotIn".Translate((all.Count > 0 ? all[all.Count - 1].Fame : 0f).ToString("0"))
            : (string)"NR_ClubTopNext".Translate(best - 1, (all[best - 2].Fame - all[best - 1].Fame + 1f).ToString("0"));
        Text.Font = GameFont.Tiny;
        GUI.color = NetrunUI.Accent;
        Widgets.Label(new Rect(fx + 12f, rect.y + 4f, rect.xMax - fx - 12f, 20f), goal);
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        // Column heads.
        var head = new Rect(rect.x, rect.y + 34f, rect.width - 16f, 20f);
        float nameX = head.x + 64f, kindX = head.x + head.width * 0.5f, fameX = head.xMax - 250f, trendX = head.xMax - 70f;
        Text.Font = GameFont.Tiny;
        GUI.color = NetrunUI.Muted;
        Widgets.Label(new Rect(head.x + 8f, head.y, 50f, 20f), "#");
        Widgets.Label(new Rect(nameX, head.y, kindX - nameX, 20f), NetrunUI.Upper(NetrunUI.T("NR_ClubTopColName")));
        Widgets.Label(new Rect(kindX, head.y, 120f, 20f), NetrunUI.Upper(NetrunUI.T("NR_ClubTopColKind")));
        Widgets.Label(new Rect(fameX, head.y, 170f, 20f), NetrunUI.Upper(NetrunUI.T("NR_ClubTopColFame")));
        Widgets.Label(new Rect(trendX, head.y, 66f, 20f), NetrunUI.Upper(NetrunUI.T("NR_ClubTopColWeek")));
        GUI.color = Color.white;
        Widgets.DrawBoxSolid(new Rect(head.x, head.yMax, head.width, 1f), new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.35f));

        var list = new Rect(rect.x, head.yMax + 3f, rect.width, rect.yMax - head.yMax - 3f - 60f);
        int filter = _topFilter;
        List<(RankEntry e, int place)> rows = NetrunUI.Cached(TopRowsKey + filter, () => all.Select((e, i) => (e, i + 1))
            .Where(r => filter == 0 || (filter == 1 ? r.e.Kind == RankKind.Crew : r.e.Kind == RankKind.Colony)).ToList(), 1f);
        float maxFame = all.Count > 0 ? Mathf.Max(1f, all[0].Fame) : 1f;
        var view = new Rect(0f, 0f, list.width - 16f, rows.Count * 26f);
        NetrunUI.BeginScroll(list, ref _scroll, view);
        Rect visible = NetrunUI.ClipRect();
        float y = 0f;
        float shift = list.x - head.x;
        foreach ((RankEntry e, int place) in rows)
        {
            var row = new Rect(0f, y, view.width, 24f);
            y += 26f;
            if (visible.width > 0f && (row.yMax < visible.y || row.y > visible.yMax))
                continue;
            Color c = e.Kind == RankKind.Colony ? NetrunUI.Ok : e.Kind == RankKind.Crew ? NetrunUI.Purple : NetrunUI.TextColor;
            if (e.Kind == RankKind.Colony)
            {
                Widgets.DrawBoxSolid(row, new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.12f));
                Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), NetrunUI.Ok);
            }
            else if (place % 2 == 0)
                Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.03f));
            if (Mouse.IsOver(row))
                Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.05f));
            // The top three wear their medal colours.
            Color medal = place == 1 ? Gold : place == 2 ? Silver : place == 3 ? Bronze : NetrunUI.Muted;
            if (place <= 3)
                Widgets.DrawBoxSolid(new Rect(row.x + 6f, row.y + 4f, 36f, row.height - 8f), new Color(medal.r, medal.g, medal.b, 0.18f));
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = medal;
            Widgets.Label(new Rect(row.x + 6f, row.y, 36f, row.height), "#" + place);
            Text.Anchor = TextAnchor.MiddleLeft;
            // A crew wears its faction's colour dot.
            float nx = nameX - head.x + shift;
            if (e.Kind == RankKind.Crew && Find.FactionManager.AllFactionsListForReading.FirstOrDefault(fa => fa.loadID == e.RefId) is { } crew)
            {
                GUI.color = Lifted(crew);
                GUI.DrawTexture(new Rect(nx - 14f, row.center.y - 4f, 8f, 8f), NetrunUI.Disc);
            }
            GUI.color = c;
            Widgets.Label(new Rect(nx, row.y, kindX - nameX - 8f, row.height), e.Callsign);
            GUI.color = NetrunUI.Muted;
            Widgets.Label(new Rect(kindX - head.x + shift, row.y, 120f, row.height), ("NR_RankKind_" + e.Kind).Translate());
            // Fame: the number and a thin bar against the leader.
            float fx2 = fameX - head.x + shift;
            GUI.color = NetrunUI.TextColor;
            Widgets.Label(new Rect(fx2, row.y, 60f, row.height), e.Fame.ToString("0"));
            Widgets.DrawBoxSolid(new Rect(fx2 + 60f, row.center.y - 2f, 100f, 4f), new Color(c.r, c.g, c.b, 0.12f));
            Widgets.DrawBoxSolid(new Rect(fx2 + 60f, row.center.y - 2f, 100f * Mathf.Clamp01(e.Fame / maxFame), 4f), new Color(c.r, c.g, c.b, 0.7f));
            float trend = e.Trend;
            GUI.color = trend > 0.5f ? NetrunUI.Ok : trend < -0.5f ? NetrunUI.Bad : NetrunUI.Muted;
            Widgets.Label(new Rect(trendX - head.x + shift, row.y, 66f, row.height),
                trend > 0.5f ? "▲ " + trend.ToString("0") : trend < -0.5f ? "▼ " + (-trend).ToString("0") : "·");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            if (Mouse.IsOver(row))
                NetrunUI.Tip(row, NetRanking.Title(place));
        }
        Text.Font = GameFont.Small;
        Widgets.EndScrollView();

        Text.Font = GameFont.Tiny;
        GUI.color = NetrunUI.Muted;
        Widgets.Label(new Rect(rect.x, rect.yMax - 56f, rect.width, 56f), "NR_ClubTopHelp".Translate());
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }
}
