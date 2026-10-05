using System.Linq;
using Verse;

namespace Netrun;

/// <summary>
/// The AI socket implant. Deliberately not a Hediff_Cyberlink: Hacking Expansion treats every
/// Hediff_Cyberlink as a cyberlink, and the socket only works on top of one.
/// </summary>
public class Hediff_AISocket : HediffWithComps
{
    public NetAI Passenger => AIManager.Get()?.AIs.FirstOrDefault(a => a.Host == pawn);

    public override string LabelInBrackets
    {
        get
        {
            NetAI ai = Passenger;
            return ai == null ? "NR_AISocketEmpty".Translate() : ai.Name;
        }
    }

    /// <summary>The implant was taken out: the passenger falls back into a fragment.</summary>
    public override void PostRemoved()
    {
        base.PostRemoved();

        NetAI ai = Passenger;
        if (ai != null)
            AIManager.Get()?.EjectFromHost(ai, "NR_AISocketRemoved");
    }
}
