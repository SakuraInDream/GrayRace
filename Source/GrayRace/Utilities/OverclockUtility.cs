using RimWorld;
using SD.GrayRace.Hediffs;
using Verse;

namespace SD.GrayRace.Utilities
{
    public static class OverclockUtility
    {
        public static bool IsOverclockable(BodyPartRecord part)
        {
            return IsArm(part) || IsLeg(part) || IsEye(part) || IsCorePart(part);
        }

        public static bool IsCorePart(BodyPartRecord part) => IsBrain(part) || IsHeart(part);

        // 简单的 Tag 判断逻辑 (实际请根据你的 BodyDef XML 调整)
        public static bool IsArm(BodyPartRecord part) => part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore);
        public static bool IsLeg(BodyPartRecord part) => part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore);
        public static bool IsEye(BodyPartRecord part) => part.def.tags.Contains(BodyPartTagDefOf.SightSource);
        public static bool IsBrain(BodyPartRecord part) => part.def.tags.Contains(BodyPartTagDefOf.ConsciousnessSource);
        public static bool IsHeart(BodyPartRecord part) => part.def.tags.Contains(BodyPartTagDefOf.BloodPumpingSource);

        public static HediffDef GetHediffDefForPart(BodyPartRecord part)
        {

            if (IsArm(part)) return GrayRaceDefOf.GR_Overclock_Arm;
            if (IsLeg(part)) return GrayRaceDefOf.GR_Overclock_Leg;
            if (IsEye(part)) return GrayRaceDefOf.GR_Overclock_Eye;
            if (IsBrain(part)) return GrayRaceDefOf.GR_Overclock_Brain;
            if (IsHeart(part)) return GrayRaceDefOf.GR_Overclock_Heart;

            return null;
        }
        public static string GetAffectedStatLabel(BodyPartRecord part)
        {
            if (IsArm(part))
            {
                return PawnCapacityDefOf.Manipulation.GetLabelFor(); // 操作
            }

            if (IsLeg(part))
            {
                return PawnCapacityDefOf.Moving.GetLabelFor(); // 移动
            }

            if (IsEye(part))
            {
                return PawnCapacityDefOf.Sight.GetLabelFor(); // 视觉
            }

            if (IsBrain(part))
            {
                return PawnCapacityDefOf.Consciousness.GetLabelFor(); // 意识
            }

            if (IsHeart(part))
            {
                return PawnCapacityDefOf.BloodPumping.GetLabelFor(); // 血液循环
            }

            return "Unknown".Translate();
        }

        // 计算预览数值
        public static float GetStatBonus(BodyPartRecord part, float level)
        {
            HediffDef def = GetHediffDefForPart(part);

            if (def is HediffDef_Overclock ovhediffdef)
            {
                if (ovhediffdef.capacityToBoost != null)
                {
                    return ovhediffdef.capacityRange.LerpThroughRange(level);
                }

                return ovhediffdef.efficiencyRange.LerpThroughRange(level);
            }

            return 0f;
        }

        public static float GetEnergyCost(BodyPartRecord part, float level)
        {
            HediffDef def = GetHediffDefForPart(part);
            if (def is HediffDef_Overclock ovDef)
            {
                float costFactor = ovDef.baseEnergyDrain * level;

                if (IsCorePart(part)) costFactor *= 2f;

                return costFactor;
            }
            return 0f;
        }
    }
}
