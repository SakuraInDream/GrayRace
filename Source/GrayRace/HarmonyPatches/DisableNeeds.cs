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
public static class Patch_Pawn_NeedsTracker
{
    [HarmonyPatch("ShouldHaveNeed")]
    [HarmonyPostfix]
    public static void Postfix(Pawn_NeedsTracker __instance, Pawn ___pawn, NeedDef nd, ref bool __result)
    {
        if (!__result) return;

        DisableNeeds extension = ___pawn.def.GetModExtension<DisableNeeds>();

        if (extension != null && extension.disabledNeeds.Contains(nd))
        {
            __result = false;
        }
    }
}
