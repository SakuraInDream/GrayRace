using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SD.GrayRace.Defs;

public class GRMechSlotTypeDef : Def
{
    public bool ignoreSize;
}

public class GRMechSlotSizeDef : Def
{
}

public class GRMechComponentSlotTemplateDef : Def
{
    public GRMechSlotTypeDef slotType;
    public GRMechSlotSizeDef slotSize;
    public bool isFixed;
}

public class GRMechSectionRoleDef : Def
{
}

public class GRMechSlotDef
{
    public string key;
    public string label;
    public GRMechComponentSlotTemplateDef template;
    public BodyPartDef anchorBodyPart;
    public int uiOrder;

    public GRMechSlotTypeDef slotType => template?.slotType;

    public GRMechSlotSizeDef slotSize => template?.slotSize;
}

public class GRMechChassisSectionDef
{
    public GRMechSectionRoleDef role;
    public List<GRMechSectionLayoutDef> layouts = new();
    public GRMechSectionLayoutDef defaultLayout;
}

public class GRMechPresetSectionDef
{
    public GRMechSectionRoleDef role;
    public GRMechSectionLayoutDef layout;
}

public class GRMechPresetModuleDef
{
    public string slotKey;
    public GRMechModuleDef module;
}

public class GRMechChassisDef : Def
{
    public PawnKindDef pawnKindDef;
    public List<ThingDefCountClass> baseCostList = new();
    public int fixedWorkTicks = 60000;
    public List<ResearchProjectDef> researchPrerequisites = new();
    public List<GRMechChassisSectionDef> sections = new();
    public string designerPreviewPath;
    public int uiOrder;

    public ThingDef ProducedRace => pawnKindDef?.race;

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

    public bool TryGetSection(GRMechSectionRoleDef role, out GRMechChassisSectionDef section)
    {
        if (sections != null)
        {
            for (int i = 0; i < sections.Count; i++)
            {
                GRMechChassisSectionDef current = sections[i];
                if (current?.role == role)
                {
                    section = current;
                    return true;
                }
            }
        }

        section = null;
        return false;
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
        {
            yield return error;
        }

        if (pawnKindDef == null)
        {
            yield return defName + " has null pawnKindDef.";
        }
        else if (pawnKindDef.race == null)
        {
            yield return defName + " has pawnKindDef without race.";
        }

        if (fixedWorkTicks <= 0)
        {
            yield return defName + " must have fixedWorkTicks > 0.";
        }

        if (sections == null || sections.Count == 0)
        {
            yield return defName + " has no mech sections.";
            yield break;
        }

        HashSet<GRMechSectionRoleDef> seenRoles = new();
        for (int i = 0; i < sections.Count; i++)
        {
            GRMechChassisSectionDef section = sections[i];
            if (section == null)
            {
                yield return defName + " has null section entry.";
                continue;
            }

            if (section.role == null)
            {
                yield return defName + " has section with null role.";
            }
            else if (!seenRoles.Add(section.role))
            {
                yield return defName + " has duplicate section role " + section.role.defName + ".";
            }

            if (section.layouts == null || section.layouts.Count == 0)
            {
                yield return defName + " section " + section.role?.defName + " has no layouts.";
                continue;
            }

            if (section.defaultLayout == null)
            {
                yield return defName + " section " + section.role?.defName + " has null defaultLayout.";
            }

            bool defaultFound = false;
            for (int j = 0; j < section.layouts.Count; j++)
            {
                GRMechSectionLayoutDef layout = section.layouts[j];
                if (layout == null)
                {
                    yield return defName + " section " + section.role?.defName + " has null layout.";
                    continue;
                }

                if (layout.sectionRole != section.role)
                {
                    yield return defName + " layout " + layout.defName + " does not match section role " + section.role?.defName + ".";
                }

                if (layout == section.defaultLayout)
                {
                    defaultFound = true;
                }
            }

            if (section.defaultLayout != null && !defaultFound)
            {
                yield return defName + " section " + section.role?.defName + " defaultLayout is not present in layouts.";
            }
        }
    }
}

public class GRMechSectionLayoutDef : Def
{
    public GRMechSectionRoleDef sectionRole;
    public List<GRMechSlotDef> componentSlots = new();
    public int smallUtilitySlots;
    public int mediumUtilitySlots;
    public int largeUtilitySlots;
    public int auxUtilitySlots;
    public List<ThingDefCountClass> additionalCostList = new();
    public List<ResearchProjectDef> researchPrerequisites = new();
    public int uiOrder;

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

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
        {
            yield return error;
        }

