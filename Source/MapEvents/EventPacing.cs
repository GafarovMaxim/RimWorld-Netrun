using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>Threat scheduler and Netrun letter cap numbers (Defs/Pacing.xml; docs/BALANCE_PLAN.md 6 and 15.6, T1.4).</summary>
public class PacingSettingsDef : Def
{
    /// <summary>Chance a day of a network attack by Heat level: Shadow, Noticed, Tracked, Target, Red Zone.</summary>
    public List<float> threatChanceByLevel = [0f, 0.08f, 0.15f, 0.25f, 0.4f];
    /// <summary>At most one Netrun threat in this many days.</summary>
    public float ourThreatGapDays = 3f;
    /// <summary>No Netrun threat while a storyteller threat is on a home map, nor this many hours after.</summary>
    public float storytellerQuietHours = 24f;
    /// <summary>The first crossing of 50 and 75 Heat in a season: a warning, then a guaranteed attack this many hours later.</summary>
    public IntRange thresholdDelayHours = new(12, 36);
    /// <summary>Netrun choice letters a day; the rest wait in a queue of at most maxQueuedLetters.</summary>
    public int maxChoiceLettersPerDay = 2;
    public int maxQueuedLetters = 4;
}

/// <summary>
/// Paces Netrun's own events (T1.4). Network attacks come from a daily roll by Heat level and from the first crossing of
/// 50 and 75 Heat in a season, never during a storyteller threat or within 24 h after one, at most one in 3 days.
/// Netrun choice letters (AI events, news choices, the courier offer) go through a daily cap; vanilla, storyteller and
/// other mods' letters are never touched. Checked every 2500 ticks from MapEventsManager's tick.
/// </summary>
public class EventPacing : GameComponent, INetrunCleanable
{
    public const int CheckInterval = GenDate.TicksPerHour;
    private const int Never = -10_000_000;

    private static PacingSettingsDef S => NetrunDefOf.NR_Pacing;
    private static int Now => Find.TickManager.TicksGame;

    private int _lastStorytellerThreatTick = Never;
    private int _lastOurThreatTick = Never;
    private int _nextRollTick = -1;
    private int _thresholdDueTick = -1;
    private int _season50 = -1;
    private int _season75 = -1;
    private int _dayStartTick = Never;
    private int _sentToday;
    private List<QueuedLetter> _queue = [];
    // Not saved: Heat at the last check, to see a crossing; -1 right after loading.
    private float _prevHeat = -1f;

    public EventPacing(Game game)
    {
    }

    public static EventPacing Get() => GameComp<EventPacing>.Get();

    // ---- Threats ----

    public void NoteStorytellerThreat(IncidentDef def)
    {
        _lastStorytellerThreatTick = Now;
        if (Prefs.DevMode)
            Log.Message($"[Netrun] storyteller threat {def?.defName}: no Netrun threats for {S.storytellerQuietHours:0} h.");
    }

    public void NoteOurThreat() => _lastOurThreatTick = Now;

    /// <summary>Why a Netrun threat may not come now; null if it may.</summary>
    public string ThreatBlock()
    {
        int now = Now;
        foreach (Map map in Find.Maps)
            if (map.IsPlayerHome && map.dangerWatcher.DangerRating != StoryDanger.None)
            {
                _lastStorytellerThreatTick = now;
                return "storyteller threat on " + map;
            }
        int quiet = (int)(S.storytellerQuietHours * GenDate.TicksPerHour) - (now - _lastStorytellerThreatTick);
        if (quiet > 0)
            return $"quiet after a storyteller threat, {quiet.ToStringTicksToPeriod()} left";
        int gap = (int)(S.ourThreatGapDays * GenDate.TicksPerDay) - (now - _lastOurThreatTick);
        if (gap > 0)
            return $"gap between Netrun threats, {gap.ToStringTicksToPeriod()} left";
        return null;
    }

    public float ChancePerDay(HeatLevel level)
        => S.threatChanceByLevel.Count > (int)level ? S.threatChanceByLevel[(int)level] * NetrunMod.Settings.NetAttackFrequency : 0f;

