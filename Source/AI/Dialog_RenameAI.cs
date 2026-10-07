using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>A small window to give an AI a name of the player's choosing.</summary>
public class Dialog_RenameAI : Window
{
    private readonly NetAI _ai;
    private string _name;
    private bool _focused;

    public override Vector2 InitialSize => new(420f, 170f);

    public Dialog_RenameAI(NetAI ai)
    {
        _ai = ai;
        _name = ai.Name;
        forcePause = false;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
        closeOnAccept = false;
        doCloseX = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "NR_AIRenameTitle".Translate(_ai.Name).Colorize(NetrunUI.Purple));
        Text.Font = GameFont.Small;
        GUI.SetNextControlName("NetrunRenameAIField");
        _name = Widgets.TextField(new Rect(0f, 44f, inRect.width, 32f), _name, 24);
        if (!_focused)
        {
            GUI.FocusControl("NetrunRenameAIField");
            _focused = true;
        }
        AcceptanceReport valid = _name.NullOrEmpty() || _name.Trim().Length == 0 ? (AcceptanceReport)"NR_AIRenameEmpty".Translate() : true;
        bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        if (NetrunUI.Button(new Rect(inRect.width - 140f, inRect.height - 34f, 140f, 32f), "NR_AIRenameOk".Translate(), NetrunUI.Ok, valid) || (enter && valid.Accepted))
        {
            _ai.Name = _name.Trim();
            NetrunSound.Play("UI_Confirm");
            Close();
            if (enter)
                Event.current.Use();
        }
    }
}
