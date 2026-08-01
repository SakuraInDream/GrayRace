using System.Collections.Generic;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

internal static class GrayMechSectionCanvasMetrics
{
    private static int cachedMaximumConfiguredCellCount;

    internal static int GetDisplayCellCount(int slotCount)
    {
        int required = Mathf.Max(GrayMechDrydockTabStyle.MinimumDisplaySlotCount, slotCount);
        int columns = GrayMechDrydockTabStyle.SlotGridColumns;
        return ((required + columns - 1) / columns) * columns;
    }

    internal static int GetDisplayRowCount(int slotCount)
    {
        return GetDisplayCellCount(slotCount) / GrayMechDrydockTabStyle.SlotGridColumns;
    }

    internal static int GetMaximumConfiguredCellCount()
    {
        if (cachedMaximumConfiguredCellCount > 0)
        {
            return cachedMaximumConfiguredCellCount;
        }

        int maximum = GrayMechDrydockTabStyle.MinimumDisplaySlotCount;
        List<GRMechSectionLayoutDef> layouts = DefDatabase<GRMechSectionLayoutDef>.AllDefsListForReading;
        for (int i = 0; i < layouts.Count; i++)
        {
            GRMechSectionLayoutDef layout = layouts[i];
            int slotCount = layout?.ResolvedSlots?.Count ?? 0;
            maximum = Mathf.Max(maximum, GetDisplayCellCount(slotCount));
        }

        cachedMaximumConfiguredCellCount = maximum;
        return maximum;
    }

    internal static void GetMaximumSectionSlotCounts(GrayMechDesignSnapshot snapshot, out int weaponCount, out int supportCount)
    {
        weaponCount = 0;
        supportCount = 0;
        if (snapshot?.sections == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.sections.Count; i++)
        {
            GRMechSectionLayoutDef layout = snapshot.sections[i]?.layout;
            List<GRMechSlotEntry> slots = layout?.ResolvedSlots;
            if (slots == null)
            {
                continue;
            }

            int sectionWeaponCount = 0;
            int sectionSupportCount = 0;
            for (int j = 0; j < slots.Count; j++)
            {
                switch (slots[j]?.slotCategory ?? GRMechSlotCategory.Undefined)
                {
                    case GRMechSlotCategory.Weapon:
                        sectionWeaponCount++;
                        break;
                    case GRMechSlotCategory.Utility:
                    case GRMechSlotCategory.Auxiliary:
                        sectionSupportCount++;
                        break;
                }
            }

            weaponCount = Mathf.Max(weaponCount, sectionWeaponCount);
            supportCount = Mathf.Max(supportCount, sectionSupportCount);
        }
    }
}
