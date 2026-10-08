using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The terminal's main menu (docs/CONCEPT_MAIN_MENU.md section 1, mockups 30 and 31), drawn on the terminal's own
/// screen: six tiles (Continue, Settings, Cyberpedia, Achievements, Difficulty, Customization) and their pages.
/// The casing's MENU key opens it, ACHIEV opens the achievements; Esc and "Back" step out.
/// </summary>
public partial class Dialog_NetTerminal
{
    private enum MenuPage
    {
        Home,
        Settings,
        Achievements,
        Difficulty,
        Customization,
    }

    private static readonly string[] CustomTabs = ["frame", "sound", "graphics"];
    private static readonly Dictionary<string, string> CustomTabIcons = new() { ["frame"] = "Tab_Frame", ["sound"] = "Tab_Sound", ["graphics"] = "Tab_Graphics" };
    private static readonly Color AchYellow = NetrunUI.Hex(0xe6c431);
    private static readonly Color CustomBlue = NetrunUI.Hex(0x5ec8f2);

    private MenuPage _menuPage;
    private View _viewBeforeMenu = View.Map;
    private string _customTab = "frame";
    private Vector2 _achScroll;
    private float _achHeight = 600f;

    private void OpenMenu(MenuPage page = MenuPage.Home)
    {
        if (_view != View.Menu)
            _viewBeforeMenu = _view == View.Help ? _viewBeforeHelp : _view;
        _guide = false;
        _menuPage = page;
        _view = View.Menu;
    }

    /// <summary>Back: from a page to the tiles, from the tiles to where the menu was opened.</summary>
    private void LeaveMenu()
    {
        if (_view != View.Menu)
            return;
        if (_menuPage != MenuPage.Home)
            _menuPage = MenuPage.Home;
        else
            _view = _viewBeforeMenu == View.Menu ? View.Map : _viewBeforeMenu;
    }

    private string MenuTitle => _menuPage switch
    {
        MenuPage.Home => "NR_MenuTitle".Translate(),
        _ => "NR_MenuTitlePage".Translate(MenuPageLabel(_menuPage).ToUpper()),
    };

    private static string MenuPageLabel(MenuPage page) => ("NR_MenuPage_" + page).Translate();

    private void DrawMenu(Rect body)
    {
        Rect area = body.ContractedBy(16f);
        switch (_menuPage)
        {
            case MenuPage.Home:
                DrawMenuHome(area);
                break;
            case MenuPage.Settings:
                NetrunMod.Settings.DrawPage(area, "settings");
                break;
            case MenuPage.Difficulty:
                NetrunMod.Settings.DrawPage(area, "difficulty");
                break;
            case MenuPage.Achievements:
                DrawAchievements(area);
                break;
            case MenuPage.Customization:
                DrawCustomization(area);
                break;
        }
    }

    // ---- Tiles ----

