using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Art that may not exist yet (docs/ART_NEEDED_4.md). At startup: if the abandoned server's own sprite is there,
/// it replaces the grey AI-server stand-in.
/// </summary>
[StaticConstructorOnStartup]
public static class OptionalArt
{
    private const string DistressServerTex = "Things/Building/Misc/NR_DistressServer";

    static OptionalArt()
    {
        ThingDef server = NetrunDefOf.NR_DistressServer;
        if (server?.graphicData == null || ContentFinder<Texture2D>.Get(DistressServerTex, reportFailure: false) == null)
            return;

        var data = new GraphicData();
        data.CopyFrom(server.graphicData);
        data.texPath = DistressServerTex;
        data.color = Color.white;
        server.graphicData = data;
        server.graphic = data.Graphic;
    }
}
