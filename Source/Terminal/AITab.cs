using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

[StaticConstructorOnStartup]
public static class AITab
{
    private const float HeaderHeight = 64f;
    private const float RowGap = 10f;
    private const float Padding = 12f;
    private const float StripeWidth = 5f;
    private const float PortraitSize = 96f;
    private const float StatusHeight = 22f;
    private const float ButtonWidth = 230f;
    private const float ButtonHeight = 40f;
    private const float ButtonGap = 6f;
    private const float ButtonIconSize = 34f;
    private const float LineHeight = 24f;
    private const float BarHeight = 16f;
    private const float SectionHeight = 36f;

    private static readonly Color TrustColor = new(0.30f, 0.80f, 0.45f);
    private static readonly Color HungerColor = new(0.95f, 0.50f, 0.25f);
    private static readonly Texture2D TrustTex = SolidColorMaterials.NewSolidColorTexture(TrustColor);
    private static readonly Texture2D HungerTex = SolidColorMaterials.NewSolidColorTexture(HungerColor);
    private static readonly Texture2D BarBackTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.08f, 0.08f, 0.10f));

    private static readonly Color CardBack = new(0.12f, 0.13f, 0.16f);
    private static readonly Color IconBack = new(0.05f, 0.06f, 0.08f, 0.85f);
    private static readonly Color CoprocessorColor = new(0.70f, 0.50f, 1.00f);
    private static readonly Color HostColor = new(0.45f, 1.00f, 0.55f);
    private static readonly Color DangerColor = new(1.00f, 0.40f, 0.40f);

    public static void Draw(Rect rect, Map map, ref Vector2 scroll)
    {
        AIManager manager = AIManager.Get();
        if (manager == null || map == null)
            return;

        DrawHeader(new Rect(rect.x, rect.y, rect.width, HeaderHeight), map);

        var listRect = new Rect(rect.x, rect.y + HeaderHeight, rect.width, rect.height - HeaderHeight);
        IReadOnlyList<NetAI> ais = manager.AIs;
        if (ais.Count == 0)
        {
            GUI.color = NetrunTex.DimText;
            Widgets.Label(listRect, "NR_AINone".Translate());
            GUI.color = Color.white;
            return;
        }

        // Separate lists by where each AI lives: the server, a coprocessor or a host.
        var groups = new List<(string label, Color color, List<NetAI> list)>
        {
            ("NR_AISectionServer".Translate(), NetrunTex.Accent, ais.Where(a => a.InMainServer).ToList()),
            ("NR_AISectionCoprocessor".Translate(), CoprocessorColor, ais.Where(a => a.InCoprocessor).ToList()),
            ("NR_AISectionHost".Translate(), HostColor, ais.Where(a => a.InHost).ToList()),
            ("NR_AISectionOther".Translate(), NetrunTex.DimText, ais.Where(a => !a.InMainServer && !a.InCoprocessor && !a.InHost).ToList()),
        };
        groups.RemoveAll(g => g.list.Count == 0);

        float width = listRect.width - 16f;
        float height = groups.Sum(g => SectionHeight + g.list.Sum(a => CardHeight(a, width) + RowGap));
        var viewRect = new Rect(0f, 0f, width, height);

        Widgets.BeginScrollView(listRect, ref scroll, viewRect);
        float y = 0f;
        foreach ((string label, Color color, List<NetAI> list) in groups)
        {
            DrawSectionHeader(new Rect(0f, y, width, SectionHeight), label, list.Count, color);
            y += SectionHeight;

            foreach (NetAI ai in list)
            {
                float h = CardHeight(ai, width);
                DrawAI(new Rect(0f, y, width, h), ai, map, manager, color);
                y += h + RowGap;
            }
        }
        Widgets.EndScrollView();
    }

    private static void DrawSectionHeader(Rect rect, string label, int count, Color color)
    {
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(rect.x, rect.y, rect.width, rect.height - 6f), $"{label} ({count})".Colorize(color));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        GUI.color = color;
        Widgets.DrawLineHorizontal(rect.x, rect.yMax - 6f, rect.width);
        GUI.color = Color.white;
    }

    private static void DrawHeader(Rect rect, Map map)
    {
        List<Building> servers = map.listerBuildings.AllBuildingsColonistOfDef(NetrunDefOf.NR_AIServer).ToList();
        int slots = servers.Sum(s => s.GetComp<CompAIServer>()?.Slots ?? 0);
        int used = servers.Sum(s => s.GetComp<CompAIServer>()?.Used ?? 0);
        int fragments = AIAwakening.Fragments(map).Count();

        float textWidth = rect.width - ButtonWidth - 12f;
        Widgets.Label(new Rect(rect.x, rect.y, textWidth, 24f), "NR_AIHeader".Translate(servers.Count, used, slots, fragments));

        AcceptanceReport report = AIAwakening.CanAwaken(map);
        Button(new Rect(rect.xMax - ButtonWidth, rect.y, ButtonWidth, ButtonHeight), NetrunTex.AIEmbody,
            "NR_CommandAwakenAI".Translate(), "NR_CommandAwakenAIDesc".Translate(), report, () => AIAwakening.OpenMenu(map), NetrunTex.Accent);

        if (!report.Accepted)
        {
            GUI.color = NetrunTex.DimText;
            Widgets.Label(new Rect(rect.x, rect.y + 28f, textWidth, 32f), report.Reason);
            GUI.color = Color.white;
        }
    }

    private static float TextWidth(float width) => width - StripeWidth - 2 * Padding - PortraitSize - 14f - ButtonWidth - 14f;

    private static int ButtonCount(NetAI ai)
    {
        int count = 2;
        if (ai.Request == AIRequestKind.Deliver)
            count++;
        if (ai.Housed)
            count += 2;
        if (ai.InMainServer)
            count += 2;
        else if (ai.InHost || ai.InCoprocessor)
            count++;
        return count;
    }

    private static float CardHeight(NetAI ai, float width)
    {
        float textWidth = TextWidth(width);
        float lines = 34f + LineHeight + Text.CalcHeight(ai.Personality.Description(), textWidth) + 6f
            + LineHeight + 2 * (LineHeight + 4f) + LineHeight
            + (ai.Escaping ? Text.CalcHeight(EscapeText(ai), textWidth) + 8f : 0f)
            + (ai.HasRequest ? Text.CalcHeight(RequestText(ai), textWidth - 12f) + 14f : 0f);
        float portrait = PortraitSize + 6f + StatusHeight;
        float buttons = ButtonCount(ai) * (ButtonHeight + ButtonGap) - ButtonGap;
        return Mathf.Max(lines, portrait, buttons) + 2 * Padding;
    }

    private static string EscapeText(NetAI ai)
        => "NR_AIEscapeLine".Translate((ai.EscapeAtTick - Find.TickManager.TicksGame).ToStringTicksToPeriod());

    private static string RequestText(NetAI ai)
        => "NR_AIRequestLine".Translate(AIManager.RequestText(ai), ai.RequestDeadlineTick > Find.TickManager.TicksGame
            ? (ai.RequestDeadlineTick - Find.TickManager.TicksGame).ToStringTicksToPeriod() : "0");

    private static (string label, Color color) Status(NetAI ai)
    {
        if (ai.Isolated)
            return ("NR_AIStatusIsolated".Translate(), ColorLibrary.Gold);
        if (!ai.Powered)
            return ("NR_AIStatusAsleep".Translate(), NetrunTex.DimText);
        if (ai.Escaping)
            return ("NR_AIStatusEscaping".Translate(), DangerColor);
        return ("NR_AIStatusActive".Translate(), ColorLibrary.Green);
    }

    private static void DrawAI(Rect rect, NetAI ai, Map map, AIManager manager, Color color)
    {
        // Card: dark body, colored stripe on the left, thin colored frame.
        Widgets.DrawBoxSolid(rect, CardBack);
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, StripeWidth, rect.height), color);
        GUI.color = new Color(color.r, color.g, color.b, 0.45f);
        Widgets.DrawBox(rect);
        GUI.color = Color.white;
        Widgets.DrawHighlightIfMouseover(rect);

        Rect inner = new Rect(rect.x + StripeWidth, rect.y, rect.width - StripeWidth, rect.height).ContractedBy(Padding);

        // Portrait in a frame, status badge under it.
        var portraitRect = new Rect(inner.x, inner.y, PortraitSize, PortraitSize);
        Widgets.DrawBoxSolid(portraitRect, IconBack);
        GUI.DrawTexture(portraitRect.ContractedBy(4f), NetrunTex.Portrait(ai.Personality));
        GUI.color = color;
        Widgets.DrawBox(portraitRect, 2);
        GUI.color = Color.white;

        (string statusLabel, Color statusColor) = Status(ai);
        var statusRect = new Rect(inner.x, portraitRect.yMax + 6f, PortraitSize, StatusHeight);
        Widgets.DrawBoxSolid(statusRect, new Color(statusColor.r, statusColor.g, statusColor.b, 0.25f));
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(statusRect, statusLabel.Colorize(statusColor));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        float x = portraitRect.xMax + 14f;
        float width = TextWidth(rect.width);
        float y = inner.y;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(x, y, width, 34f), ai.Name.Colorize(NetrunTex.Accent));
        Text.Font = GameFont.Small;
        y += 34f;

        Widgets.Label(new Rect(x, y, width, LineHeight), "NR_AIPersonalityLine".Translate(ai.Personality.Label().Colorize(ColorLibrary.Gold)));
        y += LineHeight;

        float descHeight = Text.CalcHeight(ai.Personality.Description(), width);
        GUI.color = NetrunTex.DimText;
        Widgets.Label(new Rect(x, y, width, descHeight), ai.Personality.Description());
        y += descHeight + 6f;
        Widgets.Label(new Rect(x, y, width, LineHeight), ai.LocationText.Colorize(color));
        GUI.color = Color.white;
        y += LineHeight;

        y = DrawBar(x, y, width, "NR_AITrust".Translate(ai.Trust.ToString("0").Colorize(NetrunTex.Gauge(ai.Trust))),
            ai.Trust, TrustTex, "NR_AITrustTip".Translate());
        y = DrawBar(x, y, width, "NR_AIHunger".Translate(ai.Hunger.ToString("0").Colorize(NetrunTex.Gauge(ai.Hunger, highIsGood: false))),
            ai.Hunger, HungerTex, "NR_AIHungerTip".Translate(AIBehavior.FoodText(ai.Personality)));

        GUI.color = NetrunTex.DimText;
        Widgets.Label(new Rect(x, y, width, LineHeight), "NR_AIFoodLine".Translate(AIBehavior.FoodText(ai.Personality)));
        GUI.color = Color.white;
        y += LineHeight;

        if (ai.Escaping)
        {
            string escape = EscapeText(ai);
            float eh = Text.CalcHeight(escape, width);
            y += 4f;
            Widgets.Label(new Rect(x, y, width, eh), escape.Colorize(DangerColor));
            y += eh + 4f;
        }

        if (ai.HasRequest)
        {
            // The request stands out: a gold box the player cannot miss.
            string request = RequestText(ai);
            float h = Text.CalcHeight(request, width - 12f);
            var box = new Rect(x, y + 4f, width, h + 8f);
            Widgets.DrawBoxSolid(box, new Color(0.9f, 0.75f, 0.2f, 0.15f));
            GUI.color = ColorLibrary.Gold;
            Widgets.DrawBox(box);
            GUI.color = Color.white;
            Widgets.Label(new Rect(box.x + 6f, box.y + 4f, box.width - 12f, h), request.Colorize(ColorLibrary.Gold));
        }

        DrawButtons(new Rect(inner.xMax - ButtonWidth, inner.y, ButtonWidth, inner.height), ai, map, manager);
    }

    private static float DrawBar(float x, float y, float width, string label, float value, Texture2D tex, string tip)
    {
        const float labelWidth = 150f;
        Widgets.Label(new Rect(x, y, labelWidth, LineHeight), label);
        var barRect = new Rect(x + labelWidth + 4f, y + (LineHeight - BarHeight) / 2f, width - labelWidth - 4f, BarHeight);
        Widgets.FillableBar(barRect, value / 100f, tex, BarBackTex, doBorder: true);
        TooltipHandler.TipRegion(new Rect(x, y, width, LineHeight), tip);
        return y + LineHeight + 4f;
    }

    private static void DrawButtons(Rect rect, NetAI ai, Map map, AIManager manager)
    {
        float y = rect.y;

        void Add(Texture2D icon, string label, string tip, AcceptanceReport report, System.Action action, Color? color = null)
        {
            Button(new Rect(rect.x, y, rect.width, ButtonHeight), icon, label, tip, report, action, color);
            y += ButtonHeight + ButtonGap;
        }

        Add(NetrunTex.AIUse, AIBehavior.ServiceLabel(ai.Personality), AIBehavior.ServiceDescription(ai.Personality),
            AIBehavior.CanUseService(ai), () => AIBehavior.UseService(ai, map), NetrunTex.Accent);

        Add(NetrunTex.AIFeed, "NR_AIFeed".Translate(), "NR_AIFeedTip".Translate(AIBehavior.FoodText(ai.Personality)),
            AIBehavior.CanFeed(ai, map), () => AIBehavior.Feed(ai, map), HungerColor);

        if (ai.Request == AIRequestKind.Deliver)
            Add(NetrunTex.AIFeed, "NR_AIFulfil".Translate(), "NR_AIFulfilTip".Translate(), manager.CanFulfil(ai, map),
                () => manager.Fulfil(ai, map), ColorLibrary.Gold);

        if (ai.Housed)
            Add(NetrunTex.AIIsolate, ai.Isolated ? "NR_AIConnect".Translate() : "NR_AIIsolate".Translate(), "NR_AIIsolateTip".Translate(), true,
                () => ai.Isolated = !ai.Isolated);

        if (ai.InMainServer)
        {
            Add(NetrunTex.AIEmbody, "NR_AIUpload".Translate(), "NR_AIUploadTip".Translate(),
                AIHosting.CanUpload(ai, map), () => AIHosting.OpenUploadMenu(ai, map), HostColor);
            Add(NetrunTex.AIToCoprocessor, "NR_AIToCoprocessor".Translate(), "NR_AIToCoprocessorTip".Translate(),
                AIHosting.CanMoveToCoprocessor(ai, map), () => AIHosting.MoveToCoprocessor(ai, map), CoprocessorColor);
        }
        else if (ai.InHost || ai.InCoprocessor)
        {
            Add(NetrunTex.AIReturnServer, "NR_AIDownload".Translate(), "NR_AIDownloadTip".Translate(),
                AIHosting.CanDownload(ai, map), () => AIHosting.Download(ai, map), NetrunTex.Accent);
        }

        if (ai.Housed)
            Add(NetrunTex.AIErase, "NR_AIErase".Translate(), "NR_AIEraseTip".Translate(NetrunDefOf.NR_AI.eraseAdvancedCyberdata), true,
                () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "NR_AIEraseConfirm".Translate(ai.Name), () => manager.Erase(ai), destructive: true)), DangerColor);
    }

    /// <summary>
    /// A button with a large icon on a dark tile and a colored label. The tooltip always describes it,
    /// a disabled one also says why; disabled buttons are faded.
    /// </summary>
    private static void Button(Rect rect, Texture2D icon, string label, string tip, AcceptanceReport report, System.Action action, Color? color = null)
    {
        bool active = report.Accepted;
        bool clicked = Widgets.ButtonText(rect, string.Empty, active: active);

        var iconTile = new Rect(rect.x + 3f, rect.y + (rect.height - ButtonIconSize) / 2f, ButtonIconSize, ButtonIconSize);
        Widgets.DrawBoxSolid(iconTile, IconBack);
        GUI.color = active ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        GUI.DrawTexture(iconTile.ContractedBy(2f), icon);
        GUI.color = Color.white;

        Color textColor = active ? color ?? Color.white : NetrunTex.DimText * new Color(1f, 1f, 1f, 0.7f);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(iconTile.xMax + 8f, rect.y, rect.width - ButtonIconSize - 14f, rect.height), label.Colorize(textColor));
        Text.Anchor = TextAnchor.UpperLeft;

        if (active)
        {
            TooltipHandler.TipRegion(rect, tip);
            if (clicked)
                action();
            return;
        }

        TooltipHandler.TipRegion(rect, tip + "\n\n" + report.Reason.Colorize(ColorLibrary.RedReadable));
    }
}
