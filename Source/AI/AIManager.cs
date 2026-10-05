using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>All AIs the colony has awakened, wherever they currently are.</summary>
public class AIManager : GameComponent
{
    private static readonly string[] Names =
    [
        "Marlowe", "Case", "Moth", "Sable", "Switchback", "Kestrel", "Halcyon", "Vesper",
        "Juno", "Orpheus", "Lumen", "Cipher", "Wren", "Tallis", "Nyx", "Basilisk",
    ];

    private const int TickInterval = GenDate.TicksPerHour;

    private List<NetAI> _ais = [];
    private int _nextId = 1;
    private int _lastHackTick = -1;

    public AIManager(Game game) { }

    public static AIManager Get() => Current.Game?.GetComponent<AIManager>();

    public IReadOnlyList<NetAI> AIs => _ais;
    public NetAI ById(int id) => _ais.Find(a => a.Id == id);
    public IEnumerable<NetAI> InServer(Building server) => _ais.Where(a => a.Server == server);

    private static AISettingsDef Settings => NetrunDefOf.NR_AI;

    /// <summary>Wakes the AI stored in a fragment (or a brand new one) and moves it into the server.</summary>
    public NetAI Awaken(Thing fragment, Building server, Pawn worker)
    {
        int storedId = fragment.TryGetComp<CompAIFragment>()?.StoredAIId ?? -1;
        NetAI ai = storedId >= 0 ? ById(storedId) : null;

        if (ai == null)
        {
            ai = new NetAI
            {
                Id = _nextId++,
                Name = $"{Names.RandomElement()}-{Rand.Range(10, 99)}",
                Personality = AIPersonalityUtility.RandomFromFragment(),
                AwakenedTick = Find.TickManager.TicksGame,
                Trust = Settings.startTrust,
            };
            _ais.Add(ai);
        }

        ai.Server = server;
        ai.NextRequestTick = Find.TickManager.TicksGame + Settings.requestIntervalDays.RandomInRange * GenDate.TicksPerDay;
        fragment.Destroy();

        Find.LetterStack.ReceiveLetter(
            "NR_AIAwakenedLabel".Translate(ai.Name),
            "NR_AIAwakened".Translate(ai.Name, ai.Personality.Label(), ai.Personality.Description(), worker.LabelShort),
            LetterDefOf.PositiveEvent, new LookTargets(server));

        TryDetectAwakening(ai);
        return ai;
    }