    private void DrawMenuHome(Rect area)
    {
        var footer = new Rect(area.x, area.yMax - 28f, area.width, 28f);
        area.yMax = footer.y - 8f;
        HelpProgress help = HelpProgress.Get();
        int newPages = help == null ? 0 : NetrunUI.Cached("menu.newpages", () => DefDatabase<HelpPageDef>.AllDefs.Count(p => help.IsNew(p)), 2f);
        var achievements = DefDatabase<NetrunAchievementDef>.AllDefsListForReading;
        int done = NetrunUI.Cached("menu.achdone", () => achievements.Count(a => a.Done), 2f);

        (string id, Color color, Texture2D icon, string badge, System.Action open)[] tiles =
        [
            ("Continue", NetrunUI.Ok, NetrunTex.MenuIcon("Continue"), null, () => { _menuPage = MenuPage.Home; _view = _viewBeforeMenu == View.Menu ? View.Map : _viewBeforeMenu; }),
            ("Settings", NetrunUI.Warn, NetrunTex.MenuIcon("Settings"), null, () => _menuPage = MenuPage.Settings),
            ("Cyberpedia", NetrunUI.Purple, NetrunTex.MenuIcon("Cyberpedia"), newPages > 0 ? (string)"NR_MenuNewBadge".Translate(newPages) : null, () => { _view = _viewBeforeMenu; OpenHelp(); }),
            ("Achievements", AchYellow, NetrunTex.MenuIcon("Achievements"), $"{done}/{achievements.Count}", () => _menuPage = MenuPage.Achievements),
            ("Difficulty", NetrunUI.Bad, NetrunTex.MenuIcon("Difficulty"), null, () => _menuPage = MenuPage.Difficulty),
            ("Customization", CustomBlue, NetrunTex.TabIconByName("Tab_Frame"), null, () => _menuPage = MenuPage.Customization),
        ];
        // The tiles fill the screen (two rows of three, or three of two when narrow); the net's state runs below them.
        var status = new Rect(area.x, area.yMax - 40f, area.width, 40f);
        area.yMax = status.y - 12f;
        int cols = area.width >= 720f ? 3 : 2;
        int rows = Mathf.CeilToInt(tiles.Length / (float)cols);
        const float gap = 16f;
        float tw = (area.width - gap * (cols - 1)) / cols;
        float th = Mathf.Max(120f, (area.height - gap * (rows - 1)) / rows);
        DrawMenuStatus(status);
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            var r = new Rect(area.x + i % cols * (tw + gap), area.y + i / cols * (th + gap), tw, th);
            bool over = Mouse.IsOver(r);
            if (over)
                NetrunUI.Glow(r, t.color, 0.25f, 10f);
            NetrunUI.Box(r, over ? NetrunUI.BgCardAlt : NetrunUI.BgCard, new Color(t.color.r, t.color.g, t.color.b, over ? 1f : 0.75f), 2);
            if (t.icon != null)
            {
                float s = NetrunUI.Icon(Mathf.Clamp(th * 0.36f, 44f, 84f));
                NetrunUI.BrightIcon(new Rect(r.x + 18f, r.y + 18f, s, s), t.icon);
            }
            if (t.badge != null)
            {
                Text.Font = GameFont.Tiny;
                float bw = Text.CalcSize(t.badge).x + 14f;
                var b = new Rect(r.xMax - bw - 10f, r.y + 10f, bw, 18f);
                Widgets.DrawBoxSolid(b, t.color);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(b, t.badge.Colorize(NetrunUI.Hex(0x101317)));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
            Text.Font = GameFont.Medium;
            string title = ((string)("NR_MenuPage_" + t.id).Translate()).ToUpper();
            Widgets.Label(new Rect(r.x + 16f, r.yMax - 76f, r.width - 32f, 32f), title.Colorize(t.color));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(r.x + 16f, r.yMax - 46f, r.width - 32f, 42f), ((string)("NR_MenuDesc_" + t.id).Translate()).Colorize(NetrunUI.Muted));
            if (over && Event.current.type == EventType.Repaint && _menuHover != i)
            {
                _menuHover = i;
                NetrunSound.Hover();
            }
            if (Widgets.ButtonInvisible(r, doMouseoverSound: false))
            {
                NetrunSound.Click();
                t.open();
            }
        }

