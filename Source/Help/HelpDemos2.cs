using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// More live Cyberpedia blocks, like the cyberspace article: [[aigallery]] (AI archetypes), [[fixerpool]] and
/// [[contractflow]] (fixers and contracts), [[braindance]] (recording to chip), [[ice]] and [[loadout]] (ICE, programs,
/// daemons). Text and numbers come from the game's own defs and settings.
/// </summary>
public partial class HelpBrowser
{
    // ---- [[aigallery]] / [[aigallery:Ghost]] ----

    private AIPersonality _demoAI = AIPersonality.DataHunter;
    private string _aiPinnedFor;
    private float _demoTrust = 50f;

    private static bool AIKnown(AIPersonality p) => HelpProgress.Get()?.Has("ai:" + p) == true;

    private float AIGalleryDemo(float y, float w, string preset)
    {
        // An archetype page opens the gallery on its own AI (once per visit).
        if (preset != null && _aiPinnedFor != preset && System.Enum.TryParse(preset, out AIPersonality p0))
        {
            _demoAI = p0;
            _aiPinnedFor = preset;
        }
        var all = (AIPersonality[])System.Enum.GetValues(typeof(AIPersonality));
        const float tile = 54f;
        int perRow = Mathf.Max(4, Mathf.FloorToInt((w - 20f + 6f) / (tile + 6f)));
        int rows = Mathf.CeilToInt(all.Length / (float)perRow);
        float cardH = 250f;
        Rect inner = DemoBox(y, w, rows * (tile + 22f) + cardH + 30f, NetrunUI.Purple);
        for (int i = 0; i < all.Length; i++)
        {
            AIPersonality p = all[i];
            bool known = AIKnown(p);
            var r = new Rect(inner.x + i % perRow * (tile + 6f), inner.y + i / perRow * (tile + 22f), tile, tile);
            bool sel = p == _demoAI;
            Color c = known ? p.Color() : NetrunUI.Muted;
            if (sel)
                NetrunUI.Glow(r, c, 0.8f, 8f);
            NetrunUI.Box(r, NetrunUI.BgWindow, new Color(c.r, c.g, c.b, sel ? 1f : 0.5f), sel ? 2 : 1);
            if (known)
                GUI.DrawTexture(r.ContractedBy(4f), NetrunTex.Portrait(p));
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Medium;
                Widgets.Label(r, "???".Colorize(NetrunUI.Muted));
                Text.Font = GameFont.Small;
            }
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(r.x - 6f, r.yMax + 2f, r.width + 12f, 18f), (known ? p.Label() : "???").Truncate(r.width + 12f).Colorize(sel ? c : NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (Widgets.ButtonInvisible(r))
            {
                _demoAI = p;
                NetrunSound.Click();
            }
        }

        var card = new Rect(inner.x, inner.y + rows * (tile + 22f) + 6f, inner.width, cardH);
        Widgets.DrawBoxSolid(card, NetrunUI.BgWindow);
        Rect cr = card.ContractedBy(10f);
        if (!AIKnown(_demoAI))
        {
            Widgets.Label(cr, "NR_HelpAIUnknown".Translate().Colorize(NetrunUI.Muted));
            return inner.yMax + 20f;
        }
        Color pc = _demoAI.Color();
        Widgets.DrawBoxSolid(new Rect(card.x, card.y, 3f, card.height), pc);
        GUI.DrawTexture(new Rect(cr.x, cr.y, 64f, 64f), NetrunTex.Portrait(_demoAI));
        float tx = cr.x + 74f;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(tx, cr.y - 2f, cr.width - 74f, 30f), _demoAI.Label().Colorize(pc));
        Text.Font = GameFont.Tiny;
        string desc = _demoAI.Description();
        Widgets.Label(new Rect(tx, cr.y + 28f, cr.width - 74f, 40f), desc.Truncate((cr.width - 74f) * 2f).Colorize(NetrunUI.TextColor));
        float yy = cr.y + 72f;
        void Row(string label, string value)
        {
            float h = Mathf.Max(18f, Text.CalcHeight(value, cr.width - 110f));
            Widgets.Label(new Rect(cr.x, yy, 104f, h), label.Colorize(NetrunUI.Muted));
            Widgets.Label(new Rect(cr.x + 108f, yy, cr.width - 108f, h), value);
            yy += h + 3f;
        }
        Row("NR_HelpAIService".Translate(), AIBehavior.ServiceLabel(_demoAI) + " — " + ("NR_AIServiceDesc_" + _demoAI).Translate());
        Row("NR_HelpAIFood".Translate(), AIBehavior.FoodText(_demoAI));

