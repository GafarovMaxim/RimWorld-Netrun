using Verse;

namespace Netrun;

/// <summary>
/// The training dive's coach: reads the session state and says what to do next and why (shown in a box over the dive
/// map), plus which action button to outline. The result is cached and recomputed only when the state changes
/// (docs/TECH_DETAILS.md: no per-frame work in draw paths).
/// </summary>
public static class TrainingCoach
{
    /// <summary>The training is forgiving: a flat bonus to every hack chance (the result is still clamped).</summary>
    public const float HackBonus = 0.30f;
    /// <summary>More turns than a real node of the same size.</summary>
    public const int ExtraTurns = 4;
    /// <summary>Shield charges at the start.</summary>
    public const int Shields = 2;

    /// <summary>Which instruction is shown (0 = none yet).</summary>
    public enum Step
    {
        None,
        MoveFirst,
        Move,
        Gate,
        ForkScan,
        ForkChoose,
        HiddenScan,
        Ice,
        Cache,
        Passage,
        Files,
        Exit,
        Done,
        Failed,
    }

    public struct Result
    {
        public Step Step;
        /// <summary>The action button to outline: Move, Scan, Hack, Download, Exit, or Choose (the fork cards); null for none.</summary>
        public string Action;
        public string Text;
    }

    private struct StateKey
    {
        public DiveSession Session;
        public int Position, Visits, ActionsLeft, Turn, Scanned, Forks, Shields;
        public int Flags;

        public bool Equals(StateKey o)
            => Session == o.Session && Position == o.Position && Visits == o.Visits && ActionsLeft == o.ActionsLeft && Turn == o.Turn
               && Scanned == o.Scanned && Forks == o.Forks && Shields == o.Shields && Flags == o.Flags;
    }

    private static StateKey _key;
    private static Result _result;
    private static bool _has;

    /// <summary>The coach's current instruction for the session; recomputed only when something it depends on changed.</summary>
    public static Result Get(DiveSession session)
    {
        StateKey key = KeyOf(session);
        if (_has && _key.Equals(key))
            return _result;
        _key = key;
        _has = true;
        _result = Compute(session);
        return _result;
    }

    private static StateKey KeyOf(DiveSession s)
    {
        DiveElement next = s.Finished ? null : s.Next;
        int flags = (s.Finished ? 1 : 0) | (next != null && next.Hidden ? 2 : 0) | (next != null && next.Cleared ? 4 : 0) | (s.Current.Looted ? 8 : 0);
        return new StateKey
        {
            Session = s,
            Position = s.Position,
            Visits = s.Visited.Count,
            ActionsLeft = s.ActionsLeft,
            Turn = s.Turn,
            Scanned = s.ForkScanned.Count,
            Forks = s.ForkIndex.Count,
            Shields = s.ShieldCharges,
            Flags = flags,
        };
    }

    private static Result Compute(DiveSession s)
    {
        Step step;
        string action = null;
        string text;
        DiveElement here = s.Current;
        DiveElement next = s.Next;
        if (s.Finished)
        {
            step = here.Kind == DiveElementKind.Exit ? Step.Done : Step.Failed;
            text = (step == Step.Done ? "NR_Coach_Done" : "NR_Coach_Failed").Translate();
            return new Result { Step = step, Text = text };
        }
        if (s.AtUndecidedFork)
        {
            bool scanned = s.ForkScanned.Contains(s.Position);
            step = scanned ? Step.ForkChoose : Step.ForkScan;
            action = scanned ? "Choose" : "Scan";
            text = (scanned ? "NR_Coach_ForkChoose" : "NR_Coach_ForkScan").Translate();
        }
        else if (here.Kind is DiveElementKind.Files or DiveElementKind.Memory && !here.Looted)
        {
            step = here.Passage ? Step.Passage : here.Cache ? Step.Cache : Step.Files;
            action = "Download";
            text = (here.Passage ? "NR_Coach_Passage" : here.Cache ? "NR_Coach_Cache" : "NR_Coach_Files").Translate();
        }
        else if (here.Kind == DiveElementKind.Exit)
        {
            step = Step.Exit;
            action = "Exit";
            text = "NR_Coach_Exit".Translate();
        }
        else if (next == null)
        {
            step = Step.None;
            text = string.Empty;
        }
        else if (next.Hidden)
        {
            step = Step.HiddenScan;
            action = "Scan";
            text = "NR_Coach_HiddenScan".Translate();
        }
        else if (next.Blocks)
        {
            action = "Hack";
            string chance = s.CurrentHackChance.ToStringPercent();
            if (next.Kind == DiveElementKind.Ice)
            {
                step = Step.Ice;
                text = "NR_Coach_Ice".Translate(chance, s.ShieldCharges);
            }
            else
            {
                step = Step.Gate;
                text = "NR_Coach_Gate".Translate(chance);
            }
        }
        else
        {
            bool first = here.Kind == DiveElementKind.Lobby;
            step = first ? Step.MoveFirst : Step.Move;
            action = "Move";
            text = (first ? "NR_Coach_MoveFirst" : "NR_Coach_Move").Translate();
        }

        // A second line about the clock: the turns running low matter more than the actions of this turn.
        int turnsLeft = System.Math.Max(0, s.MaxTurns - s.Turn + 1);
        if (step != Step.None && turnsLeft <= 3)
            text = text + "\n" + (string)"NR_Coach_TimeLow".Translate(turnsLeft);
        else if (step != Step.None && s.ActionsLeft <= 1)
            text = text + "\n" + (string)"NR_Coach_ActionsLow".Translate();
        return new Result { Step = step, Action = action, Text = text };
    }
}
