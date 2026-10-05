using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Goggles record on their own: a strong feeling or a kill leaves a raw recording next to the wearer.
/// One recording per wearer per cooldown, so a bad day does not flood the stockpile.
/// </summary>
public class BraindanceRecorder : GameComponent
{
    private const float MinMood = 8f;
    private const float FullStrengthMood = 20f;
    private const float KillStrength = 0.7f;
    private const int CooldownTicks = GenDate.TicksPerDay;

    private Dictionary<Pawn, int> _lastRecord = [];
    private List<Pawn> _keys;
    private List<int> _values;

    public BraindanceRecorder(Game game)
    {
    }

    public static BraindanceRecorder Get() => Current.Game?.GetComponent<BraindanceRecorder>();

    public void TryRecord(Pawn pawn, string what, BraindanceMood mood, float strength)
    {
        if (pawn == null || !pawn.Spawned || pawn.Faction != Faction.OfPlayer || !BraindanceUtility.HasGoggles(pawn))
            return;

        int now = Find.TickManager.TicksGame;
        if (_lastRecord.TryGetValue(pawn, out int last) && now - last < CooldownTicks)
            return;
        _lastRecord[pawn] = now;

        Thing raw = ThingMaker.MakeThing(NetrunDefOf.NR_BraindanceRaw);
        CompBraindanceRecord rec = raw.TryGetComp<CompBraindanceRecord>();
        rec.Author = pawn.LabelShort;
        rec.Event = what;
        rec.Mood = mood;
        rec.Strength = Mathf.Clamp01(strength);

        if (GenPlace.TryPlaceThing(raw, pawn.Position, pawn.Map, ThingPlaceMode.Near))
            Messages.Message("NR_BDRecorded".Translate(pawn.LabelShort, what), raw, MessageTypeDefOf.PositiveEvent, historical: false);
    }

    public void OnMemory(Pawn pawn, Thought_Memory thought)
    {
        if (thought?.def == null || thought.def.defName.StartsWith("NR_"))
            return;

        float mood = thought.CurStage?.baseMoodEffect ?? 0f;
        if (Mathf.Abs(mood) < MinMood)
            return;

        TryRecord(pawn, thought.LabelCap, mood > 0f ? BraindanceMood.Bright : BraindanceMood.Dark, Mathf.Abs(mood) / FullStrengthMood);
    }

    public void OnKill(Pawn killer, Pawn victim)
        => TryRecord(killer, "NR_BDEventKill".Translate(victim.LabelShort), BraindanceMood.Combat, KillStrength);

    public override void ExposeData()
    {
        base.ExposeData();
        if (Scribe.mode == LoadSaveMode.Saving)
            _lastRecord.RemoveAll(kv => kv.Key == null || kv.Key.Destroyed);
        Scribe_Collections.Look(ref _lastRecord, "lastRecord", LookMode.Reference, LookMode.Value, ref _keys, ref _values);
        _lastRecord ??= [];
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            _lastRecord.RemoveAll(kv => kv.Key == null);
    }
}

[HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), typeof(Thought_Memory), typeof(Pawn))]
public static class Patch_MemoryThoughtHandler_TryGainMemory
{
    static void Postfix(MemoryThoughtHandler __instance, Thought_Memory newThought)
    {
        try
        {
            BraindanceRecorder.Get()?.OnMemory(__instance.pawn, newThought);
        }
        catch (Exception ex)
        {
            Log.ErrorOnce("[Netrun] Braindance recording failed: " + ex, 0x4E520031);
        }
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
public static class Patch_Pawn_Kill_Braindance
{
    static void Postfix(Pawn __instance, DamageInfo? dinfo)
    {
        try
        {
            if (__instance.Dead && dinfo?.Instigator is Pawn killer && __instance.HostileTo(Faction.OfPlayer))
                BraindanceRecorder.Get()?.OnKill(killer, __instance);
        }
        catch (Exception ex)
        {
            Log.ErrorOnce("[Netrun] Braindance kill recording failed: " + ex, 0x4E520032);
        }
    }
}