        if (sectionRole == null)
        {
            yield return defName + " has null sectionRole.";
        }

        HashSet<string> seenKeys = new();
        bool hasAnySlots = false;
        if (componentSlots != null)
        {
            for (int i = 0; i < componentSlots.Count; i++)
            {
                GRMechSlotDef slot = componentSlots[i];
                if (slot == null)
                {
                    yield return defName + " has null slot entry.";
                    continue;
                }

                hasAnySlots = true;
                if (slot.key.NullOrEmpty())
                {
                    yield return defName + " has slot with empty key.";
                }
                else if (!seenKeys.Add(slot.key))
                {
                    yield return defName + " has duplicate slot key " + slot.key + ".";
                }

                if (slot.template == null)
                {
                    yield return defName + " slot " + slot.key + " has null template.";
                }

                if (slot.slotType == null)
                {
                    yield return defName + " slot " + slot.key + " has null slotType.";
                }

                if (slot.slotSize == null && !(slot.slotType?.ignoreSize ?? false))
                {
                    yield return defName + " slot " + slot.key + " has null slotSize.";
                }
            }
        }

        hasAnySlots |= ValidateGeneratedSlots(this, ref seenKeys, GRMechLayoutSlotUtility.SmallUtilitySlotSize, smallUtilitySlots, "SMALL_UTILITY", out string smallError);
        if (smallError != null)
        {
            yield return smallError;
        }

        hasAnySlots |= ValidateGeneratedSlots(this, ref seenKeys, GRMechLayoutSlotUtility.MediumUtilitySlotSize, mediumUtilitySlots, "MEDIUM_UTILITY", out string mediumError);
        if (mediumError != null)
        {
            yield return mediumError;
        }

        hasAnySlots |= ValidateGeneratedSlots(this, ref seenKeys, GRMechLayoutSlotUtility.LargeUtilitySlotSize, largeUtilitySlots, "LARGE_UTILITY", out string largeError);
        if (largeError != null)
        {
            yield return largeError;
        }

        hasAnySlots |= ValidateGeneratedSlots(this, ref seenKeys, null, auxUtilitySlots, "AUX_UTILITY", out string auxError);
        if (auxError != null)
        {
            yield return auxError;
        }

        if (!hasAnySlots)
        {
            yield return defName + " has no slots.";
            yield break;
        }
    }

    private static bool ValidateGeneratedSlots(GRMechSectionLayoutDef layout, ref HashSet<string> seenKeys, GRMechSlotSizeDef sizeDef, int count, string familyKey, out string error)
    {
        error = null;
        if (count < 0)
        {
            error = layout.defName + " has negative " + familyKey.ToLowerInvariant() + " count.";
            return false;
        }

        if (count == 0)
        {
            return false;
        }

        for (int i = 1; i <= count; i++)
        {
            string key = GRMechLayoutSlotUtility.MakeGeneratedKey(layout, familyKey, sizeDef, i);
            if (!seenKeys.Add(key))
            {
                error = layout.defName + " generates duplicate slot key " + key + ".";
                return true;
            }
        }

        return true;
    }
}

public static class GRMechLayoutSlotUtility
{
    private static GRMechSlotTypeDef cachedUtilitySlotType;
    private static GRMechSlotTypeDef cachedAuxSlotType;
    private static GRMechSlotSizeDef cachedSmallUtilitySlotSize;
    private static GRMechSlotSizeDef cachedMediumUtilitySlotSize;
    private static GRMechSlotSizeDef cachedLargeUtilitySlotSize;

    private static GRMechSlotTypeDef UtilitySlotType => cachedUtilitySlotType ??= DefDatabase<GRMechSlotTypeDef>.GetNamedSilentFail("GR_MechSlot_Utility");
    private static GRMechSlotTypeDef AuxSlotType => cachedAuxSlotType ??= DefDatabase<GRMechSlotTypeDef>.GetNamedSilentFail("GR_MechSlot_Aux");
    public static GRMechSlotSizeDef SmallUtilitySlotSize => cachedSmallUtilitySlotSize ??= DefDatabase<GRMechSlotSizeDef>.GetNamedSilentFail("GR_MechSlotSize_S");
    public static GRMechSlotSizeDef MediumUtilitySlotSize => cachedMediumUtilitySlotSize ??= DefDatabase<GRMechSlotSizeDef>.GetNamedSilentFail("GR_MechSlotSize_M");
    public static GRMechSlotSizeDef LargeUtilitySlotSize => cachedLargeUtilitySlotSize ??= DefDatabase<GRMechSlotSizeDef>.GetNamedSilentFail("GR_MechSlotSize_L");

