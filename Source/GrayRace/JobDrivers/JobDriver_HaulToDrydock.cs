using System.Collections.Generic;
using SD.GrayRace.ThingClasses;
using Verse;
using Verse.AI;

namespace SD.GrayRace.JobDrivers;

public class JobDriver_HaulToDrydock : JobDriver_HaulToContainer
{
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() => Building_GR_Drydock.WasLoadingCancelled(Container));
        return base.MakeNewToils();
    }
}
