using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>A fixer's shop in the terminal style: item cards with icon, price and a buy button.</summary>
public class Dialog_FixerShop : Window
{
    private const float RowHeight = 48f;
    private const float RowGap = 6f;

    private readonly Fixer _fixer;
    private readonly Map _map;
    private Vector2 _scroll;

    public override Vector2 InitialSize => new(600f, 500f);
    protected override float Margin => 0f;

    public Dialog_FixerShop(Fixer fixer, Map map)
    {
        _fixer = fixer;
        _map = map;

        doCloseX = false;
        forcePause = false;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = true;
        draggable = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Widgets.DrawBoxSolid(inRect, NetrunUI.BgWindow);
        Rect r = inRect.ContractedBy(14f);
        int silver = ColonyGoods.Count(_map, ThingDefOf.Silver);

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(r.x, r.y, r.width - 200f, 32f), "NR_ShopTitle".Translate(_fixer.Name).Colorize(NetrunUI.Accent));
        Text.Font = GameFont.Small;
        NetrunUI.HeaderChip(r.xMax, r.y + 4f, "NR_ChipSilver".Translate(silver));
        float y = r.y + 36f;
        Widgets.Label(new Rect(r.x, y, r.width, 22f),
            "NR_ShopRestock".Translate(FixerManager.Get()?.TicksUntilRotation.ToStringTicksToPeriod() ?? "?").Colorize(NetrunUI.Muted));
        y += 30f;

        var listRect = new Rect(r.x, y, r.width, r.yMax - y - 44f);
        if (_fixer.Stock.Count == 0)
        {
            Widgets.Label(listRect, "NR_ShopEmpty".Translate().Colorize(NetrunUI.Muted));
        }
        else
        {
            var view = new Rect(0f, 0f, listRect.width - 16f, _fixer.Stock.Count * (RowHeight + RowGap));
            Widgets.BeginScrollView(listRect, ref _scroll, view);
            float ry = 0f;
            foreach (ThingDef def in _fixer.Stock.ToArray())
            {
                if (DrawRow(new Rect(0f, ry, view.width, RowHeight), def, silver))
                    break;
                ry += RowHeight + RowGap;
            }
            Widgets.EndScrollView();
        }

        string close = "CloseButton".Translate();
        float cw = NetrunUI.ButtonWidth(close);
        if (NetrunUI.Button(new Rect(r.xMax - cw, r.yMax - 34f, cw, 34f), close, NetrunUI.Muted, true))
            Close();
    }

    /// <summary>One item; true when it was bought (the stock changed, stop drawing this frame).</summary>
    private bool DrawRow(Rect row, ThingDef def, int silver)
    {
        NetrunUI.Card(row, NetrunUI.Ok);
        Widgets.ThingIcon(new Rect(row.x + 12f, row.y + 8f, 32f, 32f), def);
        Widgets.InfoCardButton(row.x + 50f, row.y + 13f, def);

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(row.x + 80f, row.y, row.width - 300f, row.height), def.LabelCap);
        int price = _fixer.PriceOf(def);
        Widgets.Label(new Rect(row.xMax - 210f, row.y, 100f, row.height),
            "NR_ShopPrice".Translate(price).Colorize(price > silver ? NetrunUI.Bad : NetrunUI.Warn));
        Text.Anchor = TextAnchor.UpperLeft;

        AcceptanceReport can = price <= silver ? true : (AcceptanceReport)"NR_ShopNoSilver".Translate();
        if (NetrunUI.Button(new Rect(row.xMax - 100f, row.y + 7f, 90f, 34f), "NR_ShopBuy".Translate(), NetrunUI.Ok, can, "NR_FixerShopTip".Translate()))
        {
            FixerDeals.Buy(_fixer, _map, def);
            return true;
        }
        return false;
    }
}
