using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.Defs;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SD.GrayRace.Mechs;

public static class GrayMechCombatComputerUtility
{
    public const int TacticalWaitCombatExpiryTicks = 60;
    private const float PreferredRangeTolerance = 0.5f;
    private const float RetreatStepDistance = 1f;
    private const float RetreatThreatClusterPadding = 4f;
    private static readonly List<Thing> RetreatThreats = new();
    private static Pawn retreatPawn;
    private static Map retreatMap;
    private static Thing retreatNearestThreat;
    private static float retreatMinThreatDistance;

    public static GRMechCombatComputerModuleDef GetActiveCombatComputer(Pawn pawn)
    {
        return pawn?.TryGetComp<CompGrayMechSystems>()?.TurretBankSystem?.ActiveCombatComputer;
    }

    public static GRMechCombatComputerWeaponSelectionMode ResolveWeaponSelection(GRMechCombatComputerModuleDef combatComputer)
    {
        return combatComputer?.weaponSelection == GRMechCombatComputerWeaponSelectionMode.ShortestRange
            ? GRMechCombatComputerWeaponSelectionMode.ShortestRange
            : GRMechCombatComputerWeaponSelectionMode.LongestRange;
    }

    public static Thing ResolveTacticalTarget(Pawn pawn, GRMechCombatComputerModuleDef combatComputer, Thing fallbackTarget)
    {
        if (combatComputer?.behavior == GRMechCombatComputerBehavior.Picket
            && TryGetNearestThreat(pawn, 18f, out Thing nearestThreat, out _))
        {
            return nearestThreat;
        }

        return fallbackTarget;
    }

    public static bool TryFindPosition(Pawn pawn, GRMechCombatComputerModuleDef combatComputer, Thing anchorTarget, Verb verb, out IntVec3 dest)
    {
        dest = IntVec3.Invalid;

        if (pawn == null || anchorTarget == null || verb == null)
        {
            return false;
        }

        ClearRetreatContext();
        bool retreatRequired = RequiresRetreat(combatComputer, pawn, anchorTarget, verb);

        CastPositionRequest request = new()
        {
            caster = pawn,
            target = anchorTarget,
            verb = verb,
            maxRangeFromTarget = verb.EffectiveRange,
            wantCoverFromTarget = WantsCover(combatComputer, verb)
        };

        if (retreatRequired)
        {
            request.validator = RetreatCastPositionValidator;
        }

        if ((retreatRequired && TryGetPreferredRetreatCastPosition(pawn, anchorTarget, out IntVec3 preferred))
            || TryGetPreferredCastPosition(pawn, anchorTarget, verb, combatComputer, out preferred))
        {
            request.preferredCastPosition = preferred;
        }

        bool found = CastPositionFinder.TryFindCastPosition(request, out dest);
        if (!found && retreatRequired)
        {
            found = TryFindFallbackRetreatPosition(pawn, out dest);
        }

        ClearRetreatContext();
        return found;
    }

    public static bool ShouldWaitAtCurrentPosition(GRMechCombatComputerModuleDef combatComputer, Pawn pawn, Thing anchorTarget, Verb verb, bool hasCover, bool canReserveCurrentCell, bool canHitTarget, bool targetVeryClose, out int waitTicks)
    {
        waitTicks = 0;
        if (!canHitTarget || anchorTarget == null)
        {
            return false;
        }

        bool vanillaWait = (hasCover && canReserveCurrentCell) || targetVeryClose;
        if (combatComputer == null || combatComputer.positioning == GRMechCombatComputerPositioningMode.Vanilla)
        {
            return vanillaWait;
        }

        if (RequiresRetreat(combatComputer, pawn, anchorTarget, verb))
        {
            ClearRetreatContext();
            return false;
        }

        float desiredRange = GetPreferredRange(pawn, anchorTarget, verb, combatComputer);
        if (desiredRange <= 0f)
        {
            return vanillaWait;
        }

        float currentDistance = GetDistanceToTargetAnchor(pawn, anchorTarget);
        bool shouldWait = combatComputer.positioning switch
        {
            GRMechCombatComputerPositioningMode.CloseToPreferredRange => currentDistance <= desiredRange + PreferredRangeTolerance,
            GRMechCombatComputerPositioningMode.HoldPreferredRange => Mathf.Abs(currentDistance - desiredRange) <= PreferredRangeTolerance,
            _ => vanillaWait
        };

        if (shouldWait && !vanillaWait)
        {
            waitTicks = TacticalWaitCombatExpiryTicks;
        }

        return shouldWait;
    }

