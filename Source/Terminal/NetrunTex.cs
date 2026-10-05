using UnityEngine;
using Verse;

namespace Netrun;

[StaticConstructorOnStartup]
public static class NetrunTex
{
    public static readonly Texture2D NetTerminal = ContentFinder<Texture2D>.Get("UI/Netrun/Gizmo_NetTerminal");
    public static readonly Texture2D CallFixer = ContentFinder<Texture2D>.Get("UI/Netrun/Gizmo_CallFixer");
    public static readonly Texture2D Dive = ContentFinder<Texture2D>.Get("UI/Netrun/Gizmo_NetrunDive");

    private static readonly Texture2D[] HeatIcons =
    [
        ContentFinder<Texture2D>.Get("UI/Netrun/Heat/Heat_Shadow"),
        ContentFinder<Texture2D>.Get("UI/Netrun/Heat/Heat_Noticed"),
        ContentFinder<Texture2D>.Get("UI/Netrun/Heat/Heat_Tracked"),
        ContentFinder<Texture2D>.Get("UI/Netrun/Heat/Heat_Target"),
        ContentFinder<Texture2D>.Get("UI/Netrun/Heat/Heat_RedZone"),
    ];

    private static readonly Texture2D[] FixerPortraits =
    [
        ContentFinder<Texture2D>.Get("UI/Netrun/Fixers/Fixer_ShadowBroker"), // Broker
        ContentFinder<Texture2D>.Get("UI/Netrun/Fixers/Fixer_Corp"),         // Cleaner
        ContentFinder<Texture2D>.Get("UI/Netrun/Fixers/Fixer_Street"),       // Fence
    ];

    private static readonly Texture2D[] TabIcons =
    [
        ContentFinder<Texture2D>.Get("UI/Netrun/Tabs/Tab_Overview"),
        ContentFinder<Texture2D>.Get("UI/Netrun/Tabs/Tab_Fixers"),
        ContentFinder<Texture2D>.Get("UI/Netrun/Tabs/Tab_Contracts"),
        ContentFinder<Texture2D>.Get("UI/Netrun/Tabs/Tab_AI"),
        ContentFinder<Texture2D>.Get("UI/Netrun/Tabs/Tab_Braindance"),
        ContentFinder<Texture2D>.Get("UI/Netrun/Tabs/Tab_Log"),
    ];

    public static Texture2D HeatIcon(HeatLevel level) => HeatIcons[(int)level];
    public static Texture2D Portrait(FixerSpecialty specialty) => FixerPortraits[(int)specialty];

    /// <summary>Indexed by Dialog_NetTerminal tab order.</summary>
    public static Texture2D TabIcon(int index) => TabIcons[index];

    public static readonly Texture2D AIUse = ContentFinder<Texture2D>.Get("UI/Netrun/Gizmo_AI_Use");
    public static readonly Texture2D AIFeed = ContentFinder<Texture2D>.Get("UI/Netrun/Gizmo_AI_Feed");
    public static readonly Texture2D AIIsolate = ContentFinder<Texture2D>.Get("UI/Netrun/Gizmo_AI_Isolate");
    public static readonly Texture2D AIEmbody = ContentFinder<Texture2D>.Get("UI/Netrun/Gizmo_AI_Embody");
    public static readonly Texture2D AIErase = ContentFinder<Texture2D>.Get("UI/Netrun/Gizmo_AI_Erase");
    public static readonly Texture2D AIToCoprocessor = ContentFinder<Texture2D>.Get("UI/Netrun/Gizmo_AI_CoprocessorUpload");
    public static readonly Texture2D AIReturnServer = ContentFinder<Texture2D>.Get("UI/Netrun/Gizmo_AI_ReturnServer");
    public static readonly Texture2D BDWatch = ContentFinder<Texture2D>.Get("UI/Netrun/Gizmo_BD_Watch");
    /// <summary>Pinned-fixer mark: our own art when it exists, the vanilla pin until then.</summary>
    public static readonly Texture2D Pinned = ContentFinder<Texture2D>.Get("UI/Netrun/Icon_Pinned", reportFailure: false)
        ?? ContentFinder<Texture2D>.Get("UI/Icons/Pin");
    public static readonly Texture2D BDRecord = ContentFinder<Texture2D>.Get("UI/Netrun/Gizmo_BD_Record");

    private static readonly System.Collections.Generic.Dictionary<AIPersonality, Texture2D> Portraits = [];

    /// <summary>Portrait of an AI personality; falls back to the AI tab icon when there is no art yet.</summary>
    public static Texture2D Portrait(AIPersonality personality)
    {
        if (!Portraits.TryGetValue(personality, out Texture2D tex))
        {
            tex = ContentFinder<Texture2D>.Get("UI/Netrun/AI/AI_" + personality, reportFailure: false) ?? TabIcon(3);
            Portraits[personality] = tex;
        }
        return tex;
    }

    /// <summary>Secondary text that stays readable on the dark window background.</summary>
    public static readonly Color DimText = new(0.78f, 0.78f, 0.78f);

    /// <summary>Terminal highlight colors to pick from in the mod settings: cyan, magenta, green, amber, white.</summary>
    public static readonly Color[] Accents =
    [
        new(0.35f, 0.90f, 1.00f),
        new(1.00f, 0.40f, 0.85f),
        new(0.45f, 1.00f, 0.55f),
        new(1.00f, 0.75f, 0.30f),
        Color.white,
    ];

    public static Color Accent => Accents[Mathf.Clamp(NetrunMod.Settings?.TerminalAccent ?? 0, 0, Accents.Length - 1)];

    /// <summary>Green when good, gold in the middle, red when bad; hunger-like values pass highIsGood false.</summary>
    public static Color Gauge(float value, bool highIsGood = true)
    {
        float good = highIsGood ? value : 100f - value;
        return good >= 60f ? ColorLibrary.Green : good >= 30f ? ColorLibrary.Gold : ColorLibrary.RedReadable;
    }
}
