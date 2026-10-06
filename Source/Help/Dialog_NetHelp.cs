using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Netrun;

/// <summary>
/// The mod's encyclopedia (docs/HELP.md, mockup 11-help): topics on the left, the article on the right,
/// search at the top, back/forward, opened on the page of the screen it was called from.
/// </summary>
public class Dialog_NetHelp : Window
{
    private const float TitleHeight = 44f;
    private const float StatusHeight = 32f;
    private const float TreeWidth = 230f;

    private readonly string _context;
    private readonly System.Action<string> _openScreen;
    private HelpPageDef _page;
    private readonly List<HelpPageDef> _back = [];
    private readonly List<HelpPageDef> _forward = [];
    private string _search = string.Empty;
    private Vector2 _treeScroll;
    private Vector2 _articleScroll;
    private float _articleHeight = 600f;

    public override Vector2 InitialSize => new(Mathf.Min(960f, UI.screenWidth - 40f), Mathf.Min(720f, UI.screenHeight - 40f));
    protected override float Margin => 0f;

    /// <param name="context">The terminal screen it is opened from (Core, Heat, Club, ...).</param>
    /// <param name="openScreen">Opens a terminal screen by name ("> open:" links); null hides those links.</param>
    public Dialog_NetHelp(string context, System.Action<string> openScreen = null, HelpPageDef page = null)
    {
        _context = context;
        _openScreen = openScreen;
        _page = page ?? HelpPageDef.ForContext(context);
        doCloseX = false;
        draggable = true;
        resizeable = true;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = false;
        forcePause = false;
    }

    /// <summary>Open the help on the page for this screen (or bring it to that page if open).</summary>
    public static void Open(string context, System.Action<string> openScreen = null)
    {
        HelpProgress progress = HelpProgress.Get();
        if (progress != null)
        {
            progress.HelpOpened = true;
            progress.Refresh(Find.CurrentMap);
        }
        Find.WindowStack.TryRemove(typeof(Dialog_NetHelp), doCloseSound: false);
        Find.WindowStack.Add(new Dialog_NetHelp(context, openScreen));
    }

    public override void DoWindowContents(Rect inRect)
    {
        Widgets.DrawBoxSolid(inRect, NetrunUI.BgWindow);
        DrawTitle(new Rect(inRect.x, inRect.y, inRect.width, TitleHeight));
        var body = new Rect(inRect.x, inRect.y + TitleHeight, inRect.width, inRect.height - TitleHeight - StatusHeight);
        DrawTree(new Rect(body.x, body.y, TreeWidth, body.height));
        GUI.color = new Color(1f, 1f, 1f, 0.12f);
        Widgets.DrawLineVertical(body.x + TreeWidth, body.y, body.height);
        GUI.color = Color.white;
        DrawArticle(new Rect(body.x + TreeWidth + 1f, body.y, body.width - TreeWidth - 1f, body.height));
        DrawStatus(new Rect(inRect.x, body.yMax, inRect.width, StatusHeight));
    }

    // ---- Title: name, search, close ----

