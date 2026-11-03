using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SD.GrayRace.HarmonyPatches;

/// <summary>
/// 让携带 GR_Hediff_Thruster 的 Pawn“悬浮”移动。
/// 参考 Maru 种族的 MaruTerrainSkip：接管 Pawn_PathFollower.CostToMoveIntoCell，
/// 跳过地形（含水面、雪、沙、地形 tag 减速）带来的移动耗时，
/// 但保留建筑与门的通行耗时——推进器只能浮过地面，不能破门。
/// 注意：这里只改变“进入下一格的耗时”，不影响寻路成本与地形可通行性判断。
/// </summary>
[HarmonyPatch]
internal static class Patch_Pawn_PathFollower_CostToMoveIntoCell
{
    private const string ThrusterHediffDefName = "GR_Hediff_Thruster";

    // 与原版 Pawn_PathFollower.CostToMoveIntoCell 保持一致的上限/阈值。
    private const float MaxMoveCostCap = 450f;
    private const int DoorPassRawPenalty = 45;
    private const int MinRepeaterPathCost = 25;
    private const int MinCostAmbleCap = 60;
    private const int MinCostWalkCap = 50;

    private static HediffDef thrusterDef;
    private static bool warnedMissingThruster;

    private static HediffDef ThrusterDef
    {
        get
        {
            if (thrusterDef == null)
            {
                thrusterDef = DefDatabase<HediffDef>.GetNamedSilentFail(ThrusterHediffDefName);
                if (thrusterDef == null && !warnedMissingThruster)
                {
                    warnedMissingThruster = true;
                    Log.Warning($"[GrayRace] 未找到 HediffDef '{ThrusterHediffDefName}'，推进器地形忽略功能不会生效。");
                }
            }

            return thrusterDef;
        }
    }

    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
                   typeof(Pawn_PathFollower),
                   "CostToMoveIntoCell",
                   new[] { typeof(Pawn), typeof(IntVec3) })
               ?? throw new Exception(
                   "Patch_Pawn_PathFollower_CostToMoveIntoCell: 未找到 Pawn_PathFollower.CostToMoveIntoCell(Pawn, IntVec3)（游戏版本不匹配？）");
    }

    private static bool Prefix(Pawn pawn, IntVec3 c, ref float __result)
    {
        HediffDef def = ThrusterDef;
        if (def == null
            || pawn?.health?.hediffSet == null
            || !pawn.health.hediffSet.HasHediff(def))
        {
            return true;
        }

        Map map = pawn.Map;
        if (map == null)
        {
            return true;
        }

        float ticks = c.x != pawn.Position.x && c.z != pawn.Position.z
            ? pawn.TicksPerMoveDiagonal
            : pawn.TicksPerMoveCardinal;

        // 只保留障碍（含门）成本；地形 pathCost、雪、沙全部忽略。
        ticks += GetObstacleCost(map.pathing.For(pawn).pathGrid, c, pawn.Position);

        Building edifice = c.GetEdifice(map);
        if (edifice != null)
        {
            ticks += edifice.PathWalkCostFor(pawn);
        }

        // 刻意跳过 pawn.kindDef.moveSpeedFactorByTerrainTag：
        // 推进器浮空后不再受沼泽/水流等地形 tag 的减速乘数影响。

        if (ticks > MaxMoveCostCap)
        {
            ticks = MaxMoveCostCap;
        }

        if (pawn.CurJob != null)
        {
            Pawn locomotionUrgencySameAs = pawn.jobs?.curDriver?.locomotionUrgencySameAs;
            LocomotionUrgency urgency = pawn.jobs.curJob.locomotionUrgency;
            if (locomotionUrgencySameAs != null
                && locomotionUrgencySameAs != pawn
                && locomotionUrgencySameAs.Spawned
                && locomotionUrgencySameAs.CurJob != null)
            {
                urgency = locomotionUrgencySameAs.jobs.curJob.locomotionUrgency;
            }

            switch (urgency)
            {
                case LocomotionUrgency.Amble:
                    ticks *= 3f;
                    if (ticks < MinCostAmbleCap)
                    {
                        ticks = MinCostAmbleCap;
                    }

                    break;
                case LocomotionUrgency.Walk:
                    ticks *= 2f;
                    if (ticks < MinCostWalkCap)
                    {
                        ticks = MinCostWalkCap;
                    }

                    break;
                case LocomotionUrgency.Sprint:
                    ticks = Mathf.RoundToInt(ticks * 0.75f);
                    break;
            }
        }

        __result = Mathf.Max(ticks, 1f);
        return false;
    }

    /// <summary>
    /// 复刻原版 PathGrid.CalculatedCostAt 中与障碍/门有关的部分，
    /// 但完全不累加地形基础成本（terrain.pathCost）与雪/沙成本。
    /// </summary>
    private static int GetObstacleCost(PathGrid pathGrid, IntVec3 cell, IntVec3 prevCell)
    {
        Map map = pathGrid.map;
        TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
        PathGridDef gridDef = pathGrid.def;

        // 与 PathGrid.CalculatedCostAt 一致：不可通行地形仍然不可通行。
        if (terrain == null
            || (terrain.passability == Traversability.Impassable
                && (!gridDef.flying || !terrain.forcePassableByFlyingPawns)))
        {
            return 10000;
        }

        int cost = 0;
        bool doorPenalty = false;
        List<Thing> things = map.thingGrid.ThingsListAt(cell);
        for (int i = 0; i < things.Count; i++)
        {
            Thing thing = things[i];
            if (thing.def.passability == Traversability.Impassable
                && (!gridDef.flying || !thing.def.forcePassableByFlyingPawns))
            {
                return 10000;
            }

            if (!gridDef.fencePassable
                && thing.def.building != null
                && thing.def.building.isFence)
            {
                return 10000;
            }

            if (!IsPathCostIgnoreRepeater(thing.def)
                || !prevCell.IsValid
                || !ContainsPathCostIgnoreRepeater(pathGrid, prevCell))
            {
                int thingCost = thing.def.pathCost;
                if (thingCost > cost)
                {
                    cost = thingCost;
                }
            }

            if (thing is Building_Door door
                && prevCell.IsValid
                && prevCell.GetEdifice(map) is Building_Door prevDoor
                && !door.FreePassage
                && !prevDoor.FreePassage)
            {
                doorPenalty = true;
            }
        }

        if (doorPenalty)
        {
            cost += DoorPassRawPenalty;
        }

        return cost;
    }

    private static bool IsPathCostIgnoreRepeater(ThingDef def)
    {
        return def != null
               && def.pathCost >= MinRepeaterPathCost
               && def.pathCostIgnoreRepeat;
    }

    private static bool ContainsPathCostIgnoreRepeater(PathGrid pathGrid, IntVec3 cell)
    {
        if (!GenGrid.InBounds(cell, pathGrid.map))
        {
            return false;
        }

        List<Thing> things = pathGrid.map.thingGrid.ThingsListAtFast(cell);
        for (int i = 0; i < things.Count; i++)
        {
            if (IsPathCostIgnoreRepeater(things[i].def))
            {
                return true;
            }
        }

        return false;
    }
}
