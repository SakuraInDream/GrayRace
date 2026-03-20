using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using SD.GrayRace.ThingClasses;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

internal static class GrayMechDrydockTabText
{
    internal static float MeasureWrappedTextHeight(string text, float width, GameFont font)
    {
        GameFont oldFont = Text.Font;
        bool oldWrap = Text.WordWrap;
        Text.Font = font;
        Text.WordWrap = true;
        float height = Text.CalcHeight(text ?? string.Empty, Mathf.Max(1f, width));
        Text.Font = oldFont;
        Text.WordWrap = oldWrap;
        return height;
    }

    internal static void DrawWrappedLabel(Rect rect, string text)
    {
        bool oldWrap = Text.WordWrap;
        Text.WordWrap = true;
        Widgets.Label(rect, text);
        Text.WordWrap = oldWrap;
    }

    internal static string BuildCostSummary(List<ThingDefCountClass> costs, StringBuilder buffer)
    {
        buffer.Clear();
        for (int i = 0; i < costs.Count; i++)
        {
            ThingDefCountClass cost = costs[i];
            if (cost?.thingDef == null || cost.count <= 0)
            {
                continue;
            }

            if (buffer.Length > 0)
            {
                buffer.Append(", ");
            }

            buffer.Append(cost.thingDef.LabelCap);
            buffer.Append(" x");
            buffer.Append(cost.count);
        }

        return buffer.ToString();
    }

    internal static string BuildSectionSummary(GrayMechDesignSnapshot snapshot, StringBuilder buffer)
    {
        if (snapshot?.sections == null || snapshot.sections.Count == 0)
        {
            return "None";
        }

        buffer.Clear();
        for (int i = 0; i < snapshot.sections.Count; i++)
        {
            GrayMechSectionSelection section = snapshot.sections[i];
            if (section?.role == null || section.layout == null)
            {
                continue;
            }

            if (buffer.Length > 0)
            {
                buffer.Append("  |  ");
            }

            buffer.Append(section.role.LabelCap);
            buffer.Append(": ");
            buffer.Append(section.layout.LabelCap);
        }

        return buffer.Length == 0 ? "None" : buffer.ToString();
    }

