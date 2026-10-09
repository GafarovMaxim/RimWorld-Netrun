using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Netrun;

public class GuideStep
{
    [MustTranslate]
    public string text;
    /// <summary>Optional "Try: ..." line.</summary>
    [MustTranslate]
    public string hint;
    /// <summary>Optional: another area of the screen to pulse during this step.</summary>
    public string highlight;
}

/// <summary>
/// One area of a terminal screen for the guide mode (docs/HELP.md, mockup 12-guide-mode): matched by
/// <see cref="area"/> to the rect the screen registers in <see cref="GuideRegistry"/>; label is the title.
/// </summary>
public class GuideAreaDef : Def
{
    public string area;
    /// <summary>Core, Heat, Club, Board, Archive, Server, Studio, Gateway, Dive, Onboarding, or Any (on every screen).</summary>
    public string screen;
    public int order;
    public List<GuideStep> steps = [];
    public HelpPageDef helpPage;

    public static GuideAreaDef ForArea(string area) => DefDatabase<GuideAreaDef>.AllDefs.FirstOrDefault(d => d.area == area);

    /// <summary>The areas of a screen in order (the screen's own, then "Any").</summary>
    public static List<GuideAreaDef> ForScreen(string screen)
    {
        // Built once per screen: the defs never change while the game runs.
        if (!ByScreen.TryGetValue(screen, out List<GuideAreaDef> list))
            ByScreen[screen] = list = DefDatabase<GuideAreaDef>.AllDefs.Where(d => d.screen == screen).OrderBy(d => d.order)
                .Concat(DefDatabase<GuideAreaDef>.AllDefs.Where(d => d.screen == "Any").OrderBy(d => d.order)).ToList();
        return list;
    }

    private static readonly Dictionary<string, List<GuideAreaDef>> ByScreen = [];
}
