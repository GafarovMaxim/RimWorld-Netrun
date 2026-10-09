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
        // Rich-text tags would be laid out word by word and show up as text: drop them (and a trailing mod name).
        line = HelpPageDef.StripModName(line).StripTags();
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

    /// <summary>One laid-out piece: a run of plain words on one row, or one word of a link.</summary>
    private struct Run
    {
        public Rect R;
        public string Text;
        public int Seg;
    }

    private sealed class Layout
    {
        public readonly List<Run> Runs = [];
        public float Height;
    }

    // Layouts by (segments, width, font, font size): the words are measured once, not on every GUI event
    // (docs/PERF_AUDIT_4.md, 4). The segment lists are themselves cached, so the reference is a stable key.
    private static readonly Dictionary<(List<Segment>, int, int, int), Layout> Layouts = [];

    private static Layout LayoutOf(List<Segment> segments, float width)
    {
        var key = (segments, Mathf.RoundToInt(width), (int)Text.Font, Text.CurFontStyle.fontSize);
        if (Layouts.TryGetValue(key, out Layout done))
            return done;
        if (Layouts.Count > 500)
            Layouts.Clear();
        var layout = new Layout();
        float lineH = NetrunText.LineHeight;
        float cx = 0f, cy = 0f;
        float space = Text.CalcSize("a a").x - Text.CalcSize("aa").x;
        for (int s = 0; s < segments.Count; s++)
        {
            string[] parts = segments[s].Text.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                    cx += space;
                if (parts[i].Length == 0)
                    continue;
                float ww = Text.CalcSize(parts[i]).x;
                if (cx > 0f && cx + ww > width)
                {
                    cx = 0f;
                    cy += lineH;
                }
                var r = new Rect(cx, cy, ww + 1f, lineH);
                // One run per word: a joined line of words drifted from the measured positions and links stood off.
                layout.Runs.Add(new Run { R = r, Text = parts[i], Seg = s });
                cx += ww;
            }
        }
        layout.Height = cy + lineH;
        Layouts[key] = layout;
        return layout;
    }

    /// <summary>Lays segments out in the current font (cached); links call <paramref name="go"/>. Returns the height.</summary>
    public static float Flow(List<Segment> segments, float x, float y, float width, bool draw, System.Action<HelpPageDef> go, Color? textColor = null)
    {
        Layout layout = LayoutOf(segments, width);
        if (!draw)
            return layout.Height;
        List<Run> runs = layout.Runs;
        // Only the rows inside the visible part of a scroll view are drawn.
        Rect visible = NetrunUI.ClipRect();
        bool clip = visible.width > 0f;

        // A link is one thing: hovering any of its words lights the whole phrase, one tooltip, one click.
        int hovered = -1;
        if (go != null)
            for (int k = 0; k < runs.Count; k++)
                if (segments[runs[k].Seg].Link != null && Mouse.IsOver(Shift(runs[k].R, x, y)))
                    hovered = runs[k].Seg;
        Color before = GUI.color;
        Color plain = textColor is { } tc ? tc * before : before;
        for (int k = 0; k < runs.Count; k++)
        {
            Run run = runs[k];
            Rect r = Shift(run.R, x, y);
            if (clip && (r.yMax < visible.y || r.y > visible.yMax))
                continue;
            HelpPageDef link = go == null ? null : segments[run.Seg].Link;
            if (link == null)
            {
                GUI.color = plain;
                Widgets.Label(r, run.Text);
                GUI.color = before;
                continue;
            }
            Color c = hovered == run.Seg ? Color.white : NetrunUI.Accent;
            GUI.color = c * before;
            Widgets.Label(r, run.Text);
            // The underline bridges the space to the previous word of the same link on this row.
            bool joined = k > 0 && runs[k - 1].Seg == run.Seg && Mathf.Approximately(runs[k - 1].R.y, run.R.y);
            float from = joined ? runs[k - 1].R.xMax + x - 1f : r.x;
            GUI.color = new Color(c.r, c.g, c.b, 0.5f);
            Widgets.DrawLineHorizontal(from, r.yMax - 3f, r.xMax - 1f - from);
            GUI.color = before;
            Rect hit = joined ? new Rect(from, r.y, r.xMax - from, r.height) : r;
            if (Mouse.IsOver(hit))
                NetrunUI.Tip(hit, "NR_HelpLinkTip".Translate(link.LabelCap));
            if (Widgets.ButtonInvisible(hit))
                go(link);
        }
        return layout.Height;
    }

    private static Rect Shift(Rect r, float x, float y) => new(r.x + x, r.y + y, r.width, r.height);

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
