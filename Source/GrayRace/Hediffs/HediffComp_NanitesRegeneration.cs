using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Comps;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Hediffs
{
    // 待重构：原来的再生逻辑是直接对资源进行消耗，消耗量为 properties 里的内容，新的 CompResource 使用 StatDef 控制回复速率
    public class HediffComp_NanitesRegeneration : HediffComp
    {
        // private CompResource_Nanites _resNanites;
        private CompResource_NanitesNew _resNanites;

        private List<Hediff_Injury> _tmpHediffInjuries = new List<Hediff_Injury>();
        private List<Hediff_MissingPart> _tmpHediffMissingParts = new List<Hediff_MissingPart>();

        public HediffCompProperties_NanitesRegeneration Props => (HediffCompProperties_NanitesRegeneration)props;
        private HediffSet HediffSet => Pawn.health.hediffSet;

        private static readonly IComparer<Hediff_Injury> s_injurySeverityComparer = Comparer<Hediff_Injury>.Create((a, b) => b.Severity.CompareTo(a.Severity));

        private static readonly Dictionary<BodyPartTagDef, float> s_partImportanceMap = new Dictionary<BodyPartTagDef, float>
        {
            { BodyPartTagDefOf.BloodFiltrationSource, 2.0f },
            { BodyPartTagDefOf.BloodFiltrationLiver, 1.9f },
            { BodyPartTagDefOf.BloodFiltrationKidney, 1.8f },
            { BodyPartTagDefOf.BloodPumpingSource, 1.5f },
            { BodyPartTagDefOf.BreathingSource, 1.5f },
            { BodyPartTagDefOf.BreathingPathway, 1.9f },
            { BodyPartTagDefOf.ConsciousnessSource, 2.0f },
            { BodyPartTagDefOf.MovingLimbCore, 1.8f },
            { BodyPartTagDefOf.Spine, 1.7f }
        };

        private void NaniteHeal_NewTemp()
        {
            float resourceFraction = Mathf.Clamp01(_resNanites.CurrentNanites / _resNanites.Max);
            float speedMultiplier = Mathf.Lerp(0.1f, 1f, resourceFraction);

            // 每次治疗的最大纳米机械消耗量
            float maxConsumableNanites = Props.naniteCostPerSeconds * speedMultiplier * 10f;
            float availableNanites = Mathf.Min(_resNanites.CurrentNanites, maxConsumableNanites);

            if (availableNanites <= 0.1f) return;

            // 评估伤势和缺失部件的紧急程度
            (float injuryUrgency, float missingUrgency) = EvaluateMedicalUrgency();
            // Log.Message($"Urgency Weight => Injury={injuryUrgency:F2}, Missing={missingUrgency:F2}");
            // 根据紧急程度计算资源分配权重
            (float weightInjury, float weightMissing) = CalculateAllocationWeights(injuryUrgency, missingUrgency);

            // 计算总权重
            float totalWeight = weightInjury + weightMissing;

            if (totalWeight <= 0f) return;

            // 按权重分配纳米机械资源
            float allocatedForInjuries = availableNanites * (weightInjury /  totalWeight);
            float allocatedForMissing = availableNanites * (weightMissing / totalWeight);

            // Log.Message($"Allocation => Injuries={allocatedForInjuries:F2}, Missing={allocatedForMissing:F2}");
            if (allocatedForInjuries > 0)
            {
                float remainingInjuryAllocation = ProcessInjuryHealing(allocatedForInjuries);
                // 没用完的资源就给断肢再生
                // Log.Message($"Unused {remainingInjuryAllocation:F2} nanites from injuries, reallocated to missing parts");
                allocatedForMissing += remainingInjuryAllocation;
            }

            // Log.Message($"Allocation After InjueryHealing => Missing={allocatedForMissing:F2}");
            if (allocatedForMissing > 0 && Pawn.IsHashIntervalTick(600))
            {
                ProcessMissingPartRepair(allocatedForMissing);
            }
        }

        // 逻辑分离1 伤口修复
        private float ProcessInjuryHealing(float allocatedNanites)
        {
            _tmpHediffInjuries.Sort(s_injurySeverityComparer);

            var remainingAllocation = allocatedNanites;

            foreach (var injury in _tmpHediffInjuries)
            {
                if (remainingAllocation <= 0f) break;

                float maxHealable = Mathf.Min(injury.Severity, Props.healAmountPerSeconds);

                if (maxHealable <= 0f) continue;

                var cost = maxHealable / Props.healAmountPerSeconds * Props.naniteCostPerSeconds;
                if (GrayRaceUtilities.TryConsumeNanites(Pawn, cost))
                {
                    if(injury.Bleeding || injury.TendableNow())
                        injury.Tended(new  FloatRange(0.5f, 1f).RandomInRange, 1f);

                    injury.Heal(maxHealable);
                    remainingAllocation -= cost;
                }
            }

            return remainingAllocation;
        }

        // 逻辑分离2 断肢再生
        private void ProcessMissingPartRepair(float allocatedNanites)
        {
            var remainingAllocation = allocatedNanites;

            // tmpHediffMissingParts.SortByDescending(h => GetBodyPartImportance(h.Part));

            // Log.Message($"MissingPartRepair => remainingAllocation={remainingAllocation:F2} count={tmpHediffMissingParts.Count}");

            foreach (var missingPart in _tmpHediffMissingParts)
            {
                if(remainingAllocation <= 0f) break;

                BodyPartRecord part = missingPart.Part;

                var partImportance = GetBodyPartImportance(part);

                var nanitesPerHP = Props.naniteCostPerSeconds * partImportance / Props.healAmountPerSeconds;

                var partMaxHealth = part.def.GetMaxHealth(Pawn);

                // var maxRestorable = Mathf.Min(remainingAllocation / nanitesPerHP, partMaxHealth * 0.1f);
                var maxRestorable = Mathf.Min(nanitesPerHP * partMaxHealth, Props.healAmountPerSeconds);

                // Log.Message($"maxRestorable: {maxRestorable} partMaxHealth: {partMaxHealth}");
                if (maxRestorable <= 0f) continue;

                var cost = maxRestorable * nanitesPerHP;

                // Log.Message($"maxRestorable: {maxRestorable} nanitesPerHP: {nanitesPerHP} cost:{cost:F2}");

                if(missingPart.Bleeding || missingPart.TendableNow())
                    missingPart.Tended(new FloatRange(0.5f, 1f).RandomInRange, 1f);

                if (Pawn.health.hediffSet.HasNaturallyHealingInjury()) continue;
                if (GrayRaceUtilities.TryConsumeNanites(Pawn, cost))
                {
                    Pawn.health.RemoveHediff(missingPart);
                    var partHealth = HediffSet.GetPartHealth(part);
                    var regenHediff = Pawn.health.AddHediff(HediffDefOf.Misc, part);
                    regenHediff.Severity = Mathf.Max(partHealth - 1f, partHealth * 0.9f);

                    remainingAllocation -= cost;
                }
            }
        }
        // 评估伤势和缺失部件的紧急程度
        private (float injuryUrgency, float missingUrgency) EvaluateMedicalUrgency()
        {
            float totalInjurySeverity = 0f;
            float criticalInjuries = 0f;
            float totalMissingImpact = 0f;

            // 评估伤势紧急程度
            foreach (var injury in _tmpHediffInjuries)
            {
                totalInjurySeverity += injury.Severity;
                if (injury.Severity > 0.7f || injury.Bleeding) // 严重伤势
                    criticalInjuries += injury.Severity;
            }

            // 评估缺失部件紧急程度
            foreach (var missing in _tmpHediffMissingParts)
            {
                var partImportance = GetBodyPartImportance(missing.Part);
                if(missing.Bleeding)
                    totalMissingImpact += missing.Severity;
                totalMissingImpact += partImportance;
            }

            // 计算紧急程度评分
            float injuryUrgency = totalInjurySeverity * 0.7f + criticalInjuries * 0.3f;
            float missingUrgency = totalMissingImpact * 1.2f; // 缺失部件通常更紧急

            return (injuryUrgency, missingUrgency);
        }
        // 根据紧急程度计算资源分配权重
        private (float weightInjury, float weightMissing) CalculateAllocationWeights(float injuryUrgency, float missingUrgency)
        {
            if (injuryUrgency > 5.0f)
                return (0.9f, 0.1f);

            if (missingUrgency > 4.0f)
                return (0.2f, 0.8f);

            float weightInjury = Mathf.Max(0.1f, injuryUrgency);
            float weightMissing = Mathf.Max(0.1f, missingUrgency * 1.5f);
            return (weightInjury, weightMissing);
        }

        // 优先保活，再保移动能力 根据后续器官随时更改 参考 BodyPartTagDef
        private float GetBodyPartImportance(BodyPartRecord part)
        {
            if (part == null) return 0f;
            foreach (var tag in part.def.tags)
            {
                if (s_partImportanceMap.TryGetValue(tag, out var value))
                {
                    return value;
                }
            }

            return 1f;
        }

        private void RefreshTmpHediffLists()
        {
            _tmpHediffInjuries.Clear();
            _tmpHediffMissingParts.Clear();

            HediffSet.GetHediffs(ref _tmpHediffInjuries, h => true);

            HediffSet.GetHediffs(
                ref _tmpHediffMissingParts,
                h =>
                    h.Part.parent != null &&
                    // !tmpHediffInjuries.Any(x => x.Part == h.Part.parent) &&
                    HediffSet.GetFirstHediffMatchingPart<Hediff_MissingPart>(h.Part.parent) == null
                    && HediffSet.GetFirstHediffMatchingPart<Hediff_AddedPart>(h.Part.parent) == null
            );
        }

        public override void CompPostMake()
        {
            base.CompPostMake();
            _resNanites = Pawn?.TryGetComp<CompResource_NanitesNew>();
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            if (Pawn.Dead) return;

            _resNanites ??= Pawn.TryGetComp<CompResource_NanitesNew>();

            if (_resNanites is null || _resNanites.CurrentNanites < 0.9) return;

            // Log.Message($"InCompTick");
            // 每 300tick 重建一次伤口和断肢列表
            if (Pawn.IsHashIntervalTick(300))
            {
                // Log.Message("HashIntervalTick 300");
                RefreshTmpHediffLists();
            }

            if (Pawn.IsHashIntervalTick(600, delta))
            {
                // Log.Message("HashIntervalTick 600");
                NaniteHeal_NewTemp();
            };

        }

        public override string CompDebugString()
        {
            return $"Injuries: {_tmpHediffInjuries.Count}, MissingParts: {_tmpHediffMissingParts.Count}";
        }
    }
}
