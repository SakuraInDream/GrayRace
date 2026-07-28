using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Defs;

public class GRMechModuleDef : Def
{
    public List<GRMechSlotDef> compatibleSlots = new();
    public List<GRMechChassisDef> allowedChassis = new();
    public List<GRMechModuleDef> upgradesTo = new();
    public int power;
    public List<ThingDefCountClass> costList = new();
    public List<ResearchProjectDef> researchPrerequisites = new();
    [NoTranslate]
    public string iconPath;
    [NoTranslate]
    public string slotFamily;
    [Unsaved]
    public Texture2D uiIcon = BaseContent.BadTex;
    public ThingDef equipmentDef;
    public ThingDef equipmentStuff;
    public List<StatModifier> statOffsets = new();
    public List<StatModifier> statFactors = new();
    public BodyPartDef anchorBodyPart;
    public int uiOrder;

    public float accuracyTouch = 1f;
    public float accuracyShort = 1f;
    public float accuracyMedium = 1f;
    public float accuracyLong = 1f;
    public float forcedMissRadius;

    public ThingDef ProjectileDef => equipmentDef?.Verbs?.FirstOrDefault()?.defaultProjectile;

    public override void PostLoad()
    {
        base.PostLoad();
        if (!iconPath.NullOrEmpty())
        {
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                Texture2D loadedIcon = ContentFinder<Texture2D>.Get(iconPath, false);
                if (loadedIcon != null)
                {
                    uiIcon = loadedIcon;
                }
            });
        }
    }

    public IEnumerable<ResearchProjectDef> EnumerateResearchPrerequisites()
    {
        if (researchPrerequisites == null)
        {
            yield break;
        }

        HashSet<ResearchProjectDef> seen = new();
        for (int i = 0; i < researchPrerequisites.Count; i++)
        {
            ResearchProjectDef project = researchPrerequisites[i];
            if (project != null && seen.Add(project))
            {
                yield return project;
            }
        }
    }

    public bool Matches(GRMechChassisDef chassis, GRMechSlotEntry slot)
    {
        if (slot?.slotDef == null || chassis == null)
        {
            return false;
        }

        if (!allowedChassis.NullOrEmpty())
        {
            bool chassisAllowed = false;
            for (int i = 0; i < allowedChassis.Count; i++)
            {
                if (allowedChassis[i] == chassis)
                {
                    chassisAllowed = true;
                    break;
                }
            }

            if (!chassisAllowed)
            {
                return false;
            }
        }

        if (compatibleSlots == null)
        {
            return false;
        }

        for (int i = 0; i < compatibleSlots.Count; i++)
        {
            if (compatibleSlots[i] == slot.slotDef)
            {
                return true;
            }
        }

        return false;
    }

    public bool UsesSlotCategory(GRMechSlotCategory category)
    {
        if (compatibleSlots == null)
        {
            return false;
        }

        for (int i = 0; i < compatibleSlots.Count; i++)
        {
            if (compatibleSlots[i]?.slotCategory == category)
            {
                return true;
            }
        }

        return false;
    }

    public virtual int GetConfiguredPower(GRMechChassisDef chassis) => power;

    public virtual int GetNetPower(GRMechChassisDef chassis) => GetConfiguredPower(chassis);

    public virtual int GetPowerGeneration(GRMechChassisDef chassis)
    {
        int netPower = GetNetPower(chassis);
        return netPower > 0 ? netPower : 0;
    }

    public virtual int GetPowerConsumption(GRMechChassisDef chassis)
    {
        int netPower = GetNetPower(chassis);
        return netPower < 0 ? -netPower : 0;
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        foreach (StatDrawEntry item in base.SpecialDisplayStats(req))
        {
            yield return item;
        }

        int baseNetPower = GetNetPower(null);
        if (baseNetPower == 0)
        {
            yield break;
        }

        yield return new StatDrawEntry(
            StatCategoryDefOf.BasicsImportant,
            "Power Budget",
            FormatSignedPower(baseNetPower),
            "Positive values provide reactor output. Negative values consume reactor output.",
            3900);
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
        {
            yield return error;
        }

        if (compatibleSlots == null || compatibleSlots.Count == 0)
        {
            yield return defName + " must define at least one compatible slot.";
            yield break;
        }

        HashSet<GRMechSlotDef> seenSlots = new();
        GRMechSlotCategory? firstCategory = null;
        for (int i = 0; i < compatibleSlots.Count; i++)
        {
            GRMechSlotDef slot = compatibleSlots[i];
            if (slot == null)
            {
                yield return defName + " has null compatible slot entry.";
                continue;
            }

            if (!seenSlots.Add(slot))
            {
                yield return defName + " contains duplicate compatible slot " + slot.defName + ".";
            }

            if (firstCategory == null)
            {
                firstCategory = slot.slotCategory;
            }
            else if (firstCategory != slot.slotCategory)
            {
                yield return defName + " mixes incompatible slot categories in compatibleSlots.";
            }
        }

        if (equipmentStuff != null && equipmentDef == null)
        {
            yield return defName + " has equipmentStuff without equipmentDef.";
        }

        if (equipmentStuff != null && !equipmentStuff.IsStuff)
        {
            yield return defName + " equipmentStuff is not a stuff ThingDef.";
        }

        if (!allowedChassis.NullOrEmpty())
        {
            HashSet<GRMechChassisDef> seenChassis = new();
            for (int i = 0; i < allowedChassis.Count; i++)
            {
                GRMechChassisDef chassis = allowedChassis[i];
                if (chassis == null)
                {
                    yield return defName + " has null allowedChassis entry.";
                    continue;
                }

                if (!seenChassis.Add(chassis))
                {
                    yield return defName + " contains duplicate allowed chassis " + chassis.defName + ".";
                }
            }
        }

        if (upgradesTo != null && upgradesTo.Count > 0)
        {
            HashSet<GRMechModuleDef> seenUpgradeTargets = new();
            for (int i = 0; i < upgradesTo.Count; i++)
            {
                GRMechModuleDef next = upgradesTo[i];
                if (next == null)
                {
                    yield return defName + " has null upgradesTo entry.";
                    continue;
                }

                if (next == this)
                {
                    yield return defName + " cannot upgrade to itself.";
                }

                if (!seenUpgradeTargets.Add(next))
                {
                    yield return defName + " has duplicate upgradesTo entry " + next.defName + ".";
                }

                if (!CompatibleSlotsEqual(next))
                {
                    yield return defName + " upgradesTo target " + next.defName + " has mismatched compatibleSlots.";
                }
            }
        }

    }

    private bool CompatibleSlotsEqual(GRMechModuleDef other)
    {
        if (other == null || other.compatibleSlots == null)
        {
            return false;
        }

        int thisCount = compatibleSlots?.Count ?? 0;
        int otherCount = other.compatibleSlots.Count;
        if (thisCount != otherCount)
        {
            return false;
        }

        if (thisCount == 0)
        {
            return true;
        }

        HashSet<GRMechSlotDef> otherSlots = new(other.compatibleSlots);
        for (int i = 0; i < thisCount; i++)
        {
            if (!otherSlots.Contains(compatibleSlots[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string FormatSignedPower(int value)
    {
        return value > 0 ? "+" + value : value.ToString();
    }
}
