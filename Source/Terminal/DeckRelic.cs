using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The Relic, an old field deck (docs/mockups/47b-old-orange.png, Maxim's pick 2026-10-15): worn cream plastic with
/// chipped paint and rust runs, an orange side panel, a rubber gasket round the screen, rubber keys on a dark plate,
/// a dial gauge for TUNE, jewel lamps with ALARM, a small LCD for RF, an orange LCD, round keys, Heat lamps in a
/// printed frame, a speaker grille and a slide switch for NET LINK. Textures from tools/gen_deck_relic.py.
/// </summary>
public static partial class NetrunUI
{
    private const float RelicOut = 4f;
    private const float RelicInL = 60f, RelicInT = 96f, RelicInR = 64f, RelicInB = 100f;
    private const float RelicSlice = 170f;
    public static readonly Vector2 RelicExtra = new(2f * RelicOut + RelicInL + RelicInR, 2f * RelicOut + RelicInT + RelicInB);

    private static readonly Color RelicInk = new(0.2f, 0.19f, 0.16f, 0.9f), RelicLight = new(0.86f, 0.83f, 0.74f, 0.95f),
        RelicLcdInk = new(0.24f, 0.09f, 0.02f), RelicRfInk = new(0.16f, 0.18f, 0.12f), RelicNeedle = new(0.75f, 0.16f, 0.12f);

    private static class RelicTex
    {
        private static Texture2D Get(string name) => ContentFinder<Texture2D>.Get("UI/Netrun/Frame/Relic/" + name, reportFailure: false);

        public static readonly Texture2D Body = Get("Body"), Badge = Get("Badge"), Plate = Get("Plate"), KeyRed = Get("Key_Red"), KeyGrey = Get("Key_Grey"),
            KeyOrange = Get("Key_Orange"), Gauge = Get("Gauge"), LampBezel = Get("LampBezel"), Glass = Get("Glass"), Lcd = Get("Lcd"),
            LcdOrange = Get("LcdOrange"), Round = Get("Round"), SlideOff = Get("Slide_0"), SlideOn = Get("Slide_1"), Grille = Get("Grille"),
            Jacks = Get("Jacks"), Danger = Get("Danger"), Rivet = Get("Rivet"), Barcode = Get("Barcode");

        public static bool Ready => Body != null;
    }

    private static void RelicCaption(float cx, float y, string text, Color c) => Stencil(new Rect(cx - 40f, y, 80f, 14f), text, TextAnchor.UpperCenter, c);

    private static Rect RelicBezel(Rect outer, float heat, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu)
    {
        var box = new Rect(outer.x + RelicOut, outer.y + RelicOut, outer.width - 2f * RelicOut, outer.height - 2f * RelicOut);
        var screen = new Rect(box.x + RelicInL, box.y + RelicInT, box.width - RelicInL - RelicInR, box.height - RelicInT - RelicInB);
        bool paint = Event.current.type == EventType.Repaint;
        if (paint)
            DrawRelicCasing(box, screen);
        DrawRelicTop(box, level, online, anim, time, onKey, menu, paint);
        DrawRelicBottom(box, heat, level, online, anim, time, menu ? onKey : null, paint);
        return screen;
    }

    private static void DrawRelicCasing(Rect box, Rect screen)
    {
        NineSlice(box, RelicTex.Body, RelicSlice, RelicSlice, RelicSlice, RelicSlice);
        Sprite(RelicTex.Badge, new Vector2(box.x + 44f, box.y + 10f));
        // Rivets with rust runs down both rails; the jack strip on the left, DANGER and a barcode on the orange panel.
        for (float y = screen.y + 40f; y < screen.yMax - 60f; y += 180f)
        {
            Sprite(RelicTex.Rivet, new Vector2(box.x + 11f, y));
            Sprite(RelicTex.Rivet, new Vector2(box.xMax - 35f, y));
        }
        if (screen.height >= 340f)
            Sprite(RelicTex.Jacks, new Vector2(box.x + 13f, screen.y + 80f));
        if (screen.height >= 300f)
        {
            Sprite(RelicTex.Danger, new Vector2(box.xMax - 43f, screen.y + 130f));
            Sprite(RelicTex.Barcode, new Vector2(box.xMax - 46f, screen.yMax - 110f));
        }
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
            glyph?.Invoke(new Rect(face.x, face.y - 2f, face.width, face.height), active ? ink : new Color(ink.r, ink.g, ink.b, 0.4f));
        }
        return CasingHit(key, active, tip);
    }

    private static void DrawRelicTop(Rect box, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu, bool paint)
    {
        float cy = box.y + 38f, ly = box.y + 62f;
        float px = box.x + 420f;
        if (paint)
            NineSlice(new Rect(px, box.y + 14f, 214f, 66f), RelicTex.Plate, 10f, 10f, 10f, 10f);

        string[] names = ["CLOSE", "MENU", "ACHIEV", "NET LOCK"];
        NodeDefense defense = Current.Game != null ? NodeDefense.Get() : null;
        bool isolated = defense?.Isolated == true;
        AcceptanceReport lockReport = defense?.CanToggleIsolation() ?? false;
        for (int i = 0; i < 4; i++)
        {
            float kx = px + 32f + i * 50f;
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
            if (RelicKey(kx, cy, tex, tip, active, glyph, RelicLight))
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

        // TUNE: the dial gauge, its needle at the tuner; from Target it shudders.
        float gx = px + 214f + 50f;
        var gauge = new Rect(gx - 30f, cy - 30f, 60f, 60f);
        GUI.DrawTexture(gauge, RelicTex.Gauge);
        float drift = level >= HeatLevel.Target && anim ? (Noise(time, 8f) - 0.5f) * (level >= HeatLevel.RedZone ? 0.14f : 0.06f) : 0f;
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(150f + 240f * Mathf.Clamp01(_tuner + drift), gauge.center);
        Widgets.DrawBoxSolid(new Rect(gauge.center.x - 3f, gauge.center.y - 1f, 21f, 2f), RelicNeedle);
        GUI.matrix = m;
        Widgets.DrawBoxSolid(new Rect(gauge.center.x - 2f, gauge.center.y - 2f, 4f, 4f), RelicInk);
        RelicCaption(gx, ly + 6f, "TUNE", RelicInk);
        Tip(gauge, "NR_DeckTuner".Translate());
        float used = gauge.xMax + 20f;

        // RF on a small LCD, right.
        bool hot = level >= HeatLevel.Target;
        float rx = box.xMax - 160f;
        if (rx >= used)
        {
            var lcd = new Rect(rx, cy - 16f, 92f, 32f);
            NineSlice(lcd, RelicTex.Lcd, 8f, 8f, 8f, 8f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(lcd.x + 7f, lcd.y + 4f, 30f, 16f), "RF".Colorize(RelicRfInk));
            Text.Font = GameFont.Small;
            int jitter = hot && anim ? Mathf.FloorToInt(Noise(time, 9f) * 3f) - 1 : 0;
            int lit = online ? Mathf.Clamp(Mathf.RoundToInt(_rf / 6f * 7f) + jitter, 1, 7) : 0;
            for (int i = 0; i < 7; i++)
            {
                float h = 3f + i * 2f;
                bool on = i < lit || hot && i == 6 && online;
                Color c = hot && i == 6 ? Bad : RelicRfInk;
                Widgets.DrawBoxSolid(new Rect(lcd.x + 28f + i * 8f, lcd.yMax - 8f - h, 5f, h), on ? c : new Color(c.r, c.g, c.b, 0.15f));
            }
            RelicCaption(lcd.center.x, ly, "LINK", RelicInk);
            Tip(lcd, (online ? "NR_DeckRfOn" : "NR_DeckRfOff").Translate());
        }
        else
            rx = box.xMax - 40f;

        // ALARM, NET, SICK, POD: jewel lamps in bezels, between the gauge and RF.
        float lx = rx - 130f;
        if (!NetrunMod.Settings.FrameLights || lx - 14f < used)
            return;
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
            var bezel = new Rect(x - 10f, cy - 10f, 20f, 20f);
            GUI.DrawTexture(bezel, RelicTex.LampBezel);
            var glass = bezel.ContractedBy(4f);
            Color c = lights[i].c;
            GUI.color = new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f);
            GUI.DrawTexture(glass, RelicTex.Glass);
            if (lights[i].a > 0f)
            {
                Glow(glass, c, 0.6f * lights[i].a, 5f);
                GUI.color = new Color(c.r, c.g, c.b, lights[i].a);
                GUI.DrawTexture(glass, RelicTex.Glass);
            }
            GUI.color = Color.white;
            RelicCaption(x, ly, lights[i].label, RelicInk);
            Tip(bezel.ExpandedBy(5f), lights[i].tip);
        }
    }

    private static void DrawRelicBottom(Rect box, float heat, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool paint)
    {
        float yb = box.yMax - RelicInB;
        float cb = yb + 52f, lb = yb + 74f;
        if (paint && NetrunMod.Settings.FrameLcd)
        {
            var lcd = new Rect(box.x + 50f, yb + 24f, 272f, 54f);
            NineSlice(lcd, RelicTex.LcdOrange, 8f, 8f, 8f, 8f);
            Glow(lcd.ContractedBy(8f), DeckOrange, 0.15f, 6f);
            string[] lines = Cached("deck.lcd", () => LcdLines(heat, level, online), 1f);
            Text.Font = GameFont.Tiny;
            for (int i = 0; i < lines.Length; i++)
                Widgets.Label(new Rect(lcd.x + 12f, lcd.center.y - 8f + (i - (lines.Length - 1) / 2f) * 14f, lcd.width - 20f, 16f), lines[i].Colorize(RelicLcdInk));
            Text.Font = GameFont.Small;
            if (Mouse.IsOver(lcd))
                Tip(lcd, "NR_DeckLcd".Translate());
        }

        string[] glyphs = ["◁", "○", "?"];
        string[] names = ["BACK", "CORE", "HELP"];
        int[] ids = [KeyBack, KeyCore, KeyHelp];
        float kx = box.x + 376f;
        for (int i = 0; i < 3; i++)
        {
            float cx = kx + i * 56f;
            var hit = new Rect(cx - 17f, cb - 21f, 34f, 34f);
            bool active = onKey != null;
            string tip = active && Mouse.IsOver(hit) ? (string)("NR_DeckKey_" + ids[i]).Translate() : null;
            string g = glyphs[i];
            if (RelicKey(cx, cb - 4f, RelicTex.Round, tip, active, (r, c) => GlyphText(new Rect(r.x, r.y + 2f, r.width, r.height), c, g, GameFont.Small), RelicLight))
                onKey?.Invoke(ids[i]);
            if (paint)
                RelicCaption(cx, lb, names[i], RelicInk);
        }
        if (!paint)
            return;
        float used = kx + 2f * 56f + 34f;

        // NET LINK: the slide switch on the right.
        float sx = box.xMax - 160f;
        if (sx >= used)
        {
            var slide = new Rect(sx, cb - 15f, RelicTex.SlideOn.width, RelicTex.SlideOn.height);
            GUI.DrawTexture(slide, online ? RelicTex.SlideOn : RelicTex.SlideOff);
            Stencil(new Rect(sx - 34f, cb - 12f, 30f, 14f), "OFF", TextAnchor.UpperRight, RelicInk);
            Stencil(new Rect(slide.xMax + 4f, cb - 12f, 30f, 14f), "ON", TextAnchor.UpperLeft, RelicInk);
            RelicCaption(slide.center.x, lb, "NET LINK", RelicInk);
            if (Mouse.IsOver(slide))
                Tip(slide, (online ? "NR_DeckNetLinkOn" : "NR_DeckNetLinkOff").Translate());
            sx -= 40f;
        }
        else
            sx = box.xMax - 40f;

        // HEAT: five lamps in a printed frame; the top lit one pulses from Target.
        float hx = box.x + 576f;
        if (hx + 180f > sx)
            return;
        var frame = new Rect(hx - 10f, cb - 20f, 180f, 30f);
        GUI.color = RelicInk;
        Widgets.DrawBox(frame);
        GUI.color = Color.white;
        for (int i = 0; i < 5; i++)
        {
            var sq = new Rect(hx + i * 34f, cb - 13f, 24f, 16f);
            Widgets.DrawBoxSolid(sq, new Color(0.16f, 0.15f, 0.13f));
            bool lit = i <= (int)level;
            float k = !lit ? 0f : i == (int)level && level >= HeatLevel.Target && anim
                ? 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(time * (level >= HeatLevel.RedZone ? 5f : 2.5f))) : 1f;
            Color c = HeatSegments[i];
            if (k > 0f)
                Glow(sq.ContractedBy(2f), c, 0.45f * k, 4f);
            Widgets.DrawBoxSolid(sq.ContractedBy(2f), Color.Lerp(new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f), c, k));
        }
        RelicCaption(frame.center.x, lb, "HEAT", RelicInk);
        if (Mouse.IsOver(frame))
            Tip(frame, "NR_DeckHeatScale".Translate(heat.ToString("0"), level.Label()));

        // The speaker grille fills the gap before NET LINK.
        float gxl = frame.xMax + 20f;
        if (sx - gxl >= RelicTex.Grille.width + 10f)
            Sprite(RelicTex.Grille, new Vector2(Mathf.Round((gxl + sx - RelicTex.Grille.width) / 2f), cb - 28f));
    }
}
