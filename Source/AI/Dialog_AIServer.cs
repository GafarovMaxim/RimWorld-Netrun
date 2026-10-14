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

    public override Vector2 InitialSize => new(Mathf.Min(1080f, UI.screenWidth - 40f), Mathf.Min(840f, UI.screenHeight - 40f));
    protected override float Margin => 0f;

    public Dialog_AIServer(Map map)
    {
        _map = map;
        doCloseX = false;
        // Like the game's own windows: paused once on opening (if set), but time, the camera and a click outside still work.
        forcePause = false;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = true;
        preventCameraMotion = false;
        // Moved only by the casing, like the terminal.
        draggable = false;
        resizeable = true;
    }

    public override void PreOpen()
    {
        base.PreOpen();
        if (NetrunMod.Settings.PauseOnOpen && Find.TickManager is { Paused: false } ticks)
            ticks.TogglePaused();
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
            NetrunSound.Ambient("Ambient_Cyberspace");
            Rect screen = NetrunUI.DeckBezel(inRect, "NR_DeckPlate".Translate(), (HeatTracker.Get()?.Heat ?? 0f) / HeatTracker.MaxHeat, NetworkGate.IsOpen,
                key => { if (key == NetrunUI.KeyClose) Close(); }, menu: false);
            NetrunUI.DragByCasing(inRect, screen);
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
            // Wake a fragment right here: a colonist carries it to this server.
            string awaken = "NR_CommandAwakenAI".Translate();
            float aw = NetrunUI.ButtonWidth(awaken, icon: true);
            if (NetrunUI.Button(new Rect(title.xMax - cw - aw - 20f, title.y + 7f, aw, 30f), awaken, NetrunUI.Accent, AIAwakening.CanAwaken(_map),
                    "NR_CommandAwakenAIDesc".Translate(), NetrunTex.AIAwaken))
                AIAwakening.OpenMenu(_map);
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            Widgets.DrawLineHorizontal(title.x, title.yMax - 1f, title.width);
            GUI.color = Color.white;
            AITab.Draw(new Rect(screen.x, title.yMax, screen.width, screen.height - title.height).ContractedBy(12f), _map, ref _scroll);
            NetrunUI.Scanlines(screen);
            // NetrunUI.DeckGlass(screen); // Glass Screen is off for now (Maxim, 2026-10-09: works badly)
        }
        finally
        {
            NetrunText.Pop(size);
            NetrunFont.Pop(font);
        }
    }
}
