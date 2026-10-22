using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>The first-visit training offer in the terminal's own look (Maxim, 2026-10-23: the vanilla box broke the style).</summary>
public class Dialog_TrainingOffer : Window
{
    private readonly GatewayManager _gateway;
    private readonly Map _map;

    public Dialog_TrainingOffer(GatewayManager gateway, Map map)
    {
        _gateway = gateway;
        _map = map;
        doCloseX = false;
        forcePause = true;
        absorbInputAroundWindow = true;
        doWindowBackground = false;
        drawShadow = false;
    }

    public override Vector2 InitialSize => new(540f, 250f);

    protected override float Margin => 0f;

    public override void DoWindowContents(Rect inRect)
    {
        Widgets.DrawBoxSolid(inRect, NetrunUI.BgWindow);
        GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.6f);
        Widgets.DrawBox(inRect);
        GUI.color = Color.white;
        Widgets.DrawBoxSolid(new Rect(inRect.x, inRect.y, 3f, inRect.height), NetrunUI.Accent);
        Rect body = inRect.ContractedBy(20f);
        Text.Font = GameFont.Medium;
        GUI.color = NetrunUI.Accent;
        Widgets.Label(new Rect(body.x, body.y, body.width, 32f), "NR_TrainingOfferTitle".Translate());
        GUI.color = NetrunUI.TextColor;
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(body.x, body.y + 40f, body.width, body.height - 90f), "NR_TrainingOffer".Translate());
        GUI.color = Color.white;
        float bw = (body.width - 10f) / 2f;
        var buttons = new Rect(body.x, body.yMax - 34f, bw, 34f);
        if (NetrunUI.Button(buttons, "NR_TrainingLater".Translate(), NetrunUI.Muted, true))
            Close();
        if (NetrunUI.Button(new Rect(buttons.xMax + 10f, buttons.y, bw, 34f), "NR_TrainingStart".Translate(), NetrunUI.Accent, true))
        {
            Close();
            _gateway.StartTraining(_map);
        }
    }
}
