using System.Collections.Generic;
using System.Linq;
using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>Who an AI event is about: the AI, its map, a chosen colonist and the colony's best netrunner.</summary>
public class AIEventContext
{
    public NetAI AI;
    public Map Map;
    public Pawn Target;
    public Pawn Hacker;
    public AIEventDef Def;

    public static Pawn BestHacker(Map map)
        => map.mapPawns.FreeColonistsSpawned.Where(p => p.IsHacker() && !p.Downed)
            .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0).FirstOrDefault();

    public static AIEventContext Make(NetAI ai, Map map, AIEventDef def, Pawn target = null)
    {
        Pawn hacker = BestHacker(map);
        target ??= map.mapPawns.FreeColonistsSpawned.Where(p => p != ai.Host && !p.Downed).RandomElementWithFallback()
                   ?? map.mapPawns.FreeColonistsSpawned.RandomElementWithFallback();
        return new AIEventContext { AI = ai, Map = map, Target = target, Hacker = hacker, Def = def };
    }

    public string Format(string text)
        => text.NullOrEmpty() ? string.Empty : (string)text.Formatted(
            AI.Name.Named("AI"),
            (Target?.LabelShort ?? "-").Named("PAWN"),
            (Hacker?.LabelShort ?? "-").Named("HACKER"));

    public Thing Anchor => AI.Anchor;
    public IntVec3 Spot => Anchor is { Spawned: true } a ? a.Position : Target?.Position ?? Map.Center;
}

/// <summary>
/// Picks and runs AI events: once every 4-8 days per AI (NetAI.NextEventTick), at most one a day for the
/// colony, none while isolated. Also keeps what events leave behind: delayed outcomes, a defense bonus
/// against network attacks and a pause on mercenary raids.
/// </summary>
public class AIEventManager : GameComponent
{
    private int _lastEventTick = -GenDate.TicksPerDay;
    private float _defenseBonus;
    private int _defenseUntilTick;
    private int _mercBlockUntilTick;
    private List<PendingEffect> _pending = [];

    public AIEventManager(Game game)
    {
    }

    public static AIEventManager Get() => Current.Game?.GetComponent<AIEventManager>();

    public float DefenseBonus => Find.TickManager.TicksGame < _defenseUntilTick ? _defenseBonus : 0f;
    /// <summary>Dev: forget the colony-wide gap between AI events.</summary>
    public void DevResetGap() => _lastEventTick = -GenDate.TicksPerDay * 30;

    public bool MercsBlocked => Find.TickManager.TicksGame < _mercBlockUntilTick;

    public void AddDefense(float amount, float days)
    {
        _defenseBonus = DefenseBonus + amount;
        _defenseUntilTick = Find.TickManager.TicksGame + (int)(days * GenDate.TicksPerDay);
    }

    public void BlockMercs(float days)
        => _mercBlockUntilTick = System.Math.Max(_mercBlockUntilTick, Find.TickManager.TicksGame + (int)(days * GenDate.TicksPerDay));

    /// <summary>Called when an AI's own timer runs out. False: nothing fitting, the caller may fall back.</summary>
    public bool TryFire(NetAI ai)
    {
        int now = Find.TickManager.TicksGame;
        float gapHours = NetrunDefOf.NR_AI.colonyEventGapHours / NetrunMod.Settings.AIEventFrequency;
        if (ai.Isolated || now - _lastEventTick < gapHours * GenDate.TicksPerHour)
            return false;

        Map map = ai.Anchor?.MapHeld ?? Find.AnyPlayerHomeMap;
        if (map == null)
            return false;

        List<AIEventDef> candidates = DefDatabase<AIEventDef>.AllDefsListForReading.Where(d => d.CanFire(ai, map)).ToList();
        if (candidates.Count == 0)
            return false;

        // Own events first: a personality event wins over a general one twice as often.
        AIEventDef def = candidates.RandomElementByWeight(d => d.weight * (d.General ? 1f : 2f));
        _lastEventTick = now;
        EconomyTracker.Get()?.Note(aiEvent: true);
        Fire(def, AIEventContext.Make(ai, map, def));
        return true;
    }

    public void Fire(AIEventDef def, AIEventContext context)
    {
        foreach (AIEventEffect effect in def.effects)
            effect.Apply(context);

        string label = "NR_AIEventLabel".Translate(context.AI.Name, def.LabelCap);
        string text = context.Format(def.description);
        LookTargets look = context.Anchor is { Spawned: true } a ? new LookTargets(a) : LookTargets.Invalid;

        if (def.options.NullOrEmpty())
        {
            Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.NeutralEvent, look);
            return;
        }

