using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The NR-80 retro deck (docs/mockups/42d-retro.png): a cream handheld case with a sun-over-mountain badge, six sunset
/// bars across the top, cream keys and lamps on a sunk dark plate, control strips on the rails (VOL, PHONES, POWER;
/// CONT, EXT, DATA), a green LCD, round BACK/CORE/HELP keys, the Heat as pixel flames on a second LCD and a NET LINK
/// slide switch. Every part is a texture from tools/gen_deck_retro.py; the layout numbers mirror its mock().
/// </summary>
public static partial class NetrunUI
{
    private const float RetroOut = 8f;
    private const float RetroInL = 64f, RetroInT = 96f, RetroInR = 64f, RetroInB = 104f;
    private const float RetroSlice = 170f, RetroSideW = 32f;
    public static readonly Vector2 RetroExtra = new(2f * RetroOut + RetroInL + RetroInR, 2f * RetroOut + RetroInT + RetroInB);

    private static readonly Color RetroInk = new(0.16f, 0.13f, 0.12f, 0.9f), RetroLight = new(0.89f, 0.87f, 0.82f, 0.95f),
        RetroLcdInk = new(0.13f, 0.19f, 0.1f), RetroLcdOff = new(0.5f, 0.56f, 0.39f);

    // Five pixel flames on the Heat LCD, lit up to the Heat level (like the lives in a handheld game).
    private static readonly string[] RetroFlame = ["...#....", "..##....", "..###.#.", ".#####..", ".######.", "##.####.", "##..###.", ".######.", "..####.."];
    // The second frame, the tips leaning the other way: lit flames swap between the two from Target.
    private static readonly string[] RetroFlame2 = ["....#...", "....##..", ".#.###..", "..#####.", ".######.", ".####.##", ".###..##", ".######.", "..####.."];

    private static class RetroTex
    {
        private static Texture2D Get(string name) => ContentFinder<Texture2D>.Get("UI/Netrun/Frame/Retro/" + name, reportFailure: false);

        public static readonly Texture2D Body = Get("Body"), Badge = Get("Badge"), Stripes = Get("Stripes"), Plate = Get("KeyPlate"), Pill = Get("Pill"),
            Screw = Get("Screw"), LampBezel = Get("LampBezel"), Glass = Get("Glass"), SideL = Get("SideL"), SideR = Get("SideR"), Wheel = Get("Wheel"),
            Ridges = Get("Ridges"), Caption = Get("Caption"), Lcd = Get("Lcd"), Round = Get("Round"), Slide = Get("Slide"), Grip = Get("Grip");

        public static bool Ready => Body != null;
    }

    /// <summary>A cream key or a round key: lights under the mouse, sinks while pressed, greys out when it does nothing here.</summary>
    private static bool RetroKey(Rect key, Texture2D tex, string tip, bool active, System.Action<Rect, Color> glyph, Color ink)
    {
        if (Event.current.type == EventType.Repaint)
        {
            bool over = active && Mouse.IsOver(key);
            bool down = over && _pressed;
            float k = !active ? 0.7f : down ? 0.85f : over ? 1.08f : 1f;
            var at = new Vector2(key.x, key.y + (down ? 1f : 0f));
            Sprite(tex, at, new Color(k, k, k));
            glyph?.Invoke(new Rect(at.x, at.y - 1f, key.width, key.height), active ? ink : new Color(ink.r, ink.g, ink.b, 0.35f));
        }
        return CasingHit(key, active, tip);
    }

    private static void RetroLamp(Rect glass, Color c, float a)
    {
        if (a <= 0f)
            return;
        Glow(glass.ContractedBy(2f), c, a * 0.6f, 5f);
        GUI.color = new Color(c.r, c.g, c.b, a);
        GUI.DrawTexture(glass, RetroTex.Glass);
        GUI.color = Color.white;
    }

    private static Rect RetroBezel(Rect outer, float heat, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu)
    {
        var box = new Rect(outer.x + RetroOut, outer.y + RetroOut, outer.width - 2f * RetroOut, outer.height - 2f * RetroOut);
        var screen = new Rect(box.x + RetroInL, box.y + RetroInT, box.width - RetroInL - RetroInR, box.height - RetroInT - RetroInB);
        bool paint = Event.current.type == EventType.Repaint;
        if (paint)
            DrawRetroCasing(box, screen, online);
        DrawRetroTop(box, online, anim, time, onKey, menu, paint);
        DrawRetroBottom(box, heat, level, online, anim, time, menu ? onKey : null, paint);
        return screen;
    }

