using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// A right-click menu in the terminal's style (dark box, accent frame, glyph per row) instead of the game's float menu.
/// Opens at the mouse, closes on a pick, a click outside, or Esc. A row with a reason is shown greyed with the reason.
/// </summary>
public class TerminalMenu : Window
{
    public struct Item
    {
        public string Label;
        public string Glyph;
        public System.Action Action;
        public string Disabled;
    }

    private readonly List<Item> _items;
    private readonly string _title;
    private const float RowH = 26f, Pad = 6f, HeadH = 22f;

    public TerminalMenu(string title, List<Item> items)
    {
        _title = title;
        _items = items;
        layer = WindowLayer.Super;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = false;
        doWindowBackground = false;
        drawShadow = false;
        preventCameraMotion = false;
        doCloseX = false;
        soundAppear = null;
        soundClose = null;
    }

    public static void Open(string title, List<Item> items)
    {
        if (items.Count > 0)
            Find.WindowStack.Add(new TerminalMenu(title, items));
    }

    private float Width()
    {
        Text.Font = GameFont.Small;
        float w = Text.CalcSize(_title ?? string.Empty).x;
        foreach (Item it in _items)
            w = Mathf.Max(w, Text.CalcSize(it.Label).x + (it.Disabled != null ? 0f : 0f));
        return Mathf.Clamp(w + 46f, 180f, 420f);
    }

    public override Vector2 InitialSize => new(Width(), Pad * 2f + (_title.NullOrEmpty() ? 0f : HeadH) + _items.Count * RowH);

    protected override void SetInitialSizeAndPosition()
    {
        Vector2 size = InitialSize;
        Vector2 mouse = UI.MousePositionOnUIInverted;
        float x = Mathf.Min(mouse.x + 4f, UI.screenWidth - size.x - 4f);
        float y = Mathf.Min(mouse.y + 4f, UI.screenHeight - size.y - 4f);
        windowRect = new Rect(x, y, size.x, size.y).Rounded();
    }

    protected override float Margin => 0f;

    public override void DoWindowContents(Rect inRect)
    {
        bool font = NetrunFont.Push();
        try
        {
            Widgets.DrawBoxSolid(inRect, new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.97f));
            GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.6f);
            Widgets.DrawBox(inRect);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(inRect.x, inRect.y, 3f, inRect.height), NetrunUI.Accent);
            float y = inRect.y + Pad;
            if (!_title.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                GUI.color = NetrunUI.Accent;
                Widgets.Label(new Rect(inRect.x + 12f, y + 2f, inRect.width - 16f, HeadH), "// " + NetrunUI.Upper(_title));
                GUI.color = Color.white;
                y += HeadH;
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            foreach (Item it in _items)
            {
                var row = new Rect(inRect.x + 4f, y, inRect.width - 8f, RowH);
                bool on = it.Disabled == null;
                if (on && Mouse.IsOver(row))
                    Widgets.DrawBoxSolid(row, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.18f));
                GUI.color = on ? NetrunUI.Accent : NetrunUI.Muted;
                Widgets.Label(new Rect(row.x + 6f, row.y, 20f, row.height), it.Glyph ?? "▸");
                GUI.color = on ? NetrunUI.TextColor : NetrunUI.Muted;
                Widgets.Label(new Rect(row.x + 26f, row.y, row.width - 28f, row.height), it.Label);
                GUI.color = Color.white;
                if (!on && Mouse.IsOver(row))
                    NetrunUI.Tip(row, it.Disabled);
                if (Widgets.ButtonInvisible(row))
                {
                    if (on)
                    {
                        NetrunSound.Click();
                        Close();
                        it.Action?.Invoke();
                    }
                    else
                        NetrunSound.Play("UI_Deny");
                }
                y += RowH;
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }
        finally
        {
            NetrunFont.Pop(font);
        }
    }
}
