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
    private readonly struct TextMeasureKey : IEquatable<TextMeasureKey>
    {
        private readonly string text;
        private readonly int width;
        private readonly GameFont font;

        public TextMeasureKey(string text, int width, GameFont font)
        {
            this.text = text ?? string.Empty;
            this.width = width;
            this.font = font;
        }

        public bool Equals(TextMeasureKey other)
        {
            return width == other.width && font == other.font && string.Equals(text, other.text, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TextMeasureKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + width;
                hash = hash * 31 + (int)font;
                hash = hash * 31 + (text?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }

    private static readonly Dictionary<TextMeasureKey, float> CachedTextHeights = new();
    private static readonly Dictionary<GRMechSectionLayoutDef, string> CachedLayoutExpressions = new();
    private static readonly Dictionary<GRMechModuleDef, string> CachedModuleCostSummaries = new();

    internal static float MeasureWrappedTextHeight(string text, float width, GameFont font)
    {
        int roundedWidth = Mathf.Max(1, Mathf.CeilToInt(width));
        TextMeasureKey key = new(text, roundedWidth, font);
        if (CachedTextHeights.TryGetValue(key, out float cachedHeight))
        {
            return cachedHeight;
        }

        GameFont oldFont = Text.Font;
        bool oldWrap = Text.WordWrap;
        Text.Font = font;
        Text.WordWrap = true;
        float height = Text.CalcHeight(text ?? string.Empty, roundedWidth);
        Text.Font = oldFont;
        Text.WordWrap = oldWrap;
        if (CachedTextHeights.Count > 4096)
        {
            CachedTextHeights.Clear();
        }

        CachedTextHeights[key] = height;
        return height;
    }

    internal static void DrawWrappedLabel(Rect rect, string text)
    {
        bool oldWrap = Text.WordWrap;
        Text.WordWrap = true;
        Widgets.Label(rect, text);
        Text.WordWrap = oldWrap;
    }

    internal static void DrawWrappedLabelCentered(Rect rect, string text)
    {
        TextAnchor oldAnchor = Text.Anchor;
        bool oldWrap = Text.WordWrap;
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.WordWrap = true;
        Widgets.Label(rect, text);
        Text.WordWrap = oldWrap;
        Text.Anchor = oldAnchor;
    }

    internal static float GetPillHeight(string text, float width)
    {
        return MeasureWrappedTextHeight(text, Mathf.Max(1f, width - 8f), GameFont.Tiny) + 8f;
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

    internal static string BuildLayoutSlotExpression(GRMechSectionLayoutDef layout, StringBuilder buffer)
    {
        if (layout == null)
        {
            return "None";
        }

        if (CachedLayoutExpressions.TryGetValue(layout, out string cached))
        {
            return cached;
        }

        int corePower = 0;
        int coreThruster = 0;
        int coreSensor = 0;
        int coreComputer = 0;

        foreach (GRMechSlotEntry slot in GRMechLayoutSlotUtility.EnumerateSlots(layout))
        {
            switch (slot?.slotCategory ?? GRMechSlotCategory.Undefined)
            {
                case GRMechSlotCategory.CoreSystem:
                    switch (slot.coreRole)
                    {
                        case GRMechCoreComponentRole.PowerCore:
                            corePower++;
                            break;
                        case GRMechCoreComponentRole.Thruster:
                            coreThruster++;
                            break;
                        case GRMechCoreComponentRole.Sensor:
                            coreSensor++;
                            break;
                        case GRMechCoreComponentRole.CombatComputer:
                            coreComputer++;
                            break;
                    }

                    break;
            }
        }

        buffer.Clear();
        List<GRMechSlotSizeDef> orderedSlotSizes = new(DefDatabase<GRMechSlotSizeDef>.AllDefsListForReading);
        orderedSlotSizes.Sort(CompareSlotSizeDefs);
        AppendSizedSlotExpressionParts(layout, GRMechSlotCategory.Weapon, "W", orderedSlotSizes, buffer);
        AppendSizedSlotExpressionParts(layout, GRMechSlotCategory.Utility, "U", orderedSlotSizes, buffer);
        AppendSlotExpressionPart(buffer, "A", CountSlotsByCategory(layout, GRMechSlotCategory.Auxiliary));
        AppendSlotExpressionPart(buffer, "Core:R", corePower);
        AppendSlotExpressionPart(buffer, "Core:E", coreThruster);
        AppendSlotExpressionPart(buffer, "Core:S", coreSensor);
        AppendSlotExpressionPart(buffer, "Core:C", coreComputer);
        string expression = buffer.Length == 0 ? "None" : buffer.ToString();
        CachedLayoutExpressions[layout] = expression;
        return expression;
    }

    private static void AppendSizedSlotExpressionParts(GRMechSectionLayoutDef layout, GRMechSlotCategory category, string prefix, List<GRMechSlotSizeDef> orderedSlotSizes, StringBuilder buffer)
    {
        for (int i = 0; i < orderedSlotSizes.Count; i++)
        {
            GRMechSlotSizeDef slotSize = orderedSlotSizes[i];
            if (slotSize == null)
            {
                continue;
            }

            int count = CountSlotsByCategoryAndSize(layout, category, slotSize);
            if (count <= 0)
            {
                continue;
            }

            string glyph = slotSize.glyph;
            if (glyph.NullOrEmpty())
            {
                continue;
            }

            AppendSlotExpressionPart(buffer, prefix + ":" + glyph, count);
        }
    }

    private static int CountSlotsByCategory(GRMechSectionLayoutDef layout, GRMechSlotCategory category)
    {
        int count = 0;
        foreach (GRMechSlotEntry slot in GRMechLayoutSlotUtility.EnumerateSlots(layout))
        {
            if (slot?.slotCategory == category)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountSlotsByCategoryAndSize(GRMechSectionLayoutDef layout, GRMechSlotCategory category, GRMechSlotSizeDef slotSize)
    {
        int count = 0;
        foreach (GRMechSlotEntry slot in GRMechLayoutSlotUtility.EnumerateSlots(layout))
        {
            if (slot?.slotCategory == category && slot.slotSize == slotSize)
            {
                count++;
            }
        }

        return count;
    }

    private static int CompareSlotSizeDefs(GRMechSlotSizeDef left, GRMechSlotSizeDef right)
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

        return string.CompareOrdinal(left.defName, right.defName);
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

        return "Draft";
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

        if (CachedModuleCostSummaries.TryGetValue(module, out string cached))
        {
            return cached;
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

        string summary = buffer.ToString();
        CachedModuleCostSummaries[module] = summary;
        return summary;
    }

    internal static string BuildSlotTooltip(GrayMechResolvedSlot resolvedSlot, GRMechModuleDef module)
    {
        string title = GetSlotDisplayName(resolvedSlot.slot);
        string state = module != null ? "Installed: " + module.LabelCap : "Installed: None";
        return title
               + "\n"
               + GetSlotOwnerLabel(resolvedSlot)
               + "\n"
               + BuildSlotTypeSummary(resolvedSlot.slot)
               + "\n"
               + state;
    }

    internal static string BuildSlotTypeSummary(GRMechSlotEntry slot)
    {
        if (slot?.slotDef?.requiredComponentSet != null)
        {
            return slot.slotDef.requiredComponentSet.label ?? slot.slotDef.requiredComponentSet.defName;
        }

        if (slot == null)
        {
            return "?";
        }

        if (slot.slotSize == null)
        {
            return GetComponentTypeLabel(slot.componentType);
        }

        GRMechSlotSizeDef slotSize = slot.slotSize;
        string size = slotSize.LabelCap.ToString();
        if (size.NullOrEmpty())
        {
            size = slotSize.glyph.NullOrEmpty() ? "?" : slotSize.glyph;
        }

        if (slot.componentType == GRMechSlotComponentType.Auxiliary)
        {
            return size;
        }

        string type = GetComponentTypeLabel(slot.componentType);
        return size + " / " + type;
    }

    private static string GetComponentTypeLabel(GRMechSlotComponentType componentType)
    {
        return componentType switch
        {
            GRMechSlotComponentType.Weapon => "weapon slot",
            GRMechSlotComponentType.StrikeCraft => "strike craft bay",
            GRMechSlotComponentType.Utility => "utility slot",
            GRMechSlotComponentType.Auxiliary => "auxiliary slot",
            _ => "?"
        };
    }

    internal static string GetSlotDisplayName(GRMechSlotEntry slot)
    {
        if (slot == null)
        {
            return "Unknown Slot";
        }

        return slot.label.NullOrEmpty() ? slot.key : slot.label;
    }

    internal static string GetSlotOwnerLabel(GrayMechResolvedSlot resolvedSlot)
    {
        if (resolvedSlot?.sectionSlot != null)
        {
            return resolvedSlot.sectionSlot.LabelCap;
        }

        return "Core Systems";
    }

    internal static Color GetSectionAccentColor(GRMechSectionSlotDef sectionSlot)
    {
        if (sectionSlot == null)
        {
            return GrayMechDrydockTabStyle.UtilitySlotColor;
        }

        if (GRMechSectionSlotUtility.IsBow(sectionSlot))
        {
            return GrayMechDrydockTabStyle.MainWeaponColor;
        }

        if (GRMechSectionSlotUtility.IsStern(sectionSlot))
        {
            return GrayMechDrydockTabStyle.EngineColor;
        }

        return GrayMechDrydockTabStyle.AuxiliaryColor;
    }

    internal static int CompareChassisDefs(GRMechChassisDef left, GRMechChassisDef right)
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
