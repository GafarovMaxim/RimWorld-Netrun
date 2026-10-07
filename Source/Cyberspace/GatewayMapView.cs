using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// The Gateway with the cyberspace map (docs/CYBERSPACE_V2.md): backbone nodes by district, links and their states,
/// branches as small dots around their node, runners walking in game time. The panel shows the runners, the selected
/// node with its branches, and the dive panel for the selected branch.
/// </summary>
public static partial class GatewayView
{
    private static int _selectedMapNode = -1;
    private static int _selectedRunner;
    private static bool _offeredSetup;

    private static readonly Vector2 FieldCenter = new(0.5f, 0.33f);

    public static Color DistrictColor(District d) => d switch
    {
        District.Home => NetrunUI.Accent,
        District.Outskirts => new Color(0.35f, 0.78f, 0.47f),
        District.Transit => new Color(0.24f, 0.82f, 0.9f),
        District.Corp => new Color(0.92f, 0.35f, 0.4f),
        District.Market => new Color(0.9f, 0.82f, 0.47f),
        District.Ruins => new Color(0.67f, 0.59f, 0.51f),
        District.Campus => new Color(0.43f, 0.78f, 0.82f),
        District.Entertainment => new Color(0.94f, 0.47f, 0.6f),
        District.Dump => new Color(0.59f, 0.67f, 0.35f),
        District.Dark => NetrunUI.Purple,
        District.OldNet => new Color(0.86f, 0.24f, 0.27f),
        _ => NetrunUI.Muted,
    };

    private static NetRunner SelectedRunner(NetMapManager net)
        => net.Runners.Count == 0 ? null : net.Runners[Mathf.Clamp(_selectedRunner, 0, net.Runners.Count - 1)];

