using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using USH_HE;
using Verse;

namespace Netrun;

public enum DiveElementKind
{
    Lobby,
    Gate,
    Ice,
    Files,
    Memory,
    Exit,
}

public enum DiveProgram
{
    Sword,
    Shield,
    Decoy,
    Stealth,
    Killer,
}

/// <summary>One element of a node's architecture (Data Fortress style): a box on the dive map.</summary>
public class DiveElement : IExposable
{
    public DiveElementKind Kind;
    public int Strength;
    public bool Hidden;
    public bool Cleared;
    public bool Looted;

    /// <summary>Gates and ICE block the way on until they are hacked.</summary>
    public bool Blocks => (Kind == DiveElementKind.Gate || Kind == DiveElementKind.Ice) && !Cleared;

    public void ExposeData()
    {
        Scribe_Values.Look(ref Kind, "kind");
        Scribe_Values.Look(ref Strength, "strength");
        Scribe_Values.Look(ref Hidden, "hidden");
        Scribe_Values.Look(ref Cleared, "cleared");
        Scribe_Values.Look(ref Looted, "looted");
    }
}

/// <summary>
/// A dive by turns (docs/CYBERSPACE.md, mockup 3-dive): the netrunner moves along the node's elements,
/// spending network actions; when they run out the node answers (Black ICE attacks, the trace grows).
/// The game is paused while the terminal is open, so a dive takes no game time.
/// </summary>
public class DiveSession : IExposable
{
    private static CyberspaceSettingsDef Settings => NetrunDefOf.NR_Cyberspace;

    public ExternalNode Node;
    public Pawn Runner;
    public List<DiveElement> Elements = [];
    public int Position;
    public int Turn = 1;
    public int ActionsLeft;
    public float Trace;
    public List<DiveProgram> Programs = [];
    public int ShieldCharges;
    public int DecoyCharges;
    public int KillerCharges;
    public int LootCyberdata;
    public int LootAdvanced;
    public int LootSilver;
    public List<ThingDef> LootItems = [];
    public List<string> Log = [];
    public bool Finished;

    public int ActionsPerTurn
    {
        get
        {
            float speed = Runner?.GetStatValue(StatDefOf.HackingSpeed) ?? 1f;
            return speed < 0.8f ? 1 : speed < 1.3f ? 2 : 3;
        }
    }

    public DiveElement Current => Elements[Position];
    public DiveElement Next => Position + 1 < Elements.Count ? Elements[Position + 1] : null;

    public static DiveSession Start(ExternalNode node, Pawn runner, List<DiveProgram> programs)
    {
        var session = new DiveSession { Node = node, Runner = runner, Programs = programs.ToList() };
        session.ShieldCharges = programs.Count(p => p == DiveProgram.Shield);
        session.DecoyCharges = programs.Count(p => p == DiveProgram.Decoy);
        session.KillerCharges = programs.Count(p => p == DiveProgram.Killer);
        session.Build();
        session.ActionsLeft = session.ActionsPerTurn;
        session.AddLog("NR_DiveLogStart".Translate(runner.LabelShort, node.Name));
        return session;
    }

    /// <summary>Lobby, 1-3 code gates, maybe a hidden cache, Black ICE if the node has it, files, a memory unit, the exit.</summary>
    private void Build()
    {
        Elements.Add(new DiveElement { Kind = DiveElementKind.Lobby, Cleared = true });
        int gates = 1 + Node.Defense / 2;
        for (int i = 0; i < gates; i++)
            Elements.Add(new DiveElement { Kind = DiveElementKind.Gate, Strength = Node.Defense + Rand.RangeInclusive(0, 2) });
        if (Rand.Chance(0.6f))
            Elements.Add(new DiveElement { Kind = DiveElementKind.Files, Hidden = true, Cleared = true });
        if (Node.BlackIce)
            Elements.Add(new DiveElement { Kind = DiveElementKind.Ice, Strength = Node.IceStrength });
        Elements.Add(new DiveElement { Kind = DiveElementKind.Files, Cleared = true });
        if (Node.Type is ExternalNodeType.Abandoned or ExternalNodeType.Corp or ExternalNodeType.DarkExchange)
            Elements.Add(new DiveElement { Kind = DiveElementKind.Memory, Cleared = true });
        Elements.Add(new DiveElement { Kind = DiveElementKind.Exit, Cleared = true });
    }

