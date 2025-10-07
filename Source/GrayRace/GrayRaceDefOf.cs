using RimWorld;
using Verse;

namespace SD.GrayRace
{
    [DefOf]
    public static class GrayRaceDefOf
    {
        // public static JobDef DoBill_GrayRace;
        public static PawnKindDef GR_colonist;

        // ThingDefs
        public static ThingDef GR_Nanites;
        public static ThingDef GR_Incubator;

        // HediffDefs
        public static HediffDef NanitesRegeneration;

        // JobDefs
        public static JobDef GR_ConsumeMetal;
        public static JobDef GR_HaulToIncubator;

        static GrayRaceDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(GrayRaceDefOf));
        }
    }
}
