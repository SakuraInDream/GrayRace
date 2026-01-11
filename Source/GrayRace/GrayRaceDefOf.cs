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
        public static HediffDef GR_Overclock_Arm;
        public static HediffDef GR_Overclock_Leg;
        public static HediffDef GR_Overclock_Heart;
        public static HediffDef GR_Overclock_Brain;
        public static HediffDef GR_Overclock_Eye;

        // JobDefs
        public static JobDef GR_ConsumeMetal;
        public static JobDef GR_HaulToIncubator;

        // StatDefs
        public static StatDef GRStat_NaniteMax;
        public static StatDef GRStat_NaniteRegenRate;

        static GrayRaceDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(GrayRaceDefOf));
        }
    }
}
