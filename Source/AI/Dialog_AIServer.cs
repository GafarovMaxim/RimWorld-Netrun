using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The server room on its own, opened from the AI server building: the same AI cards as in the terminal, inside the
/// cyberdeck casing, with no navigation to the rest of the network.
/// </summary>
public class Dialog_AIServer : Window
{
    private readonly Map _map;
    private Vector2 _scroll;

    public override Vector2 InitialSize => new(Mathf.Min(980f, UI.screenWidth - 40f), Mathf.Min(720f, UI.screenHeight - 40f));
    protected override float Margin => 0f;

    public Dialog_AIServer(Map map)
    {
        _map = map;
        doCloseX = false;
        forcePause = NetrunMod.Settings.PauseOnOpen;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = false;
        draggable = true;
        resizeable = true;
    }

    public override void PostOpen()
    {
        base.PostOpen();
        NetrunSound.Play("UI_Open");
    }

    public override void PostClose()
    {
        base.PostClose();
        NetrunSound.Play("UI_Close");
        NetrunSound.EndAmbient();
    }

    public override void DoWindowContents(Rect inRect)
    {
        bool font = NetrunFont.Push();
        bool size = NetrunText.Push();
        try
        {
            NetrunSound.Ambient("Ambient_Terminal");
            Rect screen = NetrunUI.Bezel(inRect, 18f, "NR_DeckPlate".Translate());
            Widgets.DrawBoxSolid(screen, NetrunUI.BgWindow);
            var title = new Rect(screen.x, screen.y, screen.width, 44f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(title.x + 14f, title.y, title.width - 120f, title.height), "NR_AIServerWindowTitle".Translate().Colorize(NetrunUI.Purple));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            string close = "CloseButton".Translate();
            float cw = NetrunUI.ButtonWidth(close);
            if (NetrunUI.Button(new Rect(title.xMax - cw - 12f, title.y + 7f, cw, 30f), close, NetrunUI.Muted, true))
                Close();
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            Widgets.DrawLineHorizontal(title.x, title.yMax - 1f, title.width);
            GUI.color = Color.white;
            AITab.Draw(new Rect(screen.x, title.yMax, screen.width, screen.height - title.height).ContractedBy(12f), _map, ref _scroll);
            NetrunUI.Scanlines(screen);
        }
        finally
        {
            NetrunText.Pop(size);
            NetrunFont.Pop(font);
        }
    }
}
