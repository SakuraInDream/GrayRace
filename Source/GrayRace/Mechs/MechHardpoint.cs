using System;
using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Defs;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.Mechs;

/// <summary>
/// 单个武器挂载点的完整状态。密封类，由 GrayMechWeaponrySystem 持有和驱动。
/// 搜索职责分离到 <see cref="HardpointSearcher"/>。
/// </summary>
public sealed class MechHardpoint
{
    public enum State : byte
    {
        Idle,
        WarmingUp,
        Firing,
        Cooling,
    }

    // --- 配置（Rebuild 时写入，运行时只读）---
    public GRMechModuleDef module;
    public GRMechSectionSlotDef sectionSlot;
    public GRMechSectionLayoutDef layout;
    public GRMechSlotEntry slot;
    public string slotKey;
    public ThingWithComps gun;
    public Thing caster;

    // --- 运行时状态 ---
    public State state;
    public LocalTargetInfo currentTarget = LocalTargetInfo.Invalid;
    public LocalTargetInfo lastAttackedTarget = LocalTargetInfo.Invalid;
    public int lastAttackTargetTick;
    public int warmupTicksLeft;
    public int cooldownTicksLeft;
    public int baseWarmupTicks = 1;
    public float curRotation;

    // --- 瞄准视觉/音效（仅 WarmingUp 期间有效，由 TurretBankSystem 驱动）---
    private Sustainer aimSustainer;
    private Effecter aimEffecter;
    private Mote aimLineMote;
    private Mote aimChargeMote;
    private Mote aimTargetMote;
    private bool needsEffectInit;

    // --- 快捷访问 ---
    public Verb AttackVerb => gun?.TryGetComp<CompEquippable>()?.PrimaryVerb;
    public Verb CurrentEffectiveVerb => AttackVerb;

