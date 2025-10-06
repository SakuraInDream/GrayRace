using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.ThingClasses;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SD.GrayRace.WorkGivers
{
    public class WorkGiver_CarryToIncubator: WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(GrayRaceDefOf.GR_Incubator);

        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_GRIncubator building_Incubator) || building_Incubator.Working || building_Incubator.selectedRecipe == null)
            {
                return false;
            }

            if (pawn.Map.designationManager.DesignationOn(building_Incubator, DesignationDefOf.Deconstruct) != null) return false;

            return !building_Incubator.IsBurning() && pawn.CanReserve(building_Incubator) && FindIngredients(pawn, building_Incubator).Thing != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var building_Incubator = t as Building_GRIncubator;
            if (building_Incubator == null || building_Incubator.Working || building_Incubator.State != IncubatorState.Preparing) return null;

            ThingCount thingCount = FindIngredients(pawn, building_Incubator);

            if (thingCount.Thing == null || thingCount.Count == 0) return null;

            Job job = HaulAIUtility.HaulToContainerJob(pawn, thingCount.Thing, t);
            job.count = Mathf.Min(job.count, thingCount.Count);

            if (DebugSettings.godMode)
            {
                Log.Message($"GR_CarryToIncubator - Job Count: {job.count} ThingCount: {thingCount.Thing.LabelCapNoCount}x{thingCount.Count}");
            }

            return job;
        }

        private ThingCount FindIngredients(Pawn pawn, Building_GRIncubator incubator)
        {
            Thing thing = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                validator: x => pawn.CanReserve(x) && !x.IsForbidden(pawn) && incubator.CanAcceptIngredient(x)
            );

            if (thing == null) return default;

            var count = incubator.GetRequiredCountOf(thing.def) + incubator.GetRequiredCountOf_Foundation(thing.def) - incubator.innerContainer.TotalStackCountOfDef(thing.def);

            return new ThingCount(thing, count, true);
        }
    }
}
