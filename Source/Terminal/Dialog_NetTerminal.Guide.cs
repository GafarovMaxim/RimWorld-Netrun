using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Netrun;

/// <summary>
/// Guide mode (docs/HELP.md, mockup 12-guide-mode): the screen dims, its areas get numbered frames,
/// hovering lights one up, a click opens a small step-by-step guide next to it. Input is handled before the
/// screen draws, so the screen underneath never gets the clicks.
/// </summary>
public partial class Dialog_NetTerminal
{
    private bool _guide;
    private GuideAreaDef _guideArea;
    private int _guideStep;

    // Hit areas of the guide card from the last frame (handled by hand, before the screen draws).
    private Rect _guideCard;
    private Rect _guideBack, _guideNext, _guideHelp, _guideExit;

    private static readonly Texture2D GuideIcon = ContentFinder<Texture2D>.Get("UI/Netrun/Icon_Guide", reportFailure: false);

    /// <summary>The book button; lit and pulsing while the guide is on (pressing it leaves the guide).</summary>
    private bool GuideIconButton(Rect rect)
    {
        Color c = _guide ? NetrunUI.Warn : NetrunUI.Ok;
        if (_guide)
        {
            float p = NetrunMod.Settings.Animations ? 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 4f) : 1f;
            Widgets.DrawBoxSolid(rect.ExpandedBy(4f), new Color(c.r, c.g, c.b, 0.25f + 0.3f * p));
        }
        bool clicked = NetrunUI.Button(rect, GuideIcon == null ? "◎" : string.Empty, c, true, (_guide ? "NR_GuideButtonLeaveTip" : "NR_GuideButtonTip").Translate());
        if (GuideIcon != null)
        {
            GUI.color = c;
            GUI.DrawTexture(rect.ContractedBy(1f), GuideIcon);
            GUI.color = Color.white;
        }
        return clicked;
    }

    private void ToggleGuide()
    {
        _guide = !_guide;
        _guideArea = null;
        NetrunSound.Click();
        if (_guide)
            HelpProgress.Get()?.MarkGuideScreen(HelpContext);
    }

    /// <summary>Esc leaves the guide (or closes the card) instead of closing the terminal.</summary>
    public override void OnCancelKeyPressed()
    {
        if (_view == View.Help && !_guide)
        {
            LeaveHelp();
            Event.current.Use();
            return;
        }
        if (_guide)
        {
            if (_guideArea != null)
                _guideArea = null;
            else
                _guide = false;
            Event.current.Use();
            return;
        }
        base.OnCancelKeyPressed();
    }

    /// <summary>Clicks in guide mode, with last frame's areas; true when the event was used.</summary>
    private bool GuideInput()
    {
        if (!_guide)
            return false;
        Event e = Event.current;
        if (e.type != EventType.MouseDown && e.type != EventType.MouseUp && e.type != EventType.ScrollWheel)
            return false;
        // The window can still be dragged and resized by its edges; everything else belongs to the guide.
        Vector2 m = e.mousePosition;
        // The title bar stays live: drag the window, "?" and "Guide".
        if (m.y < TitleHeight)
            return false;
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (_guideArea != null && _guideCard.Contains(m))
            {
                if (_guideBack.Contains(m) && _guideStep > 0)
                    _guideStep--;
                else if (_guideNext.Contains(m))
                {
                    if (_guideStep < _guideArea.steps.Count - 1)
                        _guideStep++;
                    else
                        _guideArea = null;
                }
                else if (_guideHelp.Contains(m) && _guideArea.helpPage != null)
                    OpenHelp(_guideArea.helpPage);
                else if (_guideExit.Contains(m))
                    _guide = false;
                NetrunSound.Click();
            }
            else
            {
                GuideAreaDef hit = AreasNow().Where(a => a.rect.Contains(m)).OrderBy(a => a.rect.width * a.rect.height).Select(a => a.def).FirstOrDefault();
                if (hit != null)
                {
                    _guideArea = hit;
                    _guideStep = 0;
                    HelpProgress.Get()?.MarkGuideArea(hit);
                    NetrunSound.Click();
                }
                else
                {
                    _guideArea = null;
                }
            }
        }
        e.Use();
        return true;
    }

    /// <summary>Registered rects that have a guide def, in the screen's order.</summary>
    private List<(GuideAreaDef def, Rect rect)> AreasNow()
    {
        var list = new List<(GuideAreaDef, Rect)>();
        foreach (GuideAreaDef def in GuideAreaDef.ForScreen(HelpContext))
        {
            foreach ((string id, Rect rect) in GuideRegistry.Current)
            {
                if (id == def.area)
                    list.Add((def, rect));
            }
        }
        return list;
    }

    private void DrawGuideOverlay(Rect inRect)
    {
        if (!_guide)
            return;
        List<(GuideAreaDef def, Rect rect)> areas = AreasNow();
        Widgets.DrawBoxSolid(new Rect(inRect.x, inRect.y + TitleHeight, inRect.width, inRect.height - TitleHeight), new Color(0f, 0f, 0f, 0.55f));
        Vector2 mouse = Event.current.mousePosition;
        string pulse = _guideArea != null && _guideStep < _guideArea.steps.Count ? _guideArea.steps[_guideStep].highlight : null;
        HelpProgress progress = HelpProgress.Get();
        // Only the smallest area under the mouse is hovered: a part inside a bigger one (the news line in the bottom bar).
        GuideAreaDef hovered = _guideArea == null
            ? areas.Where(a => a.rect.Contains(mouse)).OrderBy(a => a.rect.width * a.rect.height).Select(a => a.def).FirstOrDefault()
            : null;

        for (int i = 0; i < areas.Count; i++)
        {
            (GuideAreaDef def, Rect rect) = areas[i];
            bool hover = def == hovered;
            bool active = _guideArea == def;
            bool pulsing = pulse != null && def.area == pulse;
            bool areaSeen = progress?.GuideAreaSeen(def) == true;
            Color c = areaSeen ? NetrunUI.Ok : NetrunUI.Accent;
            // Quiet by default: corner brackets and a faint tint; the hovered or open one gets a full frame.
            Widgets.DrawBoxSolid(rect, new Color(c.r, c.g, c.b, hover || active ? 0.14f : 0.04f));
            if (pulsing)
                Widgets.DrawBoxSolid(rect, new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.1f + 0.15f * (0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 4f))));
            if (hover || active)
            {
                GUI.color = Color.white;
                Widgets.DrawBox(rect, 2);
                GUI.color = Color.white;
            }
            NetrunUI.Brackets(rect, new Color(c.r, c.g, c.b, hover || active ? 1f : 0.85f), hover || active ? 3f : 2f, Mathf.Clamp(Mathf.Min(rect.width, rect.height) * 0.25f, 8f, 22f));

            // The number badge on the corner; seen areas are green.
            var badge = new Rect(Mathf.Clamp(rect.x - 11f, inRect.x + 3f, inRect.xMax - 27f), Mathf.Clamp(rect.y - 11f, inRect.y + 3f, inRect.yMax - 27f), 24f, 24f);
            GUI.color = NetrunUI.BgWindow;
            GUI.DrawTexture(badge.ExpandedBy(2f), NetrunUI.Disc);
            GUI.color = c;
            GUI.DrawTexture(badge, NetrunUI.Disc);
            // Not looked at yet: a slow ring breathes around the number.
            if (!areaSeen && NetrunMod.Settings.Animations)
            {
                float f = Time.realtimeSinceStartup / 1.4f % 1f;
                GUI.color = new Color(c.r, c.g, c.b, 0.6f * (1f - f));
                GUI.DrawTexture(badge.ExpandedBy(2f + 8f * f), NetrunUI.Ring);
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(badge, (i + 1).ToString().Colorize(NetrunUI.BgWindow));
            Text.Anchor = TextAnchor.UpperLeft;

            // The hovered area says its name right next to its number.
            if (hover)
            {
                string name = def.LabelCap;
                float nw = Text.CalcSize(name).x + 16f;
                var plate = new Rect(Mathf.Min(badge.xMax + 4f, inRect.xMax - nw - 4f), badge.y, nw, 24f);
                NetrunUI.Box(plate, NetrunUI.BgWindow, c);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(plate, name);
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        if (_guideArea != null)
        {
            int index = areas.FindIndex(a => a.def == _guideArea);
            Rect target = index >= 0 ? areas[index].rect : new Rect(inRect.center, Vector2.zero);
            DrawGuideCard(inRect, target, index + 1, areas.Count);
        }
    }

    private void DrawGuideCard(Rect inRect, Rect target, int number, int total)
    {
        GuideAreaDef def = _guideArea;
        GuideStep step = def.steps.Count > 0 ? def.steps[Mathf.Clamp(_guideStep, 0, def.steps.Count - 1)] : null;
        const float width = 360f;
        Text.Font = GameFont.Small;
        string text = step?.text ?? string.Empty;
        float textH = Text.CalcHeight(text, width - 24f);
        string hint = step?.hint.NullOrEmpty() == false ? "▸ " + "NR_GuideTry".Translate() + " " + step.hint : null;
        float hintH = hint == null ? 0f : Text.CalcHeight(hint, width - 24f) + 4f;
        float height = 12f + 18f + 26f + textH + 6f + hintH + 34f + 10f;

        // Beside the area: right if it fits, else left, else below; always inside the window.
        float x = target.xMax + 14f;
        if (x + width > inRect.xMax - 8f)
            x = target.x - width - 14f;
        if (x < inRect.x + 8f)
            x = Mathf.Clamp(target.center.x - width / 2f, inRect.x + 8f, inRect.xMax - width - 8f);
        float y = Mathf.Clamp(target.y, inRect.y + 50f, inRect.yMax - height - 8f);
        var card = new Rect(x, y, width, height);
        _guideCard = card;

        Widgets.DrawBoxSolid(card, NetrunUI.BgWindow);
        GUI.color = Color.white;
        Widgets.DrawBox(card, 2);
        Rect r = card.ContractedBy(12f);
        float cy = r.y;
        Text.Font = GameFont.Tiny;
        string header = "NR_GuideCardHeader".Translate(number, total);
        if (def.steps.Count > 1)
            header += " · " + "NR_GuideStep".Translate(_guideStep + 1, def.steps.Count);
        Widgets.Label(new Rect(r.x, cy, r.width, 18f), header.Colorize(NetrunUI.Accent));
        cy += 18f;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(r.x, cy, r.width, 26f), def.LabelCap);
        cy += 26f;
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(r.x, cy, r.width, textH), text);
        cy += textH + 6f;
        if (hint != null)
        {
            Widgets.Label(new Rect(r.x, cy, r.width, hintH), hint.Colorize(NetrunUI.Ok));
            cy += hintH;
        }

        // Buttons (drawn here, clicked in GuideInput).
        float by = card.yMax - 34f;
        Text.Font = GameFont.Tiny;
        float W(string s) => Text.CalcSize(s).x + 20f;
        string backText = "NR_GuideBack".Translate(), nextText = (_guideStep < def.steps.Count - 1 ? "NR_GuideNext" : "NR_GuideDone").Translate();
        string helpText = "NR_GuideHelp".Translate(), exitText = "NR_GuideExit".Translate();
        _guideBack = new Rect(r.x, by, W(backText), 24f);
        _guideNext = new Rect(_guideBack.xMax + 4f, by, W(nextText), 24f);
        _guideHelp = new Rect(_guideNext.xMax + 4f, by, W(helpText), 24f);
        _guideExit = new Rect(r.xMax - W(exitText), by, W(exitText), 24f);
        Text.Font = GameFont.Small;
        GuideButton(_guideBack, "NR_GuideBack".Translate(), _guideStep > 0 ? NetrunUI.Muted : new Color(1f, 1f, 1f, 0.2f));
        GuideButton(_guideNext, (_guideStep < def.steps.Count - 1 ? "NR_GuideNext" : "NR_GuideDone").Translate(), NetrunUI.Accent);
        if (def.helpPage != null)
            GuideButton(_guideHelp, "NR_GuideHelp".Translate(), NetrunUI.Ok);
        else
            _guideHelp = default;
        GuideButton(_guideExit, "NR_GuideExit".Translate(), NetrunUI.Muted);
    }

    private static void GuideButton(Rect rect, string label, Color color)
    {
        if (Mouse.IsOver(rect))
            Widgets.DrawBoxSolid(rect, new Color(color.r, color.g, color.b, 0.15f));
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, label.Colorize(color));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }
}