        // Trust: what opens on the way from 0 to 100.
        AISettingsDef s = NetrunDefOf.NR_AI;
        yy += 4f;
        Widgets.Label(new Rect(cr.x, yy, cr.width, 18f), "NR_HelpAITrust".Translate(_demoTrust.ToString("0")).Colorize(NetrunUI.Muted));
        _demoTrust = Widgets.HorizontalSlider(new Rect(cr.x, yy + 18f, cr.width, 16f), _demoTrust, 0f, 100f, roundTo: 1f);
        yy += 38f;
        var steps = new List<(float at, string text, bool good)>
        {
            (s.betrayalTrust, "NR_HelpAITrustBetray".Translate(s.betrayalTrust.ToString("0")), false),
            (s.escapeTrust, "NR_HelpAITrustEscape".Translate(s.escapeTrust.ToString("0"), s.escapeHunger.ToString("0")), false),
            (s.minTrustForService, "NR_HelpAITrustService".Translate(s.minTrustForService.ToString("0")), true),
            (s.traitRevealTrustFirst, "NR_HelpAITrustTrait1".Translate(s.traitRevealTrustFirst.ToString("0")), true),
            (s.eventGoodTrust, "NR_HelpAITrustEvents".Translate(s.eventGoodTrust.ToString("0")), true),
            (s.traitRevealTrustSecond, "NR_HelpAITrustTrait2".Translate(s.traitRevealTrustSecond.ToString("0")), true),
        };
        foreach ((float at, string text, bool good) in steps.OrderBy(t => t.at))
        {
            // Bad marks are in effect below their value, good ones from their value up.
            bool on = good ? _demoTrust >= at : _demoTrust < at;
            Color col = on ? good ? NetrunUI.Ok : NetrunUI.Bad : NetrunUI.Muted;
            Widgets.Label(new Rect(cr.x, yy, cr.width, 18f), ((on ? "● " : "○ ") + text).Colorize(col));
            yy += 17f;
        }
        Text.Font = GameFont.Small;
        return inner.yMax + 20f;
    }

    // ---- [[fixerpool]] ----

    private static readonly (FixerSpecialty spec, FixerTrait trait, bool isNew)[] DemoFixers =
    [
        (FixerSpecialty.Broker, FixerTrait.Gambler, false),
        (FixerSpecialty.Cleaner, FixerTrait.Veteran, false),
        (FixerSpecialty.Fence, FixerTrait.Collector, true),
        (FixerSpecialty.Broker, FixerTrait.Rat, true),
        (FixerSpecialty.Imperial, FixerTrait.Loyal, false),
    ];

    private int _demoFixer = 1;
    private float _demoRep = 30f;

    private float FixerPoolDemo(float y, float w)
    {
        FixerSettingsDef s = NetrunDefOf.NR_Fixers;
        float tileW = Mathf.Min(96f, (w - 20f - 4 * 6f) / 5f);
        Rect inner = DemoBox(y, w, tileW + 34f + 210f, NetrunUI.Warn);
        for (int i = 0; i < DemoFixers.Length; i++)
        {
            var f = DemoFixers[i];
            var r = new Rect(inner.x + i * (tileW + 6f), inner.y, tileW, tileW);
            Color c = FixersTab.SpecialtyColor(f.spec);
            bool sel = i == _demoFixer;
            if (sel)
                NetrunUI.Glow(r, c, 0.8f, 8f);
            NetrunUI.Box(r, NetrunUI.BgWindow, new Color(c.r, c.g, c.b, sel ? 1f : 0.5f), sel ? 2 : 1);
            GUI.DrawTexture(r.ContractedBy(4f), NetrunTex.Portrait(f.spec));
            if (f.isNew)
                NetrunUI.Chip(r.x + 3f, r.y + 3f, "NR_FixerNewTag".Translate(), NetrunUI.Accent);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(r.x - 4f, r.yMax + 2f, r.width + 8f, 18f), ((string)("NR_FixerSpecialty_" + f.spec).Translate()).Colorize(sel ? c : NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (Widgets.ButtonInvisible(r))
            {
                _demoFixer = i;
                NetrunSound.Click();
            }
        }

        var pick = DemoFixers[_demoFixer];
        // A real fixer object, so prices and scrub rates come from the same code the game uses.
        var fixer = new Fixer { Specialty = pick.spec, Trait = pick.trait, Reputation = _demoRep };
        var card = new Rect(inner.x, inner.y + tileW + 28f, inner.width, 200f);
        Widgets.DrawBoxSolid(card, NetrunUI.BgWindow);
        Rect cr = card.ContractedBy(10f);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(cr.x, cr.y, cr.width, 18f), "NR_HelpFixerRep".Translate(_demoRep.ToString("0")).Colorize(NetrunUI.Muted));
        _demoRep = Widgets.HorizontalSlider(new Rect(cr.x, cr.y + 18f, cr.width, 16f), _demoRep, 0f, 100f, roundTo: 1f);
        // Marks for the trait reveal and pinning on the slider.
        foreach ((float at, Color mc) in new[] { (s.traitRevealReputation, NetrunUI.Purple), (s.pinnedReputation, NetrunUI.Warn) })
            Widgets.DrawBoxSolid(new Rect(cr.x + cr.width * at / 100f - 1f, cr.y + 14f, 2f, 24f), mc);
        float yy = cr.y + 44f;
        void Line(string text, Color col)
        {
            float h = Mathf.Max(18f, Text.CalcHeight(text, cr.width));
            Widgets.Label(new Rect(cr.x, yy, cr.width, h), text.Colorize(col));
            yy += h + 2f;
        }
        bool traitSeen = _demoRep >= s.traitRevealReputation;
        Line(traitSeen
            ? "NR_HelpFixerTrait".Translate(("NR_FixerTrait_" + pick.trait).Translate(), ("NR_FixerTraitDesc_" + pick.trait).Translate())
            : "NR_HelpFixerTraitHidden".Translate(Mathf.CeilToInt(s.traitRevealReputation - _demoRep)), traitSeen ? NetrunUI.TextColor : NetrunUI.Muted);
        bool pinned = _demoRep >= s.pinnedReputation;
        Line(pinned ? "NR_HelpFixerPinned".Translate(s.maxPinned) : "NR_HelpFixerToPin".Translate(Mathf.CeilToInt(s.pinnedReputation - _demoRep)), pinned ? NetrunUI.Warn : NetrunUI.Muted);
        Line("NR_HelpFixerPrice".Translate(fixer.PriceFactor.ToStringPercent(), fixer.HeatPerUnitAt(0f, false).ToString("0.#"), fixer.EffectiveCallCost), NetrunUI.Ok);
        if (pick.spec == FixerSpecialty.Fence)
            Line(pinned || pick.trait == FixerTrait.Collector ? "NR_HelpFixerRareOn".Translate() : "NR_HelpFixerRareOff".Translate(), NetrunUI.Accent);
        Text.Font = GameFont.Small;
        return inner.yMax + 20f;
    }

    // ---- [[contractflow]] ----

    private int _demoStage = -1;
    private ContractType _demoContract = ContractType.SystemTap;

    private float ContractFlowDemo(float y, float w)
    {
        FixerSettingsDef s = NetrunDefOf.NR_Fixers;
        string[] stages = ["Board", "Taken", "Work", "Pay"];
        var types = (ContractType[])System.Enum.GetValues(typeof(ContractType));
        Rect inner = DemoBox(y, w, 330f, NetrunUI.Ok);
        float bw = (inner.width - 3 * 22f) / 4f;
        for (int i = 0; i < stages.Length; i++)
        {
            var r = new Rect(inner.x + i * (bw + 22f), inner.y, bw, 34f);
            bool sel = _demoStage == i;
            if (i > 0)
                Widgets.DrawLineHorizontal(r.x - 20f, r.center.y, 18f);
            if (NetrunUI.Button(r, ("NR_HelpContractStage_" + stages[i]).Translate(), sel ? NetrunUI.Ok : NetrunUI.Accent, true))
                _demoStage = sel ? -1 : i;
        }
        Text.Font = GameFont.Tiny;
        string stageText = _demoStage < 0 ? "NR_HelpContractStageHint".Translate() : ("NR_HelpContractStageDesc_" + stages[_demoStage]).Translate(s.failReputationLoss.ToString("0"), s.failSilentDays);
        Widgets.Label(new Rect(inner.x, inner.y + 40f, inner.width, 36f), stageText.Colorize(_demoStage < 0 ? NetrunUI.Muted : NetrunUI.TextColor));
        Text.Font = GameFont.Small;

        // The contract kinds, then an example card with the real numbers.
        float x = inner.x, yy = inner.y + 82f;
        foreach (ContractType t in types)
        {
            string label = ("NR_Contract_" + t).Translate();
            float tw = NetrunUI.ButtonWidth(label);
            if (x + tw > inner.xMax)
            {
                x = inner.x;
                yy += 30f;
            }
            if (NetrunUI.Button(new Rect(x, yy, tw, 26f), label, t == _demoContract ? NetrunUI.Ok : NetrunUI.Muted, true))
                _demoContract = t;
            x += tw + 6f;
        }
        yy += 34f;
        (IntRange goal, string reward) = _demoContract switch
        {
            ContractType.DataDelivery => (s.dataDeliveryGoal, "NR_HelpContractPer".Translate(s.dataDeliveryRewardPerItem).ToString()),
            ContractType.SystemTap => (s.systemTapGoal, "NR_HelpContractPer".Translate(s.systemTapRewardPerHack).ToString()),
            ContractType.QuietRun => (s.quietRunGoal, "NR_HelpContractPer".Translate(s.quietRunRewardPerHack).ToString()),
            ContractType.Surveillance => (s.surveillanceHours, "NR_HelpContractPer".Translate(s.surveillanceRewardPerHour).ToString()),
            ContractType.LayLow => (s.layLowDrop, "NR_HelpContractPer".Translate(s.layLowRewardPerHeat).ToString()),
            ContractType.Rookie => (new IntRange(s.rookieDives, s.rookieDives), "NR_HelpContractFlat".Translate(s.rookieReward).ToString()),
            ContractType.AIOrder => (new IntRange(1, 1), "NR_HelpContractFlat".Translate(s.aiOrderReward).ToString()),
            _ => (s.cleanLabChips, "NR_HelpContractPer".Translate(s.cleanLabRewardPerChip).ToString()),
        };
        var card = new Rect(inner.x, yy, inner.width, inner.yMax - yy);
        Widgets.DrawBoxSolid(card, NetrunUI.BgWindow);
        Widgets.DrawBoxSolid(new Rect(card.x, card.y, 3f, card.height), NetrunUI.Ok);
        Rect cr = card.ContractedBy(10f);
        var sample = new Contract { Type = _demoContract, Goal = goal.max, Param = (int)BraindanceMood.Dark };
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(cr.x, cr.y, cr.width, 28f), sample.Title.Colorize(NetrunUI.Ok));
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(cr.x, cr.y + 30f, cr.width, 40f), sample.Description);
        Widgets.Label(new Rect(cr.x, cr.y + 72f, cr.width, 36f), "NR_HelpContractNumbers".Translate(goal.min, goal.max, reward, s.contractDays.min, s.contractDays.max, s.contractReputation.ToString("0")).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        return inner.yMax + 20f;
    }

    // ---- [[braindance]] / [[braindance:Dark]] ----

    private BraindanceMood _demoMood = BraindanceMood.Bright;
    private float _demoStrength2 = 0.6f;
    private string _moodPinnedFor;

    private float BraindanceDemo(float y, float w, string preset)
    {
        if (preset != null && _moodPinnedFor != preset && System.Enum.TryParse(preset, out BraindanceMood m0))
        {
            _demoMood = m0;
            _moodPinnedFor = preset;
        }
        Rect inner = DemoBox(y, w, 250f, NetrunUI.Accent);
        // The path: raw recording → editing at the console → chip.
        ThingDef chipDef = BraindanceEditing.ChipsFor(_demoMood).FirstOrDefault() ?? NetrunDefOf.NR_BraindanceChip;
        (ThingDef def, string label)[] path =
        [
            (NetrunDefOf.NR_BraindanceRaw, "NR_HelpBDStepRaw".Translate()),
            (ThingDefOf.CommsConsole, "NR_HelpBDStepEdit".Translate()),
            (chipDef, chipDef.LabelCap),
        ];
        float step = (inner.width - 2 * 40f) / 3f;
        for (int i = 0; i < path.Length; i++)
        {
            var r = new Rect(inner.x + i * (step + 40f), inner.y, step, 64f);
            Widgets.DrawBoxSolid(r, NetrunUI.BgWindow);
            if (path[i].def != null)
                Widgets.ThingIcon(new Rect(r.x + 8f, r.y + 8f, 48f, 48f), path[i].def);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(r.x + 62f, r.y, r.width - 66f, r.height), path[i].label);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (i > 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(r.x - 40f, r.y, 40f, r.height), "→".Colorize(NetrunUI.Accent));
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }
        // The mood switch and the strength of the moment.
        float yy = inner.y + 74f, x = inner.x;
        foreach (BraindanceMood mood in System.Enum.GetValues(typeof(BraindanceMood)))
        {
            string label = ((string)("NR_BDMood_" + mood).Translate()).CapitalizeFirst();
            float bw = NetrunUI.ButtonWidth(label);
            if (NetrunUI.Button(new Rect(x, yy, bw, 26f), label, mood == _demoMood ? NetrunUI.Ok : NetrunUI.Muted, true))
                _demoMood = mood;
            x += bw + 6f;
        }
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(x + 10f, yy - 2f, inner.xMax - x - 10f, 16f), "NR_HelpBDStrength".Translate(_demoStrength2.ToStringPercent()).Colorize(NetrunUI.Muted));
        _demoStrength2 = Widgets.HorizontalSlider(new Rect(x + 10f, yy + 14f, inner.xMax - x - 10f, 14f), _demoStrength2, 0f, 1f, roundTo: 0.05f);
        yy += 36f;

        // The chip card: what it does, from its defs.
        int tier = _demoStrength2 >= 0.85f ? 2 : _demoStrength2 >= 0.55f ? 1 : 0;
        var drug = chipDef.GetCompProperties<CompProperties_Drug>();
        ThoughtDef high = DefDatabase<ThoughtDef>.GetNamedSilentFail(chipDef == NetrunDefOf.NR_BlackBraindanceChip ? "NR_BlackBraindanceHigh" : "NR_BraindanceHigh");
        float mood2 = high?.stages?.FirstOrDefault()?.baseMoodEffect ?? 0f;
        var card = new Rect(inner.x, yy, inner.width, inner.yMax - yy);
        Widgets.DrawBoxSolid(card, NetrunUI.BgWindow);
        Color cc = chipDef == NetrunDefOf.NR_BlackBraindanceChip ? NetrunUI.Bad : NetrunUI.Accent;
        Widgets.DrawBoxSolid(new Rect(card.x, card.y, 3f, card.height), cc);
        Rect cr = card.ContractedBy(10f);
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(cr.x, cr.y, cr.width, 22f), (chipDef.LabelCap + " · " + ("NR_BDQuality_" + tier).Translate()).Colorize(cc));
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(cr.x, cr.y + 24f, cr.width, cr.height - 24f),
            "NR_HelpBDChipCard".Translate(chipDef.ingestible?.joy.ToStringPercent() ?? "-", mood2.ToString("+0;-0"), drug?.addictiveness.ToStringPercent() ?? "-",
                ("NR_HelpBDTier_" + tier).Translate()));
        Text.Font = GameFont.Small;
        return inner.yMax + 20f;
    }

    // ---- [[ice]] ----

    private int _demoIceStep = -1;

    private float IceDemo(float y, float w)
    {
        CyberspaceSettingsDef s = Cyber;
        Rect inner = DemoBox(y, w, 200f, NetrunUI.Bad);
        (string key, string icon, Color c)[] steps =
        [
            ("Strike", "Dive_ICE", NetrunUI.Bad),
            ("Shield", "Program_Shield", NetrunUI.Accent),
            ("Decoy", "Program_Decoy", NetrunUI.Purple),
            ("Killer", "Program_Killer", NetrunUI.Warn),
            ("Hit", "Dive_BLACKICE", NetrunUI.Bad),
        ];
        float bw = (inner.width - 4 * 18f) / 5f;
        for (int i = 0; i < steps.Length; i++)
        {
            var r = new Rect(inner.x + i * (bw + 18f), inner.y, bw, 70f);
            bool sel = _demoIceStep == i;
            if (sel)
                NetrunUI.Glow(r, steps[i].c, 0.8f, 8f);
            Widgets.DrawBoxSolid(r, NetrunUI.BgWindow);
            NetrunUI.Brackets(r, steps[i].c, 2f, 10f);
            if (NetrunTex.CyberIcon(steps[i].icon) is { } icon)
                GUI.DrawTexture(new Rect(r.center.x - 18f, r.y + 6f, 36f, 36f), icon);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(r.x, r.y + 46f, r.width, 20f), ("NR_HelpIce_" + steps[i].key).Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (i > 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(r.x - 18f, r.y, 18f, r.height), "→".Colorize(NetrunUI.Muted));
                Text.Anchor = TextAnchor.UpperLeft;
            }
            if (Widgets.ButtonInvisible(r))
                _demoIceStep = sel ? -1 : i;
        }
        string text = _demoIceStep < 0
            ? "NR_HelpIceHint".Translate()
            : ("NR_HelpIceDesc_" + steps[_demoIceStep].key).Translate(s.iceAttackChance.ToStringPercent(), s.decoyChance.ToStringPercent(),
                s.netsickPerIceHit.ToStringPercent(), s.blackIceCyberlinkLoss.ToStringPercent());
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(inner.x, inner.y + 80f, inner.width, inner.height - 80f), text.Colorize(_demoIceStep < 0 ? NetrunUI.Muted : NetrunUI.TextColor));
        Text.Font = GameFont.Small;
        return inner.yMax + 20f;
    }

    // ---- [[loadout]] ----

    private readonly HashSet<DiveProgram> _demoPrograms = [];
    private int _demoDaemon = -1;

    private float LoadoutDemo(float y, float w)
    {
        CyberspaceSettingsDef s = Cyber;
        Rect inner = DemoBox(y, w, 190f, NetrunUI.Accent);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(inner.x, inner.y, inner.width, 18f),
            "NR_HelpLoadoutPrograms".Translate(_demoPrograms.Count, s.programSlots, s.programCost).Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        float x = inner.x, yy = inner.y + 20f;
        foreach (DiveProgram p in System.Enum.GetValues(typeof(DiveProgram)))
        {
            string label = ("NR_Program_" + p).Translate();
            float bw = NetrunUI.ButtonWidth(label, icon: true);
            if (x + bw > inner.xMax)
            {
                x = inner.x;
                yy += 30f;
            }
            bool on = _demoPrograms.Contains(p);
            AcceptanceReport can = on || _demoPrograms.Count < s.programSlots ? true : (AcceptanceReport)"NR_HelpLoadoutFull".Translate();
            if (NetrunUI.Button(new Rect(x, yy, bw, 26f), (on ? "✓ " : "") + label, on ? NetrunUI.Ok : NetrunUI.TextColor, can,
                    ("NR_ProgramDesc_" + p).Translate(), NetrunTex.CyberIcon("Program_" + p)))
            {
                if (on)
                    _demoPrograms.Remove(p);
                else
                    _demoPrograms.Add(p);
            }
            x += bw + 6f;
        }
        yy += 32f;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(inner.x, yy, inner.width, 18f), "NR_HelpLoadoutDaemon".Translate().Colorize(NetrunUI.Muted));
        Text.Font = GameFont.Small;
        yy += 18f;
        x = inner.x;
        foreach (DiveDaemon d in System.Enum.GetValues(typeof(DiveDaemon)))
        {
            string label = ("NR_Daemon_" + d).Translate();
            float bw = NetrunUI.ButtonWidth(label, icon: true);
            if (x + bw > inner.xMax)
                break;
            bool on = _demoDaemon == (int)d;
            if (NetrunUI.Button(new Rect(x, yy, bw, 26f), (on ? "✓ " : "") + label, on ? NetrunUI.Purple : NetrunUI.TextColor, true,
                    ("NR_DaemonDesc_" + d).Translate(), NetrunTex.DaemonIcon(d)))
                _demoDaemon = on ? -1 : (int)d;
            x += bw + 6f;
        }
        yy += 34f;
        // The live result: hack chance against a strength-3 gate for a runner with Intellectual 8, and the trace factor.
        bool sword = _demoPrograms.Contains(DiveProgram.Sword) || _demoDaemon == (int)DiveDaemon.Blade;
        bool stealth = _demoPrograms.Contains(DiveProgram.Stealth) || _demoDaemon == (int)DiveDaemon.Wraith;
        float chance = Mathf.Clamp(s.hackBase + 8 * s.hackPerSkill - 3 * s.hackPerStrength + (sword ? s.swordBonus : 0f), 0.05f, 0.95f);
        int cost = s.diveBaseCost + _demoPrograms.Count * s.programCost;
        Widgets.Label(new Rect(inner.x, yy, inner.width, 22f),
            "NR_HelpLoadoutResult".Translate(chance.ToStringPercent(), (stealth ? s.stealthTraceFactor : 1f).ToStringPercent(), cost).Colorize(NetrunUI.Ok));
        return inner.yMax + 20f;
    }
}
