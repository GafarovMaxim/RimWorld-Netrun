using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The Kurenai Systems deck (Maxim's references, docs/mockups/39-kurenai.png): a corporate exec terminal printed on
/// light stock, a worn red band with 紅 down the left, hatching, registration marks, microtype, the screen in a black
/// satin bezel. Every part is a texture from tools/gen_deck_kurenai.py; the layout numbers mirror that script. The keys
/// and readouts work as on the classic deck.
/// </summary>
public static partial class NetrunUI
{
    private const float KurOut = 8f;
    private const float KurInL = 72f, KurInT = 80f, KurInR = 50f, KurInB = 96f;
    private const float KurSlice = 160f;
    public static readonly Vector2 KurenaiExtra = new(2f * KurOut + KurInL + KurInR, 2f * KurOut + KurInT + KurInB);

    private static readonly Color KurInk = Hex(0x1a1a1c), KurDim = Hex(0x6e6e6c), KurPaper = Hex(0xd9d7d1), KurRed = Hex(0xd4242c);
    private static float _kurKnob = -1f;

    private static class KurTex
    {
        private static Texture2D Get(string name) => ContentFinder<Texture2D>.Get("UI/Netrun/Frame/Kurenai/" + name, reportFailure: false);

        public static readonly Texture2D Body = Get("Body"), Kanji = Get("Kanji"), Logo = Get("Logo"), Pad = Get("Pad"), PadRed = Get("Pad_Red"),
            PadBlack = Get("Pad_Black"), Ring = Get("Ring"), RingRed = Get("Ring_Red"), LedOff = Get("Led_Off"), LedGreen = Get("Led_Green"),
            LedOrange = Get("Led_Orange"), Lcd = Get("Lcd"), Seal = Get("Seal"), Knob = Get("Knob"), Hatch = Get("Hatch"), XMarks = Get("XMarks"),
            Checker = Get("Checker"), Barcode = Get("Barcode"), MicroBottom = Get("Micro_Bottom"), MicroSide = Get("Micro_Side");

        public static bool Ready => Body != null;
    }

    /// <summary>A printed key or round sensor that sinks while pressed and fades when it does nothing here, label on it.</summary>
    private static bool KurKey(Rect hit, Texture2D tex, string label, Color ink, string tip, bool active, GameFont font)
    {
        if (Event.current.type == EventType.Repaint)
        {
            bool over = active && Mouse.IsOver(hit);
            bool down = over && _pressed;
            var at = new Vector2(hit.x, hit.y + (down ? 1f : 0f));
            float k = !active ? 1f : down ? 0.85f : over ? 1.08f : 1f;
            Sprite(tex, at, new Color(k, k, k, active ? 1f : 0.55f));
            Text.Font = font;
            Text.Anchor = TextAnchor.MiddleCenter;
            Color c = !active ? new Color(ink.r, ink.g, ink.b, 0.4f) : ink;
            Widgets.Label(new Rect(at.x, at.y, hit.width, hit.height), label.Colorize(c));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        return CasingHit(hit, active, tip);
    }

    private static void KurSmall(Rect r, string text, Color c, TextAnchor anchor = TextAnchor.UpperLeft)
    {
        Text.Font = GameFont.Tiny;
        Text.Anchor = anchor;
        Widgets.Label(r, text.Colorize(c));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }

    private static Rect KurenaiBezel(Rect outer, float heat, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu)
    {
        Rect box = outer.ContractedBy(KurOut);
        var screen = new Rect(box.x + KurInL, box.y + KurInT, box.width - KurInL - KurInR, box.height - KurInT - KurInB);
        bool paint = Event.current.type == EventType.Repaint;
        if (paint)
            DrawKurenaiCasing(box, level, anim, time);
        DrawKurenaiTop(box, level, online, anim, time, onKey, menu, paint);
        DrawKurenaiBottom(box, heat, level, online, anim, time, menu ? onKey : null, paint);
        return screen;
    }

    private static void DrawKurenaiCasing(Rect box, HeatLevel level, bool anim, float time)
    {
        NineSlice(box, KurTex.Body, KurSlice, KurSlice, KurSlice, KurSlice);
        // From Target the red band breathes: a warm light swells over it, quicker in the red zone.
        if (level >= HeatLevel.Target)
        {
            float a = anim ? 0.5f + 0.5f * Mathf.Sin(time * (level >= HeatLevel.RedZone ? 4f : 1.8f)) : 0.5f;
            Widgets.DrawBoxSolid(new Rect(box.x + 16f, box.y + 70f, 44f, box.height - 140f), new Color(1f, 0.45f, 0.4f, 0.16f * a));
        }
        // 紅 and クレナイ on the red band (the band stops 70 px above the bottom).
        if (box.y + 110f + KurTex.Kanji.height <= box.yMax - 80f)
            Sprite(KurTex.Kanji, new Vector2(box.x + 16f, box.y + 110f));

        // The right column top to bottom: hatching, three red crosses, the series in microtype; a barcode low down.
        float cx = box.xMax - KurInR;
        float y = box.y + KurInT + 20f;
        float barcodeY = box.yMax - KurInB - KurTex.Barcode.height - 30f;
        Sprite(KurTex.Hatch, new Vector2(cx + 14f, y));
        y += KurTex.Hatch.height + 18f;
        if (y + KurTex.XMarks.height <= barcodeY - 10f)
        {
            Sprite(KurTex.XMarks, new Vector2(cx + 18f, y));
            y += KurTex.XMarks.height + 22f;
        }
        if (y + KurTex.MicroSide.height <= barcodeY - 10f)
            Sprite(KurTex.MicroSide, new Vector2(cx + 20f, y));
        if (barcodeY >= box.y + KurInT + 20f + KurTex.Hatch.height + 10f)
            Sprite(KurTex.Barcode, new Vector2(cx + 10f, barcodeY));

        // The legal line along the bottom.
        if (box.x + KurInL + 58f + KurTex.MicroBottom.width <= box.xMax - 160f)
            Sprite(KurTex.MicroBottom, new Vector2(box.x + KurInL + 58f, box.yMax - 20f));
    }

    private static void DrawKurenaiTop(Rect box, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool menu, bool paint)
    {
        NetrunSettings set = NetrunMod.Settings;
        float right = box.xMax - KurInR;
        float lampsX = right - 96f;

        // CLOSE (red), MENU, ACHIEV, NET-LOCK (black while the colony is cut off), left of the lamps.
        string[] names = ["CLOSE", "MENU", "ACHIEV", "NET-LOCK"];
        NodeDefense defense = Current.Game != null ? NodeDefense.Get() : null;
        bool isolated = defense?.Isolated == true;
        AcceptanceReport lockReport = defense?.CanToggleIsolation() ?? false;
        float kx = (set.FrameLights ? lampsX - 60f : right - 20f) - 4f * 80f;
        for (int i = 0; i < 4; i++)
        {
            var key = new Rect(kx + i * 80f, box.y + 22f, 72f, 30f);
            bool active = i switch { 0 => onKey != null, 1 or 2 => onKey != null && menu, _ => defense != null && lockReport.Accepted };
            string tip = !Mouse.IsOver(key) ? null : i == 3
                ? (string)(isolated ? "NR_DeckKey_IsolateOff" : "NR_DeckKey_IsolateOn").Translate() + "\n\n" + "NR_IsolationGizmoDesc".Translate()
                  + (lockReport.Reason.NullOrEmpty() ? string.Empty : "\n\n" + lockReport.Reason)
                : !active && i > 0 ? (string)"NR_DeckKey_TerminalOnly".Translate() : (string)("NR_DeckKey_" + (KeyClose + i)).Translate();
            Texture2D tex = i switch { 0 => KurTex.PadRed, 3 when isolated => KurTex.PadBlack, _ => KurTex.Pad };
            Color ink = i == 0 ? KurPaper : i == 3 && isolated ? Hex(0xff5a62) : KurInk;
            if (paint && i == 3 && isolated && anim)
                Glow(key, KurRed, 0.25f + 0.2f * Mathf.Abs(Mathf.Sin(time * 2f)), 5f);
            if (KurKey(key, tex, names[i], ink, tip, active, GameFont.Tiny))
            {
                if (i == 3)
                    defense?.ToggleIsolation();
                else
                    onKey?.Invoke(KeyClose + i);
            }
        }
        if (!paint)
            return;

        // The wordmark, when the keys leave room for it.
        if (box.x + 140f + KurTex.Logo.width <= kx - 16f)
            Sprite(KurTex.Logo, new Vector2(box.x + 140f, box.y + 20f));

        if (!set.FrameLights)
            return;
        // RF bars, then the lamps NET, SICK, POD.
        float rx = lampsX - 44f;
        int lit = online ? Mathf.Clamp(Mathf.RoundToInt(_rf / 6f * 5f), 1, 5) : 0;
        for (int i = 0; i < 5; i++)
        {
            float h = 3f + i * 2.4f;
            Widgets.DrawBoxSolid(new Rect(rx + 2f + i * 5f, box.y + 34f - h, 3f, h), i < lit ? KurInk : new Color(KurInk.r, KurInk.g, KurInk.b, 0.2f));
        }
        KurSmall(new Rect(rx, box.y + 37f, 40f, 16f), "RF", KurDim);
        Tip(new Rect(rx - 2f, box.y + 18f, 32f, 34f), (online ? "NR_DeckRfOn" : "NR_DeckRfOff").Translate());

        bool sick = Current.Game != null && Cached("deck.sick", () => PawnsFinder.AllMaps_FreeColonists.Any(p => p.health.hediffSet.HasHediff(NetrunDefOf.NR_Netsick)), 1f);
        bool pod = Current.Game != null && Cached("deck.pod", () => Find.Maps.Any(m => m.GetComponent<USH_HE.MapComponent_CyberpodManager>()?.Pods.Any(pp => pp.ContainedThing != null) == true), 1f);
        (bool on, Texture2D tex, bool blink, string tip, string label)[] lights =
        [
            (online, KurTex.LedGreen, false, "NR_DeckLightNet".Translate(), "NET"),
            (sick, KurTex.LedOrange, true, "NR_DeckLightSick".Translate(), "SICK"),
            (pod, KurTex.LedGreen, false, "NR_DeckLightPod".Translate(), "POD"),
        ];
        for (int i = 0; i < lights.Length; i++)
        {
            var l = new Rect(lampsX + i * 30f, box.y + 24f, KurTex.LedOff.width, KurTex.LedOff.height);
            float a = !lights[i].on ? 0f : lights[i].blink && anim ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * 3f)) : 1f;
            GUI.DrawTexture(l, KurTex.LedOff);
            if (a > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.DrawTexture(l, lights[i].tex);
                GUI.color = Color.white;
            }
            KurSmall(new Rect(l.center.x - 20f, box.y + 36f, 40f, 16f), lights[i].label, KurDim, TextAnchor.UpperCenter);
            Tip(new Rect(l.center.x - 13f, box.y + 20f, 26f, 32f), lights[i].tip);
        }
    }

    /// <summary>A red rubber stamp, a little crooked: ALERT.</summary>
    private static void KurenaiStamp(Rect r)
    {
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(-7f, r.center);
        Color red = new(0.83f, 0.14f, 0.17f, 0.85f);
        GUI.color = red;
        Widgets.DrawBox(r, 2);
        GUI.color = Color.white;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(r, "ALERT".Colorize(red));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        GUI.matrix = m;
    }

    private static void DrawKurenaiBottom(Rect box, float heat, HeatLevel level, bool online, bool anim, float time, System.Action<int> onKey, bool paint)
    {
        float yb = box.yMax - KurInB;
        bool active = onKey != null;
        float x = box.x + KurInL - 2f;
        if (paint)
            Sprite(KurTex.Seal, new Vector2(x, yb + 22f));
        x += KurTex.Seal.width + 12f;

        // The status LCD.
        if (NetrunMod.Settings.FrameLcd)
        {
            var lcd = new Rect(x, yb + 18f, 240f, 56f);
            if (paint)
            {
                NineSlice(lcd, KurTex.Lcd, 8f, 8f, 8f, 8f);
                DrawLcd(lcd.ContractedBy(6f), heat, level, online, recess: false);
            }
            x = lcd.xMax;
        }
        x += 34f;

        // BACK, CORE (red), HELP: round sensors with their names under them.
        string[] glyphs = ["<", "O", "?"];
        string[] names = ["BACK", "CORE", "HELP"];
        int[] ids = [KeyBack, KeyCore, KeyHelp];
        for (int i = 0; i < 3; i++)
        {
            var b = new Rect(x + i * 54f, yb + 20f, 40f, 40f);
            string tip = active && Mouse.IsOver(b) ? (string)("NR_DeckKey_" + ids[i]).Translate() : null;
            if (KurKey(b, i == 1 ? KurTex.RingRed : KurTex.Ring, glyphs[i], KurPaper, tip, active, GameFont.Medium))
                onKey?.Invoke(ids[i]);
            if (paint)
                KurSmall(new Rect(b.x - 10f, yb + 63f, 60f, 16f), names[i], KurDim, TextAnchor.UpperCenter);
        }
        if (!paint)
            return;
        x += 3f * 54f + 30f;

        // THREAT INDEX: one square per Heat level, lit up to the current one in its colour.
        if (x + 140f <= box.xMax - KurInR)
        {
            KurSmall(new Rect(x, yb + 16f, 120f, 16f), "THREAT INDEX", KurInk);
            int lit = (int)level + 1;
            for (int i = 0; i < 5; i++)
            {
                var sq = new Rect(x + i * 26f, yb + 34f, 22f, 13f);
                Widgets.DrawBoxSolid(sq, KurInk);
                bool off = i == lit - 1 && level >= HeatLevel.RedZone && anim && time * 2f % 1f < 0.35f;
                Widgets.DrawBoxSolid(sq.ContractedBy(1f), i < lit && !off ? HeatSegments[i] : Hex(0xb0afab));
            }
            if (level >= HeatLevel.RedZone)
                KurenaiStamp(new Rect(x + 92f, yb + 8f, 58f, 20f));
            KurSmall(new Rect(x, yb + 49f, 140f, 16f), heat.ToString("0") + " / " + level.Label().ToUpper(), KurDim);
            Sprite(KurTex.Checker, new Vector2(x, yb + 66f));
            var zone = new Rect(x - 4f, yb + 14f, 140f, 50f);
            if (Mouse.IsOver(zone))
                Tip(zone, "NR_DeckHeatScale".Translate(heat.ToString("0"), level.Label()));
            x += 5f * 26f + 34f;
        }

        // NET LINK: the red slider glides between OFF and ON.
        if (x + 116f <= box.xMax - 40f)
        {
            KurSmall(new Rect(x, yb + 16f, 120f, 16f), "NET LINK", KurInk);
            Widgets.DrawBoxSolid(new Rect(x, yb + 39f, 110f, 2f), KurInk);
            for (int i = 0; i <= 110; i += 22)
                Widgets.DrawBoxSolid(new Rect(x + i, yb + 44f, 1f, 4f), KurInk);
            KurSmall(new Rect(x, yb + 49f, 40f, 16f), "OFF", KurDim);
            KurSmall(new Rect(x + 70f, yb + 49f, 40f, 16f), "ON", KurDim, TextAnchor.UpperRight);
            float target = online ? 1f : 0f;
            _kurKnob = _kurKnob < 0f || !NetrunMod.Settings.Animations ? target : Mathf.MoveTowards(_kurKnob, target, Time.unscaledDeltaTime * 2.5f);
            Sprite(KurTex.Knob, new Vector2(Mathf.Round(x + 104f * _kurKnob), yb + 29f));
            var zone = new Rect(x - 4f, yb + 14f, 120f, 50f);
            if (Mouse.IsOver(zone))
                Tip(zone, (online ? "NR_DeckNetLinkOn" : "NR_DeckNetLinkOff").Translate());
        }
    }
}
