using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Hediffs
{
    public class HediffDef_Overclock: HediffDef
    {
        public FloatRange efficiencyRange = FloatRange.Zero;

        public FloatRange capacityRange = FloatRange.Zero;
        public PawnCapacityDef capacityToBoost;

        public float baseEnergyDrain = 1f;

        public float EvaluateEfficiencyAtLevel(float level)
        {
            return EvaluateRangeUnclamped(efficiencyRange, level);
        }

        public float EvaluateCapacityAtLevel(float level)
        {
            return EvaluateRangeUnclamped(capacityRange, level);
        }

        public HediffDef_Overclock()
        {
            hediffClass = typeof(Hediff_Overclock);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string configError in base.ConfigErrors())
            {
                yield return configError;
            }

            if (capacityToBoost != null && efficiencyRange != FloatRange.Zero)
            {
                yield return "HediffDef_Overclock: specify efficiencyRange value but capacityToBoost is not null";
            }

        }

        private static float EvaluateRangeUnclamped(FloatRange range, float level)
        {
            float nonNegativeLevel = Mathf.Max(0f, level);
            return range.min + (range.max - range.min) * nonNegativeLevel;
        }
    }
}