    /// <summary>Before the map exists: the prompt that opens the "Net found" window.</summary>
    private static void DrawSetupPrompt(Rect rect)
    {
        Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.25f));
        var r = new Rect(rect.center.x - 180f, rect.center.y - 50f, 360f, 100f);
        Text.Anchor = TextAnchor.UpperCenter;
        Widgets.Label(new Rect(r.x, r.y, r.width, 40f), "NR_NetFoundPrompt".Translate().Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
        if (NetrunUI.Button(new Rect(r.x + 60f, r.y + 46f, r.width - 120f, 40f), "NR_NetFoundOpen".Translate(), NetrunUI.Accent, true))
            Find.WindowStack.Add(new Dialog_NetFound());
        if (!_offeredSetup)
        {
            _offeredSetup = true;
            Find.WindowStack.Add(new Dialog_NetFound());
        }
    }

    private static Vector2 ToScreen(Rect local, Vector2 p)
    {
        float scale = Mathf.Min(local.width / 1.04f, local.height / 0.70f) * _zoom;
        return local.center + (p - FieldCenter) * scale + _pan;
    }

    private static void DrawMap(Rect rect, NetMapManager net)
    {
        HandleDrag(rect);
        CyberMap map = net.Map;
        Widgets.BeginGroup(rect);
        var local = new Rect(0f, 0f, rect.width, rect.height);
        DrawGrid(local);

        // District names, faint, at the middle of each district.
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        foreach (IGrouping<string, MapNode> g in map.Nodes.Where(n => n.District is not (District.Home or District.Dark))
                     .GroupBy(n => n.District + "|" + n.CorpSlot))
        {
            Vector2 c = ToScreen(local, g.Aggregate(Vector2.zero, (s, n) => s + n.Pos) / g.Count());
            Color dc = DistrictColor(g.First().District);
            string label = DistrictLabel(g.First(), net);
            Widgets.Label(new Rect(c.x - 90f, c.y - 34f * _zoom - 10f, 180f, 18f), label.ToUpper().Colorize(new Color(dc.r, dc.g, dc.b, 0.45f)));
        }
        Text.Anchor = TextAnchor.UpperLeft;

        // Links.
        NetRunner selRunner = SelectedRunner(net);
        HashSet<(int, int)> route = RouteSet(selRunner);
        foreach (MapLink l in map.Links)
        {
            if (!l.Known)
                continue;
            DrawMapLink(ToScreen(local, map.Node(l.A).Pos), ToScreen(local, map.Node(l.B).Pos), l, map,
                route.Contains((Mathf.Min(l.A, l.B), Mathf.Max(l.A, l.B))));
        }

        // Nodes.
        foreach (MapNode n in map.Nodes)
        {
            if (n.District == District.Dark && !map.LinksOf(n.Id).Any(l => l.Known))
                continue;
            DrawMapNode(local, n, net);
        }

        // Runners.
        foreach (NetRunner r in net.Runners)
            DrawRunner(local, r, net, r == selRunner);
        Widgets.EndGroup();

        string reports = "NR_GatewayReports".Translate(GatewayManager.Get()?.Reports.Count ?? 0);
        float rw = Mathf.Max(3 * 34f - 4f, NetrunUI.ButtonWidth(reports, icon: true));
        DrawReportsButton(new Rect(rect.xMax - rw - 4f, rect.y + 4f, rw, 34f), reports);
        DrawZoomButtons(new Rect(rect.xMax - 3 * 34f, rect.y + 44f, 3 * 34f, 30f));
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(rect.x + 6f, rect.y + 4f, 420f, 18f),
            "NR_MapHeader".Translate(("NR_NetSize_" + map.Size).Translate(), ("NR_NetLayout_" + map.Layout).Translate()).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
    }

    private static string DistrictLabel(MapNode n, NetMapManager net)
    {
        if (n.District == District.Corp)
        {
            Faction owner = net.CorpOfSlot(n.CorpSlot);
            if (owner != null)
                return owner.Name;
        }
        return ("NR_District_" + n.District).Translate();
    }

    private static HashSet<(int, int)> RouteSet(NetRunner r)
    {
        var set = new HashSet<(int, int)>();
        if (r == null || !r.Moving)
            return set;
        int from = r.NodeId;
        foreach (int to in r.Path)
        {
            set.Add((Mathf.Min(from, to), Mathf.Max(from, to)));
            from = to;
        }
        return set;
    }

    private static void DrawMapLink(Vector2 a, Vector2 b, MapLink l, CyberMap map, bool onRoute)
    {
        Color c = l.Kind switch
        {
            LinkKind.Fast => new Color(0.24f, 0.82f, 0.9f),
            LinkKind.Dark => NetrunUI.Purple,
            LinkKind.Wall => map.WallOpen ? NetrunUI.Warn : NetrunUI.Bad,
            LinkKind.Backdoor => NetrunUI.Warn,
            _ => new Color(0.55f, 0.62f, 0.55f),
        };
        if (l.Jammed)
            c = new Color(0.95f, 0.6f, 0.2f);
        float alpha = l.Broken ? 0.25f : 0.75f;
        float width = l.Kind == LinkKind.Fast ? 2.5f : 1.5f;
        if (onRoute)
        {
            Widgets.DrawLine(a, b, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.35f), 7f);
            c = NetrunUI.Accent;
            alpha = 1f;
        }
        if (l.Kind is LinkKind.Dark or LinkKind.Backdoor or LinkKind.Wall)
        {
            // Dashed.
            float len = Vector2.Distance(a, b);
            int segments = Mathf.Max(2, Mathf.FloorToInt(len / 10f));
            for (int i = 0; i < segments; i += 2)
                Widgets.DrawLine(Vector2.Lerp(a, b, i / (float)segments), Vector2.Lerp(a, b, Mathf.Min(1f, (i + 1) / (float)segments)), new Color(c.r, c.g, c.b, alpha), width);
        }
        else
        {
            Widgets.DrawLine(a, b, new Color(c.r, c.g, c.b, 0.12f), width + 4f);
            Widgets.DrawLine(a, b, new Color(c.r, c.g, c.b, alpha), width);
        }
        Vector2 mid = (a + b) / 2f;
        Text.Font = GameFont.Tiny;
        if (l.Broken)
            Widgets.Label(new Rect(mid.x - 5f, mid.y - 9f, 14f, 18f), "✕".Colorize(NetrunUI.Bad));
        else if (l.Toll)
        {
            GUI.color = NetrunUI.Warn;
            GUI.DrawTexture(new Rect(mid.x - 4f, mid.y - 4f, 8f, 8f), NetrunUI.Disc);
            GUI.color = Color.white;
        }
        Text.Font = GameFont.Small;
    }

    private static void DrawMapNode(Rect local, MapNode n, NetMapManager net)
    {
        Vector2 p = ToScreen(local, n.Pos);
        bool home = n.District == District.Home;
        float r = (home ? 15f : n.Fortress ? 11f : 8f) * Mathf.Sqrt(_zoom);
        var rect = new Rect(p.x - r, p.y - r, r * 2f, r * 2f);
        Color c = n.Offline ? NetrunUI.Muted : DistrictColor(n.District);
        bool selected = n.Id == _selectedMapNode;
        bool hover = Mouse.IsOver(rect.ExpandedBy(4f));

        if (n.Storm)
            NetrunUI.Glow(rect, NetrunUI.Purple, 0.6f, 10f);
        if (n.Transit)
            NetrunUI.Glow(rect, new Color(0.24f, 0.82f, 0.9f), 0.6f, 8f);
        if (selected || hover)
            NetrunUI.Glow(rect, c, 0.7f, 8f);
        GUI.color = NetrunUI.BgWindow;
        GUI.DrawTexture(rect.ExpandedBy(2f), NetrunUI.Disc);
        GUI.color = new Color(c.r, c.g, c.b, n.Offline ? 0.5f : 1f);
        GUI.DrawTexture(rect, home ? NetrunUI.Ring : NetrunUI.Disc);
        if (n.Fortress)
        {
            GUI.color = NetrunUI.Bad;
            Widgets.DrawBox(rect.ExpandedBy(3f), 2);
        }
        if (n.Exit && !home)
        {
            GUI.color = NetrunUI.Ok;
            GUI.DrawTexture(rect.ExpandedBy(5f), NetrunUI.ThinRing);
        }
        GUI.color = Color.white;
        Text.Font = GameFont.Tiny;
        if (n.Offline)
            Widgets.Label(new Rect(p.x - 5f, p.y - 9f, 14f, 18f), "✕".Colorize(NetrunUI.Bad));
        if (n.Patrol)
            Widgets.Label(new Rect(rect.xMax - 2f, rect.y - 12f, 16f, 16f), "!".Colorize(NetrunUI.Bad));
        if (home)
        {
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(p.x - 40f, rect.yMax + 2f, 80f, 16f), ((string)"NR_District_Home".Translate()).ToUpper().Colorize(NetrunUI.Accent));
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // Branches: small dots around the node, colored by type; unknown ones grey.
        List<ExternalNode> branches = net.BranchesAt(n.Id).ToList();
        for (int i = 0; i < branches.Count; i++)
        {
            ExternalNode b = branches[i];
            float ang = -Mathf.PI / 2f + i * (Mathf.PI * 2f / Mathf.Max(3, branches.Count));
            Vector2 bp = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (r + 7f * Mathf.Sqrt(_zoom));
            float br = (b.IsBoss || b.Ancient ? 5f : 3.5f) * Mathf.Sqrt(_zoom);
            Color bc = b.Hidden ? NetrunUI.Muted : b.Offline ? NetrunUI.Muted : b.Color;
            GUI.color = bc;
            GUI.DrawTexture(new Rect(bp.x - br, bp.y - br, br * 2f, br * 2f), NetrunUI.Disc);
            if (b.Contract && !b.Hidden)
                Widgets.Label(new Rect(bp.x + 2f, bp.y - 14f, 14f, 14f), "★".Colorize(NetrunUI.Ok));
        }
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        if (hover)
            TooltipHandler.TipRegion(rect.ExpandedBy(4f), NodeTip(n, net, branches));
        if (Widgets.ButtonInvisible(rect.ExpandedBy(4f)))
        {
            NetrunSound.Click();
            _selectedMapNode = n.Id;
            ExternalNode first = branches.FirstOrDefault(b => !b.Hidden);
            _selectedId = first?.Id ?? -1;
        }
    }

    private static string NodeTip(MapNode n, NetMapManager net, List<ExternalNode> branches)
    {
        var lines = new List<string> { NetMapManager.NodeLabel(n) };
        if (n.Exit)
            lines.Add("NR_MapExitPoint".Translate());
        if (n.Fortress)
            lines.Add("NR_MapFortress".Translate());
        foreach (string state in NodeStates(n))
            lines.Add(state);
        int known = branches.Count(b => !b.Hidden);
        lines.Add("NR_MapBranches".Translate(known, branches.Count - known));
        return lines.ToLineList();
    }

    private static IEnumerable<string> NodeStates(MapNode n)
    {
        if (n.Offline)
            yield return "NR_MapStateOffline".Translate();
        if (n.Storm)
            yield return "NR_MapStateStorm".Translate();
        if (n.Patrol)
            yield return "NR_MapStatePatrol".Translate();
        if (n.Transit)
            yield return "NR_MapStateTransit".Translate();
    }

    private static void DrawRunner(Rect local, NetRunner r, NetMapManager net, bool selected)
    {
        CyberMap map = net.Map;
        Vector2 p = ToScreen(local, map.Node(r.NodeId).Pos);
        if (r.Moving && map.Node(r.NextNodeId) is { } next)
        {
            MapLink l = map.LinkBetween(r.NodeId, r.NextNodeId);
            float t = l == null ? 0f : Mathf.Clamp01(r.Progress / Mathf.Max(0.05f, map.Hours(l, r.NodeId)));
            p = Vector2.Lerp(p, ToScreen(local, next.Pos), t);
        }
        float s = 9f * Mathf.Sqrt(_zoom);
        var rect = new Rect(p.x - s, p.y - s - 14f * Mathf.Sqrt(_zoom), s * 2f, s * 2f);
        NetrunUI.Glow(rect, NetrunUI.Ok, selected ? 0.9f : 0.5f, 8f);
        GUI.color = selected ? NetrunUI.Ok : new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.8f);
        GUI.DrawTexture(rect, NetrunUI.Disc);
        GUI.color = Color.white;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        string initial = r.Pawn?.LabelShort is { Length: > 0 } name ? name.Substring(0, 1).ToUpper() : "?";
        Widgets.Label(rect, initial.Colorize(NetrunUI.BgWindow));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        TooltipHandler.TipRegion(rect, r.Pawn?.LabelShort ?? "?");
        if (Widgets.ButtonInvisible(rect))
            _selectedRunner = net.Runners.IndexOf(r);
    }

    // ---- Panel ----

    /// <summary>Runners and the selected map node, above the branch panel.</summary>
    private static float DrawMapPanel(Rect inner, float y, Map colonyMap, NetMapManager net)
    {
        // Runners: connected ones, then pawns in pods who could connect.
        y = NetrunUI.Section(new Rect(inner.x, y, inner.width, 36f), "NR_MapRunners".Translate(net.Runners.Count, NetMapManager.Settings.maxRunners));
        for (int i = 0; i < net.Runners.Count; i++)
        {
            NetRunner r = net.Runners[i];
            bool sel = i == Mathf.Clamp(_selectedRunner, 0, net.Runners.Count - 1);
            var row = new Rect(inner.x, y, inner.width, 44f);
            NetrunUI.Card(row, sel ? NetrunUI.Ok : NetrunUI.Muted);
            if (Widgets.ButtonInvisible(row))
                _selectedRunner = i;
            string where = r.Moving
                ? "NR_MapRunnerMoving".Translate(NetMapManager.NodeLabel(net.Map.Node(r.Path[r.Path.Count - 1])), net.HoursLeft(r).ToString("0.#"))
                : "NR_MapRunnerAt".Translate(NetMapManager.NodeLabel(net.Map.Node(r.NodeId)));
            int hours = (Find.TickManager.TicksGame - r.SessionStartTick) / GenDate.TicksPerHour;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(row.x + 8f, row.y + 3f, row.width - 12f, 18f), (r.Pawn?.LabelShort ?? "?").Colorize(sel ? NetrunUI.Ok : NetrunUI.TextColor)
                + "  " + "NR_MapSessionHours".Translate(hours).Colorize(NetrunUI.Muted));
            Widgets.Label(new Rect(row.x + 8f, row.y + 21f, row.width - 12f, 18f), where.Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
            y += 48f;
        }

        NetRunner selected = SelectedRunner(net);
        if (selected != null)
        {
            float bw = (inner.width - 6f) / 2f;
            if (selected.Moving)
            {
                if (NetrunUI.Button(new Rect(inner.x, y, bw, 28f), "NR_MapStop".Translate(), NetrunUI.Muted, true, "NR_MapStopTip".Translate()))
                    net.Stop(selected);
            }
            else
            {
                bool atExit = net.AtExit(selected);
                if (NetrunUI.Button(new Rect(inner.x, y, bw, 28f), "NR_MapExit".Translate(), NetrunUI.Ok, atExit ? true : (AcceptanceReport)"NR_MapNotAtExit".Translate(),
                        "NR_MapExitTip".Translate()))
                    net.Disconnect(selected, emergency: false);
            }
            if (NetrunUI.Button(new Rect(inner.x + bw + 6f, y, bw, 28f), "NR_MapEmergencyButton".Translate(), NetrunUI.Bad, true,
                    "NR_MapEmergencyTip".Translate(NetMapManager.Settings.emergencyNetsick.ToString("0.##"), NetMapManager.Settings.emergencyLockoutChance.ToStringPercent())))
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("NR_MapEmergencyConfirm".Translate(selected.Pawn?.LabelShort ?? "?"),
                    () => net.Disconnect(selected, emergency: true), destructive: true));
            y += 34f;
        }

        // Pawns lying in pods who are not connected yet.
        foreach ((Pawn pawn, Building_Cyberpod _) in NetMapManager.PodPawns(colonyMap).ToList())
        {
            if (net.RunnerOf(pawn) != null)
                continue;
            AcceptanceReport can = net.CanConnect(pawn);
            if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 28f), "NR_MapConnect".Translate(pawn.LabelShort), NetrunUI.Accent, can, "NR_MapConnectTip".Translate()))
            {
                net.Connect(pawn, net.Map.HomeId, colonyMap);
                _selectedRunner = net.Runners.Count - 1;
            }
            y += 32f;
        }
        if (net.Runners.Count == 0 && !NetMapManager.PodPawns(colonyMap).Any())
        {
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inner.x, y, inner.width, 34f), "NR_MapNoPodPawn".Translate().Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
            y += 36f;
        }

        // The selected node.
        MapNode node = net.Map.Node(_selectedMapNode);
        if (node == null)
            return y + 4f;
        y = NetrunUI.Section(new Rect(inner.x, y + 4f, inner.width, 36f), NetMapManager.NodeLabel(node));
        Text.Font = GameFont.Tiny;
        var info = new List<string>();
        if (node.District == District.Corp && net.CorpOfSlot(node.CorpSlot) is { } corp)
            info.Add("NR_MapOwner".Translate(corp.Name));
        if (node.Exit)
            info.Add("NR_MapExitPoint".Translate());
        if (node.Fortress)
            info.Add("NR_MapFortress".Translate());
        info.AddRange(NodeStates(node));
        if (selected != null && selected.NodeId != node.Id)
        {
            List<int> route = net.Map.Route(selected.Moving ? selected.NextNodeId : selected.NodeId, node.Id, out float hours);
            info.Add(route.Count > 0 || selected.NextNodeId == node.Id
                ? "NR_MapRouteInfo".Translate(route.Count + (selected.Moving ? 1 : 0), (hours / NetMapManager.Speed(selected.Pawn)).ToString("0.#"))
                : "NR_MapNoRoute".Translate());
        }
        string infoText = info.ToLineList();
        float ih = Text.CalcHeight(infoText, inner.width);
        Widgets.Label(new Rect(inner.x, y, inner.width, ih), infoText.Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        y += ih + 4f;

        if (selected != null && (selected.NodeId != node.Id || selected.Moving))
        {
            if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 30f), "NR_MapGo".Translate(), NetrunUI.Accent, net.CanTravel(selected, node.Id), "NR_MapGoTip".Translate()))
                net.Travel(selected, node.Id);
            y += 34f;
        }
        // Connect straight at an exit point (a jump for a little cyberdata).
        if (node.Exit && node.District != District.Home)
        {
            foreach ((Pawn pawn, Building_Cyberpod _) in NetMapManager.PodPawns(colonyMap).Where(p => net.RunnerOf(p.pawn) == null).Take(1).ToList())
            {
                if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 28f), "NR_MapJump".Translate(pawn.LabelShort, NetMapManager.Settings.jumpCost), NetrunUI.Ok,
                        net.CanConnect(pawn), "NR_MapJumpTip".Translate()))
                {
                    net.Connect(pawn, node.Id, colonyMap);
                    _selectedRunner = net.Runners.Count - 1;
                }
                y += 32f;
            }
        }

        // Branches of the node: rows to pick the dive target.
        foreach (ExternalNode b in net.BranchesAt(node.Id).ToList())
        {
            var row = new Rect(inner.x, y, inner.width, 26f);
            Color bc = b.Hidden ? NetrunUI.Muted : b.Color;
            if (b.Id == _selectedId)
                Widgets.DrawBoxSolid(row, new Color(bc.r, bc.g, bc.b, 0.15f));
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            string label = b.Hidden ? (string)"NR_GatewayHiddenRow".Translate()
                : b.Name + "  " + (string)(b.BlackIce ? "NR_GatewayBlackIce" : "NR_GatewayDefense").Translate(b.Defense)
                  + (b.Deserted ? "  " + (string)"NR_MapDeserted".Translate() : "") + (b.Offline ? "  " + (string)"NR_MapStateOffline".Translate() : "") + (b.Contract ? "  ★" : "");
            Widgets.Label(new Rect(row.x + 6f, row.y, row.width - 8f, row.height), label.Colorize(bc));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (Widgets.ButtonInvisible(row))
            {
                NetrunSound.Click();
                _selectedId = b.Id;
            }
            y += 28f;
        }
        return y + 4f;
    }
}
