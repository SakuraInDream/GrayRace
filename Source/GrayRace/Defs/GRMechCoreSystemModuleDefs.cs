using System.Collections.Generic;
using System;
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

public class GRMechCombatComputerModuleDef : GRMechModuleDef
{
    public GRMechCombatComputerBehavior behavior;
    public GRMechCombatComputerWeaponSelectionMode weaponSelection;
    public GRMechCombatComputerPositioningMode positioning;
    public GRMechCombatComputerCoverPreference coverPreference;
    public float preferredRangeFactor;
    public int powerDraw;

    /// <summary>
    /// 火控策略类。继承自 IFireControlDirector。
    /// 默认 null 时退化为使用 RangeBasedFireControl。
    /// </summary>
    public Type fireControlClass;

    public override int GetNetPower(GRMechChassisDef chassis)
    {
        return base.GetNetPower(chassis) - powerDraw;
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
        {
            yield return error;
        }

        if (powerDraw < 0)
        {
            yield return defName + " has negative powerDraw.";
        }

        if (behavior == GRMechCombatComputerBehavior.Undefined
            || weaponSelection == GRMechCombatComputerWeaponSelectionMode.Undefined
            || positioning == GRMechCombatComputerPositioningMode.Undefined
            || coverPreference == GRMechCombatComputerCoverPreference.Undefined)
        {
            yield return defName + " has incomplete combat computer settings.";
        }

        if (positioning != GRMechCombatComputerPositioningMode.Vanilla && preferredRangeFactor <= 0f)
        {
            yield return defName + " uses non-vanilla positioning but has invalid preferredRangeFactor.";
        }
    }
}