    /// <summary>Waking an AI is loud: the higher the Heat, the likelier someone notices.</summary>
    private static void TryDetectAwakening(NetAI ai)
    {
        HeatTracker heat = HeatTracker.Get();
        if (heat == null || !Rand.Chance(heat.Heat * Settings.awakenDetectChancePerHeat))
            return;

        string text = "NR_AIAwakeningDetected".Translate(ai.Name);
        heat.AddHeat(Settings.awakenDetectHeat, text);
        Messages.Message(text, MessageTypeDefOf.NegativeEvent, historical: true);
    }

    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % TickInterval != 0)
            return;

        const float dayFraction = (float)TickInterval / GenDate.TicksPerDay;
        int now = Find.TickManager.TicksGame;
        bool quiet = _lastHackTick < 0 || now - _lastHackTick > GenDate.TicksPerDay;

        foreach (NetAI ai in _ais.Where(a => a.InServer && a.Powered).ToList())
        {
            // Hunger: Ghost is calmed by silence, everyone else just gets hungry. Isolation makes it worse.
            float hunger = ai.Personality == AIPersonality.Ghost
                ? (quiet ? -Settings.ghostCalmPerDay : 0f)
                : Settings.hungerPerDay;
            if (ai.Isolated && hunger > 0f)
                hunger *= Settings.isolatedHungerFactor;
            ai.AddHunger(hunger * dayFraction);

            if (ai.Hunger >= Settings.starvingHunger)
                ai.AddTrust(-Settings.starvingTrustPerDay * dayFraction);
            if (ai.Isolated)
                ai.AddTrust(-Settings.isolatedTrustPerDay * dayFraction);

            if (ai.Active)
                UpdateRequest(ai, now);
        }
    }

    /// <summary>Called once per hack completed by a colonist.</summary>
    public void NotifyHack()
    {
        _lastHackTick = Find.TickManager.TicksGame;
        foreach (NetAI ai in _ais.Where(a => a.Personality == AIPersonality.Ghost && a.Active))
            ai.AddHunger(Settings.ghostHungerPerHack);
    }

    private void UpdateRequest(NetAI ai, int now)
    {
        if (!ai.HasRequest)
        {
            if (now >= ai.NextRequestTick)
                IssueRequest(ai, now);
            return;
        }

        if (now < ai.RequestDeadlineTick)
            return;

        // A silence request is won by waiting it out without hacks.
        if (ai.Request == AIRequestKind.Silence && (_lastHackTick < ai.RequestStartTick))
            CompleteRequest(ai);
        else
            FailRequest(ai);
    }

    private void IssueRequest(NetAI ai, int now)
    {
        ai.Request = ai.Personality switch
        {
            AIPersonality.Ghost => AIRequestKind.Silence,
            AIPersonality.Trickster => AIRequestKind.Attention,
            _ => AIRequestKind.Deliver,
        };
        ai.RequestAmount = (AIBehavior.Food(ai.Personality)?.count ?? 0) * Settings.requestFeedMultiplier;
        ai.RequestStartTick = now;
        ai.RequestDeadlineTick = now + Settings.requestDays * GenDate.TicksPerDay;

        Find.LetterStack.ReceiveLetter(
            "NR_AIRequestLabel".Translate(ai.Name),
            "NR_AIRequest".Translate(ai.Name, RequestText(ai), Settings.requestDays),
            LetterDefOf.NeutralEvent, new LookTargets(ai.Server));
    }

    public static string RequestText(NetAI ai) => ai.Request switch
    {
        AIRequestKind.Deliver => "NR_AIRequestDeliver".Translate(ai.RequestAmount, AIBehavior.Food(ai.Personality)?.thing.label ?? "?"),
        AIRequestKind.Silence => "NR_AIRequestSilence".Translate(NetrunDefOf.NR_AI.requestDays),
        AIRequestKind.Attention => "NR_AIRequestAttention".Translate(),
        _ => string.Empty,
    };

    public AcceptanceReport CanFulfil(NetAI ai, Map map)
    {
        if (ai.Request != AIRequestKind.Deliver || AIBehavior.Food(ai.Personality) is not { } food)
            return "NR_AIRequestNotDeliverable".Translate();
        if (ColonyGoods.Count(map, food.thing) < ai.RequestAmount)
            return "NR_AINoFood".Translate(ai.RequestAmount, food.thing.label);
        return true;
    }

    public void Fulfil(NetAI ai, Map map)
    {
        if (!CanFulfil(ai, map).Accepted || AIBehavior.Food(ai.Personality) is not { } food)
            return;

        ColonyGoods.TryTake(map, food.thing, ai.RequestAmount);
        ai.AddHunger(-100f);
        CompleteRequest(ai);
    }

    public void CompleteRequest(NetAI ai)
    {
        ai.Request = AIRequestKind.None;
        ai.AddTrust(Settings.requestTrust);
        ai.NextRequestTick = Find.TickManager.TicksGame + Settings.requestIntervalDays.RandomInRange * GenDate.TicksPerDay;
        Messages.Message("NR_AIRequestDone".Translate(ai.Name), MessageTypeDefOf.PositiveEvent, historical: true);
    }

    private void FailRequest(NetAI ai)
    {
        ai.Request = AIRequestKind.None;
        ai.AddTrust(-Settings.requestTrust);
        ai.NextRequestTick = Find.TickManager.TicksGame + Settings.requestIntervalDays.RandomInRange * GenDate.TicksPerDay;
        Messages.Message("NR_AIRequestFailed".Translate(ai.Name), MessageTypeDefOf.NegativeEvent, historical: true);
    }

    /// <summary>Server destroyed: its AIs fall back into fragments next to the ruins.</summary>
    public void OnServerDestroyed(Building server, Map map, IntVec3 position)
    {
        foreach (NetAI ai in InServer(server).ToList())
        {
            ai.Server = null;
            if (map == null)
                continue;

            Thing fragment = ThingMaker.MakeThing(NetrunDefOf.NR_AIFragment);
            fragment.TryGetComp<CompAIFragment>().StoredAIId = ai.Id;
            GenPlace.TryPlaceThing(fragment, position, map, ThingPlaceMode.Near);
            Messages.Message("NR_AIFellBackToFragment".Translate(ai.Name), new LookTargets(fragment), MessageTypeDefOf.NegativeEvent, historical: true);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref _ais, "ais", LookMode.Deep);
        Scribe_Values.Look(ref _nextId, "nextId", 1);
        Scribe_Values.Look(ref _lastHackTick, "lastHackTick", -1);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            _ais ??= [];
    }
}
