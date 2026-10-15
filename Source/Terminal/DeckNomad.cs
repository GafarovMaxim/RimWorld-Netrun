using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The Nomad deck (docs/mockups/37g-theme-nomad.png): a dusty sand road case with a solar panel opened behind it, a
/// paracord handle below, a battery pouch on straps to the right and a bone charm on the left. Every part is a texture
/// from tools/gen_deck_nomad.py; the layout numbers mirror that script. The keys and readouts work as on the classic deck.
/// </summary>
public static partial class NetrunUI
{
    // Where the case sits in the window (room for the protruding parts) and where the screen sits in the case.
    private const float NomadOutL = 42f, NomadOutT = 50f, NomadOutR = 80f, NomadOutB = 32f;
    private const float NomadInL = 24f, NomadInT = 60f, NomadInR = 24f, NomadInB = 72f;
    private const float NomadSlice = 170f;
    public static readonly Vector2 NomadExtra = new(NomadOutL + NomadOutR + NomadInL + NomadInR, NomadOutT + NomadOutB + NomadInT + NomadInB);

    private static readonly Color NomadInk = Hex(0xd9c89c);
    private static float _nomadLever = -1f;
    private static readonly Color NomadRedFace = Hex(0x6a2a20);

    /// <summary>The textures, loaded on first use.</summary>
    private static class NomadTex
    {
        private static Texture2D Get(string name) => ContentFinder<Texture2D>.Get("UI/Netrun/Frame/Nomad/" + name, reportFailure: false);

        public static readonly Texture2D Case = Get("Case"), Solar = Get("Solar"), Hinge = Get("Hinge"), Handle = Get("Handle"),
            Pouch = Get("Pouch"), Charm = Get("Charm"), Key = Get("Key"), KeyRed = Get("Key_Red"), LedOff = Get("Led_Off"),
            LedGreen = Get("Led_Green"), LedOrange = Get("Led_Orange"), Window = Get("Window"), Lcd = Get("Lcd"), Patch = Get("Patch"),
            Dpad = Get("Dpad"), Core = Get("Core"), Strap = Get("Strap"), Rivet = Get("Rivet"), Gem = Get("Gem"),
            LeverPlate = Get("LeverPlate"), Tally = Get("Tally"), Stencil = Get("Stencil");

        /// <summary>The lever from OFF (0, tilted left) to ON (6, tilted right).</summary>
        public static readonly Texture2D[] Lever = Enumerable.Range(0, 7).Select(i => Get("Lever_" + i)).ToArray();

        public static bool Ready => Case != null;
    }

