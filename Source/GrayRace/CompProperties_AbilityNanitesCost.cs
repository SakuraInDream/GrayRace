using RimWorld;
using Verse;

namespace SD.GrayRace
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