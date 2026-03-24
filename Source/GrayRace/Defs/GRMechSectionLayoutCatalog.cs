using System.Collections.Generic;
using Verse;

namespace SD.GrayRace.Defs;

public static class GRMechSectionLayoutCatalog
{
    private static Dictionary<GRMechChassisDef, Dictionary<GRMechSectionSlotDef, List<GRMechSectionLayoutDef>>> layoutsByShipSizeAndSlot;
    private static Dictionary<GRMechChassisDef, List<GRMechSectionSlotDef>> sectionSlotsByShipSize;

    private static void EnsureCache()
    {
        if (layoutsByShipSizeAndSlot != null)
        {
            return;
        }

        layoutsByShipSizeAndSlot = new Dictionary<GRMechChassisDef, Dictionary<GRMechSectionSlotDef, List<GRMechSectionLayoutDef>>>();
        sectionSlotsByShipSize = new Dictionary<GRMechChassisDef, List<GRMechSectionSlotDef>>();

        List<GRMechChassisDef> chassisDefs = DefDatabase<GRMechChassisDef>.AllDefsListForReading;
        for (int i = 0; i < chassisDefs.Count; i++)
        {
            GRMechChassisDef chassis = chassisDefs[i];
            if (chassis == null)
            {
                continue;
            }

            List<GRMechSectionSlotDef> sectionSlots = new();
            if (chassis.sectionSlots != null)
            {
                for (int j = 0; j < chassis.sectionSlots.Count; j++)
                {
                    GRMechSectionSlotDef sectionSlot = chassis.sectionSlots[j];
                    if (sectionSlot != null && !sectionSlots.Contains(sectionSlot))
                    {
                        sectionSlots.Add(sectionSlot);
                    }
                }
            }

            sectionSlotsByShipSize[chassis] = sectionSlots;
        }

        List<GRMechSectionLayoutDef> defs = DefDatabase<GRMechSectionLayoutDef>.AllDefsListForReading;
        for (int i = 0; i < defs.Count; i++)
        {
            GRMechSectionLayoutDef layout = defs[i];
            if (layout?.shipSize == null || layout.sectionSlot == null)
            {
                continue;
            }

            if (!layoutsByShipSizeAndSlot.TryGetValue(layout.shipSize, out Dictionary<GRMechSectionSlotDef, List<GRMechSectionLayoutDef>> layoutsBySlot))
            {
                layoutsBySlot = new Dictionary<GRMechSectionSlotDef, List<GRMechSectionLayoutDef>>();
                layoutsByShipSizeAndSlot.Add(layout.shipSize, layoutsBySlot);
            }

            if (!layoutsBySlot.TryGetValue(layout.sectionSlot, out List<GRMechSectionLayoutDef> layouts))
            {
                layouts = new List<GRMechSectionLayoutDef>();
                layoutsBySlot.Add(layout.sectionSlot, layouts);
            }

            layouts.Add(layout);
        }

        foreach (KeyValuePair<GRMechChassisDef, Dictionary<GRMechSectionSlotDef, List<GRMechSectionLayoutDef>>> pair in layoutsByShipSizeAndSlot)
        {
            foreach (KeyValuePair<GRMechSectionSlotDef, List<GRMechSectionLayoutDef>> layoutsBySlot in pair.Value)
            {
                layoutsBySlot.Value.Sort(CompareLayouts);
            }
        }
    }

    public static bool HasAnyLayouts(GRMechChassisDef shipSize)
    {
        EnsureCache();
        return shipSize != null
            && layoutsByShipSizeAndSlot != null
            && layoutsByShipSizeAndSlot.TryGetValue(shipSize, out Dictionary<GRMechSectionSlotDef, List<GRMechSectionLayoutDef>> layoutsBySlot)
            && layoutsBySlot.Count > 0;
    }

    public static bool TryGetSectionSlots(GRMechChassisDef shipSize, out List<GRMechSectionSlotDef> sectionSlots)
    {
        EnsureCache();
        if (shipSize != null && sectionSlotsByShipSize.TryGetValue(shipSize, out sectionSlots))
        {
            return true;
        }

        sectionSlots = null;
        return false;
    }

    public static bool TryGetLayouts(GRMechChassisDef shipSize, GRMechSectionSlotDef sectionSlot, out List<GRMechSectionLayoutDef> layouts)
    {
        EnsureCache();
        if (shipSize != null
            && sectionSlot != null
            && layoutsByShipSizeAndSlot.TryGetValue(shipSize, out Dictionary<GRMechSectionSlotDef, List<GRMechSectionLayoutDef>> layoutsBySlot)
            && layoutsBySlot.TryGetValue(sectionSlot, out layouts))
        {
            return true;
        }

        layouts = null;
        return false;
    }

    public static bool TryGetDefaultLayout(GRMechChassisDef shipSize, GRMechSectionSlotDef sectionSlot, out GRMechSectionLayoutDef layout)
    {
        if (TryGetLayouts(shipSize, sectionSlot, out List<GRMechSectionLayoutDef> layouts) && layouts.Count > 0)
        {
            layout = layouts[0];
            return true;
        }

        layout = null;
        return false;
    }

    private static int CompareLayouts(GRMechSectionLayoutDef left, GRMechSectionLayoutDef right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        int uiOrderCompare = left.uiOrder.CompareTo(right.uiOrder);
        if (uiOrderCompare != 0)
        {
            return uiOrderCompare;
        }

        return string.CompareOrdinal(left.label, right.label);
    }
}
