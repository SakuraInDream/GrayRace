using SD.GrayRace.Comps;
using SD.GrayRace.Defs;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SD.GrayRace.Mechs;

public static class GrayMechCombatComputerUtility
{
    private const float PreferredRangeTolerance = 0.5f;

    public static GRMechModuleDef GetActiveCombatComputer(Pawn pawn)
    {
        return pawn?.TryGetComp<CompGrayMechTurretBank>()?.ActiveCombatComputer;
    }

    public static GRMechCombatComputerWeaponSelectionMode ResolveWeaponSelection(GRMechModuleDef combatComputer)
    {
        return combatComputer?.combatComputerWeaponSelection == GRMechCombatComputerWeaponSelectionMode.ShortestRange
            ? GRMechCombatComputerWeaponSelectionMode.ShortestRange
            : GRMechCombatComputerWeaponSelectionMode.LongestRange;
    }

    public static bool TryFindPosition(Pawn pawn, GRMechModuleDef combatComputer, Verb verb, out IntVec3 dest)
    {
        dest = IntVec3.Invalid;

        Thing enemyTarget = pawn?.mindState?.enemyTarget;
        if (pawn == null || enemyTarget == null || verb == null)
        {
            return false;
        }

        CastPositionRequest request = new()
        {
            caster = pawn,
            target = enemyTarget,
            verb = verb,
            maxRangeFromTarget = verb.EffectiveRange,
            wantCoverFromTarget = WantsCover(combatComputer, verb)
        };

        if (TryGetPreferredCastPosition(pawn, enemyTarget, verb, combatComputer, out IntVec3 preferred))
        {
            request.preferredCastPosition = preferred;
        }

        return CastPositionFinder.TryFindCastPosition(request, out dest);
    }

    public static bool ShouldWaitAtCurrentPosition(GRMechModuleDef combatComputer, Pawn pawn, Thing enemyTarget, Verb verb, bool hasCover, bool canReserveCurrentCell, bool canHitTarget, bool targetVeryClose)
    {
        if (!canHitTarget)
        {
            return false;
        }

        if (combatComputer == null || combatComputer.combatComputerPositioning == GRMechCombatComputerPositioningMode.Vanilla)
        {
            return (hasCover && canReserveCurrentCell) || targetVeryClose;
        }

        float desiredRange = GetPreferredRange(pawn, enemyTarget, verb, combatComputer);
        if (desiredRange <= 0f)
        {
            return (hasCover && canReserveCurrentCell) || targetVeryClose;
        }

        float currentDistance = GetDistanceToTargetAnchor(pawn, enemyTarget);
        return combatComputer.combatComputerPositioning switch
        {
            GRMechCombatComputerPositioningMode.CloseToPreferredRange => currentDistance <= desiredRange + PreferredRangeTolerance,
            GRMechCombatComputerPositioningMode.HoldPreferredRange => Mathf.Abs(currentDistance - desiredRange) <= PreferredRangeTolerance,
            _ => (hasCover && canReserveCurrentCell) || targetVeryClose
        };
    }

    private static bool WantsCover(GRMechModuleDef combatComputer, Verb verb)
    {
        return combatComputer?.combatComputerCoverPreference switch
        {
            GRMechCombatComputerCoverPreference.PreferCover => true,
            GRMechCombatComputerCoverPreference.IgnoreCover => false,
            _ => verb.EffectiveRange > 5f
        };
    }

    private static bool TryGetPreferredCastPosition(Pawn pawn, Thing enemyTarget, Verb verb, GRMechModuleDef combatComputer, out IntVec3 preferred)
    {
        preferred = IntVec3.Invalid;
        if (pawn?.Map == null || enemyTarget == null || combatComputer == null)
        {
            return false;
        }

        if (combatComputer.combatComputerPositioning == GRMechCombatComputerPositioningMode.Vanilla)
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
        if (combatComputer.combatComputerPositioning == GRMechCombatComputerPositioningMode.CloseToPreferredRange
            && distance <= desiredRange + PreferredRangeTolerance)
        {
            preferred = pawn.Position;
            return true;
        }

        if (combatComputer.combatComputerPositioning == GRMechCombatComputerPositioningMode.HoldPreferredRange
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

    private static float GetPreferredRange(Pawn pawn, Thing enemyTarget, Verb verb, GRMechModuleDef combatComputer)
    {
        if (pawn == null || enemyTarget == null || verb == null || combatComputer == null)
        {
            return 0f;
        }

        float desiredRange = verb.EffectiveRange;
        if (combatComputer.combatComputerPreferredRangeFactor > 0f)
        {
            desiredRange *= combatComputer.combatComputerPreferredRangeFactor;
        }

        float minRange = verb.verbProps.EffectiveMinRange(enemyTarget, pawn);
        return Mathf.Clamp(desiredRange, minRange, verb.EffectiveRange);
    }

    private static float GetDistanceToTargetAnchor(Pawn pawn, Thing enemyTarget)
    {
        IntVec3 targetAnchor = enemyTarget.OccupiedRect().ClosestCellTo(pawn.Position);
        return (pawn.Position - targetAnchor).LengthHorizontal;
    }
}
