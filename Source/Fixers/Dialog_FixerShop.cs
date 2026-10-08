using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// A fixer's shop in the terminal style: the fixer on a plate, silver and restock in the header, then item cards in two
/// columns (icon, name, price, buy). Black market goods carry their own tag; every item sells once.
/// </summary>
public class Dialog_FixerShop : Window
{
    private const float CardHeight = 76f;
    private const float Gap = 8f;

    private readonly Fixer _fixer;
    private readonly Map _map;
    private Vector2 _scroll;

    public override Vector2 InitialSize => new(Mathf.Min(720f, UI.screenWidth - 40f), Mathf.Min(560f, UI.screenHeight - 40f));
    protected override float Margin => 0f;

    public Dialog_FixerShop(Fixer fixer, Map map)
    {
        _fixer = fixer;
        _map = map;

        doCloseX = false;
        forcePause = false;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = true;
        preventCameraMotion = false;
        draggable = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        bool font = NetrunFont.Push();
        bool size = NetrunText.Push();
        try
        {
            Rect screen = NetrunUI.Bezel(inRect, 8f, null, NetrunUI.Ok);
            DrawContents(screen);
            NetrunUI.Scanlines(screen);
        }
        finally
        {
            NetrunText.Pop(size);
            NetrunFont.Pop(font);
        }
    }

    private void DrawContents(Rect inRect)
    {
        Widgets.DrawBoxSolid(inRect, NetrunUI.BgWindow);
        Rect r = inRect.ContractedBy(16f);
        int silver = ColonyGoods.Count(_map, ThingDefOf.Silver);

        // Header: shop icon, the fixer's plate, silver and when the stock changes.
        float x = r.x;
        if (NetrunTex.Shop is { } shopIcon)
        {
            GUI.DrawTexture(new Rect(x, r.y, 32f, 32f), shopIcon);
            x += 40f;
        }
        Text.Font = GameFont.Medium;
        float tw = Text.CalcSize("NR_ShopTitleShort".Translate()).x;
        Widgets.Label(new Rect(x, r.y + 1f, tw + 4f, 32f), "NR_ShopTitleShort".Translate().Colorize(NetrunUI.Ok));
        x += tw + 12f;
        NetrunUI.NamePlate(x, r.y + 1f, _fixer.Name, FixersTab.SpecialtyColor(_fixer.Specialty), GameFont.Small);
        NetrunUI.HeaderChip(r.xMax, r.y + 4f, "NR_ChipSilver".Translate(silver));
        float y = r.y + 40f;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(r.x, y, r.width, 18f),
            NetrunUI.Prompt("NR_ShopRestock".Translate(FixerManager.Get()?.TicksUntilRotation.ToStringTicksToPeriod() ?? "?")).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        y += 26f;

        var listRect = new Rect(r.x, y, r.width, r.yMax - y - 44f);
        List<ThingDef> own = _fixer.Stock.ToList();
        List<ThingDef> black = (MapEventsManager.Get()?.BlackMarketStock(_fixer) ?? Enumerable.Empty<ThingDef>()).ToList();
        int total = own.Count + black.Count;
        if (total == 0)
        {
            Widgets.Label(listRect, NetrunUI.Prompt("NR_ShopEmpty".Translate()).Colorize(NetrunUI.Muted));
        }
        else
        {
            float colW = (listRect.width - 16f - Gap) / 2f;
            int rows = Mathf.CeilToInt(total / 2f);
            var view = new Rect(0f, 0f, listRect.width - 16f, rows * (CardHeight + Gap));
            NetrunUI.BeginScroll(listRect, ref _scroll, view);
            int i = 0;
            bool bought = false;
            foreach ((ThingDef def, bool isBlack) in own.Select(d => (d, false)).Concat(black.Select(d => (d, true))))
            {
                var card = new Rect(i % 2 * (colW + Gap), i / 2 * (CardHeight + Gap), colW, CardHeight);
                if (!bought && DrawCard(card, def, silver, isBlack))
                    bought = true;
                i++;
            }
            Widgets.EndScrollView();
        }

        string close = "CloseButton".Translate();
        float cw = NetrunUI.ButtonWidth(close);
        if (NetrunUI.Button(new Rect(r.xMax - cw, r.yMax - 34f, cw, 34f), close, NetrunUI.Muted, true))
            Close();
    }

    /// <summary>One item card; true when it was bought (the stock changed, stop handling clicks this frame).</summary>
    private bool DrawCard(Rect card, ThingDef def, int silver, bool black)
    {
        Color c = black ? NetrunUI.Purple : NetrunUI.Ok;
        NetrunUI.Card(card, c);
        var icon = new Rect(card.x + 14f, card.y + 12f, 52f, 52f);
        NetrunUI.Box(icon, NetrunUI.BgWindow, new Color(c.r, c.g, c.b, 0.6f));
        Widgets.ThingIcon(icon.ContractedBy(6f), def);
        float tx = icon.xMax + 10f;
        float tw = card.xMax - tx - 10f;
        Widgets.Label(new Rect(tx, card.y + 8f, tw - 24f, 24f), def.LabelCap.ToString().Truncate(tw - 24f));
        Widgets.InfoCardButton(card.xMax - 30f, card.y + 8f, def);
        if (black)
            NetrunUI.Chip(tx, card.y + 32f, "NR_ShopBlackTag".Translate(), NetrunUI.Purple, "NR_ShopBlackTip".Translate());

        int price = _fixer.PriceOf(def);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(tx, card.yMax - 30f, 110f, 24f), "NR_ShopPrice".Translate(price).Colorize(price > silver ? NetrunUI.Bad : NetrunUI.Warn));
        Text.Anchor = TextAnchor.UpperLeft;
        AcceptanceReport can = price <= silver ? true : (AcceptanceReport)"NR_ShopNoSilver".Translate();
        if (NetrunUI.Button(new Rect(card.xMax - 96f, card.yMax - 34f, 86f, 28f), "NR_ShopBuy".Translate(), c, can, "NR_FixerShopTip".Translate()))
        {
            FixerDeals.Buy(_fixer, _map, def);
            return true;
        }
        return false;
    }
}
