using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The Relic, an old field unit (docs/mockups/48d-relic-column-v3.png, Maxim's pick 2026-10-15): worn cream plastic
/// chipped to rusty metal, the screen in a dark bezel with screws, rubber keys and jewel lamps on a dark top plate, and
/// an instrument column on the right: VOL/SQL knobs, the TUNE gauge under cracked glass, SCAN with the ICE lamp, the
/// TRACE knob turned to the Heat level, JACK and PURGE. At the bottom an orange LCD, stickers, grey square keys,
/// Heat lamps and the NET LINK slide. Textures from tools/gen_deck_relic.py.
/// </summary>
public static partial class NetrunUI
{
    private const float RelicOut = 4f;
    private const float RelicInL = 60f, RelicInT = 96f, RelicInR = 168f, RelicInB = 100f;
    private const float RelicSlice = 170f;
    public static readonly Vector2 RelicExtra = new(2f * RelicOut + RelicInL + RelicInR, 2f * RelicOut + RelicInT + RelicInB);

    private static readonly Color RelicInk = new(0.2f, 0.19f, 0.16f, 0.9f), RelicLight = new(0.86f, 0.83f, 0.74f, 0.95f),
        RelicKeyInk = new(0.13f, 0.12f, 0.11f, 0.95f), RelicLcdInk = new(0.24f, 0.09f, 0.02f), RelicRfInk = new(0.16f, 0.18f, 0.12f),
        RelicNeedle = new(0.7f, 0.13f, 0.1f);

    private static class RelicTex
    {
        private static Texture2D Get(string name) => ContentFinder<Texture2D>.Get("UI/Netrun/Frame/Relic/" + name, reportFailure: false);

        public static readonly Texture2D Body = Get("Body"), Badge = Get("Badge"), RailText = Get("RailText"), RailParts = Get("RailParts"), Floppy = Get("Floppy"), Plate = Get("Plate"),
            KeyRed = Get("Key_Red"), KeyGrey = Get("Key_Grey"), KeyOrange = Get("Key_Orange"), KeySquare = Get("Key_Square"),
            Gauge = Get("Gauge"), GaugeGlass = Get("GaugeGlass"), ColKnobs = Get("Col_Knobs"), ColScan = Get("Col_Scan"),
            TraceScale = Get("Col_TraceScale"), TraceKnob = Get("Col_TraceKnob"), ColButtons = Get("Col_Buttons"), ColLine = Get("Col_Line"),
            Vent = Get("Col_Vent"), DymoTune = Get("Dymo_Tune"), LampBezel = Get("LampBezel"), Glass = Get("Glass"), Lcd = Get("Lcd"),
            LcdOrange = Get("LcdOrange"), SlideOff = Get("Slide_0"), SlideOn = Get("Slide_1"), Stickers = Get("Stickers"), Screw = Get("Screw");

        public static bool Ready => Body != null;
    }

    private static void RelicCaption(float cx, float y, string text, Color c) => Stencil(new Rect(cx - 40f, y, 80f, 14f), text, TextAnchor.UpperCenter, c);

    private static void RelicCaptionBold(float cx, float y, string text, Color c) => StencilBold(new Rect(cx - 40f, y, 80f, 14f), text, TextAnchor.UpperCenter, c);

    private static Rect RelicBezel(Rect outer, float heat, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu)
    {
        var box = new Rect(outer.x + RelicOut, outer.y + RelicOut, outer.width - 2f * RelicOut, outer.height - 2f * RelicOut);
        var screen = new Rect(box.x + RelicInL, box.y + RelicInT, box.width - RelicInL - RelicInR, box.height - RelicInT - RelicInB);
        bool paint = Event.current.type == EventType.Repaint;
        if (paint)
        {
            DrawRelicCasing(box, screen, online, anim, time);
            DrawRelicColumn(box, heat, level, anim, time);
        }
        DrawRelicTop(box, level, online, anim, time, onKey, menu, paint);
        DrawRelicBottom(box, heat, level, online, anim, time, menu ? onKey : null, paint);
        return screen;
    }

    private static void DrawRelicCasing(Rect box, Rect screen, bool online, bool anim, float time)
    {
        NineSlice(box, RelicTex.Body, RelicSlice, RelicSlice, RelicSlice, RelicSlice);
        Sprite(RelicTex.Badge, new Vector2(box.x + 48f, box.y + 12f));
        // The left rail: jacks, the battery latch, a strap lug and a serial plate at the top, worn engraved text below.
        float y = screen.y + 14f;
        if (screen.height >= RelicTex.RailParts.height + 40f)
        {
            Sprite(RelicTex.RailParts, new Vector2(box.x + 5f, y));
            y += RelicTex.RailParts.height + 24f;
        }
        // The floppy drive at the bottom of the rail; its LED flickers with disk activity while the net is up.
        float bottom = screen.yMax - 16f;
        if (bottom - RelicTex.Floppy.height >= y + 10f)
        {
            bottom -= RelicTex.Floppy.height;
            Sprite(RelicTex.Floppy, new Vector2(box.x + 5f, bottom));
            bool busy = online && anim && Noise(time, 6f) > 0.55f;
            Widgets.DrawBoxSolid(new Rect(box.x + 18f, bottom + 9f, 5f, 5f), busy ? new Color(0.35f, 0.9f, 0.45f) : new Color(0.12f, 0.24f, 0.14f));
            bottom -= 20f;
        }
        float h = Mathf.Min(RelicTex.RailText.height, bottom - y);
        if (h > 100f)
            GUI.DrawTextureWithTexCoords(new Rect(box.x + 9f, bottom - h, RelicTex.RailText.width, h), RelicTex.RailText,
                new Rect(0f, 0f, 1f, h / RelicTex.RailText.height));
    }

    /// <summary>A jewel lamp in a bezel: a dim glass, lit with a glow by <paramref name="a"/>.</summary>
    private static void RelicLamp(Rect bezel, float a, Color c)
    {
        GUI.DrawTexture(bezel, RelicTex.LampBezel);
        var glass = bezel.ContractedBy(4f);
        GUI.color = new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f);
        GUI.DrawTexture(glass, RelicTex.Glass);
        if (a > 0f)
        {
            Glow(glass, c, 0.6f * a, 5f);
            GUI.color = new Color(c.r, c.g, c.b, a);
            GUI.DrawTexture(glass, RelicTex.Glass);
        }
        GUI.color = Color.white;
    }

    /// <summary>The instrument column: sections from the top while they fit above its bottom edge.</summary>
    private static void DrawRelicColumn(Rect box, float heat, HeatLevel level, bool anim, float time)
    {
        float mx = box.xMax - 73f, top = box.y + 66f, bottom = box.yMax - 70f;
        bool Fits(float y) => top + y <= bottom - 14f;
        void Line(float y)
        {
            if (Fits(y))
                Sprite(RelicTex.ColLine, new Vector2(mx - 55f, top + y));
        }
        void At(Texture2D tex, float y) => Sprite(tex, new Vector2(Mathf.Round(mx - tex.width / 2f), top + y));

        if (!Fits(100f))
            return;
        At(RelicTex.ColKnobs, 6f);
        Line(96f);

        // TUNE: the needle at the tuner under the cracked glass; from Target it shudders.
        if (!Fits(290f))
            return;
        var gauge = new Rect(Mathf.Round(mx - 66f), top + 130f, 132f, 132f);
        GUI.DrawTexture(gauge, RelicTex.Gauge);
        float drift = level >= HeatLevel.Target && anim ? (Noise(time, 8f) - 0.5f) * (level >= HeatLevel.RedZone ? 0.14f : 0.06f) : 0f;
        Matrix4x4 m = GUI.matrix;
        NetrunUI.RotateAround(150f + 240f * Mathf.Clamp01(_tuner + drift), gauge.center);
        Widgets.DrawBoxSolid(new Rect(gauge.center.x - 9f, gauge.center.y - 1f, 54f, 2f), RelicNeedle);
        GUI.matrix = m;
        Widgets.DrawBoxSolid(new Rect(gauge.center.x - 4f, gauge.center.y - 4f, 8f, 8f), new Color(0.12f, 0.11f, 0.1f));
        GUI.DrawTexture(gauge, RelicTex.GaugeGlass);
        At(RelicTex.DymoTune, 280f - RelicTex.DymoTune.height / 2f);
        Tip(gauge, "NR_DeckTuner".Translate());
        Line(300f);

        // SCAN and the ICE lamp: lit from Target.
        if (!Fits(390f))
            return;
        At(RelicTex.ColScan, 314f);
        var ice = new Rect(Mathf.Round(mx - 61f) + 78f, top + 336f, 20f, 20f);
        float a = level < HeatLevel.Target ? 0f : !anim ? 1f : 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(time * (level >= HeatLevel.RedZone ? 5f : 2.2f)));
        RelicLamp(ice, a, Bad);
        Tip(ice.ExpandedBy(5f), "NR_DeckBeacon".Translate());
        Line(400f);

        // TRACE: the pointer knob turned to the Heat level, 1..5 over 200..340 degrees; it twitches in the Red Zone.
        if (!Fits(512f))
            return;
        At(RelicTex.TraceScale, 408f);
        var knob = new Rect(Mathf.Round(mx - 40f), top + 420f, 80f, 80f);
        float jitter = level >= HeatLevel.RedZone && anim ? (Noise(time, 12f) - 0.5f) * 8f : 0f;
        m = GUI.matrix;
        NetrunUI.RotateAround(200f + 35f * (int)level - 270f + jitter, knob.center);
        GUI.DrawTexture(knob, RelicTex.TraceKnob);
        GUI.matrix = m;
        Tip(knob, "NR_DeckHeatScale".Translate(heat.ToString("0"), level.Label()));
        Line(520f);

        if (!Fits(626f))
            return;
        At(RelicTex.ColButtons, 550f);
        Line(630f);
        if (Fits(692f))
            At(RelicTex.Vent, 640f);
    }

    /// <summary>A rubber key: lights under the mouse, sinks while pressed, dims when it does nothing here.</summary>
    private static bool RelicKey(float cx, float cy, Texture2D tex, string tip, bool active, System.Action<Rect, Color> glyph, Color ink)
    {
        var key = new Rect(Mathf.Round(cx - tex.width / 2f), Mathf.Round(cy - tex.height / 2f), tex.width, tex.height);
        if (Event.current.type == EventType.Repaint)
        {
            bool over = active && Mouse.IsOver(key);
            bool down = over && _pressed;
            float k = !active ? 0.7f : down ? 0.85f : over ? 1.1f : 1f;
            var face = new Rect(key.x, key.y + (down ? 1f : 0f), key.width, key.height);
            Sprite(tex, face.position, new Color(k, k, k));
            glyph?.Invoke(new Rect(face.x, face.y - 2f, face.width, face.height), active ? ink : new Color(ink.r, ink.g, ink.b, ink.a * 0.6f));
        }
        return CasingHit(key, active, tip);
    }

    private static void DrawRelicTop(Rect box, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu, bool paint)
    {
        float cy = box.y + 32f, ly = box.y + 52f;
        float px = box.x + 418f, pr = box.xMax - 150f;
        if (paint)
            NineSlice(new Rect(px, box.y + 10f, pr - px, 58f), RelicTex.Plate, 10f, 10f, 10f, 10f);

        string[] names = ["CLOSE", "MENU", "ACHIEV", "NET LOCK"];
        NodeDefense defense = Current.Game != null ? NodeDefense.Get() : null;
        bool isolated = defense?.Isolated == true;
        AcceptanceReport lockReport = defense?.CanToggleIsolation() ?? false;
        for (int i = 0; i < 4; i++)
        {
            float kx = px + 34f + i * 50f;
            var hit = new Rect(kx - 18f, cy - 15f, 36f, 30f);
            bool active = i switch { 0 => onKey != null, 1 or 2 => onKey != null && menu, _ => defense != null && lockReport.Accepted };
            string tip = !Mouse.IsOver(hit) ? null : i == 3
                ? (string)(isolated ? "NR_DeckKey_IsolateOff" : "NR_DeckKey_IsolateOn").Translate() + "\n\n" + "NR_IsolationGizmoDesc".Translate()
                  + (lockReport.Reason.NullOrEmpty() ? string.Empty : "\n\n" + lockReport.Reason)
                : !active && i > 0 ? (string)"NR_DeckKey_TerminalOnly".Translate() : (string)("NR_DeckKey_" + (KeyClose + i)).Translate();
            Texture2D tex = i == 0 ? RelicTex.KeyRed : i == 3 ? RelicTex.KeyOrange : RelicTex.KeyGrey;
            System.Action<Rect, Color> glyph = i switch
            {
                0 => (r, c) => GlyphText(r, c, "×", GameFont.Small),
                1 => (r, c) => GlyphTex(r.ContractedBy(8f), c, NetrunTex.MenuMain),
                2 => (r, c) => GlyphText(r, c, "★", GameFont.Tiny),
                _ => (r, c) => GlyphTex(r.ContractedBy(8f), isolated ? Bad : c, NetrunTex.NetworkLock),
            };
            if (paint && i == 3 && isolated && anim)
                Glow(hit, Bad, 0.3f + 0.25f * Mathf.Abs(Mathf.Sin(time * 2f)), 5f);
            // Light glyphs on the red key, dark on the grey and orange ones, so they read.
            if (RelicKey(kx, cy - 2f, tex, tip, active, glyph, i == 0 ? RelicLight : RelicKeyInk))
            {
                if (i == 3)
                    defense?.ToggleIsolation();
                else
                    onKey?.Invoke(KeyClose + i);
            }
            if (paint)
                RelicCaption(kx, ly, names[i], RelicLight);
        }
        if (!paint)
            return;

        // RF on a small LCD.
        bool hot = level >= HeatLevel.Target;
        float rx = px + 246f;
        if (rx + 92f > pr - 8f)
            return;
        var lcd = new Rect(rx, cy - 16f, 92f, 30f);
        NineSlice(lcd, RelicTex.Lcd, 8f, 8f, 8f, 8f);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(lcd.x + 7f, lcd.y + 3f, 30f, 16f), "RF".Colorize(RelicRfInk));
        Text.Font = GameFont.Small;
        int jitter = hot && anim ? Mathf.FloorToInt(Noise(time, 9f) * 3f) - 1 : 0;
        int lit = online ? Mathf.Clamp(Mathf.RoundToInt(_rf / 6f * 7f) + jitter, 1, 7) : 0;
        for (int i = 0; i < 7; i++)
        {
            float h = 3f + i * 2f;
            bool on = i < lit || hot && i == 6 && online;
            Color c = hot && i == 6 ? Bad : RelicRfInk;
            Widgets.DrawBoxSolid(new Rect(lcd.x + 28f + i * 8f, lcd.yMax - 7f - h, 5f, h), on ? c : new Color(c.r, c.g, c.b, 0.15f));
        }
        RelicCaption(lcd.center.x, ly, "LINK", RelicLight);
        Tip(lcd, (online ? "NR_DeckRfOn" : "NR_DeckRfOff").Translate());

        // ALARM, NET, SICK, POD: jewel lamps after the LCD, as many as fit on the plate.
        if (!NetrunMod.Settings.FrameLights)
            return;
        float lx = lcd.xMax + 34f;
        bool sick = Current.Game != null && Cached("deck.sick", () => PawnsFinder.AllMaps_FreeColonists.Any(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick)), 1f);
        bool pod = Current.Game != null && Cached("deck.pod", () => Find.Maps.Any(mm => mm.GetComponent<USH_HE.MapComponent_CyberpodManager>()?.Pods.Any(pp => pp.ContainedThing != null) == true), 1f);
        float phase = time * 1.4f % 1f;
        float alarm = !hot ? 0f : !anim ? 1f
            : level >= HeatLevel.RedZone ? (phase < 0.12f || phase is > 0.24f and < 0.36f ? 1f : 0.08f)
            : 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(time * 2.2f));
        (float a, Color c, string tip, string label)[] lights =
        [
            (alarm, Bad, "NR_DeckBeacon".Translate(), "ALARM"),
            (online ? 1f : 0f, Ok, "NR_DeckLightNet".Translate(), "NET"),
            (!sick ? 0f : anim ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * 3f)) : 1f, DeckOrange, "NR_DeckLightSick".Translate(), "SICK"),
            (pod ? 1f : 0f, PodGreen, "NR_DeckLightPod".Translate(), "POD"),
        ];
        for (int i = 0; i < lights.Length; i++)
        {
            float x = lx + i * 34f;
            if (x + 16f > pr - 6f)
                break;
            var bezel = new Rect(x - 10f, cy - 12f, 20f, 20f);
            RelicLamp(bezel, lights[i].a, lights[i].c);
            RelicCaption(x, ly - 7f, lights[i].label, RelicLight);
            Tip(bezel.ExpandedBy(5f), lights[i].tip);
        }
    }

    private static void DrawRelicBottom(Rect box, float heat, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool paint)
    {
        float yb = box.yMax - RelicInB;
        float cb = yb + 58f, lb = yb + 80f;
        if (paint && NetrunMod.Settings.FrameLcd)
        {
            NineSlice(new Rect(box.x + 50f, cb - 31f, 252f, 62f), RelicTex.Plate, 10f, 10f, 10f, 10f);
            var lcd = new Rect(box.x + 54f, cb - 27f, 244f, 54f);
            NineSlice(lcd, RelicTex.LcdOrange, 8f, 8f, 8f, 8f);
            Glow(lcd.ContractedBy(8f), DeckOrange, 0.15f, 6f);
            string[] lines = Cached("deck.lcd", () => LcdLines(heat, level, online), 1f);
            Text.Font = GameFont.Tiny;
            for (int i = 0; i < lines.Length; i++)
            {
                // One line per row, never wrapped: a wrapped line pushed the next ones out of the LCD.
                var row = new Rect(lcd.x + 10f, Mathf.Round(lcd.center.y - 7f + (i - (lines.Length - 1) / 2f) * 14f), lcd.width - 16f, 15f);
                Text.WordWrap = false;
                Widgets.Label(row, lines[i].Colorize(RelicLcdInk));
                Text.WordWrap = true;
            }
            Text.Font = GameFont.Small;
            Sprite(RelicTex.Screw, new Vector2(box.x + 30f, cb - 8f));
            Sprite(RelicTex.Screw, new Vector2(box.x + 306f, cb - 8f));
            if (Mouse.IsOver(lcd))
                Tip(lcd, "NR_DeckLcd".Translate());
        }
        if (paint)
            Sprite(RelicTex.Stickers, new Vector2(box.x + 334f, cb - 28f));

        // BACK, CORE, HELP: grey square keys like the old handhelds.
        string[] glyphs = ["◁", "○", "?"];
        string[] names = ["BACK", "CORE", "HELP"];
        int[] ids = [KeyBack, KeyCore, KeyHelp];
        float kx = box.x + 568f;
        for (int i = 0; i < 3; i++)
        {
            float cx = kx + i * 50f;
            var hit = new Rect(cx - 21f, cb - 17f, 42f, 34f);
            bool active = onKey != null;
            string tip = active && Mouse.IsOver(hit) ? (string)("NR_DeckKey_" + ids[i]).Translate() : null;
            string g = glyphs[i];
            if (RelicKey(cx, cb, RelicTex.KeySquare, tip, active, (r, c) => GlyphText(new Rect(r.x, r.y - 1f, r.width, r.height), c, g, GameFont.Medium), RelicKeyInk))
                onKey?.Invoke(ids[i]);
            if (paint)
                RelicCaptionBold(cx, lb, names[i], RelicInk);
        }
        if (!paint)
            return;
        float used = kx + 2f * 50f + 30f;

        // NET LINK: the slide switch just left of the column.
        float sx = box.xMax - 238f;
        if (sx - 30f >= used)
        {
            var slide = new Rect(sx, cb - 11f, RelicTex.SlideOn.width, RelicTex.SlideOn.height);
            GUI.DrawTexture(slide, online ? RelicTex.SlideOn : RelicTex.SlideOff);
            StencilBold(new Rect(sx - 34f, cb - 7f, 30f, 14f), "OFF", TextAnchor.UpperRight, RelicInk);
            StencilBold(new Rect(slide.xMax + 4f, cb - 7f, 30f, 14f), "ON", TextAnchor.UpperLeft, RelicInk);
            RelicCaptionBold(slide.center.x, lb, "NET LINK", RelicInk);
            if (Mouse.IsOver(slide))
                Tip(slide, (online ? "NR_DeckNetLinkOn" : "NR_DeckNetLinkOff").Translate());
            sx -= 40f;
        }
        else
            sx = box.xMax - 150f;

        // HEAT: five lamps in a dark box; the top lit one pulses from Target. The box shrinks to the room between the keys
        // and NET LINK (Maxim, 2026-10-16: at the default scale it used to vanish).
        float avail = sx - used - 20f;
        if (avail < 124f)
            return;
        float fw = Mathf.Min(164f, avail), step = (fw - 44f) / 4f, sw = Mathf.Min(20f, step - 6f);
        var frame = new Rect(Mathf.Round(used + 10f + (avail - fw) / 2f), cb - 18f, fw, 36f);
        NineSlice(frame, RelicTex.Plate, 10f, 10f, 10f, 10f);
        for (int i = 0; i < 5; i++)
        {
            var sq = new Rect(Mathf.Round(frame.x + 10f + (step - sw) / 2f + i * step + 2f), cb - 9f, sw, 18f);
            bool lit = i <= (int)level;
            float k = !lit ? 0f : i == (int)level && level >= HeatLevel.Target && anim
                ? 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(time * (level >= HeatLevel.RedZone ? 5f : 2.5f))) : 1f;
            Color c = HeatSegments[i];
            if (k > 0f)
                Glow(sq, c, 0.45f * k, 4f);
            Widgets.DrawBoxSolid(sq, Color.Lerp(new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f), c, k));
        }
        RelicCaptionBold(frame.center.x, lb, "HEAT", RelicInk);
        if (Mouse.IsOver(frame))
            Tip(frame, "NR_DeckHeatScale".Translate(heat.ToString("0"), level.Label()));
    }
}
