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
            if (!(t is Building_GRIncubator { State: IncubatorState.Preparing } buildingIncubator))
            {
                return false;
            }

            if (pawn.Map.designationManager.DesignationOn(buildingIncubator, DesignationDefOf.Deconstruct) != null) return false;

            return !buildingIncubator.IsBurning() && pawn.CanReserve(buildingIncubator) && FindIngredients(pawn, buildingIncubator).Thing != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_GRIncubator { State: IncubatorState.Preparing } buildingIncubator)) return null;

            ThingCount thingCount = FindIngredients(pawn, buildingIncubator);

            if (thingCount.Thing == null || thingCount.Count == 0) return null;

            Job job = JobMaker.MakeJob(GrayRaceDefOf.GR_HaulToIncubator, thingCount.Thing, t);
            // int needcount = t.TryGetInnerInteractableThingOwner().GetCountCanAccept(t, true);
            // job.count = Mathf.Min(thingCount.Thing.stackCount, needcount);
            job.count = Mathf.Min(thingCount.Thing.stackCount, thingCount.Count);
            job.haulMode = HaulMode.ToContainer;

            if (DebugSettings.godMode)
            {
                Log.Message($"[Incubator Haul] Creating job for {pawn.Name.ToStringShort}. " +
                            $"Target: {thingCount.Thing.def.defName}. " +
                            $"Required: {thingCount.Count}. " +
                            // $"CountCanAccept: {needcount}. " +
                            $"On Ground: {thingCount.Thing.stackCount}. " +
                            $"Final job.count: {job.count}");
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

            int count = incubator.GetRequiredCountOf(thing.def) + incubator.GetRequiredCountOf_Foundation(thing.def) - incubator.innerContainer.TotalStackCountOfDef(thing.def);

            return new ThingCount(thing, count, true);
        }
    }
}
