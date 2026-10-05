using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

public static class AITab
{
    private const float HeaderHeight = 64f;
    private const float RowGap = 8f;
    private const float Padding = 10f;
    private const float PortraitSize = 64f;
    private const float ButtonWidth = 200f;

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
        foreach (NetAI ai in ais)
        {
            float h = CardHeight(ai, width);
            DrawAI(new Rect(0f, y, width, h), ai);
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

    private static float CardHeight(NetAI ai, float width)
    {
        float textWidth = width - 2 * Padding - PortraitSize - 12f;
        float description = Text.CalcHeight(ai.Personality.Description(), textWidth);
        return Mathf.Max(PortraitSize, 24f + 24f + description + 24f) + 2 * Padding;
    }

    private static void DrawAI(Rect rect, NetAI ai)
    {
        Widgets.DrawOptionBackground(rect, selected: false);
        Rect inner = rect.ContractedBy(Padding);

        // Portrait placeholder until the personality art arrives.
        GUI.DrawTexture(new Rect(inner.x, inner.y, PortraitSize, PortraitSize), NetrunTex.TabIcon(3));

        float x = inner.x + PortraitSize + 12f;
        float width = inner.xMax - x;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(x, inner.y, width, 30f), ai.Name);
        Text.Font = GameFont.Small;

        Widgets.Label(new Rect(x, inner.y + 28f, width, 24f), "NR_AIPersonalityLine".Translate(ai.Personality.Label()));

        float descHeight = Text.CalcHeight(ai.Personality.Description(), width);
        GUI.color = NetrunTex.DimText;
        Widgets.Label(new Rect(x, inner.y + 52f, width, descHeight), ai.Personality.Description());
        Widgets.Label(new Rect(x, inner.y + 52f + descHeight, width, 24f), ai.LocationText);
        GUI.color = Color.white;
    }
}
