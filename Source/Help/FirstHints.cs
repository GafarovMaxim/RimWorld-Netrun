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

/// <summary>A small card in the corner: the hint, "Read" and "Got it". It does not pause the game.</summary>
public class Dialog_FirstHint : Window
{
    private readonly string _key;
    private readonly string _page;

    public Dialog_FirstHint(string key, string page)
    {
        _key = key;
        _page = page;
        doCloseX = true;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = false;
        preventCameraMotion = false;
        forcePause = false;
        layer = WindowLayer.GameUI;
        soundAppear = null;
    }

    public override Vector2 InitialSize => new(440f, 200f);

    protected override void SetInitialSizeAndPosition()
    {
        Vector2 size = InitialSize;
        windowRect = new Rect(UI.screenWidth - size.x - 20f, UI.screenHeight - size.y - 120f, size.x, size.y).Rounded();
    }

    public override void DoWindowContents(Rect inRect)
    {
        bool font = NetrunFont.Push();
        try
        {
            Widgets.DrawBoxSolid(inRect, NetrunUI.BgWindow);
            NetrunUI.Brackets(inRect, NetrunUI.Accent, 2f, 14f);
            Rect r = inRect.ContractedBy(12f);
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
            if (NetrunUI.Button(new Rect(r.x + bw + 8f, r.yMax - 32f, bw, 32f), "NR_HintGotIt".Translate(), NetrunUI.Muted, true))
                Close();
        }
        finally
        {
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
