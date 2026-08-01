using System;
using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Defs;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Mechs;

public sealed class MechHardpoint
{
    public enum State : byte
    {
        Idle,
        WarmingUp,
        Firing,
        Cooling,
    }

    public GRMechModuleDef module;
    public GRMechSlotEntry slot;
    public ThingWithComps gun;
    public Thing caster;

    private Verb attackVerb;
    private bool hasDebugAnchorOverride;
    private Vector2 debugAnchorOverride;
    private int burstShotsLeft;
    private int ticksToNextBurstShot;
    private IntVec3[] forcedMissTargets = Array.Empty<IntVec3>();
    private int forcedMissTargetCount;
    private int forcedMissTargetIndex = -1;

    internal string SectionSlotId { get; set; }

    internal Vector2 BaseAnchor => slot?.hardpointAnchor ?? Vector2.zero;

    internal Vector2 EffectiveAnchor => hasDebugAnchorOverride ? debugAnchorOverride : BaseAnchor;

    internal bool HasDebugAnchorOverride => hasDebugAnchorOverride;

    public bool projectileFliesOverhead;
    public bool isIncendiary;
    public bool isEmp;
    public bool ignoresBlindSmoke;

    public State state;
    public LocalTargetInfo currentTarget = LocalTargetInfo.Invalid;
    public LocalTargetInfo lastAttackedTarget = LocalTargetInfo.Invalid;
    public int lastAttackTargetTick;
    public int warmupTicksLeft;
    public int cooldownTicksLeft;
    public int baseWarmupTicks = 1;
    public float curRotation;
    public float deploymentProgress;
    public Vector3 anchorPosition;
    public Vector3 swarmPosition;
    public Vector3 swarmVelocity;

    public Verb AttackVerb => attackVerb;

    public bool IsActivelyEngaging => state == State.WarmingUp || state == State.Firing;
    public bool IsBursting => burstShotsLeft > 0;
    public bool IsAiming => currentTarget.IsValid && state != State.Idle;
    public bool WarmingUp => state == State.WarmingUp && warmupTicksLeft > 0;
    public bool IsFullyDeployed => deploymentProgress >= 1f;

    internal void SetDebugAnchorOverride(Vector2 anchor)
    {
        debugAnchorOverride = anchor;
        hasDebugAnchorOverride = true;
    }

    internal void ClearDebugAnchorOverride()
    {
        debugAnchorOverride = default;
        hasDebugAnchorOverride = false;
    }

    public void TickDeployment(bool deployRequested)
    {
        int duration = Mathf.Max(1, module?.weaponDeploymentTicks ?? 1);
        float target = deployRequested ? 1f : 0f;
        deploymentProgress = Mathf.MoveTowards(deploymentProgress, target, 1f / duration);
    }

    public void Setup(Thing casterPawn)
    {
        CancelBurst();
        attackVerb = null;
        projectileFliesOverhead = false;
        isIncendiary = false;
        isEmp = false;
        ignoresBlindSmoke = false;

        if (gun == null)
        {
            return;
        }

        caster = casterPawn;

        CompEquippable equip = gun.TryGetComp<CompEquippable>();
        if (equip == null)
        {
            return;
        }

        attackVerb = equip.PrimaryVerb;
        projectileFliesOverhead = attackVerb?.ProjectileFliesOverhead() ?? false;
        isIncendiary = attackVerb?.IsIncendiary_Ranged() ?? false;
        isEmp = attackVerb?.IsEMP() ?? false;
        CompUniqueWeapon uniqueWeapon = gun.TryGetComp<CompUniqueWeapon>();
        ignoresBlindSmoke = uniqueWeapon?.IgnoreAccuracyMaluses ?? false;

        float aimingFactor = 1f;
        if (casterPawn is Pawn pawn)
        {
            aimingFactor = pawn.GetStatValue(StatDefOf.AimingDelayFactor);
        }

        List<Verb> allVerbs = equip.AllVerbs;
        for (int i = 0; i < allVerbs.Count; i++)
        {
            Verb verb = allVerbs[i];
            verb.caster = casterPawn;
        }

        if (attackVerb != null)
        {
            EnsureForcedMissTargetCapacity(Mathf.Max(1, attackVerb.BurstShotCount));
            baseWarmupTicks = Mathf.Max(1, (attackVerb.verbProps.warmupTime * aimingFactor).SecondsToTicks());
        }
    }

    public bool CanEngageTarget(LocalTargetInfo target, Pawn owner, Map map)
    {
        Verb verb = AttackVerb;
        if (verb == null || owner == null || !verb.Available() || !target.IsValid || !target.HasThing)
        {
            return false;
        }

        Thing t = target.Thing;
        if (t == null || t.Destroyed || !t.Spawned || t.Map != map)
        {
            return false;
        }

        if (IsOutOfRange(verb, target))
        {
            return false;
        }

        return verb.TryFindShootLineFromTo(owner.Position, target, out _, ignoreRange: true);
    }

    public void BeginWarmup(LocalTargetInfo target)
    {
        CancelBurst();
        currentTarget = target;
        warmupTicksLeft = baseWarmupTicks;
        state = State.WarmingUp;
    }

    public void NotifyCastStarted()
    {
        lastAttackedTarget = currentTarget;
        lastAttackTargetTick = Find.TickManager.TicksGame;
        state = State.Firing;
    }

