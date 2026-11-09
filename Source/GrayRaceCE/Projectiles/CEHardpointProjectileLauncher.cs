using CombatExtended;
using RimWorld;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;

namespace SD.GrayRace.CE;

internal static class CEHardpointProjectileLauncher
{
    public static bool TryLaunch(
        MechHardpoint hp,
        Pawn pawn,
        Verb_LaunchProjectileCE ceVerb,
        ThingDef projectileDef,
        ProjectilePropertiesCE props,
        LocalTargetInfo target,
        Vector3 origin)
    {
        if (hp == null || projectileDef == null || props == null || ceVerb == null || pawn == null || pawn.Map == null)
        {
            return false;
        }

        if (!target.IsValid || !target.HasThing || target.Thing == null)
        {
            return false;
        }

        Thing targetThing = target.Thing;
        if (targetThing.Destroyed || !targetThing.Spawned || targetThing.Map != pawn.Map)
        {
            return false;
        }

        Thing equipment = hp.gun;

        // origin.y 是渲染用的 altitude（Pawn.DrawPos.y 约 8.4），不是物理高度。
        // CE 的弹道必须使用物理高度：枪口高度取 CE 的 ShotHeight，
        // 目标高度取 CollisionVertical 的身体高度，而不是 GenThing.TrueCenter 的 y。
        float shotHeight = ceVerb.ShotHeight;
        Vector2 origin2D = new Vector2(origin.x, origin.z);
        Vector3 source = new Vector3(origin.x, shotHeight, origin.z);

        // 与 CE 默认自动瞄准一致：取身体高度范围的平均值，而不是渲染用的 DrawPos.y。
        Vector3 targetCenter = GenThing.TrueCenter(targetThing);
        CollisionVertical targetCollision = new CollisionVertical(targetThing);
        float targetHeight = (targetCollision.BottomHeight + targetCollision.MiddleHeight) * 0.5f
            + props.aimHeightOffset;
        Vector3 targetPos = new Vector3(targetCenter.x, targetHeight, targetCenter.z);

        ShiftVecReport report = ceVerb.ShiftVecReportFor(target);
        float shotSpeed = report != null && report.shotSpeed > 0f
            ? report.shotSpeed
            : props.speed;

        Vector2 aimPoint = new Vector2(targetPos.x, targetPos.z);
        if (report != null)
        {
            // 保留原版 forcedMissRadius / forcedMissEvenDispersal 语义。
            bool appliedForcedMiss = false;
            float forcedMissRadius = ceVerb.verbProps.ForcedMissRadius;
            if (forcedMissRadius > 0.5f)
            {
                forcedMissRadius *= ceVerb.verbProps.GetForceMissFactorFor(equipment, pawn);
                float adjustedForcedMiss = VerbUtility.CalculateAdjustedForcedMiss(
                    forcedMissRadius,
                    target.Cell - pawn.Position);

                if (adjustedForcedMiss > 0.5f)
                {
                    IntVec3 forcedMissTarget;
                    bool hasForcedMissTarget;
                    if (ceVerb.verbProps.forcedMissEvenDispersal)
                    {
                        hasForcedMissTarget = hp.TryGetEvenDispersalForcedMissTarget(
                            target.Cell,
                            adjustedForcedMiss,
                            pawn.Position,
                            out forcedMissTarget);
                    }
                    else
                    {
                        int radialCellCount = GenRadial.NumCellsInRadius(adjustedForcedMiss);
                        forcedMissTarget = target.Cell + GenRadial.RadialPattern[Rand.Range(0, radialCellCount)];
                        hasForcedMissTarget = true;
                    }

                    if (hasForcedMissTarget)
                    {
                        Vector3 forcedPos = forcedMissTarget.ToVector3Shifted();
                        aimPoint = new Vector2(forcedPos.x, forcedPos.z);
                        appliedForcedMiss = true;
                    }
                }
            }

            if (!appliedForcedMiss)
            {
                aimPoint += report.GetRandCircularVec();
            }

            if (!props.isInstant)
            {
                aimPoint += report.GetRandLeadVec();
            }
        }

        Vector3 aimPos = new Vector3(aimPoint.x, targetPos.y, aimPoint.y);
        float shotAngle = props.TrajectoryWorker.ShotAngle(props, source, aimPos, shotSpeed);
        float shotRotation = props.TrajectoryWorker.ShotRotation(props, source, aimPos);

        float spreadDegrees = report?.spreadDegrees ?? 0f;
        Vector2 spread = report?.GetRandSpreadVec() ?? Vector2.zero;
        shotRotation = (shotRotation + spread.x) % 360f;
        shotAngle += spread.y * Mathf.Deg2Rad;

        // 与 CE 的 Verb_LaunchProjectileCE.TryCastShot 一致：先按 caster 位置生成，
        // 再用硬点 origin 覆盖 ExactPosition，保证弹道和命中从硬点出发。
        ProjectileCE projectile = (ProjectileCE)ThingMaker.MakeThing(projectileDef);
        GenSpawn.Spawn(projectile, pawn.Position, pawn.Map);

        projectile.ExactPosition = source;
        projectile.canTargetSelf = false;
        projectile.intendedTarget = target;
        projectile.intendedTargetHeight = targetPos.y;
        projectile.mount = null;
        projectile.AccuracyFactor = 1f;

        float horizontalDistance = Vector2.Distance(origin2D, aimPoint);
        projectile.minCollisionDistance = horizontalDistance <= 7.5f
            ? Mathf.Min(1.5f, horizontalDistance * 0.75f)
            : horizontalDistance * 0.2f;

        if (props.isInstant)
        {
            // 激光等瞬时投射物必须走 RayCast，不能走普通 Launch。
            float rayHeight = shotHeight;
            float heightAdjust = ceVerb.AdjustShotHeight(pawn, target, ref rayHeight);
            projectile.RayCast(
                pawn,
                ceVerb.verbProps,
                origin2D,
                shotAngle + heightAdjust,
                shotRotation,
                rayHeight,
                shotSpeed,
                spreadDegrees,
                0.03f,
                equipment);
        }
        else
        {
            projectile.Launch(
                pawn,
                origin2D,
                shotAngle,
                shotRotation,
                shotHeight,
                shotSpeed,
                equipment,
                horizontalDistance);
        }

        return true;
    }
}
