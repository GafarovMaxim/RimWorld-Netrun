using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>What a "Now" rule looks at.</summary>
public enum CoreAdviceCondition
{
    AIEscape,
    CounterHack,
    ContractDue,
    HeatHigh,
    HeatNearNext,
    AIRequest,
    RawRecordings,
    FreeNode,
    Nothing,
}

/// <summary>Where a quick button of the "Now" block leads.</summary>
public enum CoreAdviceButton
{
    Scrub,
    Club,
    Board,
    Server,
    Studio,
    Gateway,
    HeatScreen,
}

/// <summary>
/// One rule of the Core's "Now" block (docs/CORE_V3.md): the highest-priority rule whose condition holds
/// shows its title and text (label and description, formatted with the condition's values) and up to two buttons.
/// </summary>
public class CoreAdviceDef : Def
{
    public CoreAdviceCondition condition;
    public int priority;
    /// <summary>ContractDue: hours left; HeatNearNext: Heat points to the next level.</summary>
    public float threshold = 10f;
    /// <summary>Bad, Warn, Ok, Accent or Purple.</summary>
    public string color = "Accent";
    public List<CoreAdviceButton> buttons = [];
    /// <summary>The body text (not description: other mods append to descriptions).</summary>
    [MustTranslate]
    public string text;

    public Color Color => color switch
    {
        "Bad" => NetrunUI.Bad,
        "Warn" => NetrunUI.Warn,
        "Ok" => NetrunUI.Ok,
        "Purple" => NetrunUI.Purple,
        _ => NetrunUI.Accent,
    };

    /// <summary>The first rule that applies now, with the values for its text.</summary>
    public static (CoreAdviceDef def, NamedArgument[] args) Pick(Map map, HeatTracker tracker)
    {
        foreach (CoreAdviceDef def in DefDatabase<CoreAdviceDef>.AllDefs.OrderByDescending(d => d.priority))
        {
            NamedArgument[] args = def.Check(map, tracker);
            if (args != null)
                return (def, args);
        }
        return (null, null);
    }

    /// <summary>Null when the condition does not hold; otherwise the text values.</summary>
    private NamedArgument[] Check(Map map, HeatTracker tracker)
    {
        int now = Find.TickManager.TicksGame;
        switch (condition)
        {
            case CoreAdviceCondition.AIEscape:
            {
                NetAI ai = AIManager.Get()?.AIs.FirstOrDefault(a => a.Escaping);
                return ai == null ? null : [ai.Name, (ai.EscapeAtTick - now).ToStringTicksToPeriod()];
            }
            case CoreAdviceCondition.CounterHack:
                return MapEventsManager.Get()?.CounterHackRunning == true ? [] : null;
            case CoreAdviceCondition.ContractDue:
            {
                Contract c = ContractManager.Get()?.Active.Where(x => x.TicksLeft < threshold * GenDate.TicksPerHour).OrderBy(x => x.TicksLeft).FirstOrDefault();
                return c == null ? null : [c.Title, c.TicksLeft.ToStringTicksToPeriod()];
            }
            case CoreAdviceCondition.HeatHigh:
                return tracker.Level >= HeatLevel.Target ? [tracker.Heat.ToString("0"), tracker.Level.Label()] : null;
            case CoreAdviceCondition.HeatNearNext:
            {
                if (tracker.Level == HeatLevel.RedZone)
                    return null;
                HeatLevel next = tracker.Level + 1;
                float left = next.MinHeat() - tracker.Heat;
                return left <= threshold ? [tracker.Heat.ToString("0"), next.Label(), left.ToString("0")] : null;
            }
            case CoreAdviceCondition.AIRequest:
            {
                NetAI ai = AIManager.Get()?.AIs.FirstOrDefault(a => a.HasRequest);
                return ai == null ? null : [ai.Name, AIManager.RequestText(ai)];
            }
            case CoreAdviceCondition.RawRecordings:
            {
                int raws = BraindanceEditing.RawRecordings(map).Count();
                return raws > 0 ? [raws] : null;
            }
            case CoreAdviceCondition.FreeNode:
            {
                int nodes = GatewayManager.Get()?.Nodes.Count(n => !n.Hidden) ?? 0;
                Pawn runner = GatewayManager.Runner(map, out _);
                return nodes > 0 && runner != null ? [nodes, runner.LabelShort] : null;
            }
            default:
                return [];
        }
    }

    public static string ButtonLabel(CoreAdviceButton button) => ("NR_AdviceButton_" + button).Translate();
}