    private static void DrawRetroCasing(Rect box, Rect screen, bool online)
    {
        NineSlice(box, RetroTex.Body, RetroSlice, RetroSlice, RetroSlice, RetroSlice);
        Sprite(RetroTex.Badge, new Vector2(box.x + 60f, box.y + 14f));

        // Screws at the ends of both rails, grip ridges low on them.
        float lc = box.x + 6f + RetroSideW / 2f, rc = box.xMax - 6f - RetroSideW / 2f;
        foreach (float cx in new[] { lc, rc })
        {
            Sprite(RetroTex.Screw, new Vector2(cx - 5f, box.y + RetroInT - 8f));
            Sprite(RetroTex.Screw, new Vector2(cx - 5f, screen.yMax + 4f));
        }
        float ridgesY = screen.yMax - 60f - RetroTex.Ridges.height;
        float sideY = screen.y + 26f;
        if (ridgesY > sideY + RetroTex.SideL.height + 20f)
        {
            Sprite(RetroTex.Ridges, new Vector2(box.x + 10f, ridgesY));
            Sprite(RetroTex.Ridges, new Vector2(box.xMax - 10f - RetroTex.Ridges.width, ridgesY));
        }
        else
            ridgesY = screen.yMax;

        // The control strips with the wheels peeking out of the case edges, POWER always lit, DATA with the net.
        if (sideY + RetroTex.SideL.height > ridgesY - 10f)
            return;
        Sprite(RetroTex.SideL, new Vector2(box.x + 6f, sideY));
        Sprite(RetroTex.SideR, new Vector2(box.xMax - 6f - RetroSideW, sideY));
        Sprite(RetroTex.Wheel, new Vector2(box.x - 5f, sideY + 14f));
        Sprite(RetroTex.Wheel, new Vector2(box.xMax + 5f - RetroTex.Wheel.width, sideY + 14f));
        RetroLamp(new Rect(lc - 4.5f, sideY + 196.5f, 9f, 9f), Bad, 1f);
        RetroLamp(new Rect(rc - 4.5f, sideY + 196.5f, 9f, 9f), Ok, online ? 1f : 0f);
        Tip(new Rect(rc - 10f, sideY + 190f, 20f, 36f), (online ? "NR_DeckRfOn" : "NR_DeckRfOff").Translate());
        float capY = sideY + 250f;
        if (capY + RetroTex.Caption.height < ridgesY - 10f)
            Sprite(RetroTex.Caption, new Vector2(rc - 7f, capY));
    }

