using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// One-time fixes for saves made with older Netrun versions. Runs once after loading; a new game starts at the
/// current version and skips everything.
/// </summary>
public class NetrunMigration : GameComponent
{
    /// <summary>1: research moved from HE projects to Netrun's own (docs/CONCEPTS_10.md, section 3).</summary>
    private const int CurrentVersion = 1;

    private int _version;

    public NetrunMigration(Game game)
    {
    }

    public override void StartedNewGame()
    {
        base.StartedNewGame();
        _version = CurrentVersion;
    }

    public override void LoadedGame()
    {
        base.LoadedGame();
        if (_version >= CurrentVersion)
            return;
        try
        {
            if (_version < 1)
                MigrateResearch();
        }
        catch (System.Exception e)
        {
            Log.Warning($"[Netrun] save migration failed: {e.Message}");
        }
        _version = CurrentVersion;
    }

    /// <summary>
    /// Goggles, the neurostabilizer, the AI server, racks, coprocessor and socket used to hang on HE projects.
    /// A colony that already had the old prerequisite keeps what it could build.
    /// </summary>
    private static void MigrateResearch()
    {
        Grant(NetrunDefOf.NR_Res_Neurorecording, "USH_BasicCybertech");
        Grant(NetrunDefOf.NR_Res_AIAwakening, "USH_AdvancedCybertech");
        Grant(NetrunDefOf.NR_Res_AIArchitecture, "USH_AdvancedCybertech");
    }

    private static void Grant(ResearchProjectDef project, string oldPrerequisite)
    {
        ResearchProjectDef old = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(oldPrerequisite);
        if (project == null || project.IsFinished || old?.IsFinished != true)
            return;
        Find.ResearchManager.FinishProject(project, doCompletionDialog: false, researcher: null, doCompletionLetter: false);
        Log.Message($"[Netrun] old save: {project.defName} granted because {oldPrerequisite} was already researched.");
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref _version, "version");
    }
}
