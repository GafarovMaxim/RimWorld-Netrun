using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// "[[cards]]" in an article: the bullet list right after it ("- !icon Title: text") becomes a row of tiles, icon and
/// title, and the picked one opens in a card with its full text (links work). Lists of events, node types, finds and
/// so on read like the rest of the Cyberpedia without a demo of their own.
/// </summary>
public partial class HelpBrowser
{
    private readonly Dictionary<string, int> _cardPick = [];

    private float CardsBlock(string id, List<string> items, float y, float w)
    {
        if (items.Count == 0)
            return y;
        // Split every line into an icon, a title (before the first ": ") and the text.
        var cards = new List<(Texture2D icon, string title, string text)>();
        foreach (string raw in items)
        {
            string s = raw;
            Texture2D icon = HelpIcons.TakeLeading(ref s);
            int colon = s.IndexOf(": ", System.StringComparison.Ordinal);
            cards.Add(colon > 0 && colon < 60 ? (icon, s.Substring(0, colon), s.Substring(colon + 2)) : (icon, s.Truncate(40f * 7f), s));
        }
        int pick = Mathf.Clamp(_cardPick.TryGetValue(id, out int p) ? p : 0, 0, cards.Count - 1);
        return Frame("cards." + id, y, w, NetrunUI.Accent, inner =>
        {
            // Rows as even as possible: never one lonely tile under a full row.
            int maxPerRow = Mathf.Clamp(Mathf.FloorToInt(inner.width / 112f), 2, 6);
            int rowsNeeded = Mathf.CeilToInt(cards.Count / (float)maxPerRow);
            int perRow = Mathf.CeilToInt(cards.Count / (float)rowsNeeded);
            const float gap = 6f;
            float tileW = (inner.width - (perRow - 1) * gap) / perRow;
            bool anyIcon = cards.Exists(c => c.icon != null);
            // Titles in the normal font if they fit in two lines, else the small one.
            Text.Font = GameFont.Small;
            bool tiny = cards.Exists(c => Text.CalcHeight(c.title, tileW - 12f) > Text.LineHeight * 2.2f);
            Text.Font = tiny ? GameFont.Tiny : GameFont.Small;
            float textH = 0f;
            foreach (var c in cards)
                textH = Mathf.Max(textH, Text.CalcHeight(c.title, tileW - 12f));
            float iconS = anyIcon ? 30f : 0f;
            float tileH = 10f + iconS + (anyIcon ? 4f : 0f) + textH + 8f;
            for (int i = 0; i < cards.Count; i++)
            {
                var r = new Rect(inner.x + i % perRow * (tileW + gap), inner.y + i / perRow * (tileH + gap), tileW, tileH);
                bool sel = i == pick;
                bool over = Mouse.IsOver(r);
                NetrunUI.Box(r, sel ? NetrunUI.BgCardAlt : NetrunUI.BgWindow, sel ? NetrunUI.Accent : new Color(1f, 1f, 1f, over ? 0.3f : 0.12f), sel ? 2 : 1);
                if (cards[i].icon != null)
                    NetrunUI.BrightIcon(new Rect(r.center.x - iconS / 2f, r.y + 8f, iconS, iconS), NetrunTint.OfAny(cards[i].icon, _page?.Topic.Color ?? NetrunUI.Accent));
                Text.Font = tiny ? GameFont.Tiny : GameFont.Small;
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(new Rect(r.x + 6f, r.y + 10f + iconS + (anyIcon ? 4f : 0f), r.width - 12f, textH + 4f), cards[i].title.Colorize(sel ? NetrunUI.Accent : NetrunUI.TextColor));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                if (Widgets.ButtonInvisible(r, doMouseoverSound: false))
                {
                    _cardPick[id] = i;
                    NetrunSound.Click();
                }
            }
            int rows = Mathf.CeilToInt(cards.Count / (float)perRow);
            var picked = cards[pick];
            return Panel("cards." + id + ".card", inner, inner.y + rows * (tileH + gap) + 2f, NetrunUI.Accent, cr =>
            {
                float x = cr.x;
                if (picked.icon != null)
                {
                    GUI.DrawTexture(new Rect(cr.x, cr.y, 32f, 32f), NetrunTint.OfAny(picked.icon, _page?.Topic.Color ?? NetrunUI.Accent));
                    x += 40f;
                }
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(x, cr.y, cr.xMax - x, 34f), picked.title.Colorize(NetrunUI.Accent));
                Text.Font = GameFont.Small;
                float h = HyperText.Height(picked.text, cr.width, id);
                HyperText.Label(new Rect(cr.x, cr.y + 38f, cr.width, h), picked.text, NetrunUI.TextColor, id);
                return cr.y + 38f + h + 2f;
            });
        });
    }
}
