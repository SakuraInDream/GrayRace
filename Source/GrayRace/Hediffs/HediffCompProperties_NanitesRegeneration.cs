using Verse;

namespace SD.GrayRace.Hediffs
{
    public class HediffCompProperties_NanitesRegeneration: HediffCompProperties
    {
        public float naniteCostPerSeconds = 0.01f;

        public float healAmountPerSeconds = 50f;

        public HediffCompProperties_NanitesRegeneration()
        {
            compClass = typeof(HediffComp_NanitesRegeneration);
        }
    }
}
