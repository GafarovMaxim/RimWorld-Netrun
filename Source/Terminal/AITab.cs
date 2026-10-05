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
    private const float RowGap = 8f;
    private const float Padding = 10f;
    private const float PortraitSize = 64f;
    private const float ButtonWidth = 190f;
    private const float ButtonHeight = 30f;
    private const float LineHeight = 24f;

    private static readonly Texture2D TrustTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.30f, 0.75f, 0.45f));
    private static readonly Texture2D HungerTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.85f, 0.45f, 0.25f));

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

        float width = listRect.width - 16f;
        float height = ais.Sum(a => CardHeight(a, width) + RowGap);
        var viewRect = new Rect(0f, 0f, width, height);

        Widgets.BeginScrollView(listRect, ref scroll, viewRect);
        float y = 0f;
        foreach (NetAI ai in ais.ToList())
        {
            float h = CardHeight(ai, width);
            DrawAI(new Rect(0f, y, width, h), ai, map, manager);
            y += h + RowGap;
        }
        Widgets.EndScrollView();
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
        var buttonRect = new Rect(rect.xMax - ButtonWidth, rect.y, ButtonWidth, 32f);
        if (report.Accepted)
        {
            if (Widgets.ButtonText(buttonRect, "NR_CommandAwakenAI".Translate()))
                AIAwakening.OpenMenu(map);
            TooltipHandler.TipRegion(buttonRect, "NR_CommandAwakenAIDesc".Translate());
        }
        else
        {
            Widgets.ButtonText(buttonRect, "NR_CommandAwakenAI".Translate(), active: false);
            GUI.color = NetrunTex.DimText;
            Widgets.Label(new Rect(rect.x, rect.y + 28f, textWidth, 32f), report.Reason);
            GUI.color = Color.white;
        }
    }

    private static float TextWidth(float width) => width - 2 * Padding - PortraitSize - 12f - ButtonWidth - 12f;

    private static float CardHeight(NetAI ai, float width)
    {
        float description = Text.CalcHeight(ai.Personality.Description(), TextWidth(width));
        float lines = 30f + LineHeight + description + 4 * LineHeight + (ai.HasRequest ? LineHeight * 2 : 0f)
            + (ai.Escaping ? LineHeight * 2 : 0f);
        float buttons = 7 * (ButtonHeight + 6f);
        return Mathf.Max(lines, buttons) + 2 * Padding;
    }

    private static void DrawAI(Rect rect, NetAI ai, Map map, AIManager manager)
    {
        Widgets.DrawOptionBackground(rect, selected: false);
        Rect inner = rect.ContractedBy(Padding);

        // Portrait placeholder until the personality art arrives.
        GUI.DrawTexture(new Rect(inner.x, inner.y, PortraitSize, PortraitSize), NetrunTex.Portrait(ai.Personality));

        float x = inner.x + PortraitSize + 12f;
        float width = TextWidth(rect.width);
        float y = inner.y;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(x, y, width, 30f), ai.Name);
        Text.Font = GameFont.Small;
        y += 30f;

        Widgets.Label(new Rect(x, y, width, LineHeight), "NR_AIPersonalityLine".Translate(ai.Personality.Label()));
        y += LineHeight;

        float descHeight = Text.CalcHeight(ai.Personality.Description(), width);
        GUI.color = NetrunTex.DimText;
        Widgets.Label(new Rect(x, y, width, descHeight), ai.Personality.Description());
        y += descHeight;
        Widgets.Label(new Rect(x, y, width, LineHeight), ai.LocationText);
        GUI.color = Color.white;
        y += LineHeight;

        y = DrawBar(x, y, width, "NR_AITrust".Translate(ai.Trust.ToString("0")), ai.Trust, TrustTex, "NR_AITrustTip".Translate());
        y = DrawBar(x, y, width, "NR_AIHunger".Translate(ai.Hunger.ToString("0")), ai.Hunger, HungerTex,
            "NR_AIHungerTip".Translate(AIBehavior.FoodText(ai.Personality)));

        GUI.color = NetrunTex.DimText;
        Widgets.Label(new Rect(x, y, width, LineHeight), "NR_AIFoodLine".Translate(AIBehavior.FoodText(ai.Personality)));
        GUI.color = Color.white;
        y += LineHeight;

        if (ai.Escaping)
        {
            string escape = "NR_AIEscapeLine".Translate((ai.EscapeAtTick - Find.TickManager.TicksGame).ToStringTicksToPeriod());
            float eh = Text.CalcHeight(escape, width);
            GUI.color = ColorLibrary.RedReadable;
            Widgets.Label(new Rect(x, y, width, eh), escape);
            GUI.color = Color.white;
            y += eh;
        }

        if (ai.HasRequest)
        {
            string request = "NR_AIRequestLine".Translate(AIManager.RequestText(ai), ai.RequestDeadlineTick > Find.TickManager.TicksGame
                ? (ai.RequestDeadlineTick - Find.TickManager.TicksGame).ToStringTicksToPeriod() : "0");
            float h = Text.CalcHeight(request, width);
            GUI.color = ColorLibrary.Gold;
            Widgets.Label(new Rect(x, y, width, h), request);
            GUI.color = Color.white;
        }

        DrawButtons(new Rect(inner.xMax - ButtonWidth, inner.y, ButtonWidth, inner.height), ai, map, manager);
    }

    private static float DrawBar(float x, float y, float width, string label, float value, Texture2D tex, string tip)
    {
        var labelRect = new Rect(x, y, 150f, LineHeight);
        Widgets.Label(labelRect, label);
        var barRect = new Rect(x + 154f, y + 5f, width - 154f, 14f);
        Widgets.FillableBar(barRect, value / 100f, tex, BaseContent.BlackTex, doBorder: true);
        TooltipHandler.TipRegion(new Rect(x, y, width, LineHeight), tip);
        return y + LineHeight;
    }

    private static void DrawButtons(Rect rect, NetAI ai, Map map, AIManager manager)
    {
        float y = rect.y;

        void Add(Texture2D icon, string label, string tip, AcceptanceReport report, System.Action action)
        {
            Button(new Rect(rect.x, y, rect.width, ButtonHeight), icon, label, tip, report, action);
            y += ButtonHeight + 6f;
        }

        Add(NetrunTex.AIUse, AIBehavior.ServiceLabel(ai.Personality), AIBehavior.ServiceDescription(ai.Personality),
            AIBehavior.CanUseService(ai), () => AIBehavior.UseService(ai, map));

        Add(NetrunTex.AIFeed, "NR_AIFeed".Translate(), "NR_AIFeedTip".Translate(AIBehavior.FoodText(ai.Personality)),
            AIBehavior.CanFeed(ai, map), () => AIBehavior.Feed(ai, map));

        if (ai.Request == AIRequestKind.Deliver)
            Add(NetrunTex.AIFeed, "NR_AIFulfil".Translate(), "NR_AIFulfilTip".Translate(), manager.CanFulfil(ai, map), () => manager.Fulfil(ai, map));

        if (ai.Housed)
            Add(NetrunTex.AIIsolate, ai.Isolated ? "NR_AIConnect".Translate() : "NR_AIIsolate".Translate(), "NR_AIIsolateTip".Translate(), true,
                () => ai.Isolated = !ai.Isolated);

        if (ai.InMainServer)
        {
            Add(NetrunTex.AIEmbody, "NR_AIUpload".Translate(), "NR_AIUploadTip".Translate(),
                AIHosting.CanUpload(ai, map), () => AIHosting.OpenUploadMenu(ai, map));
            Add(NetrunTex.AIEmbody, "NR_AIToCoprocessor".Translate(), "NR_AIToCoprocessorTip".Translate(),
                AIHosting.CanMoveToCoprocessor(ai, map), () => AIHosting.MoveToCoprocessor(ai, map));
        }
        else if (ai.InHost || ai.InCoprocessor)
        {
            Add(NetrunTex.AIEmbody, "NR_AIDownload".Translate(), "NR_AIDownloadTip".Translate(),
                AIHosting.CanDownload(ai, map), () => AIHosting.Download(ai, map));
        }

        if (ai.Housed)
            Add(NetrunTex.AIErase, "NR_AIErase".Translate(), "NR_AIEraseTip".Translate(NetrunDefOf.NR_AI.eraseAdvancedCyberdata), true,
                () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "NR_AIEraseConfirm".Translate(ai.Name), () => manager.Erase(ai), destructive: true)));
    }

    /// <summary>
    /// A button with an icon that explains itself: the tooltip always describes it, a disabled one says why.
    /// </summary>
    private static void Button(Rect rect, Texture2D icon, string label, string tip, AcceptanceReport report, System.Action action)
    {
        bool clicked = Widgets.ButtonText(rect, "      " + label, active: report.Accepted);
        GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + (rect.height - 22f) / 2f, 22f, 22f), icon);

        if (report.Accepted)
        {
            TooltipHandler.TipRegion(rect, tip);
            if (clicked)
                action();
            return;
        }

        TooltipHandler.TipRegion(rect, tip + "\n\n" + report.Reason.Colorize(ColorLibrary.RedReadable));
    }
}
