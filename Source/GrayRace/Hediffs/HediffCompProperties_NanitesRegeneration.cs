using Verse;

namespace SD.GrayRace.Hediffs
{
    public class HediffCompProperties_NanitesRegeneration: HediffCompProperties
    {
        public float naniteCostPerSeconds = 10f;

        public float healAmountPerSeconds = 2f;

        public HediffCompProperties_NanitesRegeneration()
        {
            compClass = typeof(HediffComp_NanitesRegeneration);
            // compClass = typeof(HediffComp_NanitesRegenerationNew);
        }
    }
}