    private static bool RequiresRetreat(GRMechCombatComputerModuleDef combatComputer, Pawn pawn, Thing enemyTarget, Verb verb)
    {
        if (combatComputer == null
            || pawn == null
            || enemyTarget == null
            || verb == null
            || combatComputer.positioning != GRMechCombatComputerPositioningMode.HoldPreferredRange)
        {
            return false;
        }

        float desiredRange = GetPreferredRange(pawn, enemyTarget, verb, combatComputer);
        if (desiredRange <= 0f)
        {
            return false;
        }

        if (!TryGetNearestThreat(pawn, desiredRange, out Thing nearestThreat, out float nearestThreatDistance))
        {
            return false;
        }

        if (nearestThreatDistance + PreferredRangeTolerance >= desiredRange)
        {
            return false;
        }

        BuildRetreatContext(pawn, nearestThreat, nearestThreatDistance, desiredRange);
        return true;
    }

    private static bool WantsCover(GRMechCombatComputerModuleDef combatComputer, Verb verb)
    {
        return combatComputer?.coverPreference switch
        {
            GRMechCombatComputerCoverPreference.PreferCover => true,
            GRMechCombatComputerCoverPreference.IgnoreCover => false,
            _ => verb.EffectiveRange > 5f
        };
    }

    private static bool TryGetPreferredCastPosition(Pawn pawn, Thing enemyTarget, Verb verb, GRMechCombatComputerModuleDef combatComputer, out IntVec3 preferred)
    {
        preferred = IntVec3.Invalid;
        if (pawn?.Map == null || enemyTarget == null || combatComputer == null)
        {
            return false;
        }

        if (combatComputer.positioning == GRMechCombatComputerPositioningMode.Vanilla)
        {
            return false;
        }

        float desiredRange = GetPreferredRange(pawn, enemyTarget, verb, combatComputer);
        if (desiredRange <= 0f)
        {
            return false;
        }

        IntVec3 targetAnchor = enemyTarget.OccupiedRect().ClosestCellTo(pawn.Position);
        int deltaX = pawn.Position.x - targetAnchor.x;
        int deltaZ = pawn.Position.z - targetAnchor.z;
        float distanceSquared = deltaX * deltaX + deltaZ * deltaZ;
        if (distanceSquared <= 0.001f)
        {
            preferred = pawn.Position;
            return true;
        }

        float distance = Mathf.Sqrt(distanceSquared);
        if (combatComputer.positioning == GRMechCombatComputerPositioningMode.CloseToPreferredRange
            && distance <= desiredRange + PreferredRangeTolerance)
        {
            preferred = pawn.Position;
            return true;
        }

        if (combatComputer.positioning == GRMechCombatComputerPositioningMode.HoldPreferredRange
            && Mathf.Abs(distance - desiredRange) <= PreferredRangeTolerance)
        {
            preferred = pawn.Position;
            return true;
        }

        float scale = desiredRange / distance;
        preferred = new IntVec3(
            targetAnchor.x + Mathf.RoundToInt(deltaX * scale),
            0,
            targetAnchor.z + Mathf.RoundToInt(deltaZ * scale));
        return preferred.InBounds(pawn.Map);
    }

    private static bool TryGetPreferredRetreatCastPosition(Pawn pawn, Thing enemyTarget, out IntVec3 preferred)
    {
        preferred = IntVec3.Invalid;
        if (pawn?.Map == null || enemyTarget == null || RetreatThreats.Count == 0)
        {
            return false;
        }

        Vector2 retreatVector = Vector2.zero;
        for (int i = 0; i < RetreatThreats.Count; i++)
        {
            Thing threat = RetreatThreats[i];
            if (threat == null || threat.Destroyed)
            {
                continue;
            }

            float dx = pawn.Position.x - threat.Position.x;
            float dz = pawn.Position.z - threat.Position.z;
            float distanceSquared = dx * dx + dz * dz;
            if (distanceSquared <= 0.001f)
            {
                continue;
            }

            float inverseDistance = 1f / Mathf.Sqrt(distanceSquared);
            retreatVector.x += dx * inverseDistance;
            retreatVector.y += dz * inverseDistance;
        }

        if (retreatVector.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        retreatVector.Normalize();
        int retreatDistance = Mathf.Max(3, Mathf.CeilToInt(retreatMinThreatDistance - GetNearestThreatDistance(pawn.Position)));
        preferred = new IntVec3(
            pawn.Position.x + Mathf.RoundToInt(retreatVector.x * retreatDistance),
            0,
            pawn.Position.z + Mathf.RoundToInt(retreatVector.y * retreatDistance));
        return preferred.InBounds(pawn.Map);
    }

    private static float GetPreferredRange(Pawn pawn, Thing enemyTarget, Verb verb, GRMechCombatComputerModuleDef combatComputer)
    {
        if (pawn == null || enemyTarget == null || verb == null || combatComputer == null)
        {
            return 0f;
        }

        float desiredRange = verb.EffectiveRange;
        if (combatComputer.preferredRangeFactor > 0f)
        {
            desiredRange *= combatComputer.preferredRangeFactor;
        }

        float minRange = verb.verbProps.EffectiveMinRange(enemyTarget, pawn);
        return Mathf.Clamp(desiredRange, minRange, verb.EffectiveRange);
    }

    private static float GetDistanceToTargetAnchor(Pawn pawn, Thing enemyTarget)
    {
        return pawn == null ? 0f : GetDistanceToTargetAnchor(pawn.Position, enemyTarget);
    }

    private static float GetDistanceToTargetAnchor(IntVec3 from, Thing enemyTarget)
    {
        IntVec3 targetAnchor = enemyTarget.OccupiedRect().ClosestCellTo(from);
        return (from - targetAnchor).LengthHorizontal;
    }

    private static bool TryGetNearestThreat(Pawn pawn, float maxDistance, out Thing nearestThreat, out float nearestThreatDistance)
    {
        nearestThreat = null;
        nearestThreatDistance = float.MaxValue;
        if (pawn?.Map == null)
        {
            return false;
        }

        List<IAttackTarget> potentialTargets = pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn);
        for (int i = 0; i < potentialTargets.Count; i++)
        {
            IAttackTarget attackTarget = potentialTargets[i];
            Thing threat = attackTarget?.Thing;
            if (!IsValidRetreatThreat(pawn, attackTarget, threat))
            {
                continue;
            }

            float distance = GetDistanceToTargetAnchor(pawn.Position, threat);
            if (distance > maxDistance || distance >= nearestThreatDistance)
            {
                continue;
            }

            nearestThreat = threat;
            nearestThreatDistance = distance;
        }

        return nearestThreat != null;
    }

