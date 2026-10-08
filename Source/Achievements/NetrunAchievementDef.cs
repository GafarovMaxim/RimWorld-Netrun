using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The mod's own copy of an achievement (Defs/NetrunAchievements.xml, generated with the VAE defs by
/// tools/gen_achievements.py). The terminal shows them with progress whether or not VAE is active; frame themes are
/// opened by them.
/// </summary>
public class NetrunAchievementDef : Def
{
    /// <summary>The colony counter (NetrunCounters) and the value that completes the achievement.</summary>
    public string key;
    public int count = 1;
    /// <summary>common, rare or secret (secret ones hide their text until done).</summary>
    public string tier = "common";
    public int points;
    public int order;

    private Texture2D _icon, _frame;

    public Texture2D Icon => _icon ??= ContentFinder<Texture2D>.Get("UI/Netrun/Achievements/" + key, reportFailure: false) ?? BaseContent.BadTex;
    public Texture2D Frame => _frame ??= ContentFinder<Texture2D>.Get("UI/Netrun/Achievements/Frame_" + tier, reportFailure: false);

    public int Progress => Current.Game == null ? 0 : Mathf.Min(NetrunSignals.Value(key), count);
    public bool Done => Current.Game != null && NetrunSignals.Value(key) >= count;
    public bool Secret => tier == "secret";
}
