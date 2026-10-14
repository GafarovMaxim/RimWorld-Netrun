using System.Linq;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The terminal font Tektur (SIL OFL, see Fonts/OFL.txt), shipped in AssetBundles/netrun_fonts_{win,mac,linux}.
/// While the mod's windows draw, it replaces the game's font for the three text sizes and is put back right after,
/// so the rest of the game keeps its own font. If the bundle or the font is missing, nothing changes.
/// </summary>
[StaticConstructorOnStartup]
public static class NetrunFont
{
    private const string AssetPath = "assets/netrun/fonts/tektur.ttf";

    /// <summary>
    /// Off: the UnityPy-built bundle loads but draws no text in game, and swapping fonts on the shared styles left
    /// Unity's text measuring cache with wrong sizes. Kept for a bundle built in the Unity Editor later.
    /// </summary>
    public static bool Enabled => false;

    public static readonly Font Tektur;

    private static readonly Font[] Saved = new Font[3];
    private static readonly Font[] SavedFields = new Font[3];
    private static int _depth;

    static NetrunFont()
    {
        if (!Enabled)
            return;
        try
        {
            ModContentPack mod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => m.PackageId.Equals(NetrunMod.HarmonyId.ToLowerInvariant()));
            foreach (AssetBundle bundle in mod?.assetBundles?.loadedAssetBundles ?? [])
            {
                Tektur = bundle.LoadAsset<Font>(AssetPath);
                if (Tektur != null)
                    break;
            }
        }
        catch (System.Exception e)
        {
            Log.Warning("[Netrun] Could not load the Tektur font, the standard font is used: " + e.Message);
        }
        if (Tektur == null && Prefs.DevMode)
            Log.Message("[Netrun] Tektur font bundle not found: the terminal uses the standard font.");
    }

    public static bool Available => Tektur != null;

    private static bool Wanted => Tektur != null && NetrunMod.Settings.TerminalFont;

    /// <summary>Switch the text styles to Tektur; true when it did (pass it to <see cref="Pop"/>).</summary>
    public static bool Push()
    {
        if (!Wanted)
            return false;
        if (_depth++ == 0)
        {
            for (int i = 0; i < 3; i++)
            {
                Saved[i] = Text.fontStyles[i].font;
                SavedFields[i] = Text.textFieldStyles[i].font;
                Text.fontStyles[i].font = Tektur;
                Text.textFieldStyles[i].font = Tektur;
            }
        }
        return true;
    }

    public static void Pop(bool pushed)
    {
        if (!pushed || _depth == 0)
            return;
        if (--_depth == 0)
        {
            for (int i = 0; i < 3; i++)
            {
                Text.fontStyles[i].font = Saved[i];
                Text.textFieldStyles[i].font = SavedFields[i];
            }
        }
    }
}
