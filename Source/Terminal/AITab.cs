using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// The Server room node (mockup 2-ai-node): sections by where the AI lives, one card per AI with a colored
/// stripe, a framed portrait, name and status, archetype and request tags, trust and hunger bars on one
/// line and a row of colored buttons.
/// </summary>
[StaticConstructorOnStartup]
public static class AITab
{
    private const float SectionHeight = 36f;
    private const float CardGap = 10f;
    private const float PortraitSize = 96f;
    private const float ButtonHeight = 34f;
    private const float ButtonGap = 6f;
    private const float Pad = 12f;

    private static readonly Color HostColor = new(0.45f, 1.00f, 0.55f);

    public static void Draw(Rect rect, Map map, ref Vector2 scroll)
    {
        AIManager manager = AIManager.Get();
        if (manager == null || map == null)
            return;

        var listRect = rect;
        IReadOnlyList<NetAI> ais = manager.AIs;
        if (ais.Count == 0)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(listRect, "NR_AINone".Translate().Colorize(NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            return;
        }

        var groups = new List<(string label, Color color, List<NetAI> list)>
        {
            ("NR_AISectionServer".Translate(), NetrunUI.Accent, ais.Where(a => a.InMainServer).ToList()),
            ("NR_AISectionCoprocessor".Translate(), NetrunUI.Purple, ais.Where(a => a.InCoprocessor).ToList()),
            ("NR_AISectionHost".Translate(), HostColor, ais.Where(a => a.InHost).ToList()),
            ("NR_AISectionOther".Translate(), NetrunUI.Muted, ais.Where(a => !a.InMainServer && !a.InCoprocessor && !a.InHost).ToList()),
        };
        groups.RemoveAll(g => g.list.Count == 0);

        float width = listRect.width - 16f;
        float height = groups.Sum(g => SectionHeight + g.list.Sum(a => CardHeight(a, map, manager, width) + CardGap));
        var viewRect = new Rect(0f, 0f, width, height);

        Widgets.BeginScrollView(listRect, ref scroll, viewRect);
        float y = 0f;
        foreach ((string label, Color color, List<NetAI> list) in groups)
        {
            // The server section counts slots (1/3), the others count AIs.
            string count = color == NetrunUI.Accent ? ServerSlots(map) ?? list.Count.ToString() : list.Count.ToString();
            y = NetrunUI.Section(new Rect(0f, y, width, SectionHeight), $"{label} ({count})");
            foreach (NetAI ai in list)
            {
                float h = CardHeight(ai, map, manager, width);
                DrawCard(new Rect(0f, y, width, h), ai, map, manager, color);
                y += h + CardGap;
            }
        }
        Widgets.EndScrollView();
    }

    /// <summary>Used and total slots of the colony's AI server, for the section title: "1/3".</summary>
    private static string ServerSlots(Map map)
    {
        CompAIServer server = map.listerBuildings.AllBuildingsColonistOfDef(NetrunDefOf.NR_AIServer).Select(b => b.GetComp<CompAIServer>()).FirstOrDefault(c => c != null);
        return server == null ? null : $"{server.Used}/{server.Slots}";
    }

    // ---- Card ----

    private static (string label, Color color) Status(NetAI ai)
    {
        if (ai.Escaping)
            return ("NR_AIStatusEscaping".Translate(), NetrunUI.Bad);
        if (ai.Isolated)
            return ("NR_AIStatusIsolated".Translate(), NetrunUI.Warn);
        if (!ai.Powered)
            return ("NR_AIStatusAsleep".Translate(), NetrunUI.Muted);
        return ("NR_AIStatusActive".Translate(), NetrunUI.TextColor);
    }

    private const float TextIndent = 4f + Pad + PortraitSize + 14f;

    private static float CardHeight(NetAI ai, Map map, AIManager manager, float width)
    {
        float textWidth = width - TextIndent - Pad;
        int rows = ButtonRows(Buttons(ai, map, manager), textWidth);
        float content = 30f + 24f + 26f + 8f + rows * (ButtonHeight + ButtonGap) - ButtonGap + (ai.Escaping ? 22f : 0f);
        return Mathf.Max(PortraitSize, content) + 2 * Pad;
    }

    private static void DrawCard(Rect rect, NetAI ai, Map map, AIManager manager, Color color)
    {
        NetrunUI.Card(rect, color);
        if (ai.HasRequest)
        {
            GUI.color = NetrunUI.Warn;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;
        }

        // Portrait framed in the location color.
        var portrait = new Rect(rect.x + 4f + Pad, rect.y + Pad, PortraitSize, PortraitSize);
        NetrunUI.Box(portrait, NetrunUI.BgWindow, color, 2);
        GUI.DrawTexture(portrait.ContractedBy(6f), NetrunTex.Portrait(ai.Personality));
        TooltipHandler.TipRegion(portrait, ai.LocationText);

        float x = rect.x + TextIndent;
        float width = rect.xMax - Pad - x;
        float y = rect.y + Pad - 2f;

        // Name and status badge.
        Text.Font = GameFont.Medium;
        float nameWidth = Mathf.Min(Text.CalcSize(ai.Name).x, width * 0.6f);
        Widgets.Label(new Rect(x, y, nameWidth + 4f, 30f), ai.Name);
        Text.Font = GameFont.Small;
        (string status, Color statusColor) = Status(ai);
        NetrunUI.Chip(x + nameWidth + 10f, y + 6f, status.ToUpper(), statusColor, ai.LocationText);
        y += 30f;

        // Archetype tag (icon, personality color) and the request, if any.
        GUI.DrawTexture(new Rect(x, y + 1f, 18f, 18f), NetrunTex.Portrait(ai.Personality));
        float tagX = x + 22f;
        tagX += NetrunUI.Chip(tagX, y + 1f, ai.Personality.Label().ToUpper(), ai.Personality.Color(), ai.Personality.Description()) + 6f;
        if (ai.HasRequest)
        {
            string left = ai.RequestDeadlineTick > Find.TickManager.TicksGame
                ? (ai.RequestDeadlineTick - Find.TickManager.TicksGame).ToStringTicksToPeriod()
                : "0";
            NetrunUI.Chip(tagX, y + 1f, "NR_AIRequestTag".Translate(AIManager.RequestText(ai), left), NetrunUI.Warn);
        }
        y += 24f;

        // Trust and hunger on one line.
        Text.Anchor = TextAnchor.MiddleLeft;
        float half = width / 2f;
        DrawGauge(new Rect(x, y, half - 10f, 22f), "NR_AITrustLabel".Translate(), ai.Trust, NetrunTex.Gauge(ai.Trust), "NR_AITrustTip".Translate());
        DrawGauge(new Rect(x + half, y, half, 22f), "NR_AIHungerLabel".Translate(), ai.Hunger, NetrunTex.Gauge(ai.Hunger, highIsGood: false),
            "NR_AIHungerTip".Translate(AIBehavior.FoodText(ai.Personality)));
        Text.Anchor = TextAnchor.UpperLeft;
        y += 26f;

        if (ai.Escaping)
        {
            string escape = "NR_AIEscapeLine".Translate((ai.EscapeAtTick - Find.TickManager.TicksGame).ToStringTicksToPeriod());
            Widgets.Label(new Rect(x, y, width, 22f), escape.Colorize(NetrunUI.Bad));
            y += 22f;
        }

        DrawButtons(new Rect(x, y + 8f, width, rect.yMax - y - 8f - Pad), Buttons(ai, map, manager));
    }

    private static void DrawGauge(Rect rect, string label, float value, Color color, string tip)
    {
        float labelWidth = Text.CalcSize(label).x + 8f;
        Widgets.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), label);
        var bar = new Rect(rect.x + labelWidth, rect.y + (rect.height - 12f) / 2f, Mathf.Max(40f, rect.width - labelWidth - 40f), 12f);
        NetrunUI.Bar(bar, value / 100f, color);
        Widgets.Label(new Rect(bar.xMax + 8f, rect.y, 36f, rect.height), value.ToString("0").Colorize(color));
        TooltipHandler.TipRegion(rect, tip);
    }

    // ---- Buttons ----

    private struct ButtonSpec
    {
        public string Label;
        public Color Color;
        public AcceptanceReport Report;
        public string Tip;
        public Texture2D Icon;
        public System.Action Action;
    }

    private static List<ButtonSpec> Buttons(NetAI ai, Map map, AIManager manager)
    {
        var list = new List<ButtonSpec>();

        void Add(string label, Color color, AcceptanceReport report, string tip, Texture2D icon, System.Action action)
            => list.Add(new ButtonSpec { Label = label, Color = color, Report = report, Tip = tip, Icon = icon, Action = action });

        Add(AIBehavior.ServiceLabel(ai.Personality), NetrunUI.Accent, AIBehavior.CanUseService(ai), AIBehavior.ServiceDescription(ai.Personality),
            NetrunTex.AIUse, () => AIBehavior.UseService(ai, map));
        Add("NR_AIFeed".Translate(), NetrunUI.Warn, AIBehavior.CanFeed(ai, map), "NR_AIFeedTip".Translate(AIBehavior.FoodText(ai.Personality)),
            NetrunTex.AIFeed, () => AIBehavior.Feed(ai, map));
        if (ai.Request == AIRequestKind.Deliver)
            Add("NR_AIFulfil".Translate(), NetrunUI.Warn, manager.CanFulfil(ai, map), "NR_AIFulfilTip".Translate(), NetrunTex.AIFeed,
                () => manager.Fulfil(ai, map));
        if (ai.InMainServer)
        {
            Add("NR_AIUpload".Translate(), HostColor, AIHosting.CanUpload(ai, map), "NR_AIUploadTip".Translate(), NetrunTex.AIEmbody,
                () => AIHosting.OpenUploadMenu(ai, map));
            Add("NR_AIToCoprocessor".Translate(), NetrunUI.Purple, AIHosting.CanMoveToCoprocessor(ai, map), "NR_AIToCoprocessorTip".Translate(),
                NetrunTex.AIToCoprocessor, () => AIHosting.MoveToCoprocessor(ai, map));
        }
        else if (ai.InHost || ai.InCoprocessor)
        {
            Add("NR_AIDownload".Translate(), HostColor, AIHosting.CanDownload(ai, map), "NR_AIDownloadTip".Translate(), NetrunTex.AIReturnServer,
                () => AIHosting.Download(ai, map));
        }
        if (ai.Housed)
        {
            Add(ai.Isolated ? "NR_AIConnect".Translate() : "NR_AIIsolate".Translate(), NetrunUI.Muted, true, "NR_AIIsolateTip".Translate(),
                NetrunTex.AIIsolate, () => ai.Isolated = !ai.Isolated);
            Add("NR_AIErase".Translate(), NetrunUI.Bad, true, "NR_AIEraseTip".Translate(NetrunDefOf.NR_AI.eraseAdvancedCyberdata), NetrunTex.AIErase,
                () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("NR_AIEraseConfirm".Translate(ai.Name), () => manager.Erase(ai), destructive: true)));
        }
        return list;
    }

    private static int ButtonRows(List<ButtonSpec> buttons, float width)
    {
        int rows = 1;
        float x = 0f;
        foreach (ButtonSpec b in buttons)
        {
            float w = NetrunUI.ButtonWidth(b.Label, icon: true);
            if (x > 0f && x + w > width)
            {
                rows++;
                x = 0f;
            }
            x += w + ButtonGap;
        }
        return rows;
    }

    private static void DrawButtons(Rect rect, List<ButtonSpec> buttons)
    {
        float x = rect.x;
        float y = rect.y;
        foreach (ButtonSpec b in buttons)
        {
            float w = NetrunUI.ButtonWidth(b.Label, icon: true);
            if (x > rect.x && x + w > rect.xMax)
            {
                x = rect.x;
                y += ButtonHeight + ButtonGap;
            }
            if (NetrunUI.Button(new Rect(x, y, w, ButtonHeight), b.Label, b.Color, b.Report, b.Tip, b.Icon))
                b.Action();
            x += w + ButtonGap;
        }
    }
}
