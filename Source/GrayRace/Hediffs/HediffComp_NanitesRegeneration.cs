using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace SD.GrayRace
{
    public class HediffComp_NanitesRegeneration : HediffComp
    {
        private CompResource_Nanites resNanites;

        private List<Hediff_Injury> tmpHediffInjuries = new List<Hediff_Injury>();
        
        private List<Hediff_MissingPart> tmpHediffMissingParts = new List<Hediff_MissingPart>();
        public HediffCompProperties_NanitesRegeneration Pros => (HediffCompProperties_NanitesRegeneration)props;
        private HediffSet hediffSet => Pawn.health.hediffSet;
        
        private static readonly IComparer<Hediff_Injury> injurySeverityComparer = Comparer<Hediff_Injury>.Create((a, b) => b.Severity.CompareTo(a.Severity));

        private static readonly Dictionary<BodyPartTagDef, float> partImportanceMap = new Dictionary<BodyPartTagDef, float>
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

        // 完全仿食尸鬼的高速再生，但是恢复速率可调 —— 诶，灵感菇来了
        // private void NaniteHeal(HediffSet hediffSet)
        // {
        //     var healBudget = Pros.healAmountPerSeconds;
        //     if (healBudget > 0f)
        //     {
        //         hediffSet.GetHediffs(ref tmpHediffInjuries, injury => true);
        //         foreach (var tmpHediffInjury in tmpHediffInjuries)
        //         {
        //             float healMax = Mathf.Min(healBudget, tmpHediffInjury.Severity);
        //             healBudget -= healMax;
        //             if (tmpHediffInjury.Bleeding || tmpHediffInjury.TendableNow())
        //                 tmpHediffInjury.Tended(new FloatRange(0.2f, healMax).RandomInRange * 1f, healMax * 1);
        //             tmpHediffInjury.Heal(healMax);
        //             hediffSet.Notify_Regenerated(Pros.naniteCostPerSeconds);
        //             if (healBudget < 0f)
        //                 break;
        //         }
        //
        //         if (healBudget > 0f)
        //         {
        //             hediffSet.GetHediffs(
        //                 ref tmpHediffMissingParts,
        //                 h =>
        //                     h.Part.parent != null &&
        //                     !tmpHediffInjuries.Any(x => x.Part == h.Part.parent) &&
        //                     hediffSet.GetFirstHediffMatchingPart<Hediff_MissingPart>(h.Part.parent) == null
        //                     && hediffSet.GetFirstHediffMatchingPart<Hediff_AddedPart>(h.Part.parent) == null
        //             );
        //             var missingPart = tmpHediffMissingParts.FirstOrDefault(h => true);
        //             if (missingPart != null)
        //             {
        //                 if (missingPart.Bleeding || missingPart.TendableNow())
        //                     missingPart.Tended(new FloatRange(0.2f, 1f).RandomInRange * 1f, 1f, 1);
        //                 BodyPartRecord part = missingPart.Part;
        //                 Pawn.health.RemoveHediff(missingPart);
        //
        //                 Hediff regenHediff = Pawn.health.AddHediff(HediffDefOf.Misc, part);
        //                 float parthealth = hediffSet.GetPartHealth(part);
        //
        //                 regenHediff.Severity = Mathf.Max(parthealth - 1f, parthealth * 0.9f);
        //                 hediffSet.Notify_Regenerated(parthealth - regenHediff.Severity);
        //             }
        //         }
        //     }
        // }

        private void NaniteHeal_NewTemp()
        {
            var resourceFraction = Mathf.Clamp01(resNanites.CurResource / resNanites.Max);
            
            // 可用资源影响治疗速度 当见底时 直接不治疗，充盈时全速治疗
            var speedMultiplier = Mathf.Lerp(0f, 1f, resourceFraction);
            
            // 每次治疗的最大纳米机械消耗量
            var maxConsumableNanites = Pros.naniteCostPerSeconds * speedMultiplier * 100f;
            
            var availableNanites = Mathf.Min(resNanites.ValueForDisplay, maxConsumableNanites);
            
            if (availableNanites <= 0f) return;
            
            // 评估伤势和缺失部件的紧急程度
            var (injuryUrgency, missingUrgency) = EvaluateMedicalUrgency();
            // Log.Message($"Urgency Weight => Injury={injuryUrgency:F2}, Missing={missingUrgency:F2}");
            // 根据紧急程度计算资源分配权重
            var (weightInjury, weightMissing) = CalculateAllocationWeights(injuryUrgency, missingUrgency);
            
            // 计算总权重
            var totalWeight = weightInjury + weightMissing;
            
            if (totalWeight <= 0f) return;
            
            // 按权重分配纳米机械资源
            var allocatedForInjuries = availableNanites * (weightInjury /  totalWeight);
            var allocatedForMissing = availableNanites * (weightMissing / totalWeight);
            
            // Log.Message($"Allocation => Injuries={allocatedForInjuries:F2}, Missing={allocatedForMissing:F2}");
            if (allocatedForInjuries > 0)
            {
                var remainingInjuryAllocation = ProcessInjuryHealing(allocatedForInjuries);
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
            tmpHediffInjuries.Sort(injurySeverityComparer);

            var remainingAllocation = allocatedNanites;

            foreach (var injury in tmpHediffInjuries)
            {
                if (remainingAllocation <= 0f) break;

                float maxHealable = Mathf.Min(injury.Severity, Pros.healAmountPerSeconds);

                if (maxHealable <= 0f) continue;

                var cost = maxHealable / Pros.healAmountPerSeconds * Pros.naniteCostPerSeconds;
                
                if (GRUtils.TryConsumeNanites(Pawn, cost))
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

            foreach (var missingPart in tmpHediffMissingParts)
            {
                if(remainingAllocation <= 0f) break;
                
                BodyPartRecord part = missingPart.Part;

                var partImportance = GetBodyPartImportance(part);
                
                var nanitesPerHP = Pros.naniteCostPerSeconds * partImportance / Pros.healAmountPerSeconds;
                
                var partMaxHealth = part.def.GetMaxHealth(Pawn);
                
                // var maxRestorable = Mathf.Min(remainingAllocation / nanitesPerHP, partMaxHealth * 0.1f);
                var maxRestorable = Mathf.Min(nanitesPerHP * partMaxHealth, Pros.healAmountPerSeconds);
                
                // Log.Message($"maxRestorable: {maxRestorable} partMaxHealth: {partMaxHealth}");
                if (maxRestorable <= 0f) continue;
                
                var cost = maxRestorable * nanitesPerHP;
                
                // Log.Message($"maxRestorable: {maxRestorable} nanitesPerHP: {nanitesPerHP} cost:{cost:F2}");

                if(missingPart.Bleeding || missingPart.TendableNow())
                    missingPart.Tended(new FloatRange(0.5f, 1f).RandomInRange, 1f);
                
                if (Pawn.health.hediffSet.HasNaturallyHealingInjury()) continue;
                if (GRUtils.TryConsumeNanites(Pawn, cost))
                {
                    Pawn.health.RemoveHediff(missingPart);
                    var partHealth = hediffSet.GetPartHealth(part);
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
            foreach (var injury in tmpHediffInjuries)
            {
                totalInjurySeverity += injury.Severity;
                if (injury.Severity > 0.7f || injury.Bleeding) // 严重伤势
                    criticalInjuries += injury.Severity;
            }
    
            // 评估缺失部件紧急程度
            foreach (var missing in tmpHediffMissingParts)
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
                if (partImportanceMap.TryGetValue(tag, out var value))
                {
                    return value;
                }
            }

            return 1f;
        }
        
        private void RefreshTmpHediffLists()
        {
            tmpHediffInjuries.Clear();
            tmpHediffMissingParts.Clear();
            
            hediffSet.GetHediffs(ref tmpHediffInjuries, h => true);
            
            hediffSet.GetHediffs(
                ref tmpHediffMissingParts,
                h =>
                    h.Part.parent != null &&
                    // !tmpHediffInjuries.Any(x => x.Part == h.Part.parent) &&
                    hediffSet.GetFirstHediffMatchingPart<Hediff_MissingPart>(h.Part.parent) == null
                    && hediffSet.GetFirstHediffMatchingPart<Hediff_AddedPart>(h.Part.parent) == null
            );
        }

        public override void CompPostMake()
        {
            base.CompPostMake();
            resNanites = Pawn?.TryGetComp<CompResource_Nanites>();
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            resNanites ??= Pawn.TryGetComp<CompResource_Nanites>();

            if (resNanites.CurResource < Pros.naniteCostPerSeconds) return;
            
            Log.Message($"InCompTick");
            // 每 300tick 重建一次伤口和断肢列表
            if (Pawn.IsHashIntervalTick(300))
            {
                Log.Message("HashIntervalTick 300");
                RefreshTmpHediffLists();
            }

            if (Pawn.IsHashIntervalTick(600, delta))
            { 
                Log.Message("HashIntervalTick 600");
                NaniteHeal_NewTemp();
            };
            
        }
        
        public override string CompDebugString()
        {
            return $"Injuries: {tmpHediffInjuries.Count}, MissingParts: {tmpHediffMissingParts.Count}";
        }
    }
}