        // Footer: the version, diagnostics, a bug report, Esc.
        GUI.color = new Color(1f, 1f, 1f, 0.12f);
        Widgets.DrawLineHorizontal(footer.x, footer.y - 4f, footer.width);
        GUI.color = Color.white;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        string version = "NR_MenuVersion".Translate(LoadedModManager.GetMod<NetrunMod>()?.Content?.ModMetaData?.ModVersion ?? "?");
        float x = footer.x;
        Widgets.Label(new Rect(x, footer.y, 260f, footer.height), version.Colorize(NetrunUI.Muted));
        x += Text.CalcSize(version).x + 24f;
        if (FooterLink(ref x, footer, "NR_MenuCopyDiagnostics".Translate()))
        {
            GUIUtility.systemCopyBuffer = NetrunDiagnostics.Build();
            Messages.Message("NR_SettingsReportCopied".Translate(), RimWorld.MessageTypeDefOf.NeutralEvent, historical: false);
        }
        if (FooterLink(ref x, footer, "NR_MenuReportBug".Translate()))
        {
            _view = _viewBeforeMenu;
            OpenHelp(DefDatabase<HelpPageDef>.GetNamedSilentFail("NR_Help_Settings"));
        }
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(footer, ((string)"NR_MenuEsc".Translate()).Colorize(NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }

    private int _menuHover = -1;

    /// <summary>One line under the tiles: Heat, awake AIs, runners in the net, cyberdata.</summary>
    private void DrawMenuStatus(Rect r)
    {
        NetrunUI.Box(r, NetrunUI.BgCard, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.35f));
        HeatTracker heat = HeatTracker.Get();
        HeatLevel level = heat?.Level ?? HeatLevel.Shadow;
        (string label, string value, Color color)[] cells =
        [
            ("NR_MenuStatusHeat".Translate(), $"{heat?.Heat ?? 0f:0} · {level.Label()}", level.Color()),
            ("NR_MenuStatusAI".Translate(), (AIManager.Get()?.AIs.Count ?? 0).ToString(), NetrunUI.Purple),
            ("NR_MenuStatusRunners".Translate(), (NetMapManager.Get()?.Runners.Count ?? 0).ToString(), NetrunUI.Accent),
            ("NR_MenuStatusData".Translate(), NetrunUI.Cached("menu.data", () => _map == null ? 0 : ColonyGoods.Count(_map, NetrunDefOf.USH_Cyberdata), 1f).ToString(), NetrunUI.Ok),
        ];
        float cw = r.width / cells.Length;
        for (int i = 0; i < cells.Length; i++)
        {
            var c = new Rect(r.x + 14f + i * cw, r.y, cw - 14f, r.height);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            string label = cells[i].label.ToUpper();
            float lw = Text.CalcSize(label).x + 8f;
            Widgets.Label(new Rect(c.x, c.y, lw, c.height), label.Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(c.x + lw, c.y, c.width - lw, c.height), cells[i].value.Colorize(cells[i].color));
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }

    private static bool FooterLink(ref float x, Rect footer, string label)
    {
        float w = Text.CalcSize(label).x;
        var r = new Rect(x, footer.y, w, footer.height);
        bool over = Mouse.IsOver(r);
        Widgets.Label(r, label.Colorize(over ? NetrunUI.Accent : NetrunUI.TextColor));
        if (over)
            Widgets.DrawLineHorizontal(r.x, r.center.y + 8f, w);
        x += w + 24f;
        return Widgets.ButtonInvisible(r);
    }

    // ---- Achievements ----

    private void DrawAchievements(Rect area)
    {
        var list = DefDatabase<NetrunAchievementDef>.AllDefsListForReading.OrderBy(a => a.order).ToList();
        int done = list.Count(a => a.Done);
        Text.Font = GameFont.Small;
        string head = "NR_MenuAchHead".Translate(done, list.Count, list.Where(a => a.Done).Sum(a => a.points));
        Widgets.Label(new Rect(area.x, area.y, area.width, 24f), head.Colorize(AchYellow));
        if (ModsConfig.IsActive("vanillaexpanded.achievements"))
            Widgets.Label(new Rect(area.x, area.y + 22f, area.width, 22f), ((string)"NR_MenuAchVae".Translate()).Colorize(NetrunUI.Muted));
        area.yMin += 50f;

        int cols = area.width >= 760f ? 3 : 2;
        const float gap = 10f, ch = 92f;
        bool scrolls = _achHeight > area.height;
        var view = new Rect(0f, 0f, area.width - (scrolls ? 16f : 0f), Mathf.Max(_achHeight, area.height));
        float cw = (view.width - gap * (cols - 1)) / cols;
        Widgets.BeginScrollView(area, ref _achScroll, view);
        for (int i = 0; i < list.Count; i++)
        {
            NetrunAchievementDef a = list[i];
            var r = new Rect(i % cols * (cw + gap), i / cols * (ch + gap), cw, ch);
            bool got = a.Done;
            Color c = got ? AchYellow : NetrunUI.Muted;
            NetrunUI.Box(r, NetrunUI.BgCard, new Color(c.r, c.g, c.b, got ? 0.9f : 0.35f), got ? 2 : 1);
            var chip = new Rect(r.x + 8f, r.y + 8f, 64f, 64f);
            GUI.color = got ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            if (a.Frame != null)
                GUI.DrawTexture(chip, a.Frame);
            GUI.DrawTexture(chip, a.Icon);
            GUI.color = Color.white;
            float tx = chip.xMax + 10f, tw = r.xMax - tx - 8f;
            bool hidden = a.Secret && !got;
            Widgets.Label(new Rect(tx, r.y + 6f, tw, 22f), (hidden ? "???" : a.LabelCap.ToString()).Colorize(got ? AchYellow : NetrunUI.TextColor));
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(tx, r.y + 28f, tw, 34f), (hidden ? (string)"NR_MenuAchSecret".Translate() : a.description).Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
            // Progress, and the frame theme this achievement opens.
            var bar = new Rect(tx, r.yMax - 16f, tw - 60f, 8f);
            NetrunUI.Bar(bar, a.Progress / (float)a.count, got ? AchYellow : NetrunUI.Accent);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(bar.xMax + 6f, r.yMax - 22f, 56f, 18f), $"{a.Progress}/{a.count}".Colorize(NetrunUI.Muted));
            Text.Font = GameFont.Small;
            DeckTheme reward = DeckThemes.All.FirstOrDefault(t => t.UnlockKey == a.key);
            string tip = hidden ? (string)"NR_MenuAchSecret".Translate() : a.description;
            if (reward != null)
            {
                var badge = new Rect(r.xMax - 26f, r.y + 6f, 20f, 20f);
                GUI.color = got ? AchYellow : NetrunUI.Muted;
                GUI.DrawTexture(badge, NetrunTex.CyberIcon("Icon_Lock") ?? BaseContent.BadTex);
                GUI.color = Color.white;
                tip += "\n\n" + "NR_MenuAchTheme".Translate(reward.Label);
            }
            NetrunUI.Tip(r, tip);
        }
        _achHeight = (list.Count + cols - 1) / cols * (ch + gap);
        Widgets.EndScrollView();
    }

    // ---- Customization ----

    private void DrawCustomization(Rect area)
    {
        float x = area.x;
        foreach (string tab in CustomTabs)
        {
            string label = ((string)("NR_CustomTab_" + tab).Translate()).ToUpper();
            Texture2D icon = NetrunTex.TabIconByName(CustomTabIcons[tab]);
            float w = NetrunUI.ButtonWidth(label, icon != null) + 16f;
            var r = new Rect(x, area.y, w, 34f);
            bool on = _customTab == tab;
            if (on)
                Widgets.DrawBoxSolid(r, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.18f));
            if (NetrunUI.Button(r, label, on ? NetrunUI.Accent : NetrunUI.Muted, true, null, icon))
                _customTab = tab;
            x += w + 8f;
        }
        var footer = new Rect(area.x, area.yMax - 22f, area.width, 22f);
        Text.Font = GameFont.Tiny;
        Widgets.Label(footer, ((string)"NR_CustomFooter".Translate()).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        NetrunMod.Settings.DrawPage(new Rect(area.x, area.y + 44f, area.width, footer.y - area.y - 50f), _customTab);
    }
}
