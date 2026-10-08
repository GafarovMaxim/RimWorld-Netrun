using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// "Colony defense" in the terminal (docs/TASKS_2026-10-08.md, B2): shown on the Gateway map when Home is picked.
/// The whole defense is run from here: the total and what makes it up, the ICE tier with its upkeep, the firewalls
/// and which one is the honeypot, network isolation, intrusions on their way. The firewall building has no gizmos.
/// </summary>
public static class DefensePanel
{
    private static readonly List<(string label, int value)> Parts = [];

    public static float Draw(Rect inner, float y, Map map)
    {
        NodeDefense defense = NodeDefense.Get();
        if (defense == null || map == null)
            return y;
        y = NetrunUI.Section(new Rect(inner.x, y + 4f, inner.width, 36f), "NR_DefensePanelTitle".Translate());

        // Total and its parts.
        Parts.Clear();
        int total = defense.Defense(map, Parts);
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inner.x, y, inner.width, 32f), "NR_DefensePanelTotal".Translate(total).Colorize(total > 0 ? NetrunUI.Ok : NetrunUI.Bad));
        Text.Font = GameFont.Small;
        y += 32f;
        Text.Font = GameFont.Tiny;
        foreach ((string label, int value) in Parts)
        {
            Widgets.Label(new Rect(inner.x + 8f, y, inner.width - 50f, 18f), label.Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inner.xMax - 44f, y, 40f, 18f), ("+" + value).Colorize(NetrunUI.TextColor));
            Text.Anchor = TextAnchor.UpperLeft;
            y += 17f;
        }
        if (Parts.Count == 0)
        {
            Widgets.Label(new Rect(inner.x + 8f, y, inner.width - 8f, 30f), ((string)"NR_DefensePanelNone".Translate()).Colorize(NetrunUI.Muted));
            y += 30f;
        }
        Text.Font = GameFont.Small;
        y += 6f;

        // ICE tier: one button per tier, the locked ones explain why.
        Widgets.Label(new Rect(inner.x, y, inner.width, 22f), ((string)"NR_DefensePanelIce".Translate()).ToUpper().Colorize(NetrunUI.Muted));
        y += 22f;
        IceTier max = NodeDefense.MaxTier();
        float x = inner.x;
        foreach (IceTier tier in System.Enum.GetValues(typeof(IceTier)))
        {
            (int regular, int advanced) = NodeDefense.Upkeep(tier);
            string label = ("NR_IceTier_" + tier).Translate() + (tier > IceTier.None ? " +" + NodeDefense.TierDefense(tier) : string.Empty);
            string tip = tier == IceTier.None ? (string)"NR_IceTierGizmoDesc".Translate()
                : advanced > 0 ? (string)"NR_IceTierUpkeepAdv".Translate(advanced, NetrunDefOf.NR_Defense.upkeepDays)
                : (string)"NR_IceTierUpkeep".Translate(regular, NetrunDefOf.NR_Defense.upkeepDays);
            float w = NetrunUI.ButtonWidth(label);
            if (x > inner.x && x + w > inner.xMax)
            {
                x = inner.x;
                y += 32f;
            }
            bool on = defense.Tier == tier;
            AcceptanceReport can = tier > max ? (AcceptanceReport)"NR_IceTierLocked".Translate() : true;
            var r = new Rect(x, y, w, 28f);
            if (on)
                Widgets.DrawBoxSolid(r, new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.18f));
            if (NetrunUI.Button(r, (on ? "● " : string.Empty) + label, on ? NetrunUI.Ok : tier == IceTier.BlackIce ? NetrunUI.Bad : NetrunUI.Accent, can, tip) && !on)
                defense.SetTier(tier, map);
            x += w + 6f;
        }
        y += 36f;

        // Firewalls: which one is the honeypot (a firewall left open as bait).
        List<Building> firewalls = NodeDefense.Firewalls(map).ToList();
        Widgets.Label(new Rect(inner.x, y, inner.width, 22f), ((string)"NR_DefensePanelFirewalls".Translate(firewalls.Count, NetrunDefOf.NR_Defense.maxFirewalls)).ToUpper().Colorize(NetrunUI.Muted));
        y += 22f;
        if (firewalls.Count == 0)
        {
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inner.x + 8f, y, inner.width - 8f, 30f), ((string)"NR_DefensePanelNoFirewall".Translate()).Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
            y += 30f;
        }
        for (int i = 0; i < firewalls.Count; i++)
        {
            Building f = firewalls[i];
            bool bait = defense.HoneypotId == f.thingIDNumber;
            string label = "NR_DefensePanelFirewall".Translate(i + 1) + ": " + (bait ? "NR_HoneypotGizmo".Translate() : "NR_DefensePanelGuarding".Translate());
            if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 28f), label, bait ? NetrunUI.Warn : NetrunUI.TextColor, true, "NR_HoneypotGizmoDesc".Translate()))
                defense.HoneypotId = bait ? -1 : f.thingIDNumber;
            y += 32f;
        }

        // Isolation.
        AcceptanceReport toggle = defense.CanToggleIsolation();
        string iso = defense.Isolated ? "NR_DefensePanelReconnect".Translate() : "NR_IsolationGizmo".Translate();
        if (NetrunUI.Button(new Rect(inner.x, y, inner.width, 32f), iso, defense.Isolated ? NetrunUI.Ok : NetrunUI.Bad, toggle, "NR_IsolationGizmoDesc".Translate(), NetrunTex.NetworkLock))
            defense.ToggleIsolation();
        y += 38f;

        // Intrusions on their way.
        int packets = IntrusionManager.Get()?.Packets.Count ?? 0;
        Text.Font = GameFont.Tiny;
        string intr = packets > 0 ? (string)"NR_DefensePanelIntrusions".Translate(packets) : (string)"NR_DefensePanelQuiet".Translate();
        Widgets.Label(new Rect(inner.x, y, inner.width, 32f), intr.Colorize(packets > 0 ? NetrunUI.Bad : NetrunUI.Muted));
        Text.Font = GameFont.Small;
        return y + 34f;
    }
}
