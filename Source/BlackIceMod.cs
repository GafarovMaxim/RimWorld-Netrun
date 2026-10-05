using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace BlackIce
{
    public class BlackIceMod : Mod
    {
        public static BlackIceSettings Settings { get; private set; }
        public static Harmony HarmonyInstance { get; private set; }

        public BlackIceMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<BlackIceSettings>();
            HarmonyInstance = new Harmony("OpenAI.HackingExpansionBlackIce");
            PatchHackingExpansion();
        }

        public override string SettingsCategory() => "Hacking Expansion: Black ICE";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);
            list.Label("Digital heat gain multiplier: " + Settings.HeatGainMultiplier.ToString("0.0x"));
            Settings.HeatGainMultiplier = list.Slider(Settings.HeatGainMultiplier, 0.25f, 3f);
            list.Gap();
            list.Label("Network attack intensity: " + Settings.NetworkAttackMultiplier.ToString("0.0x"));
            Settings.NetworkAttackMultiplier = list.Slider(Settings.NetworkAttackMultiplier, 0.25f, 3f);
            list.Gap();
            list.Label("Braindance addiction multiplier: " + Settings.BraindanceAddictionMultiplier.ToString("0.0x"));
            Settings.BraindanceAddictionMultiplier = list.Slider(Settings.BraindanceAddictionMultiplier, 0.25f, 3f);
            list.End();
            base.DoSettingsWindowContents(inRect);
        }

        private static void PatchHackingExpansion()
        {
            try
            {
                Type hackType = AccessTools.TypeByName("Ushanka.HackingExpansion.CompHackable")
                                ?? AccessTools.TypeByName("CompHackable");

                if (hackType == null)
                {
                    Log.Warning("[Black ICE] Hacking Expansion CompHackable was not found. Integration patches skipped.");
                    return;
                }

                if (AccessTools.Method(hackType, "ProcessHacked") != null)
                    PatchIfExists(hackType, "ProcessHacked", nameof(HarmonyPatches.HackPostfix));
                else if (AccessTools.Method(hackType, "OnHacked") != null)
                    PatchIfExists(hackType, "OnHacked", nameof(HarmonyPatches.HackPostfix));
                else if (AccessTools.Method(hackType, "Hack") != null)
                    PatchIfExists(hackType, "Hack", nameof(HarmonyPatches.HackPostfix));
                PatchCanHack(hackType);
            }
            catch (Exception ex)
            {
                Log.Error("[Black ICE] Failed to patch Hacking Expansion: " + ex);
            }
        }

        private static void PatchIfExists(Type targetType, string methodName, string postfixName)
        {
            MethodInfo method = AccessTools.Method(targetType, methodName);
            MethodInfo postfix = AccessTools.Method(typeof(HarmonyPatches), postfixName);
            if (method != null && postfix != null)
                HarmonyInstance.Patch(method, postfix: new HarmonyMethod(postfix));
        }

        private static void PatchCanHack(Type targetType)
        {
            MethodInfo method = AccessTools.Method(targetType, "CanHackNow");
            MethodInfo postfix = AccessTools.Method(typeof(HarmonyPatches), nameof(HarmonyPatches.CanHackPostfix));
            if (method != null && postfix != null && method.ReturnType == typeof(bool))
                HarmonyInstance.Patch(method, postfix: new HarmonyMethod(postfix));
        }
    }

    public class BlackIceSettings : ModSettings
    {
        public float HeatGainMultiplier = 1f;
        public float NetworkAttackMultiplier = 1f;
        public float BraindanceAddictionMultiplier = 1f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref HeatGainMultiplier, "heatGainMultiplier", 1f);
            Scribe_Values.Look(ref NetworkAttackMultiplier, "networkAttackMultiplier", 1f);
            Scribe_Values.Look(ref BraindanceAddictionMultiplier, "braindanceAddictionMultiplier", 1f);
        }
    }

    public static class HarmonyPatches
    {
        public static void HackPostfix(object __instance, object[] __args)
        {
            try
            {
                Thing target = ReflectionUtility.FindThing(__instance);
                Pawn hacker = __args?.OfType<Pawn>().FirstOrDefault();
                if (target == null) return;
                BlackIceGameComponent.Instance.RegisterHack(hacker, target);
            }
            catch (Exception ex)
            {
                Log.Error("[Black ICE] Hack postfix failed: " + ex);
            }
        }

        public static void CanHackPostfix(object __instance, ref bool __result)
        {
            if (!__result) return;
            try
            {
                Thing target = ReflectionUtility.FindThing(__instance);
                if (target == null) return;
                if (BlackIceGameComponent.Instance.ShouldAdaptiveICEBlock(target))
                {
                    __result = false;
                    Messages.Message("Adaptive ICE rejected the intrusion attempt.", MessageTypeDefOf.RejectInput, false);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[Black ICE] Adaptive ICE postfix failed: " + ex);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetGizmos))]
        public static void ThingWithCompsGizmosPostfix(ThingWithComps __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__result == null) return;
            List<Gizmo> result = __result.ToList();
            if (__instance.def?.defName == "USH_Cyberpod")
            {
                result.Add(BlackIceGameComponent.Instance.MakeJackInGizmo(__instance));
            }
            __result = result;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
        public static void PawnGizmosPostfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__result == null) return;
            if (!BlackIceGameComponent.Instance.IsInCyberspace(__instance)) return;
            List<Gizmo> result = __result.ToList();
            result.Add(new Command_Action
            {
                defaultLabel = "Jack out",
                defaultDesc = "End the active cyberspace session.",
                action = () => BlackIceGameComponent.Instance.JackOut(__instance),
            });
            __result = result;
        }
    }

    public sealed class CompProperties_NetTerminal : CompProperties
    {
        public CompProperties_NetTerminal() => compClass = typeof(CompNetTerminal);
    }

    public sealed class CompNetTerminal : ThingComp
    {
        public override string CompInspectStringExtra()
        {
            return "Digital heat: " + BlackIceGameComponent.Instance.DigitalHeat.ToString("0") + "/100";
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            foreach (Gizmo gizmo in BlackIceGameComponent.Instance.MakeTerminalGizmos(parent)) yield return gizmo;
        }
    }

    public sealed class CompCyberData : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            yield return new Command_Action
            {
                defaultLabel = "Sell cyberdata",
                defaultDesc = "Sell this data fragment on the black market.",
                action = () => BlackIceGameComponent.Instance.SellCyberdata(parent),
            };
        }
    }

    public sealed class CompAIFragment : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            yield return new Command_Action
            {
                defaultLabel = "Establish contact",
                defaultDesc = "Connect to the fragment and let it introduce itself.",
                action = () => BlackIceGameComponent.Instance.EstablishAIContact(parent)
            };
            yield return new Command_Action
            {
                defaultLabel = "Sell to collector",
                defaultDesc = "Sell the fragment without opening it.",
                action = () => BlackIceGameComponent.Instance.SellAIFragment(parent),
            };
        }
    }

    public static class ReflectionUtility
    {
        public static Thing FindThing(object instance)
        {
            if (instance == null) return null;
            Type t = instance.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (string name in new[] { "parent", "Parent", "thing", "Thing", "target", "Target" })
            {
                FieldInfo f = t.GetField(name, flags);
                if (f != null && f.GetValue(instance) is Thing thing) return thing;
                PropertyInfo p = t.GetProperty(name, flags);
                if (p != null && p.CanRead && p.GetValue(instance, null) is Thing thing2) return thing2;
            }
            return null;
        }
    }

    public class BlackIceGameComponent : GameComponent
    {
        public static BlackIceGameComponent Instance { get; private set; }

        public float DigitalHeat;
        public int LastAttackTick;
        public int NextAttackTick = -1;
        public int TotalSuccessfulHacks;
        public int TotalDataRecovered;
        public int TraceEscalations;

        private List<string> adaptiveKeys = new List<string>();
        private List<int> adaptiveCounts = new List<int>();
        private List<string> aiNames = new List<string>();
        private List<int> aiTypes = new List<int>();
        private List<int> aiLevels = new List<int>();
        private int activeFixerType = -1;
        private int fixerProgress;
        private int fixerGoal;
        private int fixerReward;
        private int fixerExpiresTick;
        private string fixerTitle = "";
        private HashSet<int> activeCyberPawnIds = new HashSet<int>();

        public BlackIceGameComponent(Game game)
        {
            Instance = this;
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (NextAttackTick < 0) ScheduleNextAttack();
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref DigitalHeat, "digitalHeat", 0f);
            Scribe_Values.Look(ref LastAttackTick, "lastAttackTick", 0);
            Scribe_Values.Look(ref NextAttackTick, "nextAttackTick", -1);
            Scribe_Values.Look(ref TotalSuccessfulHacks, "successfulHacks", 0);
            Scribe_Values.Look(ref TotalDataRecovered, "dataRecovered", 0);
            Scribe_Values.Look(ref TraceEscalations, "traceEscalations", 0);
            Scribe_Collections.Look(ref adaptiveKeys, "adaptiveKeys", LookMode.Value);
            Scribe_Collections.Look(ref adaptiveCounts, "adaptiveCounts", LookMode.Value);
            Scribe_Collections.Look(ref aiNames, "aiNames", LookMode.Value);
            Scribe_Collections.Look(ref aiTypes, "aiTypes", LookMode.Value);
            Scribe_Collections.Look(ref aiLevels, "aiLevels", LookMode.Value);
            Scribe_Values.Look(ref activeFixerType, "activeFixerType", -1);
            Scribe_Values.Look(ref fixerProgress, "fixerProgress", 0);
            Scribe_Values.Look(ref fixerGoal, "fixerGoal", 0);
            Scribe_Values.Look(ref fixerReward, "fixerReward", 0);
            Scribe_Values.Look(ref fixerExpiresTick, "fixerExpiresTick", 0);
            Scribe_Values.Look(ref fixerTitle, "fixerTitle", "");
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 250 == 0)
            {
                DigitalHeat = Mathf.Max(0f, DigitalHeat - 0.015f);
                UpdateCyberspaceDependency(now);
                UpdateGhostContract(now);
            }
            if (now >= NextAttackTick && DigitalHeat >= 55f && now - LastAttackTick > 18000)
            {
                ExecuteNetworkAttack();
                ScheduleNextAttack();
            }
            if (now % 2000 == 0)
            {
                MaybeEscalateTraceEvent();
            }
        }

        public void RegisterHack(Pawn hacker, Thing target)
        {
            if (target == null || target.Destroyed) return;
            TotalSuccessfulHacks++;
            string key = target.def?.defName ?? "unknown";
            int idx = adaptiveKeys.IndexOf(key);
            if (idx < 0)
            {
                adaptiveKeys.Add(key);
                adaptiveCounts.Add(1);
            }
            else adaptiveCounts[idx]++;

            float skillFactor = hacker?.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 8f;
            float heatGain = Mathf.Clamp(2.5f + skillFactor * 0.45f, 4f, 12f);
            if (IsInCyberspace(hacker)) heatGain *= 0.55f;
            DigitalHeat = Mathf.Min(100f, DigitalHeat + heatGain * BlackIceMod.Settings.HeatGainMultiplier);

            GiveCyberdata(target, hacker);
            if (hacker != null && idx >= 0 && adaptiveCounts[idx] >= 3 && Rand.Chance(0.08f + Mathf.Min(0.2f, adaptiveCounts[idx] * 0.01f)))
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("BI_ICEFeedback");
                if (def != null)
                    hacker.health.AddHediff(def);
            }

            if (activeFixerType == 1)
            {
                fixerProgress++;
                TryCompleteFixer();
            }
        }

        public bool ShouldAdaptiveICEBlock(Thing target)
        {
            if (target?.def == null) return false;
            int idx = adaptiveKeys.IndexOf(target.def.defName);
            if (idx < 0) return false;
            int count = adaptiveCounts[idx];
            if (count < 3) return false;
            float chance = Mathf.Min(0.35f, (count - 2) * 0.035f);
            return Rand.Chance(chance);
        }

        private void GiveCyberdata(Thing target, Pawn hacker)
        {
            ThingDef dataDef = DefDatabase<ThingDef>.GetNamedSilentFail("BI_Cyberdata");
            if (dataDef == null) return;
            int amount = 1;
            if (target.def.building != null) amount += Rand.RangeInclusive(0, 1);
            if (hacker != null && hacker.skills.GetSkill(SkillDefOf.Intellectual).Level >= 15) amount++;
            amount = Mathf.Min(amount, 4);
            Thing data = ThingMaker.MakeThing(dataDef);
            data.stackCount = amount;
            Map map = hacker?.Map ?? Find.CurrentMap;
            if (map != null)
            {
                GenPlace.TryPlaceThing(data, hacker?.Position ?? CellFinder.RandomCell(map), map, ThingPlaceMode.Near);
                TotalDataRecovered += amount;
                if (activeFixerType == 0)
                {
                    fixerProgress += amount;
                    TryCompleteFixer();
                }
                if (Rand.Chance(0.015f + DigitalHeat / 1500f)) SpawnAIFragment(map, hacker?.Position ?? CellFinder.RandomCell(map));
            }
        }

        private void SpawnAIFragment(Map map, IntVec3 cell)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("BI_UnknownAI");
            if (def == null || map == null) return;
            Thing thing = ThingMaker.MakeThing(def);
            GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
            Find.LetterStack.ReceiveLetter("UNKNOWN PROCESS", "Something inside the recovered data shard is still active. The fragment occasionally emits a handshake request.", LetterDefOf.PositiveEvent);
        }

        public IEnumerable<Gizmo> MakeTerminalGizmos(Thing terminal)
        {
            yield return new Command_Action
            {
                defaultLabel = "Call fixer",
                defaultDesc = "Open a contract with a black-market fixer.",
                action = RequestFixerOffer,
            };
            yield return new Command_Action
            {
                defaultLabel = "Scrub digital trace",
                defaultDesc = "Burn 2 cyberdata to erase 20 points of digital heat.",
                action = ScrubTrace,
            };
            if (aiNames.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Consult contacted AI",
                    defaultDesc = "Ask one of the AIs you have brought back from the network for assistance.",
                    action = ConsultAI
                };
            }
        }

        public Gizmo MakeJackInGizmo(ThingWithComps pod)
        {
            return new Command_Action
            {
                defaultLabel = "Jack into cyberspace",
                defaultDesc = "Select a colonist to enter a deep network session. Long sessions build dependency.",
                action = () => ChooseDiver(pod),
                icon = TexCommand.Attack
            };
        }

        private void ChooseDiver(ThingWithComps pod)
        {
            Map map = pod.Map;
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned.Where(p => !p.Dead && p.health.capacities.CapableOf(PawnCapacityDefOf.Consciousness)))
            {
                Pawn localPawn = pawn;
                options.Add(new FloatMenuOption(localPawn.LabelShort, () => StartDive(localPawn, pod)));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("No eligible colonists", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public void StartDive(Pawn pawn, Thing pod)
        {
            if (pawn == null || pawn.Dead) return;
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("BI_CyberspaceDependency");
            if (def != null && pawn.health.hediffSet.GetFirstHediffOfDef(def) == null)
                pawn.health.AddHediff(def);
            activeCyberPawnIds.Add(pawn.thingIDNumber);
            Messages.Message(pawn.LabelShort + " is jacked into cyberspace.", MessageTypeDefOf.PositiveEvent, false);
        }

        public bool IsInCyberspace(Pawn pawn) => pawn != null && activeCyberPawnIds.Contains(pawn.thingIDNumber);

        public void JackOut(Pawn pawn)
        {
            if (pawn == null) return;
            activeCyberPawnIds.Remove(pawn.thingIDNumber);
            Messages.Message(pawn.LabelShort + " disconnected from cyberspace.", MessageTypeDefOf.NeutralEvent, false);
        }

        private void UpdateCyberspaceDependency(int now)
        {
            if (now % 250 != 0) return;
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive.Where(p => p.Faction == Faction.OfPlayer && !p.Dead))
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("BI_CyberspaceDependency");
                if (def == null) continue;
                Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(def);
                if (h == null) continue;
                if (IsInCyberspace(pawn)) h.Severity = Mathf.Min(1f, h.Severity + 0.0015f);
                else h.Severity = Mathf.Max(0f, h.Severity - 0.00025f);
                if (h.Severity <= 0.001f) pawn.health.RemoveHediff(h);
            }
        }

        public void RequestFixerOffer()
        {
            if (activeFixerType >= 0)
            {
                Messages.Message("A fixer contract is already active.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Ghost Delivery — 5 cyberdata — 650 silver", () => StartFixer(0, 5, 650, 60000, "Ghost Delivery")),
                new FloatMenuOption("System Tap — 3 successful hacks — 900 silver", () => StartFixer(1, 3, 900, 90000, "System Tap")),
                new FloatMenuOption("Burn The Trace — reduce digital heat below 20 — 700 silver", () => StartFixer(2, 20, 700, 90000, "Burn The Trace"))
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void StartFixer(int type, int goal, int reward, int duration, string title)
        {
            activeFixerType = type;
            fixerProgress = type == 2 ? (int)DigitalHeat : 0;
            fixerGoal = goal;
            fixerReward = reward;
            fixerExpiresTick = Find.TickManager.TicksGame + duration;
            fixerTitle = title;
            Find.LetterStack.ReceiveLetter("FIXER // JOB ACCEPTED", title + " is live. Reward: " + reward + " silver. Deadline: " + ((duration / 60000f) * 1f).ToString("0.0") + " days.", LetterDefOf.NeutralEvent);
        }

        private void UpdateGhostContract(int now)
        {
            if (activeFixerType != 2) return;
            if (now > fixerExpiresTick)
            {
                FailFixer();
                return;
            }
            if (DigitalHeat <= fixerGoal) TryCompleteFixer();
        }

        private void TryCompleteFixer()
        {
            if (activeFixerType < 0) return;
            bool complete = activeFixerType switch
            {
                0 => fixerProgress >= fixerGoal,
                1 => fixerProgress >= fixerGoal,
                2 => DigitalHeat <= fixerGoal,
                _ => false
            };
            if (!complete) return;
            RewardFixer();
        }

        private void RewardFixer()
        {
            Map map = PlayerMap();
            if (map != null)
            {
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = fixerReward;
                GenPlace.TryPlaceThing(silver, CellFinder.RandomCell(map), map, ThingPlaceMode.Near);
            }
            Find.LetterStack.ReceiveLetter("FIXER // PAID", fixerTitle + " completed. Payment delivered.", LetterDefOf.PositiveEvent);
            activeFixerType = -1;
            fixerProgress = fixerGoal = fixerReward = fixerExpiresTick = 0;
            fixerTitle = "";
        }

        private void FailFixer()
        {
            Find.LetterStack.ReceiveLetter("FIXER // BURNED", fixerTitle + " failed. Your contact will not answer for a while.", LetterDefOf.NegativeEvent);
            DigitalHeat = Mathf.Min(100f, DigitalHeat + 10f);
            activeFixerType = -1;
            fixerProgress = fixerGoal = fixerReward = fixerExpiresTick = 0;
            fixerTitle = "";
        }

        public void SellCyberdata(Thing thing)
        {
            if (thing == null || thing.Destroyed) return;
            int reward = thing.stackCount * 70;
            Map map = thing.MapHeld ?? Find.CurrentMap;
            IntVec3 pos = thing.PositionHeld;
            int count = thing.stackCount;
            thing.Destroy();
            if (map != null)
            {
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = reward;
                GenPlace.TryPlaceThing(silver, pos.IsValid ? pos : CellFinder.RandomCell(map), map, ThingPlaceMode.Near);
            }
            if (activeFixerType == 0)
            {
                fixerProgress += count;
                TryCompleteFixer();
            }
        }

        private int CountAndConsumeCyberdata(int amount)
        {
            int left = amount;
            Map map = PlayerMap();
            ThingDef cyberdataDef = DefDatabase<ThingDef>.GetNamedSilentFail("BI_Cyberdata");
            if (map != null && cyberdataDef != null)
            {
                foreach (Thing t in map.listerThings.ThingsOfDef(cyberdataDef).ToList())
                {
                    if (left <= 0) break;
                    int take = Mathf.Min(left, t.stackCount);
                    t.stackCount -= take;
                    left -= take;
                    if (t.stackCount <= 0) t.Destroy();
                }
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (left <= 0) break;
                    foreach (Thing t in pawn.inventory.innerContainer.ToList())
                    {
                        if (t.def.defName != "BI_Cyberdata") continue;
                        int take = Mathf.Min(left, t.stackCount);
                        t.stackCount -= take;
                        left -= take;
                        if (t.stackCount <= 0) t.Destroy();
                        if (left <= 0) break;
                    }
                }
            }
            return amount - left;
        }

        private void ScrubTrace()
        {
            int consumed = CountAndConsumeCyberdata(2);
            if (consumed < 2)
            {
                Messages.Message("You need 2 cyberdata fragments to scrub the trace.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            DigitalHeat = Mathf.Max(0f, DigitalHeat - 20f);
            Messages.Message("Digital trace scrubbed. Corporate systems lost 20 points of certainty.", MessageTypeDefOf.PositiveEvent, false);
        }

        public void EstablishAIContact(Thing fragment)
        {
            if (fragment == null || fragment.Destroyed) return;
            string[] names = { "Ghost", "Marlowe", "Case", "Neuromancer", "Switchback", "Moth", "Sable" };
            string aiName = names[Rand.Range(0, names.Length)] + "-" + Rand.Range(10, 99);
            int type = Rand.RangeInclusive(0, 3);
            int idx = aiNames.IndexOf(aiName);
            if (idx < 0)
            {
                aiNames.Add(aiName);
                aiTypes.Add(type);
                aiLevels.Add(1);
            }
            fragment.Destroy();
            string role = type switch
            {
                0 => "Broker: improves your access to the black market and occasionally sends money.",
                1 => "Ghost: specializes in anonymity and can reduce digital heat.",
                2 => "Muse: provides neural insights and boosts a colonist temporarily.",
                _ => "Predator: can launch dangerous counter-intrusions in exchange for power."
            };
            Find.LetterStack.ReceiveLetter("AI CONTACT ESTABLISHED", aiName + " is now reachable through your net terminal.\n\n" + role, LetterDefOf.PositiveEvent);
        }

        public void SellAIFragment(Thing fragment)
        {
            if (fragment == null || fragment.Destroyed) return;
            Map map = fragment.MapHeld ?? Find.CurrentMap;
            IntVec3 pos = fragment.PositionHeld;
            fragment.Destroy();
            if (map != null)
            {
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = 1500;
                GenPlace.TryPlaceThing(silver, pos.IsValid ? pos : CellFinder.RandomCell(map), map, ThingPlaceMode.Near);
            }
        }

        private void ConsultAI()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            for (int i = 0; i < aiNames.Count; i++)
            {
                int index = i;
                options.Add(new FloatMenuOption(aiNames[i], () => UseAI(index)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void UseAI(int index)
        {
            if (index < 0 || index >= aiNames.Count) return;
            int type = aiTypes[index];
            aiLevels[index] = Math.Min(10, aiLevels[index] + 1);
            switch (type)
            {
                case 0:
                    Map map = PlayerMap();
                    if (map != null)
                    {
                        Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                        silver.stackCount = 250 + 50 * aiLevels[index];
                        GenPlace.TryPlaceThing(silver, CellFinder.RandomCell(map), map, ThingPlaceMode.Near);
                    }
                    Messages.Message(aiNames[index] + " routed a payment through three shell accounts.", MessageTypeDefOf.PositiveEvent, false);
                    break;
                case 1:
                    DigitalHeat = Mathf.Max(0f, DigitalHeat - 12f);
                    Messages.Message(aiNames[index] + " erased a chunk of your trace.", MessageTypeDefOf.PositiveEvent, false);
                    break;
                case 2:
                    Pawn pawn = PlayerMap()?.mapPawns.FreeColonistsSpawned.Where(p => !p.Dead).RandomElementWithFallback(null);
                    HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("BI_BrainDanceEuphoria");
                    if (pawn != null && def != null) pawn.health.AddHediff(def);
                    Messages.Message(aiNames[index] + " uploaded a neural heuristic.", MessageTypeDefOf.PositiveEvent, false);
                    break;
                default:
                    DigitalHeat = Mathf.Min(100f, DigitalHeat + 18f);
                    LastAttackTick = Find.TickManager.TicksGame - 18000;
                    NextAttackTick = Find.TickManager.TicksGame;
                    Messages.Message(aiNames[index] + " opened a hostile route into the network.", MessageTypeDefOf.ThreatBig, false);
                    break;
            }
        }

        private void MaybeEscalateTraceEvent()
        {
            if (DigitalHeat < 30f || Rand.Value > 0.015f * BlackIceMod.Settings.NetworkAttackMultiplier) return;
            TraceEscalations++;
            if (DigitalHeat >= 75f)
                Find.LetterStack.ReceiveLetter("DIGITAL TRACE ESCALATED", "Your intrusion profile has been correlated across multiple systems. Someone has started hunting for the signal source.", LetterDefOf.ThreatSmall);
            else
                Messages.Message("A distant system noticed a familiar intrusion signature.", MessageTypeDefOf.CautionInput, false);
        }

        private void ScheduleNextAttack()
        {
            int min = DigitalHeat >= 85f ? 12000 : 24000;
            int max = DigitalHeat >= 85f ? 36000 : 70000;
            NextAttackTick = Find.TickManager.TicksGame + Rand.Range(min, max);
        }

        private Map PlayerMap() => Find.Maps.FirstOrDefault(m => m.IsPlayerHome);

        private void ExecuteNetworkAttack()
        {
            Map map = PlayerMap();
            if (map == null) return;
            LastAttackTick = Find.TickManager.TicksGame;
            int disabled = 0;
            foreach (Thing thing in map.listerThings.AllThings.Where(t => t.Spawned && t.TryGetComp<CompPowerTrader>()?.PowerOn == true).InRandomOrder().Take(12))
            {
                CompBreakdownable breakdown = thing.TryGetComp<CompBreakdownable>();
                if (breakdown != null && !breakdown.BrokenDown && Rand.Chance(0.35f))
                {
                    try
                    {
                        breakdown.DoBreakdown();
                        disabled++;
                    }
                    catch { }
                }
            }
            Pawn affected = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Dead).InRandomOrder().FirstOrDefault();
            if (affected != null && Rand.Chance(0.65f))
            {
                HediffDef noise = DefDatabase<HediffDef>.GetNamedSilentFail("BI_NeuralNoise");
                if (noise != null) affected.health.AddHediff(noise);
            }
            DigitalHeat = Mathf.Min(100f, DigitalHeat + 8f * BlackIceMod.Settings.NetworkAttackMultiplier);
            Find.LetterStack.ReceiveLetter("NETWORK ATTACK", "An external intruder has penetrated colony systems. " + disabled + " electronic systems were knocked offline and neural traffic was corrupted.", LetterDefOf.ThreatBig);
        }
    }
}
