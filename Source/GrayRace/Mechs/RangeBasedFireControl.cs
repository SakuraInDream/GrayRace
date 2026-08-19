using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace SD.GrayRace.Mechs;

/// <summary>
/// 默认火控：一次读取地图敌对候选，并为全部空闲硬点批量评分和分配目标。
/// </summary>
public sealed class RangeBasedFireControl : IFireControlDirector
{
    private const int InitialCandidateCapacity = 128;
    private const int RecentTargetTicks = 300;

    private bool[] automaticHardpoints = Array.Empty<bool>();
    private Thing[] selectedTargets = Array.Empty<Thing>();
    private float[] candidateScores = Array.Empty<float>();
    private Map gasMap;
    private readonly Func<IntVec3, bool> noBlindSmokeValidator;

    public RangeBasedFireControl()
    {
        noBlindSmokeValidator = CellHasNoBlindSmoke;
    }

    public void Prepare(int hardpointCapacity)
    {
        EnsureHardpointCapacity(hardpointCapacity);
        EnsureCandidateScoreCapacity(hardpointCapacity * InitialCandidateCapacity);
    }

    public void AssignTargets(
        MechHardpoint[] hardpoints,
        int hardpointCount,
        Pawn owner,
        LocalTargetInfo forcedTarget,
        bool fireAtWill,
        LocalTargetInfo[] results)
    {
        if (hardpoints == null || results == null || owner == null || owner.Map == null || hardpointCount <= 0)
        {
            return;
        }

        EnsureHardpointCapacity(hardpointCount);
        int automaticCount = 0;
        int selectedCount = 0;
        Thing forcedTargetThing = forcedTarget.HasThing ? forcedTarget.Thing : null;

        for (int i = 0; i < hardpointCount; i++)
        {
            results[i] = LocalTargetInfo.Invalid;
            automaticHardpoints[i] = false;

            MechHardpoint hp = hardpoints[i];
            if (hp == null)
            {
                continue;
            }

            if (hp.state != MechHardpoint.State.Idle
                && hp.currentTarget.HasThing
                && hp.currentTarget.Thing != forcedTargetThing)
            {
                AddSelectedTarget(hp.currentTarget.Thing, ref selectedCount);
            }

            Verb verb = hp.AttackVerb;
            if (verb == null || !verb.Available())
            {
                continue;
            }

            if (forcedTarget.IsValid)
            {
                if (hp.state != MechHardpoint.State.Firing
                    && hp.state != MechHardpoint.State.Cooling)
                {
                    if (hp.state == MechHardpoint.State.WarmingUp && hp.currentTarget == forcedTarget)
                    {
                        continue;
                    }

                    if (hp.CanEngageTarget(forcedTarget, owner, owner.Map))
                    {
                        results[i] = forcedTarget;
                    }
                }

                continue;
            }

            if (fireAtWill && hp.state == MechHardpoint.State.Idle)
            {
                automaticHardpoints[i] = true;
                automaticCount++;
            }
        }

        if (automaticCount == 0)
        {
            ClearSelectedTargets(selectedCount);
            return;
        }

        List<IAttackTarget> candidates = owner.Map.attackTargetsCache.GetPotentialTargetsFor(owner);
        int candidateCount = candidates.Count;
        if (candidateCount == 0)
        {
            ClearSelectedTargets(selectedCount);
            return;
        }

        int scoreCount = candidateCount * hardpointCount;
        EnsureCandidateScoreCapacity(scoreCount);
        for (int i = 0; i < scoreCount; i++)
        {
            candidateScores[i] = float.MinValue;
        }

        Map map = owner.Map;
        Lord lord = owner.GetLord();
        gasMap = map;
        for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
        {
            IAttackTarget candidate = candidates[candidateIndex];
            if (!PassesSharedFilters(candidate, owner, map, lord))
            {
                continue;
            }

            int scoreOffset = candidateIndex * hardpointCount;
            sbyte smokeBlockedLos = 0;
            sbyte unrestrictedLos = 0;
            for (int hardpointIndex = 0; hardpointIndex < hardpointCount; hardpointIndex++)
            {
                if (!automaticHardpoints[hardpointIndex])
                {
                    continue;
                }

                MechHardpoint hp = hardpoints[hardpointIndex];
                if (CanWeaponTarget(hp, candidate, owner, map, ref smokeBlockedLos, ref unrestrictedLos))
                {
                    candidateScores[scoreOffset + hardpointIndex] = GetShootingTargetScore(candidate, hp, owner, map);
                }
            }
        }

        gasMap = null;
        for (int hardpointIndex = 0; hardpointIndex < hardpointCount; hardpointIndex++)
        {
            if (!automaticHardpoints[hardpointIndex])
            {
                continue;
            }

            int selectedIndex = SelectCandidateIndex(candidates, candidateCount, hardpointCount, hardpointIndex, selectedCount);
            if (selectedIndex < 0)
            {
                continue;
            }

            Thing selectedThing = candidates[selectedIndex].Thing;
            results[hardpointIndex] = selectedThing;
            AddSelectedTarget(selectedThing, ref selectedCount);
        }

        ClearSelectedTargets(selectedCount);
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
                if (candidate != null
                    && candidate.Available()
                    && owner != null
                    && hardpoints[i].CanEngageTarget(target, owner, owner.MapHeld))
                {
                    hittableCount++;
                }
            }
        }

        int bestIdx = -1;
        float bestRange = float.MinValue;
        float bestMinRange = float.MinValue;

        for (int i = 0; i < count; i++)
        {
            Verb candidate = hardpoints[i].AttackVerb;
            if (candidate == null || !candidate.Available())
            {
                continue;
            }

            Pawn owner = candidate.CasterPawn;
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

            if (candidateRange > bestRange || (Mathf.Approximately(candidateRange, bestRange) && candidateMinRange > bestMinRange))
            {
                bestIdx = i;
                bestRange = candidateRange;
                bestMinRange = candidateMinRange;
            }
        }

        return bestIdx >= 0 ? hardpoints[bestIdx].AttackVerb : null;
    }

    private static bool PassesSharedFilters(IAttackTarget target, Pawn owner, Map map, Lord lord)
    {
        Thing thing = target?.Thing;
        if (thing == null || thing == owner || thing.Destroyed || !thing.Spawned || thing.Map != map || !owner.HostileTo(thing))
        {
            return false;
        }

        if (lord != null && !lord.LordJob.ValidateAttackTarget(owner, thing))
        {
            return false;
        }

        if (target.ThreatDisabled(owner) || !AttackTargetFinder.IsAutoTargetable(target))
        {
            return false;
        }

        if (owner.def.race != null && (int)owner.def.race.intelligence >= 2)
        {
            CompExplosive explosive = thing.TryGetComp<CompExplosive>();
            if (explosive != null && explosive.wickStarted)
            {
                return false;
            }
        }

        if (target is Pawn pawn && !pawn.IsCombatant() && !GenSight.LineOfSightToThing(owner.Position, pawn, map))
        {
            return false;
        }

        return !ShouldRejectFoggedTarget(thing, owner);
    }

    private static bool ShouldRejectFoggedTarget(Thing target, Pawn owner)
    {
        if (!owner.IsColonist && owner.Faction != Faction.OfPlayer)
        {
            return false;
        }

        Map map = target.Map;
        if (target.def.size.x == 1 && target.def.size.z == 1)
        {
            return target.Position.Fogged(map);
        }

        foreach (IntVec3 cell in target.OccupiedRect())
        {
            if (!cell.Fogged(map))
            {
                return false;
            }
        }

        return true;
    }

    private bool CanWeaponTarget(
        MechHardpoint hp,
        IAttackTarget target,
        Pawn owner,
        Map map,
        ref sbyte smokeBlockedLos,
        ref sbyte unrestrictedLos)
    {
        Thing thing = target.Thing;
        if (hp.isEmp && target is Pawn pawn && pawn.RaceProps.IsFlesh)
        {
            return false;
        }

        if (hp.isIncendiary && thing.IsBurning())
        {
            return false;
        }

        if (hp.projectileFliesOverhead)
        {
            RoofDef roof = thing.Position.GetRoof(map);
            if (roof != null && roof.isThickRoof)
            {
                return false;
            }
        }

        if (!hp.projectileFliesOverhead)
        {
            if (hp.ignoresBlindSmoke)
            {
                if (unrestrictedLos == 0)
                {
                    unrestrictedLos = owner.CanSee(thing) ? (sbyte)1 : (sbyte)-1;
                }

                if (unrestrictedLos < 0)
                {
                    return false;
                }
            }
            else
            {
                if (smokeBlockedLos == 0)
                {
                    smokeBlockedLos = noBlindSmokeValidator(owner.Position)
                        && noBlindSmokeValidator(thing.Position)
                        && owner.CanSee(thing, noBlindSmokeValidator)
                            ? (sbyte)1
                            : (sbyte)-1;
                }

                if (smokeBlockedLos < 0)
                {
                    return false;
                }
            }
        }

        return hp.CanEngageTarget(thing, owner, map);
    }

    private static float GetShootingTargetScore(IAttackTarget target, MechHardpoint hp, Pawn owner, Map map)
    {
        Thing thing = target.Thing;
        float score = 60f;
        score -= Mathf.Min((thing.Position - owner.Position).LengthHorizontal, 40f);
        if (target.TargetCurrentlyAimingAt == owner)
        {
            score += 10f;
        }

        if (hp.lastAttackedTarget.HasThing
            && hp.lastAttackedTarget.Thing == thing
            && Find.TickManager.TicksGame - hp.lastAttackTargetTick <= RecentTargetTicks)
        {
            score += 40f;
        }

        score -= CoverUtility.CalculateOverallBlockChance(thing.Position, owner.Position, map) * 10f;
        if (target is Pawn pawn)
        {
            if (!pawn.IsCombatant())
            {
                score -= 50f;
            }
            else if (pawn.DevelopmentalStage.Juvenile())
            {
                score -= 25f;
            }

            Verb verb = hp.AttackVerb;
            if (verb.verbProps.ai_TargetHasRangedAttackScoreOffset != 0f
                && pawn.CurrentEffectiveVerb?.verbProps.Ranged == true)
            {
                score += verb.verbProps.ai_TargetHasRangedAttackScoreOffset;
            }

            if (pawn.Downed)
            {
                score -= 50f;
            }
        }

        // 原版锥形友伤评分对机械体直接返回 0；这里只保留实际生效的爆炸半径修正。
        score += FriendlyFireBlastRadiusTargetScoreOffset(target, hp.AttackVerb, owner, map);
        return score * target.TargetPriorityFactor;
    }

    private static float FriendlyFireBlastRadiusTargetScoreOffset(IAttackTarget target, Verb verb, Pawn owner, Map map)
    {
        float radius = verb.verbProps.ai_AvoidFriendlyFireRadius;
        if (radius <= 0f)
        {
            return 0f;
        }

        Thing targetThing = target.Thing;
        IntVec3 position = targetThing.Position;
        int cellCount = GenRadial.NumCellsInRadius(radius);
        float score = 0f;
        for (int i = 0; i < cellCount; i++)
        {
            IntVec3 cell = position + GenRadial.RadialPattern[i];
            if (!cell.InBounds(map))
            {
                continue;
            }

            bool checkLineOfSight = true;
            List<Thing> things = cell.GetThingList(map);
            for (int j = 0; j < things.Count; j++)
            {
                Thing thing = things[j];
                if (!(thing is IAttackTarget) || thing == targetThing)
                {
                    continue;
                }

                if (checkLineOfSight)
                {
                    if (!GenSight.LineOfSight(position, cell, map, skipFirstCell: true))
                    {
                        break;
                    }

                    checkLineOfSight = false;
                }

                float offset = thing == owner
                    ? 40f
                    : thing is Pawn pawn
                        ? pawn.def.race.Animal ? 7f : 18f
                        : 10f;
                score += owner.HostileTo(thing) ? offset * 0.6f : -offset;
            }
        }

        return score;
    }

    private int SelectCandidateIndex(
        List<IAttackTarget> candidates,
        int candidateCount,
        int hardpointCount,
        int hardpointIndex,
        int selectedCount)
    {
        bool hasUnselected = false;
        for (int i = 0; i < candidateCount; i++)
        {
            if (candidateScores[i * hardpointCount + hardpointIndex] != float.MinValue
                && !ContainsSelectedTarget(candidates[i].Thing, selectedCount))
            {
                hasUnselected = true;
                break;
            }
        }

        int bestIndex = -1;
        float bestScore = float.MinValue;
        for (int i = 0; i < candidateCount; i++)
        {
            float score = candidateScores[i * hardpointCount + hardpointIndex];
            if (score == float.MinValue || (hasUnselected && ContainsSelectedTarget(candidates[i].Thing, selectedCount)))
            {
                continue;
            }

            if (bestIndex < 0 || score > bestScore)
            {
                bestIndex = i;
                bestScore = score;
            }
        }

        if (bestIndex < 0 || bestScore < 1f)
        {
            return bestIndex;
        }

        float minimumScore = bestScore - 30f;
        float totalWeight = 0f;
        for (int i = 0; i < candidateCount; i++)
        {
            float score = candidateScores[i * hardpointCount + hardpointIndex];
            if (score < minimumScore || (hasUnselected && ContainsSelectedTarget(candidates[i].Thing, selectedCount)))
            {
                continue;
            }

            totalWeight += Mathf.InverseLerp(minimumScore, bestScore, score);
        }

        float choice = Rand.Value * totalWeight;
        for (int i = 0; i < candidateCount; i++)
        {
            float score = candidateScores[i * hardpointCount + hardpointIndex];
            if (score < minimumScore || (hasUnselected && ContainsSelectedTarget(candidates[i].Thing, selectedCount)))
            {
                continue;
            }

            choice -= Mathf.InverseLerp(minimumScore, bestScore, score);
            if (choice <= 0f)
            {
                return i;
            }
        }

        return bestIndex;
    }

    private void AddSelectedTarget(Thing target, ref int selectedCount)
    {
        if (target == null || ContainsSelectedTarget(target, selectedCount))
        {
            return;
        }

        selectedTargets[selectedCount++] = target;
    }

    private bool ContainsSelectedTarget(Thing target, int selectedCount)
    {
        for (int i = 0; i < selectedCount; i++)
        {
            if (selectedTargets[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearSelectedTargets(int selectedCount)
    {
        for (int i = 0; i < selectedCount; i++)
        {
            selectedTargets[i] = null;
        }
    }

    private bool CellHasNoBlindSmoke(IntVec3 cell)
    {
        return !cell.AnyGas(gasMap, GasType.BlindSmoke);
    }

    private void EnsureHardpointCapacity(int hardpointCapacity)
    {
        if (automaticHardpoints.Length < hardpointCapacity)
        {
            automaticHardpoints = new bool[hardpointCapacity];
        }

        if (selectedTargets.Length < hardpointCapacity)
        {
            selectedTargets = new Thing[hardpointCapacity];
        }
    }

    private void EnsureCandidateScoreCapacity(int required)
    {
        if (candidateScores.Length >= required)
        {
            return;
        }

        int capacity = candidateScores.Length == 0 ? InitialCandidateCapacity : candidateScores.Length;
        while (capacity < required)
        {
            capacity *= 2;
        }

        candidateScores = new float[capacity];
    }
}
