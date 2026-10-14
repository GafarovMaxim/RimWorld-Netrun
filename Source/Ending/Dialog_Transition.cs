using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using USH_HE;
using Verse;

namespace Netrun;

/// <summary>
/// The Transition for a group (Maxim, 2026-10-15): every occupied pod on the map in one list, tick who goes, and they all
/// start the upload at the same moment. Pods that cannot start show why.
/// </summary>
public class Dialog_Transition : Window
{
    private const float RowHeight = 44f;

    private readonly List<Building_Cyberpod> _pods;
    private readonly HashSet<Building_Cyberpod> _picked = [];
    private Vector2 _scroll;

    public override Vector2 InitialSize => new(520f, 460f);
    protected override float Margin => 0f;

    public Dialog_Transition(Building_Cyberpod from)
    {
        _pods = CyberpodUtility.PlayerPods(from.Map).Where(p => p.ContainedThing is Pawn pawn && !TransitionManager.IsShell(pawn)).ToList();
        TransitionManager manager = TransitionManager.Get();
        if (manager != null && manager.CanBegin(from).Accepted)
            _picked.Add(from);
        doCloseX = false;
        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
        draggable = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        bool font = NetrunFont.Push();
        try
        {
            Rect screen = NetrunUI.Bezel(inRect, 8f);
            DrawContents(screen);
            NetrunUI.Scanlines(screen);
        }
        finally
        {
            NetrunFont.Pop(font);
        }
    }

    private void DrawContents(Rect inRect)
    {
        TransitionManager manager = TransitionManager.Get();
        Widgets.DrawBoxSolid(inRect, NetrunUI.BgWindow);
        Rect r = inRect.ContractedBy(14f);
        float y = r.y;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(r.x, y, r.width, 32f), ((string)"NR_UploadBegin".Translate()).Colorize(NetrunUI.Accent));
        Text.Font = GameFont.Small;
        y += 34f;
        string intro = "NR_TransitionPick".Translate(TransitionManager.UploadHours.ToString("0"));
        float ih = Text.CalcHeight(intro, r.width);
        Widgets.Label(new Rect(r.x, y, r.width, ih), intro.Colorize(NetrunUI.Muted));
        y += ih + 8f;

        var list = new Rect(r.x, y, r.width, r.yMax - 44f - y);
        var view = new Rect(0f, 0f, list.width - 16f, _pods.Count * (RowHeight + 6f));
        Widgets.BeginScrollView(list, ref _scroll, view);
        float ry = 0f;
        foreach (Building_Cyberpod pod in _pods)
        {
            DrawRow(new Rect(0f, ry, view.width, RowHeight), pod, manager);
            ry += RowHeight + 6f;
        }
        Widgets.EndScrollView();

        _picked.RemoveWhere(p => manager == null || !manager.CanBegin(p).Accepted);
        float by = r.yMax - 36f;
        string confirm = "NR_TransitionStart".Translate(_picked.Count);
        AcceptanceReport can = _picked.Count > 0 ? true : (AcceptanceReport)"NR_TransitionNobody".Translate();
        float cw = NetrunUI.ButtonWidth(confirm);
        if (NetrunUI.Button(new Rect(r.xMax - cw, by, cw, 34f), confirm, NetrunUI.Accent, can))
        {
            List<Building_Cyberpod> picked = _picked.ToList();
            string names = string.Join(", ", picked.Select(p => (p.ContainedThing as Pawn)?.LabelShort));
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "NR_UploadConfirm".Translate(names, TransitionManager.UploadHours.ToString("0")), () => manager?.BeginGroup(picked), destructive: true));
            Close();
        }
        string cancel = "CloseButton".Translate();
        float xw = NetrunUI.ButtonWidth(cancel);
        if (NetrunUI.Button(new Rect(r.xMax - cw - 8f - xw, by, xw, 34f), cancel, NetrunUI.Muted, true))
            Close();
    }

    /// <summary>A checkbox, the colonist's name and either "ready" or the reason the pod cannot start.</summary>
    private void DrawRow(Rect rect, Building_Cyberpod pod, TransitionManager manager)
    {
        Pawn pawn = pod.ContainedThing as Pawn;
        AcceptanceReport can = manager?.CanBegin(pod) ?? false;
        bool uploading = manager != null && manager.Uploading(pawn);
        NetrunUI.Card(rect, can.Accepted ? NetrunUI.Accent : NetrunUI.Muted);
        bool on = _picked.Contains(pod);
        bool was = on;
        Widgets.Checkbox(new Vector2(rect.x + 10f, rect.y + (rect.height - 24f) / 2f), ref on, 24f, disabled: !can.Accepted);
        if (on != was)
        {
            if (on)
                _picked.Add(pod);
            else
                _picked.Remove(pod);
        }
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(rect.x + 44f, rect.y, rect.width * 0.45f, rect.height), pawn?.LabelShortCap ?? "?");
        string state = uploading ? manager.Progress(pawn).ToStringPercent()
            : can.Accepted ? (string)"NR_TransitionReady".Translate() : can.Reason ?? "";
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(rect.x + 44f + rect.width * 0.45f, rect.y, rect.width * 0.55f - 54f, rect.height),
            state.Colorize(can.Accepted ? NetrunUI.Ok : uploading ? NetrunUI.Accent : NetrunUI.Muted));
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        if (can.Accepted && Widgets.ButtonInvisible(new Rect(rect.x + 40f, rect.y, rect.width - 40f, rect.height)))
        {
            if (!_picked.Remove(pod))
                _picked.Add(pod);
        }
    }
}