    public void AddLog(string line)
    {
        Log.Add(line);
        if (Log.Count > 8)
            Log.RemoveAt(0);
    }

    // ---- Actions ----

    public AcceptanceReport CanMove()
    {
        if (Next == null)
            return "NR_DiveNoFurther".Translate();
        if (Next.Hidden)
            return "NR_DiveHiddenAhead".Translate();
        if (Next.Blocks)
            return "NR_DiveBlocked".Translate();
        return true;
    }

    public void Move()
    {
        Position++;
        AddLog("NR_DiveLogMove".Translate(ElementLabel(Current)));
        Spend(Settings.tracePerAction);
    }

    public AcceptanceReport CanScan() => Next is { Hidden: true } ? true : "NR_DiveNothingToScan".Translate();

    public void Scan()
    {
        Next.Hidden = false;
        AddLog("NR_DiveLogScan".Translate(ElementLabel(Next)));
        Spend(Settings.tracePerAction);
    }

    public AcceptanceReport CanHack() => Next is { Blocks: true, Hidden: false } ? true : "NR_DiveNothingToHack".Translate();

    public float HackChance(DiveElement target)
    {
        int skill = Runner?.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
        float chance = Settings.hackBase + skill * Settings.hackPerSkill - target.Strength * Settings.hackPerStrength;
        if (Programs.Contains(DiveProgram.Sword))
            chance += Settings.swordBonus;
        return Mathf.Clamp(chance, 0.05f, 0.95f);
    }

    public void Hack()
    {
        DiveElement target = Next;
        bool sword = Programs.Contains(DiveProgram.Sword);
        if (KillerCharges > 0)
        {
            KillerCharges--;
            target.Cleared = true;
            AddLog("NR_DiveLogKiller".Translate(ElementLabel(target)));
        }
        else if (Rand.Chance(HackChance(target)))
        {
            target.Cleared = true;
            AddLog("NR_DiveLogHackOk".Translate(ElementLabel(target)));
        }
        else
        {
            AddLog("NR_DiveLogHackFail".Translate(ElementLabel(target)).Colorize(NetrunUI.Bad));
            if (target.Kind == DiveElementKind.Ice)
                IceAttack();
        }
        Spend(Settings.tracePerAction * (sword ? 2f : 1f));
    }

    public AcceptanceReport CanDownload()
    {
        if (Current.Kind is not (DiveElementKind.Files or DiveElementKind.Memory))
            return "NR_DiveNothingToDownload".Translate();
        return Current.Looted ? "NR_DiveAlreadyLooted".Translate() : true;
    }

    public void Download()
    {
        Current.Looted = true;
        if (Current.Kind == DiveElementKind.Memory)
            RollMemory();
        else
            RollFiles();
        AddLog("NR_DiveLogDownload".Translate(LootText()));
        Spend(Settings.tracePerAction);
    }

    private void RollFiles()
    {
        switch (Node.Type)
        {
            case ExternalNodeType.Market:
                LootCyberdata += Rand.RangeInclusive(3, 5);
                break;
            case ExternalNodeType.Archive:
                LootCyberdata += Rand.RangeInclusive(2, 4);
                break;
            case ExternalNodeType.Corp:
                LootCyberdata += Rand.RangeInclusive(4, 6);
                LootAdvanced += Rand.RangeInclusive(1, 2);
                break;
            case ExternalNodeType.DarkExchange:
                LootSilver += Rand.RangeInclusive(200, 400);
                LootCyberdata += Rand.RangeInclusive(1, 3);
                break;
            default:
                LootCyberdata += Rand.RangeInclusive(2, 3);
                break;
        }
    }

    private void RollMemory()
    {
        if (Rand.Chance(Node.Type == ExternalNodeType.Abandoned ? 0.25f : 0.1f))
            LootItems.Add(NetrunDefOf.NR_AIFragment);
        else if (Rand.Chance(0.05f))
            LootItems.Add(NetrunDefOf.NR_AICoreModule);
        else
            LootAdvanced += Rand.RangeInclusive(1, 2);
    }

