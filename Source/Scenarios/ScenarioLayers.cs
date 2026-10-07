using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// RimWorld 1.6 scenarios carry their planet layers (the surface, and the orbit with Odyssey) as scenario parts; a
/// scenario without the surface layer breaks the scenario screen. Instead of guessing the layer XML, the mod's
/// scenarios copy the layers of the vanilla "Crashlanded" scenario at startup, so they match the installed DLCs.
/// </summary>
[StaticConstructorOnStartup]
public static class ScenarioLayers
{
    static ScenarioLayers()
    {
        try
        {
            Scenario source = ScenarioDefOf.Crashlanded?.scenario;
            if (source == null)
                return;
            var sourceSurface = Traverse.Create(source).Field("surfaceLayer").GetValue<ScenPart_PlanetLayer>();
            List<ScenPart> sourceParts = Traverse.Create(source).Field("parts").GetValue<List<ScenPart>>() ?? [];
            foreach (ScenarioDef def in DefDatabase<ScenarioDef>.AllDefs.Where(d => d.defName.StartsWith("NR_Scen_")))
            {
                Scenario scen = def.scenario;
                if (scen == null)
                    continue;
                Traverse surface = Traverse.Create(scen).Field("surfaceLayer");
                if (surface.GetValue() == null && sourceSurface != null)
                    surface.SetValue(sourceSurface.CopyForEditing());
                List<ScenPart> parts = Traverse.Create(scen).Field("parts").GetValue<List<ScenPart>>();
                if (parts == null)
                    continue;
                parts.RemoveAll(p => p == null);
                foreach (ScenPart_PlanetLayer layer in sourceParts.OfType<ScenPart_PlanetLayer>())
                {
                    if (!parts.OfType<ScenPart_PlanetLayer>().Any(p => p.tag == layer.tag))
                        parts.Insert(0, layer.CopyForEditing());
                }
            }
        }
        catch (System.Exception e)
        {
            Log.Error("[Netrun] could not set up the scenarios' planet layers: " + e);
        }
    }
}
