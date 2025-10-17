using System.Collections.Generic;
using SD.GrayRace.ThingClasses;
using Verse;
using Verse.AI;

namespace SD.GrayRace.JobDrivers
{
    public class JobDriver_HaulToIncubator: JobDriver_HaulToContainer
    {
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 就是为了加行判断而已
            this.FailOn(() => Building_GRIncubator.WasLoadingCancelled(Container));
            return base.MakeNewToils();
        }
    }
}
