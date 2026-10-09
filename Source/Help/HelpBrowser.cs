using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Netrun;

/// <summary>
/// Cyberpedia, drawn inside the terminal as one more screen (docs/HELP.md): topics on the left, the article on
/// the right with back/forward next to its title, search on top. Keeps its own history.
/// </summary>
public partial class HelpBrowser
{
    private const float TreeWidth = 260f;

    private HelpPageDef _page;
    /// <summary>Changes on every page visit so the title types out again.</summary>
    private int _openStamp;
    private readonly List<HelpPageDef> _back = [];
    private readonly List<HelpPageDef> _forward = [];
    private string _search = string.Empty;
    private Vector2 _treeScroll;
    private float _treeHeight;
    private Vector2 _articleScroll;
    private float _articleHeight = 600f;

    /// <summary>Opens a terminal screen by name ("> open:" links).</summary>
    public System.Action<string> OpenScreen;
    /// <summary>Leaves the Cyberpedia back to the screen it was opened from.</summary>
    public System.Action Leave;
    /// <summary>The label of that screen, for "◄ To ...".</summary>
    public string FromLabel;

    public HelpPageDef Page => _page;

    public void Show(HelpPageDef page)
    {
        HelpProgress progress = HelpProgress.Get();
        if (progress != null)
        {
            progress.HelpOpened = true;
            progress.Refresh(Find.CurrentMap);
        }
        _back.Clear();
        _forward.Clear();
        _search = string.Empty;
        _page = page ?? HelpPageDef.Topics.FirstOrDefault();
        _openStamp++;
        _articleScroll = Vector2.zero;
    }

