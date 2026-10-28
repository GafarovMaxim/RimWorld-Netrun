using System.Xml;
using Verse;

namespace Netrun;

/// <summary>
/// Runs its child operations only when the "Costlier HE crafting" setting is on. Mod classes (and their settings) are
/// created before XML patches are applied (LoadedModManager.LoadAllActiveMods), so the setting is known here.
/// </summary>
public class PatchOperationIfSetting : PatchOperationSequence
{
    protected override bool ApplyWorker(XmlDocument xml)
    {
        // Settings can be null only if the mod class failed to load; treat as on (the default).
        if (NetrunMod.Settings != null && !NetrunMod.Settings.CostlierHE)
            return true;
        return base.ApplyWorker(xml);
    }
}
