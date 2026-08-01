using System;
using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.Dialogs;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SD.GrayRace.Comps;

[StaticConstructorOnStartup]
public class CompMultiTurretGun : ThingComp
{
    internal readonly struct HardpointDebugData
    {
        public readonly string SectionSlotId;
        public readonly string SlotKey;
        public readonly string ModuleLabel;
        public readonly Vector2 BaseAnchor;
        public readonly Vector2 EffectiveAnchor;
        public readonly Vector2 LocalPosition;
        public readonly Vector2 WorldPosition;
        public readonly float DeploymentProgress;
        public readonly MechHardpoint.State State;
        public readonly bool HasAnchorOverride;

        public HardpointDebugData(
            string sectionSlotId,
            string slotKey,
            string moduleLabel,
            Vector2 baseAnchor,
            Vector2 effectiveAnchor,
            Vector2 localPosition,
            Vector2 worldPosition,
            float deploymentProgress,
            MechHardpoint.State state,
            bool hasAnchorOverride)
        {
            SectionSlotId = sectionSlotId;
            SlotKey = slotKey;
            ModuleLabel = moduleLabel;
            BaseAnchor = baseAnchor;
            EffectiveAnchor = effectiveAnchor;
            LocalPosition = localPosition;
            WorldPosition = worldPosition;
            DeploymentProgress = deploymentProgress;
            State = state;
            HasAnchorOverride = hasAnchorOverride;
        }
    }

    private static readonly CachedTexture ToggleTurretIcon = new("UI/Gizmos/ToggleTurret");
    private static readonly CachedTexture ForceTargetIcon = new("UI/Commands/Attack");
    private static readonly CachedTexture StopForceTargetIcon = new("UI/Commands/Halt");

    private const int CombatTargetSearchInterval = 10;
    private const int IdleTargetSearchInterval = 60;
    private const int WeaponGraphicCombatGraceTicks = 300;
    private const float HardpointAltitudeLayer = 92f;

    private MechHardpoint[] hardpoints = Array.Empty<MechHardpoint>();
    private LocalTargetInfo[] assignedTargets = Array.Empty<LocalTargetInfo>();
    private int hardpointCount;
    private int hardpointRevision;
    private IFireControlDirector director;
    private GRMechCombatComputerModuleDef activeCombatComputer;
    private GRMechHardpointSwarmSettings swarmSettings;
    private readonly Predicate<TargetInfo> forcedTargetValidator;
    private bool fireAtWill = true;
    private bool pendingRebuild;
    private bool targetSearchRequested = true;
    private bool deploymentRequestInitialized;
    private bool deploymentRequested;
    private bool deploymentTransitionActive;
    private bool movementFireSuppressed;
    private LocalTargetInfo forcedTarget = LocalTargetInfo.Invalid;

    public CompMultiTurretGun()
    {
        forcedTargetValidator = CanForceAttack;
    }

    private Pawn Pawn => parent as Pawn;
    private ThingWithComps ParentThing => parent;
    private Thing ForcedTargetThing => forcedTarget.HasThing ? forcedTarget.Thing : null;

    public int HardpointCount => hardpointCount;

    internal bool TryGetForcedTarget(out Thing target)
    {
        target = ForcedTargetThing;
        return forcedTarget.IsValid
            && IsValidVerbSelectionTarget(target)
            && HasEngageableHardpoint(forcedTarget);
    }

    internal int DebugHardpointCount => hardpointCount;

    internal int DebugHardpointRevision => hardpointRevision;

    internal bool DebugTargetValid => Pawn is { Destroyed: false, Dead: false, Spawned: true };

    internal string DebugPawnLabel => Pawn?.LabelShort ?? "Unknown mech";

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