    public static IEnumerable<GRMechSlotDef> EnumerateSlots(GRMechSectionLayoutDef layout)
    {
        if (layout == null)
        {
            yield break;
        }

        if (layout.componentSlots != null)
        {
            for (int i = 0; i < layout.componentSlots.Count; i++)
            {
                GRMechSlotDef slot = layout.componentSlots[i];
                if (slot != null)
                {
                    yield return slot;
                }
            }
        }

        foreach (GRMechSlotDef slot in EnumerateGeneratedUtilitySlots(layout, SmallUtilitySlotSize, layout.smallUtilitySlots, "SMALL_UTILITY", 1000))
        {
            yield return slot;
        }

        foreach (GRMechSlotDef slot in EnumerateGeneratedUtilitySlots(layout, MediumUtilitySlotSize, layout.mediumUtilitySlots, "MEDIUM_UTILITY", 1100))
        {
            yield return slot;
        }

        foreach (GRMechSlotDef slot in EnumerateGeneratedUtilitySlots(layout, LargeUtilitySlotSize, layout.largeUtilitySlots, "LARGE_UTILITY", 1200))
        {
            yield return slot;
        }

        if (layout.auxUtilitySlots > 0)
        {
            for (int i = 1; i <= layout.auxUtilitySlots; i++)
            {
                yield return new GRMechSlotDef
                {
                    key = MakeGeneratedKey(layout, "AUX_UTILITY", null, i),
                    label = "Auxiliary " + i,
                    template = new GRMechComponentSlotTemplateDef
                    {
                        defName = MakeGeneratedKey(layout, "AUX_UTILITY_TEMPLATE", null, i),
                        label = "Aux",
                        slotType = AuxSlotType,
                        slotSize = null
                    },
                    uiOrder = 2000 + i
                };
            }
        }
    }

    public static int CountSlots(GRMechSectionLayoutDef layout)
    {
        int count = 0;
        foreach (GRMechSlotDef slot in EnumerateSlots(layout))
        {
            if (slot != null)
            {
                count++;
            }
        }

        return count;
    }

    private static IEnumerable<GRMechSlotDef> EnumerateGeneratedUtilitySlots(GRMechSectionLayoutDef layout, GRMechSlotSizeDef slotSize, int count, string familyKey, int baseOrder)
    {
        if (slotSize == null || count <= 0)
        {
            yield break;
        }

        for (int i = 1; i <= count; i++)
        {
            yield return new GRMechSlotDef
            {
                key = MakeGeneratedKey(layout, familyKey, slotSize, i),
                label = slotSize.LabelCap + " utility " + i,
                template = new GRMechComponentSlotTemplateDef
                {
                    defName = MakeGeneratedKey(layout, familyKey + "_TEMPLATE", slotSize, i),
                    label = slotSize.LabelCap + " utility",
                    slotType = UtilitySlotType,
                    slotSize = slotSize
                },
                uiOrder = baseOrder + i
            };
        }
    }

    public static string MakeGeneratedKey(GRMechSectionLayoutDef layout, string familyKey, GRMechSlotSizeDef slotSize, int index)
    {
        string sizePart = slotSize != null ? "_" + slotSize.defName : string.Empty;
        return layout.defName + "_" + familyKey + sizePart + "_" + index;
    }
}

public class GRMechModuleDef : Def
{
    public List<GRMechSlotTypeDef> allowedSlotTypes = new();
    public List<GRMechSlotSizeDef> allowedSlotSizes = new();
    public List<GRMechChassisDef> allowedChassis = new();
    public List<ThingDefCountClass> costList = new();
    public List<ResearchProjectDef> researchPrerequisites = new();
    public ThingDef equipmentDef;
    public ThingDef equipmentStuff;
    public HediffDef hediffToApply;
    public BodyPartDef anchorBodyPart;
    public int uiOrder;

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

