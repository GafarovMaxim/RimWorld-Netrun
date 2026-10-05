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
