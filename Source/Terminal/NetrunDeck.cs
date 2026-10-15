using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The terminal's casing, template A v3 (docs/CONCEPT_MAIN_MENU.md section 2, mockups 29 and 32): rails as handles,
/// ribbed rubber bumpers; on top the hardware keys CLOSE, MENU, ACHIEV, THEME, the tuner, the maker's plate, the
/// lights NET, SICK, POD and the RF signal meter with the antenna; below the status LCD, the keys back, core and
/// Cyberpedia, the segmented Heat scale and the NET LINK switch. Colours come from the chosen theme (DeckThemes).
/// </summary>
public static partial class NetrunUI
{
    // The bottom band carries no stencils (the terminal's status bar names the same things), so it is lower.
    public const float DeckRail = 22f, DeckSide = 28f, DeckTop = 64f, DeckBottom = 58f;
    private static readonly Vector2 ClassicExtra = new(2f * (DeckRail + DeckSide), DeckTop + DeckBottom);
    /// <summary>How much bigger a window gets around its screen with the chosen deck casing.</summary>
    public static Vector2 DeckExtra => DeckThemes.Current.Style switch
    {
        DeckStyle.Nomad when NomadTex.Ready => NomadExtra,
        DeckStyle.Street when StreetTex.Ready => StreetExtra,
        _ => ClassicExtra,
    };
    /// <summary>What the chosen casing takes beyond the classic one: windows grow by it, so the screen keeps its size.</summary>
    public static Vector2 DeckExtraOverClassic => DeckExtra - ClassicExtra;

    /// <summary>onKey ids: the bottom keys back, core, Cyberpedia, then the top keys close, menu, achievements.</summary>
    public const int KeyBack = 0, KeyCore = 1, KeyHelp = 2, KeyClose = 3, KeyMenu = 4, KeyAchievements = 5;

    private const float Bump = 78f;
    private static readonly Color DeckOrange = Hex(0xff9a4a);
    private static readonly Color PodGreen = Hex(0x31c46a);
    private static readonly Color LcdGreen = Hex(0x5dff8a);
    /// <summary>Heat level colours of the segmented scale: Shadow, Noticed, Tracked, Target, Red Zone.</summary>
    private static readonly Color[] HeatSegments = [Hex(0x31c46a), Hex(0xe6c431), Hex(0xff8a3c), Hex(0xff4a3c), Hex(0xb3122a)];

    private static DeckTheme _t = DeckThemes.All[0];

    // The textured set from tools/gen_frame.py (docs/CONCEPT_TERMINAL_FRAME.md); each part falls back to code drawing.
    private static Texture2D FrameTex(string name) => ContentFinder<Texture2D>.Get("UI/Netrun/Frame/" + name, reportFailure: false);
    private static readonly Texture2D TexKey = FrameTex("Deck_Key"), TexKeyHot = FrameTex("Deck_Key_Hot"), TexRail = FrameTex("Deck_Rail"),
        TexBracket = FrameTex("Deck_Bracket"), TexLedGreen = FrameTex("Deck_Led_Green"), TexLedOrange = FrameTex("Deck_Led_Orange"),
        TexLedRed = FrameTex("Deck_Led_Red"), TexLedOff = FrameTex("Deck_Led_Off");
    private static readonly Texture2D[] TexBumpers = [FrameTex("Deck_Bumper_TL"), FrameTex("Deck_Bumper_TR"), FrameTex("Deck_Bumper_BL"), FrameTex("Deck_Bumper_BR")];
    /// <summary>The field deck's key colour: other themes tint the key texture by their key colour against it.</summary>
    private static readonly Color FieldKey = Hex(0x252a31);

