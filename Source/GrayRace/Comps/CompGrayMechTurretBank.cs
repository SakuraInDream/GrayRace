using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SD.GrayRace.Comps;

public class CompGrayMechTurretBank : ThingComp
{
    private sealed class GrayMechTurretState : IAttackTargetSearcher
    {
        private readonly CompGrayMechTurretBank bank;

        public GRMechModuleDef module;
        public GRMechSectionSlotDef sectionSlot;
        public string slotKey;
        public ThingWithComps gun;
        public int burstCooldownTicksLeft;
        public int burstWarmupTicksLeft;
        public LocalTargetInfo currentTarget = LocalTargetInfo.Invalid;
        public LocalTargetInfo lastAttackedTarget = LocalTargetInfo.Invalid;
        public int lastAttackTargetTick;
        public float curRotation;

        public GrayMechTurretState(CompGrayMechTurretBank bank)
        {
            this.bank = bank;
        }

        public Thing Thing => bank.parent;

        public Verb CurrentEffectiveVerb => AttackVerb;

        public LocalTargetInfo LastAttackedTarget => lastAttackedTarget;

        public int LastAttackTargetTick => lastAttackTargetTick;

        public Verb AttackVerb => gun?.TryGetComp<CompEquippable>()?.PrimaryVerb;

        private bool WarmingUp => burstWarmupTicksLeft > 0;

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
                currentTarget = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(this, TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable);
                if (currentTarget.IsValid)
                {
                    burstWarmupTicksLeft = 1;
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
                verb.caster = bank.parent;
                verb.castCompleteCallback = OnCastComplete;
            }
        }

        private void OnCastComplete()
        {
            Verb attackVerb = AttackVerb;
            if (attackVerb != null)
            {
                burstCooldownTicksLeft = attackVerb.verbProps.defaultCooldownTime.SecondsToTicks();
            }
        }

        private void ResetCurrentTarget()
        {
            currentTarget = LocalTargetInfo.Invalid;
            burstWarmupTicksLeft = 0;
        }
    }

    private readonly List<GrayMechTurretState> turrets = new();
    private bool fireAtWill = true;
    private bool pendingRebuild;

    private Pawn Pawn => parent as Pawn;

    public int TurretCount => turrets.Count;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref fireAtWill, "fireAtWill", defaultValue: true);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            pendingRebuild = true;
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        pendingRebuild = pendingRebuild || respawningAfterLoad || turrets.Count == 0;
        TryRebuildFromLoadout();
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
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

        if (!CanOperate(pawn))
        {
            return;
        }

        for (int i = 0; i < turrets.Count; i++)
        {
            turrets[i].Tick(pawn);
        }
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo item in base.CompGetGizmosExtra())
        {
            yield return item;
        }

        if (parent is Pawn { IsColonyMechPlayerControlled: not false })
        {
            Command_Toggle command = new();
            command.defaultLabel = "CommandToggleTurret".Translate();
            command.defaultDesc = "CommandToggleTurretDesc".Translate();
            command.isActive = () => fireAtWill;
            command.icon = ContentFinder<Texture2D>.Get("UI/Gizmos/ToggleTurret");
            command.toggleAction = delegate
            {
                fireAtWill = !fireAtWill;
            };
            yield return command;
        }
    }

    public void RebuildFromSnapshot(GrayMechDesignSnapshot snapshot)
    {
        ClearTurrets();
        pendingRebuild = false;

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

        int bestIndex = -1;
        float bestScore = float.MinValue;
        for (int i = 0; i < turrets.Count; i++)
        {
            Verb candidate = turrets[i].AttackVerb;
            if (candidate == null || !candidate.Available())
            {
                continue;
            }

            float score = candidate.verbProps.range;
            if (target != null && candidate.CanHitTarget(target))
            {
                score += 1000f;
            }

            if (score > bestScore)
            {
                bestScore = score;
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

    private void TryRebuildFromLoadout()
    {
        if (!pendingRebuild)
        {
            return;
        }

        CompGrayMechLoadout loadout = Pawn?.TryGetComp<CompGrayMechLoadout>();
        RebuildFromSnapshot(loadout?.DesignSnapshot);
    }

    private void ClearTurrets()
    {
        for (int i = 0; i < turrets.Count; i++)
        {
            turrets[i].DestroyGun();
        }

        turrets.Clear();
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

        if (pawn.IsColonyMechPlayerControlled && !fireAtWill)
        {
            return false;
        }

        CompCanBeDormant dormant = parent.TryGetComp<CompCanBeDormant>();
        return dormant == null || dormant.Awake;
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
        return module?.equipmentDef != null && module.UsesSlotCategory(GRMechSlotCategory.Weapon);
    }
}
