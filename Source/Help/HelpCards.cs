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
            int perRow = Mathf.Clamp(Mathf.FloorToInt(inner.width / 150f), 2, 4);
            const float gap = 8f;
            float tileW = (inner.width - (perRow - 1) * gap) / perRow;
            Text.Font = GameFont.Small;
            float tileH = 0f;
            foreach (var c in cards)
                tileH = Mathf.Max(tileH, Text.CalcHeight(c.title, tileW - 16f));
            tileH = Mathf.Max(84f, tileH + 58f);
            for (int i = 0; i < cards.Count; i++)
            {
                var r = new Rect(inner.x + i % perRow * (tileW + gap), inner.y + i / perRow * (tileH + gap), tileW, tileH);
                bool sel = i == pick;
                bool over = Mouse.IsOver(r);
                if (sel)
                    NetrunUI.Glow(r, NetrunUI.Accent, 0.6f, 6f);
                NetrunUI.Box(r, sel ? NetrunUI.BgCardAlt : NetrunUI.BgWindow, sel ? NetrunUI.Accent : new Color(1f, 1f, 1f, over ? 0.3f : 0.12f), sel ? 2 : 1);
                if (cards[i].icon != null)
                    NetrunUI.BrightIcon(new Rect(r.center.x - 20f, r.y + 8f, 40f, 40f), cards[i].icon);
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(new Rect(r.x + 8f, r.y + 52f, r.width - 16f, r.height - 54f), cards[i].title.Colorize(sel ? NetrunUI.Accent : NetrunUI.TextColor));
                Text.Anchor = TextAnchor.UpperLeft;
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
                    GUI.DrawTexture(new Rect(cr.x, cr.y, 32f, 32f), picked.icon);
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