    /// <summary>The search box, drawn in the terminal's title bar.</summary>
    public void DrawSearch(Rect rect)
    {
        Widgets.DrawBoxSolid(rect, NetrunUI.BgCard);
        string before = _search;
        GUI.SetNextControlName("NR_HelpSearch");
        _search = Widgets.TextField(rect.ContractedBy(2f), _search);
        if (_search.NullOrEmpty() && GUI.GetNameOfFocusedControl() != "NR_HelpSearch")
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 8f, rect.y, rect.width - 10f, rect.height), ("⌕ " + "NR_HelpSearch".Translate()).Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
        }
        if (before != _search)
            _treeScroll = Vector2.zero;
    }

    public void Draw(Rect rect)
    {
        GuideRegistry.Area("help.tree", new Rect(rect.x, rect.y, TreeWidth, rect.height));
        GuideRegistry.Area("help.article", new Rect(rect.x + TreeWidth + 1f, rect.y, rect.width - TreeWidth - 1f, rect.height));
        DrawTree(new Rect(rect.x, rect.y, TreeWidth, rect.height));
        GUI.color = new Color(1f, 1f, 1f, 0.12f);
        Widgets.DrawLineVertical(rect.x + TreeWidth, rect.y, rect.height);
        GUI.color = Color.white;
        DrawArticle(new Rect(rect.x + TreeWidth + 1f, rect.y, rect.width - TreeWidth - 1f, rect.height));
    }

    // ---- Left: topics with icons (or search results) ----

    private void DrawTree(Rect rect)
    {
        Rect r = rect.ContractedBy(10f);
        float y = NetrunUI.Section(new Rect(r.x, r.y, r.width, 32f), (_search.NullOrEmpty() ? "NR_HelpTopics" : "NR_HelpResults").Translate());
        var outer = new Rect(r.x, y, r.width, r.yMax - y);
        List<(HelpPageDef page, int depth)> rows = Rows();
        bool scrolls = _treeHeight > outer.height;
        var view = new Rect(0f, 0f, outer.width - (scrolls ? 16f : 0f), Mathf.Max(_treeHeight, outer.height));
        NetrunUI.BeginScroll(outer, ref _treeScroll, view);
        float ry = 0f;
        HelpProgress progress = HelpProgress.Get();
        if (rows.Count == 0)
        {
            Widgets.Label(new Rect(0f, 0f, view.width, 40f), "NR_HelpNoResults".Translate().Colorize(NetrunUI.Muted));
            ry = 40f;
        }
        foreach ((HelpPageDef page, int depth) in rows)
        {
            bool locked = progress != null && !progress.Unlocked(page);
            bool current = page == _page;
            bool openTopic = depth == 0 && _page?.Topic == page;
            float indent = depth * 14f;
            float iconSize = depth == 0 ? 22f : 16f;
            Texture2D icon = locked ? HelpIcons.Lock : HelpIcons.For(page);
            float textX = indent + 10f + (icon != null ? iconSize + 6f : 0f);
            float textW = view.width - textX - 18f;
            Text.Font = depth == 0 ? GameFont.Small : GameFont.Tiny;
            string label = locked && page.lockedTitle ? "???" : (string)page.LabelCap;
            float h = Mathf.Max(depth == 0 ? 30f : 22f, Text.CalcHeight(label, textW) + 6f);
            var row = new Rect(indent, ry, view.width - indent, h);

            if (depth == 0)
            {
                Widgets.DrawBoxSolid(row, openTopic ? NetrunUI.BgCardAlt : NetrunUI.BgCard);
                Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), locked ? NetrunUI.Muted : page.Color);
            }
            else if (current)
            {
                Widgets.DrawBoxSolid(row, NetrunUI.BgCard);
                Widgets.DrawBoxSolid(new Rect(row.x, row.y, 2f, row.height), page.Topic.Color);
            }
            if (Mouse.IsOver(row))
                Widgets.DrawHighlight(row);
            if (icon != null)
            {
                GUI.color = locked ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
                GUI.DrawTexture(new Rect(indent + 8f, row.y + (h - iconSize) / 2f, iconSize, iconSize), NetrunTint.OfAny(icon, page.Topic.Color));
                GUI.color = Color.white;
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            Color color = locked ? NetrunUI.Muted : current || depth == 0 ? NetrunUI.TextColor : NetrunUI.Muted;
            Widgets.Label(new Rect(textX, row.y, textW, h), label.Colorize(color));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // "New" and "you are here" as small dots on the right, no text over the label.
            if (progress?.IsNew(page) == true)
            {
                Text.Font = GameFont.Tiny;
                string tag = "NR_HelpNewTag".Translate();
                float tw = Text.CalcSize(tag).x + 8f;
                var pill = new Rect(row.xMax - tw - 4f, row.center.y - 8f, tw, 16f);
                Widgets.DrawBoxSolid(pill, NetrunUI.Ok);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(pill, tag.Colorize(NetrunUI.BgWindow));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                NetrunUI.Tip(pill, "NR_HelpNew".Translate());
            }
            else if (current)
                Dot(new Vector2(row.xMax - 9f, row.center.y), page.Topic.Color, "NR_HelpHere".Translate());

            if (locked)
                NetrunUI.Tip(row, page.lockHint.NullOrEmpty() ? (string)"NR_HelpLocked".Translate() : page.lockHint);
            if (Widgets.ButtonInvisible(row))
                Go(page);
            ry += h + 2f;
        }
        _treeHeight = ry + 4f;
        Widgets.EndScrollView();
    }

    private static void Dot(Vector2 center, Color color, string tip)
    {
        var r = new Rect(center.x - 4f, center.y - 4f, 8f, 8f);
        GUI.color = color;
        GUI.DrawTexture(r, NetrunUI.Disc);
        GUI.color = Color.white;
        NetrunUI.Tip(r.ExpandedBy(4f), tip);
    }

    private List<(HelpPageDef, int)> Rows()
    {
        var rows = new List<(HelpPageDef, int)>();
        if (!_search.NullOrEmpty())
        {
            string q = _search.Trim().ToLowerInvariant();
            HelpProgress progress = HelpProgress.Get();
            foreach (HelpPageDef page in DefDatabase<HelpPageDef>.AllDefs.OrderBy(p => p.Topic.order).ThenBy(p => p.IsTopic ? 0 : 1).ThenBy(p => p.order))
            {
                if (progress != null && !progress.Unlocked(page) && page.lockedTitle)
                    continue;
                if (page.label.ToLowerInvariant().Contains(q) || HelpPageDef.Tokens(page.body ?? string.Empty).ToLowerInvariant().Contains(q))
                    rows.Add((page, 0));
            }
            return rows;
        }
        foreach (HelpPageDef topic in HelpPageDef.Topics)
        {
            rows.Add((topic, 0));
            if (_page?.Topic == topic)
                rows.AddRange(topic.Children.Select(c => (c, 1)));
        }
        return rows;
    }

    // ---- Right: the article ----

    private void DrawArticle(Rect rect)
    {
        if (_page == null)
            return;
        HelpProgress progress = HelpProgress.Get();
        bool locked = progress != null && !progress.Unlocked(_page);
        if (!locked)
            progress?.MarkSeen(_page);

        Rect area = rect.ContractedBy(16f);
        // Navigation row: "◄" (or "◄ To <screen>" on the first article), "►", breadcrumbs.
        float nx = area.x;
        string backLabel = _back.Count > 0 ? "◄" : "◄ " + "NR_HelpBackTo".Translate(FromLabel ?? string.Empty);
        float bw = NetrunUI.ButtonWidth(backLabel) - 6f;
        if (NetrunUI.Button(new Rect(nx, area.y, bw, 26f), backLabel, NetrunUI.Accent, true, _back.Count > 0 ? (string)"NR_HelpBack".Translate() : null))
        {
            if (_back.Count > 0)
                Step(_back, _forward);
            else
                Leave?.Invoke();
        }
        nx += bw + 6f;
        if (NetrunUI.Button(new Rect(nx, area.y, 34f, 26f), "►", NetrunUI.Accent, _forward.Count > 0 ? true : (AcceptanceReport)false, "NR_HelpForward".Translate()))
            Step(_forward, _back);
        nx += 44f;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        string crumbs = ((string)"NR_HelpCrumbRoot".Translate()).ToUpper() + (_page.parent != null ? " › " + _page.parent.label.ToUpper() : string.Empty);
        Widgets.Label(new Rect(nx, area.y, area.xMax - nx, 26f), crumbs.Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        var outer = new Rect(area.x, area.y + 34f, area.width, area.height - 34f);
        bool scrolls = _articleHeight > outer.height;
        var view = new Rect(0f, 0f, outer.width - (scrolls ? 16f : 0f), Mathf.Max(_articleHeight, outer.height));
        NetrunUI.BeginScroll(outer, ref _articleScroll, view);
        float y = 0f;
        float w = view.width;

        // Header: a big icon and the title.
        Texture2D icon = HelpIcons.For(_page);
        float tx = 0f;
        if (icon != null)
        {
            var ir = new Rect(0f, y, 48f, 48f);
            if (locked)
                GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(ir, icon);
            GUI.color = Color.white;
            tx = 58f;
        }
        Text.Font = GameFont.Medium;
        string title = locked && _page.lockedTitle ? "???" : (string)_page.LabelCap;
        float th = Mathf.Max(icon != null ? 48f : 0f, Text.CalcHeight(title, w - tx));
        Text.Anchor = icon != null ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft;
        Widgets.Label(new Rect(tx, y, w - tx, th), NetrunUI.Typed("help:" + _page.defName + _openStamp, title, 60f).Colorize(_page.Topic.Color));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        y += th + 10f;

        if (locked)
        {
            string hint = _page.lockHint.NullOrEmpty() ? (string)"NR_HelpLocked".Translate() : _page.lockHint;
            Widgets.Label(new Rect(0f, y, w, 60f), ("🔒 " + hint).Colorize(NetrunUI.Muted));
            y += 60f;
        }
        else
        {
            // "[[cards]]" turns the bullet list right after it into a set of tiles with a card for the picked one.
            string[] lines = HelpPageDef.Tokens(_page.body ?? string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd();
                if (line == "[[cards]]")
                {
                    var items = new List<string>();
                    while (i + 1 < lines.Length && lines[i + 1].TrimEnd().StartsWith("- "))
                        items.Add(lines[++i].TrimEnd().Substring(2));
                    y = CardsBlock(_page.defName + "." + i, items, y, w);
                    continue;
                }
                y = BodyLine(line, y, w);
            }

            if (_page.tips.Count > 0)
            {
                y = NetrunUI.Section(new Rect(0f, y + 6f, w, 32f), "NR_HelpTips".Translate());
                foreach (string tip in _page.tips)
                {
                    string t = HelpPageDef.Tokens(tip);
                    float h = Mathf.Max(26f, FlowHeight(t, w - 40f) + 8f);
                    var row = new Rect(0f, y, w, h);
                    Widgets.DrawBoxSolid(row, NetrunUI.BgCard);
                    Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), NetrunUI.Ok);
                    Widgets.Label(new Rect(row.x + 12f, row.y + 4f, 20f, 22f), "✔".Colorize(NetrunUI.Ok));
                    FlowText(t, row.x + 34f, row.y + 4f, w - 40f);
                    y += h + 4f;
                }
            }
        }

        var related = _page.related.Concat(_page.IsTopic ? _page.Children : Enumerable.Empty<HelpPageDef>()).Distinct().ToList();
        if (related.Count > 0)
        {
            y = NetrunUI.Section(new Rect(0f, y + 6f, w, 32f), (_page.IsTopic ? "NR_HelpInThisTopic" : "NR_HelpSeeAlso").Translate());
            foreach (HelpPageDef link in related)
                y = LinkButton(link, null, y, w);
        }
        _articleHeight = y + 8f;
        Widgets.EndScrollView();
    }

    /// <summary>"[[name:Preset]]" → "Preset" (null without one).</summary>
    private static string Preset(string line)
    {
        int colon = line.IndexOf(':');
        return colon < 0 ? null : line.Substring(colon + 1).TrimEnd(']');
    }

    private float BodyLine(string line, float y, float w)
    {
        if (line.Length == 0)
            return y + 6f;
        if (line.StartsWith("## "))
        {
            string heading = line.Substring(3);
            Texture2D icon = HelpIcons.TakeLeading(ref heading);
            return Heading(heading, icon, y + 8f, w);
        }
        if (line == "[[levels]]")
            return LevelTiles(y, w);
        if (line == "[[dive]]")
            return DiveSchema(y, w);
        if (line == "[[scrub]]")
            return ScrubDemo(y, w);
        if (line == "[[netsick]]")
            return NetsickDemo(y, w);
        if (line == "[[gateway]]")
            return GatewayDemo(y, w);
        if (line == "[[hackchance]]")
            return HackChanceDemo(y, w);
        if (line == "[[trace]]")
            return TraceDemo(y, w);
        if (line.StartsWith("[[aigallery"))
            return AIGalleryDemo(y, w, Preset(line));
        if (line == "[[fixerpool]]")
            return FixerPoolDemo(y, w);
        if (line == "[[contractflow]]")
            return ContractFlowDemo(y, w);
        if (line.StartsWith("[[braindance"))
            return BraindanceDemo(y, w, Preset(line));
        if (line == "[[ice]]")
            return IceDemo(y, w);
        if (line == "[[aihomes]]")
            return AIHomesDemo(y, w);
        if (line == "[[aihunger]]")
            return AIHungerDemo(y, w);
        if (line == "[[aitraits]]")
            return AITraitsDemo(y, w);
        if (line == "[[aiawaken]]")
            return AIAwakenDemo(y, w);
        if (line == "[[fixertraits]]")
            return FixerTraitsDemo(y, w);
        if (line == "[[market]]")
            return MarketDemo(y, w);
        if (line == "[[checklist]]")
            return ChecklistDemo(y, w);
        if (line == "[[terminalmap]]")
            return TerminalMapDemo(y, w);
        if (line == "[[hackheat]]")
            return HackHeatDemo(y, w);
        if (line == "[[newcomer]]")
            return NewcomerDemo(y, w);
        if (line == "[[lowerheat]]")
            return LowerHeatDemo(y, w);
        if (line == "[[loadout]]")
            return LoadoutDemo(y, w);
        if (line == "[[diagnostics]]")
        {
            if (NetrunUI.Button(new Rect(0f, y, Mathf.Min(w, 360f), 34f), "NR_DiagnosticsButton".Translate(), NetrunUI.Accent, true, "NR_DiagnosticsTip".Translate()))
                NetrunDiagnostics.CopyToClipboard();
            return y + 44f;
        }
        if (line.StartsWith("> page:"))
        {
            (string target, string text) = SplitLink(line.Substring(7));
            HelpPageDef page = DefDatabase<HelpPageDef>.GetNamedSilentFail(target);
            return page == null ? y : LinkButton(page, text, y, w);
        }
        if (line.StartsWith("> open:"))
        {
            (string screen, string text) = SplitLink(line.Substring(7));
            if (OpenScreen == null)
                return y;
            if (NetrunUI.Button(new Rect(0f, y, w, 30f), (text ?? screen) + " ↗", NetrunUI.Ok, true))
                OpenScreen(screen);
            return y + 36f;
        }
        bool bullet = line.StartsWith("- ");
        string textLine = bullet ? line.Substring(2) : line;
        Texture2D lineIcon = HelpIcons.TakeLeading(ref textLine);
        float iconSize = NetrunUI.Icon(40f);
        float indent = (bullet ? 16f : 0f) + (lineIcon != null ? iconSize + 10f : 0f);
        if (bullet && lineIcon == null)
            Widgets.Label(new Rect(0f, y, 14f, 22f), "•".Colorize(NetrunUI.Accent));
        if (lineIcon != null)
            GUI.DrawTexture(new Rect(bullet ? 2f : 0f, y, iconSize, iconSize), lineIcon);
        float pad = lineIcon != null ? Mathf.Max(0f, (iconSize - NetrunText.LineHeight) / 2f) : 0f;
        float h = FlowText(textLine, indent, y + pad, w - indent);
        h = Mathf.Max(lineIcon != null ? iconSize + 2f : 0f, h + pad);
        return y + h + (bullet ? 5f : 8f);
    }

    // ---- Hyperlinks (HyperText): each page is linked once per article, at its first mention ----

    private readonly Dictionary<string, List<HyperText.Segment>> _segments = [];
    private readonly HashSet<HelpPageDef> _used = [];
    private HelpPageDef _segmentsFor;

    private List<HyperText.Segment> Segments(string line)
    {
        if (_segmentsFor != _page)
        {
            _segments.Clear();
            _used.Clear();
            _used.Add(_page);
            _segmentsFor = _page;
        }
        if (!_segments.TryGetValue(line, out List<HyperText.Segment> segs))
            _segments[line] = segs = HyperText.Split(line, _used);
        return segs;
    }

    private float FlowHeight(string line, float width) => HyperText.Flow(Segments(line), 0f, 0f, width, false, Go);

    private float FlowText(string line, float x, float y, float width) => HyperText.Flow(Segments(line), x, y, width, true, Go);

    private static (string target, string text) SplitLink(string s)
    {
        int bar = s.IndexOf('|');
        return bar < 0 ? (s.Trim(), null) : (s.Substring(0, bar).Trim(), s.Substring(bar + 1).Trim());
    }

    private float LinkButton(HelpPageDef page, string text, float y, float w)
    {
        bool locked = HelpProgress.Get()?.Unlocked(page) == false;
        string label = text ?? (locked && page.lockedTitle ? "???" : (string)page.LabelCap);
        if (NetrunUI.Button(new Rect(0f, y, w, 30f), label, NetrunUI.Ok, true, null, locked ? HelpIcons.Lock : HelpIcons.For(page)))
            Go(page);
        return y + 36f;
    }

    /// <summary>A section heading: a dark band with the topic's colour bar, the icon and the title in capitals.</summary>
    private float Heading(string heading, Texture2D icon, float y, float w)
    {
        Color c = _page.Topic.Color;
        var band = new Rect(0f, y, w, 36f);
        Widgets.DrawBoxSolid(band, NetrunUI.BgCard);
        Widgets.DrawBoxSolid(new Rect(band.x, band.y, 4f, band.height), c);
        GUI.color = new Color(c.r, c.g, c.b, 0.35f);
        Widgets.DrawLineHorizontal(band.x, band.yMax - 1f, band.width);
        GUI.color = Color.white;
        float x = 14f;
        if (icon != null)
        {
            float s = Mathf.Min(34f, NetrunUI.Icon(28f));
            GUI.DrawTexture(new Rect(x, band.center.y - s / 2f, s, s), icon);
            x += s + 10f;
        }
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(x, band.y, w - x - 8f, band.height), heading.ToUpper().Colorize(c));
        Text.Anchor = TextAnchor.UpperLeft;
        return band.yMax + 8f;
    }

    // ---- [[dive]]: the steps of a node, a runner moving along them; hover explains, a click pins the text ----

    private static readonly string[] DiveSteps = ["LOBBY", "GATE", "FILES", "HIDDEN", "BLACKICE", "MEMORY", "EXIT"];
    private int _diveStep = -1;

    private float DiveSchema(float y, float w)
    {
        const float tile = 58f;
        int n = DiveSteps.Length;
        int perRow = Mathf.Clamp(Mathf.FloorToInt((w + 24f) / (tile + 30f)), 3, n);
        int rows = Mathf.CeilToInt(n / (float)perRow);
        float gap = perRow > 1 ? (w - perRow * tile) / (perRow - 1) : 0f;
        float rowH = tile + 34f;
        var centers = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            int row = i / perRow;
            int col = i % perRow;
            if (row % 2 == 1)
                col = perRow - 1 - col; // a snake, so the path never jumps back across
            centers[i] = new Vector2(col * (tile + gap) + tile / 2f, y + row * rowH + tile / 2f + 4f);
        }

        // Links between steps, then a packet running the whole path.
        for (int i = 0; i + 1 < n; i++)
            Widgets.DrawLine(centers[i], centers[i + 1], new Color(NetrunUI.Line.r, NetrunUI.Line.g, NetrunUI.Line.b, 0.8f), 2f);
        float t = NetrunUI.AnimTime / 1.1f % (n - 1);
        int seg = Mathf.Min(n - 2, Mathf.FloorToInt(t));
        int active = NetrunMod.Settings.Animations ? Mathf.RoundToInt(t) : -1;

        for (int i = 0; i < n; i++)
        {
            var r = new Rect(centers[i].x - tile / 2f, centers[i].y - tile / 2f, tile, tile);
            bool hover = Mouse.IsOver(r);
            bool on = hover || _diveStep == i || active == i;
            Color c = DiveSteps[i] == "BLACKICE" ? NetrunUI.Bad : DiveSteps[i] == "EXIT" ? NetrunUI.Ok : NetrunUI.Accent;
            if (on)
                NetrunUI.Glow(r, c, hover || _diveStep == i ? 0.9f : 0.5f, 10f);
            Widgets.DrawBoxSolid(r, NetrunUI.BgWindow);
            NetrunUI.Brackets(r, new Color(c.r, c.g, c.b, on ? 1f : 0.55f), 2f, 12f);
            if (NetrunTex.CyberIcon("Dive_" + DiveSteps[i]) is { } icon)
                GUI.DrawTexture(r.ContractedBy(9f), icon);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(r.x - 20f, r.yMax + 3f, r.width + 40f, 18f), ((string)("NR_HelpDiveStep_" + DiveSteps[i]).Translate()).ToUpper().Colorize(on ? c : NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            NetrunUI.Tip(r, ("NR_HelpDiveStepDesc_" + DiveSteps[i]).Translate());
            if (Widgets.ButtonInvisible(r))
                _diveStep = _diveStep == i ? -1 : i;
        }
        if (NetrunMod.Settings.Animations)
        {
            Vector2 p = Vector2.Lerp(centers[seg], centers[seg + 1], t - seg);
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(p.x - 4f, p.y - 4f, 8f, 8f), NetrunUI.Disc);
            GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.5f);
            GUI.DrawTexture(new Rect(p.x - 10f, p.y - 10f, 20f, 20f), NetrunUI.GlowTex);
            GUI.color = Color.white;
        }
        y += rows * rowH + 4f;

        // The pinned (or the hint) text under the schema.
        string text = _diveStep >= 0 ? ("NR_HelpDiveStepDesc_" + DiveSteps[_diveStep]).Translate() : "NR_HelpDiveSchemaHint".Translate();
        float th = Text.CalcHeight(text, w - 24f) + 14f;
        var box = new Rect(0f, y, w, th);
        Widgets.DrawBoxSolid(box, NetrunUI.BgCard);
        Widgets.DrawBoxSolid(new Rect(box.x, box.y, 3f, box.height), _diveStep >= 0 ? NetrunUI.Accent : NetrunUI.Muted);
        Widgets.Label(new Rect(box.x + 12f, box.y + 7f, w - 24f, th), _diveStep >= 0 ? text : text.Colorize(NetrunUI.Muted));
        return box.yMax + 10f;
    }

    // ---- [[scrub]]: a calculator. Pick a Heat and the cyberdata to spend; see where the Heat lands. ----

    private float _demoHeat = 80f;
    private int _demoData = 10;

    private float ScrubDemo(float y, float w)
    {
        FixerSettingsDef s = NetrunDefOf.NR_Fixers;
        var box = new Rect(0f, y, w, 132f);
        Widgets.DrawBoxSolid(box, NetrunUI.BgCard);
        Widgets.DrawBoxSolid(new Rect(box.x, box.y, 3f, box.height), NetrunUI.Accent);
        Rect inner = box.ContractedBy(10f);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(inner.x, inner.y, inner.width * 0.5f, 18f), "NR_HelpDemoHeat".Translate(_demoHeat.ToString("0")).Colorize(NetrunUI.Muted));
        _demoHeat = NetrunUI.Slider(new Rect(inner.x, inner.y + 18f, inner.width * 0.48f, 16f), _demoHeat, 0f, HeatTracker.MaxHeat, 1f);
        Widgets.Label(new Rect(inner.x + inner.width * 0.52f, inner.y, inner.width * 0.48f, 18f), "NR_HelpDemoData".Translate(_demoData).Colorize(NetrunUI.Muted));
        _demoData = Mathf.RoundToInt(NetrunUI.Slider(new Rect(inner.x + inner.width * 0.52f, inner.y + 18f, inner.width * 0.48f, 16f), _demoData, 0f, 60f, 1f));

        // Unit by unit, at the level the Heat stands at (a fixer with no discount).
        float heat = _demoHeat;
        int needed = -1;
        for (int i = 0; i < 200 && heat > 0.01f; i++)
        {
            if (i == _demoData)
                break;
            float factor = s.scrubFactorByLevel.NullOrEmpty() ? 1f : s.scrubFactorByLevel[Mathf.Clamp((int)HeatLevelUtility.LevelOf(heat), 0, s.scrubFactorByLevel.Count - 1)];
            heat = Mathf.Max(0f, heat - s.heatPerCyberdata * factor * NetrunMod.Settings.ScrubMultiplier);
        }
        float h2 = _demoHeat;
        for (int i = 0; i < 400 && h2 > 0.01f; i++)
        {
            float factor = s.scrubFactorByLevel.NullOrEmpty() ? 1f : s.scrubFactorByLevel[Mathf.Clamp((int)HeatLevelUtility.LevelOf(h2), 0, s.scrubFactorByLevel.Count - 1)];
            h2 = Mathf.Max(0f, h2 - s.heatPerCyberdata * factor * NetrunMod.Settings.ScrubMultiplier);
            needed = i + 1;
        }

        var bar = new Rect(inner.x, inner.y + 46f, inner.width, 16f);
        NetrunUI.Bar(bar, _demoHeat / HeatTracker.MaxHeat, NetrunUI.Bad);
        Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * heat / HeatTracker.MaxHeat, bar.height), HeatLevelUtility.LevelOf(heat).Color());
        Widgets.Label(new Rect(inner.x, inner.y + 68f, inner.width, 18f),
            "NR_HelpDemoResult".Translate(_demoHeat.ToString("0"), heat.ToString("0"), HeatLevelUtility.LevelOf(heat).Label()).Colorize(NetrunUI.TextColor));
        Widgets.Label(new Rect(inner.x, inner.y + 88f, inner.width, 18f),
            "NR_HelpDemoNeeded".Translate(Mathf.Max(0, needed)).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        return box.yMax + 10f;
    }

    // ---- [[netsick]]: the stages on a bar; hover a stage to read it, a marker shows how it creeps up. ----

    private float NetsickDemo(float y, float w)
    {
        (float from, float to, string key, Color color)[] stages =
        [
            (0f, 0.3f, "NR_HelpDemoSick_Mild", NetrunUI.Ok),
            (0.3f, 0.6f, "NR_HelpDemoSick_Noticeable", NetrunUI.Warn),
            (0.6f, 0.85f, "NR_HelpDemoSick_Strong", new Color(1f, 0.55f, 0.2f)),
            (0.85f, 1f, "NR_HelpDemoSick_Severe", NetrunUI.Bad),
        ];
        var bar = new Rect(0f, y + 4f, w, 26f);
        string hovered = null;
        foreach ((float from, float to, string key, Color color) in stages)
        {
            var seg = new Rect(bar.x + bar.width * from + 1f, bar.y, bar.width * (to - from) - 2f, bar.height);
            bool hover = Mouse.IsOver(seg);
            Widgets.DrawBoxSolid(seg, new Color(color.r, color.g, color.b, hover ? 0.55f : 0.28f));
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(seg, $"{from:P0}".Colorize(Color.white));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (hover)
                hovered = key;
        }
        if (NetrunMod.Settings.Animations)
        {
            float f = Time.realtimeSinceStartup / 8f % 1f;
            float mx = bar.x + bar.width * f;
            Widgets.DrawBoxSolid(new Rect(mx - 1f, bar.y - 4f, 2f, bar.height + 8f), Color.white);
        }
        string text = (hovered ?? "NR_HelpDemoSickHint").Translate();
        float th = Text.CalcHeight(text, w - 16f) + 10f;
        var box = new Rect(0f, bar.yMax + 6f, w, th);
        Widgets.DrawBoxSolid(box, NetrunUI.BgCard);
        Widgets.Label(new Rect(box.x + 8f, box.y + 5f, w - 16f, th), hovered == null ? text.Colorize(NetrunUI.Muted) : text);
        return box.yMax + 10f;
    }

    /// <summary>Five tiles: each Heat level with its icon, range and one line.</summary>
    private static float LevelTiles(float y, float w)
    {
        var levels = (HeatLevel[])System.Enum.GetValues(typeof(HeatLevel));
        float tw = (w - (levels.Length - 1) * 6f) / levels.Length;
        float maxH = 0f;
        Text.Font = GameFont.Tiny;
        foreach (HeatLevel l in levels)
            maxH = Mathf.Max(maxH, Text.CalcHeight(l.Description(), tw - 12f));
        float h = 52f + maxH + 8f;
        for (int i = 0; i < levels.Length; i++)
        {
            HeatLevel l = levels[i];
            var tile = new Rect(i * (tw + 6f), y, tw, h);
            Widgets.DrawBoxSolid(tile, NetrunUI.BgCard);
            Widgets.DrawBoxSolid(new Rect(tile.x, tile.yMax - 2f, tile.width, 2f), l.Color());
            GUI.DrawTexture(new Rect(tile.x + 6f, tile.y + 6f, 24f, 24f), NetrunTex.HeatIcon(l));
            string range = l == HeatLevel.RedZone ? $"{l.MinHeat():0}+" : $"{l.MinHeat():0}–{(l + 1).MinHeat() - 1f:0}";
            Widgets.Label(new Rect(tile.x + 34f, tile.y + 6f, tile.width - 38f, 32f), $"{l.Label().ToUpper()}\n{range}".Colorize(l.Color()));
            Widgets.Label(new Rect(tile.x + 6f, tile.y + 42f, tile.width - 10f, maxH + 4f), l.Description().Colorize(NetrunUI.Muted));
        }
        Text.Font = GameFont.Small;
        return y + h + 8f;
    }

    public void Go(HelpPageDef page)
    {
        if (page == null || page == _page)
            return;
        NetrunSound.Click();
        if (_page != null)
            _back.Add(_page);
        _forward.Clear();
        _page = page;
        _openStamp++;
        _articleScroll = Vector2.zero;
        _search = string.Empty;
        _diveStep = -1;
    }

    private void Step(List<HelpPageDef> from, List<HelpPageDef> to)
    {
        if (from.Count == 0)
            return;
        to.Add(_page);
        _page = from[from.Count - 1];
        from.RemoveAt(from.Count - 1);
        _articleScroll = Vector2.zero;
    }
}

