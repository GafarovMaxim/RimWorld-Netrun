using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

[StaticConstructorOnStartup]
public static class NetTerminalUtility
{
    // Placeholder until the dedicated icon arrives: Hacking Expansion's hack target icon.
    private static readonly Texture2D Icon = ContentFinder<Texture2D>.Get("UI/Gizmos/HackTarget");

    public static Command_Action MakeGizmo(Map map, bool powered)
    {
        var command = new Command_Action
        {
            defaultLabel = "NR_CommandNetTerminal".Translate(),
            defaultDesc = "NR_CommandNetTerminalDesc".Translate(),
            icon = Icon,
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
