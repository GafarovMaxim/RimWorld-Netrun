using HarmonyLib;
using UnityEngine;
using Verse;

namespace Netrun;

public class NetrunMod : Mod
{
    public const string HarmonyId = "GafarovMaxim.HackingExpansionNetrun";

    public static NetrunSettings Settings { get; private set; }

    public NetrunMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<NetrunSettings>();
        LoadOptionalAssemblies(content);
    }

    /// <summary>
    /// The achievements' tracker (Optional/NetrunVAE.dll) needs Vanilla Achievements Expanded, so it is not in
    /// Assemblies/: it is loaded here, the way the game loads mod assemblies, only when VAE is active. Mod classes are
    /// made before the defs are read, so the achievement defs (MayRequire VAE) find the tracker class.
    /// </summary>
    private static void LoadOptionalAssemblies(ModContentPack content)
    {
        try
        {
            if (!ModsConfig.IsActive("vanillaexpanded.achievements"))
                return;
            string path = System.IO.Path.Combine(content.RootDir, "Optional", "NetrunVAE.dll");
            if (!System.IO.File.Exists(path))
                return;
            System.Reflection.Assembly asm = System.Reflection.Assembly.Load(System.IO.File.ReadAllBytes(path));
            content.assemblies.loadedAssemblies.Add(asm);
            GenTypes.ClearCache();
        }
        catch (System.Exception e)
        {
            Log.Error("[Netrun] could not load the achievements for Vanilla Achievements Expanded: " + e);
        }
    }

    public override string SettingsCategory() => "Hacking Expansion: Netrun";

    public override void DoSettingsWindowContents(Rect inRect) => Settings.DoWindowContents(inRect);
}

/// <summary>
/// Patches are applied on the main thread after loading. Patching from the mod constructor runs on the
/// loading thread and triggers Hacking Expansion's static texture loading there, which breaks its icons.
/// </summary>
[StaticConstructorOnStartup]
public static class NetrunStartup
{
    static NetrunStartup() => new Harmony(NetrunMod.HarmonyId).PatchAll();
}