    public void BeginBurst()
    {
        Verb verb = AttackVerb;
        burstShotsLeft = Mathf.Max(1, verb?.BurstShotCount ?? 1);
        ticksToNextBurstShot = 0;
        forcedMissTargetCount = 0;
        forcedMissTargetIndex = -1;
        EnsureForcedMissTargetCapacity(burstShotsLeft);
    }

    public bool TickBurst()
    {
        if (!IsBursting)
        {
            return false;
        }

        if (ticksToNextBurstShot > 0)
        {
            ticksToNextBurstShot--;
        }

        return ticksToNextBurstShot <= 0;
    }

    public void NotifyBurstShotFired()
    {
        if (!IsBursting)
        {
            return;
        }

        burstShotsLeft--;
        ticksToNextBurstShot = burstShotsLeft > 0
            ? Mathf.Max(0, AttackVerb?.TicksBetweenBurstShots ?? 0)
            : 0;
    }

    public void CancelBurst()
    {
        burstShotsLeft = 0;
        ticksToNextBurstShot = 0;
        forcedMissTargetCount = 0;
        forcedMissTargetIndex = -1;
    }

    public bool TryGetEvenDispersalForcedMissTarget(
        IntVec3 root,
        float radius,
        IntVec3 casterPosition,
        out IntVec3 target)
    {
        target = default;
        if (!IsBursting)
        {
            return false;
        }

        if (forcedMissTargetCount <= 0)
        {
            int count = burstShotsLeft;
            EnsureForcedMissTargetCapacity(count);

            float randomRotationOffset = Rand.Range(0f, 360f);
            float goldenRatio = (1f + Mathf.Pow(5f, 0.5f)) / 2f;
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f * i / goldenRatio;
                float elevation = Mathf.Acos(1f - 2f * (i + 0.5f) / count);
                int x = (int)(Mathf.Cos(angle) * Mathf.Sin(elevation) * radius);
                int z = (int)(Mathf.Cos(elevation) * radius);
                Vector3 offset = new Vector3(x, 0f, z).RotatedBy(randomRotationOffset);
                forcedMissTargets[i] = root + offset.ToIntVec3();
            }

            // Match Verse's descending sort followed by List.Pop().
            for (int i = 1; i < count; i++)
            {
                IntVec3 value = forcedMissTargets[i];
                int valueDistance = value.DistanceToSquared(casterPosition);
                int j = i - 1;
                while (j >= 0
                    && forcedMissTargets[j].DistanceToSquared(casterPosition) < valueDistance)
                {
                    forcedMissTargets[j + 1] = forcedMissTargets[j];
                    j--;
                }

                forcedMissTargets[j + 1] = value;
            }

            forcedMissTargetCount = count;
            forcedMissTargetIndex = count - 1;
        }

        if (forcedMissTargetIndex < 0)
        {
            forcedMissTargetCount = 0;
            return false;
        }

        target = forcedMissTargets[forcedMissTargetIndex--];
        if (forcedMissTargetIndex < 0)
        {
            forcedMissTargetCount = 0;
        }

        return true;
    }

    public void NotifyCastComplete()
    {
        CancelBurst();
        Verb verb = AttackVerb;
        cooldownTicksLeft = verb?.verbProps != null
            ? verb.verbProps.AdjustedCooldownTicks(verb, caster as Pawn)
            : 0;
        state = State.Cooling;
    }

    public bool HasInvalidCurrentTarget(Map map)
    {
        if (!currentTarget.HasThing)
        {
            return false;
        }

        Thing targetThing = currentTarget.Thing;
        return targetThing == null
            || targetThing.Destroyed
            || !targetThing.Spawned
            || targetThing.Map != map;
    }

    public bool TickCooldown()
    {
        if (cooldownTicksLeft > 0)
        {
            cooldownTicksLeft--;
        }

        return cooldownTicksLeft <= 0;
    }

    public void ResetCurrentTarget()
    {
        CancelBurst();
        currentTarget = LocalTargetInfo.Invalid;
        warmupTicksLeft = 0;
    }

    public void ResetTarget()
    {
        ResetCurrentTarget();
    }

    public void ResetToIdle()
    {
        ResetCurrentTarget();
        state = State.Idle;
    }

    public void DestroyGun()
    {
        CancelBurst();
        if (gun != null && !gun.Destroyed)
        {
            gun.Destroy();
        }

        gun = null;
        attackVerb = null;
        caster = null;
        projectileFliesOverhead = false;
        isIncendiary = false;
        isEmp = false;
        ignoresBlindSmoke = false;
        SectionSlotId = null;
        ClearDebugAnchorOverride();
    }

    public bool TryFindShootLineTo(LocalTargetInfo target, out ShootLine resultingLine)
    {
        Verb verb = AttackVerb;
        if (caster is Pawn pawn && verb != null && pawn.Spawned)
        {
            if (IsOutOfRange(verb, target))
            {
                resultingLine = default;
                return false;
            }

            return verb.TryFindShootLineFromTo(pawn.Position, target, out resultingLine, ignoreRange: true);
        }

        resultingLine = default;
        return false;
    }

    private bool IsOutOfRange(Verb verb, LocalTargetInfo target)
    {
        CellRect occupiedRect = target.HasThing
            ? target.Thing.OccupiedRect()
            : CellRect.SingleCell(target.Cell);
        return verb.OutOfRange(anchorPosition.ToIntVec3(), target, occupiedRect);
    }

    private void EnsureForcedMissTargetCapacity(int count)
    {
        if (forcedMissTargets.Length < count)
        {
            forcedMissTargets = new IntVec3[count];
        }
    }
}
