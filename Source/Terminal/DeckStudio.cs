using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// Braindance // Studio (docs/mockups/49-studio.png, Maxim's pick 2026-10-16): a violet monitor standing behind a wide
/// sloped desk. The monitor's top strip has the title, a timeline (timecode and a track scrolling under the playhead),
/// the chip bay, REC / NET / SICK / POD and signal bars; the desk has the LEVEL meter (HEAT and SIG LEDs), the keys on a
/// panel, the SESSION LCD with TONE / PAN, the channel faders with NET LINK and the TUNER JOG wheel, plus VHS stickers.
/// Textures and the matching mockup from tools/gen_deck_studio.py; the layout numbers mirror its mock().
/// </summary>
public static partial class NetrunUI
{
    private const float StudioOut = 4f;
    private const float StudioInL = 52f, StudioInT = 58f, StudioInR = 52f, StudioInB = 166f;
    private const float StudioOver = 24f, StudioDesk = 150f, StudioFader = 196f;
    public static readonly Vector2 StudioExtra = new(2f * StudioOut + StudioInL + StudioInR, 2f * StudioOut + StudioInT + StudioInB);

    private static readonly Color StudioInk = new(0.77f, 0.75f, 0.69f, 0.92f), StudioAccent = new(0.71f, 0.48f, 0.66f),
        StudioPink = new(0.93f, 0.47f, 0.78f), StudioKeyInk = new(0.16f, 0.12f, 0.09f, 0.95f), StudioKeyLight = new(0.94f, 0.86f, 0.78f, 0.95f);

    private static readonly float[] StudioChannels = [0.3f, 0.55f, 0.7f, 0.45f];

    private static class StudioTex
    {
        private static Texture2D Get(string name) => ContentFinder<Texture2D>.Get("UI/Netrun/Frame/Studio/" + name, reportFailure: false);

        public static readonly Texture2D Body = Get("Body"), Meter = Get("Meter"), Bay = Get("Bay"), Wave = Get("Wave"),
            KeyGrey = Get("Key_Grey"), KeyRed = Get("Key_Red"), KeyAmber = Get("Key_Amber"), Lcd = Get("Lcd"), Panel = Get("Panel"),
            FaderSlot = Get("FaderSlot"), FaderCap = Get("FaderCap"), FaderCapPink = Get("FaderCapPink"), Jog = Get("Jog"),
            LedBezel = Get("LedBezel"), Glass = Get("Glass"), Plate = Get("Plate"), Knob = Get("KnobSmall"), Note = Get("Note"),
            Vent = Get("Vent"), Badge = Get("Badge"), Sun = Get("Sticker_Sun"), Phones = Get("Sticker_Phones"), BdLogo = Get("Sticker_BdLogo"),
            Play = Get("Sticker_Play"), Jp = Get("Sticker_Jp"), Rating = Get("Sticker_Rating"), Side = Get("Sticker_Side"), Warn = Get("Sticker_Warn");

        public static bool Ready => Body != null;
    }

    private static void StudioCaption(float cx, float y, string text, Color? c = null)
        => StencilBold(new Rect(cx - 40f, y, 80f, 14f), text, TextAnchor.UpperCenter, c ?? StudioInk);

    private static Rect StudioBezel(Rect outer, float heat, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu)
    {
        var box = new Rect(outer.x + StudioOut, outer.y + StudioOut, outer.width - 2f * StudioOut, outer.height - 2f * StudioOut);
        var screen = new Rect(box.x + StudioInL, box.y + StudioInT, box.width - StudioInL - StudioInR, box.height - StudioInT - StudioInB);
        bool paint = Event.current.type == EventType.Repaint;
        if (paint)
        {
            NineSlice(box, StudioTex.Body, 170f, 120f, 170f, 200f);
            DrawStudioMonitor(box, screen, level, online, anim, time);
        }
        DrawStudioDesk(box, heat, level, online, anim, time, onKey, menu, paint);
        return screen;
    }

    /// <summary>A small LED in its bezel, lit with a glow by <paramref name="a"/>.</summary>
    private static void StudioLed(Vector2 c, float a, Color col)
    {
        Sprite(StudioTex.LedBezel, new Vector2(c.x - 7f, c.y - 7f));
        var glass = new Rect(c.x - 5f, c.y - 5f, 10f, 10f);
        GUI.color = new Color(col.r * 0.3f, col.g * 0.3f, col.b * 0.3f);
        GUI.DrawTexture(glass, StudioTex.Glass);
        if (a > 0f)
        {
            Glow(glass, col, 0.6f * a, 5f);
            GUI.color = new Color(col.r, col.g, col.b, a);
            GUI.DrawTexture(glass, StudioTex.Glass);
        }
        GUI.color = Color.white;
    }

