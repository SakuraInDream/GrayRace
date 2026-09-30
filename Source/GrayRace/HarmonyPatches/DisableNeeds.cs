using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace SD.GrayRace.HarmonyPatches;

public class DisableNeeds : DefModExtension
{
    public List<NeedDef> disabledNeeds = null!;
}

[HarmonyPatch(typeof(Pawn_NeedsTracker))]
internal static class Patch_Pawn_NeedsTracker
{
    private static FieldInfo fi_pawn = AccessTools.Field(typeof(Pawn_NeedsTracker), "pawn");

    [HarmonyPatch("ShouldHaveNeed")]
    [HarmonyPostfix]
    public static void Postfix(Pawn_NeedsTracker __instance, NeedDef nd, ref bool __result)
    {
        if (!__result) return;
        Pawn pawn = (Pawn)fi_pawn.GetValue(__instance);
        var extension = pawn.def.GetModExtension<DisableNeeds>();
        if (extension != null && extension.disabledNeeds.Contains(nd))
        {
            __result = false;
        }
    }
}