    public void Check()
    {
        int now = Now;
        // Cheap: the danger rating is the game's own cache (recounted every 101 ticks at most).
        foreach (Map map in Find.Maps)
            if (map.IsPlayerHome && map.dangerWatcher.DangerRating != StoryDanger.None)
                _lastStorytellerThreatTick = now;

        CheckThresholds(now);
        if (_thresholdDueTick >= 0)
        {
            if (now >= _thresholdDueTick && ThreatBlock() == null)
            {
                _thresholdDueTick = -1;
                bool fired = FireAttack();
                if (Prefs.DevMode)
                    Log.Message("[Netrun] threshold attack: " + (fired ? "fired" : "nothing to hit, dropped"));
            }
        }
        else if (_nextRollTick < 0)
            _nextRollTick = now + GenDate.TicksPerDay;
        else if (now >= _nextRollTick)
        {
            _nextRollTick = now + GenDate.TicksPerDay;
            string result = Roll(false);
            if (Prefs.DevMode)
                Log.Message("[Netrun] threat roll: " + result);
        }

        ReleaseLetters(now);
    }

    private void CheckThresholds(int now)
    {
        float heat = HeatTracker.Get()?.Heat ?? 0f;
        float prev = _prevHeat;
        _prevHeat = heat;
        if (prev < 0f || NetrunMod.Settings.NetAttackFrequency <= 0f)
            return;
        int season = Find.TickManager.TicksAbs / GenDate.TicksPerQuadrum;
        bool crossed = false;
        if (heat >= HeatLevelUtility.TargetThreshold && prev < HeatLevelUtility.TargetThreshold && _season75 != season)
        {
            _season75 = season;
            crossed = true;
        }
        if (heat >= HeatLevelUtility.TrackedThreshold && prev < HeatLevelUtility.TrackedThreshold && _season50 != season)
        {
            _season50 = season;
            crossed = true;
        }
        if (!crossed || _thresholdDueTick >= 0 || !Find.Maps.Any(m => m.IsPlayerHome && Candidates(m).Any()))
            return;
        int hours = S.thresholdDelayHours.RandomInRange;
        _thresholdDueTick = now + hours * GenDate.TicksPerHour;
        HeatLevel level = HeatLevelUtility.LevelOf(heat);
        Find.LetterStack.ReceiveLetter("NR_ThreatWarnLabel".Translate(),
            "NR_ThreatWarn".Translate(level.Label(), S.thresholdDelayHours.min, S.thresholdDelayHours.max), LetterDefOf.ThreatSmall);
    }

    /// <summary>The daily roll; <paramref name="guaranteed"/> skips the chance (dev), never the no-overlap rule.</summary>
    public string Roll(bool guaranteed)
    {
        HeatLevel level = HeatTracker.Get()?.Level ?? HeatLevel.Shadow;
        float chance = ChancePerDay(level);
        if (!guaranteed && !Rand.Chance(chance))
            return $"{level} {chance:P0}: no attack today";
        string block = ThreatBlock();
        if (block != null)
            return $"{level}: blocked ({block})";
        return FireAttack() ? $"{level}: attack fired" : $"{level}: nothing to hit";
    }

    /// <summary>A random network attack that can hit a player home now; true if one fired.</summary>
    private bool FireAttack()
    {
        foreach (Map map in Find.Maps.Where(m => m.IsPlayerHome).InRandomOrder())
        {
            var options = Candidates(map).ToList();
            if (options.Count == 0)
                continue;
            (IncidentDef def, IncidentParms parms) = options.RandomElementByWeight(o => o.def.GetModExtension<NetAttackExtension>()?.weight ?? 1f);
            if (!def.Worker.TryExecute(parms))
                continue;
            // Worker.TryExecute does not record the fire (the storyteller does), so minRefireDays would never hold.
            map.StoryState.Notify_IncidentFired(new FiringIncident(def, null, parms));
            NoteOurThreat();
            return true;
        }
        return false;
    }

    /// <summary>Network attacks that can fire on this map now (Heat level, refire days, something to hit).</summary>
    public static IEnumerable<(IncidentDef def, IncidentParms parms)> Candidates(Map map)
    {
        foreach (IncidentDef def in DefDatabase<IncidentDef>.AllDefsListForReading)
        {
            if (def.Worker is not IncidentWorker_NetAttack)
                continue;
            if (map.StoryState.lastFireTicks.TryGetValue(def, out int last) && Now - last < def.minRefireDays * GenDate.TicksPerDay)
                continue;
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
            if (def.Worker.CanFireNow(parms))
                yield return (def, parms);
        }
    }

