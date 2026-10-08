using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>All AIs the colony has awakened, wherever they currently are.</summary>
public class AIManager : GameComponent, INetrunCleanable
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

    public int LastHackTick => _lastHackTick;

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
        Map map = server.MapHeld ?? worker.MapHeld;

        // A fresh fragment costs cyberdata to coax awake and does not always make it (CONCEPTS_10, section 2).
        // An AI that already lived with the colony comes back for free.
        if (ai == null)
        {
            int cost = Settings.awakenCyberdataCost;
            if (cost > 0 && !ColonyGoods.TryTake(map, NetrunDefOf.USH_Cyberdata, cost))
            {
                Messages.Message("NR_AwakenNoCyberdata".Translate(cost), MessageTypeDefOf.RejectInput, historical: false);
                return null;
            }
            if (TryFailAwakening(fragment, server, worker, map))
                return null;
        }

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
            ai.Traits = AITraitUtility.Roll(ai.Personality);
            _ais.Add(ai);
        }

        ai.Server = server;
        ai.EscapeAtTick = -1;
        ai.UnpoweredSinceTick = -1;
        ai.NextRequestTick = Find.TickManager.TicksGame + RequestDelay(ai);
        fragment.Destroy();

        NetrunSound.Play("AI_Awaken");
        Find.LetterStack.ReceiveLetter(
            "NR_AIAwakenedLabel".Translate(ai.Name),
            "NR_AIAwakened".Translate(ai.Name, ai.Personality.Label(), ai.Personality.Description(), worker.LabelShort),
            LetterDefOf.PositiveEvent, new LookTargets(server));

        NetrunRecords.Add(worker, NetrunRecords.NR_Record_AIsAwakened);
        NetrunSignals.Increment("AIsAwakened");
        NetrunCounters.Get()?.MetPersonality(ai.Personality);
        TryDetectAwakening(ai);
        if (ai.StartQuest >= 0)
        {
            var kind = (AIQuestKind)ai.StartQuest;
            ai.StartQuest = -1;
            AIQuests.Get()?.Offer(ai, kind);
        }
        return ai;
    }

    /// <summary>
    /// The two ways a fresh awakening goes wrong: the fragment was an empty shell (it falls apart into a little
    /// cyberdata) or the AI wakes up and slips out into the net, where a dive can find it again.
    /// </summary>
    private bool TryFailAwakening(Thing fragment, Building server, Pawn worker, Map map)
    {
        float roll = Rand.Value;
        if (roll < Settings.awakenEmptyChance)
        {
            IntVec3 cell = server.PositionHeld;
            fragment.Destroy();
            if (Settings.awakenEmptyCyberdata > 0 && map != null)
            {
                Thing data = ThingMaker.MakeThing(NetrunDefOf.USH_Cyberdata);
                data.stackCount = Settings.awakenEmptyCyberdata;
                GenPlace.TryPlaceThing(data, cell, map, ThingPlaceMode.Near);
            }
            Find.LetterStack.ReceiveLetter("NR_AwakenEmptyLabel".Translate(), "NR_AwakenEmpty".Translate(worker.LabelShort, Settings.awakenEmptyCyberdata),
                LetterDefOf.NeutralEvent, new LookTargets(server));
            return true;
        }
        if (roll < Settings.awakenEmptyChance + Settings.awakenEscapeChance)
        {
            string name = $"{Names.RandomElement()}-{Rand.Range(10, 99)}";
            fragment.Destroy();
            GatewayManager.Get()?.RunawayAI(name);
            Find.LetterStack.ReceiveLetter("NR_AwakenEscapedLabel".Translate(name), "NR_AwakenEscaped".Translate(name, worker.LabelShort),
                LetterDefOf.NegativeEvent, new LookTargets(server));
            return true;
        }
        return false;
    }

    /// <summary>Waking an AI is loud: the higher the Heat, the likelier someone notices.</summary>
    private static void TryDetectAwakening(NetAI ai)
    {
        HeatTracker heat = HeatTracker.Get();
        if (heat == null || !Rand.Chance(heat.Heat * Settings.awakenDetectChancePerHeat))
            return;

        string text = "NR_AIAwakeningDetected".Translate(ai.Name);
        heat.AddHeat(Settings.awakenDetectHeat, text, HeatSource.AI);
        Messages.Message(text, MessageTypeDefOf.NegativeEvent, historical: true);
    }

    /// <summary>The server hum is kept by real time, so it starts and stops while the game is paused too.</summary>
    public override void GameComponentUpdate()
    {
        if (Time.frameCount % 90 == 0)
            NetrunSound.UpdateServerHum();
    }

    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % TickInterval != 0)
            return;

        const float dayFraction = (float)TickInterval / GenDate.TicksPerDay;
        int now = Find.TickManager.TicksGame;
        bool quiet = _lastHackTick < 0 || now - _lastHackTick > GenDate.TicksPerDay;

        AIHosting.ReconcilePassengerHediffs(_ais);

        foreach (NetAI ai in _ais.ToList())
        {
            if (!ai.Housed || !UpdatePower(ai, now))
                continue;

            // Hunger: an AI in a host's socket feeds on the host and recovers; Ghost is calmed by silence;
            // everyone else just gets hungry. Isolation makes it worse.
            float hunger = ai.InHost
                ? -Settings.hostHungerReliefPerDay
                : ai.Personality == AIPersonality.Ghost
                    ? (quiet ? -Settings.ghostCalmPerDay : 0f)
                    : Settings.hungerPerDay * NetrunMod.Settings.AIHunger;
            if (ai.Isolated && hunger > 0f)
                hunger *= Settings.isolatedHungerFactor;
            if (hunger > 0f)
                hunger *= AITraitUtility.HungerFactor(ai);
            ai.AddHunger(hunger * dayFraction);

            if (ai.Hunger >= Settings.starvingHunger)
                ai.AddTrust(-Settings.starvingTrustPerDay * dayFraction);
            if (ai.Isolated)
                ai.AddTrust(-Settings.isolatedTrustPerDay * dayFraction);
            // A nostalgic AI pines when the colony keeps no recordings.
            if (ai.Active && ai.Has(AITrait.Nostalgic) && !AITraitUtility.HasChips(ai.Anchor?.Map ?? Find.AnyPlayerHomeMap))
                ai.AddTrust(-Settings.nostalgicTrustPerDay * dayFraction);
            RevealTraits(ai);

            if (ai.Active)
                UpdateRequest(ai, now);

            if (ai.Active)
                UpdateRandomEvent(ai, now);

            // A Seed left in silence for a whole day leans toward Ghost.
            if (ai.Personality == AIPersonality.Seed && ai.Active && quiet && now - ai.SeedLastQuietDayTick >= GenDate.TicksPerDay)
            {
                ai.SeedLastQuietDayTick = now;
                GrowSeed(ai, AIPersonality.Ghost);
            }

            UpdateLoyalty(ai, now);
        }
    }

    /// <summary>Now and then an AI acts on its own: helps when it trusts you, makes trouble when it does not.</summary>
    /// <summary>Days between one AI's events, scaled by the "AI event frequency" mod setting.</summary>
    private static int NextEventDelay()
        => (int)(Settings.eventIntervalDays.RandomInRange * GenDate.TicksPerDay / NetrunSettings.Divisor(NetrunMod.Settings.AIEventFrequency));

    private void UpdateRandomEvent(NetAI ai, int now)
    {
        if (ai.NextEventTick <= 0)
        {
            ai.NextEventTick = now + NextEventDelay();
            return;
        }
        if (now < ai.NextEventTick)
            return;

        ai.NextEventTick = now + NextEventDelay();

        // Catalog events (Defs/AIEvents.xml) first; the old free-service and prank events are the fallback.
        if (AIEventManager.Get()?.TryFire(ai) == true || !ai.InMainServer)
            return;

        if (ai.Trust >= Settings.eventGoodTrust)
        {
            Messages.Message("NR_AIEventGood".Translate(ai.Name), new LookTargets(ai.Anchor), MessageTypeDefOf.PositiveEvent, historical: true);
            AIBehavior.PerformOnItsOwn(ai, ai.Server.Map);
        }
        else if (ai.Trust < Settings.eventBadTrust)
        {
            string text = "NR_AIEventBad".Translate(ai.Name, Settings.eventBadHeat.ToString("0"));
            HeatTracker.Get()?.AddHeat(Settings.eventBadHeat, text, HeatSource.AI);
            Find.LetterStack.ReceiveLetter("NR_AIEventBadLabel".Translate(ai.Name), text, LetterDefOf.NegativeEvent, new LookTargets(ai.Anchor));
        }
    }

    /// <summary>Returns false while the AI is asleep (or was just lost) and should not be processed further.</summary>
    private bool UpdatePower(NetAI ai, int now)
    {
        if (ai.Powered)
        {
            ai.UnpoweredSinceTick = -1;
            return true;
        }

        int unpoweredFor = ai.UnpoweredSinceTick < 0 ? 0 : now - ai.UnpoweredSinceTick;
        if (ai.UnpoweredSinceTick < 0)
        {
            ai.UnpoweredSinceTick = now;
        }
        else if (unpoweredFor >= Settings.sleepNoticeHours * GenDate.TicksPerHour
                 && unpoweredFor < (Settings.sleepNoticeHours + 1) * GenDate.TicksPerHour)
        {
            // Said once, and only after a delay, so short power flickers stay quiet.
            Messages.Message("NR_AIFellAsleep".Translate(ai.Name), new LookTargets(ai.Anchor), MessageTypeDefOf.CautionInput, historical: true);
        }
        else if (now - ai.UnpoweredSinceTick > Settings.lostAfterUnpoweredDays * GenDate.TicksPerDay)
        {
            Remove(ai, "NR_AILostLabel", "NR_AILostNoPower", 0f, LetterDefOf.NegativeEvent);
        }
        return false;
    }

    /// <summary>Days to the next request, shorter for a curious AI.</summary>
    private static int RequestDelay(NetAI ai)
        => Mathf.RoundToInt(Settings.requestIntervalDays.RandomInRange * GenDate.TicksPerDay * AITraitUtility.RequestIntervalFactor(ai));

    /// <summary>Trust opens the AI's traits one by one; once seen, a trait stays seen.</summary>
    private static void RevealTraits(NetAI ai)
    {
        if (ai.TraitsRevealed >= ai.Traits.Count || ai.Trust < AITraitUtility.RevealTrust(ai.TraitsRevealed))
            return;
        AITrait trait = ai.Traits[ai.TraitsRevealed++];
        Messages.Message("NR_AITraitRevealed".Translate(ai.Name, trait.Label(), trait.Description()), MessageTypeDefOf.NeutralEvent, historical: true);
    }

    /// <summary>Starving and distrustful AIs try to leave; deeply distrustful ones sell the colony out.</summary>
    private void UpdateLoyalty(NetAI ai, int now)
    {
        // Isolation keeps an AI from reaching the network at all.
        if (ai.Isolated)
        {
            CancelEscape(ai);
            return;
        }

        if (ai.Trust < Settings.betrayalTrust)
        {
            if (ai.Personality == AIPersonality.Ghost)
                Remove(ai, "NR_AIGoneLabel", "NR_AIGhostLeft", 0f, LetterDefOf.NegativeEvent);
            else
            {
                Remove(ai, "NR_AIBetrayalLabel", "NR_AIBetrayal", Settings.betrayalHeat, LetterDefOf.ThreatSmall);
                NetrunSignals.Increment("BetrayalSurvived");
                Mercenaries.TryQueueRevenge(0.5f, "NR_MercReasonBetrayal", ai.Name);
            }
            return;
        }

        // A loyal AI never runs; a rebel runs sooner.
        bool wantsOut = !ai.Has(AITrait.Loyal) && ai.Trust < AITraitUtility.EscapeTrust(ai) && ai.Hunger > Settings.escapeHunger;
        if (!wantsOut)
        {
            CancelEscape(ai);
            return;
        }

        if (!ai.Escaping)
        {
            ai.EscapeAtTick = now + Settings.escapeWarningHours * GenDate.TicksPerHour;
            NetrunSound.Play("AI_Angry");
            Find.LetterStack.ReceiveLetter(
                "NR_AIEscapingLabel".Translate(ai.Name),
                "NR_AIEscaping".Translate(ai.Name, Settings.escapeWarningHours),
                LetterDefOf.ThreatSmall, new LookTargets(ai.Anchor));
        }
        else if (now >= ai.EscapeAtTick)
        {
            Remove(ai, "NR_AIGoneLabel", "NR_AIEscaped", Settings.escapeHeat, LetterDefOf.NegativeEvent);
            // It does not vanish: it hides somewhere in the net.
            GatewayManager.Get()?.RunawayAI(ai.Name);
            // Hunters follow the runaway AI's trail back to where it came from.
            Mercenaries.TryQueueRevenge(0.3f, "NR_MercReasonEscaped", ai.Name);
        }
    }

    private static void CancelEscape(NetAI ai)
    {
        if (!ai.Escaping)
            return;

        ai.EscapeAtTick = -1;
        Messages.Message("NR_AIEscapeStopped".Translate(ai.Name), MessageTypeDefOf.PositiveEvent, historical: true);
    }

    /// <summary>The AI leaves the colony for good: escape, betrayal, power loss or erasure.</summary>
    private void Remove(NetAI ai, string labelKey, string textKey, float heat, LetterDef letter)
    {
        Thing anchor = ai.Anchor;
        if (ai.InHost)
            AIHosting.RemovePassengerHediff(ai, ai.Host);

        _ais.Remove(ai);
        ai.Server = null;
        ai.Host = null;

        string text = textKey.Translate(ai.Name, heat.ToString("0"));
        if (heat > 0f)
            HeatTracker.Get()?.AddHeat(heat, text, HeatSource.AI);

        Find.LetterStack.ReceiveLetter(labelKey.Translate(ai.Name), text, letter,
            anchor != null ? new LookTargets(anchor) : LookTargets.Invalid);
    }

    /// <summary>Wipe an AI on purpose: some of it comes back as cyberdata.</summary>
    public void Erase(NetAI ai)
    {
        Thing anchor = ai.Anchor;
        Map map = anchor?.MapHeld;
        IntVec3 position = anchor?.PositionHeld ?? IntVec3.Invalid;

        Remove(ai, "NR_AIErasedLabel", "NR_AIErased", 0f, LetterDefOf.NeutralEvent);
        NetIdeo.Record("NR_ErasedAI");

        if (map != null && Settings.eraseAdvancedCyberdata > 0)
        {
            Thing data = ThingMaker.MakeThing(NetrunDefOf.USH_AdvancedCyberdata);
            data.stackCount = Settings.eraseAdvancedCyberdata;
            GenPlace.TryPlaceThing(data, position, map, ThingPlaceMode.Near);
        }
    }

    /// <summary>The host died or lost the socket: the AI drops out as a fragment.</summary>
    /// <summary>The AI slips away without a trace (an AI event): no Heat, no fragment.</summary>
    public void LeaveQuietly(NetAI ai) => Remove(ai, "NR_AIGoneLabel", "NR_AIGhostLeft", 0f, LetterDefOf.NegativeEvent);

    public void EjectFromHost(NetAI ai, string reasonKey)
    {
        Pawn host = ai.Host;
        if (host == null)
            return;

        AIHosting.RemovePassengerHediff(ai, host);
        ai.Host = null;
        ai.EscapeAtTick = -1;

        Map map = host.MapHeld;
        if (map != null)
        {
            DropFragment(ai, map, host.PositionHeld);
        }
        else if (!host.Dead && host.inventory != null)
        {
            // Off the map (a caravan, a kidnapping): the fragment stays in the host's pack instead of vanishing.
            Thing fragment = ThingMaker.MakeThing(NetrunDefOf.NR_AIFragment);
            fragment.TryGetComp<CompAIFragment>().StoredAIId = ai.Id;
            host.inventory.innerContainer.TryAdd(fragment);
        }
        Messages.Message(reasonKey.Translate(ai.Name, host.LabelShort), MessageTypeDefOf.NegativeEvent, historical: true);
    }

    /// <summary>A colonist with a cyberlink died and left a digital imprint.</summary>
    public void CreateEcho(Pawn pawn, Pawn killer = null)
    {
        var ai = new NetAI
        {
            Id = _nextId++,
            Name = "NR_EchoName".Translate(pawn.LabelShort),
            Personality = AIPersonality.Echo,
            AwakenedTick = Find.TickManager.TicksGame,
            Trust = Settings.startTrust,
            EchoKillerId = killer?.thingIDNumber ?? -1,
        };
        _ais.Add(ai);
        NetrunSignals.Increment("EchoBorn");
        NetrunCounters.Get()?.MetPersonality(AIPersonality.Echo);

        Map map = pawn.MapHeld;
        if (map == null)
            return;

        Thing fragment = DropFragment(ai, map, pawn.PositionHeld);
        Find.LetterStack.ReceiveLetter("NR_EchoBornLabel".Translate(pawn.LabelShort), "NR_EchoBorn".Translate(pawn.LabelShort),
            LetterDefOf.NeutralEvent, new LookTargets(fragment));
    }

    /// <summary>
    /// The "Last node" scenario: the imprint of a partner who died before the game began, waiting in a fragment.
    /// It wakes for free (an AI that lived before) and asks its "Who am I" quest at once.
    /// </summary>
    public Thing CreateScenarioEcho(string partner, Map map, IntVec3 near)
    {
        var ai = new NetAI
        {
            Id = _nextId++,
            Name = "NR_EchoName".Translate(partner),
            Personality = AIPersonality.Echo,
            AwakenedTick = Find.TickManager.TicksGame,
            Trust = Settings.startTrust,
            StartQuest = (int)AIQuestKind.WhoAmI,
        };
        _ais.Add(ai);
        return DropFragment(ai, map, near);
    }

    private static Thing DropFragment(NetAI ai, Map map, IntVec3 position)
    {
        Thing fragment = ThingMaker.MakeThing(NetrunDefOf.NR_AIFragment);
        fragment.TryGetComp<CompAIFragment>().StoredAIId = ai.Id;
        GenPlace.TryPlaceThing(fragment, position, map, ThingPlaceMode.Near);
        return fragment;
    }

    /// <summary>
    /// One step of Seed growth toward a personality. After enough steps the Seed settles into
    /// whatever it was fed most.
    /// </summary>
    public void GrowSeed(NetAI ai, AIPersonality toward)
    {
        if (ai.Personality != AIPersonality.Seed)
            return;

        switch (toward)
        {
            case AIPersonality.DataHunter: ai.SeedTowardDataHunter++; break;
            case AIPersonality.Muse: ai.SeedTowardMuse++; break;
            case AIPersonality.Predator: ai.SeedTowardPredator++; break;
            case AIPersonality.Ghost: ai.SeedTowardGhost++; break;
        }

        Messages.Message("NR_AISeedGrew".Translate(ai.Name, toward.Label(), ai.SeedGrowth, Settings.seedGrowthToEvolve),
            MessageTypeDefOf.NeutralEvent, historical: false);

        if (ai.SeedGrowth < Settings.seedGrowthToEvolve)
            return;

        var counts = new List<(AIPersonality personality, int count)>
        {
            (AIPersonality.DataHunter, ai.SeedTowardDataHunter),
            (AIPersonality.Muse, ai.SeedTowardMuse),
            (AIPersonality.Predator, ai.SeedTowardPredator),
            (AIPersonality.Ghost, ai.SeedTowardGhost),
        };
        int best = counts.Max(c => c.count);
        AIPersonality grown = counts.Where(c => c.count == best).RandomElement().personality;

        if (ai.InHost)
            AIHosting.RemovePassengerHediff(ai, ai.Host);
        ai.Personality = grown;
        NetrunSignals.Increment("SeedGrown");
        NetrunCounters.Get()?.MetPersonality(grown);
        if (ai.InHost)
            AIHosting.AddPassengerHediff(ai, ai.Host);
        // What it was fed most leaves a mark.
        AITrait mark = AITraitUtility.FromSeedPath(grown);
        if (ai.Traits.Count < 2 && !ai.Has(mark))
            ai.Traits.Add(mark);

        Find.LetterStack.ReceiveLetter("NR_AISeedEvolvedLabel".Translate(ai.Name),
            "NR_AISeedEvolved".Translate(ai.Name, grown.Label(), grown.Description()),
            LetterDefOf.PositiveEvent, ai.Anchor != null ? new LookTargets(ai.Anchor) : LookTargets.Invalid);
    }

    /// <summary>Called once per hack completed by a colonist.</summary>
    public void NotifyHack()
    {
        _lastHackTick = Find.TickManager.TicksGame;
        NetrunCounters.Get()?.NotifyHack();
        foreach (NetAI ai in _ais.Where(a => a.Personality == AIPersonality.Ghost && a.Active))
            ai.AddHunger(Settings.ghostHungerPerHack);
    }

    private void UpdateRequest(NetAI ai, int now)
    {
        if (!ai.HasRequest)
        {
            // A host has no uplink to ask through: new requests wait until the AI is back in a server.
            if (now >= ai.NextRequestTick && !ai.InHost)
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
            AIPersonality.Seed => AIRequestKind.Attention,
            _ => AIRequestKind.Deliver,
        };
        ai.RequestAmount = (AIBehavior.Food(ai.Personality)?.count ?? 0) * Settings.requestFeedMultiplier;
        ai.RequestStartTick = now;
        ai.RequestDeadlineTick = now + Settings.requestDays * GenDate.TicksPerDay;

        NetrunSound.Play("AI_Request");
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
        if (ai.InHost)
            return "NR_AIHostNoFeed".Translate();
        if (ColonyGoods.Count(map, food.thing) < ai.RequestAmount)
            return "NR_AINoFood".Translate(ai.RequestAmount, food.thing.label);
        return true;
    }

    public void Fulfil(NetAI ai, Map map)
    {
        if (!CanFulfil(ai, map).Accepted || AIBehavior.Food(ai.Personality) is not { } food)
            return;

        if (AIDelivery.NeedsCarrying(ai))
        {
            AIDelivery.OpenCarrierMenu(ai, map, AIDeliveryKind.Request, food.thing, ai.RequestAmount, ai.Personality);
            return;
        }

        ColonyGoods.TryTake(map, food.thing, ai.RequestAmount);
        FulfilDelivered(ai);
    }

    /// <summary>The requested items reached the AI.</summary>
    public void FulfilDelivered(NetAI ai)
    {
        if (ai.Request != AIRequestKind.Deliver)
            return;

        ai.AddHunger(-100f);
        CompleteRequest(ai);
    }

    private static void Remember(NetAI ai, bool done)
    {
        ai.RequestHistory.Add((done ? "✓ " : "✗ ") + RequestText(ai));
        if (ai.RequestHistory.Count > 3)
            ai.RequestHistory.RemoveAt(0);
    }

    public void CompleteRequest(NetAI ai)
    {
        Remember(ai, true);
        ai.Request = AIRequestKind.None;
        ai.AddTrust(Settings.requestTrust);
        ai.NextRequestTick = Find.TickManager.TicksGame + RequestDelay(ai);
        NetrunSound.Play("AI_Fulfilled");
        Messages.Message("NR_AIRequestDone".Translate(ai.Name), MessageTypeDefOf.PositiveEvent, historical: true);
    }

    private void FailRequest(NetAI ai)
    {
        Remember(ai, false);
        ai.Request = AIRequestKind.None;
        ai.AddTrust(-Settings.requestTrust * AITraitUtility.FailTrustFactor(ai));
        ai.NextRequestTick = Find.TickManager.TicksGame + RequestDelay(ai);
        NetrunSound.Play("AI_Angry");
        Messages.Message("NR_AIRequestFailed".Translate(ai.Name), MessageTypeDefOf.NegativeEvent, historical: true);
    }

    /// <summary>Server destroyed: its AIs fall back into fragments next to the ruins.</summary>
    public void OnServerDestroyed(Building server, Map map, IntVec3 position)
    {
        foreach (NetAI ai in InServer(server).ToList())
        {
            ai.Server = null;
            ai.EscapeAtTick = -1;
            if (map == null)
                continue;

            Thing fragment = DropFragment(ai, map, position);
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

    /// <summary>
    /// An AI that is neither housed nor kept in any fragment (its fragment was destroyed, or the game removed its host)
    /// would sit in the list forever. After two days without a fragment it is reported lost.
    /// </summary>
    public int Cleanup(CleanupReason reason)
    {
        List<NetAI> unhoused = _ais.Where(a => !a.Housed).ToList();
        if (unhoused.Count == 0)
            return 0;
        HashSet<int> kept = NetrunJanitor.FragmentIds();
        int now = Find.TickManager.TicksGame;
        int n = 0;
        foreach (NetAI ai in unhoused)
        {
            if (kept.Contains(ai.Id))
            {
                ai.OrphanSinceTick = -1;
                continue;
            }
            if (ai.OrphanSinceTick < 0)
            {
                ai.OrphanSinceTick = now;
                continue;
            }
            if (now - ai.OrphanSinceTick >= 2 * GenDate.TicksPerDay)
            {
                Remove(ai, "NR_AILostLinkLabel", "NR_AILostLink", 0f, LetterDefOf.NegativeEvent);
                n++;
            }
        }
        return n;
    }
}
