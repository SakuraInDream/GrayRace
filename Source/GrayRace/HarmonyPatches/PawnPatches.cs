using HarmonyLib;
using SD.GrayRace.Comps;
using Verse;

namespace SD.GrayRace.HarmonyPatches;

public static class PawnPatches
{
    public static void TryGetAttackVerbPostfix(Pawn __instance, Thing target, ref Verb __result)
    {
        if (__instance == null)
        {
            return;
        }

        if (__result != null && !__result.verbProps.IsMeleeAttack)
        {
            return;
        }

        CompGrayMechTurretBank turretBank = __instance.TryGetComp<CompGrayMechTurretBank>();
        if (turretBank != null && turretBank.TryGetTacticalVerb(target, out Verb turretVerb))
        {
            __result = turretVerb;
        }
    }
}
