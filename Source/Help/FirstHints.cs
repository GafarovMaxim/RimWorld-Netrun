using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// First-time hints (docs/CONCEPT_MAIN_MENU.md, section 3): the first ICE hit, Netsick, fixer call and AI event each
/// bring one short card with "Read" (the Cyberpedia page) and "Got it". Seen hints are kept in the save
/// (HelpProgress); the whole thing can be turned off in the settings.
/// </summary>
public static class FirstHints
{
    public static void Show(string key, string page)
    {
        try
        {
            if (!NetrunMod.Settings.FirstHints || Current.Game == null || HelpProgress.Get() is not { } progress || progress.HintSeen(key))
                return;
            progress.MarkHint(key);
            Find.WindowStack.Add(new Dialog_FirstHint(key, page));
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] first-time hint failed: " + e.Message, 0x4E520052);
        }
    }
}

/// <summary>
/// A small card at the top right (Maxim, 2026-10-14): above every other window, without the game's window frame and
/// close cross, sliding in with a short pulse of its brackets so it is noticed. "Read" and "Got it"; no pause.
/// </summary>
public class Dialog_FirstHint : Window
{
    private readonly string _key;
    private readonly string _page;
    private float _openedAt = -1f;

    private const float AppearSeconds = 0.35f;
    private const float PulseSeconds = 2.4f;

    public Dialog_FirstHint(string key, string page)
    {
        _key = key;
        _page = page;
        doCloseX = false;
        doWindowBackground = false;
        drawShadow = false;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = false;
        preventCameraMotion = false;
        forcePause = false;
        // Over the terminal and any other dialog.
        layer = WindowLayer.Super;
        soundAppear = null;
    }

    public override Vector2 InitialSize => new(440f, 200f);
    protected override float Margin => 0f;

    protected override void SetInitialSizeAndPosition()
    {
        Vector2 size = InitialSize;
        // Top right, below the game's own readouts there.
        windowRect = new Rect(UI.screenWidth - size.x - 20f, 140f, size.x, size.y).Rounded();
    }

    public override void DoWindowContents(Rect inRect)
    {
        bool font = NetrunFont.Push();
        Color oldColor = GUI.color;
        try
        {
            float now = Time.realtimeSinceStartup;
            if (_openedAt < 0f)
                _openedAt = now;
            float age = now - _openedAt;
            bool anim = NetrunMod.Settings.Animations;
            // Slides in from the right and fades in, then the brackets pulse for a moment.
            float appear = anim ? Mathf.Clamp01(age / AppearSeconds) : 1f;
            float ease = 1f - (1f - appear) * (1f - appear);
            Rect card = inRect;
            card.x += (1f - ease) * 60f;
            GUI.color = new Color(1f, 1f, 1f, ease);
            Widgets.DrawBoxSolid(card, NetrunUI.BgWindow);
            float pulse = anim && age < PulseSeconds ? 0.5f + 0.5f * Mathf.Sin(age * 9f) : 0f;
            NetrunUI.Brackets(card, Color.Lerp(NetrunUI.Accent, Color.white, pulse * 0.6f), 2f + pulse * 1.5f, 14f + pulse * 4f);
            Rect r = card.ContractedBy(12f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(r.x, r.y, r.width, 16f), ((string)"NR_HintCaption".Translate()).Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(r.x, r.y + 16f, r.width - 20f, 30f), ((string)("NR_HintTitle_" + _key).Translate()).Colorize(NetrunUI.Accent));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(r.x, r.y + 48f, r.width, r.height - 90f), ("NR_Hint_" + _key).Translate());
            float bw = (r.width - 8f) / 2f;
            if (NetrunUI.Button(new Rect(r.x, r.yMax - 32f, bw, 32f), "NR_HintRead".Translate(), NetrunUI.Accent, true))
            {
                OpenPage();
                Close();
            }
            // A live colour: the muted one read as a disabled button.
            if (NetrunUI.Button(new Rect(r.x + bw + 8f, r.yMax - 32f, bw, 32f), "NR_HintGotIt".Translate(), NetrunUI.TextColor, true))
                Close();
        }
        finally
        {
            GUI.color = oldColor;
            NetrunFont.Pop(font);
        }
    }
    private void OpenPage()
    {
        HelpPageDef page = DefDatabase<HelpPageDef>.GetNamedSilentFail(_page);
        if (page == null)
            return;
        if (Find.WindowStack.WindowOfType<Dialog_NetTerminal>() == null && Find.CurrentMap is { } map)
            NetTerminalUtility.OpenTerminal(map);
        Dialog_NetTerminal.OpenPage(page);
    }
}
