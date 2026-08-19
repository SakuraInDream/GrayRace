using RimWorld;
using SD.GrayRace.Comps;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace SD.GrayRace.JobGivers;

public class JobGiver_GrayMechFightEnemies : JobGiver_AIFightEnemies
{
    protected override Job TryGiveJob(Pawn pawn)
    {
        if ((pawn.IsColonist || pawn.IsColonySubhuman) && pawn.playerSettings.hostilityResponse != HostilityResponseMode.Attack && (!(pawn.GetLord()?.LordJob is LordJob_Ritual_Duel lordJob_Ritual_Duel) || !lordJob_Ritual_Duel.duelists.Contains(pawn)))
        {
            return null;
        }

        CompMultiTurretGun turret = pawn.TryGetComp<CompMultiTurretGun>();
        Thing forcedTarget = null;
        bool hasForcedTarget = turret != null
            && turret.TryGetForcedTarget(out forcedTarget);
        if (hasForcedTarget)
        {
            pawn.mindState.enemyTarget = forcedTarget;
        }
        else
        {
            UpdateEnemyTarget(pawn);
        }

        Thing enemyTarget = hasForcedTarget ? forcedTarget : pawn.mindState.enemyTarget;
        if (enemyTarget == null)
        {
            return null;
        }

        if (enemyTarget is Pawn enemyPawn && enemyPawn.IsPsychologicallyInvisible())
        {
            return null;
        }

        if (hasForcedTarget)
        {
            return MakeCombatWaitJob(pawn, ExpiryInterval_ShooterSucceeded.RandomInRange);
        }

        bool allowAbilityVerbs = !pawn.IsColonist && !pawn.IsColonySubhuman && !DisableAbilityVerbs;
        if (allowAbilityVerbs)
        {
            Job abilityJob = GetAbilityJob(pawn, enemyTarget);
            if (abilityJob != null)
            {
                return abilityJob;
            }
        }

        if (OnlyUseAbilityVerbs)
        {
            if (!TryFindShootingPosition(pawn, out IntVec3 abilityDest))
            {
                return null;
            }

            if (abilityDest == pawn.Position)
            {
                pawn.pather?.StopDead();
                return JobMaker.MakeJob(JobDefOf.Wait_Combat, ExpiryInterval_Ability.RandomInRange, checkOverrideOnExpiry: true);
            }

            Job abilityMoveJob = JobMaker.MakeJob(JobDefOf.Goto, abilityDest);
            abilityMoveJob.expiryInterval = ExpiryInterval_Ability.RandomInRange;
            abilityMoveJob.checkOverrideOnExpire = true;
            return abilityMoveJob;
        }

        Verb verb = ResolveCombatVerb(pawn, enemyTarget);
        if (verb == null)
        {
            return null;
        }

        if (verb.verbProps.IsMeleeAttack)
        {
            return MeleeAttackJob(pawn, enemyTarget);
        }

        Thing movementTarget = enemyTarget;
        bool hasCover = CoverUtility.CalculateOverallBlockChance(pawn, movementTarget.Position, pawn.Map) > 0.01f;
        bool canReserveCurrentCell = pawn.Position.WalkableBy(pawn.Map, pawn) && pawn.Map.pawnDestinationReservationManager.CanReserve(pawn.Position, pawn, pawn.Drafted);
        bool canHitTarget = verb.CanHitTarget(movementTarget);
        bool targetVeryClose = (pawn.Position - movementTarget.Position).LengthHorizontalSquared < 25;
        if ((hasCover && canReserveCurrentCell && canHitTarget) || (targetVeryClose && canHitTarget))
        {
            return MakeCombatWaitJob(pawn, ExpiryInterval_ShooterSucceeded.RandomInRange);
        }

        if (!TryFindShootingPosition(pawn, out IntVec3 dest, verb))
        {
            return null;
        }

        if (dest == pawn.Position)
        {
            return MakeCombatWaitJob(pawn, ExpiryInterval_ShooterSucceeded.RandomInRange);
        }

        Job moveJob = JobMaker.MakeJob(JobDefOf.Goto, dest);
        moveJob.expiryInterval = ExpiryInterval_ShooterSucceeded.RandomInRange;
        moveJob.checkOverrideOnExpire = true;
        return moveJob;
    }

    protected override bool TryFindShootingPosition(Pawn pawn, out IntVec3 dest, Verb verbToUse = null)
    {
        Verb verb = verbToUse ?? ResolveRangedVerb(pawn, pawn?.mindState?.enemyTarget);
        if (verb == null)
        {
            return base.TryFindShootingPosition(pawn, out dest);
        }

        return base.TryFindShootingPosition(pawn, out dest, verb);
    }

    private static Job MakeCombatWaitJob(Pawn pawn, int waitTicks)
    {
        pawn.pather?.StopDead();
        return JobMaker.MakeJob(
            JobDefOf.Wait_Combat,
            waitTicks > 0 ? waitTicks : ExpiryInterval_ShooterSucceeded.RandomInRange,
            checkOverrideOnExpiry: true);
    }

    private static Verb ResolveCombatVerb(Pawn pawn, Thing target)
    {
        Verb rangedVerb = ResolveRangedVerb(pawn, target);
        if (rangedVerb != null)
        {
            return rangedVerb;
        }

        return pawn?.meleeVerbs?.TryGetMeleeVerb(target);
    }

    private static Verb ResolveRangedVerb(Pawn pawn, Thing target)
    {
        CompMultiTurretGun turretBank = pawn?.TryGetComp<CompMultiTurretGun>();
        if (turretBank != null && turretBank.TryGetTacticalVerb(target, out Verb turretVerb))
        {
            return turretVerb;
        }

        return null;
    }
}
