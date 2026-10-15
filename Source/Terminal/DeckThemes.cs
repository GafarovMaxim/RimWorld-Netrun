using System.Linq;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>Classic: the coloured field deck drawn in code. The others have their own silhouette and textures.</summary>
public enum DeckStyle { Classic, Nomad, Street, Kurenai, Iron, Retro, Cyber, Relic }

/// <summary>
/// A look of the terminal casing (docs/CONCEPT_MAIN_MENU.md, section 1): colours of the body, rubber, keys and
/// stencils. The field deck is always there; the others open with an achievement and stay open in every game.
/// </summary>
public sealed class DeckTheme
{
    public string Id;
    public DeckStyle Style;
    /// <summary>The folder under Textures/UI/Netrun/Frame with the theme's art when it differs from the Id.</summary>
    public string Art;
    public Color Top, Bottom, Edge, Rubber, Stencil, KeyFace, Tube, TubeLight, TubeDark;
    /// <summary>The achievement counter that opens the theme (null: always open) and how much of it.</summary>
    public string UnlockKey;
    public int UnlockCount;

    public string Label => ("NR_DeckTheme_" + Id).Translate();
    public string Description => ("NR_DeckThemeDesc_" + Id).Translate();
}

public static class DeckThemes
{
    private static Color Hex(int rgb) => NetrunUI.Hex(rgb);

    public static readonly DeckTheme[] All =
    [
        new DeckTheme
        {
            // The NR-1 cyberdeck (tools/gen_deck_classic.py); the Id stays Field, the old drawn casing is the fallback.
            Id = "Field", Style = DeckStyle.Cyber, Art = "Cyber", Top = Hex(0x23282e), Bottom = Hex(0x101317), Edge = Hex(0x434b54), Rubber = Hex(0x090a0c),
            Stencil = new Color(1f, 1f, 1f, 0.58f), KeyFace = Hex(0x252a31), Tube = Hex(0x2a3036), TubeLight = Hex(0x4b545e), TubeDark = Hex(0x15181c),
        },
        // Iron Company: a rugged field tablet in chipped olive paint (tools/gen_deck_iron.py). The Id stays Milspec, so a
        // theme opened in an older version stays open.
        new DeckTheme
        {
            Id = "Milspec", Style = DeckStyle.Iron, Art = "Iron", Top = Hex(0x343a26), Bottom = Hex(0x171a10), Edge = Hex(0x5a6244), Rubber = Hex(0x0f110a),
            Stencil = new Color(0.93f, 0.9f, 0.72f, 0.62f), KeyFace = Hex(0x2a2f1e), Tube = Hex(0x3a3f2c), TubeLight = Hex(0x646b4c), TubeDark = Hex(0x1a1d12),
            UnlockKey = "CleanStreak", UnlockCount = 10,
        },
        // A yellow panel with a neon Heat tube, a holographic kitsune and graffiti (tools/gen_deck_street.py).
        new DeckTheme
        {
            Id = "Street", Style = DeckStyle.Street, Top = Hex(0x33262a), Bottom = Hex(0x150f12), Edge = Hex(0x6a4a42), Rubber = Hex(0x0c0809),
            Stencil = new Color(1f, 0.83f, 0.29f, 0.7f), KeyFace = Hex(0x2e2226), Tube = Hex(0x3b2c2c), TubeLight = Hex(0x7a5a4a), TubeDark = Hex(0x1a1213),
            UnlockKey = "BlackICEPassed", UnlockCount = 1,
        },
        // Kurenai Systems: a corporate exec terminal on light printed stock (tools/gen_deck_kurenai.py). The Id stays
        // OpsPad, so a theme opened in an older version stays open.
        new DeckTheme
        {
            Id = "OpsPad", Style = DeckStyle.Kurenai, Art = "Kurenai", Top = Hex(0xc4c8c4), Bottom = Hex(0x8e938f), Edge = Hex(0x6c726e), Rubber = Hex(0x2a2d2f),
            Stencil = new Color(0.08f, 0.09f, 0.1f, 0.75f), KeyFace = Hex(0x3a3f44), Tube = Hex(0x9aa09c), TubeLight = Hex(0xdfe3df), TubeDark = Hex(0x5e6460),
            UnlockKey = "ContractsDone", UnlockCount = 25,
        },
        // A dusty road case with a solar panel, a paracord handle, a battery on straps (tools/gen_deck_nomad.py).
        new DeckTheme
        {
            Id = "Nomad", Style = DeckStyle.Nomad, Top = Hex(0x5a4f37), Bottom = Hex(0x3b3325), Edge = Hex(0x6a5e44), Rubber = Hex(0x15130f),
            Stencil = new Color(0.86f, 0.79f, 0.62f, 0.62f), KeyFace = Hex(0x3d3629), Tube = Hex(0x3a3b35), TubeLight = Hex(0x6a685c), TubeDark = Hex(0x1a1914),
            UnlockKey = "Dives", UnlockCount = 50,
        },
        // NR-80: a cream retro handheld with sunset bars and pixel Heat flames (tools/gen_deck_retro.py). Opens with a
        // season spent in Shadow: an old-school runner keeps quiet.
        new DeckTheme
        {
            Id = "Retro", Style = DeckStyle.Retro, Top = Hex(0xded6c0), Bottom = Hex(0xb8b09a), Edge = Hex(0x403e46), Rubber = Hex(0x2a2826),
            Stencil = new Color(0.16f, 0.13f, 0.12f, 0.8f), KeyFace = Hex(0xc4beae), Tube = Hex(0x9aa776), TubeLight = Hex(0xc8d4a0), TubeDark = Hex(0x5a6444),
            UnlockKey = "ShadowSeason", UnlockCount = 1,
        },
        // The Relic: an old worn field deck (tools/gen_deck_relic.py). Opens after ten dives (provisional).
        new DeckTheme
        {
            Id = "Relic", Style = DeckStyle.Relic, Top = Hex(0xd2cab6), Bottom = Hex(0xa89e88), Edge = Hex(0x403e3a), Rubber = Hex(0x1e1e1e),
            Stencil = new Color(0.2f, 0.19f, 0.16f, 0.85f), KeyFace = Hex(0x7c7c76), Tube = Hex(0xec7828), TubeLight = Hex(0xffa060), TubeDark = Hex(0x7a3a14),
            UnlockKey = "OldNetEntered", UnlockCount = 1,
        },
    ];

