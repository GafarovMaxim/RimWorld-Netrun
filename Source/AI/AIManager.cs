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

    private List<NetAI> _ais = [];
    private int _nextId = 1;

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
            };
            _ais.Add(ai);
        }

        ai.Server = server;
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

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            _ais ??= [];
    }
}
