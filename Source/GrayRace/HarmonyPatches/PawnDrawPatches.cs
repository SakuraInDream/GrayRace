using HarmonyLib;
using SD.GrayRace.Comps;
using UnityEngine;
using Verse;

namespace SD.GrayRace.HarmonyPatches;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.DynamicDrawPhaseAt))]
public static class PawnDrawPatches
{
    [HarmonyPostfix]
    public static void DynamicDrawPhaseAtPostfix(Pawn __instance, DrawPhase phase, Vector3 drawLoc)
    {
        if (phase != DrawPhase.Draw || __instance == null || !__instance.Spawned)
        {
            return;
        }

        __instance.TryGetComp<CompMultiTurretGun>()?.DrawAfterPawnRendered(drawLoc);
    }
}
