using RimWorld;
using SD.GrayRace.ThingClasses;
using Verse;
using Verse.AI;

namespace SD.GrayRace.WorkGivers;

public class WorkGiver_CarryToDrydock : WorkGiver_Scanner
{
    public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(GrayRaceDefOf.GR_Drydock);

    public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (t is not Building_GR_Drydock drydock || !drydock.HasActiveOrder || !drydock.CurrentOrderNeedsMaterials)
        {
            return false;
        }

        if (pawn.Map.designationManager.DesignationOn(drydock, DesignationDefOf.Deconstruct) != null)
        {
            return false;
        }

        return !drydock.IsBurning() && pawn.CanReserve(drydock) && FindIngredient(pawn, drydock).Thing != null;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (t is not Building_GR_Drydock drydock || !drydock.HasActiveOrder || !drydock.CurrentOrderNeedsMaterials)
        {
            return null;
        }

        ThingCount thingCount = FindIngredient(pawn, drydock);
        if (thingCount.Thing == null || thingCount.Count <= 0)
        {
            return null;
        }

        Job job = JobMaker.MakeJob(GrayRaceDefOf.GR_HaulToDrydock, thingCount.Thing, drydock);
        job.count = thingCount.Count < thingCount.Thing.stackCount ? thingCount.Count : thingCount.Thing.stackCount;
        job.haulMode = HaulMode.ToContainer;
        return job;
    }

    private static ThingCount FindIngredient(Pawn pawn, Building_GR_Drydock drydock)
    {
        Thing thing = GenClosest.ClosestThingReachable(
            pawn.Position,
            pawn.Map,
            ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
            PathEndMode.ClosestTouch,
            TraverseParms.For(pawn),
            validator: x => pawn.CanReserve(x) && !x.IsForbidden(pawn) && drydock.CanAcceptIngredient(x));

        if (thing == null)
        {
            return default;
        }

        int count = drydock.GetRequiredCountOf(thing.def) - drydock.GetLoadedCountOf(thing.def);
        return count > 0 ? new ThingCount(thing, count, true) : default;
    }
}
