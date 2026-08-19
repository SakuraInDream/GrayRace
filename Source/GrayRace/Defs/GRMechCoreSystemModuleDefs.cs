using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SD.GrayRace.Defs;

public class GRMechPowerCoreModuleDef : GRMechModuleDef
{
    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
        {
            yield return error;
        }

        if (power <= 0)
        {
            yield return defName + " must define positive power output.";
        }

        if (allowedChassis.NullOrEmpty())
        {
            yield return defName + " must define at least one allowed chassis.";
        }

        if (costList == null || costList.Count == 0)
        {
            yield return defName + " must define costList.";
            yield break;
        }

        HashSet<ThingDef> seenCosts = new();
        for (int i = 0; i < costList.Count; i++)
        {
            ThingDefCountClass cost = costList[i];
            if (cost?.thingDef == null)
            {
                yield return defName + " has null cost entry.";
            }
            else if (cost.count <= 0)
            {
                yield return defName + " has non-positive cost for " + cost.thingDef.defName + ".";
            }
            else if (!seenCosts.Add(cost.thingDef))
            {
                yield return defName + " contains duplicate cost " + cost.thingDef.defName + ".";
            }
        }
    }
}

public class GRMechThrusterModuleDef : GRMechModuleDef
{
}

public class GRMechSensorModuleDef : GRMechModuleDef
{
}
