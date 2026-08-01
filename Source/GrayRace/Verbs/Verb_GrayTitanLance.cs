using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.Verbs;

public sealed class TitanLanceExtension : DefModExtension
{
    public float minPulseDamage;
    public float maxPulseDamage;
    public float armorPenetration;
    public SimpleCurve damageRampCurve;
    public SimpleCurve distanceDamageFactorCurve;

    private ThingDef parentWeaponDef;

    public override void ResolveReferences(Def parentDef)
    {
        base.ResolveReferences(parentDef);
        parentWeaponDef = parentDef as ThingDef;
    }

    public override IEnumerable<string> ConfigErrors()
    {
        if (minPulseDamage <= 0f || maxPulseDamage <= 0f)
        {
            yield return "Titan lance pulse damage values must be greater than zero.";
        }
        else if (maxPulseDamage < minPulseDamage)
        {
            yield return "Titan lance maxPulseDamage must be greater than or equal to minPulseDamage.";
        }

        if (!Mathf.Approximately(armorPenetration, 1f))
        {
            yield return "Titan lance armorPenetration must be exactly 1.";
        }

        foreach (string error in ValidateDamageRampCurve())
        {
            yield return error;
        }

        VerbProperties verbProps = FindTitanLanceVerbProperties();
        if (verbProps == null)
        {
            yield return "Titan lance extension must be attached to a weapon with Verb_GrayTitanLance.";
            yield break;
        }

        if (verbProps.burstShotCount != 24 || verbProps.ticksBetweenBurstShots != 15
            || verbProps.burstShotCount * verbProps.ticksBetweenBurstShots != 360)
        {
            yield return "Titan lance must define 24 pulses at 15 ticks per pulse for a 360-tick beam.";
        }

        if (verbProps.beamDamageDef == null || verbProps.beamDamageDef.defName != "GR_Laser")
        {
            yield return "Titan lance beamDamageDef must reference GR_Laser.";
        }

        if (verbProps.beamMoteDef == null
            || verbProps.beamMoteDef.defName != "Mote_GR_TitanLance"
            || verbProps.beamMoteDef.thingClass == null
            || !typeof(MoteDualAttached).IsAssignableFrom(verbProps.beamMoteDef.thingClass))
        {
            yield return "Titan lance beamMoteDef must reference Mote_GR_TitanLance using MoteDualAttached.";
        }

        foreach (string error in ValidateDistanceCurve(verbProps))
        {
            yield return error;
        }
    }

    private VerbProperties FindTitanLanceVerbProperties()
    {
        List<VerbProperties> verbs = parentWeaponDef?.Verbs;
        if (verbs == null)
        {
            return null;
        }

        for (int i = 0; i < verbs.Count; i++)
        {
            VerbProperties current = verbs[i];
            if (current?.verbClass == typeof(Verb_GrayTitanLance))
            {
                return current;
            }
        }

        return null;
    }

    private IEnumerable<string> ValidateDamageRampCurve()
    {
        if (damageRampCurve == null || damageRampCurve.PointsCount < 2)
        {
            yield return "Titan lance damageRampCurve must contain at least two points.";
            yield break;
        }

        List<CurvePoint> points = damageRampCurve.Points;
        if (!Mathf.Approximately(points[0].x, 0f) || !Mathf.Approximately(points[0].y, 0f)
            || !Mathf.Approximately(points[points.Count - 1].x, 1f)
            || !Mathf.Approximately(points[points.Count - 1].y, 1f))
        {
            yield return "Titan lance damageRampCurve must cover (0, 0) through (1, 1).";
        }

        for (int i = 0; i < points.Count; i++)
        {
            CurvePoint point = points[i];
            if (point.y < 0f || point.y > 1f)
            {
                yield return "Titan lance damageRampCurve factors must stay between 0 and 1.";
                yield break;
            }

            if (i > 0 && (point.x <= points[i - 1].x || point.y < points[i - 1].y))
            {
                yield return "Titan lance damageRampCurve points must increase in X without decreasing in Y.";
                yield break;
            }
        }
    }

    private IEnumerable<string> ValidateDistanceCurve(VerbProperties verbProps)
    {
        if (distanceDamageFactorCurve == null || distanceDamageFactorCurve.PointsCount < 2)
        {
            yield return "Titan lance distanceDamageFactorCurve must contain at least two points.";
            yield break;
        }

        List<CurvePoint> points = distanceDamageFactorCurve.Points;
        if (points[0].x > verbProps.minRange || points[points.Count - 1].x < verbProps.range)
        {
            yield return "Titan lance distanceDamageFactorCurve must cover the complete configured range.";
        }

        for (int i = 0; i < points.Count; i++)
        {
            CurvePoint point = points[i];
            if (point.y <= 0f || point.y > 1f)
            {
                yield return "Titan lance distance damage factors must be greater than 0 and no greater than 1.";
                yield break;
            }

            if (i > 0 && point.x <= points[i - 1].x)
            {
                yield return "Titan lance distanceDamageFactorCurve points must strictly increase in X.";
                yield break;
            }
        }
    }
}

public sealed class Verb_GrayTitanLance : Verb
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