    /// <summary>The chosen theme when it is open, the field deck otherwise.</summary>
    public static DeckTheme Current => All.FirstOrDefault(t => t.Id == NetrunMod.Settings.FrameTheme && Unlocked(t)) ?? All[0];

    /// <summary>Open by its achievement counter in this game, or earned before in any game.</summary>
    public static bool Unlocked(DeckTheme theme)
    {
        if (theme.UnlockKey == null || NetrunMod.Settings.UnlockedThemes.Contains(theme.Id))
            return true;
        if (InGame && NetrunSignals.Value(theme.UnlockKey) >= theme.UnlockCount)
        {
            NetrunMod.Settings.UnlockedThemes.Add(theme.Id);
            NetrunMod.Settings.Write();
            return true;
        }
        return false;
    }

    private static bool InGame => Verse.Current.Game != null;

    /// <summary>What opens a locked theme: the achievement's name and the goal.</summary>
    public static string LockText(DeckTheme theme)
    {
        NetrunAchievementDef ach = DefDatabase<NetrunAchievementDef>.AllDefs.FirstOrDefault(a => a.key == theme.UnlockKey);
        string name = ach?.LabelCap ?? (TaggedString)theme.UnlockKey;
        int have = InGame ? Mathf.Min(NetrunSignals.Value(theme.UnlockKey), theme.UnlockCount) : 0;
        return "NR_DeckThemeLocked".Translate(name, have, theme.UnlockCount);
    }

    public static void Select(DeckTheme theme)
    {
        if (!Unlocked(theme))
            return;
        NetrunMod.Settings.FrameTheme = theme.Id;
        NetrunMod.Settings.Write();
    }

}
