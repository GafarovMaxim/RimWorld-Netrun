using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The right panel of the cyberspace map, v5 look (Maxim 2026-10-09, mockups 34a/34b): runner cards with a route bar,
/// contract cards, a watchlist of cards. With no hub picked it shows the overview (contracts, watchlist, the next three
/// days, dive reports); with a hub picked, the hub, its nodes and the dive.
/// </summary>
public static partial class GatewayView
{
    /// <summary>A runner as a card: the name, where it is or goes, hours in the net, and a bar for the route walked.</summary>
    private static float RunnerCard(Rect inner, float y, NetRunner r, NetMapManager net, bool sel, int index)
    {
        bool moving = r.Moving;
        var card = new Rect(inner.x, y, inner.width, moving ? 54f : 44f);
        Color c = sel ? NetrunUI.Ok : NetrunUI.Muted;
        NetrunUI.Card(card, c);
        if (sel)
            Widgets.DrawBoxSolid(card, new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.06f));
        else if (Mouse.IsOver(card))
            Widgets.DrawBoxSolid(card, new Color(1f, 1f, 1f, 0.04f));
        if (Widgets.ButtonInvisible(card))
        {
            _selectedRunner = index;
            NetrunSound.Click();
        }
        // A small badge with the initial, as on the map.
        var badge = new Rect(card.x + 10f, card.y + 9f, 24f, 24f);
        GUI.color = c;
        GUI.DrawTexture(badge, HubRing);
        GUI.color = Color.white;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        string name = r.Pawn?.LabelShort ?? "?";
        GUI.color = c;
        Widgets.Label(badge, name.Substring(0, 1).ToUpper());
        Text.Anchor = TextAnchor.UpperLeft;
        float tx = badge.xMax + 8f;
        Text.Font = GameFont.Small;
        GUI.color = sel ? NetrunUI.Ok : NetrunUI.TextColor;
        Widgets.Label(new Rect(tx, card.y + 3f, card.width - tx - 8f, 22f), name + "  ·  " + (moving ? NetrunUI.T("NR_MapRunnerWalking") : NetrunUI.T("NR_MapRunnerStanding")));
        Text.Font = GameFont.Tiny;
        GUI.color = NetrunUI.Muted;
        int hours = (Find.TickManager.TicksGame - r.SessionStartTick) / GenDate.TicksPerHour;
        string where = moving
            ? NetMapManager.NodeLabel(net.Map.Node(r.NodeId)) + " → " + NetMapManager.NodeLabel(net.Map.Node(r.Path[r.Path.Count - 1])) + " · " + net.HoursLeft(r).ToString("0.#") + " h"
            : NetMapManager.NodeLabel(net.Map.Node(r.NodeId));
        Widgets.Label(new Rect(tx, card.y + 24f, card.width - tx - 96f, 16f), where.Truncate(card.width - tx - 96f));
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(new Rect(card.xMax - 96f, card.y + 24f, 88f, 16f), "NR_MapSessionHours".Translate(hours));
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
        if (moving)
        {
            // Route walked so far: the first link's progress against the whole route.
            float total = Mathf.Max(0.01f, net.HoursLeft(r) * NetMapManager.Speed(r.Pawn) + r.Progress);
            float done = Mathf.Clamp01(r.Progress / total);
            var bar = new Rect(tx, card.yMax - 11f, card.xMax - tx - 10f, 4f);
            Widgets.DrawBoxSolid(bar, new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.15f));
            Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * done, bar.height), NetrunUI.Ok);
        }
        Text.Font = GameFont.Small;
        return card.yMax + 6f;
    }

    /// <summary>The contracts running now, as cards: type icon, title, progress bar, time left; "Show" for one with a hub.</summary>
    private static float DrawActiveContracts(Rect inner, float y)
    {
        List<Contract> active = NetrunUI.Cached(ActiveContractsKey, () => ContractManager.Get()?.Active.Where(c => c.Accepted).ToList() ?? [], 0.5f);
        if (active.Count == 0)
            return y;
        float top = y;
        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_MapActiveContracts".Translate());
        foreach (Contract c in active)
        {
            var card = new Rect(inner.x, y, inner.width, 50f);
            Color tc = c.TicksLeft < GenDate.TicksPerDay ? NetrunUI.Warn : NetrunUI.Accent;
            NetrunUI.Card(card, tc);
            if (ContractsTab.IconOf(c.Type) is { } icon)
                GUI.DrawTexture(new Rect(card.x + 10f, card.y + 11f, 26f, 26f), NetrunTint.OfAny(icon, tc));
            float tx = card.x + 44f;
            bool show = c.WatchesHub;
            float right = card.xMax - (show ? 72f : 8f);
            Text.Font = GameFont.Small;
            GUI.color = NetrunUI.TextColor;
            Widgets.Label(new Rect(tx, card.y + 3f, right - tx, 22f), c.Title.Truncate(right - tx));
            Text.Font = GameFont.Tiny;
            GUI.color = NetrunUI.Muted;
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(tx, card.y + 6f, right - tx, 16f), c.TicksLeft.ToStringTicksToPeriod());
            Text.Anchor = TextAnchor.UpperLeft;
            // Progress: a bar and the count.
            var bar = new Rect(tx, card.y + 31f, right - tx - 50f, 6f);
            float f = c.Goal > 0 ? Mathf.Clamp01(c.Progress / (float)c.Goal) : 0f;
            Widgets.DrawBoxSolid(bar, new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.15f));
            Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * f, bar.height), NetrunUI.Ok);
            GUI.color = NetrunUI.Ok;
            Widgets.Label(new Rect(bar.xMax + 6f, card.y + 26f, 50f, 16f), $"{c.Progress}/{c.Goal}");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (show && NetrunUI.Button(new Rect(card.xMax - 66f, card.y + 10f, 58f, 28f), "NR_MapShow".Translate(), NetrunUI.Warn, true, c.ParamText))
                FocusNode(c.TargetHub);
            if (Mouse.IsOver(card))
                NetrunUI.Tip(card, () => c.Description);
            y += card.height + 6f;
        }
        GuideRegistry.InScroll("gateway.contracts", _panelOuter, _panelScroll, new Rect(inner.x, top, inner.width, y - top));
        return y + 2f;
    }

    private const string ActiveContractsKey = "map.activeContracts";
}
