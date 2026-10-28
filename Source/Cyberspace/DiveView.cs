using System.Linq;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// A dive in progress (mockup 3-dive): the node's steps on a staircase from the lobby to the exit, and a HUD
/// panel with the actions, programs, turn and trace gauges, the haul and a terminal-style log.
/// </summary>
public static class DiveView
{
    private const float PanelWidth = 290f;
    private const float MinBoxWidth = 116f;
    private const float MaxBoxWidth = 190f;
    private const float MinBoxHeight = 70f;
    private const float MaxBoxHeight = 132f;
    private const float TraceGaugeMax = 20f;

    private static Vector2 _panelScroll;
    private static float _panelHeight;
    private static Vector2 _logScroll;
    private static int _logCount = -1;
    private static float _logChangedAt;

    public static void Draw(Rect rect, DiveSession session)
    {
        bool tour = session.Training && TourActive(session);
        TourWantsAreas = tour;
        // While the tour runs, clicks outside its card do nothing (the dive waits until the player has looked around).
        if (tour && Event.current.isMouse && rect.ExpandedBy(0f, 40f).Contains(Event.current.mousePosition) && !_tourCard.Contains(Event.current.mousePosition))
            Event.current.Use();
        var mapRect = new Rect(rect.x, rect.y, rect.width - PanelWidth - 12f, rect.height);
        // The training coach sits over the step map and takes its share of the height.
        if (session.Training)
        {
            float coachHeight = DrawCoach(mapRect, session);
            mapRect.yMin += coachHeight + 8f;
        }
        GuideRegistry.Area("dive.steps", mapRect);
        DrawElements(mapRect, session);
        DrawPanel(new Rect(rect.xMax - PanelWidth, rect.y - 40f, PanelWidth, rect.height + 40f), session);
        if (tour)
            DrawTour(new Rect(rect.x, rect.y - 40f, rect.width, rect.height + 40f), session);
    }

    // ---- Training: a tour of the dive screen before the first move (Maxim, 2026-10-15) ----

    /// <summary>The parts of the dive screen, in the order the tour shows them (guide areas the screen registers).</summary>
    private static readonly string[] TourAreas = ["dive.steps", "dive.gauges", "dive.actions", "dive.programs", "dive.haul", "dive.log"];
    private static DiveSession _tourSession;
    private static int _tourStep;
    private static Rect _tourCard;

    /// <summary>True while the training tour needs the screen's areas (the terminal collects them only then or in guide mode).</summary>
    public static bool TourWantsAreas;

    private static bool TourActive(DiveSession session)
    {
        if (!ReferenceEquals(_tourSession, session))
        {
            _tourSession = session;
            // Only at the very start of a training dive; a dive already under way skips the tour.
            _tourStep = session.Turn <= 1 && session.Visited.Count <= 1 ? 0 : TourAreas.Length;
            _tourCard = default;
        }
        return _tourStep < TourAreas.Length;
    }