    public void Notify_LoadoutChanged()
    {
        Pawn pawn = Pawn;
        if (!IsPawnRuntimeReady(pawn))
        {
            pendingRebuild = true;
            return;
        }

        RebuildFromSnapshot(pawn.TryGetComp<CompGrayMechLoadout>()?.DesignSnapshot);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref fireAtWill, "fireAtWill", defaultValue: true);
        Scribe_TargetInfo.Look(ref forcedTarget, "forcedTarget");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            pendingRebuild = true;
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        pendingRebuild = pendingRebuild || hardpointCount == 0;
        TryRebuildFromLoadout();
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
        ClearHardpoints();
    }

    public override void CompTick()
    {
        base.CompTick();
        Pawn p = Pawn;
        if (!IsPawnRuntimeReady(p))
        {
            return;
        }

        if (pendingRebuild)
        {
            TryRebuildFromLoadout();
        }

        UpdateMovementFireSuppression(p);
        RefreshForcedTargetState();
        bool canOperate = CanOperate(p);
        bool deployRequested = canOperate && ShouldRequestDeployment(p);
        UpdateDeployment(deployRequested);
        if (hardpointCount > 0 && (deployRequested || deploymentTransitionActive))
        {
            HardpointSwarmSolver.Tick(hardpoints, hardpointCount, p.DrawPos, swarmSettings, Find.TickManager.TicksGame);
        }

        if (!canOperate)
        {
            AbortActiveBursts();
            if (!fireAtWill && !forcedTarget.IsValid)
            {
                ClearIdleTargets();
            }

            return;
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

            if (hp.HasInvalidCurrentTarget(map))
            {
                bool wasFiring = hp.state == MechHardpoint.State.Firing;
                hp.ResetCurrentTarget();
                if (wasFiring)
                {
                    hp.NotifyCastComplete();
                }
                else if (hp.state == MechHardpoint.State.WarmingUp)
                {
                    hp.state = MechHardpoint.State.Idle;
                }
            }

            switch (hp.state)
            {
                case MechHardpoint.State.Firing:
                    if (!hp.IsBursting)
                    {
                        hp.NotifyCastComplete();
                        break;
                    }

                    if (!hp.TickBurst())
                    {
                        break;
                    }

                    if (TryStartHardpointCast(hp, p))
                    {
                        hp.NotifyBurstShotFired();
                        if (!hp.IsBursting)
                        {
                            hp.NotifyCastComplete();
                        }
                    }
                    else
                    {
                        hp.NotifyCastComplete();
                    }

                    break;

                case MechHardpoint.State.WarmingUp:
                    if (!hp.IsFullyDeployed)
                    {
                        break;
                    }

                    if (--hp.warmupTicksLeft <= 0)
                    {
                        hp.BeginBurst();
                        if (TryStartHardpointCast(hp, p))
                        {
                            hp.NotifyCastStarted();
                            hp.NotifyBurstShotFired();
                            if (!hp.IsBursting)
                            {
                                hp.NotifyCastComplete();
                            }
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
                    break;
            }

            if (hp.currentTarget.IsValid)
            {
                hp.curRotation = (GetTargetDrawPos(hp.currentTarget) - p.DrawPos).AngleFlat();
            }
        }

        if (movementFireSuppressed)
        {
            return;
        }

        int searchInterval = HasCombatSearchContext(p)
            ? CombatTargetSearchInterval
            : IdleTargetSearchInterval;
        if (director != null && (targetSearchRequested || p.IsHashIntervalTick(searchInterval)))
        {
            director.AssignTargets(hardpoints, hardpointCount, p, forcedTarget, fireAtWill, assignedTargets);
            targetSearchRequested = false;
            for (int i = 0; i < hardpointCount; i++)
            {
                LocalTargetInfo newTarget = assignedTargets[i];
                if (!newTarget.IsValid)
                {
                    continue;
                }

                MechHardpoint hp = hardpoints[i];
                hp.BeginWarmup(newTarget);
                hp.curRotation = (GetTargetDrawPos(newTarget) - p.DrawPos).AngleFlat();
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

    public void RebuildFromSnapshot(GrayMechDesignSnapshot snapshot)
    {
        Pawn p = Pawn;
        if (!IsPawnRuntimeReady(p))
        {
            pendingRebuild = true;
            return;
        }

        ClearHardpoints();
        pendingRebuild = false;
        activeCombatComputer = ResolveCombatComputer(snapshot);
        director = CreateDirector(activeCombatComputer);
        swarmSettings = snapshot?.chassis?.hardpointSwarmSettings;

        if (snapshot?.modules == null)
        {
            return;
        }

        int weaponCount = 0;
        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            GrayMechModuleAssignment assignment = snapshot.modules[i];
            if (GrayMechDesignUtility.IsWeaponModule(assignment?.module)
                && GrayMechDesignUtility.TryResolveSlot(snapshot, assignment.sectionSlot, assignment.slotKey, out GRMechSlotEntry slot, out _)
                && slot.weaponMountMode == GRMechWeaponMountMode.Hardpoint)
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

        if (assignedTargets.Length < weaponCount)
        {
            assignedTargets = new LocalTargetInfo[weaponCount];
        }

        director?.Prepare(weaponCount);
        targetSearchRequested = true;
        deploymentRequestInitialized = false;
        deploymentTransitionActive = false;

        hardpointCount = 0;
        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            GrayMechModuleAssignment assignment = snapshot.modules[i];
            GRMechModuleDef module = assignment?.module;
            if (!GrayMechDesignUtility.IsWeaponModule(module))
            {
                continue;
            }

            if (!GrayMechDesignUtility.TryResolveSlot(snapshot, assignment.sectionSlot, assignment.slotKey, out GRMechSlotEntry resolvedSlot, out _)
                || resolvedSlot.weaponMountMode != GRMechWeaponMountMode.Hardpoint)
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
            hp.gun = gun;
            hp.CancelBurst();
            hp.slot = resolvedSlot;
            hp.state = MechHardpoint.State.Idle;
            hp.currentTarget = LocalTargetInfo.Invalid;
            hp.lastAttackedTarget = LocalTargetInfo.Invalid;
            hp.lastAttackTargetTick = 0;
            hp.cooldownTicksLeft = 0;
            hp.warmupTicksLeft = 0;
            hp.curRotation = 0f;
            hp.deploymentProgress = 0f;
            string sectionSlotId = GRMechSectionSlotUtility.GetSlotId(assignment.sectionSlot);
            hp.SectionSlotId = sectionSlotId;
            hp.ClearDebugAnchorOverride();
            hp.Setup(ParentThing);

            hardpointCount++;
        }

        HardpointSwarmSolver.Initialize(hardpoints, hardpointCount, p.DrawPos);
    }

    private void TryRebuildFromLoadout()
    {
        if (!pendingRebuild)
        {
            return;
        }

        Pawn pawn = Pawn;
        if (!IsPawnRuntimeReady(pawn))
        {
            return;
        }

        RebuildFromSnapshot(pawn.TryGetComp<CompGrayMechLoadout>()?.DesignSnapshot);
    }

    private void ClearHardpoints()
    {
        for (int i = 0; i < hardpointCount; i++)
        {
            hardpoints[i].DestroyGun();
            assignedTargets[i] = LocalTargetInfo.Invalid;
        }

        hardpointCount = 0;
        hardpointRevision++;
        targetSearchRequested = true;
        deploymentRequestInitialized = false;
        deploymentTransitionActive = false;
        swarmSettings = null;
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

        List<object> selectedObjects = Find.Selector.SelectedObjectsListForReading;
        for (int i = 0; i < selectedObjects.Count; i++)
        {
            if (TryGetSelectedPlayerMech(selectedObjects[i], out CompMultiTurretGun turret))
            {
                if (!turret.TrySetForcedTarget(target, out string failReason) && !failReason.NullOrEmpty())
                {
                    Messages.Message(failReason.CapitalizeFirst() + ".", target.Thing, MessageTypeDefOf.RejectInput, historical: false);
                }
            }
        }
    }

    private static bool TryGetSelectedPlayerMech(object selectedObject, out CompMultiTurretGun turret)
    {
        turret = null;
        if (selectedObject is not Pawn pawn
            || !pawn.Spawned
            || !pawn.Drafted
            || !pawn.IsColonyMechPlayerControlled)
        {
            return false;
        }

        turret = pawn.TryGetComp<CompMultiTurretGun>();
        return turret != null;
    }

    private bool TrySetForcedTarget(LocalTargetInfo target, out string failReason)
    {
        failReason = null;
        Pawn p = Pawn;
        if (!target.IsValid || p == null || !p.Spawned || !p.Drafted)
        {
            return false;
        }

        Thing targetThing = target.Thing;
        if (targetThing == null
            || targetThing.Destroyed
            || !targetThing.Spawned
            || !ParentThing.Spawned
            || targetThing.Map != ParentThing.MapHeld)
        {
            return false;
        }

        if (targetThing == p)
        {
            failReason = "CannotAttackSelf".Translate();
            return false;
        }

        if (targetThing is Pawn targetPawn
            && (p.InSameExtraFaction(targetPawn, ExtraFactionType.HomeFaction)
                || p.InSameExtraFaction(targetPawn, ExtraFactionType.MiniFaction)))
        {
            failReason = "CannotAttackSameFactionMember".Translate();
            return false;
        }

        if (!HasEngageableHardpoint(target))
        {
            failReason = "CannotFire".Translate();
            return false;
        }

        forcedTarget = target;
        p.mindState.enemyTarget = target.Thing;
        targetSearchRequested = true;
        p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
        return true;
    }

    private void ResetForcedTarget()
    {
        List<object> selectedObjects = Find.Selector.SelectedObjectsListForReading;
        for (int i = 0; i < selectedObjects.Count; i++)
        {
            if (TryGetSelectedPlayerMech(selectedObjects[i], out CompMultiTurretGun turret))
            {
                if (turret.ResetForcedTargetLocal())
                {
                    turret.InterruptCurrentJob();
                }
            }
        }
    }

    private bool ResetForcedTargetLocal()
    {
        LocalTargetInfo oldForcedTarget = forcedTarget;
        forcedTarget = LocalTargetInfo.Invalid;
        targetSearchRequested = true;

        Pawn p = Pawn;
        if (oldForcedTarget.HasThing && p?.mindState?.enemyTarget == oldForcedTarget.Thing)
        {
            p.mindState.enemyTarget = null;
        }

        if (!oldForcedTarget.IsValid)
        {
            return false;
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

        return true;
    }

    private void InterruptCurrentJob()
    {
        Pawn?.jobs?.EndCurrentJob(JobCondition.InterruptForced);
    }

    private void RefreshForcedTargetState()
    {
        if (forcedTarget.IsValid && Pawn is { Drafted: false })
        {
            ResetForcedTargetLocal();
            return;
        }

        if (!forcedTarget.IsValid || !forcedTarget.HasThing)
        {
            return;
        }

        Thing targetThing = forcedTarget.Thing;
        if (!IsValidVerbSelectionTarget(targetThing) || !HasEngageableHardpoint(forcedTarget))
        {
            ResetForcedTargetLocal();
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
        if (!hp.IsBursting)
        {
            hp.ResetToIdle();
        }
    }

    private void AbortActiveBursts()
    {
        for (int i = 0; i < hardpointCount; i++)
        {
            MechHardpoint hp = hardpoints[i];
            if (hp.state == MechHardpoint.State.Firing && hp.IsBursting)
            {
                hp.NotifyCastComplete();
            }
        }
    }

    private bool TryStartHardpointCast(MechHardpoint hp, Pawn pawn)
    {
        Verb verb = hp.AttackVerb;
        LocalTargetInfo target = hp.currentTarget;
        if (verb == null || !verb.Available() || !target.IsValid || !target.HasThing)
        {
            return false;
        }

        Thing targetThing = target.Thing;
        if (targetThing == null || targetThing.Destroyed || !targetThing.Spawned || targetThing.Map != pawn.Map)
        {
            return false;
        }

        if (!hp.TryFindShootLineTo(target, out ShootLine shootLine))
        {
            return false;
        }

        ThingDef projectileDef = hp.module?.ProjectileDef;
        if (projectileDef == null)
        {
            return false;
        }

        if (!TryGetProjectileOrigin(pawn.DrawPos, pawn, hp, out Vector3 origin))
        {
            return false;
        }

        Projectile projectile = (Projectile)GenSpawn.Spawn(projectileDef, pawn.Position, pawn.Map);

        float forcedMissRadius = verb.verbProps.ForcedMissRadius;
        if (forcedMissRadius > 0.5f)
        {
            forcedMissRadius *= verb.verbProps.GetForceMissFactorFor(hp.gun, pawn);
            float adjustedForcedMiss = VerbUtility.CalculateAdjustedForcedMiss(forcedMissRadius, target.Cell - pawn.Position);
            if (adjustedForcedMiss > 0.5f)
            {
                IntVec3 forcedMissTarget;
                bool hasForcedMissTarget;
                if (verb.verbProps.forcedMissEvenDispersal)
                {
                    hasForcedMissTarget = hp.TryGetEvenDispersalForcedMissTarget(
                        target.Cell,
                        adjustedForcedMiss,
                        pawn.Position,
                        out forcedMissTarget);
                }
                else
                {
                    int radialCellCount = GenRadial.NumCellsInRadius(adjustedForcedMiss);
                    forcedMissTarget = target.Cell + GenRadial.RadialPattern[Rand.Range(0, radialCellCount)];
                    hasForcedMissTarget = true;
                }

                if (hasForcedMissTarget && forcedMissTarget != target.Cell)
                {
                    ProjectileHitFlags forcedMissHitFlags = Rand.Chance(0.5f)
                        ? ProjectileHitFlags.All
                        : ProjectileHitFlags.NonTargetWorld;
                    projectile.Launch(pawn, origin, forcedMissTarget, target, forcedMissHitFlags, equipment: hp.gun);
                    return true;
                }
            }
        }

        ShotReport report = ShotReport.HitReportFor(pawn, verb, target);
        Thing randomCover = report.GetRandomCoverToMissInto();
        ThingDef targetCoverDef = randomCover?.def;

        if (verb.verbProps.canGoWild && !Rand.Chance(report.AimOnTargetChance_IgnoringPosture))
        {
            shootLine.ChangeDestToMissWild(report.AimOnTargetChance_StandardTarget, projectileDef.projectile.flyOverhead, pawn.Map);
            ProjectileHitFlags wildMissHitFlags = ProjectileHitFlags.NonTargetWorld;
            if (Rand.Chance(0.5f))
            {
                wildMissHitFlags |= ProjectileHitFlags.NonTargetPawns;
            }

            projectile.Launch(pawn, origin, shootLine.Dest, target, wildMissHitFlags, equipment: hp.gun, targetCoverDef: targetCoverDef);
            return true;
        }

        if (target.Thing is { def.CanBenefitFromCover: true } && !Rand.Chance(report.PassCoverChance))
        {
            ProjectileHitFlags coverHitFlags = ProjectileHitFlags.NonTargetWorld | ProjectileHitFlags.NonTargetPawns;
            projectile.Launch(pawn, origin, randomCover, target, coverHitFlags, equipment: hp.gun, targetCoverDef: targetCoverDef);
            return true;
        }

        ProjectileHitFlags hitFlags = ProjectileHitFlags.IntendedTarget | ProjectileHitFlags.NonTargetPawns;
        if (!target.HasThing || target.Thing.def.Fillage == FillCategory.Full)
        {
            hitFlags |= ProjectileHitFlags.NonTargetWorld;
        }

        LocalTargetInfo usedTarget = target.HasThing ? target : shootLine.Dest;
        projectile.Launch(pawn, origin, usedTarget, target, hitFlags, equipment: hp.gun, targetCoverDef: targetCoverDef);
        return true;
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
        Pawn pawn = Pawn;
        if (target == null
            || pawn == null
            || target.Destroyed
            || !target.Spawned
            || !ParentThing.Spawned
            || target.Map != ParentThing.MapHeld)
        {
            return false;
        }

        if (ParentThing.HostileTo(target))
        {
            return true;
        }

        if (target != ForcedTargetThing || target == pawn)
        {
            return false;
        }

        if (pawn != null
            && target is Pawn targetPawn
            && (pawn.InSameExtraFaction(targetPawn, ExtraFactionType.HomeFaction)
                || pawn.InSameExtraFaction(targetPawn, ExtraFactionType.MiniFaction)))
        {
            return false;
        }

        return true;
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (Pawn is { IsColonyMechPlayerControlled: true})
        {
            if (Pawn.Drafted)
            {
                Command_Target forceAttackCommand = new()
                {
                    defaultLabel = "CommandSetForceAttackTarget".Translate(),
                    defaultDesc = "CommandSetForceAttackTargetDesc".Translate(),
                    icon = ForceTargetIcon.Texture,
                    hotKey = KeyBindingDefOf.Misc4,
                    targetingParams = BuildForcedTargetingParameters(),
                    action = OrderAttack,
                    onUpdate = DrawTargetingPreview
                };

                yield return forceAttackCommand;
            }

            if (forcedTarget.IsValid)
            {
                Command_Action stopForceAttackCommand = new()
                {
                    defaultLabel = "CommandStopForceAttack".Translate(),
                    defaultDesc = "CommandStopForceAttackDesc".Translate(),
                    icon = StopForceTargetIcon.Texture,
                    hotKey = KeyBindingDefOf.Misc5,
                    action = ResetForcedTarget
                };
                yield return stopForceAttackCommand;
            }

            Command_Toggle command = new()
            {
                defaultLabel = "CommandToggleTurret".Translate(),
                defaultDesc = "CommandToggleTurretDesc".Translate(),
                isActive = () => fireAtWill,
                icon = ToggleTurretIcon.Texture,
                toggleAction = delegate
                {
                    fireAtWill = !fireAtWill;
                    targetSearchRequested = fireAtWill;
                    if (!fireAtWill && !forcedTarget.IsValid)
                    {
                        ClearIdleTargets();
                    }
                }
            };
            yield return command;
        }

        if (DebugSettings.ShowDevGizmos && hardpointCount > 0)
        {
            yield return new Command_Action
            {
                defaultLabel = "DEBUG: Hardpoint 锚点调试",
                defaultDesc = "读取并临时微调该 mech 的运行时 Hardpoint 锚点。不会修改 XML、Def 或存档。",
                icon = TexButton.ToggleTweak,

                action = OpenHardpointAnchorTuner,
            };
        }
    }

    internal bool TryGetHardpointDebugData(int index, out HardpointDebugData data)
    {
        Pawn pawn = Pawn;
        if (pawn == null || index < 0 || index >= hardpointCount)
        {
            data = default;
            return false;
        }

        MechHardpoint hp = hardpoints[index];
        if (hp == null)
        {
            data = default;
            return false;
        }

        Vector3 pawnDrawPos = pawn.DrawPos;
        Vector3 worldPosition = hp.swarmPosition;
        data = new HardpointDebugData(
            hp.SectionSlotId ?? string.Empty,
            hp.slot?.key ?? string.Empty,
            hp.module?.label ?? hp.module?.defName ?? string.Empty,
            hp.BaseAnchor,
            hp.EffectiveAnchor,
            new Vector2(worldPosition.x - pawnDrawPos.x, worldPosition.z - pawnDrawPos.z),
            new Vector2(worldPosition.x, worldPosition.z),
            hp.deploymentProgress,
            hp.state,
            hp.HasDebugAnchorOverride);
        return true;
    }

    internal bool DebugSetHardpointAnchor(int index, Vector2 anchor)
    {
        if (index < 0
            || index >= hardpointCount
            || float.IsNaN(anchor.x)
            || float.IsNaN(anchor.y)
            || float.IsInfinity(anchor.x)
            || float.IsInfinity(anchor.y))
        {
            return false;
        }

        MechHardpoint hp = hardpoints[index];
        Vector2 previousAnchor = hp.EffectiveAnchor;
        hp.SetDebugAnchorOverride(anchor);
        ShiftHardpointRuntimePosition(hp, anchor - previousAnchor);
        return true;
    }

    internal bool DebugResetHardpointAnchor(int index)
    {
        if (index < 0 || index >= hardpointCount)
        {
            return false;
        }

        MechHardpoint hp = hardpoints[index];
        if (!hp.HasDebugAnchorOverride)
        {
            return true;
        }

        Vector2 previousAnchor = hp.EffectiveAnchor;
        Vector2 baseAnchor = hp.BaseAnchor;
        hp.ClearDebugAnchorOverride();
        ShiftHardpointRuntimePosition(hp, baseAnchor - previousAnchor);
        return true;
    }

    internal void DebugResetAllHardpointAnchors()
    {
        for (int i = 0; i < hardpointCount; i++)
        {
            DebugResetHardpointAnchor(i);
        }
    }

    private void OpenHardpointAnchorTuner()
    {
        Find.WindowStack.WindowOfType<Dialog_HardpointAnchorTuner>()?.Close(false);
        Find.WindowStack.Add(new Dialog_HardpointAnchorTuner(this));
    }

    private static void ShiftHardpointRuntimePosition(MechHardpoint hp, Vector2 delta)
    {
        Vector3 worldDelta = new(delta.x, 0f, delta.y);
        hp.anchorPosition += worldDelta;
        hp.swarmPosition += worldDelta;
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
        if (targetThing.Destroyed
            || !targetThing.Spawned
            || targetThing.Map != ParentThing.MapHeld)
        {
            return false;
        }

        return HasEngageableHardpoint(target.Thing);
    }

    private bool HasEngageableHardpoint(LocalTargetInfo target)
    {
        Pawn pawn = Pawn;
        Map map = ParentThing?.MapHeld;
        if (pawn == null || map == null || !target.IsValid || !target.HasThing)
        {
            return false;
        }

        for (int i = 0; i < hardpointCount; i++)
        {
            MechHardpoint hardpoint = hardpoints[i];
            if (hardpoint != null && hardpoint.CanEngageTarget(target, pawn, map))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawTargetingPreview(LocalTargetInfo target)
    {
        List<object> selectedObjects = Find.Selector.SelectedObjectsListForReading;
        for (int i = 0; i < selectedObjects.Count; i++)
        {
            if (TryGetSelectedPlayerMech(selectedObjects[i], out CompMultiTurretGun turret))
            {
                turret.DrawTargetingPreviewLocal(target);
            }
        }
    }

    private void DrawTargetingPreviewLocal(LocalTargetInfo target)
    {
        if (!ParentThing.Spawned)
        {
            return;
        }

        for (int i = 0; i < hardpointCount; i++)
        {
            MechHardpoint hardpoint = hardpoints[i];
            Verb attackVerb = hardpoint.AttackVerb;
            if (attackVerb != null)
            {
                attackVerb.verbProps.DrawRadiusRing(hardpoint.anchorPosition.ToIntVec3(), attackVerb);
                if (target.IsValid)
                {
                    GenDraw.DrawTargetHighlight(target);
                }
            }
        }
    }

    public override void PostDrawExtraSelectionOverlays()
    {
        Pawn p = Pawn;
        if (!ShouldDrawSelectionOverlays(p) || !p.Spawned || hardpointCount == 0)
        {
            return;
        }

        DrawWarmupAimPies(p);
    }

    private void DrawWarmupAimPies(Pawn pawn)
    {
        Vector3 baseDrawPos = pawn.DrawPos;
        for (int i = 0; i < hardpointCount; i++)
        {
            MechHardpoint hp = hardpoints[i];
            Verb verb = hp.AttackVerb;
            if (!hp.WarmingUp
                || !hp.IsFullyDeployed
                || !hp.currentTarget.IsValid
                || verb?.verbProps?.drawAimPie != true
                || !TryGetTargetDrawPos(pawn.MapHeld, hp.currentTarget, out _)
                || !TryGetHardpointDrawPos(baseDrawPos, pawn, hp, out Vector3 center))
            {
                continue;
            }

            float aimAngle = GetDisplayAimAngle(pawn, hp, center);
            center.y = AltitudeLayer.MetaOverlays.AltitudeFor();
            GenDraw.DrawAimPieRaw(center, aimAngle, hp.warmupTicksLeft);
        }
    }

    public void DrawAfterPawnRendered(Vector3 baseDrawPos)
    {
        Pawn p = Pawn;
        if (p == null || !p.Spawned || hardpointCount == 0)
        {
            return;
        }

        for (int i = 0; i < hardpointCount; i++)
        {
            MechHardpoint hp = hardpoints[i];
            if (hp.gun == null || hp.deploymentProgress <= 0f || !TryGetHardpointDrawPos(baseDrawPos, p, hp, out Vector3 drawPos))
            {
                continue;
            }

            float aimAngle = GetDisplayAimAngle(p, hp, drawPos);
            PawnRenderUtility.DrawEquipmentAiming(hp.gun, drawPos, aimAngle);
        }
    }

    private bool TryGetHardpointDrawPos(Vector3 baseDrawPos, Pawn pawn, MechHardpoint hp, out Vector3 drawPos)
    {
        if (pawn == null)
        {
            drawPos = default;
            return false;
        }

        float deployment = SmoothDeploymentProgress(hp.deploymentProgress);
        Vector3 renderCorrection = baseDrawPos - pawn.DrawPos;
        renderCorrection.y = 0f;
        Vector3 deployedPosition = hp.swarmPosition + renderCorrection;
        Vector3 retractedPosition = baseDrawPos;
        retractedPosition.y = 0f;
        drawPos = Vector3.Lerp(retractedPosition, deployedPosition, deployment);
        drawPos.y = baseDrawPos.y + PawnRenderUtility.AltitudeForLayer(HardpointAltitudeLayer);
        return true;
    }

    private bool TryGetProjectileOrigin(Vector3 baseDrawPos, Pawn pawn, MechHardpoint hp, out Vector3 origin)
    {
        if (!TryGetHardpointDrawPos(baseDrawPos, pawn, hp, out Vector3 drawPos))
        {
            origin = default;
            return false;
        }

        origin = drawPos;
        origin.y = 0f;
        return true;
    }

    private float GetDisplayAimAngle(Pawn pawn, MechHardpoint hp, Vector3 weaponDrawPos)
    {
        if (pawn == null)
        {
            return 0f;
        }

        // 只要当前目标有效就保持瞄准（Cooling→Idle 的重选目标窗口期里不会回落到待机姿）；
        // 目标在 HasInvalidCurrentTarget 里会被 ResetCurrentTarget 真正清除，到那时才统一朝向地图北方。
        if (hp.currentTarget.IsValid && TryGetTargetDrawPos(pawn.MapHeld, hp.currentTarget, out Vector3 currentTargetPos))
        {
            float? verbAimAngle = hp.AttackVerb?.AimAngleOverride;
            if (verbAimAngle.HasValue)
            {
                return verbAimAngle.Value;
            }

            return (currentTargetPos - weaponDrawPos).AngleFlat();
        }

        return 0f;
    }

    private static float SmoothDeploymentProgress(float progress)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
    }

    // ========== Utility ==========

    private static bool IsPawnRuntimeReady(Pawn pawn)
    {
        return pawn != null
            && pawn.Spawned
            && pawn.MapHeld != null
            && pawn.pather != null;
    }

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

        return pawn.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) ?? false;
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

    private static bool ShouldDrawSelectionOverlays(Pawn pawn)
    {
        return pawn != null
            && pawn.Faction == Faction.OfPlayer
            && Find.Selector.IsSelected(pawn);
    }

    private void UpdateDeployment(bool requested)
    {
        if (!deploymentRequestInitialized || deploymentRequested != requested)
        {
            deploymentRequestInitialized = true;
            deploymentRequested = requested;
            deploymentTransitionActive = true;
        }

        if (!deploymentTransitionActive)
        {
            return;
        }

        bool stable = true;
        for (int i = 0; i < hardpointCount; i++)
        {
            MechHardpoint hp = hardpoints[i];
            hp.TickDeployment(requested);
            if (requested ? !hp.IsFullyDeployed : hp.deploymentProgress > 0f)
            {
                stable = false;
            }
        }

        deploymentTransitionActive = !stable;
        if (stable && !requested && hardpointCount > 0)
        {
            HardpointSwarmSolver.Initialize(hardpoints, hardpointCount, Pawn.DrawPos);
        }
    }

    private bool HasCombatSearchContext(Pawn pawn)
    {
        if (forcedTarget.IsValid)
        {
            return true;
        }

        Thing enemyTarget = pawn.mindState?.enemyTarget;
        if (enemyTarget != null && !enemyTarget.Destroyed && enemyTarget.Spawned && enemyTarget.Map == pawn.MapHeld)
        {
            return true;
        }

        int currentTick = Find.TickManager.TicksGame;
        for (int i = 0; i < hardpointCount; i++)
        {
            MechHardpoint hp = hardpoints[i];
            if (hp.currentTarget.IsValid
                || (hp.lastAttackTargetTick > 0 && currentTick - hp.lastAttackTargetTick <= WeaponGraphicCombatGraceTicks))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateMovementFireSuppression(Pawn pawn)
    {
        bool moving = pawn.pather?.Moving ?? false;
        if (moving)
        {
            if (!movementFireSuppressed)
            {
                movementFireSuppressed = true;
                if (forcedTarget.IsValid)
                {
                    ResetForcedTargetLocal();
                }

                for (int i = 0; i < hardpointCount; i++)
                {
                    MechHardpoint hp = hardpoints[i];
                    hp.ResetCurrentTarget();
                    if (hp.state == MechHardpoint.State.WarmingUp)
                    {
                        hp.state = MechHardpoint.State.Idle;
                    }
                    else if (hp.state == MechHardpoint.State.Firing)
                    {
                        hp.NotifyCastComplete();
                    }
                }
            }

            targetSearchRequested = false;
            return;
        }

        if (movementFireSuppressed)
        {
            movementFireSuppressed = false;
            targetSearchRequested = fireAtWill || forcedTarget.IsValid;
        }
    }

    private bool ShouldRequestDeployment(Pawn pawn)
    {
        if (pawn.Faction == Faction.OfPlayer && pawn.Drafted)
        {
            return true;
        }

        if (forcedTarget.IsValid)
        {
            return true;
        }

        Thing enemyTarget = pawn.mindState?.enemyTarget;
        if (enemyTarget != null && !enemyTarget.Destroyed && enemyTarget.Spawned && enemyTarget.Map == pawn.MapHeld)
        {
            return true;
        }

        int currentTick = Find.TickManager.TicksGame;
        for (int i = 0; i < hardpointCount; i++)
        {
            MechHardpoint hp = hardpoints[i];
            if (hp.IsActivelyEngaging
                || (hp.lastAttackTargetTick > 0 && currentTick - hp.lastAttackTargetTick <= WeaponGraphicCombatGraceTicks))
            {
                return true;
            }
        }

        return false;
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
