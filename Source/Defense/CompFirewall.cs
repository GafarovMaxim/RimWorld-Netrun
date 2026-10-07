using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

public class CompProperties_Firewall : CompProperties
{
    public CompProperties_Firewall() => compClass = typeof(CompFirewall);
}

/// <summary>The firewall's gizmos: the colony's ICE tier, honeypot mode, network isolation; and the defense readout.</summary>
public class CompFirewall : ThingComp
{
    private static NodeDefense Defense => NodeDefense.Get();

    public bool IsHoneypot => Defense?.HoneypotId == parent.thingIDNumber;

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo g in base.CompGetGizmosExtra())
            yield return g;
        NodeDefense defense = Defense;
        if (defense == null || parent.Faction != Faction.OfPlayer)
            yield break;

        yield return new Command_Action
        {
            defaultLabel = "NR_IceTierGizmo".Translate(("NR_IceTier_" + defense.Tier).Translate()),
            defaultDesc = "NR_IceTierGizmoDesc".Translate(),
            icon = NetrunTex.CyberIcon(defense.Tier == IceTier.BlackIce ? "Dive_BLACKICE" : "Dive_ICE"),
            action = () => Find.WindowStack.Add(new FloatMenu(TierOptions(defense))),
        };

        yield return new Command_Toggle
        {
            defaultLabel = "NR_HoneypotGizmo".Translate(),
            defaultDesc = "NR_HoneypotGizmoDesc".Translate(),
            icon = NetrunTex.CyberIcon("Event_Honeypot"),
            isActive = () => IsHoneypot,
            toggleAction = () => defense.HoneypotId = IsHoneypot ? -1 : parent.thingIDNumber,
        };

        yield return IsolationGizmo();
    }

    /// <summary>The network isolation switch; also on the comms console.</summary>
    public static Command_Toggle IsolationGizmo()
    {
        NodeDefense defense = NodeDefense.Get();
        var toggle = new Command_Toggle
        {
            defaultLabel = "NR_IsolationGizmo".Translate(),
            defaultDesc = "NR_IsolationGizmoDesc".Translate(),
            icon = NetrunTex.CyberIcon("Icon_Lock"),
            isActive = () => defense?.Isolated == true,
            toggleAction = () => defense?.ToggleIsolation(),
        };
        AcceptanceReport can = defense?.CanToggleIsolation() ?? false;
        if (!can.Accepted)
            toggle.Disable(can.Reason);
        return toggle;
    }

    private List<FloatMenuOption> TierOptions(NodeDefense defense)
    {
        var options = new List<FloatMenuOption>();
        IceTier max = NodeDefense.MaxTier();
        foreach (IceTier tier in System.Enum.GetValues(typeof(IceTier)))
        {
            (int regular, int advanced) = NodeDefense.Upkeep(tier);
            string cost = tier == IceTier.None ? "" : " (" + (advanced > 0
                ? "NR_IceTierUpkeepAdv".Translate(advanced, NetrunDefOf.NR_Defense.upkeepDays)
                : "NR_IceTierUpkeep".Translate(regular, NetrunDefOf.NR_Defense.upkeepDays)) + ")";
            string label = ("NR_IceTier_" + tier).Translate() + (tier > IceTier.None ? " +" + NodeDefense.TierDefense(tier) : "") + cost;
            if (tier > max)
                options.Add(new FloatMenuOption(label + ": " + "NR_IceTierLocked".Translate(), null));
            else
            {
                IceTier pick = tier;
                options.Add(new FloatMenuOption(label, () => defense.SetTier(pick, parent.Map)));
            }
        }
        return options;
    }

    public override string CompInspectStringExtra()
    {
        NodeDefense defense = Defense;
        if (defense == null || parent.Map == null)
            return null;
        string text = "NR_DefenseTotal".Translate(defense.Defense(parent.Map));
        if (IsHoneypot)
            text += "\n" + "NR_HoneypotActive".Translate();
        if (defense.Isolated)
            text += "\n" + "NR_DefenseIsolated".Translate();
        return text;
    }
}
