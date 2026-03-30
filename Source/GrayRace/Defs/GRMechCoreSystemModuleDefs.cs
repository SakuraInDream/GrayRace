using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace SD.GrayRace.Defs;

public class GRMechPowerCoreModuleDef : GRMechModuleDef
{
    public List<ChassisPower> powerByChassis = new();

    public override int GetConfiguredPower(GRMechChassisDef chassis)
    {
        if (chassis != null && powerByChassis != null)
        {
            for (int i = 0; i < powerByChassis.Count; i++)
            {
                ChassisPower entry = powerByChassis[i];
                if (entry?.chassis == chassis)
                {
                    return entry.power;
                }
            }
        }

        return base.GetConfiguredPower(chassis);
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        if (powerByChassis == null || powerByChassis.Count == 0)
        {
            foreach (StatDrawEntry item in base.SpecialDisplayStats(req))
            {
                yield return item;
            }

            yield break;
        }

        StringBuilder sb = new();
        sb.AppendLine("Power output varies by chassis:");
        if (power != 0)
        {
            sb.Append("Default: ");
            sb.AppendLine(FormatSignedPower(GetNetPower(null)));
        }

        for (int i = 0; i < powerByChassis.Count; i++)
        {
            ChassisPower entry = powerByChassis[i];
            if (entry?.chassis == null)
            {
                continue;
            }

            sb.Append(entry.chassis.LabelCap);
            sb.Append(": ");
            sb.AppendLine(FormatSignedPower(GetNetPower(entry.chassis)));
        }

        yield return new StatDrawEntry(
            StatCategoryDefOf.BasicsImportant,
            "Power Budget",
            "Varies by chassis",
            sb.ToString().TrimEndNewlines(),
            3900);
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
        {
            yield return error;
        }

        if (power <= 0 && (powerByChassis == null || powerByChassis.Count == 0))
        {
            yield return defName + " must define positive power output.";
        }

        if (powerByChassis != null && powerByChassis.Count > 0)
        {
            HashSet<GRMechChassisDef> seenChassis = new();
            for (int i = 0; i < powerByChassis.Count; i++)
            {
                ChassisPower entry = powerByChassis[i];
                if (entry == null)
                {
                    yield return defName + " has null powerByChassis entry.";
                    continue;
                }

                if (entry.chassis == null)
                {
                    yield return defName + " powerByChassis entry " + i + " has null chassis.";
                    continue;
                }

                if (entry.power <= 0)
                {
                    yield return defName + " powerByChassis entry " + entry.chassis.defName + " must be positive.";
                }

                if (!seenChassis.Add(entry.chassis))
                {
                    yield return defName + " powerByChassis contains duplicate chassis " + entry.chassis.defName + ".";
                }
            }
        }
    }

    private static string FormatSignedPower(int value)
    {
        return value > 0 ? "+" + value : value.ToString();
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

public class ChassisPower
{
    public GRMechChassisDef chassis;
    public int power;
}
