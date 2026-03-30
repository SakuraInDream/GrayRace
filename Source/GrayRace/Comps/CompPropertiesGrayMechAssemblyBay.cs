using Verse;
using System.Collections.Generic;
using SD.GrayRace.Defs;

namespace SD.GrayRace.Comps;

public class CompPropertiesGrayMechAssemblyBay : CompProperties
{
    public List<GRMechChassisDef> allowedChassis;
    public List<GRMechChassisDef> blockedChassis;

    public CompPropertiesGrayMechAssemblyBay()
    {
        compClass = typeof(CompGrayMechAssemblyBay);
    }
}
