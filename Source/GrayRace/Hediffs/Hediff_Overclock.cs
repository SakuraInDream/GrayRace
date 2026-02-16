using System.Collections.Generic;
using Verse;

namespace SD.GrayRace.Hediffs
{
    public class Hediff_Overclock: HediffWithComps
    {
        private HediffStage _cachedStage;

        public HediffDef_Overclock Def => (HediffDef_Overclock)def;

        public override HediffStage CurStage
        {
            get
            {
                _cachedStage ??= new HediffStage();
                _cachedStage.partEfficiencyOffset = Def.EvaluateEfficiencyAtLevel(Severity);
                if (Def.capacityToBoost == null)
                {
                    return _cachedStage;
                }

                _cachedStage.capMods ??= new List<PawnCapacityModifier> { new() { capacity = Def.capacityToBoost } };
                if (_cachedStage.capMods.Count <= 0)
                {
                    PawnCapacityModifier modifier = new() { capacity = Def.capacityToBoost};
                    _cachedStage.capMods.Add(modifier);
                }
                _cachedStage.capMods[0].offset = Def.EvaluateCapacityAtLevel(Severity);

                return _cachedStage;
            }
        }
        public override float Severity
        {
            get => base.Severity;
            set => base.Severity = value;
        }
    }
}
