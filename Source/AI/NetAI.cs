using Verse;

namespace Netrun;

/// <summary>A networked AI the colony owns. It lives in an AI server or sleeps inside a fragment.</summary>
public class NetAI : IExposable, ILoadReferenceable
{
    public int Id;
    public string Name;
    public AIPersonality Personality;
    public int AwakenedTick;

    /// <summary>The server hosting this AI; null while it sleeps in a fragment.</summary>
    public Building Server;

    public bool InServer => Server != null && !Server.Destroyed;
    public bool Powered => InServer && Server.GetComp<CompAIServer>()?.Powered == true;

    public string LocationText
    {
        get
        {
            if (!InServer)
                return "NR_AILocationFragment".Translate();
            if (!Powered)
                return "NR_AILocationServerOff".Translate(Server.LabelCap);
            return "NR_AILocationServer".Translate(Server.LabelCap);
        }
    }

    public string GetUniqueLoadID() => "NetrunAI_" + Id;

    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id");
        Scribe_Values.Look(ref Name, "name");
        Scribe_Values.Look(ref Personality, "personality");
        Scribe_Values.Look(ref AwakenedTick, "awakenedTick");
        Scribe_References.Look(ref Server, "server");
    }
}