    private static void BuildRetreatContext(Pawn pawn, Thing nearestThreat, float nearestThreatDistance, float desiredRange)
    {
        retreatPawn = pawn;
        retreatMap = pawn.Map;
        retreatNearestThreat = nearestThreat;
        retreatMinThreatDistance = Mathf.Min(desiredRange - PreferredRangeTolerance, nearestThreatDistance + Mathf.Max(RetreatStepDistance, (desiredRange - nearestThreatDistance) * 0.5f));
        RetreatThreats.Clear();

        float clusterDistance = nearestThreatDistance + RetreatThreatClusterPadding;
        List<IAttackTarget> potentialTargets = pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn);
        for (int i = 0; i < potentialTargets.Count; i++)
        {
            IAttackTarget attackTarget = potentialTargets[i];
            Thing threat = attackTarget?.Thing;
            if (!IsValidRetreatThreat(pawn, attackTarget, threat))
            {
                continue;
            }

            if (GetDistanceToTargetAnchor(pawn.Position, threat) <= clusterDistance)
            {
                RetreatThreats.Add(threat);
            }
        }

        if (RetreatThreats.Count == 0 && nearestThreat != null)
        {
            RetreatThreats.Add(nearestThreat);
        }
    }

    private static bool IsValidRetreatThreat(Pawn pawn, IAttackTarget attackTarget, Thing threat)
    {
        if (pawn == null || attackTarget == null || threat == null || threat == pawn || threat.Destroyed || !threat.Spawned || threat.Map != pawn.Map)
        {
            return false;
        }

        if (attackTarget.ThreatDisabled(pawn) || !threat.HostileTo(pawn))
        {
            return false;
        }

        return true;
    }

    private static bool RetreatCastPositionValidator(IntVec3 cell)
    {
        return GetNearestThreatDistance(cell) >= retreatMinThreatDistance;
    }

    private static bool TryFindFallbackRetreatPosition(Pawn pawn, out IntVec3 dest)
    {
        dest = IntVec3.Invalid;
        if (pawn?.Map == null)
        {
            return false;
        }

        int maxSearchRadius = Mathf.Max(10, Mathf.CeilToInt(retreatMinThreatDistance + RetreatThreatClusterPadding));
        if (RCellFinder.TryFindRandomCellNearWith(pawn.Position, RetreatFallbackValidator, pawn.Map, out dest, 5, maxSearchRadius))
        {
            return true;
        }

        if (retreatNearestThreat != null)
        {
            Job fleeJob = FleeUtility.FleeJob(pawn, retreatNearestThreat, maxSearchRadius);
            if (fleeJob != null)
            {
                dest = fleeJob.targetA.Cell;
                return true;
            }
        }

        return false;
    }

    private static bool RetreatFallbackValidator(IntVec3 cell)
    {
        return retreatPawn != null
            && retreatMap != null
            && cell.InBounds(retreatMap)
            && cell.Standable(retreatMap)
            && retreatPawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)
            && GetNearestThreatDistance(cell) >= retreatMinThreatDistance;
    }

    private static float GetNearestThreatDistance(IntVec3 cell)
    {
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < RetreatThreats.Count; i++)
        {
            Thing threat = RetreatThreats[i];
            if (threat == null || threat.Destroyed || !threat.Spawned)
            {
                continue;
            }

            float distance = GetDistanceToTargetAnchor(cell, threat);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
            }
        }

        return nearestDistance;
    }

    private static void ClearRetreatContext()
    {
        RetreatThreats.Clear();
        retreatPawn = null;
        retreatMap = null;
        retreatNearestThreat = null;
        retreatMinThreatDistance = 0f;
    }
}
