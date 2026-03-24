using System;
using Verse;

namespace SD.GrayRace.Defs;

public static class GRMechSectionSlotUtility
{
    public const string RequiredSlotOwnerId = "required";

    public static string GetSlotId(GRMechSectionSlotDef sectionSlot)
    {
        if (sectionSlot == null)
        {
            return RequiredSlotOwnerId;
        }

        if (!(sectionSlot?.slotId).NullOrEmpty())
        {
            return sectionSlot.slotId;
        }

        string defName = sectionSlot?.defName ?? string.Empty;
        if (defName.IndexOf("Bow", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "bow";
        }

        if (defName.IndexOf("Mid", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "mid";
        }

        if (defName.IndexOf("Stern", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "stern";
        }

        return defName;
    }

    public static string GetScopedSlotKey(GRMechSectionSlotDef sectionSlot, string slotKey)
    {
        return GetSlotId(sectionSlot) + "/" + (slotKey ?? string.Empty);
    }

    public static bool Matches(GRMechSectionSlotDef left, GRMechSectionSlotDef right)
    {
        return string.Equals(GetSlotId(left), GetSlotId(right), StringComparison.Ordinal);
    }

    public static int Compare(GRMechSectionSlotDef left, GRMechSectionSlotDef right)
    {
        int leftOrder = left?.uiOrder ?? int.MaxValue;
        int rightOrder = right?.uiOrder ?? int.MaxValue;
        int orderCompare = leftOrder.CompareTo(rightOrder);
        if (orderCompare != 0)
        {
            return orderCompare;
        }

        return string.CompareOrdinal(GetSlotId(left), GetSlotId(right));
    }

    public static bool IsBow(GRMechSectionSlotDef sectionSlot)
    {
        return GetSlotId(sectionSlot) == "bow";
    }

    public static bool IsMid(GRMechSectionSlotDef sectionSlot)
    {
        return GetSlotId(sectionSlot) == "mid";
    }

    public static bool IsStern(GRMechSectionSlotDef sectionSlot)
    {
        return GetSlotId(sectionSlot) == "stern";
    }
}
