using RimWorld;

namespace SD.GrayRace.Comps
{
    public class CompProperties_AbilityNanitesCost: CompProperties_AbilityEffect
    {
        public float nanitesCost;
        public bool consumeProportionally = false;

        public CompProperties_AbilityNanitesCost()
        {
            compClass = typeof(CompAbilityEffect_NanitesCost);
        }
    }
}