    /// <summary>A texture cut in three along its height: fixed ends, a stretched middle.</summary>
    private static void ThreeSliceV(Rect r, Texture2D tex, float cap)
    {
        float v = cap / tex.height;
        GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y, r.width, cap), tex, new Rect(0f, 1f - v, 1f, v));
        GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y + cap, r.width, r.height - 2f * cap), tex, new Rect(0f, v, 1f, 1f - 2f * v));
        GUI.DrawTextureWithTexCoords(new Rect(r.x, r.yMax - cap, r.width, cap), tex, new Rect(0f, 0f, 1f, v));
    }
    private static float _tuner = 0.5f, _tunerTarget = 0.5f, _rf;
    private static int _tunerLogCount = -1;
    private static float _heatSample = -1f, _heatSampleAt, _heatTrend;

    private static Color DeckEdge => _t.Edge;
    private static Color DeckRubber => _t.Rubber;

    /// <summary>A raised edge: light on top and left, dark on the bottom and right (or the other way for a recess).</summary>
    private static void Bevel(Rect r, bool raised, float strength = 1f)
    {
        Color light = new(1f, 1f, 1f, 0.16f * strength), dark = new(0f, 0f, 0f, 0.5f * strength);
        Widgets.DrawBoxSolid(new Rect(r.x, r.y, r.width, 1f), raised ? light : dark);
        Widgets.DrawBoxSolid(new Rect(r.x, r.y, 1f, r.height), raised ? light : dark);
        Widgets.DrawBoxSolid(new Rect(r.x, r.yMax - 1f, r.width, 1f), raised ? dark : light);
        Widgets.DrawBoxSolid(new Rect(r.xMax - 1f, r.y, 1f, r.height), raised ? dark : light);
    }

    /// <summary>A small cross-head screw.</summary>
    private static void Screw(Vector2 c, float size = 7f)
    {
        var s = new Rect(c.x - size / 2f, c.y - size / 2f, size, size);
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(s.x + 0.5f, s.y + 1f, s.width, s.height), Disc);
        GUI.color = DeckEdge;
        GUI.DrawTexture(s, Disc);
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(s.x + 1f, c.y - 0.5f, s.width - 2f, 1f), BaseContent.WhiteTex);
        GUI.DrawTexture(new Rect(c.x - 0.5f, s.y + 1f, 1f, s.height - 2f), BaseContent.WhiteTex);
        GUI.color = Color.white;
    }

    /// <summary>A stencilled label on the casing (tiny capitals), never cut off.</summary>
    private static void Stencil(Rect r, string text, TextAnchor anchor = TextAnchor.UpperCenter, Color? color = null)
    {
        Text.Font = GameFont.Tiny;
        Text.Anchor = anchor;
        Vector2 size = Text.CalcSize(text);
        if (r.width < size.x + 2f)
            r = anchor == TextAnchor.UpperRight ? new Rect(r.xMax - size.x - 2f, r.y, size.x + 2f, r.height)
                : anchor == TextAnchor.UpperLeft ? new Rect(r.x, r.y, size.x + 2f, r.height)
                : new Rect(r.center.x - size.x / 2f - 1f, r.y, size.x + 2f, r.height);
        r.height = Mathf.Max(r.height, size.y);
        Widgets.Label(r, text.Colorize(color ?? _t.Stencil));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }

    /// <summary>A recessed tray for keys, lights or a screen.</summary>
    private static void Recess(Rect r, Color fill)
    {
        Widgets.DrawBoxSolid(r, fill);
        Bevel(r, false);
    }

    /// <summary>
    /// Draws the casing around <paramref name="outer"/> and returns the screen inside. <paramref name="onKey"/> gets
    /// the key ids (KeyBack...KeyAchievements); THEME is handled here. Without <paramref name="menu"/> the MENU and
    /// ACHIEV keys are shown dim (a window that is not the terminal).
    /// </summary>
    public static Rect DeckBezel(Rect outer, string plate, float heat01, bool online, System.Action<int> onKey = null, bool menu = true)
    {
        _t = DeckThemes.Current;
        bool anim = NetrunMod.Settings.Animations;
        float time = Time.realtimeSinceStartup;
        float ease = anim ? Mathf.Clamp01(Time.unscaledDeltaTime * 3f) : 1f;
        float heat = heat01 * HeatTracker.MaxHeat;
        HeatLevel level = HeatLevelUtility.LevelOf(heat);
        // Keys sink while the mouse button is held on them.
        if (Event.current.rawType == EventType.MouseDown && Event.current.button == 0)
            _pressed = true;
        else if (Event.current.rawType == EventType.MouseUp)
            _pressed = false;
        if (Event.current.type == EventType.Repaint)
        {
            // The tuner's mark jumps to a new spot on every logged event, then glides there.
            int logCount = HeatTracker.Get()?.Log.Count ?? 0;
            if (logCount != _tunerLogCount)
            {
                if (_tunerLogCount >= 0)
                    _tunerTarget = Rand.Range(0.1f, 0.9f);
                _tunerLogCount = logCount;
            }
            _tuner = Mathf.Lerp(_tuner, _tunerTarget, ease * 0.5f);
            float rfTarget = online ? 4.2f + (anim ? 1.6f * Mathf.PerlinNoise(time * 0.9f, 0.3f) : 1f) : 0f;
            _rf = Mathf.Lerp(_rf, rfTarget, ease);
            // The LCD's trend arrow: Heat now against ten seconds ago.
            if (_heatSample < 0f || time - _heatSampleAt > 10f)
            {
                _heatTrend = _heatSample < 0f ? 0f : heat - _heatSample;
                _heatSample = heat;
                _heatSampleAt = time;
            }
        }

        // Themes with their own silhouette draw their own casing around the same keys and readouts.
        if (_t.Style == DeckStyle.Nomad && NomadTex.Ready)
            return NomadBezel(outer, heat, level, online, anim, time, onKey, menu);
        if (_t.Style == DeckStyle.Street && StreetTex.Ready)
            return StreetBezel(outer, heat, level, online, anim, time, onKey, menu);

        var body = new Rect(outer.x + DeckRail, outer.y, outer.width - 2f * DeckRail, outer.height);
        var screen = new Rect(body.x + DeckSide, body.y + DeckTop, body.width - 2f * DeckSide, body.height - DeckTop - DeckBottom);
        // Everything but the keys only draws on Repaint (the other GUI events of the frame skip it: docs/PERF_AUDIT_4.md, 5).
        bool paint = Event.current.type == EventType.Repaint;
        if (paint)
            DrawCasing(outer, body, screen);
        DrawDeckTop(body, outer, plate, level, online, anim, time, onKey, menu, paint);
        DrawDeckBottom(body, level, heat, online, menu ? onKey : null, paint);
        return screen;
    }

    /// <summary>
    /// Lets the window be moved only by the casing's top and side parts (Maxim, 2026-10-14): a drag on the screen
    /// (the cyberspace map, lists) no longer drags the whole window. Call after <see cref="DeckBezel"/>, so its keys
    /// take their clicks first, and keep the window's own <c>draggable</c> off.
    /// </summary>
    public static void DragByCasing(Rect outer, Rect screen)
    {
        if (Event.current.type != EventType.MouseDown && Event.current.type != EventType.MouseDrag && Event.current.type != EventType.MouseUp)
            return;
        GUI.DragWindow(new Rect(outer.x, outer.y, outer.width, screen.y - outer.y));
        GUI.DragWindow(new Rect(outer.x, screen.y, screen.x - outer.x, screen.height));
        GUI.DragWindow(new Rect(screen.xMax, screen.y, outer.xMax - screen.xMax, screen.height));
    }

    private static readonly float[] SeamOffsets = [6f, -6f];

    private static void DrawCasing(Rect outer, Rect body, Rect screen)
    {
        DrawRails(outer, body);

        // The body: a vertical gradient in bands, panel seams across the top and bottom bands, a lit edge.
        const int bands = 16;
        for (int i = 0; i < bands; i++)
            Widgets.DrawBoxSolid(new Rect(body.x, body.y + body.height * i / bands, body.width, body.height / bands + 1f),
                Color.Lerp(_t.Top, _t.Bottom, i / (float)(bands - 1)));
        Widgets.DrawBoxSolid(new Rect(body.x, body.y, body.width, 3f), new Color(1f, 1f, 1f, 0.08f));
        GUI.color = DeckEdge;
        Widgets.DrawBox(body);
        GUI.color = Color.white;
        Bevel(body.ContractedBy(1f), true, 0.8f);
        foreach (float d in SeamOffsets)
            Widgets.DrawBoxSolid(new Rect(body.x + 80f, d > 0f ? body.y + d : body.yMax + d, body.width - 160f, 1f), new Color(0f, 0f, 0f, 0.35f));

        // The screen sits in a deep recess: a dark frame, a thin accent line.
        var recess = screen.ExpandedBy(6f);
        Widgets.DrawBoxSolid(recess, Hex(0x0b0d10));
        Bevel(recess, false, 1.2f);
        GUI.color = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
        Widgets.DrawBox(screen.ExpandedBy(2f));
        GUI.color = Color.white;
        for (int side = 0; side < 2; side++)
            for (int i = 1; i <= 3; i++)
                Screw(new Vector2(side == 0 ? body.x + DeckSide / 2f : body.xMax - DeckSide / 2f, screen.y + screen.height * i / 4f), 6f);

        DrawBumpers(body);
    }

    /// <summary>The glass over the screen: a soft diagonal sheen (Customization: glass sheen). Call after the content.</summary>
    public static void DeckGlass(Rect screen)
    {
        if (!NetrunMod.Settings.FrameGlass || Event.current.type != EventType.Repaint)
            return;
        // A stepped diagonal: each band is a little shorter and fainter, which reads as a sheen across the glass.
        const int bands = 14;
        for (int i = 0; i < bands; i++)
        {
            float t = i / (float)bands;
            var band = new Rect(screen.x, screen.y + i * 9f, screen.width * (0.55f - 0.035f * i), 9f);
            Widgets.DrawBoxSolid(band, new Color(1f, 1f, 1f, 0.035f * (1f - t)));
        }
        Widgets.DrawBoxSolid(new Rect(screen.x, screen.y, screen.width, 1f), new Color(1f, 1f, 1f, 0.08f));
    }

    private static void DrawRails(Rect outer, Rect body)
    {
        // A rounded metal tube in three tones on two brackets, the middle of each side.
        foreach (bool left in new[] { true, false })
        {
            float tx = left ? outer.x + 3f : outer.xMax - 17f;
            float top = outer.y + outer.height * 0.26f, bottom = outer.y + outer.height * 0.74f;
            var tube = new Rect(tx, top, 14f, bottom - top);
            if (TexRail != null)
            {
                // The chrome tube, tinted towards the theme's tube colour.
                GUI.color = Color.Lerp(Color.white, _t.TubeLight, 0.35f);
                ThreeSliceV(tube, TexRail, 12f);
                GUI.color = Color.white;
            }
            else
            {
                Widgets.DrawBoxSolid(tube, _t.Tube);
                Widgets.DrawBoxSolid(new Rect(tube.x + 2f, tube.y, 4f, tube.height), _t.TubeLight);
                Widgets.DrawBoxSolid(new Rect(tube.x + 3f, tube.y, 1f, tube.height), new Color(1f, 1f, 1f, 0.28f));
                Widgets.DrawBoxSolid(new Rect(tube.xMax - 3f, tube.y, 3f, tube.height), _t.TubeDark);
            }
            for (float gy = tube.center.y - 40f; gy < tube.center.y + 40f; gy += 5f)
                Widgets.DrawBoxSolid(new Rect(tube.x, gy, tube.width, 2f), new Color(0f, 0f, 0f, 0.35f));
            foreach (float by in new[] { top - 8f, bottom - 12f })
            {
                var bracket = new Rect(left ? tube.x - 1f : body.xMax - 4f, by, (left ? body.x - tube.x : tube.xMax - body.xMax) + 5f, 20f);
                if (TexBracket != null)
                {
                    GUI.color = Color.Lerp(Color.white, DeckEdge, 0.3f);
                    GUI.DrawTexture(bracket, TexBracket);
                    GUI.color = Color.white;
                }
                else
                {
                    Widgets.DrawBoxSolid(bracket, DeckEdge);
                    Bevel(bracket, true);
                    Screw(new Vector2(left ? body.x - 3f : body.xMax + 3f, bracket.center.y), 6f);
                }
            }
        }
    }

    private static void DrawBumpers(Rect body)
    {
        foreach ((float bx, float by, int sx, int sy) in new[] { (body.x, body.y, 1, 1), (body.xMax, body.y, -1, 1), (body.x, body.yMax, 1, -1), (body.xMax, body.yMax, -1, -1) })
        {
            float thick = sy > 0 ? DeckTop : DeckBottom;
            var b = new Rect(sx > 0 ? bx : bx - Bump, sy > 0 ? by : by - Bump, Bump, Bump);
            var h = new Rect(b.x, sy > 0 ? b.y : b.yMax - thick, Bump, thick);
            var v = new Rect(sx > 0 ? b.x : b.xMax - DeckSide, b.y, DeckSide, Bump);
            Widgets.DrawBoxSolid(new Rect(h.x + sx * 2f, h.y + 2f, h.width, h.height), new Color(0f, 0f, 0f, 0.25f));
            Texture2D bumper = TexBumpers[(sy > 0 ? 0 : 2) + (sx > 0 ? 0 : 1)];
            if (bumper != null)
            {
                // The textured corner (its own bolt), then the side strip in the same rubber.
                Widgets.DrawBoxSolid(v, DeckRubber);
                Bevel(v, true, 0.6f);
                GUI.DrawTexture(h, bumper);
                continue;
            }
            foreach (Rect part in new[] { h, v })
            {
                Widgets.DrawBoxSolid(part, DeckRubber);
                GUI.BeginClip(part);
                for (float d = -part.height; d < part.width; d += 7f)
                    Widgets.DrawLine(new Vector2(d, part.height), new Vector2(d + part.height, 0f), new Color(1f, 1f, 1f, 0.06f), 2f);
                GUI.EndClip();
            }
            Bevel(h, true, 0.6f);
            Bevel(v, true, 0.6f);
            Screw(new Vector2(sx > 0 ? bx + DeckSide / 2f : bx - DeckSide / 2f, sy > 0 ? by + 14f : by - 14f), 10f);
        }
    }

    /// <summary>A hardware key that sinks while pressed; true when clicked.</summary>
    private static bool DeckKey(Rect key, Color face, string tip, bool active, System.Action<Rect, Color> glyph)
    {
        bool over = active && Mouse.IsOver(key);
        bool down = over && _pressed;
        if (Event.current.type != EventType.Repaint)
        {
            if (active && Widgets.ButtonInvisible(key, doMouseoverSound: false))
            {
                NetrunSound.Click();
                return true;
            }
            return false;
        }
        var faceRect = down ? new Rect(key.x, key.y + 1f, key.width, key.height - 1f) : key;
        Color f = active ? face : Color.Lerp(face, _t.Bottom, 0.5f);
        bool hot = face == DeckOrange || face.r > 0.5f && face.g < 0.3f;
        Texture2D tex = hot ? TexKeyHot : TexKey;
        if (tex != null)
        {
            // Orange keys use the hot capsule as drawn, red ones tint it; the dark capsule takes the theme's key colour.
            Color tint = face == DeckOrange ? Color.white
                : hot ? new Color(1f, 0.42f, 0.38f)
                : new Color(Mathf.Clamp01(f.r / FieldKey.r), Mathf.Clamp01(f.g / FieldKey.g), Mathf.Clamp01(f.b / FieldKey.b));
            if (!active)
                tint = new Color(tint.r * 0.6f, tint.g * 0.6f, tint.b * 0.6f);
            if (down)
                tint = new Color(tint.r * 0.8f, tint.g * 0.8f, tint.b * 0.8f);
            GUI.color = tint;
            GUI.DrawTexture(faceRect, tex);
            if (over && !down)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.16f);
                GUI.DrawTexture(faceRect, tex);
            }
            GUI.color = Color.white;
        }
        else
        {
            Widgets.DrawBoxSolid(faceRect, over ? Color.Lerp(f, Color.white, 0.12f) : f);
            Bevel(faceRect, !down);
            Widgets.DrawBoxSolid(new Rect(key.x, key.yMax - 3f, key.width, 3f), new Color(0f, 0f, 0f, down ? 0.2f : 0.5f));
        }
        glyph(faceRect, active ? (over ? Color.white : new Color(1f, 1f, 1f, 0.85f)) : new Color(1f, 1f, 1f, 0.3f));
        if (tip != null)
            Tip(key, tip);
        if (active && Widgets.ButtonInvisible(key, doMouseoverSound: false))
        {
            NetrunSound.Click();
            return true;
        }
        return false;
    }

    private static void GlyphText(Rect r, Color c, string text, GameFont font = GameFont.Medium)
    {
        // Centred by the glyph's own size: a label's MiddleCenter leaves symbols off by a few pixels.
        Text.Font = font;
        Vector2 size = Text.CalcSize(text);
        Widgets.Label(new Rect(Mathf.Round(r.center.x - size.x / 2f), Mathf.Round(r.center.y - size.y / 2f), size.x + 2f, size.y), text.Colorize(c));
        Text.Font = GameFont.Small;
    }

    private static void GlyphTex(Rect r, Color c, Texture2D tex)
    {
        if (tex == null)
            return;
        float s = Mathf.Min(r.width, r.height) - 6f;
        // Bright on the dark key: full white (or the given colour at full strength) over a faint accent glow.
        if (c.a > 0.5f)
        {
            Glow(new Rect(r.center.x - s / 4f, r.center.y - s / 4f, s / 2f, s / 2f), Accent, 0.3f, 6f);
            c = new Color(c.r, c.g, c.b, 1f);
        }
        GUI.color = c;
        GUI.DrawTexture(new Rect(r.center.x - s / 2f, r.center.y - s / 2f, s, s), tex);
        GUI.color = Color.white;
    }

    /// <summary>The power symbol: a ring open at the top with a bar through the gap.</summary>
    private static void GlyphPower(Rect r, Color c, Color face)
    {
        float s = Mathf.Min(r.width, r.height) - 10f;
        var ring = new Rect(r.center.x - s / 2f, r.center.y - s / 2f + 1f, s, s);
        GUI.color = c;
        GUI.DrawTexture(ring, Disc);
        GUI.color = face;
        GUI.DrawTexture(ring.ContractedBy(2.5f), Disc);
        Widgets.DrawBoxSolid(new Rect(r.center.x - 3f, ring.y - 1f, 6f, s * 0.45f), face);
        Widgets.DrawBoxSolid(new Rect(r.center.x - 1f, ring.y - 2f, 2f, s * 0.55f), c);
        GUI.color = Color.white;
    }

    private static void DrawDeckTop(Rect body, Rect outer, string plate, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu, bool paint)
    {
        NetrunSettings set = NetrunMod.Settings;
        float x = body.x + Bump + 10f;

        // Hardware keys: CLOSE (red), MENU, ACHIEV, NET LOCK (isolation, red while the colony is cut off).
        const float kw = 38f, kh = 26f, gap = 16f;
        var tray = new Rect(x, body.y + 8f, 4f * kw + 3f * gap + 16f, kh + 8f);
        Recess(tray, Hex(0x111418));
        string[] names = ["CLOSE", "MENU", "ACHIEV", "NET LOCK"];
        NodeDefense defense = Current.Game != null ? NodeDefense.Get() : null;
        bool isolated = defense?.Isolated == true;
        for (int i = 0; i < 4; i++)
        {
            var key = new Rect(tray.x + 8f + i * (kw + gap), tray.y + 4f, kw, kh);
            Color face = i == 0 || i == 3 && isolated ? Hex(0x9a2b25) : _t.KeyFace;
            AcceptanceReport lockReport = defense?.CanToggleIsolation() ?? false;
            bool active = i switch { 0 => onKey != null, 1 or 2 => onKey != null && menu, _ => defense != null && lockReport.Accepted };
            string tip = !Mouse.IsOver(key) ? null : i == 3
                ? (string)(isolated ? "NR_DeckKey_IsolateOff" : "NR_DeckKey_IsolateOn").Translate() + "\n\n" + "NR_IsolationGizmoDesc".Translate()
                  + (lockReport.Reason.NullOrEmpty() ? string.Empty : "\n\n" + lockReport.Reason)
                : !active && i > 0 ? (string)"NR_DeckKey_TerminalOnly".Translate() : (string)("NR_DeckKey_" + (KeyClose + i)).Translate();
            System.Action<Rect, Color> glyph = i switch
            {
                0 => (r, c) => GlyphPower(r, c, face),
                1 => (r, c) => GlyphTex(r, c, NetrunTex.MenuMain),
                2 => (r, c) => GlyphText(r, c, "★"),
                _ => (r, c) => GlyphTex(r, isolated ? Color.white : c, NetrunTex.NetworkLock),
            };
            if (i == 3 && isolated && anim)
                Glow(key, Bad, 0.35f + 0.25f * Mathf.Abs(Mathf.Sin(time * 2f)), 6f);
            if (DeckKey(key, face, tip, active, glyph))
            {
                if (i == 3)
                    defense?.ToggleIsolation();
                else
                    onKey?.Invoke(KeyClose + i);
            }
            Stencil(new Rect(key.x - 10f, tray.yMax + 2f, key.width + 20f, 14f), names[i]);
        }
        x = tray.xMax + 18f;
        if (!paint)
            return;

        // From the right: the RF signal meter, the lights; the plate and the tuner fill the middle.
        float right = body.xMax - Bump - 10f;

        // RF SIGNAL: six bars that grow and flicker online, dark offline; the last one burns red from Target up.
        const int bars = 6;
        const float bw = 6f, bg = 2f;
        var meter = new Rect(right - bars * (bw + bg), body.y + 12f, bars * (bw + bg), 26f);
        bool hot = level >= HeatLevel.Target;
        for (int i = 0; i < bars; i++)
        {
            float h = 8f + i * 3.4f;
            var bar = new Rect(meter.x + i * (bw + bg), meter.yMax - h, bw, h);
            bool lit = online && (i < Mathf.RoundToInt(_rf) || (i == bars - 1 && hot));
            Color c = i == bars - 1 && hot ? Bad : Ok;
            if (lit && anim)
                Glow(bar, c, 0.35f, 4f);
            Widgets.DrawBoxSolid(bar, lit ? c : new Color(c.r, c.g, c.b, 0.15f));
        }
        Stencil(new Rect(meter.x - 20f, meter.yMax + 2f, meter.width + 40f, 14f), "RF SIGNAL");
        Tip(meter.ExpandedBy(4f), (online ? "NR_DeckRfOn" : "NR_DeckRfOff").Translate());
        right = meter.x - 18f;

        // Three lights: net, Netsick, pod (the pod lamp is always green, whatever the accent).
        if (set.FrameLights)
        {
            bool sick = Current.Game != null && Cached("deck.sick", () => PawnsFinder.AllMaps_FreeColonists.Any(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick)), 1f);
            bool pod = Current.Game != null && Cached("deck.pod", () => Find.Maps.Any(m => m.GetComponent<USH_HE.MapComponent_CyberpodManager>()?.Pods.Any(pp => pp.ContainedThing != null) == true), 1f);
            (bool on, Color c, bool blink, string tip, string label)[] lights =
            [
                (online, Ok, false, "NR_DeckLightNet".Translate(), "NET"),
                (sick, DeckOrange, true, "NR_DeckLightSick".Translate(), "SICK"),
                (pod, PodGreen, false, "NR_DeckLightPod".Translate(), "POD"),
            ];
            var strip = new Rect(right - lights.Length * 34f, body.y + 12f, lights.Length * 34f, 26f);
            for (int i = 0; i < lights.Length; i++)
            {
                var l = new Rect(strip.x + 9f + i * 34f, strip.center.y - 8f, 16f, 16f);
                float a = !lights[i].on ? 0.15f : lights[i].blink && anim ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * 3f)) : 1f;
                if (lights[i].on)
                    Glow(l.ContractedBy(3f), lights[i].c, a * 0.8f, 6f);
                Texture2D led = !lights[i].on ? TexLedOff : i == 1 ? TexLedOrange : TexLedGreen;
                if (led != null)
                {
                    GUI.DrawTexture(l, TexLedOff ?? led);
                    if (lights[i].on)
                    {
                        GUI.color = new Color(1f, 1f, 1f, a);
                        GUI.DrawTexture(l, led);
                        GUI.color = Color.white;
                    }
                }
                else
                {
                    Widgets.DrawBoxSolid(l, new Color(lights[i].c.r, lights[i].c.g, lights[i].c.b, Mathf.Max(0.18f, a)));
                }
                Stencil(new Rect(l.center.x - 17f, strip.yMax + 2f, 34f, 14f), lights[i].label);
                Tip(l.ExpandedBy(8f), lights[i].tip);
            }
            right = strip.x - 16f;
        }

        // The maker's plate, centred in what is left; the tuner only where it fits.
        Text.Font = GameFont.Small;
        const string serial = "SER. NR-07  ·  GUILD ISSUE";
        Text.Font = GameFont.Tiny;
        float pw = plate.NullOrEmpty() ? 0f : Mathf.Max(Text.CalcSize(plate).x, Text.CalcSize(serial).x) + 36f;
        Text.Font = GameFont.Small;
        float room = right - x;
        float tunerW = Mathf.Min(160f, room - pw - 20f);
        if (tunerW >= 90f)
        {
            var tuner = new Rect(x, body.y + 12f, tunerW, 26f);
            Recess(tuner, Hex(0x07080a));
            for (int i = 0; i <= 24; i++)
            {
                float tx = tuner.x + 5f + (tuner.width - 10f) * i / 24f;
                Widgets.DrawBoxSolid(new Rect(tx, tuner.yMax - (i % 4 == 0 ? 11f : 6f) - 2f, 1f, i % 4 == 0 ? 11f : 6f), new Color(Ok.r, Ok.g, Ok.b, i % 4 == 0 ? 0.6f : 0.3f));
            }
            float mark = tuner.x + 5f + (tuner.width - 10f) * _tuner;
            Glow(new Rect(mark - 1f, tuner.y + 2f, 2f, tuner.height - 4f), DeckOrange, 0.5f, 4f);
            Widgets.DrawBoxSolid(new Rect(mark - 1f, tuner.y + 2f, 2f, tuner.height - 4f), DeckOrange);
            Stencil(new Rect(tuner.x, tuner.yMax + 2f, tuner.width, 14f), "TUNE  88.2 - 107.9");
            Tip(tuner, "NR_DeckTuner".Translate());
            x = tuner.xMax + 20f;
        }
        if (pw > 0f && right - x >= pw)
        {
            var pr = new Rect(x + (right - x - pw) / 2f, body.y + 9f, pw, 40f);
            Recess(pr, Hex(0x15191e));
            Widgets.DrawBoxSolid(new Rect(pr.x + 14f, pr.y + 21f, pr.width - 28f, 1f), new Color(1f, 1f, 1f, 0.08f));
            foreach (float sx in new[] { pr.x + 7f, pr.xMax - 7f })
                foreach (float sy in new[] { pr.y + 7f, pr.yMax - 7f })
                    Screw(new Vector2(sx, sy), 4f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(pr.x, pr.y + 3f, pr.width, 18f), plate.Colorize(new Color(1f, 1f, 1f, 0.82f)));
            Widgets.Label(new Rect(pr.x, pr.y + 21f, pr.width, 16f), serial.Colorize(new Color(_t.Stencil.r, _t.Stencil.g, _t.Stencil.b, 0.5f)));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }

    private static void DrawDeckBottom(Rect body, HeatLevel level, float heat, bool online, System.Action<int> onKey, bool paint)
    {
        float by0 = body.yMax - DeckBottom;
        float left = body.x + Bump + 10f;

        // The status LCD: three lines, refreshed once a second.
        if (NetrunMod.Settings.FrameLcd)
        {
            var lcd = new Rect(left, by0 + 6f, 196f, 46f);
            left = lcd.xMax + 20f;
            if (paint)
                DrawLcd(lcd, heat, level, online);
        }

        // From the right, at least 120 px clear of the bumper: the NET LINK switch, then the Heat scale.
        float right = body.xMax - Bump - 120f;
        var sw = new Rect(right - 58f, by0 + 14f, 58f, 30f);
        const float segW = 20f, segH = 30f, segGap = 3f;
        float scaleW = 5f * segW + 4f * segGap;
        var scale = new Rect(sw.x - 40f - scaleW, by0 + 14f, scaleW, segH);
        if (paint)
        {
            DrawNetSwitch(sw, online);
            DrawHeatScale(scale, level, heat, segW, segH, segGap);
        }
        DrawBottomKeys(body, by0, left, scale, onKey, paint);
    }

    private static void DrawLcd(Rect lcd, float heat, HeatLevel level, bool online, bool recess = true)
    {
        {
            if (recess)
                Recess(lcd, Hex(0x06120a));
            string[] lines = Cached("deck.lcd", () => LcdLines(heat, level, online), 1f);
            Text.Font = GameFont.Tiny;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = new Rect(lcd.x + 8f, lcd.y + 1f + i * 14.5f, lcd.width - 12f, 16f);
                Widgets.Label(new Rect(line.x + 0.6f, line.y, line.width, line.height), lines[i].Colorize(new Color(LcdGreen.r, LcdGreen.g, LcdGreen.b, 0.25f)));
                Widgets.Label(line, lines[i].Colorize(LcdGreen));
            }
            Text.Font = GameFont.Small;
            // Faint LCD rows.
            for (float ly = lcd.y + 2f; ly < lcd.yMax - 2f; ly += 3f)
                Widgets.DrawBoxSolid(new Rect(lcd.x + 2f, ly, lcd.width - 4f, 1f), new Color(0f, 0f, 0f, 0.18f));
            if (Mouse.IsOver(lcd))
                Tip(lcd, "NR_DeckLcd".Translate());
        }
    }

    private static void DrawNetSwitch(Rect sw, bool online)
    {
        Recess(sw, Hex(0x07080a));
        var lever = new Rect(online ? sw.xMax - 30f : sw.x + 4f, sw.y + 4f, 26f, sw.height - 8f);
        if (online)
            Glow(lever, PodGreen, 0.45f, 6f);
        Widgets.DrawBoxSolid(lever, online ? PodGreen : Hex(0x2c3238));
        Bevel(lever, true);
        Widgets.DrawBoxSolid(new Rect(lever.x + 3f, lever.y + 2f, lever.width - 6f, 2f), new Color(1f, 1f, 1f, online ? 0.45f : 0.15f));
        if (Mouse.IsOver(sw))
            Tip(sw, (online ? "NR_DeckNetLinkOn" : "NR_DeckNetLinkOff").Translate());
    }

    private static void DrawHeatScale(Rect scale, HeatLevel level, float heat, float segW, float segH, float segGap)
    {
        // HEAT LEVEL: five segments in the level colours, lit up to the current level.
        for (int i = 0; i < 5; i++)
        {
            var seg = new Rect(scale.x + i * (segW + segGap), scale.y, segW, segH);
            Color c = HeatSegments[i];
            bool lit = i <= (int)level;
            if (lit)
                Glow(seg, c, 0.45f, 6f);
            Widgets.DrawBoxSolid(seg, lit ? c : new Color(c.r, c.g, c.b, 0.25f));
            Bevel(seg, lit, 0.7f);
        }
        if (Mouse.IsOver(scale.ExpandedBy(4f)))
            Tip(scale.ExpandedBy(4f), "NR_DeckHeatScale".Translate(heat.ToString("0"), level.Label()));
    }

    private static readonly string[] BottomGlyphs = ["◁", "○", "?"];

    private static void DrawBottomKeys(Rect body, float by0, float left, Rect scale, System.Action<int> onKey, bool paint)
    {
        // Three keys in a recessed tray: back, core, Cyberpedia; centred, or left of the Heat scale when narrow.
        string[] glyphs = BottomGlyphs;
        const float kw = 46f, kh = 26f;
        float trayW = 3f * kw + 2f * 14f + 16f;
        float tx = Mathf.Clamp(body.center.x - trayW / 2f, left, scale.x - 24f - trayW);
        var tray = new Rect(tx, by0 + 12f, trayW, kh + 8f);
        if (paint)
            Recess(tray, Hex(0x111418));
        for (int i = 0; i < 3; i++)
        {
            var key = new Rect(tray.x + 8f + i * (kw + 14f), tray.y + 4f, kw, kh);
            Color face = i == 1 ? DeckOrange : _t.KeyFace;
            string g = glyphs[i];
            bool core = i == 1;
            if (DeckKey(key, face, onKey != null && Mouse.IsOver(key) ? (string)("NR_DeckKey_" + i).Translate() : null, onKey != null,
                    (r, c) => GlyphText(r, core ? Hex(0x1b1e22) : c, g)))
                onKey?.Invoke(i);
        }
    }

    /// <summary>CYBERDATA 07 / HEAT 58 ▲ TRACKED / D2 14:20 NET ON.</summary>
    private static string[] LcdLines(float heat, HeatLevel level, bool online)
    {
        Map map = Find.CurrentMap;
        int data = map == null ? 0 : ColonyGoods.CountCached(map, NetrunDefOf.USH_Cyberdata);
        string arrow = _heatTrend > 0.5f ? "▲" : _heatTrend < -0.5f ? "▼" : "-";
        string day = "-", clock = "--:--";
        if (Current.Game != null)
        {
            day = (GenDate.DaysPassed + 1).ToString();
            float hour = map != null ? GenLocalDate.HourFloat(map) : GenDate.HourOfDay(Find.TickManager.TicksAbs, 0f);
            clock = $"{(int)hour:00}:{(int)((hour - (int)hour) * 60f):00}";
        }
        return
        [
            $"CYBERDATA {data:00}",
            $"HEAT {heat:0} {arrow} {level.ToString().ToUpperInvariant()}",
            $"D{day} {clock} NET {(online ? "ON" : "OFF")}",
        ];
    }
}
