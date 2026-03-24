using RimWorld;
using SD.GrayRace.Defs;
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
        public static ThingDef GR_Drydock;

        // HediffDefs
        public static HediffDef NanitesRegeneration;
        public static HediffDef GR_Overclock_Arm;
        public static HediffDef GR_Overclock_Leg;
        public static HediffDef GR_Overclock_Heart;
        public static HediffDef GR_Overclock_Brain;
        public static HediffDef GR_Overclock_Eye;
        public static HediffDef GR_Overclock_Ear;

        // JobDefs
        public static JobDef GR_ConsumeMetal;
        public static JobDef GR_HaulToIncubator;
        public static JobDef GR_HaulToDrydock;
        public static JobDef GR_InstallPluginUpgrade;

        // StatDefs
        public static StatDef GRStat_NaniteMax;
        public static StatDef GRStat_NaniteRegenRate;
        public static StatDef GRStat_OverclockMaxLevel;

        // Gray mech component sets
        public static GRMechComponentSetDef GR_MechComponentSet_PowerCore;
        public static GRMechComponentSetDef GR_MechComponentSet_Thruster;
        public static GRMechComponentSetDef GR_MechComponentSet_Sensor;
        public static GRMechComponentSetDef GR_MechComponentSet_CombatComputer;

        // Gray mech slot sizes
        public static GRMechSlotSizeDef GR_MechSlotSize_Small;
        public static GRMechSlotSizeDef GR_MechSlotSize_PointDefense;
        public static GRMechSlotSizeDef GR_MechSlotSize_Medium;
        public static GRMechSlotSizeDef GR_MechSlotSize_Large;
        public static GRMechSlotSizeDef GR_MechSlotSize_ExtraLarge;
        public static GRMechSlotSizeDef GR_MechSlotSize_Torpedo;
        public static GRMechSlotSizeDef GR_MechSlotSize_Hangar;
        public static GRMechSlotSizeDef GR_MechSlotSize_Auxiliary;
        public static GRMechSlotSizeDef GR_MechSlotSize_Titanic;

        static GrayRaceDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(GrayRaceDefOf));
        }
    }
}
