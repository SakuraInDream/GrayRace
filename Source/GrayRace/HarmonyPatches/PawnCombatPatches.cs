using HarmonyLib;
using RimWorld;
using SD.GrayRace.Comps;
using Verse;

namespace SD.GrayRace.HarmonyPatches;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.TryGetAttackVerb))]
public static class PawnCombatPatches
{
    [HarmonyPostfix]
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

        Verb turretVerb = __instance.TryGetComp<CompGrayMechSystems>()?.TurretBankSystem?.ResolveAttackVerbForVanilla(target);
        if (turretVerb != null)
        {
            __result = turretVerb;
        }
    }
}