    private static void DrawRetroTop(Rect box, bool online, bool anim, float time, System.Action<int> onKey, bool menu, bool paint)
    {
        float topY = box.y + 18f;
        float kx = box.xMax - RetroInR - 420f;
        float lx = box.xMax - RetroInR - 112f;
        var plate = new Rect(kx - 24f, topY - 6f, lx + 2f * 36f + 36f - (kx - 24f), 56f);
        if (paint)
        {
            // Sunset bars from the top edge down to the screen surround, between the badge and the plate, when they fit.
            float sx = kx - 40f - RetroTex.Stripes.width;
            if (sx >= box.x + 60f + RetroTex.Badge.width + 16f)
                Sprite(RetroTex.Stripes, new Vector2(sx, box.y));
            NineSlice(plate, RetroTex.Plate, 12f, 12f, 12f, 12f);
            Sprite(RetroTex.Screw, new Vector2(plate.x + 6f, plate.y + 12f));
            Sprite(RetroTex.Screw, new Vector2(plate.xMax - 16f, plate.y + 12f));
            float dv = lx - 30f;
            Widgets.DrawBoxSolid(new Rect(dv, plate.y + 8f, 2f, 38f), new Color(0.11f, 0.1f, 0.12f));
            Widgets.DrawBoxSolid(new Rect(dv + 2f, plate.y + 8f, 1f, 38f), new Color(0.38f, 0.37f, 0.41f));
        }

        // CLOSE, MENU, ACHIEV, NET-LOCK: cream keys, the lock glyph turns red while the colony is cut off.
        string[] names = ["CLOSE", "MENU", "ACHIEV", "NET-LOCK"];
        NodeDefense defense = Current.Game != null ? NodeDefense.Get() : null;
        bool isolated = defense?.Isolated == true;
        AcceptanceReport lockReport = defense?.CanToggleIsolation() ?? false;
        Color ink = new(0.24f, 0.22f, 0.24f);
        for (int i = 0; i < 4; i++)
        {
            var key = new Rect(kx + i * 72f, topY + 4f, 50f, 22f);
            bool active = i switch { 0 => onKey != null, 1 or 2 => onKey != null && menu, _ => defense != null && lockReport.Accepted };
            string tip = !Mouse.IsOver(key) ? null : i == 3
                ? (string)(isolated ? "NR_DeckKey_IsolateOff" : "NR_DeckKey_IsolateOn").Translate() + "\n\n" + "NR_IsolationGizmoDesc".Translate()
                  + (lockReport.Reason.NullOrEmpty() ? string.Empty : "\n\n" + lockReport.Reason)
                : !active && i > 0 ? (string)"NR_DeckKey_TerminalOnly".Translate() : (string)("NR_DeckKey_" + (KeyClose + i)).Translate();
            System.Action<Rect, Color> glyph = i switch
            {
                0 => (r, c) => GlyphText(r, Bad, "×", GameFont.Small),
                1 => (r, c) => GlyphTex(r.ContractedBy(3f), c, NetrunTex.MenuMain),
                2 => (r, c) => GlyphText(r, c, "★", GameFont.Tiny),
                _ => (r, c) => GlyphTex(r.ContractedBy(3f), isolated ? Bad : c, NetrunTex.NetworkLock),
            };
            if (paint && i == 3 && isolated && anim)
                Glow(key, Bad, 0.3f + 0.25f * Mathf.Abs(Mathf.Sin(time * 2f)), 5f);
            if (RetroKey(key, RetroTex.Pill, tip, active, glyph, ink))
            {
                if (i == 3)
                    defense?.ToggleIsolation();
                else
                    onKey?.Invoke(KeyClose + i);
            }
            if (paint)
                Stencil(new Rect(key.x - 11f, topY + 25f, 72f, 14f), names[i], TextAnchor.UpperCenter, RetroLight);
        }
        if (!paint || !NetrunMod.Settings.FrameLights)
            return;

        bool sick = Current.Game != null && Cached("deck.sick", () => PawnsFinder.AllMaps_FreeColonists.Any(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick)), 1f);
        bool pod = Current.Game != null && Cached("deck.pod", () => Find.Maps.Any(m => m.GetComponent<USH_HE.MapComponent_CyberpodManager>()?.Pods.Any(pp => pp.ContainedThing != null) == true), 1f);
        (bool on, Color c, bool blink, string tip, string label)[] lights =
        [
            (online, Ok, false, "NR_DeckLightNet".Translate(), "NET"),
            (sick, DeckOrange, true, "NR_DeckLightSick".Translate(), "SICK"),
            (pod, PodGreen, false, "NR_DeckLightPod".Translate(), "POD"),
        ];
        for (int i = 0; i < lights.Length; i++)
        {
            var bezel = new Rect(lx + i * 36f - 8f, topY + 7f, 16f, 16f);
            Sprite(RetroTex.LampBezel, bezel.position);
            float a = !lights[i].on ? 0f : lights[i].blink && anim ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * 3f)) : 1f;
            RetroLamp(bezel.ContractedBy(3f), lights[i].c, a);
            Stencil(new Rect(bezel.center.x - 20f, topY + 25f, 40f, 14f), lights[i].label, TextAnchor.UpperCenter, RetroLight);
            Tip(bezel.ExpandedBy(5f), lights[i].tip);
        }
    }

    private static void DrawRetroBottom(Rect box, float heat, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool paint)
    {
        float yb = box.yMax - RetroInB;
        float x = box.x + 70f;

        if (NetrunMod.Settings.FrameLcd)
        {
            var lcd = new Rect(x, yb + 26f, 240f, 60f);
            if (paint)
            {
                NineSlice(lcd, RetroTex.Lcd, 8f, 8f, 8f, 8f);
                string[] lines = Cached("deck.lcd", () => LcdLines(heat, level, online), 1f);
                // In the red zone the LCD flashes inverted for a moment every couple of seconds, like a handheld's game over.
                if (level >= HeatLevel.RedZone && anim && time * 0.45f % 1f < 0.18f)
                {
                    Widgets.DrawBoxSolid(lcd.ContractedBy(6f), RetroLcdInk);
                    Text.Font = GameFont.Medium;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(lcd, "!! ALERT !!".Colorize(RetroLcdOff));
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;
                }
                else
                {
                    Text.Font = GameFont.Tiny;
                    for (int i = 0; i < lines.Length; i++)
                        Widgets.Label(new Rect(lcd.x + 12f, lcd.y + 7f + i * 14.5f, lcd.width - 20f, 16f), lines[i].Colorize(RetroLcdInk));
                    Text.Font = GameFont.Small;
                }
                if (Mouse.IsOver(lcd))
                    Tip(lcd, "NR_DeckLcd".Translate());
            }
        }
        x += 280f;

        // BACK, CORE, HELP: round keys staggered like a handheld's A/B.
        string[] glyphs = ["◁", "●", "?"];
        string[] names = ["BACK", "CORE", "HELP"];
        int[] ids = [KeyBack, KeyCore, KeyHelp];
        bool active = onKey != null;
        for (int i = 0; i < 3; i++)
        {
            var key = new Rect(x + i * 52f, yb + 40f - (i % 2) * 10f, 38f, 38f);
            string tip = active && Mouse.IsOver(key) ? (string)("NR_DeckKey_" + ids[i]).Translate() : null;
            string g = glyphs[i];
            if (RetroKey(key, RetroTex.Round, tip, active, (r, c) => GlyphText(r, c, g, GameFont.Small), RetroLight))
                onKey?.Invoke(ids[i]);
            if (paint)
                StencilBold(new Rect(key.x - 11f, key.y + 42f, 60f, 14f), names[i], TextAnchor.UpperCenter, RetroInk);
        }
        if (!paint)
            return;

        // The Heat: five pixel flames on a second LCD, as many lit as the Heat level, when there is room before the switch.
        float sw = box.xMax - RetroInR - 230f;
        float vx = x + 186f;
        if (vx + 200f + 30f <= sw)
        {
            var win = new Rect(vx, yb + 26f, 200f, 60f);
            NineSlice(win, RetroTex.Lcd, 8f, 8f, 8f, 8f);
            int lit = (int)level + 1;
            bool dance = level >= HeatLevel.Target && anim;
            int beat = (int)(time * (level >= HeatLevel.RedZone ? 6f : 3f));
            for (int i = 0; i < 5; i++)
            {
                Color c = i < lit ? RetroLcdInk : RetroLcdOff;
                string[] f = dance && i < lit && (beat + i) % 2 == 1 ? RetroFlame2 : RetroFlame;
                for (int r = 0; r < f.Length; r++)
                    for (int col = 0; col < f[r].Length; col++)
                        if (f[r][col] == '#')
                            Widgets.DrawBoxSolid(new Rect(win.x + 12f + i * 37f + col * 4f, win.y + 12f + r * 4f, 4f, 4f), c);
            }
            StencilBold(new Rect(win.x, win.yMax + 6f, win.width, 14f), "HEAT", TextAnchor.UpperCenter, RetroInk);
            if (Mouse.IsOver(win))
                Tip(win, "NR_DeckHeatScale".Translate(heat.ToString("0"), level.Label()));
        }

        // NET LINK: a slide switch, the grip to the right while the colony is on the net.
        var slide = new Rect(sw, yb + 44f, RetroTex.Slide.width, RetroTex.Slide.height);
        GUI.DrawTexture(slide, RetroTex.Slide);
        Sprite(RetroTex.Grip, new Vector2(sw + (online ? 58f : 6f), yb + 47f));
        StencilBold(new Rect(sw, yb + 28f, 40f, 14f), "OFF", TextAnchor.UpperLeft, RetroInk);
        StencilBold(new Rect(slide.xMax - 40f, yb + 28f, 40f, 14f), "ON", TextAnchor.UpperRight, RetroInk);
        StencilBold(new Rect(sw - 7f, yb + 74f, slide.width + 14f, 14f), "NET LINK", TextAnchor.UpperCenter, RetroInk);
        if (Mouse.IsOver(slide))
            Tip(slide, (online ? "NR_DeckNetLinkOn" : "NR_DeckNetLinkOff").Translate());
    }
}
