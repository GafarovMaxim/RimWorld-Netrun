using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

public static class NetTerminalUtility
{
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
