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
        new Harmony(HarmonyId).PatchAll();
    }

    public override string SettingsCategory() => "Hacking Expansion: Netrun";

    public override void DoSettingsWindowContents(Rect inRect) => Settings.DoWindowContents(inRect);
}
