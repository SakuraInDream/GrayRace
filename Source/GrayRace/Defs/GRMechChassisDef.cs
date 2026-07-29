using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SD.GrayRace.Defs;

public class GRMechChassisDef : Def
{
    public PawnKindDef pawnKindDef;
    public List<GRMechSectionSlotDef> sectionSlots = new();
    public List<GRMechSlotEntry> requiredComponentSlots = new();
    public int fixedWorkTicks = 60000;
    public List<ResearchProjectDef> researchPrerequisites = new();
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

    public bool TryGetRequiredComponentSlot(string slotKey, out GRMechSlotEntry slot)
    {
        if (!slotKey.NullOrEmpty() && requiredComponentSlots != null)
        {
            for (int i = 0; i < requiredComponentSlots.Count; i++)
            {
                GRMechSlotEntry current = requiredComponentSlots[i];
                if (current != null && current.key == slotKey)
                {
                    slot = current;
                    return true;
                }
            }
        }

        slot = null;
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

        if (requiredComponentSlots == null || requiredComponentSlots.Count == 0)
        {
            yield return defName + " has no required component slots.";
        }
        else
        {
            HashSet<string> seenRequiredSlotKeys = new();
            for (int i = 0; i < requiredComponentSlots.Count; i++)
            {
                GRMechSlotEntry slot = requiredComponentSlots[i];
                if (slot == null)
                {
                    yield return defName + " has null required component slot entry.";
                    continue;
                }

                if (slot.key.NullOrEmpty())
                {
                    yield return defName + " has required component slot with empty key.";
                }
                else if (!seenRequiredSlotKeys.Add(slot.key))
                {
                    yield return defName + " has duplicate required component slot key " + slot.key + ".";
                }

                if (slot.slotDef == null)
                {
                    yield return defName + " required component slot " + slot.key + " has null slotDef.";
                }
                else if (slot.slotCategory != GRMechSlotCategory.CoreSystem)
                {
                    yield return defName + " required component slot " + slot.key + " must use a core-system slotDef.";
                }
                else if (slot.coreRole == GRMechCoreComponentRole.Undefined)
                {
                    yield return defName + " required component slot " + slot.key + " uses slotDef without coreRole.";
                }

                if (slot.weaponMountMode == GRMechWeaponMountMode.PrimaryEquipment
                    && slot.componentType != GRMechSlotComponentType.Weapon)
                {
                    yield return defName + " required component slot " + slot.key + " uses PrimaryEquipment but is not a Weapon slot.";
                }
            }
        }

        if (sectionSlots == null || sectionSlots.Count == 0)
        {
            yield return defName + " has no section slots.";
        }
        else
        {
            HashSet<GRMechSectionSlotDef> seenSectionSlots = new();
            for (int i = 0; i < sectionSlots.Count; i++)
            {
                GRMechSectionSlotDef sectionSlot = sectionSlots[i];
                if (sectionSlot == null)
                {
                    yield return defName + " has null section slot entry.";
                    continue;
                }

                if (!seenSectionSlots.Add(sectionSlot))
                {
                    yield return defName + " has duplicate section slot " + sectionSlot.defName + ".";
                }

                // if (!GRMechSectionLayoutCatalog.TryGetLayouts(this, sectionSlot, out List<GRMechSectionLayoutDef> layouts) || layouts.Count == 0)
                // {
                //     yield return defName + " section slot " + sectionSlot.defName + " has no section layouts.";
                // }
            }
        }
    }
}
