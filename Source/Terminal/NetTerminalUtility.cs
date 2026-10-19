using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Any comms console, vanilla or from another mod (Vanilla Gravship Expanded's compact terminal and the like), doubles as
/// the network terminal: every Building_CommsConsole def gets the terminal comp at startup, no per-mod patch needed.
/// </summary>
[StaticConstructorOnStartup]
public static class NetTerminalDefs
{
    static NetTerminalDefs()
    {
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
        {
            if (def.thingClass == null || !typeof(Building_CommsConsole).IsAssignableFrom(def.thingClass)
                || def.comps?.Any(c => c is CompProperties_NetTerminal) == true)
                continue;
            def.comps ??= [];
            def.comps.Add(new CompProperties_NetTerminal());
        }
    }
}

public static class NetTerminalUtility
{
    private static List<ThingDef> _consoleDefs;

    /// <summary>Buildings with the terminal comp: the comms console, and modded ones (VGE comms terminal) via patches.</summary>
    public static List<ThingDef> ConsoleDefs => _consoleDefs ??= DefDatabase<ThingDef>.AllDefs
        .Where(d => d.comps?.Any(c => c is CompProperties_NetTerminal) == true).ToList();

    public static IEnumerable<Building> Consoles(Map map) => ConsoleDefs.SelectMany(d => map.listerBuildings.AllBuildingsColonistOfDef(d));

    public static Command_Action MakeGizmo(Map map, bool powered)
    {
        var command = new Command_Action
        {
            defaultLabel = "NR_CommandNetTerminal".Translate(),
            defaultDesc = "NR_CommandNetTerminalDesc".Translate(),
            icon = NetrunTex.NetTerminal,
            action = () => OpenTerminal(map),
        };

        if (!powered)
            command.Disable("NR_TerminalNoPower".Translate());

        return command;
    }

    public static void OpenTerminal(Map map, bool gateway = false)
    {
        if (Find.WindowStack.WindowOfType<Dialog_NetTerminal>() is { } open)
        {
            if (gateway)
                open.OpenGatewayAt(-1);
            return;
        }

        Find.WindowStack.Add(new Dialog_NetTerminal(map, gateway));
    }

    /// <summary>The cyberpod's own button: straight to the cyberspace map.</summary>
    public static Command_Action MakeCyberspaceGizmo(Map map, bool powered)
    {
        var command = new Command_Action
        {
            defaultLabel = "NR_CommandCyberspace".Translate(),
            defaultDesc = "NR_CommandCyberspaceDesc".Translate(),
            icon = NetrunTex.NetTerminal,
            action = () => OpenTerminal(map, gateway: true),
        };
        if (!powered)
            command.Disable("NR_TerminalNoPower".Translate());
        return command;
    }
}