    // ---- Letters ----

    /// <summary>
    /// Sends a Netrun choice letter through the daily cap. <paramref name="optional"/>: losing the letter loses nothing
    /// (the choice also waits elsewhere), so a full queue may drop it. A letter with a timeout never waits past half of it.
    /// </summary>
    public static void SendChoice(Letter letter, bool optional = false)
    {
        EventPacing pacing = Get();
        if (pacing == null)
            Find.LetterStack.ReceiveLetter(letter);
        else
            pacing.Offer(letter, optional);
    }

    private int MaxPerDay => System.Math.Max(1, S.maxChoiceLettersPerDay);

    private void Offer(Letter letter, bool optional)
    {
        int now = Now;
        // Letters already waiting go first when a new day has freed slots.
        ReleaseLetters(now);
        ResetDay(now);
        int latest = Latest(letter, now);
        // Room today, or a deadline too close to wait for the next check: it comes at once.
        if (_sentToday < MaxPerDay || latest >= 0 && latest < now + CheckInterval)
        {
            Deliver(letter);
            return;
        }
        if (_queue.Count >= S.maxQueuedLetters)
            MakeRoom();
        _queue.Add(new QueuedLetter { letter = letter, queuedTick = now, latestTick = latest, optional = optional });
    }

    private static int Latest(Letter letter, int now)
        => letter is LetterWithTimeout { TimeoutActive: true } t ? now + (t.disappearAtTick - now) / 2 : -1;

    private void Deliver(Letter letter)
    {
        _sentToday++;
        Find.LetterStack.ReceiveLetter(letter);
    }

    private void ResetDay(int now)
    {
        if (now - _dayStartTick < GenDate.TicksPerDay)
            return;
        _dayStartTick = now;
        _sentToday = 0;
    }

    /// <summary>A full queue: the oldest optional letter drops; with none, the oldest letter comes now over the limit.</summary>
    private void MakeRoom()
    {
        int i = _queue.FindIndex(q => q.optional);
        if (i >= 0)
        {
            if (Prefs.DevMode)
                Log.Message($"[Netrun] letter queue full: dropped \"{_queue[i].letter.Label}\".");
            _queue.RemoveAt(i);
            return;
        }
        Letter first = _queue[0].letter;
        _queue.RemoveAt(0);
        Deliver(first);
    }

    private void ReleaseLetters(int now)
    {
        if (_queue.Count == 0)
            return;
        ResetDay(now);
        _queue.RemoveAll(Stale);
        // Deadlines first: a letter that would wait past half its timeout comes now, over the limit.
        foreach (QueuedLetter q in _queue.Where(q => q.latestTick >= 0 && q.latestTick < now + CheckInterval).ToList())
        {
            _queue.Remove(q);
            Deliver(q.letter);
        }
        while (_queue.Count > 0 && _sentToday < MaxPerDay)
        {
            Letter letter = _queue[0].letter;
            _queue.RemoveAt(0);
            Deliver(letter);
        }
    }

    /// <summary>Nothing left to answer: the timeout ran out, or a news choice was answered in the news line.</summary>
    private static bool Stale(QueuedLetter q)
        => q?.letter == null
           || q.letter is LetterWithTimeout { TimeoutPassed: true }
           || q.letter is NewsChoiceLetter nl && NewsChoices.Get()?.Pending.Any(c => c.Id == nl.ChoiceId) != true;

    // ---- Dev ----