    public string LootText()
    {
        var parts = new List<string>();
        if (LootCyberdata > 0)
            parts.Add($"{LootCyberdata} {NetrunDefOf.USH_Cyberdata.label}");
        if (LootAdvanced > 0)
            parts.Add($"{LootAdvanced} {NetrunDefOf.USH_AdvancedCyberdata.label}");
        if (LootSilver > 0)
            parts.Add($"{LootSilver} {ThingDefOf.Silver.label}");
        parts.AddRange(LootItems.Select(d => d.label));
        return parts.Count == 0 ? "-" : parts.ToCommaList();
    }

    /// <summary>Each action costs one network action and raises the trace; when actions run out the node takes its turn.</summary>
    private void Spend(float trace)
    {
        Trace += trace * (Programs.Contains(DiveProgram.Stealth) ? Settings.stealthTraceFactor : 1f);
        ActionsLeft--;
        if (ActionsLeft <= 0 && !Finished)
            NodeTurn();
    }

    private void NodeTurn()
    {
        int ice = Elements.FindIndex(e => e.Kind == DiveElementKind.Ice && !e.Cleared);
        if (ice >= 0 && Position >= ice - 1 && Rand.Chance(Settings.iceAttackChance))
            IceAttack();

        if (Finished)
            return;

        Turn++;
        ActionsLeft = ActionsPerTurn;
        if (Turn > Settings.maxTurns)
        {
            AddLog("NR_DiveLogTimeout".Translate().Colorize(NetrunUI.Bad));
            Runner?.health.AddHediff(HediffDefOf.Anesthetic);
            End(success: false, keepShare: 1f - Settings.retreatLootLoss);
        }
        else if (ice >= 0 && Position >= ice - 2)
        {
            AddLog("NR_DiveLogIceWatching".Translate().Colorize(NetrunUI.Bad));
        }
    }

    /// <summary>Black ICE strikes: a shield absorbs it, a decoy may draw it off, otherwise a Hacking Expansion outcome.</summary>
    private void IceAttack()
    {
        if (ShieldCharges > 0)
        {
            ShieldCharges--;
            AddLog("NR_DiveLogShield".Translate());
            return;
        }
        if (DecoyCharges > 0 && Rand.Chance(Settings.decoyChance))
        {
            DecoyCharges--;
            AddLog("NR_DiveLogDecoy".Translate());
            return;
        }

        AddNetsick(Settings.netsickPerIceHit);
        float roll = Rand.Value;
        if (roll < 0.6f)
        {
            AddHediff("USH_CyberspaceNausea");
            AddLog("NR_DiveLogNausea".Translate(Runner.LabelShort).Colorize(NetrunUI.Bad));
        }
        else if (roll < 0.9f)
        {
            AddHediff("USH_CyberspaceLockOut");
            AddLog("NR_DiveLogLockOut".Translate(Runner.LabelShort).Colorize(NetrunUI.Bad));
            End(success: false, keepShare: 0f);
        }
        else
        {
            AddHediff("USH_CyberspaceComa");
            AddLog("NR_DiveLogComa".Translate(Runner.LabelShort).Colorize(NetrunUI.Bad));
            End(success: false, keepShare: 0f);
        }
    }

    private void AddHediff(string defName)
    {
        HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
        if (def != null && Runner != null && !Runner.Dead)
            Runner.health.AddHediff(def, Runner.health.hediffSet.GetBrain());
    }

    private void AddNetsick(float amount)
    {
        if (Runner == null || Runner.Dead)
            return;
        CyberpodUtility.InCyberpod(Runner, out NetAI coprocessor);
        Hediff netsick = Runner.health.hediffSet.GetFirstHediffOfDef(NetrunDefOf.NR_Netsick)
                         ?? Runner.health.AddHediff(NetrunDefOf.NR_Netsick);
        netsick.Severity += amount * CyberpodUtility.NetsickFactor(coprocessor);
    }

    public AcceptanceReport CanExit() => Current.Kind == DiveElementKind.Exit ? true : "NR_DiveExitFar".Translate();

    public void Exit()
    {
        AddLog("NR_DiveLogExit".Translate());
        End(success: true, keepShare: 1f);
    }

