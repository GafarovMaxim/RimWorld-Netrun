using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// One-time fixes for saves made with older Netrun versions. Runs once after loading; a new game starts at the
/// current version and skips everything. Each step runs on its own: the save version moves up after each step
/// that worked, a failed step keeps the version (it is retried at the next load) and stops the steps after it.
/// Steps must be idempotent: the dev actions rerun them on a live game.
/// </summary>
public class NetrunMigration : GameComponent
{
    /// <summary>
    /// Ordered by version. A task that changes the save format adds one line here with the next version
    /// (docs/DEV_PLAN.md, DoD 4; docs/TECH_DETAILS.md, section 2.7).
    /// </summary>
    private static readonly (int Version, string Name, System.Action Run)[] Steps =
    [
        (1, "research moved from HE projects to Netrun's own", MigrateResearch),
    ];

    /// <summary>
    /// Save versions: 1 = research moved from HE projects to Netrun's own (docs/CONCEPTS_10.md, section 3).
    /// T1.1 rebuilt the step runner and added no version. The current version is the last step's.
    /// </summary>
    public static int CurrentVersion => Steps[Steps.Length - 1].Version;

    private int _version;

    // Not saved: what failed during the last run, for the dev report.
    private readonly Dictionary<int, string> _failed = [];

    public NetrunMigration(Game game)
    {
    }

    public static NetrunMigration Get() => GameComp<NetrunMigration>.Get();

    public int Version => _version;

    public override void StartedNewGame()
    {
        base.StartedNewGame();
        _version = CurrentVersion;
    }

    public override void LoadedGame()
    {
        base.LoadedGame();
        if (_version < CurrentVersion)
            RunPending();
    }

    /// <summary>Runs every step above the save version in order; returns one line per step it ran.</summary>
    public List<string> RunPending()
    {
        var lines = new List<string>();
        _failed.Clear();
        foreach ((int version, string name, System.Action run) in Steps)
        {
            if (version <= _version)
                continue;
            try
            {
                run();
                _version = version;
                lines.Add($"v{version} {name}: done");
                Log.Message($"[Netrun] save migration v{version} ({name}) done.");
            }
            catch (System.Exception e)
            {
                _failed[version] = e.Message;
                lines.Add($"v{version} {name}: FAILED ({e.Message}), later steps wait");
                Log.Warning($"[Netrun] save migration v{version} ({name}) failed, retried at the next load: {e.Message}");
                break;
            }
        }
        return lines;
    }

    /// <summary>Sets the save version back so that steps from <paramref name="fromVersion"/> on run again.</summary>
    public List<string> RerunFrom(int fromVersion)
    {
        _version = System.Math.Max(0, fromVersion - 1);
        return RunPending();
    }

    /// <summary>The dev report: save version, current version, one line per step.</summary>
    public List<string> Report()
    {
        var lines = new List<string> { $"save version {_version}, current {CurrentVersion}" };
        foreach ((int version, string name, _) in Steps)
        {
            string state = _failed.TryGetValue(version, out string error) ? $"failed last run ({error})"
                : version <= _version ? "done"
                : "pending";
            lines.Add($"v{version} {name}: {state}");
        }
        return lines;
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
