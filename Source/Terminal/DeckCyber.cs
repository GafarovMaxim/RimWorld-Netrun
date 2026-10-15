using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The NR-1 cyberdeck, the default frame (docs/mockups/46b-classic-cyberdeck.png, Maxim's pick 2026-10-15): matte black
/// plastic with chamfered corners, carbon inserts on the rails, grip ridges, chiclet keys with glowing legends, a TUNE
/// thumb wheel, light-pipe lamps with ALERT, RF on an OLED, an amber LCD, a light-pipe Heat tray and a NET LINK rocker.
/// A light line runs round the screen: cyan, red and pulsing from Target. Textures from tools/gen_deck_classic.py; the
/// layout numbers mirror its cyber() mockup: controls centred on one line, captions on another.
/// </summary>
public static partial class NetrunUI
{
    private const float CyberOut = 4f;
    private const float CyberInL = 60f, CyberInT = 96f, CyberInR = 60f, CyberInB = 100f;
    private const float CyberSlice = 170f;
    public static readonly Vector2 CyberExtra = new(2f * CyberOut + CyberInL + CyberInR, 2f * CyberOut + CyberInT + CyberInB);

    private static readonly Color CyberCyan = Hex(0x46dce6), CyberAmber = Hex(0xffb03c), CyberRed = Hex(0xe63c32), CyberGreen = Hex(0x5ae678),
        CyberCaption = new(0.59f, 0.61f, 0.65f, 0.95f);

    private static class CyberTex
    {
        private static Texture2D Get(string name) => ContentFinder<Texture2D>.Get("UI/Netrun/Frame/Cyber/" + name, reportFailure: false);

        public static readonly Texture2D Body = Get("Body"), Carbon = Get("Carbon"), Ridges = Get("Ridges"), Key = Get("Key"), Tray = Get("Tray"),
            Oled = Get("Oled"), Wheel = Get("Wheel"), RockerOff = Get("Rocker_0"), RockerOn = Get("Rocker_1"), Vent = Get("Vent"), Logo = Get("Logo");

        public static bool Ready => Body != null;
    }

    private static void CyberCaptionAt(float cx, float y, string text) => Stencil(new Rect(cx - 40f, y, 80f, 14f), text, TextAnchor.UpperCenter, CyberCaption);

    /// <summary>A chiclet key centred on (cx, cy): the legend glows in its colour, brighter under the mouse; it sinks while pressed.</summary>
    private static bool CyberKey(float cx, float cy, string tip, bool active, System.Action<Rect, Color> glyph, Color ink)
    {
        var key = new Rect(Mathf.Round(cx - 20f), Mathf.Round(cy - 14f), 40f, 28f);
        if (Event.current.type == EventType.Repaint)
        {
            bool over = active && Mouse.IsOver(key);
            bool down = over && _pressed;
            float k = !active ? 0.7f : down ? 0.85f : over ? 1.12f : 1f;
            var face = new Rect(key.x, key.y + (down ? 1f : 0f), key.width, key.height);
            Sprite(CyberTex.Key, new Vector2(face.x - 2f, face.y - 1f), new Color(k, k, k));
            if (active)
                Glow(new Rect(face.center.x - 6f, face.center.y - 3f, 12f, 6f), ink, over ? 0.45f : 0.3f, 4f);
            glyph?.Invoke(face, active ? ink : new Color(ink.r, ink.g, ink.b, 0.35f));
        }
        return CasingHit(key, active, tip);
    }

    /// <summary>A light pipe in its socket: a lit bar with a glow, or a dim one.</summary>
    private static void CyberPipe(Rect r, Color c, float a)
    {
        NineSlice(r.ExpandedBy(2f), CyberTex.Tray, 6f, 6f, 6f, 6f);
        Widgets.DrawBoxSolid(r, Color.Lerp(new Color(c.r * 0.22f + 0.03f, c.g * 0.22f + 0.03f, c.b * 0.22f + 0.04f), c, a));
        if (a > 0f)
            Glow(r, c, 0.6f * a, 4f);
    }

    private static Rect CyberBezel(Rect outer, float heat, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu)
    {
        var box = new Rect(outer.x + CyberOut, outer.y + CyberOut, outer.width - 2f * CyberOut, outer.height - 2f * CyberOut);
        var screen = new Rect(box.x + CyberInL, box.y + CyberInT, box.width - CyberInL - CyberInR, box.height - CyberInT - CyberInB);
        bool paint = Event.current.type == EventType.Repaint;
        if (paint)
            DrawCyberCasing(box, screen, level, anim, time);
        DrawCyberTop(box, level, online, anim, time, onKey, menu, paint);
        DrawCyberBottom(box, heat, level, online, anim, time, menu ? onKey : null, paint);
        return screen;
    }

    private static void DrawCyberCasing(Rect box, Rect screen, HeatLevel level, bool anim, float time)
    {
        NineSlice(box, CyberTex.Body, CyberSlice, CyberSlice, CyberSlice, CyberSlice);

        // Ridges along the top and bottom edges and carbon down the rails, cropped to the room (never stretched).
        float rw = Mathf.Min(CyberTex.Ridges.width, box.width - 240f);
        if (rw > 0f)
        {
            var uv = new Rect(0f, 0f, rw / CyberTex.Ridges.width, 1f);
            GUI.DrawTextureWithTexCoords(new Rect(box.x + 120f, box.y + 6f, rw, CyberTex.Ridges.height), CyberTex.Ridges, uv);
            GUI.DrawTextureWithTexCoords(new Rect(box.x + 120f, box.yMax - 12f, rw, CyberTex.Ridges.height), CyberTex.Ridges, uv);
        }
        float ch = Mathf.Min(CyberTex.Carbon.height, screen.height - 80f);
        if (ch >= 60f)
        {
            var uv = new Rect(0f, 1f - ch / CyberTex.Carbon.height, 1f, ch / CyberTex.Carbon.height);
            GUI.DrawTextureWithTexCoords(new Rect(box.x + 12f, screen.y + 40f, CyberTex.Carbon.width, ch), CyberTex.Carbon, uv);
            GUI.DrawTextureWithTexCoords(new Rect(box.xMax - 12f - CyberTex.Carbon.width, screen.y + 40f, CyberTex.Carbon.width, ch), CyberTex.Carbon, uv);
            if (ch >= 220f)
                Sprite(CyberTex.Vent, new Vector2(box.xMax - 44f, screen.y + 140f));
        }

        // The light line round the screen: cyan; from Target red, pulsing, and flashing in the red zone.
        Color c = CyberCyan;
        float a = 1f;
        if (level >= HeatLevel.Target)
        {
            c = CyberRed;
            if (anim)
                a = level >= HeatLevel.RedZone ? (time * 1.6f % 1f < 0.5f ? 1f : 0.25f) : 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * 2f));
        }
        Rect line = screen.ExpandedBy(7f);
        for (int w = 0; w < 3; w++)
        {
            Rect r = line.ExpandedBy(w);
            float k = a * (w == 0 ? 1f : w == 1 ? 0.3f : 0.12f);
            var col = new Color(c.r, c.g, c.b, k);
            Widgets.DrawBoxSolid(new Rect(r.x, r.y, r.width, 1f), col);
            Widgets.DrawBoxSolid(new Rect(r.x, r.yMax - 1f, r.width, 1f), col);
            Widgets.DrawBoxSolid(new Rect(r.x, r.y + 1f, 1f, r.height - 2f), col);
            Widgets.DrawBoxSolid(new Rect(r.xMax - 1f, r.y + 1f, 1f, r.height - 2f), col);
        }
    }

    private static void DrawCyberTop(Rect box, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu, bool paint)
    {
        float cy = box.y + 36f, ly = box.y + 59f;
        float x = box.x + 320f;
        if (paint)
        {
            Sprite(CyberTex.Logo, new Vector2(box.x + 52f, cy - 20f));
            NineSlice(new Rect(x - 27f, cy - 20f, 3f * 52f + 54f, 40f), CyberTex.Tray, 6f, 6f, 6f, 6f);
        }

        // CLOSE (red legend), MENU, ACHIEV, NET LOCK (amber, red while the colony is cut off).
        string[] names = ["CLOSE", "MENU", "ACHIEV", "NET LOCK"];
        NodeDefense defense = Current.Game != null ? NodeDefense.Get() : null;
        bool isolated = defense?.Isolated == true;
        AcceptanceReport lockReport = defense?.CanToggleIsolation() ?? false;
        for (int i = 0; i < 4; i++)
        {
            float kx = x + i * 52f;
            var hit = new Rect(kx - 20f, cy - 14f, 40f, 28f);
            bool active = i switch { 0 => onKey != null, 1 or 2 => onKey != null && menu, _ => defense != null && lockReport.Accepted };
            string tip = !Mouse.IsOver(hit) ? null : i == 3
                ? (string)(isolated ? "NR_DeckKey_IsolateOff" : "NR_DeckKey_IsolateOn").Translate() + "\n\n" + "NR_IsolationGizmoDesc".Translate()
                  + (lockReport.Reason.NullOrEmpty() ? string.Empty : "\n\n" + lockReport.Reason)
                : !active && i > 0 ? (string)"NR_DeckKey_TerminalOnly".Translate() : (string)("NR_DeckKey_" + (KeyClose + i)).Translate();
            Color ink = i switch { 0 => CyberRed, 3 => isolated ? CyberRed : CyberAmber, _ => CyberCyan };
            System.Action<Rect, Color> glyph = i switch
            {
                0 => (r, c) => GlyphText(r, c, "×", GameFont.Small),
                1 => (r, c) => GlyphTex(r.ContractedBy(5f), c, NetrunTex.MenuMain),
                2 => (r, c) => GlyphText(r, c, "★", GameFont.Tiny),
                _ => (r, c) => GlyphTex(r.ContractedBy(5f), c, NetrunTex.NetworkLock),
            };
            if (paint && i == 3 && isolated && anim)
                Glow(hit, CyberRed, 0.3f + 0.25f * Mathf.Abs(Mathf.Sin(time * 2f)), 5f);
            if (CyberKey(kx, cy, tip, active, glyph, ink))
            {
                if (i == 3)
                    defense?.ToggleIsolation();
                else
                    onKey?.Invoke(KeyClose + i);
            }
            if (paint)
                CyberCaptionAt(kx, ly, names[i]);
        }
        if (!paint)
            return;

        // TUNE: the thumb wheel, a cyan tick beside it shows where the tuner sits.
        float tx = x + 3f * 52f + 62f;
        var wheel = new Rect(tx - 11f, cy - 18f, CyberTex.Wheel.width, CyberTex.Wheel.height);
        GUI.DrawTexture(wheel, CyberTex.Wheel);
        float tick = wheel.yMax - 4f - (wheel.height - 8f) * _tuner;
        Widgets.DrawBoxSolid(new Rect(wheel.xMax + 3f, tick - 1f, 4f, 2f), CyberCyan);
        CyberCaptionAt(tx, ly, "TUNE");
        Tip(wheel.ExpandedBy(4f), "NR_DeckTuner".Translate());
        float used = wheel.xMax + 24f;

        // RF on an OLED, right; from Target the bars jitter and the last one burns red.
        float rx = box.xMax - 210f;
        bool hot = level >= HeatLevel.Target;
        if (rx >= used)
        {
            var oled = new Rect(rx, cy - 20f, 150f, 40f);
            NineSlice(oled, CyberTex.Oled, 6f, 6f, 6f, 6f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(oled.x + 8f, oled.y + 2f, 80f, 16f), "RF 2.4G".Colorize(CyberCyan));
            Text.Font = GameFont.Small;
            int jitter = hot && anim ? Mathf.FloorToInt(Noise(time, 9f) * 3f) - 1 : 0;
            int lit = online ? Mathf.Clamp(Mathf.RoundToInt(_rf / 6f * 12f) + jitter, 1, 12) : 0;
            for (int i = 0; i < 12; i++)
            {
                float h = 4f + i * 1.4f;
                Color c = hot && i == 11 ? CyberRed : CyberCyan;
                bool on = i < lit || hot && i == 11 && online;
                Widgets.DrawBoxSolid(new Rect(oled.x + 10f + i * 11f, oled.y + 33f - h, 6f, h), on ? c : new Color(c.r, c.g, c.b, 0.16f));
            }
            CyberCaptionAt(oled.center.x, ly, "LINK");
            Tip(oled, (online ? "NR_DeckRfOn" : "NR_DeckRfOff").Translate());
        }
        else
            rx = box.xMax - 40f;

        // The lamps: ALERT (from Target), NET, SICK, POD, as light pipes, when they fit between TUNE and RF.
        // The group spans lx - 16 .. lx + 130; it slides left into any room between TUNE and RF (it used to vanish while
        // it still fitted).
        float lx = Mathf.Min(rx - 150f, Mathf.Round((used + rx - 4f) / 2f - 57f));
        if (!NetrunMod.Settings.FrameLights || lx - 16f < used || lx + 130f > rx - 4f)
            return;
        bool sick = Current.Game != null && Cached("deck.sick", () => PawnsFinder.AllMaps_FreeColonists.Any(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick)), 1f);
        bool pod = Current.Game != null && Cached("deck.pod", () => Find.Maps.Any(m => m.GetComponent<USH_HE.MapComponent_CyberpodManager>()?.Pods.Any(pp => pp.ContainedThing != null) == true), 1f);
        float phase = time * 1.4f % 1f;
        float alert = !hot ? 0f : !anim ? 1f
            : level >= HeatLevel.RedZone ? (phase < 0.12f || phase is > 0.24f and < 0.36f ? 1f : 0.08f)
            : 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(time * 2.2f));
        float sickA = !sick ? 0f : anim ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * 3f)) : 1f;
        (float a, Color c, string tip, string label)[] lights =
        [
            (alert, CyberRed, "NR_DeckBeacon".Translate(), "ALERT"),
            (online ? 1f : 0f, CyberGreen, "NR_DeckLightNet".Translate(), "NET"),
            (sickA, CyberAmber, "NR_DeckLightSick".Translate(), "SICK"),
            (pod ? 1f : 0f, CyberCyan, "NR_DeckLightPod".Translate(), "POD"),
        ];
        for (int i = 0; i < lights.Length; i++)
        {
            float c0 = lx + i * 38f;
            var pipe = new Rect(c0 - 7f, cy - 3f, 14f, 6f);
            CyberPipe(pipe, lights[i].c, lights[i].a);
            CyberCaptionAt(c0, ly - 5f, lights[i].label);
            Tip(new Rect(c0 - 16f, cy - 12f, 32f, 24f), lights[i].tip);
        }
    }

    private static void DrawCyberBottom(Rect box, float heat, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool paint)
    {
        float yb = box.yMax - CyberInB;
        float cb = yb + 46f, lb = yb + 69f;

        if (paint && NetrunMod.Settings.FrameLcd)
        {
            var lcd = new Rect(box.x + 52f, yb + 26f, 260f, 54f);
            NineSlice(lcd, CyberTex.Oled, 6f, 6f, 6f, 6f);
            string[] lines = Cached("deck.lcd", () => LcdLines(heat, level, online), 1f);
            Text.Font = GameFont.Tiny;
            for (int i = 0; i < lines.Length; i++)
                Widgets.Label(new Rect(lcd.x + 14f, lcd.center.y - 8f + (i - (lines.Length - 1) / 2f) * 15f, lcd.width - 22f, 16f), lines[i].Colorize(CyberAmber));
            Text.Font = GameFont.Small;
            if (Mouse.IsOver(lcd))
                Tip(lcd, "NR_DeckLcd".Translate());
        }

        // BACK, CORE, HELP.
        string[] glyphs = ["◁", "○", "?"];
        string[] names = ["BACK", "CORE", "HELP"];
        int[] ids = [KeyBack, KeyCore, KeyHelp];
        float kx = box.x + 380f;
        for (int i = 0; i < 3; i++)
        {
            float cx = kx + i * 56f;
            var hit = new Rect(cx - 20f, cb - 14f, 40f, 28f);
            bool active = onKey != null;
            string tip = active && Mouse.IsOver(hit) ? (string)("NR_DeckKey_" + ids[i]).Translate() : null;
            string g = glyphs[i];
            if (CyberKey(cx, cb, tip, active, (r, c) => GlyphText(r, c, g, GameFont.Small), CyberCyan))
                onKey?.Invoke(ids[i]);
            if (paint)
                CyberCaptionAt(cx, lb, names[i]);
        }
        if (!paint)
            return;
        float used = kx + 2f * 56f + 40f;

        // NET LINK: the rocker on the right, its pipe green while the colony is on the net.
        float sx = box.xMax - 150f;
        if (sx >= used)
        {
            var rocker = new Rect(sx, cb - 16f, CyberTex.RockerOn.width, CyberTex.RockerOn.height);
            GUI.DrawTexture(rocker, online ? CyberTex.RockerOn : CyberTex.RockerOff);
            float px = online ? sx + 48f : sx + 14f;
            Widgets.DrawBoxSolid(new Rect(px, cb - 2f, 14f, 4f), online ? CyberGreen : new Color(0.25f, 0.27f, 0.3f));
            if (online)
                Glow(new Rect(px, cb - 2f, 14f, 4f), CyberGreen, 0.5f, 3f);
            CyberCaptionAt(rocker.center.x, lb, "NET LINK");
            if (Mouse.IsOver(rocker))
                Tip(rocker, (online ? "NR_DeckNetLinkOn" : "NR_DeckNetLinkOff").Translate());
        }
        else
            sx = box.xMax - 40f;

        // HEAT LEVEL: five light pipes in a tray, lit up to the level; the top one pulses from Target.
        // The tray narrows its pipes (40 down to 30 px apart) to stay at small scales (Maxim, 2026-10-16).
        float avail = sx - 20f - used;
        if (avail < 5f * 30f + 8f)
            return;
        float step = Mathf.Min(40f, (avail - 8f) / 5f);
        float hx = Mathf.Round(Mathf.Min(box.x + 580f, used + 8f + (avail - 5f * step - 8f) / 2f));
        var tray = new Rect(hx - 8f, cb - 12f, 5f * step + 8f, 24f);
        NineSlice(tray, CyberTex.Tray, 6f, 6f, 6f, 6f);
        for (int i = 0; i < 5; i++)
        {
            bool lit = i <= (int)level;
            float k = !lit ? 0f : i == (int)level && level >= HeatLevel.Target && anim
                ? 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(time * (level >= HeatLevel.RedZone ? 5f : 2.5f))) : 1f;
            var seg = new Rect(hx + i * step, cb - 6f, step - 8f, 12f);
            Widgets.DrawBoxSolid(seg, Color.Lerp(new Color(HeatSegments[i].r * 0.22f + 0.03f, HeatSegments[i].g * 0.22f + 0.03f, HeatSegments[i].b * 0.22f + 0.04f), HeatSegments[i], k));
            if (k > 0f)
                Glow(seg, HeatSegments[i], 0.45f * k, 5f);
        }
        CyberCaptionAt(tray.center.x, lb, "HEAT LEVEL");
        if (Mouse.IsOver(tray.ExpandedBy(4f)))
            Tip(tray.ExpandedBy(4f), "NR_DeckHeatScale".Translate(heat.ToString("0"), level.Label()));
    }
}
