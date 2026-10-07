using Verse;

namespace Netrun;

/// <summary>
/// Extra setup for the mod's pawn kinds (net factions, scenario colonists). HE installs cyberlinks with a use-job, not
/// a surgery recipe, so vanilla <c>techHediffsRequired</c> cannot give one: the implant goes straight into the brain.
/// </summary>
public class NetPawnKindExtension : DefModExtension
{
    public HediffDef brainImplant;

    public static void Apply(Pawn pawn)
    {
        HediffDef implant = pawn.kindDef?.GetModExtension<NetPawnKindExtension>()?.brainImplant;
        if (implant == null || pawn.health?.hediffSet == null || pawn.health.hediffSet.GetFirstHediffOfDef(implant) != null)
            return;
        BodyPartRecord brain = pawn.health.hediffSet.GetBrain();
        if (brain != null)
            pawn.health.AddHediff(implant, brain);
    }
}
