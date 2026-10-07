using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// Hacking Expansion items as one-shot dive programs (docs/CONCEPTS_10.md, section 8): they take a program slot,
/// one HE item per dive, and need the "combat executables" research. The item itself is the price: no cyberdata.
/// </summary>
public static class HePrograms
{
    private static List<ThingDef> _execDefs;

    public static bool IsHeItem(DiveProgram program) => program is DiveProgram.IceBreaker or DiveProgram.Exec;

    /// <summary>The HE items a program burns: the black ICE breaker, or any exec data.</summary>
    public static IReadOnlyList<ThingDef> ItemDefs(DiveProgram program)
    {
        if (program == DiveProgram.IceBreaker)
            return DefDatabase<ThingDef>.GetNamedSilentFail("USH_ICEBreaker") is { } breaker ? [breaker] : [];
        if (program == DiveProgram.Exec)
            return _execDefs ??= DefDatabase<ThingDef>.AllDefs.Where(d => d.defName.StartsWith("USH_ExecData_") && d.category == ThingCategory.Item).ToList();
        return [];
    }

    /// <summary>The item to show on the program button.</summary>
    public static ThingDef IconDef(DiveProgram program) => ItemDefs(program).FirstOrDefault();

    public static int Available(Map map, DiveProgram program) => ItemDefs(program).Sum(d => ColonyGoods.Count(map, d));

    public static AcceptanceReport CanSelect(Map map, DiveProgram program, IEnumerable<DiveProgram> selected)
    {
        if (!IsHeItem(program))
            return true;
        if (NetrunDefOf.NR_Res_ExecPrograms?.IsFinished != true)
            return "NR_HeProgramNoResearch".Translate(NetrunDefOf.NR_Res_ExecPrograms?.LabelCap ?? "?");
        if (selected.Any(p => p != program && IsHeItem(p)))
            return "NR_HeProgramOnlyOne".Translate();
        if (Available(map, program) <= 0)
            return "NR_HeProgramNoItem".Translate(IconDef(program)?.label ?? "?");
        return true;
    }

    /// <summary>Burns one item for the program; false if none is left.</summary>
    public static bool TryConsume(Map map, DiveProgram program)
    {
        foreach (ThingDef def in ItemDefs(program))
        {
            if (ColonyGoods.Count(map, def) > 0 && ColonyGoods.TryTake(map, def, 1))
                return true;
        }
        return false;
    }
}
