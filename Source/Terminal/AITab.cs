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

        List<NetAI> inServer = ais.Where(a => a.InMainServer).ToList();
        var groups = new List<(string label, Color color, List<NetAI> list)>
        {
            ("NR_AISectionServer".Translate(), NetrunUI.Accent, inServer),
            ("NR_AISectionCoprocessor".Translate(), NetrunUI.Purple, ais.Where(a => a.InCoprocessor).ToList()),
            ("NR_AISectionHost".Translate(), HostColor, ais.Where(a => a.InHost).ToList()),
            ("NR_AISectionOther".Translate(), NetrunUI.Muted, ais.Where(a => !a.InMainServer && !a.InCoprocessor && !a.InHost).ToList()),
        };
        groups.RemoveAll(g => g.list.Count == 0);

        GuideRegistry.Area("server.list", listRect);
        float width = listRect.width - 16f;
        float height = groups.Sum(g => SectionHeight + g.list.Sum(a => CardHeight(a, map, manager, width) + CardGap));
        var viewRect = new Rect(0f, 0f, width, height);

        Widgets.BeginScrollView(listRect, ref scroll, viewRect);
        float y = 0f;
        foreach ((string label, Color color, List<NetAI> list) in groups)
        {
            // The server section counts slots (1/3), the others count AIs.
            string count = list == inServer ? ServerSlots(map) ?? list.Count.ToString() : list.Count.ToString();
            y = NetrunUI.Section(new Rect(0f, y, width, SectionHeight), $"{label} ({count})");
            foreach (NetAI ai in list)
            {
                float h = CardHeight(ai, map, manager, width);
                if (ai == groups[0].list[0])
                    GuideRegistry.InScroll("server.card", listRect, scroll, new Rect(0f, y, width, h));
                DrawCard(new Rect(0f, y, width, h), ai, map, manager, color);
                y += h + CardGap;
            }
        }
        Widgets.EndScrollView();
    }

    /// <summary>Used and total slots of the colony's AI server, for the section title: "1/3".</summary>
    private static string ServerSlots(Map map)
    {
        CompAIServer server = NetrunUI.Cached("ai.server." + map.uniqueID,
            () => map.listerBuildings.AllBuildingsColonistOfDef(NetrunDefOf.NR_AIServer).Select(b => b.GetComp<CompAIServer>()).FirstOrDefault(c => c != null), 1f);
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
        return ("NR_AIStatusActive".Translate(), NetrunUI.Ok);
    }

    private const float TextIndent = 4f + Pad + PortraitSize + 14f;

    private static float CardHeight(NetAI ai, Map map, AIManager manager, float width)
    {
        float textWidth = width - TextIndent - Pad;
        int rows = ButtonRows(Buttons(ai, map, manager), textWidth);
        float content = 30f + 24f + 26f + InfoRowsHeight(ai) + 8f + rows * (ButtonHeight + ButtonGap) - ButtonGap + (ai.Escaping ? 22f : 0f);
        return Mathf.Max(PortraitSize, content) + 2 * Pad;
    }

    private static void DrawCard(Rect rect, NetAI ai, Map map, AIManager manager, Color locationColor)
    {
        // Frame and stripe in the archetype's color; the section above says where the AI lives.
        Color color = NetrunTex.PersonalityColor(ai.Personality);
        NetrunUI.Card(rect, color);
        if (ai.HasRequest)
        {
            GUI.color = NetrunUI.Warn;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;
        }

        // Portrait framed in the archetype's color.
        var portrait = new Rect(rect.x + 4f + Pad, rect.y + Pad, PortraitSize, PortraitSize);
        NetrunUI.Box(portrait, NetrunUI.BgWindow, color, 2);
        GUI.DrawTexture(portrait.ContractedBy(6f), NetrunTex.Portrait(ai.Personality));
        NetrunUI.Tip(portrait, ai.LocationText);

        float x = rect.x + TextIndent;
        float width = rect.xMax - Pad - x;
        float y = rect.y + Pad - 2f;

        // Name and status badge.
        float nameWidth = NetrunUI.NamePlate(x, y, ai.Name, NetrunTex.PersonalityColor(ai.Personality), GameFont.Medium, width * 0.6f);
        (string status, Color statusColor) = Status(ai);
        float chipW = NetrunUI.Chip(x + nameWidth + 10f, y + 6f, status.ToUpper(), statusColor, ai.LocationText);
        // Rename: a small pen button after the status.
        var pen = new Rect(x + nameWidth + 10f + chipW + 8f, y + 3f, 26f, 26f);
        if (NetrunUI.Button(pen, "✎", NetrunUI.Muted, true, "NR_AIRenameTip".Translate()))
            Find.WindowStack.Add(new Dialog_RenameAI(ai));
        y += 37f;

        // Archetype tag (icon, personality color) and the request, if any.
        float tagX = x;
        tagX += NetrunUI.Chip(tagX, y + 1f, ai.Personality.Label().ToUpper(), ai.Personality.Color(), ai.Personality.Description()) + 6f;
        // Traits: seen ones with their plus and minus, the rest as "???" until trust opens them.
        for (int i = 0; i < ai.Traits.Count; i++)
        {
            if (i < ai.TraitsRevealed)
                tagX += NetrunUI.Chip(tagX, y + 1f, ai.Traits[i].Label().ToUpper(), NetrunUI.Purple, ai.Traits[i].Description()) + 6f;
            else
                tagX += NetrunUI.Chip(tagX, y + 1f, "???", NetrunUI.Muted, "NR_AITraitHiddenDesc".Translate(AITraitUtility.RevealTrust(i).ToString("0"))) + 6f;
        }
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

        y = DrawInfoRows(x, y, width, ai);

        if (ai.Escaping)
        {
            string escape = "NR_AIEscapeLine".Translate((ai.EscapeAtTick - Find.TickManager.TicksGame).ToStringTicksToPeriod());
            Widgets.Label(new Rect(x, y, width, 22f), escape.Colorize(NetrunUI.Bad));
            y += 22f;
        }

        DrawButtons(new Rect(x, y + 8f, width, rect.yMax - y - 8f - Pad), Buttons(ai, map, manager));
    }

    private const float InfoRow = 30f;

    private static float InfoRowsHeight(NetAI ai) => InfoRow * 2f + (ai.RequestHistory.Count > 0 ? 22f : 0f) + 4f;

    /// <summary>What the AI eats and how hungry it is; its request (deadline, reward, refusal) or when the next one comes; the last requests.</summary>
    private static float DrawInfoRows(float x, float y, float width, NetAI ai)
    {
        AISettingsDef s = NetrunDefOf.NR_AI;
        Text.Font = GameFont.Small;
        // Food.
        var food = AIBehavior.Food(ai.Personality);
        Texture2D foodIcon = food?.thing.uiIcon ?? NetrunTex.AIFeed;
        float daysLeft = ai.Hunger >= s.starvingHunger ? 0f : (s.starvingHunger - ai.Hunger) / Mathf.Max(0.1f, s.hungerPerDay);
        string foodLine = "NR_AICardFood".Translate(AIBehavior.FoodText(ai.Personality))
            + "  " + (ai.Hunger >= s.starvingHunger ? "NR_AICardStarving".Translate().Colorize(NetrunUI.Bad) : "NR_AICardFedFor".Translate(daysLeft.ToString("0.#")).Colorize(NetrunUI.Muted));
        InfoRowLine(new Rect(x, y, width, InfoRow), foodIcon, foodLine, "NR_AICardFoodTip".Translate(s.feedHungerRelief.ToString("0"), s.feedTrust.ToString("0"), s.starvingHunger.ToString("0")));
        y += InfoRow;
        // Request.
        string reqLine;
        if (ai.HasRequest)
        {
            string left = ai.RequestDeadlineTick > Find.TickManager.TicksGame ? (ai.RequestDeadlineTick - Find.TickManager.TicksGame).ToStringTicksToPeriod() : "0";
            reqLine = "NR_AICardRequest".Translate(AIManager.RequestText(ai), left).Colorize(NetrunUI.Warn) + "  " + "NR_AICardRequestStakes".Translate(s.requestTrust.ToString("0")).Colorize(NetrunUI.Muted);
        }
        else
        {
            reqLine = "NR_AICardNoRequestPlain".Translate().Colorize(NetrunUI.Muted);
        }
        var plate = new Rect(x, y + 1f, width, InfoRow - 2f);
        Widgets.DrawBoxSolid(plate, ai.HasRequest ? new Color(NetrunUI.Warn.r, NetrunUI.Warn.g, NetrunUI.Warn.b, 0.12f) : new Color(1f, 1f, 1f, 0.03f));
        if (ai.HasRequest)
            Widgets.DrawBoxSolid(new Rect(plate.x, plate.y, 3f, plate.height), NetrunUI.Warn);
        InfoRowLine(new Rect(x + 4f, y, width - 4f, InfoRow), NetrunTex.CyberIcon("Icon_AIRequest") ?? NetrunTex.AIUse, reqLine, "NR_AICardRequestTip".Translate(s.requestTrust.ToString("0")));
        y += InfoRow;
        // History.
        if (ai.RequestHistory.Count > 0)
        {
            string history = "NR_AICardHistory".Translate() + " " + string.Join(" · ", ai.RequestHistory.Select(h => h.StartsWith("✓") ? h.Colorize(NetrunUI.Ok) : h.Colorize(NetrunUI.Bad)));
            Text.Font = GameFont.Tiny;
            var hr = new Rect(x + 30f, y + 2f, width - 30f, 18f);
            Widgets.Label(hr, history.Truncate(hr.width));
            NetrunUI.Tip(hr, string.Join("\n", ai.RequestHistory));
            Text.Font = GameFont.Small;
            y += 22f;
        }
        Text.Font = GameFont.Small;
        return y + 4f;
    }

    private static void InfoRowLine(Rect row, Texture2D icon, string text, string tip)
    {
        if (icon != null)
            GUI.DrawTexture(new Rect(row.x, row.y + (row.height - 24f) / 2f, 24f, 24f), icon);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(row.x + 30f, row.y, row.width - 30f, row.height), text.Truncate(row.width - 30f));
        Text.Anchor = TextAnchor.UpperLeft;
        NetrunUI.Tip(row, text + "\n\n" + tip);
    }

    private static void DrawGauge(Rect rect, string label, float value, Color color, string tip)
    {
        float labelWidth = Text.CalcSize(label).x + 8f;
        Widgets.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), label);
        var bar = new Rect(rect.x + labelWidth, rect.y + (rect.height - 12f) / 2f, Mathf.Max(40f, rect.width - labelWidth - 40f), 12f);
        NetrunUI.Bar(bar, value / 100f, color);
        Widgets.Label(new Rect(bar.xMax + 8f, rect.y, 36f, rect.height), value.ToString("0").Colorize(color));
        NetrunUI.Tip(rect, tip);
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

        float power = AITraitUtility.ServicePower(ai, map);
        string serviceTip = AIBehavior.ServiceDescription(ai.Personality)
            + (Mathf.Abs(power - 1f) > 0.01f ? "\n" + "NR_AIServicePower".Translate(power.ToStringPercent()) : string.Empty);
        Add(AIBehavior.ServiceLabel(ai.Personality), NetrunUI.Accent, AIBehavior.CanUseService(ai), serviceTip,
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
