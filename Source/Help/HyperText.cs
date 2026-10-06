using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Text with links to the Cyberpedia: "[[DefName|text]]" and the key words of help pages, coloured, underlined and
/// clickable. A page is linked only at its first mention in one block (an article, a description), so a word that
/// repeats nearby is not a row of links.
/// </summary>
public static class HyperText
{
    public struct Segment
    {
        public string Text;
        public HelpPageDef Link;
    }

    private static readonly Regex ExplicitLink = new(@"\[\[(\w+)\|([^\]]+)\]\]");
    private static readonly Dictionary<string, Regex> Patterns = [];
    private static List<(string word, HelpPageDef page)> _words;
    private static float _wordsAt = -10f;

    /// <summary>Key words of unlocked pages, longest first; refreshed every few seconds (pages unlock in play).</summary>
    private static List<(string word, HelpPageDef page)> Words()
    {
        if (_words == null || Time.realtimeSinceStartup - _wordsAt > 3f)
        {
            HelpProgress progress = HelpProgress.Get();
            _words = DefDatabase<HelpPageDef>.AllDefs
                .Where(p => p.keywords.Count > 0 && progress?.Unlocked(p) != false)
                .SelectMany(p => p.keywords.Select(k => (k, p)))
                .OrderByDescending(x => x.k.Length).ToList();
            _wordsAt = Time.realtimeSinceStartup;
        }
        return _words;
    }

    private static Regex Pattern(string word)
    {
        if (!Patterns.TryGetValue(word, out Regex re))
            Patterns[word] = re = new Regex(@"(?<![\w])" + Regex.Escape(word) + @"(?![\w])", RegexOptions.IgnoreCase);
        return re;
    }

    /// <summary>
    /// Splits a line into text and links. <paramref name="used"/> holds the pages already linked in this block
    /// (and the page the text sits on), and gets the new ones.
    /// </summary>
    public static List<Segment> Split(string line, HashSet<HelpPageDef> used)
    {
        var parts = new List<Segment>();
        int last = 0;
        foreach (Match m in ExplicitLink.Matches(line))
        {
            if (m.Index > last)
                parts.Add(new Segment { Text = line.Substring(last, m.Index - last) });
            parts.Add(new Segment { Text = m.Groups[2].Value, Link = DefDatabase<HelpPageDef>.GetNamedSilentFail(m.Groups[1].Value) });
            last = m.Index + m.Length;
        }
        if (last < line.Length)
            parts.Add(new Segment { Text = line.Substring(last) });

        foreach ((string word, HelpPageDef target) in Words())
        {
            if (used.Contains(target))
                continue;
            Regex pattern = Pattern(word);
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Link != null)
                    continue;
                Match m = pattern.Match(parts[i].Text);
                if (!m.Success)
                    continue;
                string text = parts[i].Text;
                parts.RemoveAt(i);
                parts.Insert(i, new Segment { Text = text.Substring(m.Index + m.Length) });
                parts.Insert(i, new Segment { Text = m.Value, Link = target });
                parts.Insert(i, new Segment { Text = text.Substring(0, m.Index) });
                used.Add(target);
                break;
            }
        }
        parts.RemoveAll(s => s.Text.Length == 0);
        return parts;
    }

    /// <summary>Lays segments out word by word in the current font; links call <paramref name="go"/>. Returns the height.</summary>
    public static float Flow(List<Segment> segments, float x, float y, float width, bool draw, System.Action<HelpPageDef> go, Color? textColor = null)
    {
        float lineH = Text.LineHeight;
        float cx = 0f, cy = 0f;
        float space = Text.CalcSize("a a").x - Text.CalcSize("aa").x;
        foreach (Segment seg in segments)
        {
            string[] words = seg.Text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                if (i > 0)
                    cx += space;
                if (word.Length == 0)
                    continue;
                float ww = Text.CalcSize(word).x;
                if (cx > 0f && cx + ww > width)
                {
                    cx = 0f;
                    cy += lineH;
                }
                if (draw)
                {
                    var r = new Rect(x + cx, y + cy, ww + 1f, lineH);
                    if (seg.Link == null || go == null)
                    {
                        Widgets.Label(r, textColor is { } tc ? word.Colorize(tc) : word);
                    }
                    else
                    {
                        bool hover = Mouse.IsOver(r);
                        Color c = hover ? Color.white : NetrunUI.Accent;
                        Widgets.Label(r, word.Colorize(c));
                        GUI.color = new Color(c.r, c.g, c.b, 0.5f);
                        Widgets.DrawLineHorizontal(r.x, r.yMax - 3f, ww);
                        GUI.color = Color.white;
                        NetrunUI.Tip(r, "NR_HelpLinkTip".Translate(seg.Link.LabelCap));
                        if (Widgets.ButtonInvisible(r))
                            go(seg.Link);
                    }
                }
                cx += ww;
            }
        }
        return cy + lineH;
    }

    // ---- Outside the Cyberpedia: one block of text, links open the page in the terminal ----

    private static readonly Dictionary<string, List<Segment>> Blocks = [];
    private static readonly Dictionary<string, HashSet<HelpPageDef>> Groups = [];

    /// <summary>
    /// The segments of one text. Texts of the same <paramref name="group"/> (lines of one card) share the linked pages,
    /// so a page is linked only in the first line that mentions it.
    /// </summary>
    private static List<Segment> Block(string text, string group)
    {
        string key = (group ?? string.Empty) + "" + text;
        if (!Blocks.TryGetValue(key, out List<Segment> segs))
        {
            if (Blocks.Count > 600)
            {
                Blocks.Clear();
                Groups.Clear();
            }
            HashSet<HelpPageDef> used = [];
            if (group != null && !Groups.TryGetValue(group, out used))
                Groups[group] = used = [];
            // Plain text only: rich-text tags would be split into words.
            Blocks[key] = segs = Split(text.StripTags(), used);
        }
        return segs;
    }

    /// <summary>Height of <paramref name="text"/> as linked text in the current font.</summary>
    public static float Height(string text, float width, string group = null) => Flow(Block(text, group), 0f, 0f, width, false, null);

    /// <summary>Draws <paramref name="text"/> with its first mentions linked; a click opens the page in the terminal.</summary>
    public static float Label(Rect rect, string text, Color? color = null, string group = null) =>
        Flow(Block(text, group), rect.x, rect.y, rect.width, true, Dialog_NetTerminal.OpenPage, color);
}
