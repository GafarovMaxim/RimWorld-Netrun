using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Short clickable notices in the terminal's style (a runner reached its hub): a click opens the cyberspace map on that
/// hub. Vanilla messages can only jump the camera, so these are drawn here, over the lower right of the screen, and fade
/// after a few seconds. Not saved.
/// </summary>
public class NetrunToasts : GameComponent
{
    private struct Toast
    {
        public string Text;
        public int Hub;
        public float At;
    }

    private const float Life = 8f, W = 330f, H = 34f;
    private static readonly List<Toast> Live = [];

    public NetrunToasts(Game game)
    {
    }

    /// <summary>A notice that opens the Gateway on <paramref name="hub"/> when clicked.</summary>
    public static void ShowHub(string text, int hub)
    {
        if (Live.Count >= 4)
            Live.RemoveAt(0);
        Live.Add(new Toast { Text = text, Hub = hub, At = Time.realtimeSinceStartup });

    }

    public override void GameComponentOnGUI()
    {
        if (Live.Count == 0)
            return;
        float now = Time.realtimeSinceStartup;
        Live.RemoveAll(t => now - t.At > Life);
        float y = UI.screenHeight - 240f;
        for (int i = Live.Count - 1; i >= 0; i--)
        {
            Toast t = Live[i];
            float age = now - t.At;
            float alpha = Mathf.Clamp01((Life - age) / 1.2f) * Mathf.Clamp01(age / 0.15f);
            var r = new Rect(UI.screenWidth - W - 230f, y, W, H);
            y -= H + 6f;
            Color old = GUI.color;
            Widgets.DrawBoxSolid(r, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.92f * alpha));
            Widgets.DrawBoxSolid(new Rect(r.x, r.y, 3f, r.height), new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, alpha));
            GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, (Mouse.IsOver(r) ? 0.9f : 0.45f) * alpha);
            Widgets.DrawBox(r);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(r.x + 10f, r.y, r.width - 20f, r.height), t.Text + "  ↗");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = old;
            if (Widgets.ButtonInvisible(r))
            {
                Live.RemoveAt(i);
                Map map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
                if (map == null)
                    return;
                NetTerminalUtility.OpenTerminal(map, gateway: true);
                Find.WindowStack.WindowOfType<Dialog_NetTerminal>()?.OpenGatewayAt(t.Hub);
                return;
            }
        }
    }
}
