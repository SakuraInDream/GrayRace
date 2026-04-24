using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.ITabs;

internal sealed partial class GrayMechDrydockFocusPanel
{
    private void DrawSummaryPanel(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFillMuted, GrayMechDrydockTabStyle.HullOutline);
        GrayMechDrydockPresenter state = context.State;
        Rect scrollRect = rect.ContractedBy(8f);
        float contentHeight = state.GetSummaryPanelHeight(scrollRect.width);
        bool needsScroll = contentHeight > scrollRect.height + 0.01f;
        float viewWidth = needsScroll ? scrollRect.width - 16f : scrollRect.width;
        if (needsScroll)
        {
            contentHeight = Mathf.Max(scrollRect.height, state.GetSummaryPanelHeight(viewWidth));
        }
        else
        {
            contentHeight = scrollRect.height;
            state.SummaryScrollPosition = Vector2.zero;
        }

        Rect viewRect = new Rect(0f, 0f, viewWidth, contentHeight);
        Widgets.BeginScrollView(scrollRect, ref state.SummaryScrollPosition, viewRect);
        DrawSummaryContent(context, viewRect);
        Widgets.EndScrollView();
    }

    private void DrawSummaryContent(GrayMechDrydockTabContext context, Rect rect)
    {
        Rect headerRect = new Rect(rect.x, rect.y, rect.width, rect.height);

        string queueStatus = context.State.CachedQueueStatusLabel;
        Color queueColor = context.State.CachedQueueStatusColor;
        float statusWidth = Mathf.Min(GrayMechDrydockTabStyle.SummaryStatusMaxWidth, headerRect.width * 0.34f);
        float statusHeight = GrayMechDrydockTabText.GetPillHeight(queueStatus, statusWidth);
        Rect statusRect = new Rect(headerRect.xMax - statusWidth, headerRect.y, statusWidth, statusHeight);
        GrayMechDrydockPanelWidgets.DrawPill(statusRect, queueStatus, queueColor);

        Rect thumbRect = new Rect(headerRect.x, headerRect.y, GrayMechDrydockTabStyle.SummaryThumbWidth, GrayMechDrydockTabStyle.SummaryThumbHeight);
        DrawChassisThumb(thumbRect, context.Draft?.chassis);

        float headerHeight = Mathf.Max(thumbRect.height, statusRect.height);
        float dividerY = rect.y + 8f + headerHeight + 8f;
        Widgets.DrawLineHorizontal(rect.x + 4f, dividerY, rect.width - 8f);

        Rect statsRect = new Rect(rect.x, dividerY + 8f, rect.width, rect.yMax - dividerY - 8f);
        float y = statsRect.y;

        y = DrawSummarySectionHeader(new Rect(statsRect.x, y, statsRect.width, 22f), "制造", GrayMechDrydockTabStyle.AuxiliaryColor);
        y += 4f;
        y = DrawSummaryInfoRow(statsRect, y, "Build Time", context.State.CachedBuildTimeLabel, GrayMechDrydockTabStyle.EngineColor);
        y = DrawSummaryInfoRow(statsRect, y, "队列", context.State.CachedQueueCountLabel, GrayMechDrydockTabStyle.UtilitySlotColor);
        y = DrawSummaryInfoRow(statsRect, y, "模块使用", context.State.CachedModuleUsageLabel, GrayMechDrydockTabStyle.AuxiliaryColor);
        y = DrawSummaryInfoRow(statsRect, y, "电力消耗", context.State.CachedPowerSummary, context.State.CachedPowerBudgetColor);
        y = DrawSummaryInfoRow(statsRect, y, "材料需求", context.State.CachedCostRowSummary, GrayMechDrydockTabStyle.SelectedColor, allowWrap: true);
        if (context.Draft?.chassis != null
            && !context.Dock.CanBuildChassis(context.Draft.chassis, out string restrictionReason)
            && !restrictionReason.NullOrEmpty())
        {
            y = DrawSummaryInfoRow(statsRect, y, "Bay Access", restrictionReason, GrayMechDrydockTabStyle.LockedColor, allowWrap: true);
        }

        y += 6f;
        y = DrawSummarySectionHeader(new Rect(statsRect.x, y, statsRect.width, 22f), "舰船信息", GrayMechDrydockTabStyle.HullOutline);
        y += 4f;
        y = DrawArmorRows(statsRect, y, context);
        y = DrawSummaryInfoRow(statsRect, y, "ShieldEnergy".Translate(), context.State.CachedShieldSummary, GrayMechDrydockTabStyle.EngineColor);
        y = DrawSummaryInfoRow(statsRect, y, "WalkSpeedProperty".Translate(), context.State.CachedMoveSpeedLabel, GrayMechDrydockTabStyle.EngineColor);
        y = DrawSummaryInfoRow(statsRect, y, "Bandwidth".Translate(), context.State.CachedBandwidthCostLabel, new Color(0.74f, 0.52f, 0.95f));
        float designManagementHeight = context.State.GetDesignManagementSectionHeight(statsRect.width);
        float designManagementY = Mathf.Max(y + 8f, rect.yMax - designManagementHeight - 10f);
        DrawDesignManagementSection(context, new Rect(statsRect.x, designManagementY, statsRect.width, designManagementHeight));
    }

    private void DrawQueuePanel(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFillMuted, GrayMechDrydockTabStyle.EngineColor);
        GrayMechDrydockPresenter state = context.State;
        Rect scrollRect = rect.ContractedBy(8f);
        float rowHeight = 84f;
        int entryCount = context.Dock.TotalQueuedOrderCount;
        float contentHeight = 34f + (entryCount > 0 ? entryCount * (rowHeight + 8f) : 92f);
        bool needsScroll = contentHeight > scrollRect.height + 0.01f;
        float viewWidth = needsScroll ? scrollRect.width - 16f : scrollRect.width;
        if (!needsScroll)
        {
            contentHeight = scrollRect.height;
            state.QueueScrollPosition = Vector2.zero;
        }
        else
        {
            contentHeight = Mathf.Max(scrollRect.height, contentHeight);
        }

        Rect viewRect = new Rect(0f, 0f, viewWidth, contentHeight);
        Widgets.BeginScrollView(scrollRect, ref state.QueueScrollPosition, viewRect);
        DrawQueueContent(context, viewRect, rowHeight);
        Widgets.EndScrollView();
    }

    private void DrawQueueContent(GrayMechDrydockTabContext context, Rect rect, float rowHeight)
    {
        float y = rect.y;
        y = DrawSummarySectionHeader(new Rect(rect.x, y, rect.width, 22f), "建造队列", GrayMechDrydockTabStyle.EngineColor);
        y += 4f;

        if (!context.Dock.HasActiveOrder && context.Dock.QueuedOrderCount <= 0)
        {
            Rect emptyRect = new Rect(rect.x, y + 8f, rect.width, 76f);
            Widgets.DrawBoxSolidWithOutline(emptyRect, GrayMechDrydockTabStyle.CardFill, new Color(GrayMechDrydockTabStyle.EngineColor.r, GrayMechDrydockTabStyle.EngineColor.g, GrayMechDrydockTabStyle.EngineColor.b, 0.3f));
            GrayMechDrydockTabText.DrawWrappedLabelCentered(new Rect(emptyRect.x + 12f, emptyRect.y + 14f, emptyRect.width - 24f, emptyRect.height - 28f), "<b>Queue Empty</b>\nAdd a design to start assembly.");
            return;
        }

        if (context.Dock.CurrentOrder != null)
        {
            Rect rowRect = new Rect(rect.x, y, rect.width, rowHeight);
            DrawQueueRow(context, rowRect, context.Dock.CurrentOrder, "当前", GrayMechDrydockTabStyle.EngineColor, context.Dock.CurrentOrderTicksRemaining.ToStringTicksToPeriod(), active: true, queueIndex: -1);
            y += rowHeight + 8f;
        }

        for (int i = 0; i < context.Dock.QueuedOrderCount; i++)
        {
            GrayMechAssemblyOrder order = context.Dock.GetQueuedOrder(i);
            if (order == null)
            {
                continue;
            }

            Rect rowRect = new Rect(rect.x, y, rect.width, rowHeight);
            if (DrawQueueRow(context, rowRect, order, "#" + (i + 1), GrayMechDrydockTabStyle.UtilitySlotColor, order.totalTicks.ToStringTicksToPeriod(), active: false, queueIndex: i))
            {
                return;
            }

            y += rowHeight + 8f;
        }
    }

    private bool DrawQueueRow(GrayMechDrydockTabContext context, Rect rect, GrayMechAssemblyOrder order, string badgeText, Color badgeColor, string timeText, bool active, int queueIndex)
    {
        bool canCancel = active || queueIndex >= 0;
        bool hovered = canCancel && Mouse.IsOver(rect);
        Color fill = hovered
            ? new Color(badgeColor.r, badgeColor.g, badgeColor.b, 0.12f)
            : GrayMechDrydockTabStyle.CardFill;
        Color outline = hovered ? Color.white : badgeColor;
        Widgets.DrawBoxSolidWithOutline(rect, fill, outline, active || hovered ? 2 : 1);
        Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 4f), new Color(badgeColor.r, badgeColor.g, badgeColor.b, 0.9f));

        if (canCancel)
        {
            TooltipHandler.TipRegion(rect, active ? "单击取消当前建造" : "单击取消该建造队列项");
            if (Widgets.ButtonInvisible(rect))
            {
                if (active)
                {
                    context.Controller.CancelCurrentOrder(context.Dock);
                }
                else
                {
                    context.Controller.CancelQueuedOrder(context.Dock, queueIndex);
                }

                return true;
            }
        }

        GRMechChassisDef chassis = order?.designSnapshot?.chassis;
        Rect previewRect = new Rect(rect.x + 8f, rect.y + 10f, 48f, 48f);
        DrawChassisPreview(previewRect, chassis);

        float badgeWidth = 62f;
        float contentX = previewRect.xMax + 10f;
        float contentWidth = rect.xMax - 8f - contentX;
        Rect badgeRect = new Rect(contentX, rect.y + 8f, badgeWidth, 22f);
        GrayMechDrydockPanelWidgets.DrawPill(badgeRect, badgeText, badgeColor);

        string timeLabel = active ? "预计: " + timeText : "建造时间: " + timeText;
        Rect timeRect = new Rect(badgeRect.xMax + 8f, rect.y + 9f, rect.xMax - 8f - (badgeRect.xMax + 8f), 18f);
        string title = "<b>" + (order?.Label ?? "Unnamed Order") + "</b>";
        string chassisText = "Chassis: " + (chassis?.LabelCap.ToString() ?? "无");
        float titleHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(title, contentWidth, GameFont.Small);
        float chassisWidth = contentWidth;
        float cancelHintWidth = 0f;
        if (canCancel)
        {
            cancelHintWidth = Mathf.Min(88f, contentWidth * 0.34f);
            chassisWidth -= cancelHintWidth + 8f;
        }

        float chassisHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(chassisText, Mathf.Max(1f, chassisWidth), GameFont.Tiny);
        float y = badgeRect.yMax + 6f;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(contentX, y, contentWidth, titleHeight), title);
        y += titleHeight + 3f;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(contentX, y, Mathf.Max(1f, chassisWidth), chassisHeight), chassisText);

        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        Color oldColor = GUI.color;
        bool oldWrap = Text.WordWrap;
        Text.Font = GameFont.Tiny;
        Text.WordWrap = false;
        Text.Anchor = TextAnchor.UpperRight;
        GUI.color = new Color(0.82f, 0.9f, 0.94f, 0.92f);
        Widgets.Label(timeRect, timeLabel.Truncate(timeRect.width));
        if (canCancel)
        {
            GUI.color = hovered ? GrayMechDrydockTabStyle.SelectedColor : GrayMechDrydockTabStyle.UtilitySlotColor;
            Rect cancelRect = new Rect(rect.xMax - 8f - cancelHintWidth, y, cancelHintWidth, chassisHeight);
            Widgets.Label(cancelRect, active ? "取消建造" : "单击取消");
        }

        GUI.color = oldColor;
        Text.Anchor = oldAnchor;
        Text.Font = oldFont;
        Text.WordWrap = oldWrap;
        return false;
    }

    private void DrawChassisPreview(Rect rect, GRMechChassisDef chassis)
    {
        Widgets.DrawBoxSolid(rect, GrayMechDrydockTabStyle.BgDark);
        Texture2D preview = GrayMechDrydockTabStyle.GetChassisPreview(chassis);
        if (preview != null)
        {
            Widgets.DrawTextureFitted(rect, preview, 1f);
        }
        else if (chassis?.ProducedRace != null)
        {
            Widgets.ThingIcon(rect, chassis.ProducedRace);
        }
    }

    private static float DrawSummarySectionHeader(Rect rect, string label, Color color)
    {
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), new Color(color.r, color.g, color.b, 0.3f));
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        GUI.color = color;
        Text.Font = GameFont.Small;
        Widgets.Label(rect, "<b>" + label + "</b>");
        GUI.color = oldColor;
        Text.Font = oldFont;
        return rect.yMax;
    }

    private static float DrawSummaryInfoRow(Rect areaRect, float y, string label, string value, Color valueColor, bool allowWrap = false)
    {
        const float rowHeight = 26f;
        const float valueGap = 8f;
        float labelWidth = Mathf.Min(120f, areaRect.width * 0.38f);
        float valueWidth = areaRect.width - labelWidth - valueGap;
        float actualHeight = rowHeight;
        if (allowWrap)
        {
            actualHeight = Mathf.Max(rowHeight, GrayMechDrydockTabText.MeasureWrappedTextHeight(value, valueWidth - 12f, GameFont.Tiny) + 8f);
        }

        Rect labelRect = new Rect(areaRect.x, y, labelWidth, actualHeight);
        Rect valueRect = new Rect(labelRect.xMax + valueGap, y, valueWidth, actualHeight);

        Color labelBg = new Color(0.05f, 0.08f, 0.09f, 0.95f);
        Color valueBg = new Color(0.05f, 0.08f, 0.09f, 0.98f);
        Widgets.DrawBoxSolid(labelRect, labelBg);
        Widgets.DrawBoxSolidWithOutline(labelRect, Color.clear, new Color(GrayMechDrydockTabStyle.HullOutline.r, GrayMechDrydockTabStyle.HullOutline.g, GrayMechDrydockTabStyle.HullOutline.b, 0.18f));
        Widgets.DrawBoxSolid(valueRect, valueBg);
        Widgets.DrawBoxSolidWithOutline(valueRect, Color.clear, new Color(valueColor.r, valueColor.g, valueColor.b, 0.4f));

        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        Color oldColor = GUI.color;

        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = new Color(0.88f, 0.94f, 0.92f);
        Widgets.Label(new Rect(labelRect.x + 6f, labelRect.y, labelRect.width - 12f, labelRect.height), label);

        GUI.color = valueColor;
        if (allowWrap)
        {
            Text.Anchor = TextAnchor.UpperRight;
            GrayMechDrydockTabText.DrawWrappedLabel(new Rect(valueRect.x + 6f, valueRect.y + 4f, valueRect.width - 12f, valueRect.height - 8f), "<b>" + value + "</b>");
        }
        else
        {
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(valueRect.x + 6f, valueRect.y, valueRect.width - 12f, valueRect.height), "<b>" + value + "</b>");
        }

        GUI.color = oldColor;
        Text.Anchor = oldAnchor;
        Text.Font = oldFont;
        return y + actualHeight + 4f;
    }

    private static float DrawArmorRows(Rect areaRect, float y, GrayMechDrydockTabContext context)
    {
        if (ModsConfig.IsActive(CombatExtendedPackageId))
        {
            return DrawCombatExtendedArmorRows(areaRect, y, context);
        }

        return DrawVanillaArmorRows(areaRect, y, context);
    }

    private static float DrawVanillaArmorRows(Rect areaRect, float y, GrayMechDrydockTabContext context)
    {
        GrayMechDrydockPresenter state = context.State;
        y = DrawSummaryInfoRow(areaRect, y, "ArmorSharp".Translate(), state.CachedArmorSharpLabel, GrayMechDrydockTabStyle.MainWeaponColor);
        y = DrawSummaryInfoRow(areaRect, y, "ArmorBlunt".Translate(), state.CachedArmorBluntLabel, GrayMechDrydockTabStyle.MainWeaponColor);
        y = DrawSummaryInfoRow(areaRect, y, "ArmorHeat".Translate(), state.CachedArmorHeatLabel, GrayMechDrydockTabStyle.MainWeaponColor);
        return y;
    }

    private static float DrawCombatExtendedArmorRows(Rect areaRect, float y, GrayMechDrydockTabContext context)
    {
        // 为 CE 预留的
        return DrawVanillaArmorRows(areaRect, y, context);
    }

}
