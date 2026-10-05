using RimWorld;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>Patched onto the vanilla HackingStealth stat: hackers in a cyberpod are harder to detect.</summary>
public class StatPart_InCyberpod : StatPart
{
    private static float Factor => NetrunDefOf.NR_Heat.cyberpodStealthFactor;

    public override void TransformValue(StatRequest req, ref float val)
    {
        if (InCyberpod(req))
            val *= Factor;
    }

    public override string ExplanationPart(StatRequest req)
    {
        if (!InCyberpod(req))
            return null;

        return "NR_StatPartInCyberpod".Translate() + ": x" + Factor.ToString("0.##");
    }

    private static bool InCyberpod(StatRequest req) => req.Thing is Pawn pawn && pawn.TryGetHoldingCyberpod(out _);
}
