using System;
using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.Defs;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SD.GrayRace.Mechs;

[StaticConstructorOnStartup]
public class GrayMechTurretBankSystem : GrayMechSystemBase
{
    private static readonly CachedTexture ToggleTurretIcon = new("UI/Gizmos/ToggleTurret");
    private static readonly CachedTexture ForceTargetIcon = new("UI/Commands/Attack");
    private static readonly CachedTexture StopForceTargetIcon = new("UI/Commands/Halt");
    private static readonly Material ForcedTargetLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(1f, 0.5f, 0.5f));
    private const TargetScanFlags AutoTargetScanFlags = TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;

    private sealed class GrayMechTurretState : IAttackTargetSearcher
    {
        private readonly GrayMechTurretBankSystem bank;

        public GRMechModuleDef module;
        public GRMechSectionSlotDef sectionSlot;
        public string slotKey;
        public ThingWithComps gun;
        public int burstCooldownTicksLeft;
        public int burstWarmupTicksLeft;
        public int localWarmupTicks = 1;
        public LocalTargetInfo currentTarget = LocalTargetInfo.Invalid;
        public LocalTargetInfo lastAttackedTarget = LocalTargetInfo.Invalid;
        public int lastAttackTargetTick;
        public float curRotation;

        public GrayMechTurretState(GrayMechTurretBankSystem bank)
        {
            this.bank = bank;
        }

        public Thing Thing => bank.ParentThing;

        public Verb CurrentEffectiveVerb => AttackVerb;

        public LocalTargetInfo LastAttackedTarget => lastAttackedTarget;

        public int LastAttackTargetTick => lastAttackTargetTick;

        public Verb AttackVerb => gun?.TryGetComp<CompEquippable>()?.PrimaryVerb;

        private bool WarmingUp => burstWarmupTicksLeft > 0;

        public bool CanEngageTarget(LocalTargetInfo target)
        {
            Verb attackVerb = AttackVerb;
            if (attackVerb == null || !attackVerb.Available() || !target.IsValid || !target.HasThing)
            {
                return false;
            }

            Thing targetThing = target.Thing;
            if (targetThing == null || targetThing.Destroyed || !targetThing.Spawned || targetThing.Map != bank.ParentThing.MapHeld)
            {
                return false;
            }

            return attackVerb.CanHitTarget(target);
        }

        public void Initialize(GRMechModuleDef module, GRMechSectionSlotDef sectionSlot, string slotKey, ThingWithComps gun)
        {
            this.module = module;
            this.sectionSlot = sectionSlot;
            this.slotKey = slotKey;
            this.gun = gun;
            UpdateGunVerbs();
        }

        public void Tick(Pawn pawn)
        {
            Verb attackVerb = AttackVerb;
            if (attackVerb == null || !attackVerb.Available())
            {
                ResetCurrentTarget();
                return;
            }

            if (currentTarget.HasThing)
            {
                Thing currentThing = currentTarget.Thing;
                if (currentThing == null || currentThing.Destroyed || !currentThing.Spawned || currentThing.Map != pawn.Map)
                {
                    ResetCurrentTarget();
                }
            }

            if (currentTarget.IsValid)
            {
                curRotation = (currentTarget.Cell.ToVector3Shifted() - pawn.DrawPos).AngleFlat();
            }

            attackVerb.VerbTick();
            if (attackVerb.state == VerbState.Bursting)
            {
                return;
            }

            if (WarmingUp)
            {
                burstWarmupTicksLeft--;
                if (burstWarmupTicksLeft == 0)
                {
                    attackVerb.TryStartCastOn(currentTarget, surpriseAttack: false, canHitNonTargetPawns: true, preventFriendlyFire: false, nonInterruptingSelfCast: true);
                    lastAttackTargetTick = Find.TickManager.TicksGame;
                    lastAttackedTarget = currentTarget;
                }

                return;
            }

            if (burstCooldownTicksLeft > 0)
            {
                burstCooldownTicksLeft--;
            }

            if (burstCooldownTicksLeft <= 0 && pawn.IsHashIntervalTick(10))
            {
                if (bank.TryFindTargetFor(this, out LocalTargetInfo newTarget))
                {
                    currentTarget = newTarget;
                    burstWarmupTicksLeft = localWarmupTicks;
                    bank.TryReserveTarget(currentTarget);
                }
                else
                {
                    ResetCurrentTarget();
                }
            }
        }

        public void DestroyGun()
        {
            if (gun != null && !gun.Destroyed)
            {
                gun.Destroy();
            }

            gun = null;
        }

        private void UpdateGunVerbs()
        {
            if (gun == null)
            {
                return;
            }

            List<Verb> allVerbs = gun.TryGetComp<CompEquippable>().AllVerbs;
            for (int i = 0; i < allVerbs.Count; i++)
            {
                Verb verb = allVerbs[i];
                localWarmupTicks = Mathf.Max(1, verb.WarmupTime.SecondsToTicks());
                VerbProperties clonedProps = verb.verbProps.MemberwiseClone();
                clonedProps.warmupTime = 0f;
                verb.verbProps = clonedProps;
                verb.caster = bank.ParentThing;
                verb.castCompleteCallback = OnCastComplete;
            }
        }

        private void OnCastComplete()
        {
            Verb attackVerb = AttackVerb;
            Pawn pawn = bank.Pawn;
            if (attackVerb != null && pawn != null)
            {
                burstCooldownTicksLeft = attackVerb.verbProps.AdjustedCooldownTicks(attackVerb, pawn);
            }
        }

        public void PrepareReservation()
        {
            if (currentTarget.HasThing)
            {
                Thing currentThing = currentTarget.Thing;
                if (currentThing == null || currentThing.Destroyed || !currentThing.Spawned || currentThing.Map != bank.ParentThing.MapHeld)
                {
                    ResetCurrentTarget();
                    return;
                }
            }

            bank.TryReserveTarget(currentTarget);
        }

        public void ClearCurrentTargetIfNotBursting()
        {
            if (AttackVerb?.state != VerbState.Bursting)
            {
                ResetCurrentTarget();
            }
        }

        public bool TryForceTargetNow(LocalTargetInfo target)
        {
            Verb attackVerb = AttackVerb;
            if (attackVerb == null || !attackVerb.Available() || attackVerb.state == VerbState.Bursting || burstCooldownTicksLeft > 0 || !CanEngageTarget(target))
            {
                return false;
            }

            currentTarget = target;
            burstWarmupTicksLeft = localWarmupTicks;
            bank.TryReserveTarget(currentTarget);
            return true;
        }

        public bool CurrentTargetMatches(LocalTargetInfo target)
        {
            return currentTarget.IsValid && currentTarget == target;
        }

        private void ResetCurrentTarget()
        {
            currentTarget = LocalTargetInfo.Invalid;
            burstWarmupTicksLeft = 0;
        }
    }

    private readonly List<GrayMechTurretState> turrets = new();
    private readonly HashSet<Thing> reservedTargets = new();
    private readonly Predicate<Thing> unreservedTargetValidator;
    private readonly Predicate<TargetInfo> forcedTargetValidator;
    private bool fireAtWill = true;
    private bool pendingRebuild;
    private LocalTargetInfo forcedTarget = LocalTargetInfo.Invalid;
    private GRMechCombatComputerModuleDef activeCombatComputer;

    public GrayMechTurretBankSystem()
    {
        unreservedTargetValidator = ValidateUnreservedTarget;
        forcedTargetValidator = CanForceAttack;
    }

    private Pawn Pawn => pawn;
    private ThingWithComps ParentThing => Parent;
    private Thing ForcedTargetThing => forcedTarget.HasThing ? forcedTarget.Thing : null;

    public int TurretCount => turrets.Count;
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
        pendingRebuild = pendingRebuild || respawningAfterLoad || turrets.Count == 0;
        TryRebuildFromLoadout();
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        ClearTurrets();
    }

    public override void CompTick()
    {
        Pawn pawn = Pawn;
        if (pawn == null)
        {
            return;
        }

        if (pendingRebuild)
        {
            TryRebuildFromLoadout();
        }

        RefreshForcedTargetState();
        if (!CanOperate(pawn))
        {
            if (!fireAtWill && !forcedTarget.IsValid)
            {
                ClearPendingTargets();
            }

            return;
        }

        PrepareTargetReservations();
        for (int i = 0; i < turrets.Count; i++)
        {
            turrets[i].Tick(pawn);
        }
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
                    ClearPendingTargets();
                }
            };
            yield return command;
        }
    }

    public override void PostDrawExtraSelectionOverlays()
    {
        if (!forcedTarget.IsValid || !forcedTarget.HasThing)
        {
            return;
        }

        Thing targetThing = forcedTarget.Thing;
        if (targetThing == null || targetThing.Destroyed || !targetThing.Spawned || targetThing.Map != ParentThing.MapHeld)
        {
            return;
        }

        Vector3 a = ParentThing.TrueCenter();
        Vector3 b = targetThing.TrueCenter();
        b.y = AltitudeLayer.MetaOverlays.AltitudeFor();
        a.y = b.y;
        GenDraw.DrawLineBetween(a, b, ForcedTargetLineMat);
    }

    public void RebuildFromSnapshot(GrayMechDesignSnapshot snapshot)
    {
        ClearTurrets();
        pendingRebuild = false;
        activeCombatComputer = ResolveCombatComputer(snapshot);

        Pawn pawn = Pawn;
        if (pawn == null || snapshot?.modules == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            GrayMechModuleAssignment assignment = snapshot.modules[i];
            GRMechModuleDef module = assignment?.module;
            if (!ShouldCreateTurret(module))
            {
                continue;
            }

            ThingWithComps gun = MakeInternalGun(module);
            if (gun == null)
            {
                continue;
            }

            GrayMechTurretState turret = new(this);
            turret.Initialize(module, assignment.sectionSlot, assignment.slotKey, gun);
            turrets.Add(turret);
        }
    }

    public bool TryGetTacticalVerb(Thing target, out Verb verb)
    {
        if (pendingRebuild)
        {
            TryRebuildFromLoadout();
        }

        Pawn pawn = Pawn;
        if (pawn == null || !CanOperate(pawn))
        {
            verb = null;
            return false;
        }

        target = ResolveVerbSelectionTarget(target);
        GRMechCombatComputerWeaponSelectionMode selectionMode = GrayMechCombatComputerUtility.ResolveWeaponSelection(activeCombatComputer);
        return TryGetCombatComputerVerb(target, selectionMode, out verb);
    }

    private void TryRebuildFromLoadout()
    {
        if (!pendingRebuild)
        {
            return;
        }

        RebuildFromSnapshot(Pawn?.TryGetComp<CompGrayMechLoadout>()?.DesignSnapshot);
    }

    private bool TryGetCombatComputerVerb(Thing target, GRMechCombatComputerWeaponSelectionMode selectionMode, out Verb verb)
    {
        int hittableCount = 0;
        if (target != null)
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                Verb candidate = turrets[i].AttackVerb;
                if (candidate != null && candidate.Available() && candidate.CanHitTarget(target))
                {
                    hittableCount++;
                }
            }
        }

        int bestIndex = -1;
        float bestRange = selectionMode == GRMechCombatComputerWeaponSelectionMode.ShortestRange ? float.MaxValue : float.MinValue;
        float bestMinRange = selectionMode == GRMechCombatComputerWeaponSelectionMode.ShortestRange ? float.MaxValue : float.MinValue;
        for (int i = 0; i < turrets.Count; i++)
        {
            Verb candidate = turrets[i].AttackVerb;
            if (candidate == null || !candidate.Available())
            {
                continue;
            }

            bool canHitTarget = target != null && candidate.CanHitTarget(target);
            if (hittableCount > 0 && !canHitTarget)
            {
                continue;
            }

            float candidateRange = candidate.EffectiveRange;
            float candidateMinRange = candidate.verbProps.minRange;

            if (bestIndex < 0)
            {
                bestIndex = i;
                bestRange = candidateRange;
                bestMinRange = candidateMinRange;
                continue;
            }

            if (selectionMode == GRMechCombatComputerWeaponSelectionMode.ShortestRange)
            {
                if (candidateRange < bestRange || (Mathf.Approximately(candidateRange, bestRange) && candidateMinRange < bestMinRange))
                {
                    bestRange = candidateRange;
                    bestMinRange = candidateMinRange;
                    bestIndex = i;
                }
            }
            else if (candidateRange > bestRange || (Mathf.Approximately(candidateRange, bestRange) && candidateMinRange > bestMinRange))
            {
                bestRange = candidateRange;
                bestMinRange = candidateMinRange;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0)
        {
            verb = turrets[bestIndex].AttackVerb;
            return verb != null;
        }

        verb = null;
        return false;
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

    private void ClearTurrets()
    {
        for (int i = 0; i < turrets.Count; i++)
        {
            turrets[i].DestroyGun();
        }

        turrets.Clear();
        reservedTargets.Clear();
    }

    private bool CanOperate(Pawn pawn)
    {
        if (!pawn.Spawned || pawn.Downed || pawn.Dead || !pawn.Awake())
        {
            return false;
        }

        if (pawn.stances != null && pawn.stances.stunner.Stunned)
        {
            return false;
        }

        if (pawn.IsColonyMechPlayerControlled && !fireAtWill && !forcedTarget.IsValid)
        {
            return false;
        }

        CompCanBeDormant dormant = ParentThing.TryGetComp<CompCanBeDormant>();
        return dormant == null || dormant.Awake;
    }

    private void OrderAttack(LocalTargetInfo target)
    {
        if (!target.IsValid)
        {
            ResetForcedTarget();
            return;
        }

        forcedTarget = target;
        PrepareTargetReservations();
        for (int i = 0; i < turrets.Count; i++)
        {
            turrets[i].TryForceTargetNow(forcedTarget);
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

        for (int i = 0; i < turrets.Count; i++)
        {
            if (turrets[i].CurrentTargetMatches(oldForcedTarget))
            {
                turrets[i].ClearCurrentTargetIfNotBursting();
            }
        }

        if (!fireAtWill)
        {
            ClearPendingTargets();
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

    private void PrepareTargetReservations()
    {
        reservedTargets.Clear();
        for (int i = 0; i < turrets.Count; i++)
        {
            turrets[i].PrepareReservation();
        }
    }

    private void ClearPendingTargets()
    {
        for (int i = 0; i < turrets.Count; i++)
        {
            turrets[i].ClearCurrentTargetIfNotBursting();
        }
    }

    private void TryReserveTarget(LocalTargetInfo target)
    {
        if (!target.IsValid || !target.HasThing)
        {
            return;
        }

        Thing targetThing = target.Thing;
        if (targetThing == null || targetThing.Destroyed || targetThing == ForcedTargetThing)
        {
            return;
        }

        reservedTargets.Add(targetThing);
    }

    private bool TryFindTargetFor(GrayMechTurretState turret, out LocalTargetInfo target)
    {
        if (forcedTarget.IsValid && turret.CanEngageTarget(forcedTarget))
        {
            target = forcedTarget;
            return true;
        }

        if (!fireAtWill)
        {
            target = LocalTargetInfo.Invalid;
            return false;
        }

        target = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(turret, AutoTargetScanFlags, unreservedTargetValidator);
        if (target.IsValid)
        {
            return true;
        }

        target = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(turret, AutoTargetScanFlags);
        return target.IsValid;
    }

    private bool ValidateUnreservedTarget(Thing target)
    {
        return target != null && !reservedTargets.Contains(target);
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
        for (int i = 0; i < turrets.Count; i++)
        {
            if (turrets[i].CanEngageTarget(localTarget))
            {
                return true;
            }
        }

        return false;
    }

    private TargetingParameters BuildForcedTargetingParameters()
    {
        TargetingParameters targetingParameters = TargetingParameters.ForAttackAny();
        targetingParameters.validator = forcedTargetValidator;
        targetingParameters.canTargetLocations = false;
        return targetingParameters;
    }

    private void DrawTargetingPreview(LocalTargetInfo target)
    {
        if (!ParentThing.Spawned)
        {
            return;
        }

        for (int i = 0; i < turrets.Count; i++)
        {
            Verb attackVerb = turrets[i].AttackVerb;
            if (attackVerb != null)
            {
                attackVerb.verbProps.DrawRadiusRing(ParentThing.Position, attackVerb);
            }
        }
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

    private static bool ShouldCreateTurret(GRMechModuleDef module)
    {
        return GrayMechDesignUtility.IsWeaponModule(module);
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