/// <summary>Icons for Cyberpedia pages and markup: "!Name" at the start of a line or heading, or a page's icon field.</summary>
[StaticConstructorOnStartup]
public static class HelpIcons
{
    // The cache must exist before anything resolves an icon (static fields initialise in order).
    private static readonly Dictionary<string, Texture2D> Cache = [];

    public static Texture2D Lock => Resolve("Icon_Lock");

    public static Texture2D For(HelpPageDef page) => page.icon.NullOrEmpty() ? null : Resolve(page.icon);

    /// <summary>
    /// "Name" (Textures/UI/Netrun/Cyberspace/Name), "Folder/Name" (Textures/UI/Netrun/Folder/Name),
    /// "thing:DefName" (a ThingDef's icon), "heat:Level" (the Heat level icon), "ai:Personality" (portrait),
    /// "tex:Path" (any texture path, e.g. Hacking Expansion's "UI/Gizmos/HackTarget").
    /// </summary>
    public static Texture2D Resolve(string id)
    {
        if (id.NullOrEmpty())
            return null;
        if (Cache.TryGetValue(id, out Texture2D tex))
            return tex;
        if (id.StartsWith("thing:"))
            tex = DefDatabase<ThingDef>.GetNamedSilentFail(id.Substring(6))?.uiIcon;
        else if (id.StartsWith("tex:"))
            tex = ContentFinder<Texture2D>.Get(id.Substring(4), reportFailure: false);
        else if (id.StartsWith("heat:") && System.Enum.TryParse(id.Substring(5), out HeatLevel level))
            tex = NetrunTex.HeatIcon(level);
        else if (id.StartsWith("ai:") && System.Enum.TryParse(id.Substring(3), out AIPersonality personality))
            tex = NetrunTex.Portrait(personality);
        else
            tex = ContentFinder<Texture2D>.Get(id.Contains("/") ? "UI/Netrun/" + id : "UI/Netrun/Cyberspace/" + id, reportFailure: false)
                  ?? ContentFinder<Texture2D>.Get("UI/Netrun/" + id, reportFailure: false);
        Cache[id] = tex;
        return tex;
    }

    /// <summary>Cuts a leading "!id " from the text and returns that icon.</summary>
    public static Texture2D TakeLeading(ref string text)
    {
        if (!text.StartsWith("!"))
            return null;
        int space = text.IndexOf(' ');
        if (space < 2)
            return null;
        Texture2D tex = Resolve(text.Substring(1, space - 1));
        text = text.Substring(space + 1);
        return tex;
    }
}
