using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The first session (docs/CONCEPT_MAIN_MENU.md, section 3.1): the first time the terminal opens, three short steps
/// point at the Core, the Cyberpedia button and the Guide. Closed with the cross; the menu starts it again.
/// </summary>
public partial class Dialog_NetTerminal
{
    private int _introStep = -1;
    private Rect _helpRect, _guideRectSeen;

    private void StartIntro()
    {
        _introStep = 0;
        _view = View.Map;
        _guide = false;
    }

    /// <summary>Starts the tour on the very first open (once per game, if hints are on).</summary>
    private void MaybeStartIntro()
    {
        if (_introStep >= 0 || !NetrunMod.Settings.FirstHints || HelpProgress.Get() is not { } progress || progress.HintSeen("intro"))
            return;
        progress.MarkHint("intro");
        StartIntro();
    }

    private void DrawIntro(Rect screen)
    {
        if (_introStep < 0)
            return;
        Rect target = _introStep switch
        {
            0 => new Rect(screen.x, screen.y + TitleHeight, screen.width, screen.height - TitleHeight - StatusHeight).ContractedBy(12f),
            1 => _helpRect,
            _ => _guideRectSeen,
        };
        // Dim everything but the target.
        var dim = new Color(0f, 0f, 0f, 0.55f);
        Widgets.DrawBoxSolid(new Rect(screen.x, screen.y, screen.width, target.y - screen.y), dim);
        Widgets.DrawBoxSolid(new Rect(screen.x, target.yMax, screen.width, screen.yMax - target.yMax), dim);
        Widgets.DrawBoxSolid(new Rect(screen.x, target.y, target.x - screen.x, target.height), dim);
        Widgets.DrawBoxSolid(new Rect(target.xMax, target.y, screen.xMax - target.xMax, target.height), dim);
        NetrunUI.Brackets(target.ExpandedBy(4f), NetrunUI.Warn, 2f, 12f);

        var card = new Rect(screen.center.x - 220f, screen.center.y - 90f, 440f, 180f);
        if (_introStep > 0)
            card.y = Mathf.Min(target.yMax + 24f, screen.yMax - card.height - 20f);
        Widgets.DrawBoxSolid(card, NetrunUI.BgWindow);
        NetrunUI.Brackets(card, NetrunUI.Accent, 2f, 14f);
        Rect r = card.ContractedBy(14f);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(r.x, r.y, r.width, 16f), ((string)"NR_IntroStep".Translate(_introStep + 1, 3)).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(r.x, r.y + 16f, r.width, 30f), ((string)("NR_IntroTitle_" + _introStep).Translate()).Colorize(NetrunUI.Accent));
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(r.x, r.y + 48f, r.width, r.height - 86f), ("NR_Intro_" + _introStep).Translate());
        float bw = (r.width - 8f) / 2f;
        bool last = _introStep >= 2;
        if (NetrunUI.Button(new Rect(r.x, r.yMax - 30f, bw, 30f), (last ? "NR_IntroDone" : "NR_IntroNext").Translate(), NetrunUI.Ok, true))
            _introStep = last ? -1 : _introStep + 1;
        if (!last && NetrunUI.Button(new Rect(r.x + bw + 8f, r.yMax - 30f, bw, 30f), "NR_IntroSkip".Translate(), NetrunUI.Muted, true))
            _introStep = -1;
    }
}
