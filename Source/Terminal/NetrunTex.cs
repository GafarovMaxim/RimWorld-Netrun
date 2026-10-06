using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Netrun;

[StaticConstructorOnStartup]
public static class NetrunTex
{
    public static readonly Texture2D NetTerminal = ContentFinder<Texture2D>.Get("UI/Netrun/Gizmo_NetTerminal");
    public static readonly Texture2D CallFixer = ContentFinder<Texture2D>.Get("UI/Netrun/Icon_CallFixer", reportFailure: false) ?? ContentFinder<Texture2D>.Get("UI/Netrun/Gizmo_CallFixer");
    public static readonly Texture2D Dive = ContentFinder<Texture2D>.Get("UI/Netrun/Gizmo_NetrunDive");
    public static readonly Texture2D Cyberspace = ContentFinder<Texture2D>.Get("UI/Netrun/Icon_Cyberspace");
    /// <summary>Home node icon (Textures/UI/Netrun/Nodes/Home_*), or the fallback while there is no art.</summary>
    public static Texture2D HomeNode(string name, Texture2D fallback)
        => CyberIconAt("UI/Netrun/Nodes/Home_" + name) ?? fallback;

    private static Texture2D CyberIconAt(string path)
    {
        if (!CyberIcons.TryGetValue(path, out Texture2D tex))
        {
            tex = ContentFinder<Texture2D>.Get(path, reportFailure: false);
            CyberIcons[path] = tex;
        }
        return tex;
    }

    // ---- Optional art (docs/ART_NEEDED_4.md): used when the file exists in Textures/UI/Netrun/Cyberspace/, otherwise null ----
    public static Texture2D DaemonIcon(DiveDaemon daemon) => CyberIcon("Daemon_" + daemon);
    public static Texture2D EventIcon(string name) => CyberIcon("Event_" + name);
    public static Texture2D HackerGroup => CyberIcon("Icon_HackerGroup");
    public static Texture2D GroupFavor => CyberIcon("Icon_GroupFavor");
    public static Texture2D DiveReport => CyberIcon("Icon_DiveReport");

    /// <summary>The main color of each archetype's icon (measured from the art), for frames around it.</summary>
    public static Color PersonalityColor(AIPersonality p) => NetrunUI.Hex(p switch
    {
        AIPersonality.DataHunter => 0xeba706,
        AIPersonality.Ghost => 0x87bdeb,
        AIPersonality.Muse => 0xd13eeb,
        AIPersonality.Predator => 0xeb1943,
        AIPersonality.Warden => 0x2871eb,
        AIPersonality.Oracle => 0xa47fea,
        AIPersonality.Forge => 0xeb6705,
        AIPersonality.Siren => 0x7448eb,
        AIPersonality.Trickster => 0xeb057b,
        AIPersonality.Doc => 0x04d3eb,
        AIPersonality.Archivist => 0xeba90b,
        AIPersonality.Seed => 0x04eb7c,
        AIPersonality.Echo => 0x21ebbb,
        _ => 0x8b9183,
    });
    public static Texture2D ReportOk => CyberIcon("Dive_GATE_Success");
    public static Texture2D ReportFail => CyberIcon("Dive_GATE_Failed");
    public static readonly Texture2D Disconnect = ContentFinder<Texture2D>.Get("UI/Netrun/Fixers/Disconnect", reportFailure: false);
    public static readonly Texture2D Shop = ContentFinder<Texture2D>.Get("UI/Netrun/Fixers/Fixer_Shop", reportFailure: false);
    public static readonly Texture2D Handshake = ContentFinder<Texture2D>.Get("UI/Netrun/Fixers/Handshake", reportFailure: false);
    public static readonly Texture2D HeatReduction = ContentFinder<Texture2D>.Get("UI/Netrun/Heat/Heat_Reduction", reportFailure: false);

    public static readonly Texture2D HiddenNode = ContentFinder<Texture2D>.Get("UI/Netrun/Cyberspace/Node_NeutralZone");

    private static readonly Dictionary<string, Texture2D> CyberIcons = [];

    /// <summary>Cyberspace icons by file name: Node_*, Program_*, Action_* (null if missing).</summary>
    public static Texture2D CyberIcon(string name)
    {
        if (!CyberIcons.TryGetValue(name, out Texture2D tex))
        {
            tex = ContentFinder<Texture2D>.Get("UI/Netrun/Cyberspace/" + name, reportFailure: false);
            CyberIcons[name] = tex;
        }
        return tex;
    }

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