    private static void DrawStudioMonitor(Rect box, Rect screen, HeatLevel level, bool online, bool anim, float time)
    {
        float ty = box.y + 26f;
        StencilBold(new Rect(box.x + 52f, ty - 7f, 160f, 14f), "BRAINDANCE // STUDIO", TextAnchor.UpperLeft, StudioAccent);
        Sprite(StudioTex.Note, new Vector2(box.x + 200f, box.y + 6f));
        float lx = box.xMax - 52f - 40f - 4f * 58f;

        // The timeline: a timecode and the recording's three lanes scrolling under a fixed playhead.
        var tl = new Rect(box.x + 262f, ty - 15f, 240f, 30f);
        float bayX = tl.xMax + 24f;
        if (tl.xMax <= lx - 10f)
        {
            NineSlice(tl, StudioTex.Lcd, 8f, 8f, 8f, 8f);
            float t = anim && online ? time : 767.4f;
            string code = $"{(int)(t / 3600f) % 100:00}:{(int)(t / 60f) % 60:00}:{(int)t % 60:00}:{(int)(t * 24f) % 24:00}";
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(tl.x + 9f, ty - 8f, 80f, 16f), code.Colorize(LcdGreen));
            Text.Font = GameFont.Small;
            var wave = new Rect(tl.x + 88f, ty - 11f, tl.width - 96f, 22f);
            float uw = wave.width / StudioTex.Wave.width, u = anim && online ? time * 0.06f % 1f : 0.27f;
            float first = Mathf.Min(uw, 1f - u);
            GUI.DrawTextureWithTexCoords(new Rect(wave.x, wave.y, wave.width * first / uw, wave.height), StudioTex.Wave, new Rect(u, 0f, first, 1f));
            if (first < uw)
                GUI.DrawTextureWithTexCoords(new Rect(wave.x + wave.width * first / uw, wave.y, wave.width * (uw - first) / uw, wave.height), StudioTex.Wave, new Rect(0f, 0f, uw - first, 1f));
            float ph = Mathf.Round(wave.center.x);
            Widgets.DrawBoxSolid(new Rect(ph, ty - 13f, 1f, 26f), StudioPink);
            Widgets.DrawBoxSolid(new Rect(ph - 3f, ty - 13f, 7f, 2f), StudioPink);
            Widgets.DrawBoxSolid(new Rect(ph - 1f, ty - 11f, 3f, 2f), StudioPink);
            Tip(tl, "NR_DeckStudioTimeline".Translate());
        }
        else
            bayX = tl.x;

        // The chip bay: its LED flickers with reads while the net is up.
        if (bayX + 150f <= lx - 10f)
        {
            Rect bay = Sprite(StudioTex.Bay, new Vector2(bayX, ty - 17f));
            bool busy = online && anim && Noise(time, 6f) > 0.55f;
            Widgets.DrawBoxSolid(new Rect(bay.x + 129f, bay.y + 8f, 5f, 5f), busy ? Ok : online ? new Color(0.16f, 0.4f, 0.22f) : new Color(0.1f, 0.16f, 0.12f));
            Tip(bay, "NR_DeckStudioBay".Translate());
            float x = bay.xMax + 16f;
            if (x + 50f <= lx - 12f)
                Sprite(StudioTex.Phones, new Vector2(x, ty - 22f));
            x += 64f;
            if (x + 84f <= lx - 24f)
                Sprite(StudioTex.Vent, new Vector2(x, ty - 9f));
            x += 104f;
            if (x + 136f <= lx - 16f)
                Sprite(StudioTex.Play, new Vector2(x, ty - 13f));
        }