    public List<string> ThreatReport()
    {
        int now = Now;
        HeatTracker heat = HeatTracker.Get();
        HeatLevel level = heat?.Level ?? HeatLevel.Shadow;
        var lines = new List<string>
        {
            $"Heat {heat?.Heat ?? 0f:0.#} ({level}), attack chance {ChancePerDay(level):P0} a day (setting x{NetrunMod.Settings.NetAttackFrequency})",
            "now: " + (ThreatBlock() ?? "a Netrun threat may come"),
            "next roll in " + (_nextRollTick < 0 ? "(not set)" : (_nextRollTick - now).ToStringTicksToPeriod()),
            "last storyteller threat: " + (_lastStorytellerThreatTick <= Never ? "never" : (now - _lastStorytellerThreatTick).ToStringTicksToPeriod() + " ago"),
            "last Netrun threat: " + (_lastOurThreatTick <= Never ? "never" : (now - _lastOurThreatTick).ToStringTicksToPeriod() + " ago"),
            "threshold attack: " + (_thresholdDueTick < 0 ? "none" : "due in " + (_thresholdDueTick - now).ToStringTicksToPeriod()),
            $"season {Find.TickManager.TicksAbs / GenDate.TicksPerQuadrum}, 50 crossed in season {_season50}, 75 in season {_season75}",
        };
        foreach (Map map in Find.Maps.Where(m => m.IsPlayerHome))
            lines.Add($"{map}: danger {map.dangerWatcher.DangerRating}, can hit: " + string.Join(", ", Candidates(map).Select(c => c.def.defName)));
        return lines;
    }

    public List<string> LetterReport()
    {
        int now = Now;
        var lines = new List<string>
        {
            $"choice letters today {_sentToday}/{MaxPerDay}, day resets in {(_dayStartTick + GenDate.TicksPerDay - now).ToStringTicksToPeriod()}, queue {_queue.Count}/{S.maxQueuedLetters}",
        };
        foreach (QueuedLetter q in _queue)
            lines.Add($"  \"{q.letter?.Label}\" waited {(now - q.queuedTick).ToStringTicksToPeriod()}"
                      + (q.latestTick >= 0 ? ", comes at the latest in " + (q.latestTick - now).ToStringTicksToPeriod() : "")
                      + (q.optional ? ", optional" : ""));
        return lines;
    }

    public void DevSkipThreatCooldowns()
    {
        _lastStorytellerThreatTick = _lastOurThreatTick = Never;
    }

    public int Cleanup(CleanupReason reason)
    {
        int n = _queue.RemoveAll(Stale);
        while (_queue.Count > S.maxQueuedLetters)
        {
            MakeRoom();
            n++;
        }
        return n;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref _lastStorytellerThreatTick, "lastStorytellerThreatTick", Never);
        Scribe_Values.Look(ref _lastOurThreatTick, "lastOurThreatTick", Never);
        Scribe_Values.Look(ref _nextRollTick, "nextRollTick", -1);
        Scribe_Values.Look(ref _thresholdDueTick, "thresholdDueTick", -1);
        Scribe_Values.Look(ref _season50, "season50", -1);
        Scribe_Values.Look(ref _season75, "season75", -1);
        Scribe_Values.Look(ref _dayStartTick, "dayStartTick", Never);
        Scribe_Values.Look(ref _sentToday, "sentToday");
        Scribe_Collections.Look(ref _queue, "letterQueue", LookMode.Deep);
        _queue ??= [];
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            _queue.RemoveAll(q => q?.letter == null);
    }

    private class QueuedLetter : IExposable
    {
        public Letter letter;
        public int queuedTick;
        /// <summary>-1: no deadline.</summary>
        public int latestTick = -1;
        public bool optional;

        public void ExposeData()
        {
            Scribe_Deep.Look(ref letter, "letter");
            Scribe_Values.Look(ref queuedTick, "queuedTick");
            Scribe_Values.Look(ref latestTick, "latestTick", -1);
            Scribe_Values.Look(ref optional, "optional");
        }
    }
}

/// <summary>
/// Notes every threat that fires on a player home (ThreatBig and ThreatSmall: raids, sieges, mech clusters, manhunters):
/// a Netrun incident counts as our own threat, anything else as the storyteller's. One check per executed incident.
/// </summary>
[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
public static class Patch_IncidentWorker_TryExecute_Threats
{
    static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result)
    {
        try
        {
            IncidentDef def = __instance.def;
            if (!__result || def == null || parms?.target is not Map { IsPlayerHome: true }
                || def.category != IncidentCategoryDefOf.ThreatBig && def.category != IncidentCategoryDefOf.ThreatSmall)
                return;
            EventPacing pacing = EventPacing.Get();
            if (def.defName.StartsWith("NR_"))
                pacing?.NoteOurThreat();
            else
                pacing?.NoteStorytellerThreat(def);
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Patch_IncidentWorker_TryExecute_Threats: " + e.Message, 0x4E525414);
        }
    }
}
