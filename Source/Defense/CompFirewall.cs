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
        // The defense is run from the terminal (Gateway map, Home); the building only shows its state.
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
