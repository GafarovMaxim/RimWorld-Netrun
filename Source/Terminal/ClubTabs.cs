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

    private static void Emblem(Rect r, Faction f)
    {
        if (IconOf(f) is not { } icon)
            return;
        // The faction's own hue, lifted so dark colours still read on the terminal's black.
        Color.RGBToHSV(f.Color, out float h, out float s, out float v);
        GUI.color = Color.HSVToRGB(h, Mathf.Min(s, 0.85f), Mathf.Max(v, 0.9f));
        GUI.DrawTexture(r, icon);
        GUI.color = Color.white;
    }

    // ---- Corporations ----

    private static void DrawCorps(Rect rect)
    {
        CorpManager manager = CorpManager.Get();
        List<CorpState> corps = manager?.Corps.Where(c => c.Faction is { defeated: false }).ToList() ?? [];
        const float h = 132f;
        float viewH = corps.Count * (h + 10f) + 80f;
        var view = new Rect(0f, 0f, rect.width - 16f, viewH);
        NetrunUI.BeginScroll(rect, ref _scroll, view);
        float y = 0f;
        if (corps.Count == 0)
        {
            y += NetrunUI.EmptyState(new Rect(0f, y, view.width, 40f), NetrunUI.T("NR_StateQuiet"), NetrunUI.T("NR_ClubNoCorps"), NetrunUI.Warn, center: false);
        }
        NetMapManager net = NetMapManager.Get();
        foreach (CorpState c in corps)
        {
            Faction f = c.Faction;
            var card = new Rect(0f, y, view.width, h);
            NetrunUI.Card(card, f.Color);
            Emblem(new Rect(card.x + 10f, card.y + 10f, 64f, 64f), f);
            float tx = card.x + 86f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(tx, card.y + 6f, card.width - 300f, 30f), f.Name.Colorize(f.Color));
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(tx, card.y + 34f, card.width - 300f, 18f), (f.def.LabelCap + " · " + ("NR_CorpStatus_" + c.Status).Translate()).Colorize(NetrunUI.Muted));
            int owned = net?.Map?.Nodes.Count(n => n.OwnerFaction == c.FactionId) ?? 0;
            Widgets.Label(new Rect(tx, card.y + 52f, card.width - 300f, 18f), "NR_ClubCorpTerritory".Translate(owned).Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;

            // Alert and Strength bars.
            float bx = card.xMax - 220f;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(bx, card.y + 10f, 200f, 16f), "NR_ClubCorpAlert".Translate(c.Alert.ToString("0")));
            NetrunUI.Bar(new Rect(bx, card.y + 28f, 200f, 8f), c.Alert / 100f, c.Alert >= 80f ? NetrunUI.Bad : c.Alert >= 40f ? NetrunUI.Warn : NetrunUI.Ok);
            Widgets.Label(new Rect(bx, card.y + 42f, 200f, 16f), "NR_ClubCorpStrength".Translate(c.Strength.ToString("0")));
            NetrunUI.Bar(new Rect(bx, card.y + 60f, 200f, 8f), c.Strength / 100f, NetrunUI.Accent);
            Text.Font = GameFont.Small;
            NetrunUI.Tip(new Rect(bx, card.y + 8f, 200f, 64f), "NR_ClubCorpBarsTip".Translate());

            MapNode fortress = net?.Map?.Nodes.FirstOrDefault(n => n.Fortress && n.OwnerFaction == c.FactionId);
            if (NetrunUI.Button(new Rect(card.x + 10f, card.yMax - 40f, 140f, 30f), "NR_ClubOnMap".Translate(), NetrunUI.Accent,
                    fortress != null ? true : (AcceptanceReport)"NR_MapNotReady".Translate()))
                Find.WindowStack.WindowOfType<Dialog_NetTerminal>()?.OpenGatewayAt(fortress.Id);
            IntrusionPacket packet = IntrusionManager.Get()?.Packets.FirstOrDefault(p => p.SourceFaction == c.FactionId);
            if (packet != null)
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(card.x + 160f, card.yMax - 34f, card.width - 170f, 18f),
                    "NR_ClubCorpIntrusion".Translate(packet.Attack?.label ?? "?", (packet.Path.Count * 2f).ToString("0")).Colorize(NetrunUI.Bad));
                Text.Font = GameFont.Small;
            }
            y += h + 10f;
        }
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(0f, y, view.width, 60f), "NR_ClubCorpsHelp".Translate().Colorize(NetrunUI.Muted));
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
        const float h = 150f;
        var view = new Rect(0f, 0f, rect.width - 16f, crews.Count * (h + 10f) + 60f);
        NetrunUI.BeginScroll(rect, ref _scroll, view);
        float y = 0f;
        GatewayManager gateway = GatewayManager.Get();
        NetMapManager net = NetMapManager.Get();
        NetRunner runner = net?.Runners.FirstOrDefault(r => !r.Busy && !r.Moving);
        foreach (Faction f in crews)
        {
            CrewState s = sim?.StateOf(f);
            var card = new Rect(0f, y, view.width, h);
            NetrunUI.Card(card, f.Color);
            Emblem(new Rect(card.x + 10f, card.y + 10f, 64f, 64f), f);
            float tx = card.x + 86f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(tx, card.y + 6f, card.width - 100f, 30f), f.Name.Colorize(f.Color));
            Text.Font = GameFont.Tiny;
            RankEntry entry = ranking?.CrewEntry(f);
            int place = entry == null ? 0 : ranking.PlaceOf(entry);
            string line = ("NR_CrewFocus_" + (s?.Focus ?? CrewFocus.Markets)).Translate() + " · " + "NR_ClubCrewRelation".Translate(f.PlayerGoodwill.ToStringWithSign())
                          + " · " + "NR_ClubCrewSkill".Translate(s?.Skill ?? 0) + " · " + "NR_ClubCrewFame".Translate((entry?.Fame ?? 0f).ToString("0"), place);
            Widgets.Label(new Rect(tx, card.y + 34f, card.width - 100f, 18f), line.Colorize(NetrunUI.Muted));
            if (s?.LastAction != null)
                Widgets.Label(new Rect(tx, card.y + 52f, card.width - 100f, 18f), ("NR_ClubCrewLast".Translate() + " " + s.LastAction).Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;

            float bx = card.x + 10f, by = card.yMax - 40f;
            bool friendly = !f.HostileTo(Faction.OfPlayer);
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
            y += h + 10f;
        }
        Widgets.EndScrollView();
    }

    // ---- Top 100 ----

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
        Widgets.Label(new Rect(fx + 12f, rect.y + 4f, rect.xMax - fx - 12f, 20f), goal.Colorize(NetrunUI.Accent));
        Text.Font = GameFont.Small;

        var list = new Rect(rect.x, rect.y + 34f, rect.width, rect.height - 34f - 60f);
        List<(RankEntry e, int place)> rows = all.Select((e, i) => (e, i + 1))
            .Where(r => _topFilter == 0 || (_topFilter == 1 ? r.e.Kind == RankKind.Crew : r.e.Kind == RankKind.Colony)).ToList();
        var view = new Rect(0f, 0f, list.width - 16f, rows.Count * 24f);
        NetrunUI.BeginScroll(list, ref _scroll, view);
        float y = 0f;
        Text.Font = GameFont.Tiny;
        foreach ((RankEntry e, int place) in rows)
        {
            var row = new Rect(0f, y, view.width, 22f);
            if (e.Kind == RankKind.Colony)
                Widgets.DrawBoxSolid(row, new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.12f));
            else if (place % 2 == 0)
                Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.03f));
            Color c = e.Kind == RankKind.Colony ? NetrunUI.Ok : e.Kind == RankKind.Crew ? NetrunUI.Purple : NetrunUI.TextColor;
            Widgets.Label(new Rect(row.x + 6f, row.y + 3f, 40f, 18f), ("#" + place).Colorize(NetrunUI.Muted));
            Widgets.Label(new Rect(row.x + 50f, row.y + 3f, row.width * 0.4f, 18f), e.Callsign.Colorize(c));
            Widgets.Label(new Rect(row.x + 50f + row.width * 0.4f, row.y + 3f, 120f, 18f), ("NR_RankKind_" + e.Kind).Translate().Colorize(NetrunUI.Muted));
            Widgets.Label(new Rect(row.xMax - 170f, row.y + 3f, 80f, 18f), e.Fame.ToString("0"));
            float trend = e.Trend;
            Widgets.Label(new Rect(row.xMax - 80f, row.y + 3f, 76f, 18f),
                (trend > 0.5f ? "▲ " + trend.ToString("0") : trend < -0.5f ? "▼ " + (-trend).ToString("0") : "·").Colorize(trend > 0.5f ? NetrunUI.Ok : trend < -0.5f ? NetrunUI.Bad : NetrunUI.Muted));
            NetrunUI.Tip(row, NetRanking.Title(place));
            y += 24f;
        }
        Text.Font = GameFont.Small;
        Widgets.EndScrollView();

        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(rect.x, rect.yMax - 56f, rect.width, 56f), "NR_ClubTopHelp".Translate().Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
    }
}
