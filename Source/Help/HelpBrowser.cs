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
public class HelpBrowser
{
    private const float TreeWidth = 260f;

    private HelpPageDef _page;
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
        Widgets.BeginScrollView(outer, ref _treeScroll, view);
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
                GUI.DrawTexture(new Rect(indent + 8f, row.y + (h - iconSize) / 2f, iconSize, iconSize), icon);
                GUI.color = Color.white;
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            Color color = locked ? NetrunUI.Muted : current || depth == 0 ? NetrunUI.TextColor : NetrunUI.Muted;
            Widgets.Label(new Rect(textX, row.y, textW, h), label.Colorize(color));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // "New" and "you are here" as small dots on the right, no text over the label.
            if (progress?.IsNew(page) == true)
                Dot(new Vector2(row.xMax - 9f, row.center.y), NetrunUI.Ok, "NR_HelpNew".Translate());
            else if (current)
                Dot(new Vector2(row.xMax - 9f, row.center.y), page.Topic.Color, "NR_HelpHere".Translate());

            if (locked)
                TooltipHandler.TipRegion(row, page.lockHint.NullOrEmpty() ? (string)"NR_HelpLocked".Translate() : page.lockHint);
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
        TooltipHandler.TipRegion(r.ExpandedBy(4f), tip);
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
        Widgets.BeginScrollView(outer, ref _articleScroll, view);
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
        Widgets.Label(new Rect(tx, y, w - tx, th), title.Colorize(_page.Topic.Color));
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
            foreach (string raw in HelpPageDef.Tokens(_page.body ?? string.Empty).Split('\n'))
                y = BodyLine(raw.TrimEnd(), y, w);

            if (_page.tips.Count > 0)
            {
                y = NetrunUI.Section(new Rect(0f, y + 6f, w, 32f), "NR_HelpTips".Translate());
                foreach (string tip in _page.tips)
                {
                    string t = HelpPageDef.Tokens(tip);
                    float h = Mathf.Max(26f, Text.CalcHeight(t, w - 40f) + 8f);
                    var row = new Rect(0f, y, w, h);
                    Widgets.DrawBoxSolid(row, NetrunUI.BgCard);
                    Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), NetrunUI.Ok);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(row.x + 12f, row.y, 20f, h), "✔".Colorize(NetrunUI.Ok));
                    Widgets.Label(new Rect(row.x + 34f, row.y, w - 40f, h), t);
                    Text.Anchor = TextAnchor.UpperLeft;
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

    private float BodyLine(string line, float y, float w)
    {
        if (line.Length == 0)
            return y + 6f;
        if (line.StartsWith("## "))
        {
            string heading = line.Substring(3);
            Texture2D icon = HelpIcons.TakeLeading(ref heading);
            if (icon == null)
                return NetrunUI.Section(new Rect(0f, y + 4f, w, 32f), heading);
            GUI.DrawTexture(new Rect(0f, y + 6f, 24f, 24f), icon);
            return NetrunUI.Section(new Rect(30f, y + 4f, w - 30f, 32f), heading);
        }
        if (line == "[[levels]]")
            return LevelTiles(y, w);
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
        float indent = (bullet ? 16f : 0f) + (lineIcon != null ? 30f : 0f);
        float h = Mathf.Max(lineIcon != null ? 26f : 0f, Text.CalcHeight(textLine, w - indent));
        if (bullet && lineIcon == null)
            Widgets.Label(new Rect(0f, y, 14f, 22f), "•".Colorize(NetrunUI.Accent));
        if (lineIcon != null)
            GUI.DrawTexture(new Rect(bullet ? 4f : 0f, y, 24f, 24f), lineIcon);
        Widgets.Label(new Rect(indent, y + (lineIcon != null && h <= 26f ? 2f : 0f), w - indent, h), textLine);
        return y + h + (bullet ? 4f : 6f);
    }

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
        SoundDefOf.Click.PlayOneShotOnCamera();
        if (_page != null)
            _back.Add(_page);
        _forward.Clear();
        _page = page;
        _articleScroll = Vector2.zero;
        _search = string.Empty;
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
    /// "thing:DefName" (a ThingDef's icon), "heat:Level" (the Heat level icon), "ai:Personality" (portrait).
    /// </summary>
    public static Texture2D Resolve(string id)
    {
        if (id.NullOrEmpty())
            return null;
        if (Cache.TryGetValue(id, out Texture2D tex))
            return tex;
        if (id.StartsWith("thing:"))
            tex = DefDatabase<ThingDef>.GetNamedSilentFail(id.Substring(6))?.uiIcon;
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
