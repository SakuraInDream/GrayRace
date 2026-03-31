using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.Defs;
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

        GRMechCombatComputerModuleDef combatComputer = GrayMechCombatComputerUtility.GetActiveCombatComputer(pawn);
        Thing tacticalTarget = GrayMechCombatComputerUtility.ResolveTacticalTarget(pawn, combatComputer, enemyTarget);
        Verb verb = ResolveCombatVerb(pawn, tacticalTarget);
        if (verb == null)
        {
            return null;
        }

        if (verb.verbProps.IsMeleeAttack)
        {
            return MeleeAttackJob(pawn, tacticalTarget ?? enemyTarget);
        }

        Thing movementTarget = tacticalTarget ?? enemyTarget;
        bool hasCover = CoverUtility.CalculateOverallBlockChance(pawn, movementTarget.Position, pawn.Map) > 0.01f;
        bool canReserveCurrentCell = pawn.Position.WalkableBy(pawn.Map, pawn) && pawn.Map.pawnDestinationReservationManager.CanReserve(pawn.Position, pawn, pawn.Drafted);
        bool canHitTarget = verb.CanHitTarget(movementTarget);
        bool targetVeryClose = (pawn.Position - movementTarget.Position).LengthHorizontalSquared < 25;
        if (GrayMechCombatComputerUtility.ShouldWaitAtCurrentPosition(combatComputer, pawn, movementTarget, verb, hasCover, canReserveCurrentCell, canHitTarget, targetVeryClose, out int waitTicks))
        {
            return MakeCombatWaitJob(pawn, waitTicks);
        }

        if (!TryFindShootingPosition(pawn, out IntVec3 dest, verb, movementTarget))
        {
            return null;
        }

        if (dest == pawn.Position)
        {
            GrayMechCombatComputerUtility.ShouldWaitAtCurrentPosition(combatComputer, pawn, movementTarget, verb, hasCover, canReserveCurrentCell, canHitTarget, targetVeryClose, out waitTicks);
            return MakeCombatWaitJob(pawn, waitTicks > 0 ? waitTicks : GrayMechCombatComputerUtility.TacticalWaitCombatExpiryTicks);
        }

        Job moveJob = JobMaker.MakeJob(JobDefOf.Goto, dest);
        moveJob.expiryInterval = ExpiryInterval_ShooterSucceeded.RandomInRange;
        moveJob.checkOverrideOnExpire = true;
        return moveJob;
    }

    protected override bool TryFindShootingPosition(Pawn pawn, out IntVec3 dest, Verb verbToUse = null)
    {
        Thing enemyTarget = pawn?.mindState?.enemyTarget;
        return TryFindShootingPosition(pawn, out dest, verbToUse, enemyTarget);
    }

    private bool TryFindShootingPosition(Pawn pawn, out IntVec3 dest, Verb verbToUse, Thing movementTarget)
    {
        Verb verb = verbToUse ?? ResolveRangedVerb(pawn, movementTarget);
        if (verb == null)
        {
            dest = IntVec3.Invalid;
            return false;
        }

        if (GrayMechCombatComputerUtility.TryFindPosition(pawn, GrayMechCombatComputerUtility.GetActiveCombatComputer(pawn), movementTarget, verb, out dest))
        {
            return true;
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
        GrayMechTurretBankSystem turretBank = pawn?.TryGetComp<CompGrayMechSystems>()?.TurretBankSystem;
        if (turretBank != null && turretBank.TryGetTacticalVerb(target, out Verb turretVerb))
        {
            return turretVerb;
        }

        return null;
    }
}