        // REC (from Target, the colony is being recorded), NET, SICK, POD; then the signal bars.
        bool hot = level >= HeatLevel.Target;
        if (NetrunMod.Settings.FrameLights)
        {
            bool sick = Current.Game != null && Cached("deck.sick", () => PawnsFinder.AllMaps_FreeColonists.Any(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick)), 1f);
            bool pod = Current.Game != null && Cached("deck.pod", () => Find.Maps.Any(mm => mm.GetComponent<USH_HE.MapComponent_CyberpodManager>()?.Pods.Any(pp => pp.ContainedThing != null) == true), 1f);
            float phase = time * 1.4f % 1f;
            float rec = !hot ? 0f : !anim ? 1f
                : level >= HeatLevel.RedZone ? (phase < 0.12f || phase is > 0.24f and < 0.36f ? 1f : 0.08f)
                : 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(time * 2.2f));
            (float a, Color c, string tip, string label)[] lights =
            [
                (rec, Bad, "NR_DeckBeacon".Translate(), "REC"),
                (online ? 1f : 0f, Ok, "NR_DeckLightNet".Translate(), "NET"),
                (!sick ? 0f : anim ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * 3f)) : 1f, DeckOrange, "NR_DeckLightSick".Translate(), "SICK"),
                (pod ? 1f : 0f, PodGreen, "NR_DeckLightPod".Translate(), "POD"),
            ];
            for (int i = 0; i < lights.Length; i++)
            {
                float x = lx + i * 58f;
                StudioLed(new Vector2(x, ty), lights[i].a, lights[i].c);
                StencilBold(new Rect(x + 12f, ty - 7f, 40f, 14f), lights[i].label, TextAnchor.UpperLeft, StudioInk);
                Tip(new Rect(x - 10f, ty - 10f, 50f, 20f), lights[i].tip);
            }
        }
        float bx = box.xMax - 52f - 30f;
        int jitter = hot && anim ? Mathf.FloorToInt(Noise(time, 9f) * 3f) - 1 : 0;
        int lit = online ? Mathf.Clamp(Mathf.RoundToInt(_rf / 6f * 5f) + jitter, 1, 5) : 0;
        for (int i = 0; i < 5; i++)
        {
            float h = 4f + i * 3f;
            Widgets.DrawBoxSolid(new Rect(bx + i * 6f, ty + 6f - h, 4f, h), i < lit ? Ok : new Color(0.12f, 0.2f, 0.14f));
        }
        Tip(new Rect(bx - 2f, ty - 12f, 34f, 20f), (online ? "NR_DeckRfOn" : "NR_DeckRfOff").Translate());

        // Stickers on the sides and the bezel under the screen.
        if (screen.height >= 180f)
        {
            Sprite(StudioTex.Jp, new Vector2(box.x + StudioOver + 4f, screen.y + 40f));
            Sprite(StudioTex.Warn, new Vector2(box.xMax - StudioOver - 24f, screen.yMax - 110f));
        }
        float under = box.yMax - StudioInB + 2f;
        Sprite(StudioTex.Badge, new Vector2(Mathf.Round(box.center.x - 32f), under));
        if (box.center.x - 32f > box.x + 240f)
            Sprite(StudioTex.Side, new Vector2(box.x + 120f, under));
    }

    private static void DrawStudioDesk(Rect box, float heat, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu, bool paint)
    {
        float yd = box.yMax - StudioDesk;
        void Panel(float x0, float y0, float x1, float y1) => NineSlice(new Rect(x0, y0, x1 - x0, y1 - y0), StudioTex.Panel, 12f, 12f, 12f, 12f);

        // LEVEL: HEAT lights its two columns to the Heat (the top LED pulses from Target), SIG bounces with the signal.
        float mx = box.x + 66f, my = yd + 8f;
        if (paint)
        {
            Sprite(StudioTex.Meter, new Vector2(mx, my));
            Sprite(StudioTex.Rating, new Vector2(box.x + 16f, yd + 84f));
            float[] cols = [14f, 25f, 47f, 58f];
            int heatRows = Mathf.Clamp(Mathf.RoundToInt(heat / 100f * 12f), 0, 12);
            for (int c = 0; c < 4; c++)
            {
                int n = heatRows;
                if (c >= 2)
                    n = !online ? 0 : Mathf.Clamp(Mathf.RoundToInt(_rf / 6f * 12f) - 2 + (anim ? Mathf.FloorToInt(Noise(time, 7f, c) * 5f) : 2) - (c - 2), 0, 12);
                for (int r = 0; r < n; r++)
                {
                    Color col = r < 7 ? Ok : r < 9 ? HeatSegments[1] : r < 11 ? HeatSegments[2] : HeatSegments[3];
                    float k = c < 2 && r == n - 1 && level >= HeatLevel.Target && anim
                        ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * (level >= HeatLevel.RedZone ? 5f : 2.5f))) : 1f;
                    var led = new Rect(mx + cols[c], my + 24f + (11 - r) * 7f, 9f, 5f);
                    if (r >= 9)
                        Glow(led, col, 0.35f * k, 3f);
                    Widgets.DrawBoxSolid(led, new Color(col.r, col.g, col.b, k));
                }
            }
            Tip(new Rect(mx, my, 82f, 122f), "NR_DeckHeatScale".Translate(heat.ToString("0"), level.Label()));
        }

        // The keys on their panel: CLOSE, MENU, ACHIEV, LOCK over BACK, CORE, HELP.
        float k0 = mx + 82f + 46f;
        if (paint)
            Panel(k0 - 30f, yd + 10f, k0 + 3f * 42f + 30f, yd + 130f);
        NodeDefense defense = Current.Game != null ? NodeDefense.Get() : null;
        bool isolated = defense?.Isolated == true;
        AcceptanceReport lockReport = defense?.CanToggleIsolation() ?? false;
        string[] top = ["CLOSE", "MENU", "ACHIEV", "LOCK"];
        for (int i = 0; i < 4; i++)
        {
            float kx = k0 + i * 42f, ky = yd + 34f;
            var hit = new Rect(kx - 17f, ky - 14f, 34f, 26f);
            bool active = i switch { 0 => onKey != null, 1 or 2 => onKey != null && menu, _ => defense != null && lockReport.Accepted };
            string tip = !Mouse.IsOver(hit) ? null : i == 3
                ? (string)(isolated ? "NR_DeckKey_IsolateOff" : "NR_DeckKey_IsolateOn").Translate() + "\n\n" + "NR_IsolationGizmoDesc".Translate()
                  + (lockReport.Reason.NullOrEmpty() ? string.Empty : "\n\n" + lockReport.Reason)
                : !active && i > 0 ? (string)"NR_DeckKey_TerminalOnly".Translate() : (string)("NR_DeckKey_" + (KeyClose + i)).Translate();
            System.Action<Rect, Color> glyph = i switch
            {
                0 => (r, c) => GlyphText(r, c, "×", GameFont.Small),
                1 => (r, c) => GlyphTex(r.ContractedBy(9f), c, NetrunTex.MenuMain),
                2 => (r, c) => GlyphText(r, c, "★", GameFont.Tiny),
                _ => (r, c) => GlyphTex(r.ContractedBy(9f), isolated ? Bad : c, NetrunTex.NetworkLock),
            };
            if (paint && i == 3 && isolated && anim)
                Glow(hit, Bad, 0.3f + 0.25f * Mathf.Abs(Mathf.Sin(time * 2f)), 5f);
            if (StudioKey(kx, ky, i == 0 ? StudioTex.KeyRed : StudioTex.KeyGrey, tip, active, glyph, i == 0 ? StudioKeyLight : StudioKeyInk))
            {
                if (i == 3)
                    defense?.ToggleIsolation();
                else
                    onKey?.Invoke(KeyClose + i);
            }
            if (paint)
                StudioCaption(kx, ky + 15f, top[i]);
        }
        string[] bottom = ["BACK", "CORE", "HELP"];
        string[] glyphs = ["◁", "○", "?"];
        int[] ids = [KeyBack, KeyCore, KeyHelp];
        System.Action<int> deskKey = menu ? onKey : null;
        for (int i = 0; i < 3; i++)
        {
            float kx = k0 + i * 42f, ky = yd + 86f;
            var hit = new Rect(kx - 17f, ky - 14f, 34f, 26f);
            bool active = deskKey != null;
            string tip = active && Mouse.IsOver(hit) ? (string)("NR_DeckKey_" + ids[i]).Translate() : null;
            string g = glyphs[i];
            if (StudioKey(kx, ky, i == 1 ? StudioTex.KeyAmber : StudioTex.KeyGrey, tip, active, (r, c) => GlyphText(r, c, g, GameFont.Small), StudioKeyInk))
                deskKey?.Invoke(ids[i]);
            if (paint)
                StudioCaption(kx, ky + 15f, bottom[i]);
        }
        if (!paint)
            return;

        // Right to left: the jog, NET LINK and the channel faders on their panel.
        float keysEnd = k0 + 3f * 42f + 30f;
        float right = box.xMax - StudioFader - 10f - 4f * 46f - 30f;
        float jx = box.xMax - 80f, jy = yd + 66f;
        bool faders = right + 8f >= keysEnd + 10f, link = box.xMax - StudioFader - 30f >= keysEnd + 10f;
        if (link)
        {
            var jog = new Rect(jx - 56f, jy - 56f, 112f, 112f);
            Matrix4x4 m = GUI.matrix;
            RotateAround(360f * _tuner, jog.center);
            GUI.DrawTexture(jog, StudioTex.Jog);
            GUI.matrix = m;
            StudioCaption(jx, yd + 122f, "TUNER JOG");
            Tip(jog.ContractedBy(4f), "NR_DeckTuner".Translate());
            Panel(faders ? right + 8f : box.xMax - StudioFader - 26f, yd + 6f, box.xMax - StudioFader + 26f, yd + 136f);
            for (int i = 0; i < (faders ? 5 : 1); i++)
            {
                float fx = box.xMax - StudioFader - (i == 0 ? 0f : 10f + i * 46f);
                float v = i == 0 ? (online ? 1f : 0f)
                    : Mathf.Clamp01(StudioChannels[i - 1] + (anim && online ? (Noise(time, 1.5f, i) - 0.5f) * 0.12f : 0f));
                Sprite(StudioTex.FaderSlot, new Vector2(fx - 15f, yd + 18f));
                Sprite(i == 0 ? StudioTex.FaderCapPink : StudioTex.FaderCap, new Vector2(fx - 15f, Mathf.Round(yd + 18f + 86f - 80f * v - 4f)));
                StudioCaption(fx, yd + 120f, i switch { 0 => "NET LINK", 1 => "EMOTION", 2 => "SOMATIC", 3 => "AUDIO", _ => "VISUAL" }, i == 0 ? StudioAccent : null);
                if (i == 0)
                    Tip(new Rect(fx - 16f, yd + 16f, 32f, 104f), (online ? "NR_DeckNetLinkOn" : "NR_DeckNetLinkOff").Translate());
            }
        }

        // The SESSION LCD with TONE / PAN, when it fits; stickers and the model plate in what is left.
        float x = keysEnd + 14f;
        float limit = faders ? right : link ? box.xMax - StudioFader - 30f : box.xMax - 20f;
        if (NetrunMod.Settings.FrameLcd && x + 220f + 10f <= limit)
        {
            Panel(x, yd + 10f, x + 220f, yd + 130f);
            var lcd = new Rect(x + 10f, yd + 22f, 200f, 62f);
            NineSlice(lcd, StudioTex.Lcd, 8f, 8f, 8f, 8f);
            Glow(lcd.ContractedBy(8f), LcdGreen, 0.08f, 6f);
            string[] lines = Cached("deck.lcd", () => LcdLines(heat, level, online), 1f);
            Text.Font = GameFont.Tiny;
            Text.WordWrap = false;
            for (int i = 0; i < lines.Length; i++)
                Widgets.Label(new Rect(lcd.x + 10f, lcd.y + 7f + i * 15f, lcd.width - 16f, 16f), lines[i].Colorize(LcdGreen));
            Text.WordWrap = true;
            Text.Font = GameFont.Small;
            StudioCaption(lcd.x + 30f, lcd.yMax + 10f, "SESSION");
            for (int k = 0; k < 2; k++)
            {
                float kx = lcd.xMax - 70f + k * 40f;
                Sprite(StudioTex.Knob, new Vector2(kx - 15f, yd + 92f));
                StudioCaption(kx, yd + 118f, k == 0 ? "TONE" : "PAN");
            }
            if (Mouse.IsOver(lcd))
                Tip(lcd, "NR_DeckLcd".Translate());
            x += 220f;
        }
        float gap = limit - x;
        if (gap >= 410f)
            Sprite(StudioTex.Plate, new Vector2(Mathf.Round(x + 150f + (gap - 150f) / 2f - 120f), yd + 44f));
        if (gap >= 150f)
        {
            Sprite(StudioTex.Sun, new Vector2(x + 12f, yd + 18f));
            Sprite(StudioTex.BdLogo, new Vector2(x + 70f, yd + 22f));
        }
        else if (gap >= 76f)
            Sprite(StudioTex.Sun, new Vector2(Mathf.Round(x + (gap - 66f) / 2f), yd + 30f));
    }

    /// <summary>A square key with a warm backlight: the edge glows under the mouse, the cap sinks while pressed.</summary>
    private static bool StudioKey(float cx, float cy, Texture2D tex, string tip, bool active, System.Action<Rect, Color> glyph, Color ink)
    {
        var key = new Rect(Mathf.Round(cx - tex.width / 2f), Mathf.Round(cy - tex.height / 2f), tex.width, tex.height);
        if (Event.current.type == EventType.Repaint)
        {
            bool over = active && Mouse.IsOver(key);
            bool down = over && _pressed;
            float k = !active ? 0.7f : down ? 0.85f : over ? 1.1f : 1f;
            var face = new Rect(key.x, key.y + (down ? 1f : 0f), key.width, key.height);
            if (over)
                Glow(face.ContractedBy(3f), DeckOrange, 0.3f, 5f);
            Sprite(tex, face.position, new Color(k, k, k));
            glyph?.Invoke(new Rect(face.x, face.y - 2f, face.width, face.height), active ? ink : new Color(ink.r, ink.g, ink.b, ink.a * 0.6f));
        }
        return CasingHit(new Rect(key.x + 2f, key.y + 2f, key.width - 4f, key.height - 4f), active, tip);
    }
}
