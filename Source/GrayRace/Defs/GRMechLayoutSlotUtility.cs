using System.Collections.Generic;
namespace SD.GrayRace.Defs;

public static class GRMechLayoutSlotUtility
{
    public static IEnumerable<GRMechSlotEntry> EnumerateSlots(GRMechSectionLayoutDef layout)
    {
        List<GRMechSlotEntry> slots = layout?.ResolvedSlots;
        if (slots == null)
        {
            yield break;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            GRMechSlotEntry slot = slots[i];
            if (slot != null)
            {
                yield return slot;
            }
        }
    }

    public static int CountSlots(GRMechSectionLayoutDef layout)
    {
        List<GRMechSlotEntry> slots = layout?.ResolvedSlots;
        if (slots == null)
        {
            return 0;
        }

        return slots.Count;
    }

    public static bool TryGetFirstSlot(GRMechSectionLayoutDef layout, out GRMechSlotEntry slot)
    {
        List<GRMechSlotEntry> slots = layout?.ResolvedSlots;
        if (slots != null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
                {
                    slot = slots[i];
                    return true;
                }
            }
        }

        slot = null;
        return false;
    }
}
