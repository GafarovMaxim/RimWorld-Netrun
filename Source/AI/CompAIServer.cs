using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

public class CompProperties_AIServer : CompProperties
{
    public CompProperties_AIServer() => compClass = typeof(CompAIServer);
}

/// <summary>Hosts awakened AIs. Starts with one slot; rare core modules add more.</summary>
public class CompAIServer : ThingComp
{
    private int _installedModules;
    private CompPowerTrader _power;

    private static AISettingsDef Settings => NetrunDefOf.NR_AI;

    public int Slots => System.Math.Min(Settings.maxSlots, Settings.baseSlots + _installedModules);
    public int Used => AIManager.Get()?.InServer((Building)parent).Count() ?? 0;
    public bool HasFreeSlot => Used < Slots;
    public bool Powered => _power == null || _power.PowerOn;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        _power = parent.GetComp<CompPowerTrader>();
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
        AIManager.Get()?.OnServerDestroyed((Building)parent, previousMap, parent.Position);
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            yield return gizmo;

        if (parent.Faction != Faction.OfPlayer)
            yield break;

        yield return NetTerminalUtility.MakeGizmo(parent.Map, Powered);

        if (Slots >= Settings.maxSlots)
            yield break;

        var install = new Command_Action
        {
            defaultLabel = "NR_InstallCoreModule".Translate(),
            defaultDesc = "NR_InstallCoreModuleDesc".Translate(Slots, Settings.maxSlots),
            icon = NetrunDefOf.NR_AICoreModule.uiIcon,
            action = InstallModule,
        };
        if (ColonyGoods.Count(parent.Map, NetrunDefOf.NR_AICoreModule) == 0)
            install.Disable("NR_NoCoreModule".Translate());
        yield return install;
    }

    private void InstallModule()
    {
        if (!ColonyGoods.TryTake(parent.Map, NetrunDefOf.NR_AICoreModule, 1))
            return;

        _installedModules++;
        Messages.Message("NR_CoreModuleInstalled".Translate(parent.LabelCap, Slots), parent, MessageTypeDefOf.PositiveEvent, historical: false);
    }

    public override string CompInspectStringExtra()
    {
        var lines = new List<string> { "NR_ServerSlots".Translate(Used, Slots) };
        foreach (NetAI ai in AIManager.Get()?.InServer((Building)parent) ?? [])
            lines.Add($"  {ai.Name} ({ai.Personality.Label()})");
        if (!Powered && Used > 0)
            lines.Add("NR_ServerAsleep".Translate());
        return string.Join("\n", lines);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref _installedModules, "installedModules");
    }
}
