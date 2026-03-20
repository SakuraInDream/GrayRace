using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Bills;
using Verse;
using Verse.AI;

namespace SD.GrayRace.WorkGivers;

public class WorkGiver_DoGrayMechAssembly : WorkGiver_DoBill
{
    private static readonly IntRange ReCheckFailedBillTicksRange = new(500, 600);

    private readonly List<ThingCount> chosenIngThings = new();

    public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
    {
        if (thing is not IBillGiver billGiver
            || !ThingIsUsableBillGiver(thing)
            || !billGiver.BillStack.AnyShouldDoNow
            || !billGiver.UsableForBillsAfterFueling()
            || !pawn.CanReserve(thing, 1, -1, null, forced)
            || thing.IsBurning())
        {
            return null;
        }

        billGiver.BillStack.RemoveIncompletableBills();
        return StartOrResumeAssemblyJob(pawn, billGiver);
    }

    private Job StartOrResumeAssemblyJob(Pawn pawn, IBillGiver giver)
    {
        bool showFailReason = FloatMenuMakerMap.makingFor == pawn;

        for (int i = 0; i < giver.BillStack.Count; i++)
        {
            Bill bill = giver.BillStack[i];
            if (bill is not Bill_GrayMechAssembly assemblyBill)
            {
                continue;
            }

            if ((bill.recipe.requiredGiverWorkType != null && bill.recipe.requiredGiverWorkType != def.workType)
                || (Find.TickManager.TicksGame <= bill.nextTickToSearchForIngredients && FloatMenuMakerMap.makingFor != pawn)
                || !bill.ShouldDoNow()
                || !bill.PawnAllowedToStartAnew(pawn))
            {
                continue;
            }

            SkillRequirement skillRequirement = bill.recipe.FirstSkillRequirementPawnDoesntSatisfy(pawn);
            if (skillRequirement != null)
            {
                JobFailReason.Is("UnderRequiredSkill".Translate(skillRequirement.minLevel), bill.Label);
                continue;
            }

            if (bill is Bill_Autonomous { State: not FormingState.Gathering } autonomousBill)
            {
                return WorkOnFormedBill((Thing)giver, autonomousBill);
            }

            if (!assemblyBill.HasValidDesign)
            {
                if (showFailReason)
                {
                    JobFailReason.Is("No mech design configured.", bill.Label);
                }

                continue;
            }

            List<IngredientCount> ingredients = assemblyBill.GetDynamicIngredients();
            if (!WorkGiver_DoBill.TryFindBestFixedIngredients(ingredients, pawn, (Thing)giver, chosenIngThings, bill.ingredientSearchRadius))
            {
                if (FloatMenuMakerMap.makingFor != pawn)
                {
                    bill.nextTickToSearchForIngredients = Find.TickManager.TicksGame + ReCheckFailedBillTicksRange.RandomInRange;
                }
                else if (showFailReason)
                {
                    JobFailReason.Is("MissingMaterials".Translate(assemblyBill.RequiredMaterialSummary()), bill.Label);
                    showFailReason = false;
                }

                chosenIngThings.Clear();
                continue;
            }

            Job haulOffJob;
            Job result = WorkGiver_DoBill.TryStartNewDoBillJob(pawn, bill, giver, chosenIngThings, out haulOffJob);
            chosenIngThings.Clear();
            return result;
        }

        chosenIngThings.Clear();
        return null;
    }

    private static Job WorkOnFormedBill(Thing giver, Bill_Autonomous bill)
    {
        Job job = JobMaker.MakeJob(JobDefOf.DoBill, giver);
        job.bill = bill;
        return job;
    }
}