    /// <summary>A texture cut in nine: corners kept, edges stretched along their length, the middle stretched.</summary>
    private static void NineSlice(Rect r, Texture2D tex, float l, float t, float right, float b)
    {
        float w = tex.width, h = tex.height;
        float dl = Mathf.Min(l, r.width / 2f), dr = Mathf.Min(right, r.width / 2f), dt = Mathf.Min(t, r.height / 2f), db = Mathf.Min(b, r.height / 2f);
        // Seams land on whole device pixels through the full GUI matrix (UI scale and the terminal's own content scale,
        // which carries a fractional offset), and each inner slice reaches one device pixel under its right and lower
        // neighbour, so the casing behind never shows through a seam.
        Matrix4x4 m = GUI.matrix;
        float sx = Mathf.Max(0.01f, m.m00), sy = Mathf.Max(0.01f, m.m11);
        float[] xs = [Snap(r.x, sx, m.m03), Snap(r.x + dl, sx, m.m03), Snap(r.xMax - dr, sx, m.m03), Snap(r.xMax, sx, m.m03)];
        float[] us = [0f, l / w, 1f - right / w, 1f];
        float[] ys = [Snap(r.y, sy, m.m13), Snap(r.y + dt, sy, m.m13), Snap(r.yMax - db, sy, m.m13), Snap(r.yMax, sy, m.m13)];
        // GUI texture coordinates start at the bottom.
        float[] vs = [1f, 1f - t / h, b / h, 0f];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                var dst = new Rect(xs[i], ys[j], xs[i + 1] - xs[i], ys[j + 1] - ys[j]);
                if (dst.width <= 0.01f || dst.height <= 0.01f)
                    continue;
                if (i < 2 && xs[i + 2] - xs[i + 1] > 0.01f)
                    dst.width += 1f / sx;
                if (j < 2 && ys[j + 2] - ys[j + 1] > 0.01f)
                    dst.height += 1f / sy;
                GUI.DrawTextureWithTexCoords(dst, tex, new Rect(us[i], vs[j + 1], us[i + 1] - us[i], vs[j] - vs[j + 1]));
            }
    }

    private static float Snap(float v, float scale, float offset) => (Mathf.Round(v * scale + offset) - offset) / scale;

    /// <summary>A texture at its own size, its top-left at <paramref name="at"/>.</summary>
    private static Rect Sprite(Texture2D tex, Vector2 at, Color? tint = null)
    {
        var r = new Rect(at.x, at.y, tex.width, tex.height);
        GUI.color = tint ?? Color.white;
        GUI.DrawTexture(r, tex);
        GUI.color = Color.white;
        return r;
    }

    /// <summary>A click zone of the casing with its tooltip; true when clicked (any event but Repaint).</summary>
    private static bool CasingHit(Rect r, bool active, string tip)
    {
        if (tip != null && Mouse.IsOver(r))
            Tip(r, tip);
        if (active && Widgets.ButtonInvisible(r, doMouseoverSound: false))
        {
            NetrunSound.Click();
            return true;
        }
        return false;
    }

    /// <summary>A textured key that sinks while pressed and dims when it does nothing here.</summary>
    private static bool TexturedKey(Rect key, Texture2D tex, string tip, bool active, System.Action<Rect, Color> glyph, Color ink)
    {
        if (Event.current.type == EventType.Repaint)
        {
            bool over = active && Mouse.IsOver(key);
            bool down = over && _pressed;
            Rect face = down ? new Rect(key.x, key.y + 1f, key.width, key.height) : key;
            float k = !active ? 0.55f : down ? 0.82f : 1f;
            GUI.color = new Color(k, k, k);
            GUI.DrawTexture(face, tex);
            if (over && !down)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.12f);
                GUI.DrawTexture(face, tex);
            }
            GUI.color = Color.white;
            var glyphRect = new Rect(face.x, face.y, face.width, face.height - 4f);
            glyph(glyphRect, active ? (over ? Color.Lerp(ink, Color.white, 0.4f) : ink) : new Color(ink.r, ink.g, ink.b, 0.3f));
        }
        return CasingHit(key, active, tip);
    }

    private static Rect NomadBezel(Rect outer, float heat, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu)
    {
        var box = new Rect(outer.x + NomadOutL, outer.y + NomadOutT, outer.width - NomadOutL - NomadOutR, outer.height - NomadOutT - NomadOutB);
        var screen = new Rect(box.x + NomadInL, box.y + NomadInT, box.width - NomadInL - NomadInR, box.height - NomadInT - NomadInB);
        bool paint = Event.current.type == EventType.Repaint;
        if (paint)
            DrawNomadCasing(outer, box);
        DrawNomadTop(box, level, online, anim, time, onKey, menu, paint);
        DrawNomadBottom(box, level, heat, online, menu ? onKey : null, paint);
        return screen;
    }

    private static void DrawNomadCasing(Rect outer, Rect box)
    {
        // Behind the case: the solar panel standing up from the top edge and the handle under the bottom one.
        NineSlice(new Rect(box.x + 40f, outer.y, box.width - 80f, NomadTex.Solar.height), NomadTex.Solar, 90f, 0f, 90f, 0f);
        Sprite(NomadTex.Handle, new Vector2(Mathf.Round(box.center.x - NomadTex.Handle.width / 2f), box.yMax - 18f));
        NineSlice(box, NomadTex.Case, NomadSlice, NomadSlice, NomadSlice, NomadSlice);
        foreach (float hx in new[] { box.x + 110f, box.xMax - 156f })
            Sprite(NomadTex.Hinge, new Vector2(hx, box.y - 8f));
        // The charm hangs from an eyelet on the left edge; the pouch is strapped over the right edge.
        Sprite(NomadTex.Charm, new Vector2(box.x - 40f, box.y + 30f));
        float pouchY = Mathf.Clamp(box.center.y - NomadTex.Pouch.height / 2f, box.y + NomadInT, Mathf.Max(box.y + NomadInT, box.yMax - NomadInB - NomadTex.Pouch.height));
        Sprite(NomadTex.Pouch, new Vector2(box.xMax - 26f, pouchY));
    }

    private static void DrawNomadTop(Rect box, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu, bool paint)
    {
        NetrunSettings set = NetrunMod.Settings;
        float y = box.y + 10f;

        // CLOSE, MENU, ACHIEV, NET LOCK: worn rubber keys (red for close and for the lock while the colony is cut off).
        string[] names = ["CLOSE", "MENU", "ACHIEV", "NET LOCK"];
        NodeDefense defense = Current.Game != null ? NodeDefense.Get() : null;
        bool isolated = defense?.Isolated == true;
        AcceptanceReport lockReport = defense?.CanToggleIsolation() ?? false;
        for (int i = 0; i < 4; i++)
        {
            var key = new Rect(box.x + 34f + i * 54f, y, 44f, 32f);
            bool red = i == 0 || i == 3 && isolated;
            bool active = i switch { 0 => onKey != null, 1 or 2 => onKey != null && menu, _ => defense != null && lockReport.Accepted };
            string tip = !Mouse.IsOver(key) ? null : i == 3
                ? (string)(isolated ? "NR_DeckKey_IsolateOff" : "NR_DeckKey_IsolateOn").Translate() + "\n\n" + "NR_IsolationGizmoDesc".Translate()
                  + (lockReport.Reason.NullOrEmpty() ? string.Empty : "\n\n" + lockReport.Reason)
                : !active && i > 0 ? (string)"NR_DeckKey_TerminalOnly".Translate() : (string)("NR_DeckKey_" + (KeyClose + i)).Translate();
            int k = i;
            System.Action<Rect, Color> glyph = k switch
            {
                0 => (r, c) => GlyphPower(r, c, NomadRedFace),
                1 => (r, c) => GlyphTex(r, c, NetrunTex.MenuMain),
                2 => (r, c) => GlyphText(r, c, "★"),
                _ => (r, c) => GlyphTex(r, isolated ? Color.white : c, NetrunTex.NetworkLock),
            };
            if (paint && i == 3 && isolated && anim)
                Glow(key, Bad, 0.35f + 0.25f * Mathf.Abs(Mathf.Sin(time * 2f)), 6f);
            if (TexturedKey(key, red ? NomadTex.KeyRed : NomadTex.Key, tip, active, glyph, NomadInk))
            {
                if (i == 3)
                    defense?.ToggleIsolation();
                else
                    onKey?.Invoke(KeyClose + i);
            }
            if (paint)
                Stencil(new Rect(key.x - 10f, y + 33f, key.width + 20f, 14f), names[i]);
        }
        if (!paint)
            return;

        // From the right: RF in a brass window, the three lamps; the tuner after the keys; the leather patch between.
        var rf = new Rect(box.xMax - 34f - 64f, y, 64f, 32f);
        NineSlice(rf, NomadTex.Window, 10f, 10f, 10f, 10f);
        bool hot = level >= HeatLevel.Target;
        for (int i = 0; i < 6; i++)
        {
            float h = 5f + i * 2.6f;
            var bar = new Rect(rf.x + 9f + i * 8f, rf.yMax - 7f - h, 5f, h);
            bool lit = online && (i < Mathf.RoundToInt(_rf) || i == 5 && hot);
            Color c = i == 5 && hot ? Bad : Ok;
            if (lit && anim)
                Glow(bar, c, 0.3f, 3f);
            Widgets.DrawBoxSolid(bar, lit ? new Color(c.r, c.g, c.b, 0.9f) : new Color(c.r, c.g, c.b, 0.12f));
        }
        Stencil(new Rect(rf.x - 10f, y + 33f, rf.width + 20f, 14f), "RF SIGNAL");
        Tip(rf, (online ? "NR_DeckRfOn" : "NR_DeckRfOff").Translate());
        float right = rf.x - 20f;

        if (set.FrameLights)
        {
            bool sick = Current.Game != null && Cached("deck.sick", () => PawnsFinder.AllMaps_FreeColonists.Any(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick)), 1f);
            bool pod = Current.Game != null && Cached("deck.pod", () => Find.Maps.Any(m => m.GetComponent<USH_HE.MapComponent_CyberpodManager>()?.Pods.Any(pp => pp.ContainedThing != null) == true), 1f);
            (bool on, Color c, Texture2D tex, bool blink, string tip, string label)[] lights =
            [
                (online, Ok, NomadTex.LedGreen, false, "NR_DeckLightNet".Translate(), "NET"),
                (sick, DeckOrange, NomadTex.LedOrange, true, "NR_DeckLightSick".Translate(), "SICK"),
                (pod, PodGreen, NomadTex.LedGreen, false, "NR_DeckLightPod".Translate(), "POD"),
            ];
            float sx = right - lights.Length * 34f;
            for (int i = 0; i < lights.Length; i++)
            {
                var l = new Rect(sx + 6f + i * 34f, y + 5f, 22f, 22f);
                float a = !lights[i].on ? 0f : lights[i].blink && anim ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * 3f)) : 1f;
                GUI.DrawTexture(l, NomadTex.LedOff);
                if (a > 0f)
                {
                    Glow(l.ContractedBy(5f), lights[i].c, a * 0.7f, 6f);
                    GUI.color = new Color(1f, 1f, 1f, a);
                    GUI.DrawTexture(l, lights[i].tex);
                    GUI.color = Color.white;
                }
                Stencil(new Rect(l.center.x - 17f, y + 33f, 34f, 14f), lights[i].label);
                Tip(l.ExpandedBy(6f), lights[i].tip);
            }
            right = sx - 16f;
        }

        float x = box.x + 34f + 4f * 54f + 12f;
        float tunerW = Mathf.Min(170f, right - x - 170f);
        if (tunerW >= 90f)
        {
            var tuner = new Rect(x, y, tunerW, 32f);
            NineSlice(tuner, NomadTex.Window, 10f, 10f, 10f, 10f);
            for (int i = 0; i <= 20; i++)
            {
                float tx = tuner.x + 8f + (tuner.width - 16f) * i / 20f;
                float th = i % 5 == 0 ? 10f : 5f;
                Widgets.DrawBoxSolid(new Rect(tx, tuner.yMax - 6f - th, 1f, th), new Color(NomadInk.r, NomadInk.g, NomadInk.b, i % 5 == 0 ? 0.45f : 0.22f));
            }
            float mark = tuner.x + 8f + (tuner.width - 16f) * _tuner;
            Glow(new Rect(mark - 1f, tuner.y + 5f, 2f, tuner.height - 10f), DeckOrange, 0.45f, 4f);
            Widgets.DrawBoxSolid(new Rect(mark - 1f, tuner.y + 5f, 2f, tuner.height - 10f), DeckOrange);
            Stencil(new Rect(tuner.x, y + 33f, tuner.width, 14f), "TUNE  88.2 - 107.9");
            Tip(tuner, "NR_DeckTuner".Translate());
            x = tuner.xMax + 16f;
        }
        float room = right - x;
        if (room >= 150f)
        {
            float pw = Mathf.Min(240f, room);
            var patch = new Rect(Mathf.Round(x + (room - pw) / 2f), box.y + 6f, pw, 48f);
            NineSlice(patch, NomadTex.Patch, 16f, 16f, 16f, 16f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(patch.x, patch.y + 6f, patch.width, 18f), "NOMAD // ROAD UNIT".Colorize(NomadInk));
            Widgets.Label(new Rect(patch.x, patch.y + 22f, patch.width, 16f), "RANGER 13 · BLACKTOP CLAN".Colorize(new Color(NomadInk.r, NomadInk.g, NomadInk.b, 0.5f)));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }

    private static void DrawNomadBottom(Rect box, HeatLevel level, float heat, bool online, System.Action<int> onKey, bool paint)
    {
        float by = box.yMax - NomadInB;
        float x = box.x + 34f;

        if (NetrunMod.Settings.FrameLcd)
        {
            var lcd = new Rect(x, by + 8f, 212f, 56f);
            if (paint)
            {
                NineSlice(lcd, NomadTex.Lcd, 12f, 12f, 12f, 12f);
                DrawLcd(lcd.ContractedBy(7f), heat, level, online, recess: false);
            }
            x = lcd.xMax + 24f;
        }

        // The cross pad (compact, inside the bar): back on the left arm, core in the middle, the Cyberpedia on the right.
        var pad = new Rect(x, by + 12f, 72f, 48f);
        var back = new Rect(pad.x, pad.y + 15f, 26f, 18f);
        var help = new Rect(pad.xMax - 26f, pad.y + 15f, 26f, 18f);
        var core = new Rect(pad.x + 26f, pad.y + 14f, 20f, 20f);
        bool active = onKey != null;
        if (paint)
        {
            GUI.DrawTexture(pad, NomadTex.Dpad);
            foreach (Rect arm in new[] { back, help })
                if (active && Mouse.IsOver(arm))
                    Widgets.DrawBoxSolid(arm.ContractedBy(2f), _pressed ? new Color(0f, 0f, 0f, 0.3f) : new Color(1f, 1f, 1f, 0.06f));
            Color ink = active ? NomadInk : new Color(NomadInk.r, NomadInk.g, NomadInk.b, 0.3f);
            GlyphText(back, ink, "◁", GameFont.Small);
            GlyphText(help, ink, "?", GameFont.Small);
            bool coreDown = active && Mouse.IsOver(core) && _pressed;
            float k = !active ? 0.55f : coreDown ? 0.8f : Mouse.IsOver(core) ? 1.12f : 1f;
            Sprite(NomadTex.Core, new Vector2(core.x, core.y + (coreDown ? 1f : 0f)), new Color(k, k, k));
            string[] padLabels = ["◁ BACK", "● CORE", "? HELP"];
            for (int i = 0; i < 3; i++)
                Stencil(new Rect(pad.xMax + 8f, by + 13f + i * 15f, 60f, 14f), padLabels[i], TextAnchor.UpperLeft);
        }
        string Tip3(int i, Rect r) => active && Mouse.IsOver(r) ? (string)("NR_DeckKey_" + i).Translate() : null;
        if (CasingHit(back, active, Tip3(KeyBack, back)))
            onKey?.Invoke(KeyBack);
        if (CasingHit(core, active, Tip3(KeyCore, core)))
            onKey?.Invoke(KeyCore);
        if (CasingHit(help, active, Tip3(KeyHelp, help)))
            onKey?.Invoke(KeyHelp);
        if (!paint)
            return;

        // NET LINK: a brass lever in a slot, right; the Heat strap with its ten rivets between.
        var plate = new Rect(box.xMax - 34f - 160f, by + 9f, 160f, 54f);
        GUI.DrawTexture(plate, NomadTex.LeverPlate);
        // The lever glides along the slot and leans the way it travels (Maxim, 2026-10-15).
        float target = online ? 1f : 0f;
        _nomadLever = _nomadLever < 0f || !NetrunMod.Settings.Animations ? target : Mathf.MoveTowards(_nomadLever, target, Time.unscaledDeltaTime * 2.5f);
        var pivot = new Vector2(Mathf.Round(Mathf.Lerp(plate.x + 32f, plate.x + 128f, _nomadLever)), plate.y + 30f);
        Texture2D lever = NomadTex.Lever[Mathf.Clamp(Mathf.RoundToInt(_nomadLever * 6f), 0, 6)];
        if (lever != null)
            Sprite(lever, new Vector2(pivot.x - 30f, pivot.y - 48f));
        if (online)
            Glow(new Rect(plate.xMax - 22f, plate.y + 40f, 10f, 6f), PodGreen, 0.35f, 5f);
        if (Mouse.IsOver(plate))
            Tip(plate, (online ? "NR_DeckNetLinkOn" : "NR_DeckNetLinkOff").Translate());

        float sx = pad.xMax + 76f;
        float sw = Mathf.Min(340f, plate.x - 24f - sx);
        if (sw >= 200f)
        {
            var strap = new Rect(sx, by + 12f, sw, 38f);
            NineSlice(strap, NomadTex.Strap, 20f, 0f, 20f, 0f);
            int lit = Mathf.CeilToInt(Mathf.Clamp(heat, 0f, HeatTracker.MaxHeat) / HeatTracker.MaxHeat * 10f - 0.001f);
            float step = (strap.width - 44f - 16f) / 9f;
            for (int i = 0; i < 10; i++)
            {
                var rv = new Rect(strap.x + 22f + i * step, strap.center.y - 9f, 18f, 18f);
                if (i < lit)
                {
                    Color c = HeatSegments[Mathf.Min(4, i / 2)];
                    Glow(rv.ContractedBy(4f), c, 0.35f, 5f);
                    GUI.color = new Color(c.r * 0.95f, c.g * 0.95f, c.b * 0.95f);
                    GUI.DrawTexture(new Rect(rv.x + 1f, rv.y + 1f, 16f, 16f), NomadTex.Gem);
                    GUI.color = Color.white;
                }
                else
                    GUI.DrawTexture(rv, NomadTex.Rivet);
            }
            Stencil(new Rect(strap.x + 4f, strap.yMax + 1f, 120f, 14f), "HEAT LEVEL", TextAnchor.UpperLeft);
            if (Mouse.IsOver(strap.ExpandedBy(4f)))
                Tip(strap.ExpandedBy(4f), "NR_DeckHeatScale".Translate(heat.ToString("0"), level.Label()));
            // Dives scratched into the paint, where there is room.
            if (plate.x - 12f - (strap.xMax + 12f) >= NomadTex.Tally.width)
                Sprite(NomadTex.Tally, new Vector2(strap.xMax + 12f + (plate.x - 24f - strap.xMax - NomadTex.Tally.width) / 2f, by + 24f));
        }
    }
}
