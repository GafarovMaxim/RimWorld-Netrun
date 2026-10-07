using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// Biotech: an AI of the Forge archetype on a powered server with enough trust gives every colony mechanitor
/// extra bandwidth (docs/CONCEPTS_10.md, section 9).
/// </summary>
[HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.TotalBandwidth), MethodType.Getter)]
public static class Patch_MechanitorBandwidth
{
    private static int _cacheTick = -1;
    private static bool _cached;

    public static bool ForgeActive
    {
        get
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now != _cacheTick && (now - _cacheTick >= 250 || now < _cacheTick))
            {
                _cacheTick = now;
                float trust = NetrunDefOf.NR_Cyberspace.pawnNet.forgeBandwidthTrust;
                _cached = AIManager.Get()?.AIs.Any(a => a.Personality == AIPersonality.Forge && a.Active && a.InServer && a.Trust >= trust) == true;
            }
            return _cached;
        }
    }

    public static void Postfix(Pawn_MechanitorTracker __instance, ref int __result)
    {
        try
        {
            if (__instance?.Pawn?.Faction == Faction.OfPlayer && ForgeActive)
                __result += NetrunDefOf.NR_Cyberspace.pawnNet.forgeBandwidth;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] Forge bandwidth: " + e.Message, 0x4E520031);
        }
    }
}

/// <summary>
/// Royalty: the Empire scrubs the colony's trace once in a while for a knight or higher
/// (permit "imperial trace scrub", asked for at the comms console).
/// </summary>
public class RoyalTitlePermitWorker_TraceScrub : RoyalTitlePermitWorker
{
    public override IEnumerable<FloatMenuOption> GetRoyalAidOptions(Map map, Pawn pawn, Faction faction)
    {
        FactionPermit permit = pawn.royalty?.GetPermit(def, faction);
        if (permit == null)
            yield break;
        string label = def.LabelCap;
        if (permit.OnCooldown)
        {
            int days = (int)((permit.LastUsedTick + def.CooldownTicks - Find.TickManager.TicksGame) / (float)GenDate.TicksPerDay) + 1;
            yield return new FloatMenuOption(label + ": " + "NR_PermitCooldown".Translate(days), null);
            yield break;
        }
        yield return new FloatMenuOption(label, () =>
        {
            float amount = NetrunDefOf.NR_Heat.imperialScrubAmount;
            HeatTracker.Get()?.ReduceHeat(amount, "NR_PermitScrubLog".Translate(faction.Name), HeatSource.Other);
            permit.Notify_Used();
            Messages.Message("NR_PermitScrubDone".Translate(faction.Name, amount.ToString("0")), MessageTypeDefOf.PositiveEvent, historical: true);
        });
    }
}
