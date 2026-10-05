using System;
using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.DefModExtensions;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.Verbs;

public class Verb_GrayTitanLance : Verb
{
    private MoteDualAttached beamMote;
    private Sustainer beamSustainer;
    private TitanLanceExtension extension;

    protected override int ShotsPerBurst => BurstShotCount;

    private TitanLanceExtension Extension
        => extension ??= EquipmentSource?.def.GetModExtension<TitanLanceExtension>();

    public override float? AimAngleOverride
    {
        get
        {
            if (state != VerbState.Bursting || caster == null || !currentTarget.HasThing)
            {
                return null;
            }

            return (currentTarget.CenterVector3 - caster.DrawPos).AngleFlat();
        }
    }

    public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
    {
        return targ.HasThing && base.CanHitTargetFrom(root, targ);
    }

    public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
    {
        return target.HasThing && base.ValidateTarget(target, showMessages);
    }

    public override bool TryStartCastOn(
        LocalTargetInfo castTarg,
        LocalTargetInfo destTarg,
        bool surpriseAttack = false,
        bool canHitNonTargetPawns = true,
        bool preventFriendlyFire = false,
        bool nonInterruptingSelfCast = false)
    {
        return castTarg.HasThing
            && base.TryStartCastOn(
                castTarg,
                destTarg,
                surpriseAttack,
                canHitNonTargetPawns,
                preventFriendlyFire,
                nonInterruptingSelfCast);
    }

    public override void WarmupComplete()
    {
        if (!CanContinueBeam())
        {
            base.Reset();
            EndEffects();
            return;
        }

        burstShotsLeft = ShotsPerBurst;
        // Pawn ticks its stance before its equipment, so compensate for the same-tick VerbTick.
        ticksToNextBurstShot = TicksBetweenBurstShots + 1;
        state = VerbState.Bursting;

        Find.BattleLog.Add(new BattleLogEntry_RangedFire(
            caster,
            currentTarget.Thing,
            EquipmentSource?.def,
            null,
            burst: true));

        EnsureEffects();
        UpdateEffects();

        if (CasterPawn?.stances != null && !NonInterruptingSelfCast)
        {
            CasterPawn.stances.SetStance(new Stance_Cooldown(TicksBetweenBurstShots + 1, currentTarget, this));
        }
    }

    public override void BurstingTick()
    {
        if (state != VerbState.Bursting)
        {
            EndEffects();
            return;
        }

        if (!CanContinueBeam())
        {
            InterruptBeamWithCooldown();
            return;
        }

        EnsureEffects();
        UpdateEffects();
    }

    protected override bool TryCastShot()
    {
        TitanLanceExtension settings = Extension;
        if (settings == null || !CanContinueBeam())
        {
            return false;
        }

        Thing target = currentTarget.Thing;
        int pulseIndex = ShotsPerBurst - burstShotsLeft;
        float progress = ShotsPerBurst > 1 ? (float)pulseIndex / (ShotsPerBurst - 1) : 1f;
        float ramp = Mathf.Clamp01(settings.damageRampCurve.Evaluate(progress));
        float pulseDamage = Mathf.Lerp(settings.minPulseDamage, settings.maxPulseDamage, ramp);
        float distance = caster.Position.DistanceTo(target.Position);
        float distanceFactor = settings.distanceDamageFactorCurve.Evaluate(distance);
        float weaponDamageFactor = EquipmentSource?.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier) ?? 1f;
        float amount = pulseDamage * distanceFactor * weaponDamageFactor;
        float angle = (currentTarget.Cell - caster.Position).AngleFlat;

        DamageInfo damageInfo = new(
            verbProps.beamDamageDef,
            amount,
            settings.armorPenetration,
            angle,
            caster,
            null,
            EquipmentSource?.def,
            DamageInfo.SourceCategory.ThingOrUnknown,
            target);

        lastShotTick = Find.TickManager.TicksGame;
        target.TakeDamage(damageInfo);
        return true;
    }

    public override void Reset()
    {
        if (state == VerbState.Bursting)
        {
            InterruptBeamWithCooldown();
            return;
        }

        base.Reset();
        EndEffects();
    }

    public override void Notify_EquipmentLost()
    {
        base.Notify_EquipmentLost();
        if (state == VerbState.Bursting)
        {
            InterruptBeamWithCooldown();
        }
        else
        {
            EndEffects();
        }
    }

    private bool CanContinueBeam()
    {
        Thing target = currentTarget.HasThing ? currentTarget.Thing : null;
        if (caster == null
            || caster.Destroyed
            || !caster.Spawned
            || target == null
            || target.Destroyed
            || !target.Spawned
            || target.Map != caster.Map)
        {
            return false;
        }

        if (CasterPawn is Pawn pawn
            && (pawn.Dead || pawn.Downed || pawn.stances == null || pawn.stances.stunner.Stunned))
        {
            return false;
        }

        float distance = caster.Position.DistanceTo(target.Position);
        if (distance < verbProps.minRange || distance > EffectiveRange)
        {
            return false;
        }

        return TryFindShootLineFromTo(caster.Position, currentTarget, out _);
    }

    private void EnsureEffects()
    {
        if (!CanContinueBeam())
        {
            return;
        }

        if ((beamMote == null || beamMote.Destroyed) && verbProps.beamMoteDef != null)
        {
            beamMote = MoteMaker.MakeInteractionOverlay(
                verbProps.beamMoteDef,
                new TargetInfo(caster),
                new TargetInfo(currentTarget.Thing));
            beamMote.Maintain();
        }

        if ((beamSustainer == null || beamSustainer.Ended) && verbProps.soundCastBeam != null)
        {
            beamSustainer = verbProps.soundCastBeam.TrySpawnSustainer(
                SoundInfo.InMap(caster, MaintenanceType.PerTick));
        }
    }

    private void UpdateEffects()
    {
        if (beamMote != null && !beamMote.Destroyed)
        {
            beamMote.UpdateTargets(
                new TargetInfo(caster),
                new TargetInfo(currentTarget.Thing),
                Vector3.zero,
                Vector3.zero);
            beamMote.Maintain();
        }

        if (beamSustainer != null && !beamSustainer.Ended)
        {
            beamSustainer.Maintain();
        }
    }

    private void InterruptBeamWithCooldown()
    {
        LocalTargetInfo interruptedTarget = currentTarget;
        Action completion = castCompleteCallback;
        bool applyPawnCooldown = CasterPawn?.stances != null && !NonInterruptingSelfCast;
        int cooldownTicks = applyPawnCooldown
            ? verbProps.AdjustedCooldownTicks(this, CasterPawn)
            : 0;

        base.Reset();
        EndEffects();

        if (applyPawnCooldown)
        {
            CasterPawn.stances.SetStance(new Stance_Cooldown(cooldownTicks, interruptedTarget, this));
        }

        completion?.Invoke();
    }

    private void EndEffects()
    {
        beamMote = null;
        if (beamSustainer != null && !beamSustainer.Ended)
        {
            beamSustainer.End();
        }

        beamSustainer = null;
    }
}
