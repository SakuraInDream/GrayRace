using System;
using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Defs;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SD.GrayRace.Mechs;

/// <summary>
/// 默认火控：按射程选择武器，支持最短/最长两种模式。
/// 吸收了原 TryGetCombatComputerVerb 和 TryFindTargetFor 的逻辑。
/// </summary>
public sealed class RangeBasedFireControl : IFireControlDirector
{
    public bool preferShortest;

    private HashSet<Thing> currentReserved;
    private readonly Predicate<Thing> unreservedValidator;
    private readonly HardpointSearcher searcher = new();

    public RangeBasedFireControl()
    {
        unreservedValidator = ValidateUnreservedTarget;
    }

    public bool TryAssignTarget(
        MechHardpoint hardpoint, Pawn owner,
        LocalTargetInfo forcedTarget, bool fireAtWill,
        HashSet<Thing> reservedTargets, out LocalTargetInfo target)
    {
        Verb verb = hardpoint.AttackVerb;
        if (verb == null || !verb.Available())
        {
            target = LocalTargetInfo.Invalid;
            return false;
        }

        if (forcedTarget.IsValid && hardpoint.CanEngageTarget(forcedTarget, owner, owner.MapHeld))
        {
            target = forcedTarget;
            return true;
        }

        if (!fireAtWill)
        {
            target = LocalTargetInfo.Invalid;
            return false;
        }

        searcher.Configure(hardpoint, owner);
        currentReserved = reservedTargets;
        TargetScanFlags scanFlags = BuildAutoScanFlags(verb);
        target = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(
            searcher, scanFlags, unreservedValidator);

        if (target.IsValid)
        {
            return true;
        }

        target = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(
            searcher, scanFlags);
        return target.IsValid;
    }

    public Verb SelectTacticalVerb(MechHardpoint[] hardpoints, int count, Thing target)
    {
        int hittableCount = 0;
        if (target != null)
        {
            for (int i = 0; i < count; i++)
            {
                Verb candidate = hardpoints[i].AttackVerb;
                Pawn owner = candidate?.CasterPawn;
                if (candidate != null && candidate.Available() && owner != null && candidate.CanHitTargetFrom(owner.Position, target))
                {
                    hittableCount++;
                }
            }
        }

        int bestIdx = -1;
        float bestRange = preferShortest ? float.MaxValue : float.MinValue;
        float bestMinRange = preferShortest ? float.MaxValue : float.MinValue;

        for (int i = 0; i < count; i++)
        {
            Verb candidate = hardpoints[i].AttackVerb;
            if (candidate == null || !candidate.Available())
            {
                continue;
            }

            Pawn owner = candidate?.CasterPawn;
            bool canHit = target != null && owner != null && hardpoints[i].CanEngageTarget(target, owner, owner.MapHeld);
            if (hittableCount > 0 && !canHit)
            {
                continue;
            }

            float candidateRange = candidate.EffectiveRange;
            float candidateMinRange = candidate.verbProps.minRange;

            if (bestIdx < 0)
            {
                bestIdx = i;
                bestRange = candidateRange;
                bestMinRange = candidateMinRange;
                continue;
            }

            if (preferShortest)
            {
                if (candidateRange < bestRange || (Mathf.Approximately(candidateRange, bestRange) && candidateMinRange < bestMinRange))
                {
                    bestRange = candidateRange;
                    bestMinRange = candidateMinRange;
                    bestIdx = i;
                }
            }
            else if (candidateRange > bestRange || (Mathf.Approximately(candidateRange, bestRange) && candidateMinRange > bestMinRange))
            {
                bestRange = candidateRange;
                bestMinRange = candidateMinRange;
                bestIdx = i;
            }
        }

        if (bestIdx >= 0)
        {
            return hardpoints[bestIdx].AttackVerb;
        }

        return null;
    }

    private bool ValidateUnreservedTarget(Thing target)
    {
        return target != null && !currentReserved.Contains(target);
    }

    private static TargetScanFlags BuildAutoScanFlags(Verb verb)
    {
        TargetScanFlags flags = TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
        if (!verb.ProjectileFliesOverhead())
        {
            flags |= TargetScanFlags.NeedLOSToAll;
            flags |= TargetScanFlags.LOSBlockableByGas;
        }

        if (verb.IsIncendiary_Ranged())
        {
            flags |= TargetScanFlags.NeedNonBurning;
        }

        if (verb.ProjectileFliesOverhead())
        {
            flags |= TargetScanFlags.NeedNotUnderThickRoof;
        }

        return flags;
    }
}
