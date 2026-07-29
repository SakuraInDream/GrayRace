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
    public GRMechSectionSlotDef sectionSlot;
    public GRMechSectionLayoutDef layout;
    public GRMechSlotEntry slot;
    public string slotKey;
    public ThingWithComps gun;
    public Thing caster;

    public State state;
    public LocalTargetInfo currentTarget = LocalTargetInfo.Invalid;
    public LocalTargetInfo lastAttackedTarget = LocalTargetInfo.Invalid;
    public int lastAttackTargetTick;
    public int warmupTicksLeft;
    public int cooldownTicksLeft;
    public int baseWarmupTicks = 1;
    public float curRotation;
    public float deploymentProgress;
    public int visualIndex;
    public Vector3 floatingOffset;

    public Verb AttackVerb => gun?.TryGetComp<CompEquippable>()?.PrimaryVerb;
    public Verb CurrentEffectiveVerb => AttackVerb;

    public bool IsActivelyEngaging => state == State.WarmingUp || state == State.Firing;
    public bool IsAiming => currentTarget.IsValid && state != State.Idle;
    public bool WarmingUp => state == State.WarmingUp && warmupTicksLeft > 0;
    public bool IsFullyDeployed => deploymentProgress >= 1f;

    public void TickDeployment(bool deployRequested)
    {
        int duration = Mathf.Max(1, module?.weaponDeploymentTicks ?? 1);
        float target = deployRequested ? 1f : 0f;
        deploymentProgress = Mathf.MoveTowards(deploymentProgress, target, 1f / duration);
    }

    public void Setup(Thing casterPawn)
    {
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

        float aimingFactor = 1f;
        if (casterPawn is Pawn pawn)
        {
            aimingFactor = pawn.GetStatValue(StatDefOf.AimingDelayFactor);
        }

        List<Verb> allVerbs = equip.AllVerbs;
        for (int i = 0; i < allVerbs.Count; i++)
        {
            Verb verb = allVerbs[i];
            baseWarmupTicks = Mathf.Max(1, (verb.verbProps.warmupTime * aimingFactor).SecondsToTicks());
            verb.caster = casterPawn;
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

        return verb.CanHitTargetFrom(owner.Position, target);
    }

    public void BeginWarmup(LocalTargetInfo target)
    {
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

    public void NotifyCastComplete()
    {
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

    public void TickVerb()
    {
        AttackVerb?.VerbTick();
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
        if (gun != null && !gun.Destroyed)
        {
            gun.Destroy();
        }

        gun = null;
    }

    public bool TryFindShootLineTo(LocalTargetInfo target, out ShootLine resultingLine)
    {
        Verb verb = AttackVerb;
        if (caster is Pawn pawn && verb != null && pawn.Spawned)
        {
            return verb.TryFindShootLineFromTo(pawn.Position, target, out resultingLine);
        }

        resultingLine = default;
        return false;
    }
}
