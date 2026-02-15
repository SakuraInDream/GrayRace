using System.Collections.Generic;
using SD.GrayRace.Hediffs;
using SD.GrayRace.Utilities;
using Verse;

namespace SD.GrayRace.Comps
{
    public class CompOverclock: ThingComp
    {
        private Dictionary<BodyPartRecord, float> _overclockData = new Dictionary<BodyPartRecord, float>();

        public CompProperties_Overclock Props => (CompProperties_Overclock)props;
        public Pawn Pawn => (Pawn)parent;

        // 缓存总能量消耗倍率，避免每帧计算
        public float CachedEnergyConsumptionFactor { get; private set; } = 0f;

        public override void PostExposeData()
        {
            base.PostExposeData();
            // 序列化字典需要特殊处理
            Scribe_Collections.Look(ref _overclockData, "overclockData", LookMode.BodyPart, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _overclockData ??= new Dictionary<BodyPartRecord, float>();
                RecalculateTotalDrain();
            }
        }

        // 获取当前某部位的超频值
        public float GetOverclockLevel(BodyPartRecord part)
        {
            return _overclockData.TryGetValue(part, out float val) ? val : 0f;
        }

        // 设置超频值并应用效果
        public void SetOverclockLevel(BodyPartRecord part, float level)
        {
            if (level <= 0.001f)
            {
                if (_overclockData.Remove(part))
                {
                    RemoveOverclockHediff(part);
                }
            }
            else
            {
                _overclockData[part] = level;
                ApplyOverclockHediff(part, level);
            }
            RecalculateTotalDrain();
        }
        // 应用/更新 Hediff
        private void ApplyOverclockHediff(BodyPartRecord part, float level)
        {
            // 根据部位类型获取对应的 HediffDef
            HediffDef def = GetHediffDefForPart(part);
            if (def == null) return;

            // 查找现有的
            Hediff hediff = Pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.Part == part && h.def == def);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(def, Pawn, part);
                Pawn.health.AddHediff(hediff);
            }
            // 设定严重度 = 超频等级 (0.0 - 1.0)
            // XML 中需要配置 Hediff 的 stages 来根据 severity 给予属性加成
            hediff.Severity = level;
        }

        private void RemoveOverclockHediff(BodyPartRecord part)
        {
            HediffDef def = GetHediffDefForPart(part);
            if (def == null) return;
            Hediff hediff = Pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.Part == part && h.def == def);
            if (hediff != null)
            {
                Pawn.health.RemoveHediff(hediff);
            }
        }

        private void RecalculateTotalDrain()
        {
            float totalFactor = 0f;
            var hediffs = Pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Overclock ovHediff)
                {
                    float factor = ovHediff.Def.baseEnergyDrain * ovHediff.Severity;

                    // 核心部位双倍消耗
                    if (OverclockUtility.IsCorePart(ovHediff.Part))
                    {
                        factor *= 2f;
                    }

                    totalFactor += factor;
                }
            }

            CachedEnergyConsumptionFactor = totalFactor;
        }

        // 简单的映射逻辑，实际建议用配置类
        public static HediffDef GetHediffDefForPart(BodyPartRecord part)
        {
            if (OverclockUtility.IsArm(part)) return GrayRaceDefOf.GR_Overclock_Arm;
            if (OverclockUtility.IsLeg(part)) return GrayRaceDefOf.GR_Overclock_Leg;
            if (OverclockUtility.IsEye(part)) return GrayRaceDefOf.GR_Overclock_Eye;
            if (OverclockUtility.IsBrain(part)) return GrayRaceDefOf.GR_Overclock_Brain;
            if (OverclockUtility.IsHeart(part)) return GrayRaceDefOf.GR_Overclock_Heart;
            return null;
        }
    }
}
