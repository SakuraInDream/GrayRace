using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using SD.GrayRace.Defs;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Mechs;

public struct TurretShotReport
{
    private TargetInfo target;
    private float distance;
    private List<CoverInfo> covers;
    private float coversOverallBlockChance;
    private float factorFromShooterAndDist;
    private float factorFromWeapon;
    private float factorFromTargetSize;
    private float factorFromWeather;
    private float forcedMissRadius;
    private float offsetFromDarkness;
    private float factorFromCoveringGas;

    private ShootLine shootLine;

    public float AimOnTargetChance
    {
        get
        {
            float num = factorFromShooterAndDist * factorFromWeapon * factorFromWeather * factorFromCoveringGas;
            num += offsetFromDarkness;
            if (num < 0.0201f)
            {
                num = 0.0201f;
            }
            return num;
        }
    }

    public float AimOnTargetChanceWithSize => AimOnTargetChance * factorFromTargetSize;

    public float PassCoverChance => 1f - coversOverallBlockChance;

    public float TotalEstimatedHitChance => Mathf.Clamp01(AimOnTargetChance * PassCoverChance);

    public static TurretShotReport HitReportFor(Pawn shooter, MechHardpoint hardpoint, LocalTargetInfo target)
    {
        Map map = shooter.Map;
        IntVec3 cell = target.Cell;
        Vector3 cellPos = cell.ToVector3Shifted();
        Vector3 shooterPos = shooter.DrawPos;

        TurretShotReport result = new()
        {
            distance = Vector2.Distance(new Vector2(cellPos.x, cellPos.z), new Vector2(shooterPos.x, shooterPos.z)),
            target = target.ToTargetInfo(map)
        };

        result.factorFromShooterAndDist = HitFactorFromShooter(shooter, result.distance);
        result.factorFromWeapon = HitFactorFromWeapon(hardpoint, result.distance);

        IntVec3 shooterCell = shooter.Position;
        result.covers = CoverUtility.CalculateCoverGiverSet(target, shooterCell, map);
        result.coversOverallBlockChance = CoverUtility.CalculateOverallBlockChance(target, shooterCell, map);

        result.factorFromCoveringGas = 1f;
        if (hardpoint.TryFindShootLineTo(target, out result.shootLine))
        {
            foreach (IntVec3 item in result.shootLine.Points())
            {
                if (item.InBounds(map) && item.AnyGas(map, GasType.BlindSmoke))
                {
                    result.factorFromCoveringGas = 0.7f;
                    break;
                }
            }
        }
        else
        {
            result.shootLine = new ShootLine(IntVec3.Invalid, IntVec3.Invalid);
        }

        result.factorFromWeather = shooterCell.Roofed(map) && target.Cell.Roofed(map) ? 1f : map.weatherManager.CurWeatherAccuracyMultiplier;

        if (target.HasThing)
        {
            if (target.Thing is Pawn pawn)
            {
                result.factorFromTargetSize = pawn.BodySize;
            }
            else
            {
                result.factorFromTargetSize = target.Thing.def.fillPercent * target.Thing.def.size.x * target.Thing.def.size.z * 2.5f;
            }
            result.factorFromTargetSize = Mathf.Clamp(result.factorFromTargetSize, 0.5f, 2f);
        }
        else
        {
            result.factorFromTargetSize = 1f;
        }

        result.forcedMissRadius = hardpoint.module?.forcedMissRadius ?? 0f;

        result.offsetFromDarkness = 0f;
        if (ModsConfig.IdeologyActive && target.HasThing)
        {
            if (DarknessCombatUtility.IsOutdoorsAndLit(target.Thing))
            {
                result.offsetFromDarkness = shooter.GetStatValue(StatDefOf.ShootingAccuracyOutdoorsLitOffset);
            }
            else if (DarknessCombatUtility.IsOutdoorsAndDark(target.Thing))
            {
                result.offsetFromDarkness = shooter.GetStatValue(StatDefOf.ShootingAccuracyOutdoorsDarkOffset);
            }
            else if (DarknessCombatUtility.IsIndoorsAndDark(target.Thing))
            {
                result.offsetFromDarkness = shooter.GetStatValue(StatDefOf.ShootingAccuracyIndoorsDarkOffset);
            }
            else if (DarknessCombatUtility.IsIndoorsAndLit(target.Thing))
            {
                result.offsetFromDarkness = shooter.GetStatValue(StatDefOf.ShootingAccuracyIndoorsLitOffset);
            }
        }

        return result;
    }

    private static float HitFactorFromShooter(Pawn shooter, float distance)
    {
        if (shooter == null)
        {
            return 1f;
        }

        float statValue = shooter.GetStatValue(StatDefOf.ShootingAccuracyPawn);
        statValue *= distance switch
        {
            <= 3f => shooter.GetStatValue(StatDefOf.ShootingAccuracyFactor_Touch),
            <= 12f => Mathf.Lerp(
                shooter.GetStatValue(StatDefOf.ShootingAccuracyFactor_Touch),
                shooter.GetStatValue(StatDefOf.ShootingAccuracyFactor_Short),
                (distance - 3f) / 9f),
            <= 25f => Mathf.Lerp(
                shooter.GetStatValue(StatDefOf.ShootingAccuracyFactor_Short),
                shooter.GetStatValue(StatDefOf.ShootingAccuracyFactor_Medium),
                (distance - 12f) / 13f),
            <= 40f => Mathf.Lerp(
                shooter.GetStatValue(StatDefOf.ShootingAccuracyFactor_Medium),
                shooter.GetStatValue(StatDefOf.ShootingAccuracyFactor_Long),
                (distance - 25f) / 15f),
            _ => shooter.GetStatValue(StatDefOf.ShootingAccuracyFactor_Long)
        };

        return Mathf.Max(Mathf.Pow(statValue, distance), 0.02f);
    }

    private static float HitFactorFromWeapon(MechHardpoint hardpoint, float distance)
    {
        if (hardpoint?.module == null)
        {
            return 1f;
        }

        GRMechModuleDef module = hardpoint.module;

        float factor = distance switch
        {
            <= 3f => module.accuracyTouch,
            <= 12f => Mathf.Lerp(module.accuracyTouch, module.accuracyShort, (distance - 3f) / 9f),
            <= 25f => Mathf.Lerp(module.accuracyShort, module.accuracyMedium, (distance - 12f) / 13f),
            <= 40f => Mathf.Lerp(module.accuracyMedium, module.accuracyLong, (distance - 25f) / 15f),
            _ => module.accuracyLong
        };

        return factor;
    }

    public Thing GetRandomCoverToMissInto()
    {
        return covers.TryRandomElementByWeight(cover => cover.BlockChance, out CoverInfo result) ? result.Thing : null;
    }
}