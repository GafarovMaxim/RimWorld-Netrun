using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>One line of cyberspace news that happened (not a state): kept a few days in the news line.</summary>
public class NetHeadline : IExposable
{
    public int Tick;
    public string Text;
    /// <summary>0 neutral, 1 good for the colony, 2 bad.</summary>
    public int Tone;

    public Color Color => Tone == 1 ? NetrunUI.Ok : Tone == 2 ? NetrunUI.Bad : NetrunUI.Purple;

    public void ExposeData()
    {
        Scribe_Values.Look(ref Tick, "tick");
        Scribe_Values.Look(ref Text, "text");
        Scribe_Values.Look(ref Tone, "tone");
    }
}

/// <summary>
/// Living cyberspace (docs/CONCEPTS_7.md, part 3): every few days something happens in the net and it has a real
/// effect: a node hardens its ICE or has its codes leaked, data floods the market, the net goes quiet. Clearing the
/// corporate core puts the corp nodes on lockdown; a runaway AI hides in a node you can dive into.
/// </summary>
public partial class GatewayManager
{
    public List<NetHeadline> Headlines = [];
    private int _nextHeadlineTick = -1;
    private int _glutUntilTick = -1;
    private int _quietUntilTick = -1;
    private int _lockdownUntilTick = -1;

    public bool GlutActive => Find.TickManager.TicksGame < _glutUntilTick;
    public bool QuietActive => Find.TickManager.TicksGame < _quietUntilTick;
    public bool LockdownActive => Find.TickManager.TicksGame < _lockdownUntilTick;

    /// <summary>Cyberdata found in dives while data floods the market.</summary>
    public float LootFactor => GlutActive ? Settings.newsGlutLoot : 1f;
    /// <summary>A dive's trace while the net is quiet.</summary>
    public float TraceFactor => QuietActive ? Settings.newsQuietTrace : 1f;

    /// <summary>Headlines of the last few days, newest first.</summary>
    public IEnumerable<NetHeadline> RecentHeadlines
        => Headlines.Where(h => Find.TickManager.TicksGame - h.Tick < Settings.newsKeepDays * GenDate.TicksPerDay).OrderByDescending(h => h.Tick);

    public override void GameComponentTick()
    {
        int now = Find.TickManager.TicksGame;
        if (now % GenDate.TicksPerHour != 0 || !NetworkGate.IsOpen || NetrunMod.Settings.CyberEventFrequency <= 0f)
            return;
        if (_nextHeadlineTick < 0)
            ScheduleHeadline();
        else if (now >= _nextHeadlineTick)
        {
            RollHeadline();
            ScheduleHeadline();
        }
    }

    private void ScheduleHeadline()
        => _nextHeadlineTick = Find.TickManager.TicksGame
           + (int)(Settings.newsIntervalDays.RandomInRange * GenDate.TicksPerDay / NetrunSettings.Divisor(NetrunMod.Settings.CyberEventFrequency));

    public void AddHeadline(string text, int tone)
    {
        Headlines.Add(new NetHeadline { Tick = Find.TickManager.TicksGame, Text = text, Tone = tone });
        if (Headlines.Count > 12)
            Headlines.RemoveAt(0);
        NetrunSound.Play("UI_Notify");
    }

    /// <summary>A node anyone may change: visible, not the boss, not where a runner is now.</summary>
    private IEnumerable<ExternalNode> OpenNodes
        => Nodes.Where(n => !n.Hidden && !n.IsBoss && !n.CounterHack && (Session == null || n.Id != Session.Node.Id));

    /// <summary>Something happens in the net, with a real effect.</summary>
    public void RollHeadline()
    {
        int now = Find.TickManager.TicksGame;
        var options = new List<System.Action>();
        if (OpenNodes.TryRandomElement(out ExternalNode harden))
        {
            options.Add(() =>
            {
                harden.Defense++;
                if (harden.BlackIce)
                    harden.IceStrength++;
                AddHeadline("NR_HeadlineHarden".Translate(harden.Name), 2);
            });
        }
        if (OpenNodes.Where(n => n.Defense > 1).TryRandomElement(out ExternalNode leak))
        {
            options.Add(() =>
            {
                leak.Defense--;
                AddHeadline("NR_HeadlineLeak".Translate(leak.Name), 1);
            });
        }
        if (!GlutActive)
        {
            options.Add(() =>
            {
                _glutUntilTick = now + (int)(Settings.newsGlutDays * GenDate.TicksPerDay);
                AddHeadline("NR_HeadlineGlut".Translate(Settings.newsGlutLoot.ToStringPercent(), Settings.newsGlutDays.ToString("0.#")), 1);
            });
        }
        if (!QuietActive)
        {
            options.Add(() =>
            {
                _quietUntilTick = now + (int)(Settings.newsQuietDays * GenDate.TicksPerDay);
                AddHeadline("NR_HeadlineQuiet".Translate(Settings.newsQuietTrace.ToStringPercent(), Settings.newsQuietDays.ToString("0.#")), 1);
            });
        }
        if (options.Count > 0)
            options.RandomElement()();
    }

    /// <summary>The corporate core fell: every corp node goes on lockdown (stronger ICE) for a while.</summary>
    public void CorpLockdown()
    {
        _lockdownUntilTick = Find.TickManager.TicksGame + (int)(Settings.newsLockdownDays * GenDate.TicksPerDay);
        foreach (ExternalNode node in Nodes.Where(n => n.Type == ExternalNodeType.Corp && (Session == null || n.Id != Session.Node.Id)))
            node.Defense++;
        AddHeadline("NR_HeadlineLockdown".Translate(Settings.newsLockdownDays.ToString("0")), 2);
    }

    /// <summary>A runaway AI hides in an abandoned node: a wild AI you can find by diving there.</summary>
    public void RunawayAI(string aiName)
    {
        ExternalNode node = ExternalNode.Generate(_nextId++);
        node.Type = ExternalNodeType.Abandoned;
        node.Hidden = node.Rival = node.Honeypot = node.CounterHack = false;
        node.WildAI = true;
        AddBranch(node, District.Ruins, District.Dump);
        AddHeadline("NR_HeadlineRunaway".Translate(aiName, node.Name), 0);
    }

    private void ExposeHeadlines()
    {
        Scribe_Collections.Look(ref Headlines, "headlines", LookMode.Deep);
        Scribe_Values.Look(ref _nextHeadlineTick, "nextHeadlineTick", -1);
        Scribe_Values.Look(ref _glutUntilTick, "glutUntilTick", -1);
        Scribe_Values.Look(ref _quietUntilTick, "quietUntilTick", -1);
        Scribe_Values.Look(ref _lockdownUntilTick, "lockdownUntilTick", -1);
        Headlines ??= [];
    }
}
