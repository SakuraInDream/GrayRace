using RimWorld;
using SD.GrayRace.Mechs;
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

        UpdateEnemyTarget(pawn);
        Thing enemyTarget = pawn.mindState.enemyTarget;
        if (enemyTarget == null)
        {
            return null;
        }

        if (enemyTarget is Pawn enemyPawn && enemyPawn.IsPsychologicallyInvisible())
        {
            return null;
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

        Verb verb = pawn.TryGetAttackVerb(enemyTarget, allowAbilityVerbs, allowTurrets);
        if (verb == null)
        {
            return null;
        }

        if (verb.verbProps.IsMeleeAttack)
        {
            return MeleeAttackJob(pawn, enemyTarget);
        }

        bool hasCover = CoverUtility.CalculateOverallBlockChance(pawn, enemyTarget.Position, pawn.Map) > 0.01f;
        bool canReserveCurrentCell = pawn.Position.WalkableBy(pawn.Map, pawn) && pawn.Map.pawnDestinationReservationManager.CanReserve(pawn.Position, pawn, pawn.Drafted);
        bool canHitTarget = verb.CanHitTarget(enemyTarget);
        bool targetVeryClose = (pawn.Position - enemyTarget.Position).LengthHorizontalSquared < 25;
        if (GrayMechCombatComputerUtility.ShouldWaitAtCurrentPosition(GrayMechCombatComputerUtility.GetActiveCombatComputer(pawn), pawn, enemyTarget, verb, hasCover, canReserveCurrentCell, canHitTarget, targetVeryClose))
        {
            pawn.pather?.StopDead();
            return JobMaker.MakeJob(JobDefOf.Wait_Combat, ExpiryInterval_ShooterSucceeded.RandomInRange, checkOverrideOnExpiry: true);
        }

        if (!TryFindShootingPosition(pawn, out IntVec3 dest, verb))
        {
            return null;
        }

        if (dest == pawn.Position)
        {
            pawn.pather?.StopDead();
            return JobMaker.MakeJob(JobDefOf.Wait_Combat, ExpiryInterval_ShooterSucceeded.RandomInRange, checkOverrideOnExpiry: true);
        }

        Job moveJob = JobMaker.MakeJob(JobDefOf.Goto, dest);
        moveJob.expiryInterval = ExpiryInterval_ShooterSucceeded.RandomInRange;
        moveJob.checkOverrideOnExpire = true;
        return moveJob;
    }

    protected override bool TryFindShootingPosition(Pawn pawn, out IntVec3 dest, Verb verbToUse = null)
    {
        Thing enemyTarget = pawn?.mindState?.enemyTarget;
        bool allowManualCastWeapons = pawn != null && !pawn.IsColonist && !pawn.IsColonySubhuman;
        Verb verb = verbToUse ?? pawn?.TryGetAttackVerb(enemyTarget, allowManualCastWeapons, allowTurrets);
        if (verb == null)
        {
            dest = IntVec3.Invalid;
            return false;
        }

        if (GrayMechCombatComputerUtility.TryFindPosition(pawn, GrayMechCombatComputerUtility.GetActiveCombatComputer(pawn), verb, out dest))
        {
            return true;
        }

        return base.TryFindShootingPosition(pawn, out dest, verb);
    }
}
