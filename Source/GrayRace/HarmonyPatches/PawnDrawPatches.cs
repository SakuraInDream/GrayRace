using HarmonyLib;
using SD.GrayRace.Comps;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;

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

        __instance.TryGetComp<CompGrayMechSystems>()?.TurretBankSystem?.DrawAfterPawnRendered(drawLoc);
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.DrawExtraSelectionOverlays))]
public static class PawnSelectionOverlayPatches
{
    [HarmonyPostfix]
    public static void DrawExtraSelectionOverlaysPostfix(Pawn __instance)
    {
        __instance.TryGetComp<CompGrayMechSystems>()?.TurretBankSystem?.PostDrawExtraSelectionOverlays();
    }
}

[HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
public static class VerbLaunchProjectilePatches
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> TryCastShotTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo launchWithOrigin = AccessTools.Method(typeof(VerbLaunchProjectilePatches), nameof(LaunchProjectileWithOriginOverride));
        MethodInfo launchOriginal = AccessTools.Method(
            typeof(Projectile),
            nameof(Projectile.Launch),
            new[]
            {
                typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo),
                typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef)
            });

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Callvirt && Equals(instruction.operand, launchOriginal))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, launchWithOrigin);
                continue;
            }

            yield return instruction;
        }
    }

    public static void LaunchProjectileWithOriginOverride(
        Projectile projectile,
        Thing launcher,
        Vector3 drawPos,
        LocalTargetInfo usedTarget,
        LocalTargetInfo intendedTarget,
        ProjectileHitFlags hitFlags,
        bool preventFriendlyFire,
        Thing equipment,
        ThingDef targetCoverDef,
        Verb_LaunchProjectile verb)
    {
        if (TurretVerbDrawUtility.TryGetProjectileOrigin(launcher, out Vector3 overriddenOrigin))
        {
            drawPos = overriddenOrigin;
        }

        projectile.Launch(launcher, drawPos, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
    }
}
