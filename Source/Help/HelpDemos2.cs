using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// More live Cyberpedia blocks: [[aigallery]] (AI archetypes), [[fixerpool]] and [[contractflow]] (fixers and
/// contracts), [[braindance]] (recording to chip), [[ice]] and [[loadout]] (ICE, programs, daemons). Text and numbers
/// come from the game's own defs and settings; layout from HelpKit, so nothing spills out of its frame.
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
        // A link to one archetype opens the gallery on it (once per visit).
        if (preset != null && _aiPinnedFor != preset && System.Enum.TryParse(preset, out AIPersonality p0))
        {
            _demoAI = p0;
            _aiPinnedFor = preset;
        }
        return Frame("aigallery", y, w, NetrunUI.Purple, inner =>
        {
            var all = (AIPersonality[])System.Enum.GetValues(typeof(AIPersonality));
            const float tile = 56f, gap = 8f;
            int perRow = Mathf.Max(4, Mathf.FloorToInt((inner.width + gap) / (tile + gap)));
            float rowH = tile + 24f;
            for (int i = 0; i < all.Length; i++)
            {
                AIPersonality p = all[i];
                bool known = AIKnown(p);
                var r = new Rect(inner.x + i % perRow * (tile + gap), inner.y + i / perRow * rowH, tile, tile);
                bool sel = p == _demoAI;
                Color c = known ? p.Color() : NetrunUI.Muted;
                if (sel)
                    NetrunUI.Glow(r, c, 0.8f, 8f);
                NetrunUI.Box(r, NetrunUI.BgWindow, new Color(c.r, c.g, c.b, sel ? 1f : 0.45f), sel ? 2 : 1);
                if (known)
                    GUI.DrawTexture(r.ContractedBy(4f), NetrunTex.Portrait(p));
                else
                {
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Text.Font = GameFont.Medium;
                    Widgets.Label(r, "?".Colorize(NetrunUI.Muted));
                    Text.Font = GameFont.Small;
                }
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(new Rect(r.x - gap / 2f, r.yMax + 3f, r.width + gap, 18f), (known ? p.Label() : "???").Truncate(r.width + gap).Colorize(sel ? c : NetrunUI.Muted));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                if (Widgets.ButtonInvisible(r, doMouseoverSound: false))
                {
                    _demoAI = p;
                    NetrunSound.Click();
                }
            }
            float top = inner.y + Mathf.CeilToInt(all.Length / (float)perRow) * rowH + 6f;
            Color pc = AIKnown(_demoAI) ? _demoAI.Color() : NetrunUI.Muted;
            return Panel("aigallery.card", inner, top, pc, cr =>
            {
                if (!AIKnown(_demoAI))
                    return Para(cr, cr.y, "NR_HelpAIUnknown".Translate(), NetrunUI.Muted);
                GUI.DrawTexture(new Rect(cr.x, cr.y, 64f, 64f), NetrunTex.Portrait(_demoAI));
                var head = new Rect(cr.x + 76f, cr.y, cr.width - 76f, 64f);
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(head.x, head.y - 2f, head.width, 32f), _demoAI.Label().Colorize(pc));
                float t = Mathf.Max(cr.y + 70f, Para(head, head.y + 30f, _demoAI.Description(), NetrunUI.TextColor));
                t = KV(cr, t, "NR_HelpAIService".Translate(), AIBehavior.ServiceLabel(_demoAI) + ": " + ("NR_AIServiceDesc_" + _demoAI).Translate(), labelWidth: 110f);
                t = KV(cr, t, "NR_HelpAIFood".Translate(), AIBehavior.FoodText(_demoAI), labelWidth: 110f);
                t = Slider(cr, t + 8f, "NR_HelpAITrust".Translate(Mathf.RoundToInt(_demoTrust)), ref _demoTrust, 0f, 100f);
                AISettingsDef s = NetrunDefOf.NR_AI;
                var steps = new List<(float at, string text, bool good)>
                {
                    (s.betrayalTrust, "NR_HelpAITrustBetray".Translate(s.betrayalTrust.ToString("0")), false),
                    (s.escapeTrust, "NR_HelpAITrustEscape".Translate(s.escapeTrust.ToString("0"), s.escapeHunger.ToString("0")), false),
                    (s.minTrustForService, "NR_HelpAITrustService".Translate(s.minTrustForService.ToString("0")), true),
                    (s.traitRevealTrustFirst, "NR_HelpAITrustTrait1".Translate(s.traitRevealTrustFirst.ToString("0")), true),
                    (s.eventGoodTrust, "NR_HelpAITrustEvents".Translate(s.eventGoodTrust.ToString("0")), true),
                    (s.traitRevealTrustSecond, "NR_HelpAITrustTrait2".Translate(s.traitRevealTrustSecond.ToString("0")), true),
                };
                foreach ((float at, string text, bool good) in steps.OrderBy(x => x.at))
                {
                    // Bad marks hold below their value, good ones from their value up.
                    bool on = good ? _demoTrust >= at : _demoTrust < at;
                    t = Para(cr, t, (on ? "● " : "○ ") + text, on ? good ? NetrunUI.Ok : NetrunUI.Bad : NetrunUI.Muted);
                }
                return t;
            });
        });
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

    private float FixerPoolDemo(float y, float w) => Frame("fixerpool", y, w, NetrunUI.Warn, inner =>
    {
        FixerSettingsDef s = NetrunDefOf.NR_Fixers;
        float tile = Mathf.Min(104f, (inner.width - 4 * 8f) / 5f);
        for (int i = 0; i < DemoFixers.Length; i++)
        {
            var f = DemoFixers[i];
            var r = new Rect(inner.x + i * (tile + 8f), inner.y, tile, tile);
            Color c = FixersTab.SpecialtyColor(f.spec);
            bool sel = i == _demoFixer;
            if (sel)
                NetrunUI.Glow(r, c, 0.8f, 8f);
            NetrunUI.Box(r, NetrunUI.BgWindow, new Color(c.r, c.g, c.b, sel ? 1f : 0.45f), sel ? 2 : 1);
            GUI.DrawTexture(r.ContractedBy(5f), NetrunTex.Portrait(f.spec));
            if (f.isNew)
                NetrunUI.Chip(r.x + 4f, r.y + 4f, "NR_FixerNewTag".Translate(), NetrunUI.Accent);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(r.x - 4f, r.yMax + 3f, r.width + 8f, 18f), ((string)("NR_FixerSpecialty_" + f.spec).Translate()).Truncate(r.width + 8f).Colorize(sel ? c : NetrunUI.Muted));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (Widgets.ButtonInvisible(r, doMouseoverSound: false))
            {
                _demoFixer = i;
                NetrunSound.Click();
            }
        }

        var pick = DemoFixers[_demoFixer];
        // A real fixer object: prices, the scrub rate and the call come from the same code the game uses.
        var fixer = new Fixer { Specialty = pick.spec, Trait = pick.trait, Reputation = _demoRep, CallCost = Mathf.RoundToInt(s.callCost.Average) };
        Color sc = FixersTab.SpecialtyColor(pick.spec);
        return Panel("fixerpool.card", inner, inner.y + tile + 30f, sc, cr =>
        {
            float t = cr.y;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(cr.x, t, cr.width, 32f), ((string)("NR_FixerSpecialty_" + pick.spec).Translate()).Colorize(sc));
            Text.Font = GameFont.Small;
            t = Para(cr, t + 32f, ("NR_FixerSpecialtyDesc_" + pick.spec).Translate(), NetrunUI.TextColor);
            float sliderY = t;
            t = Slider(cr, t + 4f, "NR_HelpFixerRep".Translate(Mathf.RoundToInt(_demoRep)), ref _demoRep, 0f, 100f);
            // Marks on the slider: the trait (purple) and pinning (gold).
            foreach ((float at, Color mc) in new[] { (s.traitRevealReputation, NetrunUI.Purple), (s.pinnedReputation, NetrunUI.Warn) })
                Widgets.DrawBoxSolid(new Rect(cr.x + cr.width * at / 100f - 1f, sliderY + 18f, 2f, 22f), mc);
            fixer.Reputation = _demoRep;
            bool traitSeen = _demoRep >= s.traitRevealReputation;
            t = KV(cr, t, "NR_HelpFixerTraitLabel".Translate(), traitSeen
                    ? ("NR_FixerTrait_" + pick.trait).Translate() + ". " + ("NR_FixerTraitDesc_" + pick.trait).Translate()
                    : "NR_HelpFixerTraitHidden".Translate(Mathf.CeilToInt(s.traitRevealReputation - _demoRep)),
                traitSeen ? NetrunUI.TextColor : NetrunUI.Muted);
            bool pinned = _demoRep >= s.pinnedReputation;
            t = KV(cr, t, "NR_HelpFixerPinLabel".Translate(), pinned ? "NR_HelpFixerPinned".Translate(s.maxPinned) : "NR_HelpFixerToPin".Translate(Mathf.CeilToInt(s.pinnedReputation - _demoRep)),
                pinned ? NetrunUI.Warn : NetrunUI.Muted);
            t = KV(cr, t, "NR_HelpFixerPriceLabel".Translate(), "×" + fixer.PriceFactor.ToStringPercent(), NetrunUI.Ok);
            t = KV(cr, t, "NR_HelpFixerScrubLabel".Translate(), "NR_HelpFixerScrubValue".Translate(fixer.HeatPerUnitAt(0f, false).ToString("0.#")), NetrunUI.Ok);
            t = KV(cr, t, "NR_HelpFixerCallLabel".Translate(), "NR_HelpContractFlat".Translate(fixer.EffectiveCallCost), NetrunUI.Ok);
            if (pick.spec == FixerSpecialty.Fence)
                t = KV(cr, t, "NR_HelpFixerRareLabel".Translate(), pinned || pick.trait == FixerTrait.Collector ? "NR_HelpFixerRareOn".Translate() : "NR_HelpFixerRareOff".Translate(), NetrunUI.Accent);
            return t;
        });
    });

    // ---- [[contractflow]] ----

    private int _demoStage = -1;
    private ContractType _demoContract = ContractType.SystemTap;
    private static readonly string[] Stages = ["Board", "Taken", "Work", "Pay"];

    private float ContractFlowDemo(float y, float w) => Frame("contractflow", y, w, NetrunUI.Ok, inner =>
    {
        FixerSettingsDef s = NetrunDefOf.NR_Fixers;
        const float link = 20f;
        float bw = (inner.width - 3 * link) / 4f;
        for (int i = 0; i < Stages.Length; i++)
        {
            var r = new Rect(inner.x + i * (bw + link), inner.y, bw, 34f);
            if (i > 0)
                Widgets.DrawBoxSolid(new Rect(r.x - link, r.center.y - 1f, link, 2f), new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.5f));
            bool sel = _demoStage == i;
            if (sel)
                Widgets.DrawBoxSolid(r, new Color(NetrunUI.Ok.r, NetrunUI.Ok.g, NetrunUI.Ok.b, 0.14f));
            if (NetrunUI.Button(r, ("NR_HelpContractStage_" + Stages[i]).Translate(), sel ? NetrunUI.Ok : NetrunUI.Accent, true))
                _demoStage = sel ? -1 : i;
        }
        float t = inner.y + 42f;
        t = Para(inner, t, _demoStage < 0
                ? "NR_HelpContractStageHint".Translate()
                : ("NR_HelpContractStageDesc_" + Stages[_demoStage]).Translate(s.failReputationLoss.ToString("0"), s.failSilentDays),
            _demoStage < 0 ? NetrunUI.Muted : NetrunUI.TextColor);
        t = Choices(inner, t + 6f, (ContractType[])System.Enum.GetValues(typeof(ContractType)), c => ("NR_Contract_" + c).Translate(),
            c => c == _demoContract, c => _demoContract = c);

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
        var sample = new Contract { Type = _demoContract, Goal = goal.max, Param = (int)BraindanceMood.Dark };
        return Panel("contractflow.card", inner, t, NetrunUI.Ok, cr =>
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(cr.x, cr.y, cr.width, 32f), sample.Title.Colorize(NetrunUI.Ok));
            Text.Font = GameFont.Small;
            float u = Para(cr, cr.y + 34f, sample.Description, NetrunUI.TextColor);
            u = KV(cr, u + 4f, "NR_HelpContractGoal".Translate(), goal.min == goal.max ? goal.min.ToString() : $"{goal.min}–{goal.max}");
            u = KV(cr, u, "NR_HelpContractPay".Translate(), reward, NetrunUI.Ok);
            u = KV(cr, u, "NR_HelpContractDeadline".Translate(), "NR_HelpContractDays".Translate(s.contractDays.min, s.contractDays.max));
            return KV(cr, u, "NR_HelpContractRep".Translate(), "+" + s.contractReputation.ToString("0"), NetrunUI.Warn);
        });
    });

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
        return Frame("braindance", y, w, NetrunUI.Accent, inner =>
        {
            ThingDef chipDef = BraindanceEditing.ChipsFor(_demoMood).FirstOrDefault() ?? NetrunDefOf.NR_BraindanceChip;
            (ThingDef def, string label)[] path =
            [
                (NetrunDefOf.NR_BraindanceRaw, "NR_HelpBDStepRaw".Translate()),
                (ThingDefOf.CommsConsole, "NR_HelpBDStepEdit".Translate()),
                (chipDef, chipDef.LabelCap),
            ];
            const float arrow = 34f;
            float step = (inner.width - 2 * arrow) / 3f;
            Text.Font = GameFont.Small;
            float pathH = path.Max(p => Mathf.Max(64f, Text.CalcHeight(p.label, step - 72f) + 12f));
            for (int i = 0; i < path.Length; i++)
            {
                var r = new Rect(inner.x + i * (step + arrow), inner.y, step, pathH);
                Widgets.DrawBoxSolid(r, NetrunUI.BgWindow);
                if (path[i].def != null)
                    Widgets.ThingIcon(new Rect(r.x + 8f, r.center.y - 24f, 48f, 48f), path[i].def);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(r.x + 64f, r.y, r.width - 70f, r.height), path[i].label);
                if (i > 0)
                {
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(new Rect(r.x - arrow, r.y, arrow, r.height), "→".Colorize(NetrunUI.Accent));
                }
                Text.Anchor = TextAnchor.UpperLeft;
            }
            float t = inner.y + pathH + 10f;
            t = Columns(inner, t, 0.5f,
                // Dark and combat make the same chip, so they are one choice here.
                (l, top) => Choices(l, top, new[] { BraindanceMood.Bright, BraindanceMood.Dark },
                    m => m == BraindanceMood.Bright ? ((string)"NR_BDMood_Bright".Translate()).CapitalizeFirst() : (string)"NR_HelpBDMoodDarkCombat".Translate(),
                    m => m == _demoMood || (m == BraindanceMood.Dark && _demoMood == BraindanceMood.Combat), m => _demoMood = m),
                (r, top) => Slider(r, top, "NR_HelpBDStrength".Translate(_demoStrength2.ToStringPercent()), ref _demoStrength2, 0f, 1f, 0.05f));

            int tier = _demoStrength2 >= 0.85f ? 2 : _demoStrength2 >= 0.55f ? 1 : 0;
            var drug = chipDef.GetCompProperties<CompProperties_Drug>();
            bool black = chipDef == NetrunDefOf.NR_BlackBraindanceChip;
            ThoughtDef high = DefDatabase<ThoughtDef>.GetNamedSilentFail(black ? "NR_BlackBraindanceHigh" : "NR_BraindanceHigh");
            float mood = high?.stages?.FirstOrDefault()?.baseMoodEffect ?? 0f;
            Color cc = black ? NetrunUI.Bad : NetrunUI.Accent;
            return Panel("braindance.card", inner, t, cc, cr =>
            {
                Widgets.ThingIcon(new Rect(cr.x, cr.y, 40f, 40f), chipDef);
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(cr.x + 50f, cr.y + 2f, cr.width - 50f, 34f), (chipDef.LabelCap + " · " + ("NR_BDQuality_" + tier).Translate()).Colorize(cc));
                Text.Font = GameFont.Small;
                float u = cr.y + 46f;
                u = KV(cr, u, "NR_HelpBDJoy".Translate(), chipDef.ingestible?.joy.ToStringPercent() ?? "-", NetrunUI.Ok);
                u = KV(cr, u, "NR_HelpBDMoodLabel".Translate(), mood.ToString("+0;-0"), mood >= 0 ? NetrunUI.Ok : NetrunUI.Bad);
                u = KV(cr, u, "NR_HelpBDAddiction".Translate(), drug?.addictiveness.ToStringPercent() ?? "-", black ? NetrunUI.Bad : NetrunUI.Warn);
                return KV(cr, u, "NR_HelpBDQualityLabel".Translate(), ("NR_HelpBDTier_" + tier).Translate());
            });
        });
    }

    // ---- [[ice]] ----

    private int _demoIceStep = 0;
    private static readonly (string key, string icon)[] IceSteps =
    [
        ("Strike", "Dive_ICE"), ("Shield", "Program_Shield"), ("Decoy", "Program_Decoy"), ("Killer", "Program_Killer"), ("Hit", "Dive_BLACKICE"),
    ];

    private static Color IceColor(int i) => i switch { 1 => NetrunUI.Accent, 2 => NetrunUI.Purple, 3 => NetrunUI.Warn, _ => NetrunUI.Bad };

    private float IceDemo(float y, float w) => Frame("ice", y, w, NetrunUI.Bad, inner =>
    {
        CyberspaceSettingsDef s = Cyber;
        const float arrow = 18f;
        float bw = (inner.width - 4 * arrow) / 5f;
        for (int i = 0; i < IceSteps.Length; i++)
        {
            var r = new Rect(inner.x + i * (bw + arrow), inner.y, bw, 76f);
            bool sel = _demoIceStep == i;
            Color c = IceColor(i);
            if (sel)
                NetrunUI.Glow(r, c, 0.8f, 8f);
            Widgets.DrawBoxSolid(r, NetrunUI.BgWindow);
            NetrunUI.Brackets(r, new Color(c.r, c.g, c.b, sel ? 1f : 0.6f), 2f, 10f);
            if (NetrunTex.CyberIcon(IceSteps[i].icon) is { } icon)
                GUI.DrawTexture(new Rect(r.center.x - 18f, r.y + 8f, 36f, 36f), icon);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(r.x, r.y + 48f, r.width, 24f), ((string)("NR_HelpIce_" + IceSteps[i].key).Translate()).Truncate(r.width).Colorize(sel ? c : NetrunUI.TextColor));
            if (i > 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(r.x - arrow, r.y, arrow, r.height), "→".Colorize(NetrunUI.Muted));
            }
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(r, doMouseoverSound: false))
            {
                _demoIceStep = i;
                NetrunSound.Click();
            }
        }
        return Panel("ice.card", inner, inner.y + 84f, IceColor(_demoIceStep), cr =>
            Para(cr, cr.y, ("NR_HelpIceDesc_" + IceSteps[_demoIceStep].key).Translate(s.iceAttackChance.ToStringPercent(), s.decoyChance.ToStringPercent(),
                s.netsickPerIceHit.ToStringPercent(), s.blackIceCyberlinkLoss.ToStringPercent()), NetrunUI.TextColor));
    });

    // ---- [[loadout]] ----

    private readonly HashSet<DiveProgram> _demoPrograms = [];
    private int _demoDaemon = -1;

    private float LoadoutDemo(float y, float w) => Frame("loadout", y, w, NetrunUI.Accent, inner =>
    {
        CyberspaceSettingsDef s = Cyber;
        float t = Para(inner, inner.y, "NR_HelpLoadoutPrograms".Translate(_demoPrograms.Count, s.programSlots, s.programCost), NetrunUI.Muted, GameFont.Tiny);
        t = Choices(inner, t, (DiveProgram[])System.Enum.GetValues(typeof(DiveProgram)), p => (_demoPrograms.Contains(p) ? "✓ " : "") + ("NR_Program_" + p).Translate(),
            p => _demoPrograms.Contains(p),
            p =>
            {
                if (_demoPrograms.Contains(p))
                    _demoPrograms.Remove(p);
                else if (_demoPrograms.Count < s.programSlots)
                    _demoPrograms.Add(p);
                else
                    Messages.Message("NR_HelpLoadoutFull".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            },
            icon: p => NetrunTex.CyberIcon("Program_" + p), tip: p => ("NR_ProgramDesc_" + p).Translate());
        t = Para(inner, t, "NR_HelpLoadoutDaemon".Translate(), NetrunUI.Muted, GameFont.Tiny);
        t = Choices(inner, t, (DiveDaemon[])System.Enum.GetValues(typeof(DiveDaemon)), d => (_demoDaemon == (int)d ? "✓ " : "") + ("NR_Daemon_" + d).Translate(),
            d => _demoDaemon == (int)d, d => _demoDaemon = _demoDaemon == (int)d ? -1 : (int)d,
            _ => NetrunUI.Purple, d => NetrunTex.DaemonIcon(d), d => ("NR_DaemonDesc_" + d).Translate());

        // The live result: a runner with Intellectual 8 against a strength-3 gate.
        bool sword = _demoPrograms.Contains(DiveProgram.Sword) || _demoDaemon == (int)DiveDaemon.Blade;
        bool stealth = _demoPrograms.Contains(DiveProgram.Stealth) || _demoDaemon == (int)DiveDaemon.Wraith;
        float chance = Mathf.Clamp(s.hackBase + 8 * s.hackPerSkill - 3 * s.hackPerStrength + (sword ? s.swordBonus : 0f), 0.05f, 0.95f);
        int cost = s.diveBaseCost + _demoPrograms.Count * s.programCost;
        return Panel("loadout.card", inner, t + 2f, NetrunUI.Ok, cr =>
        {
            float u = KV(cr, cr.y, "NR_HelpLoadoutChance".Translate(), chance.ToStringPercent(), chance >= 0.6f ? NetrunUI.Ok : NetrunUI.Warn);
            u = KV(cr, u, "NR_HelpLoadoutTrace".Translate(), "×" + (stealth ? s.stealthTraceFactor : 1f).ToStringPercent(), NetrunUI.Ok);
            return KV(cr, u, "NR_HelpLoadoutCost".Translate(), "NR_HelpLoadoutCostValue".Translate(cost), NetrunUI.Warn);
        });
    });
}