    /// <summary>
    /// Dims the screen but one part, outlines it and puts a card beside it: what this part is and what it is for.
    /// "Next" walks on; the last card hands over to the coach. A part the screen does not show right now is skipped.
    /// </summary>
    private static void DrawTour(Rect screen, DiveSession session)
    {
        IReadOnlyList<(string id, Rect rect)> areas = GuideRegistry.Current;
        if (areas.Count == 0)
            return;
        Rect hole = default;
        while (_tourStep < TourAreas.Length)
        {
            string id = TourAreas[_tourStep];
            int i = -1;
            for (int k = 0; k < areas.Count; k++)
                if (areas[k].id == id)
                    i = k;
            if (i >= 0)
            {
                hole = areas[i].rect.ExpandedBy(4f);
                break;
            }
            _tourStep++;
        }
        if (_tourStep >= TourAreas.Length)
            return;

        var shade = new Color(0f, 0f, 0f, 0.62f);
        Widgets.DrawBoxSolid(new Rect(screen.x, screen.y, screen.width, Mathf.Max(0f, hole.y - screen.y)), shade);
        Widgets.DrawBoxSolid(new Rect(screen.x, hole.yMax, screen.width, Mathf.Max(0f, screen.yMax - hole.yMax)), shade);
        Widgets.DrawBoxSolid(new Rect(screen.x, hole.y, Mathf.Max(0f, hole.x - screen.x), hole.height), shade);
        Widgets.DrawBoxSolid(new Rect(hole.xMax, hole.y, Mathf.Max(0f, screen.xMax - hole.xMax), hole.height), shade);
        PulseOutline(hole);

        string text = ("NR_Tour_" + _tourStep).Translate(TrainingCoach.Shields);
        const float cardW = 340f;
        Text.Font = GameFont.Small;
        float textH = Text.CalcHeight(text, cardW - 28f);
        float cardH = textH + 76f;
        // Beside the part when there is room, else over or under it.
        float x = hole.xMax + 14f + cardW <= screen.xMax ? hole.xMax + 14f : hole.x - 14f - cardW >= screen.x ? hole.x - 14f - cardW : hole.center.x - cardW / 2f;
        float y = x >= hole.xMax || x + cardW <= hole.x ? hole.center.y - cardH / 2f : hole.yMax + 14f + cardH <= screen.yMax ? hole.yMax + 14f : hole.y - 14f - cardH;
        _tourCard = new Rect(Mathf.Clamp(x, screen.x + 6f, screen.xMax - cardW - 6f), Mathf.Clamp(y, screen.y + 6f, screen.yMax - cardH - 6f), cardW, cardH);

        NetrunUI.Glow(_tourCard, CoachColor, 0.2f, 8f);
        NetrunUI.Box(_tourCard, new Color(0.07f, 0.06f, 0.03f, 0.98f), CoachColor, 2);
        Widgets.DrawBoxSolid(new Rect(_tourCard.x, _tourCard.y, 4f, _tourCard.height), CoachColor);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(_tourCard.x + 14f, _tourCard.y + 6f, cardW - 28f, 16f),
            ("// " + (string)"NR_TourTitle".Translate(_tourStep + 1, TourAreas.Length)).ToUpper().Colorize(CoachColor));
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(_tourCard.x + 14f, _tourCard.y + 26f, cardW - 28f, textH + 4f), text);
        bool last = _tourStep == TourAreas.Length - 1;
        var next = new Rect(_tourCard.xMax - 124f, _tourCard.yMax - 38f, 110f, 28f);
        if (NetrunUI.Button(next, (last ? "NR_TourStart" : "NR_TourNext").Translate(), CoachColor, true))
            _tourStep++;
        if (!last && NetrunUI.Button(new Rect(_tourCard.x + 14f, _tourCard.yMax - 38f, 110f, 28f), "NR_TourSkip".Translate(), NetrunUI.Muted, true))
            _tourStep = TourAreas.Length;
    }

    // ---- Training coach ----

    private static readonly Color CoachColor = new(1f, 0.85f, 0.25f);
    private static string _coachText;
    private static float _coachTextWidth;
    private static float _coachTextHeight;

    /// <summary>The coach box over the map (training dives only): what to do next and why. Returns its height.</summary>
    private static float DrawCoach(Rect area, DiveSession session)
    {
        TrainingCoach.Result coach = TrainingCoach.Get(session);
        if (coach.Text.NullOrEmpty())
            return -8f;
        float textWidth = area.width - 76f;
        if (!ReferenceEquals(_coachText, coach.Text) || !Mathf.Approximately(_coachTextWidth, textWidth))
        {
            Text.Font = GameFont.Small;
            _coachText = coach.Text;
            _coachTextWidth = textWidth;
            _coachTextHeight = Text.CalcHeight(coach.Text, textWidth);
        }
        float height = Mathf.Max(52f, _coachTextHeight + 34f);
        var box = new Rect(area.x, area.y, area.width, height);
        float pulse = NetrunMod.Settings.Animations ? 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 2.4f) : 0.7f;
        NetrunUI.Glow(box, CoachColor, 0.12f + 0.12f * pulse, 8f);
        NetrunUI.Box(box, new Color(CoachColor.r * 0.12f, CoachColor.g * 0.1f, 0.04f, 0.96f), new Color(CoachColor.r, CoachColor.g, CoachColor.b, 0.55f + 0.35f * pulse), 2);
        Widgets.DrawBoxSolid(new Rect(box.x, box.y, 4f, box.height), CoachColor);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(box.x + 14f, box.y + 4f, box.width - 28f, 16f), ("// " + (string)"NR_CoachTitle".Translate()).ToUpper().Colorize(CoachColor));
        Text.Font = GameFont.Small;
        GUI.color = CoachColor;
        Widgets.Label(new Rect(box.x + 62f, box.y + 22f, textWidth, _coachTextHeight + 4f), coach.Text);
        GUI.color = Color.white;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(box.x + 16f, box.y + 20f, 40f, 34f), ">>".Colorize(CoachColor));
        Text.Font = GameFont.Small;
        return height;
    }

    /// <summary>A pulsing outline around the button the coach points at.</summary>
    private static void PulseOutline(Rect r)
    {
        float pulse = NetrunMod.Settings.Animations ? 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 5f) : 1f;
        NetrunUI.Glow(r, CoachColor, 0.25f + 0.35f * pulse, 6f);
        GUI.color = new Color(CoachColor.r, CoachColor.g, CoachColor.b, 0.55f + 0.45f * pulse);
        Widgets.DrawBox(r.ExpandedBy(2f), 2);
        GUI.color = Color.white;
    }

    private static Color ElementColor(DiveElement e)
    {
        if (e.Hidden)
            return NetrunUI.Muted;
        return e.Kind switch
        {
            DiveElementKind.Lobby => NetrunUI.Ok,
            DiveElementKind.Gate => NetrunUI.Warn,
            DiveElementKind.Ice => NetrunUI.Bad,
            DiveElementKind.Files => NetrunUI.Accent,
            DiveElementKind.Memory => NetrunUI.Purple,
            _ => NetrunUI.Ok,
        };
    }

    /// <summary>The step's icon and whether it is a white line icon to tint in the step's color.</summary>
    private static (Texture2D icon, bool tint) ElementIcon(DiveElement e)
    {
        if (e.Hidden)
            return (NetrunTex.CyberIcon("Dive_HIDDEN"), true);
        return e.Kind switch
        {
            DiveElementKind.Lobby => (NetrunTex.CyberIcon("Dive_LOBBY"), true),
            DiveElementKind.Gate => (NetrunTex.CyberIcon("Dive_GATE"), false),
            DiveElementKind.Ice => (NetrunTex.CyberIcon("Dive_BLACKICE"), false),
            DiveElementKind.Files => (NetrunTex.CyberIcon("Dive_FILES"), true),
            DiveElementKind.Memory => (NetrunTex.CyberIcon("Dive_MEMORY"), true),
            _ => (NetrunTex.CyberIcon("Dive_EXIT"), true),
        };
    }

    // ---- Steps ----

    /// <summary>
    /// The node as a graph (docs/DIVE_BRANCHING.md): steps on a grid of columns and lanes (hard branch on top, easy at the
    /// bottom), elbow links, the walked path lit green with a packet running along it, the branch not taken dimmed.
    /// </summary>
    private const float StepGap = 18f;
    private const float ScrollBoxWidth = 112f;
    private static Vector2 _mapScroll;
    private static int _scrolledFor = -1;

    /// <summary>
    /// The steps with room to breathe: if they do not fit with a proper gap, the map scrolls sideways and follows the
    /// runner. A quiet animated background sits under it.
    /// </summary>
    private static void DrawElements(Rect rect, DiveSession session)
    {
        List<DiveElement> elements = session.Elements;
        if (elements.Count == 0)
            return;
        DrawBackground(rect);
        int cols = elements.Max(e => e.Col) + 1;
        float fit = (rect.width - (cols - 1) * StepGap) / cols;
        if (fit >= 100f)
        {
            DrawGraph(rect, session, Mathf.Min(fit, MaxBoxWidth));
            return;
        }
        float need = cols * ScrollBoxWidth + (cols - 1) * StepGap;
        var view = new Rect(0f, 0f, need, rect.height - 16f);
        if (_scrolledFor != session.Position)
        {
            _scrolledFor = session.Position;
            float cx = elements[session.Position].Col * (ScrollBoxWidth + StepGap) + ScrollBoxWidth / 2f;
            _mapScroll.x = Mathf.Clamp(cx - rect.width / 2f, 0f, need - rect.width);
        }
        DragMap(rect, need - rect.width);
        NetrunUI.BeginScroll(rect, ref _mapScroll, view);
        DrawGraph(view, session, ScrollBoxWidth);
        Widgets.EndScrollView();
    }

    /// <summary>Faint grid, slow data rain and a rare scan sweep behind the steps.</summary>
    private static bool _dragging;

    /// <summary>Drag the step map sideways with the left or middle mouse button (the wheel still works too).</summary>
    private static void DragMap(Rect rect, float maxScroll)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 2) && Mouse.IsOver(rect))
            _dragging = true;
        else if (e.rawType == EventType.MouseUp)
            _dragging = false;
        if (_dragging && e.type == EventType.MouseDrag)
        {
            _mapScroll.x = Mathf.Clamp(_mapScroll.x - e.delta.x, 0f, Mathf.Max(0f, maxScroll));
            e.Use();
        }
    }

    private static void DrawBackground(Rect rect)
    {
        var grid = new Color(0.22f, 0.78f, 0.38f, 0.05f);
        for (float x = rect.x + 16f; x < rect.xMax; x += 32f)
            Widgets.DrawBoxSolid(new Rect(x, rect.y, 1f, rect.height), grid);
        for (float y = rect.y + 16f; y < rect.yMax; y += 32f)
            Widgets.DrawBoxSolid(new Rect(rect.x, y, rect.width, 1f), grid);
        if (!NetrunMod.Settings.Animations || Event.current.type != EventType.Repaint)
            return;
        float now = Time.realtimeSinceStartup;
        for (int k = 0; k < 8; k++)
        {
            float period = 6f + k * 0.9f;
            float f = (now + k * 1.3f) / period;
            int slot = Mathf.FloorToInt(f);
            float t = f - slot;
            var rnd = new System.Random(slot * 977 + k * 31);
            float x = rect.x + 16f + 32f * rnd.Next(Mathf.Max(1, Mathf.FloorToInt(rect.width / 32f)));
            float len = 30f + (float)rnd.NextDouble() * 50f;
            float y = Mathf.Lerp(rect.y - len, rect.yMax, t);
            float a = 0.22f * Mathf.Sin(t * Mathf.PI);
            for (int d = 0; d < 5; d++)
            {
                float yy = y + d * len / 5f;
                if (yy < rect.y || yy > rect.yMax - 4f)
                    continue;
                GUI.color = new Color(0.3f, 0.9f, 0.5f, a * (0.3f + d * 0.17f));
                GUI.DrawTexture(new Rect(x - 0.5f, yy, 2f, len / 7f), BaseContent.WhiteTex);
            }
        }
        float sweep = now / 9f % 1f;
        if (sweep < 0.25f)
        {
            float sy = rect.y + rect.height * (sweep / 0.25f);
            GUI.color = new Color(0.3f, 0.9f, 0.5f, 0.06f);
            GUI.DrawTexture(new Rect(rect.x, sy - 14f, rect.width, 14f), BaseContent.WhiteTex);
            GUI.color = new Color(0.3f, 0.9f, 0.5f, 0.18f);
            GUI.DrawTexture(new Rect(rect.x, sy, rect.width, 1f), BaseContent.WhiteTex);
        }
        GUI.color = Color.white;
    }

    private static void DrawGraph(Rect rect, DiveSession session, float boxW)
    {
        List<DiveElement> elements = session.Elements;
        int n = elements.Count;
        int cols = elements.Max(e => e.Col) + 1;
        float boxH = MinBoxHeight;
        for (int i = 0; i < n; i++)
        {
            Text.Font = GameFont.Small;
            float body = Text.CalcHeight(ElementInfo(elements[i]), boxW - 16f);
            boxH = Mathf.Max(boxH, HeaderHeight(elements[i], boxW) + 14f + (elements[i].Kind is DiveElementKind.Gate or DiveElementKind.Ice ? 10f : 0f) + body);
        }
        boxH = Mathf.Min(boxH, MaxBoxHeight + 16f);
        Text.Font = GameFont.Small;

        bool branched = session.Forks > 0;
        float stepX = cols > 1 ? (rect.width - boxW) / (cols - 1) : 0f;
        // Lanes no farther apart than a box and a bit: the branches stay close to the trunk.
        float lane = Mathf.Min(Mathf.Max(0f, (rect.height - boxH - 60f) / 2f), boxH + 26f) * (branched ? 1f : 0.45f);
        float midY = rect.center.y + 12f;
        var boxes = new Rect[n];
        for (int i = 0; i < n; i++)
            boxes[i] = new Rect(rect.x + elements[i].Col * stepX, midY + elements[i].Row * lane - boxH / 2f, boxW, boxH);

        var visited = new HashSet<int>(session.Visited);
        bool Dim(int i) => elements[i].Branch != 0 && session.ChoiceAt(elements[i].ForkAt) is var c and > 0 && c != elements[i].Branch;

        // Links: elbows (across, up or down, across); walked ones green, ones into the branch not taken faint.
        var walked = new List<Vector2>();
        for (int i = 0; i < n; i++)
        {
            foreach (int j in elements[i].Links)
            {
                bool done = visited.Contains(i) && visited.Contains(j);
                Color line = done ? new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.9f)
                    : Dim(j) || Dim(i) ? new Color(NetrunUI.Line.r, NetrunUI.Line.g, NetrunUI.Line.b, 0.18f)
                    : NetrunUI.Line;
                List<Vector2> pts = Elbow(boxes[i], boxes[j]);
                for (int k = 0; k + 1 < pts.Count; k++)
                    Segment(pts[k], pts[k + 1], line);
                if (done)
                    walked.AddRange(pts);
            }
        }
        DrawPacket(walked);

        for (int i = 0; i < n; i++)
            DrawStep(boxes[i], elements[i], i == session.Position, visited.Contains(i) && i != session.Position, Dim(i));

        // Branch tags over the first step of each branch.
        for (int i = 0; i < n; i++)
        {
            if (!session.IsFork(i))
                continue;
            for (int k = 0; k < elements[i].Links.Count; k++)
            {
                int first = elements[i].Links[k];
                int branch = elements[first].Branch;
                if (branch == 0)
                    continue;
                Color c = branch == 1 ? NetrunUI.Bad : NetrunUI.Ok;
                bool dim = Dim(first);
                string tag = (branch == 1 ? "NR_DiveBranchHard" : "NR_DiveBranchEasy").Translate();
                Text.Font = GameFont.Tiny;
                float tw = Text.CalcSize(tag).x + 12f;
                Rect bx = boxes[first];
                var r = branch == 1 ? new Rect(bx.x, bx.y - 22f, tw, 18f) : new Rect(bx.x, bx.yMax + 4f, tw, 18f);
                Widgets.DrawBoxSolid(r, new Color(c.r, c.g, c.b, dim ? 0.08f : 0.22f));
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(r, tag.Colorize(new Color(c.r, c.g, c.b, dim ? 0.4f : 1f)));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
        }
    }

    /// <summary>Points of an elbow link from the right edge of <paramref name="a"/> into the left edge of <paramref name="b"/>.</summary>
    private static List<Vector2> Elbow(Rect a, Rect b)
    {
        var from = new Vector2(a.xMax, a.center.y);
        var to = new Vector2(b.x, b.center.y);
        if (Mathf.Abs(from.y - to.y) < 3f)
            return [from, new Vector2(to.x, from.y)];
        float mx = Mathf.Round((from.x + to.x) / 2f);
        return [from, new Vector2(mx, from.y), new Vector2(mx, to.y), to];
    }

    /// <summary>A 2 px axis-aligned bar between two points with a faint glow under it.</summary>
    private static void Segment(Vector2 p, Vector2 q, Color color)
    {
        Rect r = Mathf.Abs(p.y - q.y) < 0.5f
            ? new Rect(Mathf.Min(p.x, q.x), p.y - 1f, Mathf.Abs(q.x - p.x) + 1f, 2f)
            : new Rect(p.x - 1f, Mathf.Min(p.y, q.y), 2f, Mathf.Abs(q.y - p.y) + 1f);
        Widgets.DrawBoxSolid(r.ExpandedBy(2f), new Color(color.r, color.g, color.b, color.a * 0.15f));
        Widgets.DrawBoxSolid(r, color);
    }

    /// <summary>A data packet running along the walked path (the points come in link order).</summary>
    private static void DrawPacket(List<Vector2> pts)
    {
        if (!NetrunMod.Settings.Animations || pts.Count < 2 || Event.current.type != EventType.Repaint)
            return;
        float total = 0f;
        for (int i = 0; i + 1 < pts.Count; i++)
            total += Vector2.Distance(pts[i], pts[i + 1]);
        if (total < 1f)
            return;
        float d = Time.realtimeSinceStartup * 120f % total;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            float len = Vector2.Distance(pts[i], pts[i + 1]);
            if (d <= len && len > 0f)
            {
                Vector2 p = Vector2.Lerp(pts[i], pts[i + 1], d / len);
                GUI.color = new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.45f);
                GUI.DrawTexture(new Rect(p.x - 8f, p.y - 8f, 16f, 16f), NetrunUI.GlowTex);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(p.x - 3f, p.y - 3f, 6f, 6f), NetrunUI.Disc);
                return;
            }
            d -= len;
        }
    }

    /// <summary>Header height: the title wraps onto a second line instead of being cut.</summary>
    private static float HeaderHeight(DiveElement e, float boxW)
    {
        Text.Font = GameFont.Tiny;
        float h = Text.CalcHeight(DiveSession.ElementLabel(e).ToUpper(), boxW - 2f - 30f - 18f);
        return Mathf.Clamp(h + 6f, 24f, 36f);
    }

    private static void DrawStep(Rect box, DiveElement e, bool here, bool passed, bool dim = false)
    {
        Color color = ElementColor(e);
        float pulse = here && NetrunMod.Settings.Animations ? 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 3f) : 0.5f;
        if (here)
            NetrunUI.Glow(box, color, 0.35f + 0.3f * pulse, 10f);
        Widgets.DrawBoxSolid(box, NetrunUI.BgWindow);
        if (here)
            Widgets.DrawBoxSolid(box, new Color(color.r, color.g, color.b, 0.12f));
        float alpha = passed ? 0.5f : 1f;
        Color edge = new(color.r, color.g, color.b, alpha * (e.Hidden ? 0.6f : 1f));
        GUI.color = edge;
        Widgets.DrawBox(box, here ? 2 : 1);
        GUI.color = Color.white;
        Corners(box, edge, here ? 10f : 7f);

        // Header strip: icon and title (wraps to two lines), the state mark on the right.
        Text.Font = GameFont.Tiny;
        float hh = HeaderHeight(e, box.width);
        var header = new Rect(box.x + 1f, box.y + 1f, box.width - 2f, hh);
        Widgets.DrawBoxSolid(header, new Color(color.r, color.g, color.b, 0.18f * alpha));
        (Texture2D icon, bool tint) = ElementIcon(e);
        float tx = header.x + 6f;
        if (icon != null)
        {
            GUI.color = tint ? edge : new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(header.x + 4f, header.center.y - 10f, 20f, 20f), icon);
            GUI.color = Color.white;
            tx += 24f;
        }
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        string title = DiveSession.ElementLabel(e).ToUpper();
        Widgets.Label(new Rect(tx, header.y, header.xMax - tx - 18f, header.height), title.Colorize(edge));
        string state = e.Hidden ? "?" : passed || e.Cleared && e.Kind != DiveElementKind.Lobby ? "✓" : e.Blocks ? "■" : string.Empty;
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(new Rect(header.xMax - 18f, header.y, 14f, header.height), state.Colorize(edge));
        Text.Anchor = TextAnchor.UpperLeft;

        // Body: the info, a little larger spacing; strength as pips under it.
        string info = ElementInfo(e);
        var body = new Rect(box.x + 8f, header.yMax + 6f, box.width - 16f, box.yMax - header.yMax - 10f);
        // Terminal style: a prompt mark, the text, numbers in the step's colour.
        Text.Font = GameFont.Small;
        string styled = System.Text.RegularExpressions.Regex.Replace(info, @"\d+", m => m.Value.Colorize(passed ? NetrunUI.Muted : color));
        Widgets.Label(body, ">".Colorize(new Color(color.r, color.g, color.b, 0.6f * alpha)) + " " + styled.Colorize(passed ? NetrunUI.Muted : NetrunUI.TextColor));
        Text.Font = GameFont.Tiny;
        if (!e.Hidden && !e.Cleared && e.Strength > 0 && e.Kind is DiveElementKind.Gate or DiveElementKind.Ice)
        {
            int pips = Mathf.Clamp(e.Strength, 1, 10);
            float py = box.yMax - 11f;
            for (int i = 0; i < pips; i++)
                Widgets.DrawBoxSolid(new Rect(body.x + i * 9f, py, 6f, 5f), new Color(color.r, color.g, color.b, alpha));
        }
        Text.Font = GameFont.Small;

        // "You are here": a tag above the box instead of a line inside it.
        if (here)
        {
            Text.Font = GameFont.Tiny;
            string tag = "NR_DiveInfo_Here".Translate();
            float tw = Text.CalcSize(tag).x + 12f;
            var tagRect = new Rect(box.x, box.y - 22f, tw, 18f);
            Widgets.DrawBoxSolid(tagRect, new Color(color.r, color.g, color.b, 0.25f + 0.2f * pulse));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(tagRect, tag.Colorize(Color.white));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        if (dim)
            Widgets.DrawBoxSolid(box.ExpandedBy(3f), new Color(NetrunUI.BgWindow.r, NetrunUI.BgWindow.g, NetrunUI.BgWindow.b, 0.7f));
        NetrunUI.Tip(box, title + "\n" + info);
    }

    /// <summary>Small corner brackets around a step box.</summary>
    private static void Corners(Rect r, Color color, float len)
    {
        const float t = 2f;
        Widgets.DrawBoxSolid(new Rect(r.x - 2f, r.y - 2f, len, t), color);
        Widgets.DrawBoxSolid(new Rect(r.x - 2f, r.y - 2f, t, len), color);
        Widgets.DrawBoxSolid(new Rect(r.xMax + 2f - len, r.y - 2f, len, t), color);
        Widgets.DrawBoxSolid(new Rect(r.xMax, r.y - 2f, t, len), color);
        Widgets.DrawBoxSolid(new Rect(r.x - 2f, r.yMax, len, t), color);
        Widgets.DrawBoxSolid(new Rect(r.x - 2f, r.yMax + 2f - len, t, len), color);
        Widgets.DrawBoxSolid(new Rect(r.xMax + 2f - len, r.yMax, len, t), color);
        Widgets.DrawBoxSolid(new Rect(r.xMax, r.yMax + 2f - len, t, len), color);
    }

    private static string ElementInfo(DiveElement e)
    {
        string info = e.Hidden
            ? "NR_DiveInfo_Hidden".Translate()
            : e.Kind switch
            {
                DiveElementKind.Gate => e.Cleared ? "NR_DiveInfo_Open".Translate() : "NR_DiveInfo_Gate".Translate(e.Strength),
                DiveElementKind.Ice => e.Cleared ? "NR_DiveInfo_Broken".Translate() : "NR_DiveInfo_Ice".Translate(e.Strength),
                DiveElementKind.Files or DiveElementKind.Memory => e.Looted ? "NR_DiveInfo_Looted".Translate() : ("NR_DiveInfo_" + e.Kind).Translate(),
                _ => ("NR_DiveInfo_" + e.Kind).Translate(),
            };
        return info;
    }

    // ---- HUD panel ----

    private static void DrawPanel(Rect rect, DiveSession session)
    {
        NetrunUI.Box(rect, NetrunUI.BgCard, new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.35f));
        Rect outer = rect.ContractedBy(10f);
        // The log takes the bottom part; the rest scrolls when the window is short.
        float logHeight = Mathf.Clamp(outer.height * 0.36f, 120f, 260f);
        var top = new Rect(outer.x, outer.y, outer.width, outer.height - logHeight - 8f);
        var log = new Rect(outer.x, top.yMax + 8f, outer.width, logHeight);

        bool scrolls = _panelHeight > top.height;
        var inner = new Rect(0f, 0f, top.width - (scrolls ? 16f : 0f), Mathf.Max(_panelHeight, top.height));
        NetrunUI.BeginScroll(top, ref _panelScroll, inner);
        float y = 0f;

        if (session.Finished)
        {
            y = HudHeader(inner, y, "NR_DiveOver".Translate(), null);
            y = DrawHaul(inner, y, session);
        }
        else
        {
            string highlight = session.Training ? TrainingCoach.Get(session).Action : null;
            if (session.AtUndecidedFork)
            {
                float gFork = y;
                y = DrawFork(inner, y, session, highlight == "Choose");
                GuideRegistry.InScroll("dive.fork", top, _panelScroll, new Rect(inner.x, gFork, inner.width, y - gFork));
            }
            // Actions: pips for the actions left, buttons in a two-column grid.
            float gAct = y;
            y = HudHeader(inner, y, "NR_DiveHudActions".Translate(), Pips(session.ActionsLeft, session.ActionsPerTurn, NetrunUI.Accent));
            float bw = (inner.width - 6f) / 2f;
            int index = 0;
            float gridY = y;
            void Act(string key, Color color, AcceptanceReport report, System.Action action, string tip = null)
            {
                var r = new Rect(inner.x + index % 2 * (bw + 6f), gridY + index / 2 * 34f, bw, 30f);
                if (NetrunUI.Button(r, ("NR_DiveAct_" + key).Translate(), color, report, tip ?? ("NR_DiveActTip_" + key).Translate(), NetrunTex.CyberIcon("Action_" + key)))
                    action();
                if (highlight == key && report.Accepted)
                    PulseOutline(r);
                index++;
            }
            Act("Back", NetrunUI.Accent, session.CanBack(), session.Back);
            Act("Move", NetrunUI.Accent, session.CanMove(), session.Move);
            Act("Scan", NetrunUI.Ok, session.CanScan(), session.Scan);
            AcceptanceReport hack = session.CanHack();
            Act("Hack", NetrunUI.Warn, hack, session.Hack, hack.Accepted ? "NR_DiveActTip_HackChance".Translate(session.CurrentHackChance.ToStringPercent()) : null);
            Act("Download", NetrunUI.Accent, session.CanDownload(), session.Download);
            Act("Exit", NetrunUI.Ok, session.CanExit(), () =>
            {
                session.Exit();
                GatewayManager.Get()?.ClearFinished();
            });
            y = gridY + (index + 1) / 2 * 34f + 6f;


            GuideRegistry.InScroll("dive.actions", top, _panelScroll, new Rect(inner.x, gAct, inner.width, y - gAct));
            float gGauge = y;
            // Gauges: turn and trace.
            int maxTurns = session.MaxTurns;
            y = Gauge(inner, y, "NR_DiveHudTurn".Translate(session.Turn, maxTurns), session.Turn / (float)maxTurns,
                session.Turn >= maxTurns - 1 ? NetrunUI.Bad : NetrunUI.Accent, "NR_DiveTurn".Translate(session.Turn, maxTurns));
            float heat = session.Trace * session.Node.HeatPerTrace;
            y = Gauge(inner, y, "NR_DiveHudTrace".Translate(session.Trace.ToString("0.#"), heat.ToString("0.#")), session.Trace / TraceGaugeMax,
                HeatLevelUtility.LevelOf(Mathf.Min(99f, session.Trace / TraceGaugeMax * 100f)).Color(), "NR_DiveHudTraceTip".Translate());
            if (session.AlertApplies)
                y = AlertRow(inner, y, session);

            GuideRegistry.InScroll("dive.gauges", top, _panelScroll, new Rect(inner.x, gGauge, inner.width, y - gGauge));
            float gProg = y;
            // Programs and daemons as chips with icons and charges.
            y = HudHeader(inner, y + 4f, "NR_DivePrograms".Translate(), null);
            if (session.Programs.Count == 0 && session.Daemons.Count == 0)
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(inner.x, y, inner.width, 20f), "NR_DiveNoPrograms".Translate().Colorize(NetrunUI.Muted));
                Text.Font = GameFont.Small;
                y += 22f;
            }
            else
            {
                float x = inner.x;
                foreach (DiveProgram p in session.Programs.Distinct())
                {
                    int charges = p switch
                    {
                        DiveProgram.Shield => session.ShieldCharges,
                        DiveProgram.Decoy => session.DecoyCharges,
                        DiveProgram.Killer => session.KillerCharges,
                        DiveProgram.IceBreaker => session.BreakerCharges,
                        DiveProgram.Exec => System.Math.Max(0, session.ExecTurns - session.Turn + 1),
                        _ => -1,
                    };
                    string label = ("NR_Program_" + p).Translate() + (charges >= 0 ? " ×" + charges : string.Empty);
                    Chip(inner, ref x, ref y, NetrunTex.CyberIcon("Program_" + p) ?? HePrograms.IconDef(p)?.uiIcon, label, charges != 0, ("NR_ProgramDesc_" + p).Translate(NetrunDefOf.NR_Cyberspace.programCost));
                }
                foreach (DiveDaemon d in session.Daemons)
                    Chip(inner, ref x, ref y, NetrunTex.DaemonIcon(d), ("NR_Daemon_" + d).Translate(), true, ("NR_DaemonDesc_" + d).Translate());
                y += 30f;
            }

            GuideRegistry.InScroll("dive.programs", top, _panelScroll, new Rect(inner.x, gProg, inner.width, y - gProg));
            // Cyberspace events of this dive: after the programs, before the haul.
            if (session.HunterActive)
                y = Warning(inner, y, "NR_DiveHunterActive".Translate(), NetrunUI.Bad, NetrunTex.CyberIcon("Event_HunterICE"));
            if (session.Node.Rival && !session.RivalTook)
                y = Warning(inner, y, "NR_DiveRivalActive".Translate(System.Math.Max(0, NetrunDefOf.NR_Cyberspace.rivalTurns - session.Turn)), NetrunUI.Warn, NetrunTex.CyberIcon("Event_Rival"));
            if (session.Storm)
                y = Warning(inner, y, "NR_DiveStorm".Translate(), NetrunUI.Purple, NetrunTex.CyberIcon("Event_DataStorm"));
            float gHaul = y;
            y = DrawHaul(inner, y + 2f, session);
            GuideRegistry.InScroll("dive.haul", top, _panelScroll, new Rect(inner.x, gHaul, inner.width, y - gHaul));
        }
        _panelHeight = y + 4f;
        Widgets.EndScrollView();

        GuideRegistry.Area("dive.log", log);
        DrawLog(log, session);
    }

    /// <summary>At a fork: two cards, hard and easy. Numbers show after a scan; the choice is final.</summary>
    private static float DrawFork(Rect inner, float y, DiveSession session, bool highlight = false)
    {
        bool scanned = session.ForkScanned.Contains(session.Position);
        y = HudHeader(inner, y, "NR_DiveForkTitle".Translate(), scanned ? null : "?".Colorize(NetrunUI.Warn));
        for (int branch = 1; branch <= 2; branch++)
        {
            (string reward, int ice, float trace) = session.BranchInfo(session.Position, branch);
            Color c = branch == 1 ? NetrunUI.Bad : NetrunUI.Ok;
            string title = (branch == 1 ? "NR_DiveBranchHard" : "NR_DiveBranchEasy").Translate();
            string desc = (branch == 1 ? "NR_DiveBranchHardDesc" : "NR_DiveBranchEasyDesc").Translate();
            string numbers = scanned
                ? (string)"NR_DiveBranchNumbers".Translate(reward, ice > 0 ? (string)"NR_DiveBranchIce".Translate(ice) : (string)"NR_DiveBranchNoIce".Translate(),
                    trace.ToString("0"))
                : (string)"NR_DiveBranchScanHint".Translate();
            Text.Font = GameFont.Tiny;
            float tw = inner.width - 20f;
            float h = 8f + 20f + Text.CalcHeight(desc, tw) + 4f + Text.CalcHeight(numbers, tw) + 6f + 28f + 8f;
            var card = new Rect(inner.x, y, inner.width, h);
            NetrunUI.Box(card, NetrunUI.BgWindow, new Color(c.r, c.g, c.b, 0.5f));
            Widgets.DrawBoxSolid(new Rect(card.x, card.y, 3f, card.height), c);
            float cy = card.y + 6f;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(card.x + 10f, cy, tw, 22f), title.Colorize(c));
            cy += 22f;
            Text.Font = GameFont.Tiny;
            float dh = Text.CalcHeight(desc, tw);
            Widgets.Label(new Rect(card.x + 10f, cy, tw, dh), desc.Colorize(NetrunUI.Muted));
            cy += dh + 4f;
            float nh = Text.CalcHeight(numbers, tw);
            Widgets.Label(new Rect(card.x + 10f, cy, tw, nh), numbers.Colorize(scanned ? NetrunUI.TextColor : NetrunUI.Warn));
            cy += nh + 6f;
            Text.Font = GameFont.Small;
            int b = branch;
            var take = new Rect(card.x + 10f, cy, tw, 28f);
            if (NetrunUI.Button(take, "NR_DiveBranchTake".Translate(), c, true, "NR_DiveBranchTakeTip".Translate()))
                session.Choose(b);
            if (highlight)
                PulseOutline(take);
            y = card.yMax + 6f;
        }
        return y + 4f;
    }

    private static void Chip(Rect inner, ref float x, ref float y, Texture2D icon, string label, bool active, string tip)
    {
        Text.Font = GameFont.Tiny;
        float w = 26f + Text.CalcSize(label).x + 8f;
        if (x + w > inner.xMax)
        {
            x = inner.x;
            y += 28f;
        }
        var r = new Rect(x, y, w, 24f);
        Color c = active ? NetrunUI.Ok : NetrunUI.Muted;
        NetrunUI.Box(r, NetrunUI.BgWindow, new Color(c.r, c.g, c.b, active ? 0.8f : 0.35f));
        if (icon != null)
        {
            GUI.color = active ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            GUI.DrawTexture(new Rect(r.x + 3f, r.y + 3f, 18f, 18f), icon);
            GUI.color = Color.white;
        }
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(r.x + 24f, r.y, w - 26f, r.height), label.Colorize(c));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        NetrunUI.Tip(r, tip);
        x += w + 4f;
    }

    private static float HudHeader(Rect inner, float y, string label, string right)
    {
        Text.Font = GameFont.Tiny;
        Widgets.DrawBoxSolid(new Rect(inner.x, y + 6f, 3f, 12f), NetrunUI.Accent);
        Widgets.Label(new Rect(inner.x + 8f, y + 2f, inner.width - 90f, 20f), ("// " + label.ToUpper()).Colorize(NetrunUI.Accent));
        if (right != null)
        {
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inner.xMax - 90f, y + 2f, 90f, 20f), right);
            Text.Anchor = TextAnchor.UpperLeft;
        }
        Text.Font = GameFont.Small;
        GUI.color = new Color(NetrunUI.Accent.r, NetrunUI.Accent.g, NetrunUI.Accent.b, 0.35f);
        Widgets.DrawLineHorizontal(inner.x, y + 22f, inner.width);
        GUI.color = Color.white;
        return y + 28f;
    }

    /// <summary>Node alert: one pip per failed gate; the last one blinks once the patrol is awake.</summary>
    private static float AlertRow(Rect inner, float y, DiveSession session)
    {
        int threshold = session.AlertThreshold;
        bool awake = session.PatrolAwake;
        bool blink = awake && (!NetrunMod.Settings.Animations || Mathf.FloorToInt(Time.realtimeSinceStartup * 2f) % 2 == 0);
        string pips = new string('●', Mathf.Min(session.NodeAlert, threshold - (awake ? 1 : 0))).Colorize(NetrunUI.Warn)
            + (awake ? "●".Colorize(blink ? NetrunUI.Bad : NetrunUI.Warn) : "")
            + new string('○', Mathf.Max(0, threshold - session.NodeAlert)).Colorize(NetrunUI.Muted);
        Text.Font = GameFont.Tiny;
        var row = new Rect(inner.x, y, inner.width, 18f);
        Widgets.Label(row, ((string)"NR_DiveHudAlert".Translate() + (awake ? ": " + (string)"NR_DiveHudPatrol".Translate() : "")).Colorize(awake ? NetrunUI.Bad : NetrunUI.Muted));
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(row, pips);
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        NetrunUI.Tip(row, "NR_DiveHudAlertTip".Translate(threshold, NetrunDefOf.NR_Cyberspace.alertPatrolChance.ToStringPercent()));
        return y + 22f;
    }

    private static string Pips(int left, int total, Color color)
        => new string('●', Mathf.Max(0, left)).Colorize(color) + new string('○', Mathf.Max(0, total - left)).Colorize(NetrunUI.Muted);

    private static float Gauge(Rect inner, float y, string label, float fraction, Color color, string tip)
    {
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(inner.x, y, inner.width, 16f), label.Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        NetrunUI.Bar(new Rect(inner.x, y + 17f, inner.width, 8f), fraction, color);
        NetrunUI.Tip(new Rect(inner.x, y, inner.width, 26f), tip);
        return y + 32f;
    }

    private static float DrawHaul(Rect inner, float y, DiveSession session)
    {
        y = HudHeader(inner, y, "NR_DiveHudHaul".Translate(), null);
        var haul = new List<(Texture2D icon, string label, string tip)>();
        if (session.LootCyberdata > 0)
            haul.Add((NetrunDefOf.USH_Cyberdata.uiIcon, "×" + session.LootCyberdata, NetrunDefOf.USH_Cyberdata.LabelCap));
        if (session.LootAdvanced > 0)
            haul.Add((NetrunDefOf.USH_AdvancedCyberdata.uiIcon, "×" + session.LootAdvanced, NetrunDefOf.USH_AdvancedCyberdata.LabelCap));
        if (session.LootSilver > 0)
            haul.Add((ThingDefOf.Silver.uiIcon, "×" + session.LootSilver, ThingDefOf.Silver.LabelCap));
        foreach (IGrouping<ThingDef, ThingDef> g in session.LootItems.GroupBy(d => d))
            haul.Add((g.Key.uiIcon, "×" + g.Count(), g.Key.LabelCap));
        Text.Font = GameFont.Tiny;
        if (haul.Count == 0)
        {
            Widgets.Label(new Rect(inner.x, y, inner.width, 20f), "NR_DiveHudHaulEmpty".Translate().Colorize(NetrunUI.Muted));
            y += 22f;
        }
        float x = inner.x;
        foreach ((Texture2D icon, string label, string tip) in haul)
        {
            float w = 28f + Text.CalcSize(label).x + 6f;
            if (x + w > inner.xMax)
            {
                x = inner.x;
                y += 30f;
            }
            var r = new Rect(x, y, w, 26f);
            Widgets.DrawBoxSolid(r, NetrunUI.BgWindow);
            GUI.DrawTexture(new Rect(r.x + 2f, r.y + 1f, 24f, 24f), icon);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(r.x + 28f, r.y, w - 28f, r.height), label.Colorize(NetrunUI.Ok));
            Text.Anchor = TextAnchor.UpperLeft;
            NetrunUI.Tip(r, tip);
            x += w + 4f;
        }
        Text.Font = GameFont.Small;
        return y + (haul.Count > 0 ? 32f : 0f);
    }

    private static float Warning(Rect inner, float y, string text, Color color, Texture2D icon)
    {
        float h = Mathf.Max(28f, Text.CalcHeight(text, inner.width - 40f) + 6f);
        var row = new Rect(inner.x, y, inner.width, h);
        Widgets.DrawBoxSolid(row, new Color(color.r, color.g, color.b, 0.1f));
        Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), color);
        if (icon != null)
            GUI.DrawTexture(new Rect(row.x + 7f, row.y + (h - 22f) / 2f, 22f, 22f), icon);
        Widgets.Label(new Rect(row.x + 34f, row.y + 3f, row.width - 38f, h), text.Colorize(color));
        return y + h + 4f;
    }

    // ---- Log: a terminal ----

    private static void DrawLog(Rect rect, DiveSession session)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.03f, 0.05f, 0.04f));
        GUI.color = new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.35f);
        Widgets.DrawBox(rect);
        GUI.color = Color.white;
        Text.Font = GameFont.Tiny;
        string header = "// " + ((string)"NR_DiveLogHeader".Translate()).ToUpper() + " · " + session.Node.Name.ToLower();
        Widgets.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 16f), header.Colorize(NetrunUI.Ok));
        var outer = new Rect(rect.x + 4f, rect.y + 20f, rect.width - 8f, rect.height - 24f);
        // D6: the race with a rival netrunner, over the log: the turns left before the files are gone, red at the end.
        if (session.Node.Rival && !session.RivalTook && !session.Finished)
        {
            int turns = Mathf.Max(1, NetrunDefOf.NR_Cyberspace.rivalTurns);
            int left = Mathf.Max(0, turns - session.Turn);
            Color c = left <= 3 ? NetrunUI.Bad : NetrunUI.Warn;
            var strip = new Rect(outer.x + 2f, outer.y, outer.width - 4f, 18f);
            Widgets.DrawBoxSolid(strip, new Color(c.r, c.g, c.b, 0.08f));
            Texture2D icon = NetrunTex.CyberIcon("Event_Rival");
            if (icon != null)
                GUI.DrawTexture(new Rect(strip.x + 2f, strip.y + 1f, 16f, 16f), icon);
            string label = "NR_DiveRivalBar".Translate(left);
            float lw = Mathf.Min(strip.width * 0.55f, Text.CalcSize(label).x + 4f);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = c;
            Widgets.Label(new Rect(strip.x + 22f, strip.y, lw, strip.height), label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            var bar = new Rect(strip.x + 26f + lw, strip.y + 6f, strip.xMax - strip.x - 30f - lw, 6f);
            if (bar.width > 20f)
            {
                // Segments: one per turn the rival needs, lit for the turns still left.
                float seg = bar.width / turns;
                for (int i = 0; i < turns; i++)
                    Widgets.DrawBoxSolid(new Rect(bar.x + i * seg + 1f, bar.y, seg - 2f, bar.height), i < left ? c : new Color(c.r, c.g, c.b, 0.15f));
            }
            NetrunUI.Tip(strip, () => "NR_DiveRivalBarTip".Translate(left));
            outer.yMin += 21f;
        }

        List<string> lines = session.Log;
        if (lines.Count != _logCount)
        {
            _logCount = lines.Count;
            _logChangedAt = Time.realtimeSinceStartup;
            _logScroll.y = float.MaxValue;
        }
        float w = outer.width - 16f;
        float total = 18f;
        foreach (string line in lines)
            total += Text.CalcHeight("> " + line, w) + 1f;
        var view = new Rect(0f, 0f, w, Mathf.Max(total, outer.height));
        NetrunUI.BeginScroll(outer, ref _logScroll, view);
        float y = 0f;
        for (int i = 0; i < lines.Count; i++)
        {
            string text = lines[i];
            bool last = i == lines.Count - 1;
            // The newest line types itself out when animations are on.
            if (last && NetrunMod.Settings.Animations)
            {
                string plain = text.StripTags();
                int shown = Mathf.Clamp(Mathf.FloorToInt((Time.realtimeSinceStartup - _logChangedAt) * 90f), 0, plain.Length);
                if (shown < plain.Length)
                    text = plain.Substring(0, shown);
            }
            string prefix = last ? ">_ " : "> ";
            float h = Text.CalcHeight(prefix + text, w);
            string body = text.Contains("<color") ? text : text.Colorize(new Color(0.75f, 0.86f, 0.75f));
            Widgets.Label(new Rect(0f, y, w, h), prefix.Colorize(NetrunUI.Ok) + body);
            y += h + 1f;
        }
        bool blink = !NetrunMod.Settings.Animations || Mathf.FloorToInt(Time.realtimeSinceStartup * 2f) % 2 == 0;
        if (!session.Finished && blink)
            Widgets.Label(new Rect(0f, y, 20f, 16f), "█".Colorize(NetrunUI.Ok));
        Widgets.EndScrollView();
        Text.Font = GameFont.Small;
    }

    public static void HeaderChips(float xMax, float y, DiveSession session)
    {
        float x = NetrunUI.HeaderChip(xMax, y, "NR_DiveTraceChip".Translate(session.Trace.ToString("0")).Colorize(NetrunUI.Bad)) - 8f;
        NetrunUI.HeaderChip(x, y, "NR_DiveActionsChip".Translate(session.ActionsLeft, session.ActionsPerTurn));
    }
}
