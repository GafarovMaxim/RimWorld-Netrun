using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

public class Dialog_FixerShop : Window
{
    private const float RowHeight = 40f;

    private readonly Fixer _fixer;
    private readonly Map _map;

    public override Vector2 InitialSize => new(560f, 420f);

    public Dialog_FixerShop(Fixer fixer, Map map)
    {
        _fixer = fixer;
        _map = map;

        doCloseX = true;
        forcePause = true;
        absorbInputAroundWindow = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f), "NR_ShopTitle".Translate(_fixer.Name));
        Text.Font = GameFont.Small;

        int silver = ColonyGoods.Count(_map, ThingDefOf.Silver);
        Widgets.Label(new Rect(inRect.x, inRect.y + 36f, inRect.width, 24f), "NR_ShopSilver".Translate(silver));

        float y = inRect.y + 70f;
        if (_fixer.Stock.Count == 0)
        {
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 24f), "NR_ShopEmpty".Translate());
            return;
        }

        foreach (ThingDef def in _fixer.Stock.ToArray())
        {
            var row = new Rect(inRect.x, y, inRect.width, RowHeight);
            Widgets.DrawHighlightIfMouseover(row);

            Widgets.ThingIcon(new Rect(row.x, row.y + 4f, 32f, 32f), def);
            Widgets.InfoCardButton(row.x + 38f, row.y + 9f, def);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x + 66f, row.y, row.width - 260f, row.height), def.LabelCap);

            int price = _fixer.PriceOf(def);
            Widgets.Label(new Rect(row.xMax - 190f, row.y, 90f, row.height), "NR_ShopPrice".Translate(price));
            Text.Anchor = TextAnchor.UpperLeft;

            var buyRect = new Rect(row.xMax - 95f, row.y + 6f, 90f, 28f);
            if (price > silver)
            {
                GUI.color = Color.gray;
                Widgets.ButtonText(buyRect, "NR_ShopBuy".Translate(), active: false);
                GUI.color = Color.white;
            }
            else if (Widgets.ButtonText(buyRect, "NR_ShopBuy".Translate()))
            {
                FixerDeals.Buy(_fixer, _map, def);
                return;
            }

            y += RowHeight;
        }
    }
}
