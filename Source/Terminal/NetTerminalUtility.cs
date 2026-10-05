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

    public static void OpenTerminal(Map map)
    {
        if (Find.WindowStack.IsOpen<Dialog_NetTerminal>())
            return;

        Find.WindowStack.Add(new Dialog_NetTerminal(map));
    }
}
