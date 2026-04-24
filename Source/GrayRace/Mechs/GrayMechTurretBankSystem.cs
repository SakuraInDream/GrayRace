using System;
using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.Defs;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Mechs;

[StaticConstructorOnStartup]
public class GrayMechTurretBankSystem : GrayMechSystemBase
{
    private static readonly CachedTexture ToggleTurretIcon = new("UI/Gizmos/ToggleTurret");
    private static readonly CachedTexture ForceTargetIcon = new("UI/Commands/Attack");
    private static readonly CachedTexture StopForceTargetIcon = new("UI/Commands/Halt");
    private static readonly Material ForcedTargetLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(1f, 0.5f, 0.5f));

    private const int TargetSearchInterval = 10;
    private const int TargetReticlePersistTicks = 36;
    private const int WeaponGraphicCombatGraceTicks = 300;
    private const float HardpointAltitudeLayer = 92f;
    private const float SelectionWarmupPieOffset = 0.2f;
    private const float ProjectileOriginForwardOffset = 0.12f;
    private const float IdleAimAngle = 143f;
    private const float IdleAimAngleWest = 217f;

    private MechHardpoint[] hardpoints = Array.Empty<MechHardpoint>();
    private int hardpointCount;
    private IFireControlDirector director;
    private GRMechCombatComputerModuleDef activeCombatComputer;
    private readonly HashSet<Thing> reservedTargets = new();
    private readonly Predicate<TargetInfo> forcedTargetValidator;
    private bool fireAtWill = true;
    private bool pendingRebuild;
    private LocalTargetInfo forcedTarget = LocalTargetInfo.Invalid;

    public GrayMechTurretBankSystem()
    {
        forcedTargetValidator = CanForceAttack;
    }

    private Pawn Pawn => pawn;
    private ThingWithComps ParentThing => Parent;
    private Thing ForcedTargetThing => forcedTarget.HasThing ? forcedTarget.Thing : null;

    public int HardpointCount => hardpointCount;
    public GRMechCombatComputerModuleDef ActiveCombatComputer
    {
        get
        {
            if (pendingRebuild)
            {
                TryRebuildFromLoadout();
            }

            return activeCombatComputer;
        }
    }

    public GRMechCombatComputerBehavior CombatComputerBehavior => ActiveCombatComputer?.behavior ?? GRMechCombatComputerBehavior.Undefined;

    public override void Notify_LoadoutChanged()
    {
        RebuildFromSnapshot(Pawn?.TryGetComp<CompGrayMechLoadout>()?.DesignSnapshot);
    }

    public override void PostExposeData()
    {
        Scribe_Values.Look(ref fireAtWill, "fireAtWill", defaultValue: true);
        Scribe_TargetInfo.Look(ref forcedTarget, "forcedTarget");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            pendingRebuild = true;
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        pendingRebuild = pendingRebuild || respawningAfterLoad || hardpointCount == 0;
        TryRebuildFromLoadout();
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        ClearHardpoints();
    }

    public override void CompTick()
    {
        Pawn p = Pawn;
        if (p == null)
        {
            return;
        }

        if (pendingRebuild)
        {
            TryRebuildFromLoadout();
        }

        RefreshForcedTargetState();
        if (!CanOperate(p))
        {
            if (!fireAtWill && !forcedTarget.IsValid)
            {
                ClearIdleTargets();
            }

            return;
        }

        reservedTargets.Clear();
        for (int i = 0; i < hardpointCount; i++)
        {
            MechHardpoint hp = hardpoints[i];
            if (hp.currentTarget.HasThing && hp.currentTarget.Thing != ForcedTargetThing)
            {
                reservedTargets.Add(hp.currentTarget.Thing);
            }
        }

        Map map = p.Map;
        for (int i = 0; i < hardpointCount; i++)
        {
            MechHardpoint hp = hardpoints[i];
            Verb verb = hp.AttackVerb;
            if (verb == null)
            {
                continue;
            }

            hp.TickVerb();

            if (hp.HasInvalidCurrentTarget(map))
            {
                hp.ResetCurrentTarget();
                if (hp.state == MechHardpoint.State.WarmingUp)
                {
                    hp.state = MechHardpoint.State.Idle;
                }
            }

            switch (hp.state)
            {
                case MechHardpoint.State.Firing:
                    if (verb.state != VerbState.Bursting)
                    {
                        hp.state = MechHardpoint.State.Cooling;
                    }

                    break;

                case MechHardpoint.State.WarmingUp:
                    if (!hp.CanEngageTarget(hp.currentTarget, p, map))
                    {
                        hp.ResetToIdle();
                        break;
                    }

                    hp.TickWarmupEffects(
                        ResolveHardpointOffset(hp).RotatedBy(p.Rotation),
                        p.stances?.stunner?.Stunned ?? false);

                    if (--hp.warmupTicksLeft <= 0)
                    {
                        if (TryStartHardpointCast(hp, p, baseDrawPos: p.DrawPos))
                        {
                            hp.NotifyCastStarted();
                        }
                        else
                        {
                            hp.ResetToIdle();
                        }
                    }

                    break;

                case MechHardpoint.State.Cooling:
                    if (hp.TickCooldown())
                    {
                        hp.state = MechHardpoint.State.Idle;
                    }

                    break;

                case MechHardpoint.State.Idle:
                    if (!p.IsHashIntervalTick(TargetSearchInterval))
                    {
                        break;
                    }

                    if (director != null && director.TryAssignTarget(hp, p, forcedTarget, fireAtWill, reservedTargets, out LocalTargetInfo newTarget))
                    {
                        hp.BeginWarmup(newTarget);
                        if (newTarget.HasThing && newTarget.Thing != ForcedTargetThing)
                        {
                            reservedTargets.Add(newTarget.Thing);
                        }
                    }

                    break;
            }

            if (hp.currentTarget.IsValid)
            {
                hp.curRotation = (GetTargetDrawPos(hp.currentTarget) - p.DrawPos).AngleFlat();
            }
        }
    }

    public bool TryGetTacticalVerb(Thing target, out Verb verb)
    {
        if (pendingRebuild)
        {
            TryRebuildFromLoadout();
        }

        Pawn p = Pawn;
        if (p == null || !CanOperate(p) || director == null)
        {
            verb = null;
            return false;
        }

        target = ResolveVerbSelectionTarget(target);
        verb = director.SelectTacticalVerb(hardpoints, hardpointCount, target);
        return verb != null;
    }

    public Verb ResolveAttackVerbForVanilla(Thing target)
    {
        if (!TryGetTacticalVerb(target, out Verb verb))
        {
            return null;
        }

        return verb;
    }

    public void RebuildFromSnapshot(GrayMechDesignSnapshot snapshot)
    {
        ClearHardpoints();
        pendingRebuild = false;
        activeCombatComputer = ResolveCombatComputer(snapshot);
        director = CreateDirector(activeCombatComputer);

        Pawn p = Pawn;
        if (p == null || snapshot?.modules == null)
        {
            return;
        }

        int weaponCount = 0;
        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            if (GrayMechDesignUtility.IsWeaponModule(snapshot.modules[i]?.module))
            {
                weaponCount++;
            }
        }

        if (weaponCount == 0)
        {
            return;
        }

        if (hardpoints.Length < weaponCount)
        {
            hardpoints = new MechHardpoint[weaponCount];
        }

        hardpointCount = 0;
        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            GrayMechModuleAssignment assignment = snapshot.modules[i];
            GRMechModuleDef module = assignment?.module;
            if (!GrayMechDesignUtility.IsWeaponModule(module))
            {
                continue;
            }

            ThingWithComps gun = MakeInternalGun(module);
            if (gun == null)
            {
                continue;
            }

            MechHardpoint hp = hardpoints[hardpointCount] ??= new MechHardpoint();
            hp.module = module;
            hp.sectionSlot = assignment.sectionSlot;
            hp.slotKey = assignment.slotKey;
            hp.gun = gun;
            GrayMechDesignUtility.TryResolveSlot(snapshot, assignment.sectionSlot, assignment.slotKey, out GRMechSlotEntry resolvedSlot, out GRMechSectionLayoutDef resolvedLayout);
            hp.layout = resolvedLayout;
            hp.slot = resolvedSlot;
            hp.state = MechHardpoint.State.Idle;
            hp.currentTarget = LocalTargetInfo.Invalid;
            hp.lastAttackedTarget = LocalTargetInfo.Invalid;
            hp.lastAttackTargetTick = 0;
            hp.cooldownTicksLeft = 0;
            hp.warmupTicksLeft = 0;
            hp.curRotation = 0f;
            hp.Setup(ParentThing, () => OnHardpointCastComplete(hp));

            hardpointCount++;
        }
    }

    private void OnHardpointCastComplete(MechHardpoint hp)
    {
        hp.NotifyCastComplete(Pawn);
    }

    private void TryRebuildFromLoadout()
    {
        if (!pendingRebuild)
        {
            return;
        }

        RebuildFromSnapshot(Pawn?.TryGetComp<CompGrayMechLoadout>()?.DesignSnapshot);
    }

    private void ClearHardpoints()
    {
        for (int i = 0; i < hardpointCount; i++)
        {
            hardpoints[i].DestroyGun();
        }

        hardpointCount = 0;
        reservedTargets.Clear();
    }

    private static IFireControlDirector CreateDirector(GRMechCombatComputerModuleDef cc)
    {
        if (cc?.fireControlClass != null)
        {
            return (IFireControlDirector)Activator.CreateInstance(cc.fireControlClass);
        }

        RangeBasedFireControl rfc = new();
        rfc.preferShortest = cc?.weaponSelection == GRMechCombatComputerWeaponSelectionMode.ShortestRange;
        return rfc;
    }

    private void OrderAttack(LocalTargetInfo target)
    {
        if (!target.IsValid)
        {
            ResetForcedTarget();
            return;
        }

        forcedTarget = target;
        for (int i = 0; i < hardpointCount; i++)
        {
            TryForceHardpointNow(hardpoints[i], forcedTarget);
        }
    }

    private void ResetForcedTarget()
    {
        LocalTargetInfo oldForcedTarget = forcedTarget;
        forcedTarget = LocalTargetInfo.Invalid;

        if (!oldForcedTarget.IsValid)
        {
            return;
        }

        for (int i = 0; i < hardpointCount; i++)
        {
            MechHardpoint hp = hardpoints[i];
            if (hp.currentTarget.IsValid && hp.currentTarget == oldForcedTarget)
            {
                ClearHardpointIfNotBursting(hp);
            }
        }

        if (!fireAtWill)
        {
            ClearIdleTargets();
        }
    }

    private void RefreshForcedTargetState()
    {
        if (!forcedTarget.IsValid || !forcedTarget.HasThing)
        {
            return;
        }

        Thing targetThing = forcedTarget.Thing;
        if (targetThing == null || targetThing.Destroyed || !targetThing.Spawned || !ParentThing.Spawned || targetThing.Map != ParentThing.MapHeld)
        {
            ResetForcedTarget();
        }
    }

    private void ClearIdleTargets()
    {
        for (int i = 0; i < hardpointCount; i++)
        {
            ClearHardpointIfNotBursting(hardpoints[i]);
        }
    }

    private static void ClearHardpointIfNotBursting(MechHardpoint hp)
    {
        if (hp.state != MechHardpoint.State.Firing)
        {
            hp.ResetToIdle();
        }
    }

    private void TryForceHardpointNow(MechHardpoint hp, LocalTargetInfo target)
    {
        if (hp.state == MechHardpoint.State.Firing || hp.state == MechHardpoint.State.Cooling)
        {
            return;
        }

        Verb verb = hp.AttackVerb;
        if (verb == null || !verb.Available())
        {
            return;
        }

        if (!hp.CanEngageTarget(target, Pawn, ParentThing.MapHeld))
        {
            return;
        }

        hp.BeginWarmup(target);
    }

    private bool TryStartHardpointCast(MechHardpoint hp, Pawn pawn, Vector3 baseDrawPos)
    {
        Verb verb = hp.AttackVerb;
        if (verb == null)
        {
            return false;
        }

        if (!TryGetProjectileOrigin(baseDrawPos, pawn, hp, out Vector3 origin))
        {
            origin = pawn.DrawPos;
        }

        TurretVerbDrawUtility.BeginProjectileOriginOverride(hp.caster, origin);
        try
        {
            return verb.TryStartCastOn(hp.currentTarget, surpriseAttack: false, canHitNonTargetPawns: true, preventFriendlyFire: false, nonInterruptingSelfCast: true);
        }
        finally
        {
            TurretVerbDrawUtility.EndProjectileOriginOverride(hp.caster);
        }
    }

    private Thing ResolveVerbSelectionTarget(Thing target)
    {
        if (IsValidVerbSelectionTarget(target))
        {
            return target;
        }

        if (IsValidVerbSelectionTarget(ForcedTargetThing))
        {
            return ForcedTargetThing;
        }

        Thing enemyTarget = Pawn?.mindState?.enemyTarget;
        return IsValidVerbSelectionTarget(enemyTarget) ? enemyTarget : null;
    }

    private bool IsValidVerbSelectionTarget(Thing target)
    {
        return target != null
            && !target.Destroyed
            && target.Spawned
            && target.Map == ParentThing.MapHeld
            && ParentThing.HostileTo(target);
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (pawn is { IsColonyMechPlayerControlled: true })
        {
            Command_Target forceAttackCommand = new();
            forceAttackCommand.defaultLabel = "CommandSetForceAttackTarget".Translate();
            forceAttackCommand.defaultDesc = "CommandSetForceAttackTargetDesc".Translate();
            forceAttackCommand.icon = ForceTargetIcon.Texture;
            forceAttackCommand.hotKey = KeyBindingDefOf.Misc4;
            forceAttackCommand.targetingParams = BuildForcedTargetingParameters();
            forceAttackCommand.action = OrderAttack;
            forceAttackCommand.onUpdate = DrawTargetingPreview;
            yield return forceAttackCommand;

            if (forcedTarget.IsValid)
            {
                Command_Action stopForceAttackCommand = new();
                stopForceAttackCommand.defaultLabel = "CommandStopForceAttack".Translate();
                stopForceAttackCommand.defaultDesc = "CommandStopForceAttackDesc".Translate();
                stopForceAttackCommand.icon = StopForceTargetIcon.Texture;
                stopForceAttackCommand.hotKey = KeyBindingDefOf.Misc5;
                stopForceAttackCommand.action = ResetForcedTarget;
                yield return stopForceAttackCommand;
            }

            Command_Toggle command = new();
            command.defaultLabel = "CommandToggleTurret".Translate();
            command.defaultDesc = "CommandToggleTurretDesc".Translate();
            command.isActive = () => fireAtWill;
            command.icon = ToggleTurretIcon.Texture;
            command.toggleAction = delegate
            {
                fireAtWill = !fireAtWill;
                if (!fireAtWill && !forcedTarget.IsValid)
                {
                    ClearIdleTargets();
                }
            };
            yield return command;
        }
    }

    private TargetingParameters BuildForcedTargetingParameters()
    {
        TargetingParameters targetingParameters = TargetingParameters.ForAttackAny();
        targetingParameters.validator = forcedTargetValidator;
        targetingParameters.canTargetLocations = false;
        return targetingParameters;
    }

    private bool CanForceAttack(TargetInfo target)
    {
        if (!target.IsValid || target.Thing == null || !ParentThing.Spawned)
        {
            return false;
        }

        Thing targetThing = target.Thing;
        if (targetThing.Destroyed || !targetThing.Spawned || targetThing.Map != ParentThing.MapHeld)
        {
            return false;
        }

        LocalTargetInfo localTarget = targetThing;
        for (int i = 0; i < hardpointCount; i++)
        {
            if (hardpoints[i].CanEngageTarget(localTarget, Pawn, ParentThing.MapHeld))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawTargetingPreview(LocalTargetInfo target)
    {
        if (!ParentThing.Spawned)
        {
            return;
        }

        for (int i = 0; i < hardpointCount; i++)
        {
            Verb attackVerb = hardpoints[i].AttackVerb;
            if (attackVerb != null)
            {
                attackVerb.DrawHighlight(target);
            }
        }
    }

    public override void PostDrawExtraSelectionOverlays()
    {
        if (Pawn != null)
        {
            DrawWarmupAimPies(Pawn, Pawn.DrawPos);
        }

        if (!forcedTarget.IsValid || !forcedTarget.HasThing)
        {
            DrawActiveTargetHighlights(Pawn);
            return;
        }

        Thing targetThing = forcedTarget.Thing;
        if (targetThing == null || targetThing.Destroyed || !targetThing.Spawned || targetThing.Map != ParentThing.MapHeld)
        {
            DrawActiveTargetHighlights(Pawn);
            return;
        }

        Vector3 a = ParentThing.TrueCenter();
        Vector3 b = targetThing.TrueCenter();
        b.y = AltitudeLayer.MetaOverlays.AltitudeFor();
        a.y = b.y;
        GenDraw.DrawLineBetween(a, b, ForcedTargetLineMat);
        DrawActiveTargetHighlights(Pawn);
    }

    public void DrawAfterPawnRendered(Vector3 baseDrawPos)
    {
        Pawn p = Pawn;
        if (p == null || !p.Spawned || hardpointCount == 0)
        {
            return;
        }

        bool drawWeaponGraphics = ShouldDrawWeaponGraphics(p);
        for (int i = 0; i < hardpointCount; i++)
        {
            MechHardpoint hp = hardpoints[i];
            if (hp.gun == null || !TryGetHardpointDrawPos(baseDrawPos, p, hp, out Vector3 drawPos))
            {
                continue;
            }

            float aimAngle = GetDisplayAimAngle(p, hp);
            if (drawWeaponGraphics || hp.IsAiming)
            {
                PawnRenderUtility.DrawEquipmentAiming(hp.gun, drawPos, aimAngle);
            }
        }

        // 瞄准扇形统一由 PostDrawExtraSelectionOverlays 绘制，这里不再重复调用。

        if (ShouldDrawHostileTargetIndicators(p))
        {
            DrawActiveTargetHighlights(p);
        }
    }

    private void DrawActiveTargetHighlights(Pawn p)
    {
        if (p == null || (!ShouldDrawTargetIndicators(p) && !AnyHardpointDisplayingTarget()))
        {
            return;
        }

        int currentTick = Find.TickManager.TicksGame;
        for (int i = 0; i < hardpointCount; i++)
        {
            MechHardpoint hp = hardpoints[i];
            Verb attackVerb = hp.AttackVerb;
            if (attackVerb == null)
            {
                continue;
            }

            if (TryGetHighlightTarget(hp, currentTick, out LocalTargetInfo highlightTarget))
            {
                attackVerb.DrawHighlight(highlightTarget);
            }
        }
    }

    public override void PostDraw()
    {
        // Pawn.DynamicDrawPhaseAt draws the renderer after ThingWithComps.Comps_PostDraw,
        // so turret overlays must be drawn from a post-render hook to stay visible.
    }

    private void DrawWarmupAimPies(Pawn p, Vector3 baseDrawPos)
    {
        if (p == null || !Find.Selector.IsSelected(p))
        {
            return;
        }

        for (int i = 0; i < hardpointCount; i++)
        {
            MechHardpoint hp = hardpoints[i];
            if (hp == null || hp.state != MechHardpoint.State.WarmingUp || !hp.currentTarget.IsValid)
            {
                continue;
            }

            int degreesWide = ComputeWarmupPieDegrees(hp);
            if (degreesWide <= 0)
            {
                continue;
            }

            if (!TryGetHardpointDrawPos(baseDrawPos, p, hp, out Vector3 center))
            {
                continue;
            }

            center.y = AltitudeLayer.MetaOverlays.AltitudeFor() + SelectionWarmupPieOffset;
            float facing = (GetTargetDrawPos(hp.currentTarget) - center).AngleFlat();
            // DrawAimPieRaw 内部会将 center 再前推 0.8 格。我们传入的 center 已经是炮口位置，
            // 提前扣掉这 0.8 格，确保扇形顶点真正落在炮口上，而非被推到炮口之外。
            Vector3 pieApex = center - Quaternion.AngleAxis(facing, Vector3.up) * Vector3.forward * 0.8f;
            GenDraw.DrawAimPieRaw(pieApex, facing, degreesWide);
        }
    }

    private static int ComputeWarmupPieDegrees(MechHardpoint hp)
    {
        int ticksLeft = hp?.warmupTicksLeft ?? 0;
        int total = hp?.baseWarmupTicks ?? 0;
        if (ticksLeft <= 0 || total <= 0)
        {
            return 0;
        }

        // 起始张角：短 warmup(30-60 tick) 保底 60°，长 warmup 最多 180°；
        // 线性按剩余 tick 比例收缩，与 vanilla Stance_Warmup 观感一致。
        int maxSpan = Mathf.Clamp(total, 60, 180);
        float progress = Mathf.Clamp01(ticksLeft / (float)total);
        return Mathf.Max(1, Mathf.RoundToInt(maxSpan * progress));
    }

    private bool AnyHardpointDisplayingTarget()
    {
        for (int i = 0; i < hardpointCount; i++)
        {
            MechHardpoint hp = hardpoints[i];
            if (hp.AttackVerb != null && hp.currentTarget.IsValid && hp.IsActivelyEngaging)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetHighlightTarget(MechHardpoint hp, int currentTick, out LocalTargetInfo target)
    {
        if (hp.currentTarget.IsValid && hp.IsActivelyEngaging)
        {
            target = hp.currentTarget;
            return true;
        }

        target = hp.lastAttackedTarget;
        return target.IsValid
            && hp.lastAttackTargetTick > 0
            && currentTick - hp.lastAttackTargetTick <= TargetReticlePersistTicks;
    }

    private static bool TryGetHardpointDrawPos(Vector3 baseDrawPos, Pawn pawn, MechHardpoint hp, out Vector3 drawPos)
    {
        if (pawn == null)
        {
            drawPos = default;
            return false;
        }

        Vector3 localOffset = ResolveHardpointOffset(hp);
        drawPos = baseDrawPos.WithYOffset(PawnRenderUtility.AltitudeForLayer(HardpointAltitudeLayer)) + localOffset.RotatedBy(pawn.Rotation);
        return true;
    }

    private static bool TryGetProjectileOrigin(Vector3 baseDrawPos, Pawn pawn, MechHardpoint hp, out Vector3 origin)
    {
        if (!TryGetHardpointDrawPos(baseDrawPos, pawn, hp, out Vector3 drawPos))
        {
            origin = default;
            return false;
        }

        float aimAngle = GetDisplayAimAngle(pawn, hp);
        Vector3 forward = Vector3Utility.HorizontalVectorFromAngle(aimAngle);
        origin = drawPos + forward * ProjectileOriginForwardOffset;
        origin.y = 0f;
        return true;
    }

    private static Vector3 ResolveHardpointOffset(MechHardpoint hp)
    {
        if (hp?.slot?.HasHardpointOffset ?? false)
        {
            return hp.slot.hardpointOffset;
        }

        float z = 0f;
        if (GRMechSectionSlotUtility.IsBow(hp?.sectionSlot))
        {
            z = 0.24f;
        }
        else if (GRMechSectionSlotUtility.IsStern(hp?.sectionSlot))
        {
            z = -0.2f;
        }

        if (hp?.layout?.ResolvedSlots != null && hp.slot != null)
        {
            int weaponIndex = -1;
            int weaponCount = 0;
            List<GRMechSlotEntry> slots = hp.layout.ResolvedSlots;
            for (int i = 0; i < slots.Count; i++)
            {
                GRMechSlotEntry entry = slots[i];
                if (entry?.slotCategory != GRMechSlotCategory.Weapon)
                {
                    continue;
                }

                if (entry == hp.slot || entry.key == hp.slotKey)
                {
                    weaponIndex = weaponCount;
                }

                weaponCount++;
            }

            if (weaponCount > 0)
            {
                float midpoint = (weaponCount - 1) * 0.5f;
                float x = (weaponIndex >= 0 ? weaponIndex - midpoint : 0f) * 0.18f;
                return new Vector3(x, 0f, z);
            }
        }

        return new Vector3(0f, 0f, z);
    }

    private static float GetDisplayAimAngle(Pawn pawn, MechHardpoint hp)
    {
        if (pawn == null)
        {
            return 0f;
        }

        float? verbAimAngle = hp.AttackVerb?.AimAngleOverride;
        if (verbAimAngle.HasValue)
        {
            return verbAimAngle.Value;
        }

        // 只要当前目标有效就保持瞄准（Cooling→Idle 的重选目标窗口期里不会回落到待机姿）；
        // 目标在 HasInvalidCurrentTarget 里会被 ResetCurrentTarget 真正清除，到那时才回 idle 角度。
        if (hp.currentTarget.IsValid && TryGetTargetDrawPos(pawn.MapHeld, hp.currentTarget, out Vector3 currentTargetPos))
        {
            return (currentTargetPos - pawn.DrawPos).AngleFlat();
        }

        return GetIdleWeaponAngle(pawn.Rotation);
    }

    private static float GetIdleWeaponAngle(Rot4 rotation)
    {
        return rotation == Rot4.West ? IdleAimAngleWest : IdleAimAngle;
    }

    // ========== Utility ==========

    private static bool CanOperate(Pawn pawn)
    {
        if (!pawn.Spawned || pawn.Downed || pawn.Dead || !pawn.Awake())
        {
            return false;
        }

        if (pawn.stances != null && pawn.stances.stunner.Stunned)
        {
            return false;
        }

        return true;
    }

    private static ThingWithComps MakeInternalGun(GRMechModuleDef module)
    {
        if (module?.equipmentDef == null)
        {
            return null;
        }

        ThingDef stuff = null;
        if (module.equipmentDef.MadeFromStuff)
        {
            stuff = module.equipmentStuff ?? GenStuff.DefaultStuffFor(module.equipmentDef);
        }

        return ThingMaker.MakeThing(module.equipmentDef, stuff) as ThingWithComps;
    }

    private static Vector3 GetTargetDrawPos(LocalTargetInfo target)
    {
        if (target.HasThing && target.Thing != null)
        {
            return target.Thing.DrawPos;
        }

        return target.Cell.ToVector3Shifted();
    }

    private static bool TryGetTargetDrawPos(Map map, LocalTargetInfo target, out Vector3 drawPos)
    {
        if (!target.IsValid)
        {
            drawPos = default;
            return false;
        }

        if (target.HasThing)
        {
            Thing targetThing = target.Thing;
            if (targetThing == null || targetThing.Destroyed || !targetThing.Spawned || targetThing.Map != map)
            {
                drawPos = default;
                return false;
            }

            drawPos = targetThing.DrawPos;
            return true;
        }

        drawPos = target.Cell.ToVector3Shifted();
        return true;
    }

    private static bool ShouldDrawTargetIndicators(Pawn pawn)
    {
        if (pawn == null)
        {
            return false;
        }

        Faction playerFaction = Faction.OfPlayer;
        if (playerFaction != null && pawn.Faction != null && pawn.Faction.HostileTo(playerFaction))
        {
            return true;
        }

        return Find.Selector.IsSelected(pawn);
    }

    private static bool ShouldDrawHostileTargetIndicators(Pawn pawn)
    {
        if (pawn == null)
        {
            return false;
        }

        Faction playerFaction = Faction.OfPlayer;
        return playerFaction != null && pawn.Faction != null && pawn.Faction.HostileTo(playerFaction);
    }

    private static bool ShouldDrawWeaponGraphics(Pawn pawn)
    {
        if (pawn == null)
        {
            return false;
        }

        Faction playerFaction = Faction.OfPlayer;
        if (playerFaction != null && pawn.Faction != null && pawn.Faction.HostileTo(playerFaction))
        {
            return true;
        }

        if (pawn.Drafted)
        {
            return true;
        }

        Thing enemyTarget = pawn.mindState?.enemyTarget;
        if (enemyTarget != null && !enemyTarget.Destroyed && enemyTarget.Spawned && enemyTarget.Map == pawn.MapHeld)
        {
            return true;
        }

        return pawn.mindState?.WasRecentlyCombatantTicks(WeaponGraphicCombatGraceTicks) ?? false;
    }

    private static GRMechCombatComputerModuleDef ResolveCombatComputer(GrayMechDesignSnapshot snapshot)
    {
        if (snapshot?.modules == null)
        {
            return null;
        }

        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            if (snapshot.modules[i]?.module is GRMechCombatComputerModuleDef module)
            {
                return module;
            }
        }

        return null;
    }
}