    private void DrawTitle(Rect rect)
    {
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(rect.x + 14f, rect.y, rect.width * 0.5f, rect.height), "NR_HelpTitle".Translate().Colorize(NetrunUI.Accent));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        var close = new Rect(rect.xMax - 40f, rect.y + 8f, 28f, 28f);
        if (NetrunUI.Button(close, "✕", NetrunUI.Accent, true, "NR_HelpClose".Translate()))
            Close();
        var search = new Rect(close.x - 230f, rect.y + 9f, 220f, 26f);
        Widgets.DrawBoxSolid(search, NetrunUI.BgCard);
        string before = _search;
        GUI.SetNextControlName("NR_HelpSearch");
        _search = Widgets.TextField(search.ContractedBy(2f), _search);
        if (_search.NullOrEmpty() && GUI.GetNameOfFocusedControl() != "NR_HelpSearch")
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(search.x + 8f, search.y, search.width - 10f, search.height), ("⌕ " + "NR_HelpSearch".Translate()).Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
        }
        if (before != _search)
            _treeScroll = Vector2.zero;

        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width);
        GUI.color = Color.white;
    }

    // ---- Left: topics (or search results) ----

    private void DrawTree(Rect rect)
    {
        Rect r = rect.ContractedBy(10f);
        float y = NetrunUI.Section(new Rect(r.x, r.y, r.width, 32f), (_search.NullOrEmpty() ? "NR_HelpTopics" : "NR_HelpResults").Translate());
        var outer = new Rect(r.x, y, r.width, r.yMax - y);
        List<(HelpPageDef page, int depth)> rows = Rows();
        var view = new Rect(0f, 0f, outer.width - 16f, rows.Count * 30f);
        Widgets.BeginScrollView(outer, ref _treeScroll, view);
        float ry = 0f;
        HelpProgress progress = HelpProgress.Get();
        if (rows.Count == 0)
            Widgets.Label(new Rect(0f, 0f, view.width, 40f), "NR_HelpNoResults".Translate().Colorize(NetrunUI.Muted));
        foreach ((HelpPageDef page, int depth) in rows)
        {
            bool locked = progress != null && !progress.Unlocked(page);
            bool current = page == _page;
            var row = new Rect(depth * 12f, ry, view.width - depth * 12f, depth == 0 ? 28f : 24f);
            if (depth == 0)
            {
                Widgets.DrawBoxSolid(row, current || _page?.Topic == page ? NetrunUI.BgCardAlt : NetrunUI.BgCard);
                Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), locked ? NetrunUI.Muted : page.Color);
            }
            else if (current)
            {
                Widgets.DrawBoxSolid(row, NetrunUI.BgCard);
            }
            Text.Font = depth == 0 ? GameFont.Small : GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            string mark = locked ? "🔒 " : depth == 0 ? (_page?.Topic == page ? "▼ " : "► ") : "• ";
            string label = mark + page.LabelCap;
            Widgets.Label(new Rect(row.x + 10f, row.y, row.width - 70f, row.height), locked || (!current && depth > 0) ? label.Colorize(NetrunUI.Muted) : label);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            if (page == HelpPageDef.ForContext(_context) && depth == 0 && page == _page?.Topic)
                Widgets.Label(new Rect(row.xMax - 70f, row.y, 64f, row.height), "NR_HelpHere".Translate().Colorize(NetrunUI.Accent));
            else if (progress?.IsNew(page) == true)
                Widgets.Label(new Rect(row.xMax - 70f, row.y, 64f, row.height), ("● " + "NR_HelpNew".Translate()).Colorize(NetrunUI.Ok));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (locked)
                TooltipHandler.TipRegion(row, "NR_HelpLocked".Translate());
            else if (Widgets.ButtonInvisible(row))
                Go(page);
            ry += row.height + 2f;
        }
        Widgets.EndScrollView();
    }

    /// <summary>Tree rows: every topic, and the children of the open topic; or the search results.</summary>
    private List<(HelpPageDef, int)> Rows()
    {
        var rows = new List<(HelpPageDef, int)>();
        if (!_search.NullOrEmpty())
        {
            string q = _search.Trim().ToLowerInvariant();
            foreach (HelpPageDef page in DefDatabase<HelpPageDef>.AllDefs.OrderBy(p => p.Topic.order).ThenBy(p => p.IsTopic ? 0 : 1).ThenBy(p => p.order))
            {
                if (page.label.ToLowerInvariant().Contains(q) || (page.body ?? string.Empty).ToLowerInvariant().Contains(q))
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
        HelpProgress.Get()?.MarkSeen(_page);
        Rect outer = rect.ContractedBy(16f);
        outer.yMax -= 26f;
        bool scrolls = _articleHeight > outer.height;
        var view = new Rect(0f, 0f, outer.width - (scrolls ? 16f : 0f), Mathf.Max(_articleHeight, outer.height));
        Widgets.BeginScrollView(outer, ref _articleScroll, view);
        float y = 0f;
        float w = view.width;

        // Breadcrumbs and title.
        Text.Font = GameFont.Tiny;
        string crumbs = ((string)"NR_HelpCrumbRoot".Translate()).ToUpper() + (_page.parent != null ? " › " + _page.parent.label.ToUpper() : string.Empty);
        Widgets.Label(new Rect(0f, y, w, 18f), crumbs.Colorize(NetrunUI.Muted));
        y += 20f;
        Text.Font = GameFont.Medium;
        float th = Text.CalcHeight(_page.LabelCap, w);
        Widgets.Label(new Rect(0f, y, w, th), _page.LabelCap.ToString().Colorize(_page.Topic.Color));
        Text.Font = GameFont.Small;
        y += th + 8f;

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

        var related = _page.related.Concat(_page.IsTopic ? _page.Children : Enumerable.Empty<HelpPageDef>()).Distinct().ToList();
        if (related.Count > 0)
        {
            y = NetrunUI.Section(new Rect(0f, y + 6f, w, 32f), (_page.IsTopic ? "NR_HelpInThisTopic" : "NR_HelpSeeAlso").Translate());
            foreach (HelpPageDef link in related)
                y = LinkButton(link, link.LabelCap, y, w);
        }
        _articleHeight = y + 8f;
        Widgets.EndScrollView();

        // Back / forward.
        var nav = new Rect(rect.xMax - 220f, rect.yMax - 34f, 204f, 24f);
        if (NetrunUI.Button(new Rect(nav.x, nav.y, 98f, nav.height), "NR_HelpBack".Translate(), NetrunUI.Muted, _back.Count > 0 ? true : (AcceptanceReport)false))
            Step(_back, _forward);
        if (NetrunUI.Button(new Rect(nav.x + 106f, nav.y, 98f, nav.height), "NR_HelpForward".Translate(), NetrunUI.Muted, _forward.Count > 0 ? true : (AcceptanceReport)false))
            Step(_forward, _back);
    }

    private float BodyLine(string line, float y, float w)
    {
        if (line.Length == 0)
            return y + 6f;
        if (line.StartsWith("## "))
            return NetrunUI.Section(new Rect(0f, y + 4f, w, 32f), line.Substring(3));
        if (line == "[[levels]]")
            return LevelTiles(y, w);
        if (line.StartsWith("> page:"))
        {
            (string target, string text) = SplitLink(line.Substring(7));
            HelpPageDef page = DefDatabase<HelpPageDef>.GetNamedSilentFail(target);
            return page == null ? y : LinkButton(page, text ?? page.LabelCap, y, w);
        }
        if (line.StartsWith("> open:"))
        {
            (string screen, string text) = SplitLink(line.Substring(7));
            if (_openScreen == null)
                return y;
            if (NetrunUI.Button(new Rect(0f, y, w, 30f), (text ?? screen) + " ↗", NetrunUI.Ok, true))
            {
                _openScreen(screen);
                Close();
            }
            return y + 36f;
        }
        bool bullet = line.StartsWith("- ");
        string textLine = bullet ? line.Substring(2) : line;
        float indent = bullet ? 16f : 0f;
        float h = Text.CalcHeight(textLine, w - indent);
        if (bullet)
            Widgets.Label(new Rect(0f, y, 14f, 22f), "•".Colorize(NetrunUI.Accent));
        Widgets.Label(new Rect(indent, y, w - indent, h), textLine);
        return y + h + (bullet ? 2f : 6f);
    }

    private static (string target, string text) SplitLink(string s)
    {
        int bar = s.IndexOf('|');
        return bar < 0 ? (s.Trim(), null) : (s.Substring(0, bar).Trim(), s.Substring(bar + 1).Trim());
    }

    private float LinkButton(HelpPageDef page, string text, float y, float w)
    {
        bool locked = HelpProgress.Get()?.Unlocked(page) == false;
        if (NetrunUI.Button(new Rect(0f, y, w, 30f), (locked ? "🔒 " : string.Empty) + text, NetrunUI.Ok, locked ? (AcceptanceReport)"NR_HelpLocked".Translate() : true))
            Go(page);
        return y + 36f;
    }

    /// <summary>Five tiles: each Heat level with its range and one line.</summary>
    private static float LevelTiles(float y, float w)
    {
        var levels = (HeatLevel[])System.Enum.GetValues(typeof(HeatLevel));
        float tw = (w - (levels.Length - 1) * 6f) / levels.Length;
        float maxH = 0f;
        Text.Font = GameFont.Tiny;
        foreach (HeatLevel l in levels)
            maxH = Mathf.Max(maxH, Text.CalcHeight(l.Description(), tw - 12f));
        float h = 24f + maxH + 8f;
        for (int i = 0; i < levels.Length; i++)
        {
            HeatLevel l = levels[i];
            var tile = new Rect(i * (tw + 6f), y, tw, h);
            Widgets.DrawBoxSolid(tile, NetrunUI.BgCard);
            Widgets.DrawBoxSolid(new Rect(tile.x, tile.yMax - 2f, tile.width, 2f), l.Color());
            string range = l == HeatLevel.RedZone ? $"{l.MinHeat():0}+" : $"{l.MinHeat():0}–{(l + 1).MinHeat() - 1f:0}";
            Widgets.Label(new Rect(tile.x + 6f, tile.y + 4f, tile.width - 10f, 18f), $"{l.Label().ToUpper()} {range}".Colorize(l.Color()));
            Widgets.Label(new Rect(tile.x + 6f, tile.y + 22f, tile.width - 10f, maxH + 4f), l.Description().Colorize(NetrunUI.Muted));
        }
        Text.Font = GameFont.Small;
        return y + h + 8f;
    }

    private void Go(HelpPageDef page)
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

    // ---- Bottom ----

    private void DrawStatus(Rect rect)
    {
        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width);
        GUI.color = Color.white;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        string from = ("NR_HelpCtx_" + _context).Translate();
        Widgets.Label(new Rect(rect.x + 14f, rect.y, rect.width - 160f, rect.height), "NR_HelpOpenedFrom".Translate(from).Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(new Rect(rect.xMax - 160f, rect.y, 146f, rect.height), "NR_HelpEsc".Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }
}
