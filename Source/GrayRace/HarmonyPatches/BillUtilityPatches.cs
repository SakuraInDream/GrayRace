using RimWorld;
using SD.GrayRace.Bills;
using SD.GrayRace.DefModExtensions;
using Verse;

namespace SD.GrayRace.HarmonyPatches;

public static class BillUtilityPatches
{
    public static void MakeNewBillPostfix(RecipeDef recipe, Precept_ThingStyle precept, ref Bill __result)
    {
        if (recipe?.GetModExtension<DefModExtension_MechAssemblyRecipe>() == null)
        {
            return;
        }

        __result = new Bill_GrayMechAssembly(recipe, precept);
    }
}
