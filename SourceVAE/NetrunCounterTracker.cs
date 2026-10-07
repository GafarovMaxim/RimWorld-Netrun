using System.Linq;
using System.Reflection;
using AchievementsExpanded;
using HarmonyLib;
using Netrun;
using Verse;

namespace NetrunVAE;

/// <summary>
/// One tracker for every Netrun achievement: unlocks when a colony counter (Netrun.NetrunSignals.Counter) reaches the
/// count. VAE patches the signal method itself through MethodHook.
/// </summary>
public class NetrunCounterTracker : TrackerBase
{
    public string key;
    public int count = 1;

    public override string Key
    {
        get => "NetrunCounterTracker";
        set { }
    }

    public override MethodInfo MethodHook => AccessTools.Method(typeof(NetrunSignals), nameof(NetrunSignals.Counter));

    public override MethodInfo PatchMethod => AccessTools.Method(typeof(NetrunCounterTracker), nameof(CounterPostfix));

    protected override string[] DebugText => [$"Key: {key}", $"Count: {count}"];

    public NetrunCounterTracker()
    {
    }

    public NetrunCounterTracker(NetrunCounterTracker reference) : base(reference)
    {
        key = reference.key;
        count = reference.count;
    }

    public override bool UnlockOnStartup => Trigger(key, NetrunSignals.Value(key));

    public override (float percent, string text) PercentComplete
        => count > 1 ? (System.Math.Min(1f, NetrunSignals.Value(key) / (float)count), $"{System.Math.Min(NetrunSignals.Value(key), count)} / {count}") : base.PercentComplete;

    public bool Trigger(string signalKey, int value)
    {
        base.Trigger($"Key: {signalKey} Value: {value}");
        return signalKey == key && value >= count;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref key, "key");
        Scribe_Values.Look(ref count, "count", 1);
    }

    /// <summary>Harmony postfix on NetrunSignals.Counter.</summary>
    public static void CounterPostfix(string key, int value)
    {
        foreach (AchievementCard card in AchievementPointManager.GetCards<NetrunCounterTracker>().ToList())
        {
            try
            {
                if (card.tracker is NetrunCounterTracker tracker && tracker.Trigger(key, value))
                    card.UnlockCard();
            }
            catch (System.Exception e)
            {
                Log.Warning("[Netrun] achievement check failed: " + e.Message);
            }
        }
    }
}