        var letter = (ChoiceLetter_AIEvent)LetterMaker.MakeLetter(NetrunDefOf.NR_AIEventLetter);
        letter.Label = label;
        letter.Text = text;
        letter.lookTargets = look;
        letter.Setup(def, context);
        letter.StartTimeout((int)(NetrunDefOf.NR_AI.eventAnswerDays * GenDate.TicksPerDay));
        Find.LetterStack.ReceiveLetter(letter);
    }

    public static void Choose(AIEventDef def, int index, AIEventContext context)
    {
        if (def.options.NullOrEmpty())
            return;
        if (index < 0 || index >= def.options.Count)
            index = def.options.Count - 1;

        AIEventOption option = def.options[index];
        foreach (ThingDefCountClass c in option.cost)
            ColonyGoods.TryTake(context.Map, c.thingDef, c.count);
        if (option.needsFood && AIBehavior.Food(context.AI.Personality) is { } food)
            ColonyGoods.TryTake(context.Map, food.thing, food.count);
        foreach (AIEventEffect effect in option.effects)
            effect.Apply(context);

        if (!option.result.NullOrEmpty())
            Messages.Message(context.Format(option.result), context.Anchor is { Spawned: true } a ? new LookTargets(a) : LookTargets.Invalid,
                MessageTypeDefOf.NeutralEvent, historical: true);
    }

    public void AddPending(AIEffect_Delayed delayed, AIEventContext context)
    {
        _pending.Add(new PendingEffect
        {
            def = context.Def,
            key = delayed.key,
            aiId = context.AI.Id,
            target = context.Target,
            fireTick = Find.TickManager.TicksGame + (int)(delayed.days * GenDate.TicksPerDay),
        });
    }

    public override void GameComponentTick()
    {
        if (_pending.Count == 0 || Find.TickManager.TicksGame % GenTicks.TickRareInterval != 0)
            return;

        int now = Find.TickManager.TicksGame;
        foreach (PendingEffect pending in _pending.Where(p => now >= p.fireTick).ToList())
        {
            _pending.Remove(pending);
            NetAI ai = AIManager.Get()?.ById(pending.aiId);
            AIEffect_Delayed delayed = pending.def == null ? null : AIEffect_Delayed.Find(pending.def, pending.key);
            Map map = ai?.Anchor?.MapHeld ?? Find.AnyPlayerHomeMap;
            if (ai == null || delayed == null || map == null)
                continue;

            AIEventContext context = AIEventContext.Make(ai, map, pending.def, pending.target is { Dead: false, Spawned: true } t ? t : null);
            foreach (AIEventEffect effect in delayed.effects)
                effect.Apply(context);
            if (!delayed.result.NullOrEmpty())
                Messages.Message(context.Format(delayed.result), MessageTypeDefOf.NeutralEvent, historical: true);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref _lastEventTick, "lastEventTick", -GenDate.TicksPerDay);
        Scribe_Values.Look(ref _defenseBonus, "defenseBonus");
        Scribe_Values.Look(ref _defenseUntilTick, "defenseUntilTick");
        Scribe_Values.Look(ref _mercBlockUntilTick, "mercBlockUntilTick");
        Scribe_Collections.Look(ref _pending, "pending", LookMode.Deep);
        _pending ??= [];
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            _pending.RemoveAll(p => p?.def == null);
    }

    private class PendingEffect : IExposable
    {
        public AIEventDef def;
        public string key;
        public int aiId;
        public Pawn target;
        public int fireTick;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref aiId, "aiId");
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref fireTick, "fireTick");
        }
    }
}

/// <summary>An AI event letter with up to three choices; a timeout or dismissal counts as silence.</summary>
public class ChoiceLetter_AIEvent : ChoiceLetter
{
    private AIEventDef _def;
    private int _aiId;
    private Pawn _target;
    private bool _answered;

    public void Setup(AIEventDef def, AIEventContext context)
    {
        _def = def;
        _aiId = context.AI.Id;
        _target = context.Target;
    }

    private AIEventContext Context()
    {
        NetAI ai = AIManager.Get()?.ById(_aiId);
        Map map = ai?.Anchor?.MapHeld ?? Find.AnyPlayerHomeMap;
        return ai == null || map == null ? null : AIEventContext.Make(ai, map, _def, _target is { Dead: false, Spawned: true } t ? t : null);
    }

    public override IEnumerable<DiaOption> Choices
    {
        get
        {
            if (ArchivedOnly || _def == null)
            {
                yield return Option_Close;
                yield break;
            }

            AIEventContext context = Context();
            if (context == null)
            {
                yield return Option_Close;
                yield break;
            }

            for (int i = 0; i < _def.options.Count; i++)
            {
                int index = i;
                AIEventOption option = _def.options[i];
                var dia = new DiaOption(context.Format(option.label))
                {
                    action = () =>
                    {
                        _answered = true;
                        AIEventManager.Choose(_def, index, Context());
                        Find.LetterStack.RemoveLetter(this);
                    },
                    resolveTree = true,
                };
                AcceptanceReport report = option.Available(context);
                if (!report.Accepted)
                    dia.Disable(report.Reason);
                yield return dia;
            }
            if (lookTargets.IsValid())
                yield return Option_JumpToLocation;
        }
    }

    public override void Removed()
    {
        base.Removed();
        if (_answered || _def == null)
            return;
        _answered = true;

        AIEventContext context = Context();
        if (context == null)
            return;
        int index = _def.ignoreOption >= 0 && _def.ignoreOption < _def.options.Count ? _def.ignoreOption : _def.options.Count - 1;
        // Silence never pays a cost the colony cannot afford: fall back to the last option that is available.
        while (index >= 0 && !_def.options[index].Available(context).Accepted)
            index--;
        if (index < 0)
            return;
        Messages.Message("NR_AIEventIgnored".Translate(context.AI.Name), MessageTypeDefOf.NeutralEvent, historical: false);
        AIEventManager.Choose(_def, index, context);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Defs.Look(ref _def, "aiEventDef");
        Scribe_Values.Look(ref _aiId, "aiId");
        Scribe_References.Look(ref _target, "target");
        Scribe_Values.Look(ref _answered, "answered");
    }
}