    internal static string BuildLayoutSlotExpression(GRMechSectionLayoutDef layout, StringBuilder buffer)
    {
        if (layout == null)
        {
            return "None";
        }

        int weaponS = 0;
        int weaponM = 0;
        int weaponL = 0;
        int weaponX = 0;
        int weaponT = 0;

        if (layout.componentSlots != null)
        {
            for (int i = 0; i < layout.componentSlots.Count; i++)
            {
                GRMechSlotDef slot = layout.componentSlots[i];
                string sizeName = slot?.slotSize?.defName ?? string.Empty;
                if (sizeName.IndexOf("_M", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    weaponM++;
                }
                else if (sizeName.IndexOf("_L", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    weaponL++;
                }
                else if (sizeName.IndexOf("_X", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    weaponX++;
                }
                else if (sizeName.IndexOf("_T", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    weaponT++;
                }
                else
                {
                    weaponS++;
                }
            }
        }

        buffer.Clear();
        AppendSlotExpressionPart(buffer, "W:S", weaponS);
        AppendSlotExpressionPart(buffer, "W:M", weaponM);
        AppendSlotExpressionPart(buffer, "W:L", weaponL);
        AppendSlotExpressionPart(buffer, "W:X", weaponX);
        AppendSlotExpressionPart(buffer, "W:T", weaponT);
        AppendSlotExpressionPart(buffer, "U:S", layout.smallUtilitySlots);
        AppendSlotExpressionPart(buffer, "U:M", layout.mediumUtilitySlots);
        AppendSlotExpressionPart(buffer, "U:L", layout.largeUtilitySlots);
        AppendSlotExpressionPart(buffer, "A", layout.auxUtilitySlots);
        return buffer.Length == 0 ? "None" : buffer.ToString();
    }

    private static void AppendSlotExpressionPart(StringBuilder buffer, string prefix, int count)
    {
        if (count <= 0)
        {
            return;
        }

        if (buffer.Length > 0)
        {
            buffer.Append("  ");
        }

        buffer.Append(prefix);
        buffer.Append("x");
        buffer.Append(count);
    }

    internal static string GetSourceLabel(Building_GR_Drydock dock)
    {
        if (dock.IsEditingSavedDesign && dock.EditingDesignRecord != null)
        {
            return "Saved Design";
        }

        GRMechPresetDef preset = dock.GetSourcePreset();
        if (preset != null)
        {
            return "Template: " + preset.LabelCap;
        }

        return "Unsaved Draft";
    }

    internal static string BuildDesignTooltip(GrayMechDesignRecord design)
    {
        GrayMechDesignSnapshot snapshot = design?.snapshot;
        if (snapshot?.chassis == null)
        {
            return design?.label ?? string.Empty;
        }

        string modules = GrayMechDesignUtility.BuildModuleSummary(snapshot);
        return design.label + "\n" + "Chassis: " + snapshot.chassis.LabelCap + (modules.NullOrEmpty() ? string.Empty : "\nModules: " + modules);
    }

    internal static string BuildModuleCostSummary(GRMechModuleDef module, StringBuilder buffer)
    {
        if (module?.costList == null || module.costList.Count == 0)
        {
            return "Cost: None";
        }

        buffer.Clear();
        buffer.Append("Cost: ");
        for (int i = 0; i < module.costList.Count; i++)
        {
            ThingDefCountClass cost = module.costList[i];
            if (cost?.thingDef == null || cost.count <= 0)
            {
                continue;
            }

            if (buffer.Length > 6)
            {
                buffer.Append(", ");
            }

            buffer.Append(cost.thingDef.LabelCap);
            buffer.Append(" x");
            buffer.Append(cost.count);
        }

        return buffer.ToString();
    }

    internal static string BuildSlotTooltip(GrayMechResolvedSlot resolvedSlot, GRMechModuleDef module)
    {
        string title = GetSlotDisplayName(resolvedSlot.slot);
        string state = module != null ? "Installed: " + module.LabelCap : "Installed: None";
        return title + "\n" + resolvedSlot.role.LabelCap + "\n" + BuildSlotTypeSummary(resolvedSlot.slot) + "\n" + state;
    }

    internal static string BuildSlotTypeSummary(GRMechSlotDef slot)
    {
        if (slot?.slotType?.ignoreSize ?? false)
        {
            return slot.slotType.label ?? "?";
        }

        string size = slot?.slotSize?.label ?? "?";
        string type = slot?.slotType?.label ?? "?";
        return size + " / " + type;
    }

    internal static string GetSlotDisplayName(GRMechSlotDef slot)
    {
        if (slot == null)
        {
            return "Unknown Slot";
        }

        return slot.label.NullOrEmpty() ? slot.key : slot.label;
    }

    internal static string GetSlotSizeGlyph(GRMechSlotDef slot)
    {
        if (slot?.slotType?.ignoreSize ?? false)
        {
            return string.Empty;
        }

        if (slot?.slotSize?.label.NullOrEmpty() ?? true)
        {
            return "?";
        }

        return slot.slotSize.label.Substring(0, 1).ToUpperInvariant();
    }

    internal static string GetSlotTypeGlyph(GRMechSlotDef slot)
    {
        string typeName = slot?.slotType?.defName ?? string.Empty;
        if (typeName.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "W";
        }

        if (typeName.IndexOf("Utility", StringComparison.OrdinalIgnoreCase) >= 0 && typeName.IndexOf("Aux", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return "U";
        }

        if (typeName.IndexOf("Aux", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "A";
        }

        return "?";
    }

    internal static Color GetSectionAccentColor(GRMechSectionRoleDef role)
    {
        if (IsBowRole(role))
        {
            return GrayMechDrydockTabStyle.MainWeaponColor;
        }

        if (IsSternRole(role))
        {
            return GrayMechDrydockTabStyle.EngineColor;
        }

        return GrayMechDrydockTabStyle.AuxiliaryColor;
    }

    internal static bool IsBowRole(GRMechSectionRoleDef role)
    {
        return role?.defName?.IndexOf("Bow", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static bool IsSternRole(GRMechSectionRoleDef role)
    {
        return role?.defName?.IndexOf("Stern", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static int ComparePresetDefs(GRMechPresetDef left, GRMechPresetDef right)
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

        int orderCompare = left.uiOrder.CompareTo(right.uiOrder);
        if (orderCompare != 0)
        {
            return orderCompare;
        }

        return string.CompareOrdinal(left.label, right.label);
    }

    internal static int CompareSavedDesigns(GrayMechDesignRecord left, GrayMechDesignRecord right)
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

        return string.CompareOrdinal(left.label, right.label);
    }
}
