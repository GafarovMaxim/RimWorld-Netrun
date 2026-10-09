using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// The glossary (docs/CONCEPT_MAIN_MENU.md, section 3.5): a mod tooltip that mentions a key term (Heat, ICE, Black ICE,
/// Netsick, cyberdata, trace) gets a short definition block under it. Terms and definitions are in the Keyed files
/// (NR_GlossTerms_* are the words to look for, comma-separated, per language). Results are cached per text.
/// </summary>
public static class Glossary
{
    private static readonly string[] Keys = ["BlackIce", "Ice", "Heat", "Netsick", "Cyberdata", "Trace"];
    private static readonly Dictionary<string, string> Cache = [];
    private static List<(string key, string[] words)> _terms;
    private static string _termsLang;

    private static List<(string key, string[] words)> Terms()
    {
        string lang = LanguageDatabase.activeLanguage?.folderName ?? "";
        if (_terms != null && _termsLang == lang)
            return _terms;
        _termsLang = lang;
        Cache.Clear();
        _terms = [];
        foreach (string key in Keys)
        {
            string[] words = ((string)("NR_GlossTerms_" + key).Translate()).Split(',');
            for (int i = 0; i < words.Length; i++)
                words[i] = words[i].Trim();
            _terms.Add((key, words));
        }
        return _terms;
    }

    /// <summary>Only for a newcomer: the Newcomer preset, or the first 15 days of a game (decision by Maxim, 2026-10-09).</summary>
    public static bool ForNewcomer => NetrunMod.Settings.Difficulty == HeatDifficulty.Easy || Current.Game == null || GenDate.DaysPassed < 15;

    /// <summary>The tooltip with definitions of the terms it mentions (at most two), or the same text.</summary>
    public static string With(string text)
    {
        if (!NetrunMod.Settings.Glossary || text.NullOrEmpty() || !ForNewcomer)
            return text;
        if (Cache.TryGetValue(text, out string done))
            return done;
        if (Cache.Count > 300)
            Cache.Clear();
        string plain = text.StripTags();
        var sb = new StringBuilder();
        int found = 0;
        bool ice = false;
        foreach ((string key, string[] words) in Terms())
        {
            if (found >= 2)
                break;
            if (key == "Ice" && ice)
                continue;
            if (!Mentions(plain, words))
                continue;
            if (key == "BlackIce")
                ice = true;
            sb.Append("\n").Append(("NR_Gloss_" + key).Translate().Colorize(NetrunUI.Muted));
            found++;
        }
        string result = found == 0 ? text : text + "\n" + ((string)"NR_GlossCaption".Translate()).Colorize(NetrunUI.Accent) + sb;
        Cache[text] = result;
        return result;
    }

    private static bool Mentions(string text, string[] words)
    {
        foreach (string w in words)
        {
            if (w.Length == 0)
                continue;
            int i = text.IndexOf(w, System.StringComparison.OrdinalIgnoreCase);
            while (i >= 0)
            {
                bool startOk = i == 0 || !char.IsLetter(text[i - 1]);
                bool endOk = i + w.Length >= text.Length || !char.IsLetter(text[i + w.Length]) || w.Length >= 5;
                if (startOk && endOk)
                    return true;
                i = text.IndexOf(w, i + 1, System.StringComparison.OrdinalIgnoreCase);
            }
        }
        return false;
    }
}
