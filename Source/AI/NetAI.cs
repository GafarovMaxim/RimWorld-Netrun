using UnityEngine;
using Verse;

namespace Netrun;

public enum AIRequestKind
{
    None,
    Deliver,
    Silence,
    Attention,
}

/// <summary>A networked AI the colony owns. It lives in an AI server or sleeps inside a fragment.</summary>
public class NetAI : IExposable
{
    public int Id;
    public string Name;
    public AIPersonality Personality;
    public int AwakenedTick;

    /// <summary>The server hosting this AI; null while it sleeps in a fragment.</summary>
    public Building Server;

    public float Trust;
    public float Hunger;
    public bool Isolated;
    public int ServiceReadyTick;

    public AIRequestKind Request;
    public int RequestAmount;
    public int RequestStartTick;
    public int RequestDeadlineTick;
    public int NextRequestTick;

    public bool InServer => Server != null && !Server.Destroyed;
    public bool Powered => InServer && Server.GetComp<CompAIServer>()?.Powered == true;

    /// <summary>Awake and connected: services, hunger and requests only run in this state.</summary>
    public bool Active => Powered && !Isolated;

    public bool ServiceReady => Find.TickManager.TicksGame >= ServiceReadyTick;
    public int ServiceTicksLeft => Mathf.Max(0, ServiceReadyTick - Find.TickManager.TicksGame);
    public bool HasRequest => Request != AIRequestKind.None;

    public void AddTrust(float amount) => Trust = Mathf.Clamp(Trust + amount, 0f, 100f);
    public void AddHunger(float amount) => Hunger = Mathf.Clamp(Hunger + amount, 0f, 100f);

    public string LocationText
    {
        get
        {
            if (!InServer)
                return "NR_AILocationFragment".Translate();
            if (!Powered)
                return "NR_AILocationServerOff".Translate(Server.LabelCap);
            if (Isolated)
                return "NR_AILocationIsolated".Translate(Server.LabelCap);
            return "NR_AILocationServer".Translate(Server.LabelCap);
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id");
        Scribe_Values.Look(ref Name, "name");
        Scribe_Values.Look(ref Personality, "personality");
        Scribe_Values.Look(ref AwakenedTick, "awakenedTick");
        Scribe_References.Look(ref Server, "server");
        Scribe_Values.Look(ref Trust, "trust", 50f);
        Scribe_Values.Look(ref Hunger, "hunger");
        Scribe_Values.Look(ref Isolated, "isolated");
        Scribe_Values.Look(ref ServiceReadyTick, "serviceReadyTick");
        Scribe_Values.Look(ref Request, "request");
        Scribe_Values.Look(ref RequestAmount, "requestAmount");
        Scribe_Values.Look(ref RequestStartTick, "requestStartTick");
        Scribe_Values.Look(ref RequestDeadlineTick, "requestDeadlineTick");
        Scribe_Values.Look(ref NextRequestTick, "nextRequestTick");
    }
}
