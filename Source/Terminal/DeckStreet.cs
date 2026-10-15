using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The Street deck (Maxim's reference, docs/mockups/38b-street-yellow.png): a worn yellow panel with cut corners in a
/// black housing, a neon tube on top that burns with the Heat, keys with neon outlines, a holographic kitsune over the
/// right edge, 夜の走者 on a sprayed band, graffiti. Every part is a texture from tools/gen_deck_street.py; the layout
/// numbers mirror that script. The keys and readouts work as on the classic deck.
/// </summary>
public static partial class NetrunUI
{
    private const float StreetOutL = 16f, StreetOutT = 16f, StreetOutR = 30f, StreetOutB = 12f;
    private const float StreetInL = 34f, StreetInT = 86f, StreetInR = 74f, StreetInB = 66f;
    private const float StreetSlice = 150f;
    public static readonly Vector2 StreetExtra = new(StreetOutL + StreetOutR + StreetInL + StreetInR, StreetOutT + StreetOutB + StreetInT + StreetInB);

    private static readonly Color StreetCyan = Hex(0x3fe0e6), StreetPink = Hex(0xff4f9e), StreetRed = Hex(0xe8384f);
    private static readonly Color StreetInk = Hex(0x1e1a0e);
    private static readonly float[] StreetKeyW = [82f, 82f, 92f, 104f];
    private static float _streetKnob = -1f;

    private static class StreetTex
    {
        private static Texture2D Get(string name) => ContentFinder<Texture2D>.Get("UI/Netrun/Frame/Street/" + name, reportFailure: false);

        public static readonly Texture2D Panel = Get("Panel"), Neon = Get("Neon"), Plate = Get("Plate"), Key = Get("Key"), KeyPink = Get("Key_Pink"),
            KeyCyan = Get("Key_Cyan"), KeyRed = Get("Key_Red"), Meter = Get("Meter"), LedOff = Get("Led_Off"), LedGreen = Get("Led_Green"),
            LedCyan = Get("Led_Cyan"), LedOrange = Get("Led_Orange"), Lcd = Get("Lcd"), SwitchPlate = Get("SwitchPlate"), Knob = Get("Knob"),
            Kanji = Get("Kanji"), Slashes = Get("Slashes"), Kitsune = Get("St_Kitsune"), Skull = Get("St_Skull"),
            GraffitiTop = Get("Graffiti_Top"), GraffitiBottom = Get("Graffiti_Bottom");

        public static bool Ready => Panel != null;
    }

    /// <summary>A 9-sliced key that sinks while pressed and dims when it does nothing here, with its label on it.</summary>
    private static bool StreetKey(Rect key, Texture2D tex, string label, Color ink, string tip, bool active, GameFont font = GameFont.Small)
    {
        if (Event.current.type == EventType.Repaint)
        {
            bool over = active && Mouse.IsOver(key);
            bool down = over && _pressed;
            Rect face = down ? new Rect(key.x, key.y + 1f, key.width, key.height) : key;
            float k = !active ? 0.6f : down ? 0.82f : over ? 1.15f : 1f;
            GUI.color = new Color(k, k, k);
            NineSlice(face, tex, 10f, 10f, 10f, 10f);
            GUI.color = Color.white;
            Text.Font = font;
            Text.Anchor = TextAnchor.MiddleCenter;
            Color c = !active ? new Color(ink.r, ink.g, ink.b, 0.35f) : over ? Color.Lerp(ink, Color.white, 0.4f) : ink;
            Widgets.Label(new Rect(face.x, face.y, face.width, face.height - 3f), label.Colorize(c));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        return CasingHit(key, active, tip);
    }

    private static Rect StreetBezel(Rect outer, float heat, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu)
    {
        var box = new Rect(outer.x + StreetOutL, outer.y + StreetOutT, outer.width - StreetOutL - StreetOutR, outer.height - StreetOutT - StreetOutB);
        var screen = new Rect(box.x + StreetInL, box.y + StreetInT, box.width - StreetInL - StreetInR, box.height - StreetInT - StreetInB);
        bool paint = Event.current.type == EventType.Repaint;
        if (paint)
            DrawStreetCasing(box, heat, level, anim, time);
        DrawStreetTop(box, level, online, anim, time, onKey, menu, paint);
        DrawStreetBottom(box, heat, level, online, menu ? onKey : null, paint);
        return screen;
    }

    private static void DrawStreetCasing(Rect box, float heat, HeatLevel level, bool anim, float time)
    {
        NineSlice(box, StreetTex.Panel, StreetSlice, StreetSlice, StreetSlice, StreetSlice);

        // The neon tube in its housing over the top edge: lit to the Heat, cyan, pink from Tracked, red in the Red Zone.
        float nx0 = box.x + Mathf.Round(box.width * 0.52f), nx1 = box.xMax - StreetInR - 14f;
        if (nx1 - nx0 >= 140f)
        {
            NineSlice(new Rect(nx0, box.y - 14f, nx1 - nx0, StreetTex.Neon.height), StreetTex.Neon, 48f, 0f, 48f, 0f);
            var tube = new Rect(nx0 + 46f, box.y - 14f + 14f, nx1 - nx0 - 92f, 8f);
            Widgets.DrawBoxSolid(tube, Hex(0x1a161e));
            float frac = Mathf.Clamp01(heat / HeatTracker.MaxHeat);
            Color c = level >= HeatLevel.RedZone ? StreetRed : level >= HeatLevel.Tracked ? StreetPink : StreetCyan;
            if (frac > 0.005f)
            {
                var lit = new Rect(tube.x, tube.y, Mathf.Max(4f, tube.width * frac), tube.height);
                float pulse = level >= HeatLevel.Target && anim ? 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(time * 3f)) : 1f;
                Glow(lit, c, 0.55f * pulse, 7f);
                Widgets.DrawBoxSolid(lit, new Color(c.r, c.g, c.b, pulse));
                Widgets.DrawBoxSolid(new Rect(lit.x + 3f, lit.y + 2f, Mathf.Max(0f, lit.width - 6f), 2f), new Color(1f, 1f, 1f, 0.65f * pulse));
            }
            if (Mouse.IsOver(tube.ExpandedBy(6f)))
                Tip(tube.ExpandedBy(6f), "NR_DeckHeatScale".Translate(heat.ToString("0"), level.Label()));

            // The maker's plate on the left, graffiti between it and the tube.
            float plateEnd = box.x + 33f;
            if (nx0 - 10f - (box.x + 33f) >= StreetTex.Plate.width)
            {
                Sprite(StreetTex.Plate, new Vector2(box.x + 33f, box.y + 8f));
                plateEnd = box.x + 33f + StreetTex.Plate.width;
            }
            float room = nx0 - 6f - (plateEnd + 10f);
            if (room >= StreetTex.GraffitiTop.width)
                Sprite(StreetTex.GraffitiTop, new Vector2(Mathf.Round(plateEnd + 10f + (room - StreetTex.GraffitiTop.width) / 2f), box.y + 2f));
        }

        // The right column: 夜の走者 on its band, three slashes below the middle, a skull at the bottom, the kitsune on top.
        float kanjiY = box.y + 104f;
        bool kanji = kanjiY + StreetTex.Kanji.height <= box.yMax - StreetInB - 70f;
        if (kanji)
            Sprite(StreetTex.Kanji, new Vector2(box.xMax - 62f, kanjiY));
        float slashY = box.y + Mathf.Round(box.height * 0.6f);
        if ((!kanji || slashY >= kanjiY + StreetTex.Kanji.height + 12f) && slashY + StreetTex.Slashes.height <= box.yMax - 70f)
            Sprite(StreetTex.Slashes, new Vector2(box.xMax - 58f, slashY));
        Sprite(StreetTex.Skull, new Vector2(box.xMax - 58f, box.yMax - 62f));
        Sprite(StreetTex.Kitsune, new Vector2(box.xMax - 78f, box.y - 4f));
    }

    private static void DrawStreetTop(Rect box, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu, bool paint)
    {
        NetrunSettings set = NetrunMod.Settings;
        float y = box.y + 41f;

        // CLOSE (pink), MENU, ACHIEV (cyan), NET-LOCK (red while the colony is cut off).
        string[] names = ["CLOSE", "MENU", "ACHIEV", "NET-LOCK"];
        NodeDefense defense = Current.Game != null ? NodeDefense.Get() : null;
        bool isolated = defense?.Isolated == true;
        AcceptanceReport lockReport = defense?.CanToggleIsolation() ?? false;
        float x = box.x + 34f;
        for (int i = 0; i < 4; i++)
        {
            var key = new Rect(x, y, StreetKeyW[i], 32f);
            x += StreetKeyW[i] + 10f;
            bool active = i switch { 0 => onKey != null, 1 or 2 => onKey != null && menu, _ => defense != null && lockReport.Accepted };
            string tip = !Mouse.IsOver(key) ? null : i == 3
                ? (string)(isolated ? "NR_DeckKey_IsolateOff" : "NR_DeckKey_IsolateOn").Translate() + "\n\n" + "NR_IsolationGizmoDesc".Translate()
                  + (lockReport.Reason.NullOrEmpty() ? string.Empty : "\n\n" + lockReport.Reason)
                : !active && i > 0 ? (string)"NR_DeckKey_TerminalOnly".Translate() : (string)("NR_DeckKey_" + (KeyClose + i)).Translate();
            Texture2D tex = i switch { 0 => StreetTex.KeyPink, 2 => StreetTex.KeyCyan, 3 when isolated => StreetTex.KeyRed, _ => StreetTex.Key };
            Color ink = i switch { 0 => Hex(0xff7ab4), 2 => Hex(0x7aebf0), 3 when isolated => Hex(0xff8a96), _ => Hex(0xdcdcdc) };
            if (paint && i == 3 && isolated && anim)
                Glow(key, StreetRed, 0.35f + 0.25f * Mathf.Abs(Mathf.Sin(time * 2f)), 6f);
            if (StreetKey(key, tex, names[i], ink, tip, active))
            {
                if (i == 3)
                    defense?.ToggleIsolation();
                else
                    onKey?.Invoke(KeyClose + i);
            }
        }
        if (!paint)
            return;

        // The RF meter after the keys, the three lamps after it; both give way when the window is narrow.
        float right = box.xMax - StreetInR - 10f;
        float mx = x + 2f;
        float mw = Mathf.Min(230f, right - mx - (set.FrameLights ? 200f : 0f));
        if (mw >= 140f)
        {
            var meter = new Rect(mx, box.y + 40f, mw, 34f);
            NineSlice(meter, StreetTex.Meter, 10f, 10f, 10f, 10f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(meter.x + 9f, meter.y + 15f, 60f, 16f), "RF signal".Colorize(Hex(0x5adce6)));
            Text.Font = GameFont.Small;
            for (int i = 0; i < 4; i++)
            {
                float h = 3f + i * 2.5f;
                Widgets.DrawBoxSolid(new Rect(meter.x + 12f + i * 5f, meter.y + 13f - h, 3f, h), Hex(0x5adce6));
            }
            int bars = Mathf.FloorToInt((mw - 76f) / 8.5f);
            int lit = online ? Mathf.RoundToInt(_rf / 6f * bars) : 0;
            bool hot = level >= HeatLevel.Target;
            for (int i = 0; i < bars; i++)
            {
                Color c = hot && i == bars - 1 ? Bad : i < bars * 0.55f ? StreetCyan : Hex(0x5ae682);
                var bar = new Rect(meter.x + 66f + i * 8.5f, meter.y + 7f, 5f, 20f);
                bool on = i < lit || hot && i == bars - 1 && online;
                Widgets.DrawBoxSolid(bar, on ? c : new Color(c.r, c.g, c.b, 0.15f));
            }
            Tip(meter, (online ? "NR_DeckRfOn" : "NR_DeckRfOff").Translate());
            mx = meter.xMax + 20f;
        }

        if (set.FrameLights && right - mx >= 180f)
        {
            bool sick = Current.Game != null && Cached("deck.sick", () => PawnsFinder.AllMaps_FreeColonists.Any(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick)), 1f);
            bool pod = Current.Game != null && Cached("deck.pod", () => Find.Maps.Any(m => m.GetComponent<USH_HE.MapComponent_CyberpodManager>()?.Pods.Any(pp => pp.ContainedThing != null) == true), 1f);
            (bool on, Color c, Texture2D tex, bool blink, string tip, string label)[] lights =
            [
                (online, StreetCyan, StreetTex.LedCyan, false, "NR_DeckLightNet".Translate(), "NET"),
                (sick, DeckOrange, StreetTex.LedOrange, true, "NR_DeckLightSick".Translate(), "SICK"),
                (pod, PodGreen, StreetTex.LedGreen, false, "NR_DeckLightPod".Translate(), "POD"),
            ];
            for (int i = 0; i < lights.Length; i++)
            {
                float cx = mx + 26f + i * 60f;
                var l = new Rect(cx - 6f, box.y + 41f, 12f, 12f);
                float a = !lights[i].on ? 0f : lights[i].blink && anim ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * 3f)) : 1f;
                GUI.DrawTexture(l, StreetTex.LedOff);
                if (a > 0f)
                {
                    Glow(l.ContractedBy(3f), lights[i].c, a * 0.7f, 5f);
                    GUI.color = new Color(1f, 1f, 1f, a);
                    GUI.DrawTexture(l, lights[i].tex);
                    GUI.color = Color.white;
                }
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(new Rect(cx - 25f, box.y + 55f, 50f, 18f), lights[i].label.Colorize(StreetInk));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                Tip(new Rect(cx - 20f, box.y + 38f, 40f, 34f), lights[i].tip);
            }
        }
    }

    private static void DrawStreetBottom(Rect box, float heat, HeatLevel level, bool online, System.Action<int> onKey, bool paint)
    {
        float by = box.yMax - StreetInB;
        bool active = onKey != null;

        // < O ? on the right.
        float bx = box.xMax - StreetInR - 3f * 54f + 6f;
        string[] glyphs = ["<", "O", "?"];
        int[] ids = [KeyBack, KeyCore, KeyHelp];
        for (int i = 0; i < 3; i++)
        {
            var b = new Rect(bx + i * 54f, by + 20f, 46f, 34f);
            string tip = active && Mouse.IsOver(b) ? (string)("NR_DeckKey_" + ids[i]).Translate() : null;
            if (StreetKey(b, StreetTex.Key, glyphs[i], Hex(0xe1e1e1), tip, active, GameFont.Medium))
                onKey?.Invoke(ids[i]);
        }
        if (!paint)
            return;

        // NET LINK: the knob glides along its slot between OFF and ON.
        var plate = new Rect(box.x + 34f, by + 13f, StreetTex.SwitchPlate.width, StreetTex.SwitchPlate.height);
        GUI.DrawTexture(plate, StreetTex.SwitchPlate);
        float target = online ? 1f : 0f;
        _streetKnob = _streetKnob < 0f || !NetrunMod.Settings.Animations ? target : Mathf.MoveTowards(_streetKnob, target, Time.unscaledDeltaTime * 2.5f);
        float kx = Mathf.Round(plate.x + Mathf.Lerp(39f, 89f, _streetKnob) - StreetTex.Knob.width / 2f);
        Sprite(StreetTex.Knob, new Vector2(kx, plate.y + 14f));
        if (online)
            Glow(new Rect(plate.xMax - 24f, plate.y + 32f, 6f, 4f), PodGreen, 0.4f, 4f);
        if (Mouse.IsOver(plate))
            Tip(plate, (online ? "NR_DeckNetLinkOn" : "NR_DeckNetLinkOff").Translate());

        // The status LCD between them, graffiti in what is left.
        float lcdX = plate.xMax + 20f;
        float lcdEnd = lcdX;
        if (NetrunMod.Settings.FrameLcd)
        {
            float lw = Mathf.Min(520f, bx - 24f - lcdX);
            if (lw >= 170f)
            {
                var lcd = new Rect(lcdX, by + 5f, lw, 54f);
                NineSlice(lcd, StreetTex.Lcd, 12f, 12f, 12f, 12f);
                DrawLcd(lcd.ContractedBy(6f), heat, level, online, recess: false);
                lcdEnd = lcd.xMax;
            }
        }
        float room = bx - 8f - (lcdEnd + 8f);
        Texture2D g = StreetTex.GraffitiBottom;
        if (room >= g.width)
            Sprite(g, new Vector2(Mathf.Round(lcdEnd + 8f + (room - g.width) / 2f), Mathf.Round(by + (StreetInB - g.height) / 2f)));
    }
}