    public bool IsActivelyEngaging => state == State.WarmingUp || state == State.Firing;
    public bool IsAiming => currentTarget.IsValid && state != State.Idle;
    public bool WarmingUp => state == State.WarmingUp && warmupTicksLeft > 0;
    public bool TurretDestroyed
    {
        get
        {
            if (caster is Pawn pawn
                && AttackVerb?.verbProps?.linkedBodyPartsGroup != null
                && AttackVerb.verbProps.ensureLinkedBodyPartsGroupAlwaysUsable
                && PawnCapacityUtility.CalculateNaturalPartsAverageEfficiency(pawn.health.hediffSet, AttackVerb.verbProps.linkedBodyPartsGroup) <= 0f)
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 初始化内部 gun 的 Verb：统一清零 warmupTime，缓存原始 warmup ticks，关联 caster。
    /// </summary>
    public void Setup(Thing casterPawn, Action castCompleteCallback)
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

        List<Verb> allVerbs = equip.AllVerbs;
        for (int i = 0; i < allVerbs.Count; i++)
        {
            Verb verb = allVerbs[i];
            baseWarmupTicks = Mathf.Max(1, verb.verbProps.warmupTime.SecondsToTicks());

            VerbProperties cloned = verb.verbProps.MemberwiseClone();
            cloned.warmupTime = 0f;
            verb.verbProps = cloned;

            verb.caster = casterPawn;
            verb.castCompleteCallback = castCompleteCallback;
        }
    }

    public bool CanEngageTarget(LocalTargetInfo target, Pawn owner, Map map)
    {
        Verb verb = AttackVerb;
        if (verb == null || owner == null || !verb.Available() || TurretDestroyed || !target.IsValid || !target.HasThing)
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
        needsEffectInit = true;
    }

    public void NotifyCastStarted()
    {
        lastAttackedTarget = currentTarget;
        lastAttackTargetTick = Find.TickManager.TicksGame;
        state = State.Firing;
        CleanupWarmupEffects();
    }

    public void NotifyCastComplete(Pawn owner)
    {
        Verb verb = AttackVerb;
        cooldownTicksLeft = verb != null && owner != null
            ? verb.verbProps.AdjustedCooldownTicks(verb, owner)
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
        CleanupWarmupEffects();
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
        CleanupWarmupEffects();

        if (gun != null && !gun.Destroyed)
        {
            gun.Destroy();
        }

        gun = null;
    }

    /// <summary>
    /// 每 tick 由 TurretBankSystem 在 WarmingUp 分支调用，维护瞄准线 / 蓄能 mote / 音效。
    /// 首次调用时惰性创建特效；<paramref name="muzzleOffset"/> 是相对于 caster.DrawPos 的炮口本地偏移。
    /// </summary>
    public void TickWarmupEffects(Vector3 muzzleOffset, bool stunned)
    {
        if (caster == null || caster.Map == null || !currentTarget.IsValid)
        {
            return;
        }

        if (needsEffectInit)
        {
            InitWarmupEffects(muzzleOffset);
            needsEffectInit = false;
        }

        Verb verb = AttackVerb;
        if (verb == null)
        {
            return;
        }

        Map map = caster.Map;
        Vector3 muzzlePos = caster.DrawPos + muzzleOffset;
        Vector3 aimDir = AimDirection(muzzlePos);
        float rot = aimDir.AngleFlat();

        aimSustainer?.Maintain();
        aimEffecter?.EffectTick(new TargetInfo(caster), currentTarget.ToTargetInfo(map));

        if (aimLineMote != null)
        {
            aimLineMote.paused = stunned;
            aimLineMote.Maintain();
            Vector3 tip = AimLineTip(verb, muzzlePos, aimDir);
            IntVec3 cell = tip.ToIntVec3();
            ((MoteDualAttached)aimLineMote).UpdateTargets(
                new TargetInfo(caster),
                new TargetInfo(cell, map),
                muzzleOffset,
                tip - cell.ToVector3Shifted());
        }

        if (aimTargetMote != null)
        {
            aimTargetMote.paused = stunned;
            aimTargetMote.exactPosition = currentTarget.CenterVector3;
            aimTargetMote.exactRotation = rot;
            aimTargetMote.Maintain();
        }

        if (aimChargeMote != null)
        {
            aimChargeMote.paused = stunned;
            aimChargeMote.exactRotation = rot;
            aimChargeMote.exactPosition = muzzlePos + aimDir * verb.verbProps.aimingChargeMoteOffset;
            aimChargeMote.Maintain();
        }
    }

    private void InitWarmupEffects(Vector3 muzzleOffset)
    {
        Verb verb = AttackVerb;
        if (verb == null || caster == null || caster.Map == null || !currentTarget.IsValid)
        {
            return;
        }

        VerbProperties vp = verb.verbProps;
        Map map = caster.Map;
        Vector3 muzzlePos = caster.DrawPos + muzzleOffset;
        Vector3 aimDir = AimDirection(muzzlePos);

        if (vp.soundAiming != null)
        {
            SoundInfo info = SoundInfo.InMap(new TargetInfo(caster), MaintenanceType.PerTick);
            if (caster is Pawn casterPawn)
            {
                info.pitchFactor = 1f / casterPawn.GetStatValue(StatDefOf.AimingDelayFactor);
            }

            aimSustainer = vp.soundAiming.TrySpawnSustainer(info);
        }

        if (vp.warmupEffecter != null)
        {
            aimEffecter = vp.warmupEffecter.Spawn(caster, map);
            aimEffecter.Trigger(caster, currentTarget.ToTargetInfo(map));
        }

        if (vp.aimingLineMote != null)
        {
            Vector3 tip = AimLineTip(verb, muzzlePos, aimDir);
            IntVec3 cell = tip.ToIntVec3();
            aimLineMote = MoteMaker.MakeInteractionOverlay(
                vp.aimingLineMote,
                new TargetInfo(caster),
                new TargetInfo(cell, map),
                muzzleOffset,
                tip - cell.ToVector3Shifted());
        }

        if (vp.aimingChargeMote != null)
        {
            aimChargeMote = MoteMaker.MakeStaticMote(muzzlePos, map, vp.aimingChargeMote, 1f, makeOffscreen: true);
        }

        if (vp.aimingTargetMote != null)
        {
            aimTargetMote = MoteMaker.MakeStaticMote(currentTarget.CenterVector3, map, vp.aimingTargetMote, 1f, makeOffscreen: true);
            if (aimTargetMote != null)
            {
                aimTargetMote.exactRotation = aimDir.AngleFlat();
            }
        }

        if (vp.aimingTargetEffecter != null)
        {
            aimEffecter?.Cleanup();
            aimEffecter = vp.aimingTargetEffecter.Spawn(new TargetInfo(caster), currentTarget.ToTargetInfo(map));
        }
    }

    private void CleanupWarmupEffects()
    {
        aimEffecter?.Cleanup();
        aimEffecter = null;
        // Sustainer 与 Mote 均通过停止 Maintain 自然淡出，不需要显式销毁。
        aimSustainer = null;
        aimLineMote = null;
        aimChargeMote = null;
        aimTargetMote = null;
        needsEffectInit = false;
    }

    private Vector3 AimDirection(Vector3 muzzlePos)
    {
        Vector3 result = currentTarget.CenterVector3 - muzzlePos;
        result.y = 0f;
        if (result.sqrMagnitude > 0.0001f)
        {
            result.Normalize();
        }
        else
        {
            result = Vector3.forward;
        }

        return result;
    }

    private Vector3 AimLineTip(Verb verb, Vector3 muzzlePos, Vector3 aimDir)
    {
        float? fixedLen = verb.verbProps.aimingLineMoteFixedLength;
        if (fixedLen.HasValue)
        {
            return muzzlePos + aimDir * fixedLen.Value;
        }

        return currentTarget.CenterVector3;
    }
}