    public bool Matches(GRMechChassisDef chassis, GRMechSlotDef slot)
    {
        if (slot == null)
        {
            return false;
        }

        if (allowedChassis != null && allowedChassis.Count > 0 && !allowedChassis.Contains(chassis))
        {
            return false;
        }

        if (allowedSlotTypes != null && allowedSlotTypes.Count > 0 && !allowedSlotTypes.Contains(slot.slotType))
        {
            return false;
        }

        if (!(slot.slotType?.ignoreSize ?? false) && allowedSlotSizes != null && allowedSlotSizes.Count > 0 && !allowedSlotSizes.Contains(slot.slotSize))
        {
            return false;
        }

        return true;
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
        {
            yield return error;
        }

        if (equipmentStuff != null && equipmentDef == null)
        {
            yield return defName + " has equipmentStuff without equipmentDef.";
        }

        if (equipmentStuff != null && !equipmentStuff.IsStuff)
        {
            yield return defName + " equipmentStuff is not a stuff ThingDef.";
        }
    }
}

public class GRMechPresetDef : Def
{
    public GRMechChassisDef chassis;
    public List<GRMechPresetSectionDef> sections = new();
    public List<GRMechPresetModuleDef> modules = new();
    public int uiOrder;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
        {
            yield return error;
        }

        if (chassis == null)
        {
            yield return defName + " has null chassis.";
            yield break;
        }

        Dictionary<string, GRMechSlotDef> selectedSlots = new(StringComparer.Ordinal);
        HashSet<GRMechSectionRoleDef> seenRoles = new();

        if (sections == null || sections.Count == 0)
        {
            yield return defName + " has no section selections.";
            yield break;
        }

        for (int i = 0; i < sections.Count; i++)
        {
            GRMechPresetSectionDef sectionSelection = sections[i];
            if (sectionSelection == null)
            {
                yield return defName + " has null section selection.";
                continue;
            }

            if (sectionSelection.role == null || sectionSelection.layout == null)
            {
                yield return defName + " has section selection with null role or layout.";
                continue;
            }

            if (!seenRoles.Add(sectionSelection.role))
            {
                yield return defName + " has duplicate section role " + sectionSelection.role.defName + ".";
            }

            if (!chassis.TryGetSection(sectionSelection.role, out GRMechChassisSectionDef section))
            {
                yield return defName + " references role " + sectionSelection.role.defName + " not present on chassis " + chassis.defName + ".";
                continue;
            }

            if (!section.layouts.Contains(sectionSelection.layout))
            {
                yield return defName + " layout " + sectionSelection.layout.defName + " is not allowed by chassis " + chassis.defName + ".";
            }

            if (sectionSelection.layout.sectionRole != sectionSelection.role)
            {
                yield return defName + " layout " + sectionSelection.layout.defName + " does not match role " + sectionSelection.role.defName + ".";
            }

            foreach (GRMechSlotDef slot in GRMechLayoutSlotUtility.EnumerateSlots(sectionSelection.layout))
            {
                if (slot == null || slot.key.NullOrEmpty())
                {
                    continue;
                }

                if (selectedSlots.ContainsKey(slot.key))
                {
                    yield return defName + " selected layouts contain duplicate slot key " + slot.key + ".";
                    continue;
                }

                selectedSlots.Add(slot.key, slot);
            }
        }

        if (modules == null)
        {
            yield break;
        }

        HashSet<string> assignedSlots = new(StringComparer.Ordinal);
        for (int i = 0; i < modules.Count; i++)
        {
            GRMechPresetModuleDef moduleSelection = modules[i];
            if (moduleSelection == null)
            {
                yield return defName + " has null module assignment.";
                continue;
            }

            if (moduleSelection.slotKey.NullOrEmpty() || moduleSelection.module == null)
            {
                yield return defName + " has module assignment with null slotKey or module.";
                continue;
            }

            if (!assignedSlots.Add(moduleSelection.slotKey))
            {
                yield return defName + " assigns multiple modules to slot " + moduleSelection.slotKey + ".";
            }

            if (!selectedSlots.TryGetValue(moduleSelection.slotKey, out GRMechSlotDef slot))
            {
                yield return defName + " assigns module to unknown slot " + moduleSelection.slotKey + ".";
                continue;
            }

            if (!moduleSelection.module.Matches(chassis, slot))
            {
                yield return defName + " module " + moduleSelection.module.defName + " does not match slot " + moduleSelection.slotKey + ".";
            }
        }
    }
}