    /// <summary>Leaving early: from the lobby it is free, deeper in it costs part of the haul and some Netsick.</summary>
    public void Retreat()
    {
        bool safe = Current.Kind == DiveElementKind.Lobby;
        if (!safe)
            AddNetsick(Settings.netsickPerIceHit * 0.5f);
        AddLog("NR_DiveLogRetreat".Translate());
        End(success: false, keepShare: safe ? 1f : 1f - Settings.retreatLootLoss);
    }

    /// <summary>Delivers what was kept next to the pod, adds Heat and finishes the dive.</summary>
    private void End(bool success, float keepShare)
    {
        Finished = true;
        Map map = Runner?.MapHeld;
        CyberpodUtility.InCyberpod(Runner, out NetAI coprocessor);

        if (map != null)
        {
            IntVec3 spot = Runner.PositionHeld;
            Spawn(map, spot, NetrunDefOf.USH_Cyberdata, Mathf.RoundToInt(LootCyberdata * keepShare));
            Spawn(map, spot, NetrunDefOf.USH_AdvancedCyberdata, Mathf.RoundToInt(LootAdvanced * keepShare));
            Spawn(map, spot, ThingDefOf.Silver, Mathf.RoundToInt(LootSilver * keepShare));
            if (keepShare >= 1f)
            {
                foreach (ThingDef item in LootItems)
                    Spawn(map, spot, item, 1);
            }
        }

        float heat = Trace * Node.HeatPerTrace * CyberpodUtility.HeatFactor(coprocessor);
        if (heat > 0f)
            HeatTracker.Get()?.AddHeat(heat, "NR_DiveHeatReason".Translate(Runner?.LabelShort ?? "?", Node.Name));
        AddNetsick(Settings.netsickPerDive);

        if (success)
        {
            Runner?.skills?.Learn(SkillDefOf.Intellectual, Settings.successXp, direct: true);
            ContractManager contracts = ContractManager.Get();
            for (int i = 0; i < (Node.Contract ? 2 : 1); i++)
                contracts?.NotifyHackCompleted();
            GatewayManager.Get()?.RemoveNode(Node);
        }

        Messages.Message(success
                ? "NR_DiveDone".Translate(Runner?.LabelShort ?? "?", Node.Name, LootText(), heat.ToString("0"))
                : "NR_DiveFailed".Translate(Runner?.LabelShort ?? "?", Node.Name, heat.ToString("0")),
            success ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent, historical: true);
    }

    private static void Spawn(Map map, IntVec3 spot, ThingDef def, int count)
    {
        while (count > 0)
        {
            Thing stack = ThingMaker.MakeThing(def);
            stack.stackCount = Mathf.Min(count, def.stackLimit);
            count -= stack.stackCount;
            GenPlace.TryPlaceThing(stack, spot, map, ThingPlaceMode.Near);
        }
    }

    public static string ElementLabel(DiveElement element)
        => element.Hidden ? "NR_DiveElement_Hidden".Translate() : ("NR_DiveElement_" + element.Kind).Translate();

    public void ExposeData()
    {
        Scribe_Deep.Look(ref Node, "node");
        Scribe_References.Look(ref Runner, "runner");
        Scribe_Collections.Look(ref Elements, "elements", LookMode.Deep);
        Scribe_Values.Look(ref Position, "position");
        Scribe_Values.Look(ref Turn, "turn", 1);
        Scribe_Values.Look(ref ActionsLeft, "actionsLeft");
        Scribe_Values.Look(ref Trace, "trace");
        Scribe_Collections.Look(ref Programs, "programs", LookMode.Value);
        Scribe_Values.Look(ref ShieldCharges, "shield");
        Scribe_Values.Look(ref DecoyCharges, "decoy");
        Scribe_Values.Look(ref KillerCharges, "killer");
        Scribe_Values.Look(ref LootCyberdata, "lootCyberdata");
        Scribe_Values.Look(ref LootAdvanced, "lootAdvanced");
        Scribe_Values.Look(ref LootSilver, "lootSilver");
        Scribe_Collections.Look(ref LootItems, "lootItems", LookMode.Def);
        Scribe_Collections.Look(ref Log, "log", LookMode.Value);
        Scribe_Values.Look(ref Finished, "finished");
        Elements ??= [];
        Programs ??= [];
        LootItems ??= [];
        Log ??= [];
    }
}
