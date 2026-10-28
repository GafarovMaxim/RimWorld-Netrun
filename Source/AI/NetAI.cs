using System.Collections.Generic;
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

    /// <summary>The server hosting this AI; null while it sleeps in a fragment or rides in a host.</summary>
    public Building Server;

    /// <summary>A colonist carrying this AI in an AI socket implant.</summary>
    public Pawn Host;

    public float Trust;
    public float Hunger;
    /// <summary>Fed by itself from storage once hungry enough (Maxim, 2026-10-23: feeding by hand was a chore).</summary>
    public bool AutoFeed;
    /// <summary>Not saved: no new auto-feed order before this tick, so one hauler is not sent twice.</summary>
    public int AutoFeedRetryTick;
    public bool Isolated;
    public int ServiceReadyTick;

    public AIRequestKind Request;
    public int RequestAmount;
    /// <summary>The last few requests and how they ended ("✓ ..." / "✗ ..."), newest last.</summary>
    public List<string> RequestHistory = [];
    public int RequestStartTick;
    public int RequestDeadlineTick;
    public int NextRequestTick;

    public int UnpoweredSinceTick = -1;
    /// <summary>Since when no fragment holds this unhoused AI (NetrunJanitor), -1 while it is accounted for.</summary>
    public int OrphanSinceTick = -1;
    public int EscapeAtTick = -1;

    // Seed growth: how often it was fed toward each personality.
    public int SeedTowardDataHunter;
    public int SeedTowardMuse;
    public int SeedTowardPredator;
    public int SeedTowardGhost;
    public int SeedLastQuietDayTick;

    public int NextEventTick;

    /// <summary>0-2 traits; the first is seen at trust 25, the second at 60, and stays seen.</summary>
    public List<AITrait> Traits = [];
    public int TraitsRevealed;

    // AI quests (docs/CONCEPTS_10.md, section 16): level 1-3, quests done, the next offer, rewards for good.
    public int Level = 1;
    public int QuestsDone;
    public int NextQuestTick = -1;
    /// <summary>Warden's drills: +1 node defense for good.</summary>
    public bool Drilled;
    /// <summary>Doc's patient: the service heals 50% more for good.</summary>
    public bool DocBoost;
    /// <summary>Echo: thingIDNumber of the one who killed its colonist, -1 unknown.</summary>
    public int EchoKillerId = -1;
    /// <summary>A quest (AIQuestKind) offered as soon as it awakens: the "Last node" scenario's Echo. -1 for none.</summary>
    public int StartQuest = -1;
    public int SecondServiceReadyTick;

    public bool InServer => Server != null && !Server.Destroyed;
    public bool InHost => Host != null && !Host.Dead;
    public bool InCoprocessor => InServer && Server.GetComp<CompAIServer>()?.IsCoprocessor == true;
    public bool InMainServer => InServer && !InCoprocessor;
    public bool Housed => InServer || InHost;

    /// <summary>A server needs power; a living host always keeps the AI running.</summary>
    public bool Powered => InHost || (InServer && Server.GetComp<CompAIServer>()?.Powered == true);
    /// <summary>1 normally, lower while the main server runs cooled: scales service cooldown and hunger.</summary>
    public float CoolFactor => InMainServer ? Server.GetComp<CompAIServer>()?.CoolFactor ?? 1f : 1f;
    public bool Escaping => EscapeAtTick >= 0;

    /// <summary>Where messages and drops about this AI should point.</summary>
    public Thing Anchor => InHost ? Host : Server;

    /// <summary>Awake and connected: services, hunger and requests only run in this state.</summary>
    public bool Active => Powered && !Isolated;

    public bool ServiceReady => Find.TickManager.TicksGame >= ServiceReadyTick;
    public int ServiceTicksLeft => Mathf.Max(0, ServiceReadyTick - Find.TickManager.TicksGame);
    public bool HasRequest => Request != AIRequestKind.None;
    public int SeedGrowth => SeedTowardDataHunter + SeedTowardMuse + SeedTowardPredator + SeedTowardGhost;

    /// <summary>Gains grow 25% faster for a colony of the Machine Revelation (Ideology).</summary>
    public void AddTrust(float amount)
        => Trust = Mathf.Clamp(Trust + (amount > 0f ? amount * NetIdeo.TrustGainFactor * (Traits.Contains(AITrait.Remembering) ? 1.1f : 1f) : amount), 0f, 100f);
    public void AddHunger(float amount) => Hunger = Mathf.Clamp(Hunger + amount, 0f, 100f);

    public string LocationText
    {
        get
        {
            if (InHost)
                return Isolated ? "NR_AILocationHostIsolated".Translate(Host.LabelShort) : "NR_AILocationHost".Translate(Host.LabelShort);
            if (!InServer)
                return "NR_AILocationFragment".Translate();
            if (!Powered)
                return "NR_AILocationServerOff".Translate(Server.LabelCap);
            if (InCoprocessor)
                return Isolated ? "NR_AILocationIsolated".Translate(Server.LabelCap) : "NR_AILocationCoprocessor".Translate(Server.LabelCap);
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
        Scribe_References.Look(ref Host, "host");
        Scribe_Values.Look(ref Trust, "trust", 50f);
        Scribe_Values.Look(ref Hunger, "hunger");
        Scribe_Values.Look(ref AutoFeed, "autoFeed");
        Scribe_Values.Look(ref Isolated, "isolated");
        Scribe_Values.Look(ref ServiceReadyTick, "serviceReadyTick");
        Scribe_Values.Look(ref Request, "request");
        Scribe_Values.Look(ref RequestAmount, "requestAmount");
        Scribe_Values.Look(ref RequestStartTick, "requestStartTick");
        Scribe_Values.Look(ref RequestDeadlineTick, "requestDeadlineTick");
        Scribe_Values.Look(ref NextRequestTick, "nextRequestTick");
        Scribe_Values.Look(ref UnpoweredSinceTick, "unpoweredSinceTick", -1);
        Scribe_Values.Look(ref OrphanSinceTick, "orphanSinceTick", -1);
        Scribe_Values.Look(ref EscapeAtTick, "escapeAtTick", -1);
        Scribe_Values.Look(ref SeedTowardDataHunter, "seedTowardDataHunter");
        Scribe_Values.Look(ref SeedTowardMuse, "seedTowardMuse");
        Scribe_Values.Look(ref SeedTowardPredator, "seedTowardPredator");
        Scribe_Values.Look(ref SeedTowardGhost, "seedTowardGhost");
        Scribe_Values.Look(ref SeedLastQuietDayTick, "seedLastQuietDayTick");
        Scribe_Values.Look(ref NextEventTick, "nextEventTick");
        Scribe_Collections.Look(ref RequestHistory, "requestHistory", LookMode.Value);
        Scribe_Collections.Look(ref Traits, "traits", LookMode.Value);
        Scribe_Values.Look(ref TraitsRevealed, "traitsRevealed");
        Scribe_Values.Look(ref Level, "level", 1);
        Scribe_Values.Look(ref QuestsDone, "questsDone");
        Scribe_Values.Look(ref NextQuestTick, "nextQuestTick", -1);
        Scribe_Values.Look(ref Drilled, "drilled");
        Scribe_Values.Look(ref DocBoost, "docBoost");
        Scribe_Values.Look(ref EchoKillerId, "echoKiller", -1);
        Scribe_Values.Look(ref StartQuest, "startQuest", -1);
        Scribe_Values.Look(ref SecondServiceReadyTick, "secondServiceReady");
        RequestHistory ??= [];
        Traits ??= [];
    }
}
