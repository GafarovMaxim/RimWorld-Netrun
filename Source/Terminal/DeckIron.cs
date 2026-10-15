using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The Iron Company deck (Maxim's references, docs/mockups/41-iron.png): a rugged field tablet in chipped olive paint
/// with rubber corner bumpers, tube handles, an antenna, round connectors, vents and a data plate on the rails, the
/// controls on recessed sub-panels: guarded toggles for CLOSE and NET-LOCK, rubber soft keys, a TUNE knob, jewel lamps,
/// an amber MIL-RF window and an amber backlit LCD, a THREAT LEVEL bargraph and a NET LINK toggle between hazard
/// stripes. Every part is a texture from tools/gen_deck_iron.py; the layout numbers mirror that script.
/// </summary>
public static partial class NetrunUI
{
    private const float IronOutL = 34f, IronOutT = 8f, IronOutR = 34f, IronOutB = 8f;
    private const float IronInL = 46f, IronInT = 106f, IronInR = 46f, IronInB = 88f;
    private const float IronSlice = 170f;
    public static readonly Vector2 IronExtra = new(IronOutL + IronOutR + IronInL + IronInR, IronOutT + IronOutB + IronInT + IronInB);

    private static readonly Color IronStencil = new(0.85f, 0.82f, 0.71f, 0.9f), IronAmber = Hex(0xffb03c), IronLcdInk = Hex(0x301400);

    private static class IronTex
    {
        private static Texture2D Get(string name) => ContentFinder<Texture2D>.Get("UI/Netrun/Frame/Iron/" + name, reportFailure: false);

        public static readonly Texture2D Body = Get("Body"), Handle = Get("Handle"), Plate = Get("Plate"), Screw = Get("Screw"), Key = Get("Key"),
            Knob = Get("Knob"), LampOff = Get("Lamp_Off"), LampGreen = Get("Lamp_Green"), LampOrange = Get("Lamp_Orange"), Lcd = Get("Lcd"),
            Window = Get("Window"), Hazard = Get("Hazard"), Panel = Get("Panel"), Antenna = Get("Antenna"), ConnectorCap = Get("Connector_Cap"),
            ConnectorOpen = Get("Connector_Open"), Vent = Get("Vent"), DataPlate = Get("DataPlate"), Sticker = Get("Sticker"), Warning = Get("Warning"),
            ToggleDown = Get("Toggle_0"), ToggleUp = Get("Toggle_1"), GuardClosed = Get("Guard_Closed"), GuardOpen = Get("Guard_Open"),
            GuardLit = Get("Guard_Lit"), LampRed = Get("Lamp_Red");

        public static bool Ready => Body != null;
    }

    private static void IronStencilLabel(Rect r, string text, TextAnchor anchor = TextAnchor.UpperCenter) => Stencil(r, text, anchor, IronStencil);

    private static void IronPanel(Rect r) => NineSlice(r, IronTex.Panel, 14f, 14f, 14f, 14f);

    /// <summary>A rubber soft key with its glyph; sinks while pressed, fades when it does nothing here.</summary>
    private static bool IronKey(Rect key, string glyph, Color ink, string tip, bool active)
    {
        if (Event.current.type == EventType.Repaint)
        {
            bool over = active && Mouse.IsOver(key);
            bool down = over && _pressed;
            float k = !active ? 0.6f : down ? 0.8f : over ? 1.15f : 1f;
            var at = new Vector2(key.x, key.y + (down ? 1f : 0f));
            Sprite(IronTex.Key, at, new Color(k, k, k));
            GlyphText(new Rect(at.x, at.y - 1f, key.width, key.height), active ? ink : new Color(ink.r, ink.g, ink.b, 0.35f), glyph, GameFont.Small);
        }
        return CasingHit(key, active, tip);
    }

    /// <summary>A red push button under a hinged cover: closed, flipped up under the mouse, up with the button lit while
    /// <paramref name="armed"/>.</summary>
    private static bool IronGuard(Rect hit, bool armed, string tip, bool active)
    {
        if (Event.current.type == EventType.Repaint)
        {
            bool over = active && Mouse.IsOver(hit);
            Texture2D tex = armed ? IronTex.GuardLit : over ? IronTex.GuardOpen : IronTex.GuardClosed;
            Sprite(tex, hit.position, active ? Color.white : new Color(0.65f, 0.65f, 0.65f));
        }
        return CasingHit(hit, active, tip);
    }

    private static Rect IronBezel(Rect outer, float heat, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu)
    {
        var box = new Rect(outer.x + IronOutL, outer.y + IronOutT, outer.width - IronOutL - IronOutR, outer.height - IronOutT - IronOutB);
        var screen = new Rect(box.x + IronInL, box.y + IronInT, box.width - IronInL - IronInR, box.height - IronInT - IronInB);
        bool paint = Event.current.type == EventType.Repaint;
        if (paint)
            DrawIronCasing(box, screen);
        DrawIronTop(box, level, online, anim, time, onKey, menu, paint);
        DrawIronBottom(box, heat, level, online, menu ? onKey : null, paint);
        return screen;
    }

    private static void DrawIronCasing(Rect box, Rect screen)
    {
        // The handles behind the case, the right one mirrored.
        float hy = Mathf.Round(box.center.y - IronTex.Handle.height / 2f);
        Sprite(IronTex.Handle, new Vector2(box.x - 30f, hy));
        GUI.DrawTextureWithTexCoords(new Rect(box.xMax + 30f - IronTex.Handle.width, hy, IronTex.Handle.width, IronTex.Handle.height), IronTex.Handle, new Rect(1f, 0f, -1f, 1f));
        NineSlice(box, IronTex.Body, IronSlice, IronSlice, IronSlice, IronSlice);
        // The antenna screwed into the top-right bumper, standing only a little above the case (the window stays low).
        Sprite(IronTex.Antenna, new Vector2(box.xMax - 40f, box.y - IronOutT));

        // The rails: connectors on the left at the top, vents, a data plate on the right, screws wherever is free.
        var conn = new Rect(box.x + 2f, screen.y + 8f, 30f, 96f);
        Sprite(IronTex.ConnectorCap, new Vector2(conn.x, conn.y));
        IronStencilLabel(new Rect(conn.x - 6f, conn.y + 30f, 42f, 14f), "DATA");
        Sprite(IronTex.ConnectorOpen, new Vector2(conn.x, conn.y + 46f));
        IronStencilLabel(new Rect(conn.x - 6f, conn.y + 76f, 42f, 14f), "AUX");
        float vh = IronTex.Vent.height;
        var leftVent = new Rect(box.x + 9f, Mathf.Round(screen.center.y - vh / 2f + 43f), IronTex.Vent.width, vh);
        var plate = new Rect(box.xMax - 29f, screen.y + 84f, IronTex.DataPlate.width, IronTex.DataPlate.height);
        var rightVent = new Rect(box.xMax - 25f, screen.yMax - 190f, IronTex.Vent.width, vh);
        bool leftVentFits = leftVent.y > conn.yMax + 20f && leftVent.yMax < screen.yMax - 20f;
        bool plateFits = plate.yMax < screen.yMax - 40f;
        bool rightVentFits = rightVent.y > (plateFits ? plate.yMax : screen.y) + 20f;
        if (leftVentFits)
            GUI.DrawTexture(leftVent, IronTex.Vent);
        if (plateFits)
            GUI.DrawTexture(plate, IronTex.DataPlate);
        if (rightVentFits)
            GUI.DrawTexture(rightVent, IronTex.Vent);
        for (float y = screen.y + 60f; y < screen.yMax - 50f; y += 86f)
        {
            bool Free(Rect r, bool used) => !used || y + 10f < r.y || y - 10f > r.yMax;
            if (Free(conn, true) && Free(leftVent, leftVentFits))
                Sprite(IronTex.Screw, new Vector2(box.x + 17f - IronTex.Screw.width / 2f, y - IronTex.Screw.height / 2f));
            if (Free(plate, plateFits) && Free(rightVent, rightVentFits))
                Sprite(IronTex.Screw, new Vector2(box.xMax - 17f - IronTex.Screw.width / 2f, y - IronTex.Screw.height / 2f));
        }
    }

    private static void DrawIronTop(Rect box, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu, bool paint)
    {
        NetrunSettings set = NetrunMod.Settings;
        float y = box.y;
        float rx = box.xMax - 240f;
        var rightPanel = new Rect(rx - 112f, y + 14f, 244f, 80f);
        float x = rightPanel.x - 16f - 280f;
        var leftPanel = new Rect(x - 12f, y + 14f, 292f, 80f);
        if (paint)
            IronAlarm(new Rect(box.x + 104f, y + 14f, 18f, 18f), level, anim, time);
        if (paint)
        {
            IronPanel(leftPanel);
            IronPanel(rightPanel);
            // The nameplate with its QC sticker, when the panels leave room for it.
            if (box.x + 56f + IronTex.Plate.width <= leftPanel.x - 8f)
            {
                Sprite(IronTex.Plate, new Vector2(box.x + 56f, y + 38f));
                Sprite(IronTex.Sticker, new Vector2(box.x + 56f + 214f, y + 66f));
            }
        }

        // CLOSE (guarded), MENU, ACHIEV (rubber), NET-LOCK (guarded, the cover stays up while the colony is cut off).
        NodeDefense defense = Current.Game != null ? NodeDefense.Get() : null;
        bool isolated = defense?.Isolated == true;
        AcceptanceReport lockReport = defense?.CanToggleIsolation() ?? false;
        var close = new Rect(x, y + 22f, 40f, 56f);
        if (IronGuard(close, false, Mouse.IsOver(close) ? (string)("NR_DeckKey_" + KeyClose).Translate() : null, onKey != null))
            onKey?.Invoke(KeyClose);
        string[] glyphs = ["≡", "★"];
        for (int i = 0; i < 2; i++)
        {
            var key = new Rect(x + 52f + i * 56f, y + 32f, 46f, 34f);
            bool active = onKey != null && menu;
            string tip = !Mouse.IsOver(key) ? null : active ? (string)("NR_DeckKey_" + (KeyMenu + i)).Translate() : (string)"NR_DeckKey_TerminalOnly".Translate();
            if (IronKey(key, glyphs[i], IronStencil, tip, active))
                onKey?.Invoke(KeyMenu + i);
        }
        var lockHit = new Rect(x + 164f, y + 22f, 40f, 56f);
        string lockTip = !Mouse.IsOver(lockHit) ? null
            : (string)(isolated ? "NR_DeckKey_IsolateOff" : "NR_DeckKey_IsolateOn").Translate() + "\n\n" + "NR_IsolationGizmoDesc".Translate()
              + (lockReport.Reason.NullOrEmpty() ? string.Empty : "\n\n" + lockReport.Reason);
        if (paint && isolated && anim)
            Glow(lockHit.ContractedBy(6f), Bad, 0.3f + 0.25f * Mathf.Abs(Mathf.Sin(time * 2f)), 6f);
        if (IronGuard(lockHit, isolated, lockTip, defense != null && lockReport.Accepted))
            defense?.ToggleIsolation();
        if (!paint)
            return;

        string[] names = ["CLOSE", "MENU", "ACHIEV", "NET-LOCK", "TUNE"];
        float[] centers = [x + 20f, x + 75f, x + 131f, x + 184f, x + 244f];
        for (int i = 0; i < names.Length; i++)
            IronStencilLabel(new Rect(centers[i] - 30f, y + 77f, 60f, 14f), names[i]);

        // TUNE: the knob turns to the tuner position, with its dial scale around it.
        var knob = new Rect(x + 224f, y + 30f, IronTex.Knob.width, IronTex.Knob.height);
        Matrix4x4 m = GUI.matrix;
        NetrunUI.RotateAround(Mathf.Lerp(-135f, 135f, _tuner), knob.center);
        GUI.DrawTexture(knob, IronTex.Knob);
        GUI.matrix = m;
        Tip(knob, "NR_DeckTuner".Translate());

        // The MIL-RF window: frequency and ten amber bars.
        var win = new Rect(rx, y + 30f, 120f, 44f);
        NineSlice(win, IronTex.Window, 8f, 8f, 8f, 8f);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(win.x + 8f, win.y + 3f, 60f, 16f), "MIL-RF".Colorize(IronAmber));
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(new Rect(win.xMax - 68f, win.y + 3f, 60f, 16f), "2.4 GHz".Colorize(IronAmber));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        bool hot = level >= HeatLevel.Target;
        int lit = online ? Mathf.Clamp(Mathf.RoundToInt(_rf / 6f * 10f), 1, 10) : 0;
        for (int i = 0; i < 10; i++)
        {
            Color c = hot && i == 9 ? Bad : IronAmber;
            bool on = i < lit || hot && i == 9 && online;
            Widgets.DrawBoxSolid(new Rect(win.x + 9f + i * 10f, win.y + 24f, 6f, 10f), on ? c : new Color(c.r, c.g, c.b, 0.2f));
        }
        IronStencilLabel(new Rect(win.x, y + 77f, win.width, 14f), online ? "LINK // LOCKED" : "LINK // LOST");
        Tip(win, (online ? "NR_DeckRfOn" : "NR_DeckRfOff").Translate());

        if (!set.FrameLights)
            return;
        bool sick = Current.Game != null && Cached("deck.sick", () => PawnsFinder.AllMaps_FreeColonists.Any(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick)), 1f);
        bool pod = Current.Game != null && Cached("deck.pod", () => Find.Maps.Any(mm => mm.GetComponent<USH_HE.MapComponent_CyberpodManager>()?.Pods.Any(pp => pp.ContainedThing != null) == true), 1f);
        (bool on, Color c, Texture2D tex, bool blink, string tip, string label)[] lights =
        [
            (online, Ok, IronTex.LampGreen, false, "NR_DeckLightNet".Translate(), "NET"),
            (sick, DeckOrange, IronTex.LampOrange, true, "NR_DeckLightSick".Translate(), "SICK"),
            (pod, PodGreen, IronTex.LampGreen, false, "NR_DeckLightPod".Translate(), "POD"),
        ];
        for (int i = 0; i < lights.Length; i++)
        {
            var l = new Rect(rx - 98f + i * 30f, y + 38f, 18f, 18f);
            float a = !lights[i].on ? 0f : lights[i].blink && anim ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * 3f)) : 1f;
            GUI.DrawTexture(l, IronTex.LampOff);
            if (a > 0f)
            {
                Glow(l.ContractedBy(4f), lights[i].c, a * 0.6f, 5f);
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.DrawTexture(l, lights[i].tex);
                GUI.color = Color.white;
            }
            IronStencilLabel(new Rect(l.center.x - 17f, y + 77f, 34f, 14f), lights[i].label);
            Tip(l.ExpandedBy(5f), lights[i].tip);
        }
    }

    // The ALARM lamp above the nameplate: dark below Target, a slow pulse at Target, a double strobe in the red zone.
    private static void IronAlarm(Rect r, HeatLevel level, bool anim, float time)
    {
        float a = 0f;
        if (level >= HeatLevel.Target && NetrunMod.Settings.FrameLights)
        {
            float phase = time * 1.4f % 1f;
            a = !anim ? 1f
                : level >= HeatLevel.RedZone ? (phase < 0.12f || phase is > 0.24f and < 0.36f ? 1f : 0.08f)
                : 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(time * 2.2f));
        }
        GUI.DrawTexture(r, IronTex.LampOff);
        if (a > 0f)
        {
            Glow(r.ContractedBy(4f), Bad, a * 0.8f, 7f);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.DrawTexture(r, IronTex.LampRed);
            GUI.color = Color.white;
        }
        IronStencilLabel(new Rect(r.xMax + 6f, r.y + 2f, 60f, 14f), "ALARM", TextAnchor.UpperLeft);
        Tip(new Rect(r.x - 4f, r.y - 4f, 74f, r.height + 8f), "NR_DeckBeacon".Translate());
    }

    private static void DrawIronBottom(Rect box, float heat, HeatLevel level, bool online, System.Action<int> onKey, bool paint)
    {
        float yb = box.yMax - IronInB;
        bool active = onKey != null;
        float tx = box.xMax - IronInR - 100f;
        float x = box.x + 100f;

        // The amber LCD with the HIGH VOLTAGE stencil under it.
        if (NetrunMod.Settings.FrameLcd)
        {
            var lcd = new Rect(x, yb + 10f, 250f, 56f);
            if (paint)
            {
                NineSlice(lcd, IronTex.Lcd, 10f, 10f, 10f, 10f);
                string[] lines = Cached("deck.lcd", () => LcdLines(heat, level, online), 1f);
                Text.Font = GameFont.Tiny;
                for (int i = 0; i < lines.Length; i++)
                    Widgets.Label(new Rect(lcd.x + 12f, lcd.y + 6f + i * 14.5f, lcd.width - 20f, 16f), lines[i].Colorize(IronLcdInk));
                Text.Font = GameFont.Small;
                Sprite(IronTex.Warning, new Vector2(lcd.x + 4f, yb + 70f));
                if (Mouse.IsOver(lcd))
                    Tip(lcd, "NR_DeckLcd".Translate());
            }
            x = lcd.xMax;
        }
        float kx = x + 34f;

        // A panel with BACK, CORE, HELP and the THREAT LEVEL bargraph, as wide as the room before NET LINK allows.
        float hw = Mathf.Min(250f, tx - 40f - (kx + 3f * 56f + 24f));
        var panel = new Rect(kx - 12f, yb + 8f, hw >= 120f ? 3f * 56f + 24f + hw + 24f : 3f * 56f + 14f, 74f);
        if (paint)
            IronPanel(panel);
        string[] glyphs = ["◁", "●", "?"];
        string[] names = ["BACK", "CORE", "HELP"];
        int[] ids = [KeyBack, KeyCore, KeyHelp];
        for (int i = 0; i < 3; i++)
        {
            var key = new Rect(kx + i * 56f, yb + 21f, 46f, 34f);
            string tip = active && Mouse.IsOver(key) ? (string)("NR_DeckKey_" + ids[i]).Translate() : null;
            if (IronKey(key, glyphs[i], i == 1 ? Hex(0xf05a46) : IronStencil, tip, active))
                onKey?.Invoke(ids[i]);
            if (paint)
                IronStencilLabel(new Rect(key.x - 7f, yb + 59f, 60f, 14f), names[i]);
        }
        if (!paint)
            return;

        if (hw >= 120f)
        {
            float hx = kx + 3f * 56f + 24f;
            var bar = new Rect(hx, yb + 21f, hw, 34f);
            NineSlice(bar, IronTex.Window, 8f, 8f, 8f, 8f);
            int lit = Mathf.Clamp(Mathf.RoundToInt(heat / HeatTracker.MaxHeat * 10f), heat > 0.5f ? 1 : 0, 10);
            float step = (hw - 16f) / 10f;
            for (int i = 0; i < 10; i++)
            {
                Color c = HeatSegments[Mathf.Min(4, i / 2)];
                Widgets.DrawBoxSolid(new Rect(bar.x + 8f + i * step, bar.y + 7f, step - 5f, 20f), i < lit ? c : new Color(c.r * 0.18f, c.g * 0.18f, c.b * 0.18f));
            }
            IronStencilLabel(new Rect(bar.x, yb + 59f, bar.width, 14f), "THREAT LEVEL");
            if (Mouse.IsOver(bar))
                Tip(bar, "NR_DeckHeatScale".Translate(heat.ToString("0"), level.Label()));
        }

        // NET LINK: a toggle between hazard stripes, up when the colony is on the net.
        Sprite(IronTex.Hazard, new Vector2(tx - 16f, yb + 14f));
        Sprite(IronTex.Hazard, new Vector2(tx + 44f, yb + 14f));
        var sw = new Rect(tx, yb + 6f, 40f, 56f);
        GUI.DrawTexture(sw, online ? IronTex.ToggleUp : IronTex.ToggleDown);
        IronStencilLabel(new Rect(tx + 58f, yb + 12f, 30f, 14f), "ON", TextAnchor.UpperLeft);
        IronStencilLabel(new Rect(tx + 58f, yb + 44f, 30f, 14f), "OFF", TextAnchor.UpperLeft);
        IronStencilLabel(new Rect(tx - 10f, yb + 64f, 60f, 14f), "NET LINK");
        if (Mouse.IsOver(sw))
            Tip(sw, (online ? "NR_DeckNetLinkOn" : "NR_DeckNetLinkOff").Translate());
    }
}
