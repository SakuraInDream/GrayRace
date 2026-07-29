using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SD.GrayRace.Defs;

public class GRMechSectionLayoutDef : Def
{
    public GRMechChassisDef shipSize;
    public GRMechSectionSlotDef sectionSlot;
    public List<GRMechSlotEntry> slots = new();
    public List<ThingDefCountClass> costList = new();
    public List<ResearchProjectDef> researchPrerequisites = new();
    public int uiOrder;

    internal List<GRMechSlotEntry> ResolvedSlots => slots ??= new List<GRMechSlotEntry>();

    public IEnumerable<ResearchProjectDef> EnumerateResearchPrerequisites()
    {
        if (researchPrerequisites == null)
        {
            yield break;
        }

        HashSet<ResearchProjectDef> seen = new();
        foreach (var project in researchPrerequisites)
        {
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

        if (shipSize == null)
        {
            yield return defName + " has null shipSize.";
        }

        if (sectionSlot == null)
        {
            yield return defName + " has null sectionSlot.";
        }
        else if (shipSize?.sectionSlots != null && !shipSize.sectionSlots.Contains(sectionSlot))
        {
            yield return defName + " uses sectionSlot " + sectionSlot.defName + " not declared on shipSize " + shipSize.defName + ".";
        }

        HashSet<string> seenKeys = new();
        int explicitSlotCount = 0;
        if (slots != null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                GRMechSlotEntry slot = slots[i];
                if (slot == null)
                {
                    yield return defName + " has null slot entry.";
                    continue;
                }

                explicitSlotCount++;
                if (slot.key.NullOrEmpty())
                {
                    yield return defName + " has slot with empty key.";
                }
                else if (!seenKeys.Add(slot.key))
                {
                    yield return defName + " has duplicate slot key " + slot.key + ".";
                }

                if (slot.slotDef == null)
                {
                    yield return defName + " slot " + slot.key + " has null slotDef.";
                }
                else if (slot.componentType == GRMechSlotComponentType.Undefined)
                {
                    yield return defName + " slot " + slot.key + " has undefined componentType.";
                }
                else if (slot.slotCategory == GRMechSlotCategory.CoreSystem)
                {
                    yield return defName + " slot " + slot.key + " must not use a core-system slotDef inside section layouts.";
                }

                if (slot.slotDef != null && slot.slotSize == null)
                {
                    yield return defName + " slot " + slot.key + " has null slotSize.";
                }

                if (slot.weaponMountMode == GRMechWeaponMountMode.PrimaryEquipment
                    && slot.componentType != GRMechSlotComponentType.Weapon)
                {
                    yield return defName + " slot " + slot.key + " uses PrimaryEquipment but is not a Weapon slot.";
                }
            }
        }

        if (explicitSlotCount <= 0)
        {
            yield return defName + " has no slots.";
        }
    }
}
