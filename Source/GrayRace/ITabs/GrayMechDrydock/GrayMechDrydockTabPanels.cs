using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.ITabs;

internal sealed class GrayMechDrydockTopBarPanel
{
    internal void Draw(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Widgets.DrawBoxSolid(rect.ContractedBy(1f), GrayMechDrydockTabStyle.BgPanel);

        Rect inner = rect.ContractedBy(12f);
        Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 5f), GrayMechDrydockTabStyle.HeaderLineColor);
        Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.yMax - 6f, rect.width - 2f, 2f), new Color(GrayMechDrydockTabStyle.HeaderLineColor.r, GrayMechDrydockTabStyle.HeaderLineColor.g, GrayMechDrydockTabStyle.HeaderLineColor.b, 0.22f));

        string draftName = context.Draft?.designLabel ?? "No design";
        string chassisName = context.Draft?.chassis?.LabelCap.ToString() ?? "No chassis";
        string titleText = "<b>Ship Designer</b>    " + draftName;
        string subText = "Chassis: " + chassisName + "    Source: " + GrayMechDrydockTabText.GetSourceLabel(context.Dock);

        float textWidth = rect.width - 560f;
        float titleHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(titleText, textWidth, GameFont.Small);
        float subHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(subText, textWidth, GameFont.Small);

        Rect titleRect = new Rect(inner.x, inner.y + 2f, textWidth, titleHeight);
        Rect subRect = new Rect(inner.x, titleRect.yMax + 2f, textWidth, subHeight);
        GrayMechDrydockTabText.DrawWrappedLabel(titleRect, titleText);
        GrayMechDrydockTabText.DrawWrappedLabel(subRect, subText);

        bool ready = context.Dock.CanQueueAssemblyBill(out string reason);
        string statusText = ready ? "Ready for assembly" : reason;
        Color statusColor = ready ? GrayMechDrydockTabStyle.ReadyColor : GrayMechDrydockTabStyle.LockedColor;
        Rect pillRect = new Rect(inner.x, subRect.yMax + 7f, Mathf.Min(240f, inner.width * 0.24f), 24f);
        GrayMechDrydockPanelWidgets.DrawPill(pillRect, statusText, statusColor);

        const float smallButtonWidth = 82f;
        const float primaryButtonWidth = 106f;
        float buttonY = inner.y + inner.height - 30f;
        float x = rect.xMax - 12f - primaryButtonWidth;

        if (GrayMechDrydockPanelWidgets.DrawButton(new Rect(x, buttonY, primaryButtonWidth, 26f), "Queue Bill", true, true))
        {
            context.Controller.QueueAssemblyBill(context.Dock);
        }

        x -= smallButtonWidth + 8f;
        if (GrayMechDrydockPanelWidgets.DrawButton(new Rect(x, buttonY, smallButtonWidth, 26f), "Save", context.Dock.IsEditingSavedDesign))
        {
            context.Controller.SaveCurrentDesign(context.Dock);
        }

        x -= smallButtonWidth + 8f;
        if (GrayMechDrydockPanelWidgets.DrawButton(new Rect(x, buttonY, smallButtonWidth, 26f), "Save As"))
        {
            context.Controller.SaveAsNewDesign(context.Dock);
        }

        x -= smallButtonWidth + 8f;
        if (GrayMechDrydockPanelWidgets.DrawButton(new Rect(x, buttonY, smallButtonWidth, 26f), "Rename", context.Dock.IsEditingSavedDesign))
        {
            context.Controller.RenameCurrentDesign(context.Dock);
        }

        x -= smallButtonWidth + 8f;
        if (GrayMechDrydockPanelWidgets.DrawButton(new Rect(x, buttonY, smallButtonWidth, 26f), "Delete", context.Dock.IsEditingSavedDesign))
        {
            context.Controller.DeleteCurrentDesign(context.Dock);
        }
    }
}

internal sealed class GrayMechDrydockDesignerPanel
{
    private readonly GrayMechDrydockCanvasRenderer canvasRenderer = new();
    private readonly List<TabRecord> sectionTabs = new();
    private readonly List<GRMechSectionRoleDef> sectionTabRoles = new();
    private GRMechSectionRoleDef queuedTabSelection;

    internal void Draw(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Widgets.DrawBoxSolid(rect.ContractedBy(1f), GrayMechDrydockTabStyle.BgPanelAlt);
        Rect inner = rect.ContractedBy(8f);

        float tabHeight = context.State.GetDesignerStripHeight(context.Draft, inner.width);
        const float topSlotHeight = 74f;
        const float bottomSlotHeight = 108f;
        float previewHeight = Mathf.Max(180f, inner.height - tabHeight - topSlotHeight - bottomSlotHeight - 16f);

        Rect tabsRect = new Rect(inner.x, inner.y, inner.width, tabHeight);
        Rect topSlotsRect = new Rect(inner.x, tabsRect.yMax + 6f, inner.width, topSlotHeight);
        Rect canvasRect = new Rect(inner.x, topSlotsRect.yMax + 6f, inner.width, previewHeight);
        Rect bottomSlotsRect = new Rect(inner.x, canvasRect.yMax + 6f, inner.width, inner.yMax - canvasRect.yMax - 6f);

        DrawSectionTabs(context, tabsRect);
        DrawShipCanvas(context, topSlotsRect, canvasRect, bottomSlotsRect);
    }

    private void DrawSectionTabs(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFillMuted, GrayMechDrydockTabStyle.HullOutline);
        EnsureSectionTabs(context);
        if (sectionTabs.Count == 0)
        {
            return;
        }

        Rect tabBaseRect = new Rect(rect.x + 14f, rect.y + 32f, rect.width - 28f, 32f);
        TabDrawer.DrawTabs(tabBaseRect, sectionTabs, 220f);
        if (queuedTabSelection != null)
        {
            context.Controller.SelectSection(queuedTabSelection);
            queuedTabSelection = null;
        }
    }

    private void EnsureSectionTabs(GrayMechDrydockTabContext context)
    {
        List<GRMechChassisSectionDef> sections = context.Draft?.chassis?.sections;
        if (sections == null || sections.Count == 0)
        {
            sectionTabs.Clear();
            sectionTabRoles.Clear();
            queuedTabSelection = null;
            return;
        }

        bool rebuild = sectionTabRoles.Count != sections.Count;
        if (!rebuild)
        {
            for (int i = 0; i < sections.Count; i++)
            {
                if (sectionTabRoles[i] != sections[i]?.role)
                {
                    rebuild = true;
                    break;
                }
            }
        }

        if (rebuild)
        {
            sectionTabs.Clear();
            sectionTabRoles.Clear();
            for (int i = 0; i < sections.Count; i++)
            {
                GRMechSectionRoleDef role = sections[i]?.role;
                if (role == null)
                {
                    continue;
                }

                GRMechSectionRoleDef capturedRole = role;
                sectionTabRoles.Add(capturedRole);
                sectionTabs.Add(new TabRecord(string.Empty, delegate
                {
                    queuedTabSelection = capturedRole;
                }, false));
            }
        }

        for (int i = 0; i < sectionTabRoles.Count; i++)
        {
            GRMechSectionRoleDef role = sectionTabRoles[i];
            GrayMechDesignUtility.TryGetSelectedLayout(context.Draft, role, out GRMechSectionLayoutDef layout);
            bool selected = context.State.SelectedSectionRole == role && context.State.SelectedSlotKey.NullOrEmpty();
            sectionTabs[i].label = layout?.LabelCap.ToString() ?? role.LabelCap.ToString();
            sectionTabs[i].selected = selected;
            sectionTabs[i].labelColor = selected ? Color.white : GrayMechDrydockTabText.GetSectionAccentColor(role);
        }
    }

    private void DrawShipCanvas(GrayMechDrydockTabContext context, Rect topSlotsRect, Rect canvasRect, Rect bottomSlotsRect)
    {
        canvasRenderer.DrawSlotBay(topSlotsRect);
        canvasRenderer.DrawCanvasFrame(canvasRect);
        canvasRenderer.DrawNebulaBackdrop(canvasRect);
        canvasRenderer.DrawGrid(canvasRect);
        canvasRenderer.DrawSlotBay(bottomSlotsRect);

        Rect previewRect = canvasRenderer.GetPreviewRect(canvasRect);
        canvasRenderer.DrawShipPreview(previewRect, context.Draft);

        List<GRMechChassisSectionDef> sections = context.Draft?.chassis?.sections;
        if (sections == null)
        {
            return;
        }

        for (int i = 0; i < sections.Count; i++)
        {
            GRMechChassisSectionDef section = sections[i];
            if (section?.role == null)
            {
                continue;
            }

            Rect sectionRect = canvasRenderer.GetSectionVisualRect(context.Draft, section.role, previewRect);
            bool selected = context.State.SelectedSectionRole == section.role && context.State.SelectedSlotKey.NullOrEmpty();
            bool ownsSelectedSlot = context.State.SectionOwnsSelectedSlot(section.role);
            canvasRenderer.DrawSectionOverlay(section.role, sectionRect, selected, ownsSelectedSlot);
            if (Widgets.ButtonInvisible(sectionRect))
            {
                context.Controller.SelectSection(section.role);
            }

            GrayMechDesignUtility.TryGetSelectedLayout(context.Draft, section.role, out GRMechSectionLayoutDef layout);
            canvasRenderer.DrawSectionCaption(section.role, sectionRect, layout, context.State.TextBuilder);
            DrawSlotsForSection(context, section.role, sectionRect, topSlotsRect, bottomSlotsRect);
        }
    }

    private void DrawSlotsForSection(GrayMechDrydockTabContext context, GRMechSectionRoleDef role, Rect sectionRect, Rect topSlotsRect, Rect bottomSlotsRect)
    {
        GrayMechDrydockTabState state = context.State;
        state.PrepareSectionSlotBuffers(role);
        Color accent = GrayMechDrydockTabText.GetSectionAccentColor(role);

        if (state.WeaponSlotBuffer.Count > 0)
        {
            Rect weaponRowRect = GetSlotRowRect(topSlotsRect, sectionRect.center.x, state.WeaponSlotBuffer.Count);
            canvasRenderer.DrawSectionConnector(sectionRect, weaponRowRect, true, accent);
            DrawSlotRow(context, weaponRowRect, "W", state.WeaponSlotBuffer, accent);
        }

        float rowY = bottomSlotsRect.y + 10f;
        if (state.SmallUtilitySlotBuffer.Count > 0)
        {
            Rect slotRow = GetSlotRowRect(bottomSlotsRect, sectionRect.center.x, state.SmallUtilitySlotBuffer.Count, rowY);
            canvasRenderer.DrawSectionConnector(sectionRect, slotRow, false, accent);
            DrawSlotRow(context, slotRow, "U-S", state.SmallUtilitySlotBuffer, GrayMechDrydockTabStyle.AuxiliaryColor);
            rowY += GrayMechDrydockTabStyle.SlotButtonSize + 10f;
        }

        if (state.MediumUtilitySlotBuffer.Count > 0)
        {
            Rect slotRow = GetSlotRowRect(bottomSlotsRect, sectionRect.center.x, state.MediumUtilitySlotBuffer.Count, rowY);
            canvasRenderer.DrawSectionConnector(sectionRect, slotRow, false, accent);
            DrawSlotRow(context, slotRow, "U-M", state.MediumUtilitySlotBuffer, GrayMechDrydockTabStyle.AuxiliaryColor);
            rowY += GrayMechDrydockTabStyle.SlotButtonSize + 10f;
        }

        if (state.LargeUtilitySlotBuffer.Count > 0)
        {
            Rect slotRow = GetSlotRowRect(bottomSlotsRect, sectionRect.center.x, state.LargeUtilitySlotBuffer.Count, rowY);
            canvasRenderer.DrawSectionConnector(sectionRect, slotRow, false, accent);
            DrawSlotRow(context, slotRow, "U-L", state.LargeUtilitySlotBuffer, GrayMechDrydockTabStyle.AuxiliaryColor);
            rowY += GrayMechDrydockTabStyle.SlotButtonSize + 10f;
        }

        if (state.AuxSlotBuffer.Count > 0)
        {
            Rect slotRow = GetSlotRowRect(bottomSlotsRect, sectionRect.center.x, state.AuxSlotBuffer.Count, rowY);
            canvasRenderer.DrawSectionConnector(sectionRect, slotRow, false, accent);
            DrawSlotRow(context, slotRow, "A", state.AuxSlotBuffer, GrayMechDrydockTabStyle.EngineColor);
        }
    }

    private Rect GetSlotRowRect(Rect areaRect, float centerX, int slotCount, float? yOverride = null)
    {
        float totalWidth = slotCount * GrayMechDrydockTabStyle.SlotButtonSize + Mathf.Max(0, slotCount - 1) * 6f;
        float width = totalWidth + 36f;
        float x = Mathf.Clamp(centerX - width * 0.5f, areaRect.x + 4f, areaRect.xMax - width - 4f);
        float y = yOverride ?? (areaRect.center.y - GrayMechDrydockTabStyle.SlotButtonSize * 0.5f);
        return new Rect(x, y, width, GrayMechDrydockTabStyle.SlotButtonSize);
    }

    private void DrawSlotRow(GrayMechDrydockTabContext context, Rect rowRect, string label, List<GrayMechResolvedSlot> slots, Color accent)
    {
        Rect labelRect = new Rect(rowRect.x, rowRect.y + 7f, 30f, 18f);
        Widgets.DrawBoxSolidWithOutline(labelRect, new Color(0f, 0f, 0f, 0.34f), accent);
        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Tiny;
        Widgets.Label(labelRect, label);
        Text.Anchor = oldAnchor;
        Text.Font = oldFont;

        float x = labelRect.xMax + 6f;
        for (int i = 0; i < slots.Count; i++)
        {
            GrayMechResolvedSlot resolvedSlot = slots[i];
            Rect slotRect = new Rect(x, rowRect.y, GrayMechDrydockTabStyle.SlotButtonSize, GrayMechDrydockTabStyle.SlotButtonSize);
            GrayMechDesignUtility.TryGetSelectedModule(context.Draft, resolvedSlot.slot.key, out GRMechModuleDef module);
            bool selected = context.State.SelectedSlotKey == resolvedSlot.slot.key;
            canvasRenderer.DrawSlotWidget(slotRect, resolvedSlot.slot, module, selected, accent);
            if (Widgets.ButtonInvisible(slotRect))
            {
                context.Controller.SelectSlot(context.Dock, resolvedSlot, openFloatMenu: true);
            }

            TooltipHandler.TipRegion(slotRect, GrayMechDrydockTabText.BuildSlotTooltip(resolvedSlot, module));
            x += GrayMechDrydockTabStyle.SlotButtonSize + 6f;
        }
    }
}

internal sealed class GrayMechDrydockFocusPanel
{
    private readonly GrayMechDrydockCanvasRenderer canvasRenderer = new();

    internal void Draw(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Widgets.DrawBoxSolid(rect.ContractedBy(1f), GrayMechDrydockTabStyle.BgPanel);
        Rect inner = rect.ContractedBy(8f);

        Rect summaryRect = new Rect(inner.x, inner.y, inner.width, GrayMechDrydockTabStyle.SummaryPanelHeight);
        DrawSummaryPanel(context, summaryRect);

        Rect contentRect = new Rect(inner.x, summaryRect.yMax + 10f, inner.width, inner.yMax - summaryRect.yMax - 10f);
        if (context.State.TryGetResolvedSlot(context.State.SelectedSlotKey, out GrayMechResolvedSlot resolvedSlot))
        {
            DrawSlotFocusPanel(context, resolvedSlot, contentRect);
            return;
        }

        DrawSectionFocusPanel(context, contentRect);
    }

    private void DrawSummaryPanel(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFillMuted, GrayMechDrydockTabStyle.HullOutline);

        bool ready = context.Dock.CanQueueAssemblyBill(out string reason);
        string queueStatus = ready ? "Ready for assembly" : reason;
        Color queueColor = ready ? GrayMechDrydockTabStyle.ReadyColor : GrayMechDrydockTabStyle.LockedColor;
        Rect statusRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 22f);
        GrayMechDrydockPanelWidgets.DrawPill(statusRect, queueStatus, queueColor);

        Rect thumbRect = new Rect(rect.x + 8f, statusRect.yMax + 10f, 78f, 56f);
        DrawChassisThumb(thumbRect, context.Draft?.chassis);

        float textWidth = rect.width - thumbRect.width - 22f;
        Rect nameRect = new Rect(thumbRect.xMax + 8f, statusRect.yMax + 8f, textWidth, 20f);
        Rect chassisRect = new Rect(nameRect.x, nameRect.yMax + 2f, nameRect.width, 18f);
        Rect sourceRect = new Rect(nameRect.x, chassisRect.yMax + 2f, nameRect.width, 18f);
        Widgets.Label(nameRect, "<b>" + (context.Draft?.designLabel ?? "No design") + "</b>");
        Widgets.Label(chassisRect, "Chassis: " + (context.Draft?.chassis?.LabelCap.ToString() ?? "None"));
        Widgets.Label(sourceRect, GrayMechDrydockTabText.GetSourceLabel(context.Dock));

        Rect statsRect = new Rect(rect.x + 8f, rect.yMax - 50f, rect.width - 16f, 40f);
        Rect leftRect = new Rect(statsRect.x, statsRect.y, statsRect.width * 0.48f, statsRect.height);
        Rect rightRect = new Rect(leftRect.xMax + 8f, statsRect.y, statsRect.xMax - leftRect.xMax - 8f, statsRect.height);
        string leftText = "Slots " + context.State.CachedFilledSlotCount + "/" + context.State.CachedTotalSlotCount
                          + "\nWork " + context.State.CachedFixedWorkTicks.ToStringTicksToPeriod()
                          + "   Speed " + context.State.CachedMoveSpeed.ToString("0.#");
        string rightText = "Armor " + context.State.CachedArmorSharp.ToString("0.##")
                           + "   BW " + context.State.CachedBandwidthCost.ToString("0.#")
                           + "\nCost " + (context.State.CachedCostSummary.NullOrEmpty() ? "None" : context.State.CachedCostSummary);
        GrayMechDrydockTabText.DrawWrappedLabel(leftRect, leftText);
        GrayMechDrydockTabText.DrawWrappedLabel(rightRect, rightText);
    }

    private void DrawSectionFocusPanel(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFill, GrayMechDrydockTabStyle.HullOutline);
        Rect inner = rect.ContractedBy(8f);

        GRMechSectionRoleDef role = context.State.SelectedSectionRole;
        if (role == null || !context.Draft.chassis.TryGetSection(role, out GRMechChassisSectionDef section))
        {
            Widgets.Label(inner, "Select a ship section to edit its layout.");
            return;
        }

        GrayMechDesignUtility.TryGetSelectedLayout(context.Draft, role, out GRMechSectionLayoutDef currentLayout);
        string titleText = "<b>" + role.LabelCap + "</b>";
        string bodyText = "Choose the active structure package for this section.";
        float titleHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(titleText, inner.width, GameFont.Small);
        float bodyHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(bodyText, inner.width, GameFont.Small);
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(inner.x, inner.y, inner.width, titleHeight), titleText);
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(inner.x, inner.y + titleHeight + 2f, inner.width, bodyHeight), bodyText);

        float listY = inner.y + titleHeight + bodyHeight + 10f;
        Rect listRect = new Rect(inner.x, listY, inner.width, inner.yMax - listY);
        float viewHeight = 0f;
        for (int i = 0; i < section.layouts.Count; i++)
        {
            GRMechSectionLayoutDef layout = section.layouts[i];
            if (layout != null)
            {
                viewHeight += context.State.GetLayoutOptionHeight(layout, listRect.width - 16f) + 8f;
            }
        }

        GrayMechDrydockTabState state = context.State;
        Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(listRect.height, viewHeight));
        Widgets.BeginScrollView(listRect, ref state.FocusScrollPosition, viewRect);

        float y = 0f;
        Color accent = GrayMechDrydockTabText.GetSectionAccentColor(role);
        for (int i = 0; i < section.layouts.Count; i++)
        {
            GRMechSectionLayoutDef layout = section.layouts[i];
            if (layout == null)
            {
                continue;
            }

            bool selected = currentLayout == layout;
            bool available = GrayMechDesignUtility.IsResearchAvailable(layout);
            float rowHeight = state.GetLayoutOptionHeight(layout, viewRect.width);
            Rect rowRect = new Rect(0f, y, viewRect.width, rowHeight);
            Widgets.DrawBoxSolidWithOutline(
                rowRect,
                selected
                    ? new Color(GrayMechDrydockTabStyle.SelectedColor.r, GrayMechDrydockTabStyle.SelectedColor.g, GrayMechDrydockTabStyle.SelectedColor.b, 0.11f)
                    : new Color(accent.r, accent.g, accent.b, 0.04f),
                selected ? GrayMechDrydockTabStyle.SelectedColor : accent);

            if (available && Widgets.ButtonInvisible(rowRect))
            {
                context.Controller.SetSectionLayout(context.Dock, role, layout);
            }

            Rect iconRect = new Rect(rowRect.x + 6f, rowRect.y + 6f, 36f, 36f);
            canvasRenderer.DrawModuleIconTile(iconRect, null, layout.componentSlots != null && layout.componentSlots.Count > 0 ? layout.componentSlots[0] : null, accent);

            float leftWidth = rowRect.width - 104f;
            float leftX = iconRect.xMax + 8f;
            string layoutTitle = "<b>" + layout.LabelCap + "</b>";
            string description = layout.description ?? string.Empty;
            string slotsText = "Slots: " + GrayMechDrydockTabText.BuildLayoutSlotExpression(layout, state.TextBuilder);
            float titleH = GrayMechDrydockTabText.MeasureWrappedTextHeight(layoutTitle, leftWidth, GameFont.Small);
            float descH = GrayMechDrydockTabText.MeasureWrappedTextHeight(description, leftWidth, GameFont.Small);
            float slotsH = GrayMechDrydockTabText.MeasureWrappedTextHeight(slotsText, leftWidth, GameFont.Small);
            float localY = rowRect.y + 4f;
            GrayMechDrydockTabText.DrawWrappedLabel(new Rect(leftX, localY, leftWidth, titleH), layoutTitle);
            localY += titleH + 2f;
            GrayMechDrydockTabText.DrawWrappedLabel(new Rect(leftX, localY, leftWidth, descH), description);
            localY += descH + 2f;
            GrayMechDrydockTabText.DrawWrappedLabel(new Rect(leftX, localY, leftWidth, slotsH), slotsText);

            string stateText = selected ? "Selected" : (available ? "Unlocked" : "Locked");
            Rect stateRect = new Rect(rowRect.xMax - 60f, rowRect.y + 8f, 54f, 18f);
            GrayMechDrydockPanelWidgets.DrawPill(stateRect, stateText, selected ? GrayMechDrydockTabStyle.SelectedColor : (available ? GrayMechDrydockTabStyle.ReadyColor : GrayMechDrydockTabStyle.LockedColor));
            y += rowHeight + 8f;
        }

        Widgets.EndScrollView();
    }

    private void DrawSlotFocusPanel(GrayMechDrydockTabContext context, GrayMechResolvedSlot resolvedSlot, Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFill, GrayMechDrydockTabStyle.HullOutline);
        Rect inner = rect.ContractedBy(8f);
        Color accent = GrayMechDrydockTabText.GetSectionAccentColor(resolvedSlot.role);

        GrayMechDesignUtility.TryGetSelectedModule(context.Draft, resolvedSlot.slot.key, out GRMechModuleDef currentModule);
        Rect slotIconRect = new Rect(inner.x, inner.y + 2f, 42f, 42f);
        canvasRenderer.DrawModuleIconTile(slotIconRect, currentModule, resolvedSlot.slot, accent);

        string titleText = "<b>" + GrayMechDrydockTabText.GetSlotDisplayName(resolvedSlot.slot) + "</b>";
        string metaText = resolvedSlot.role.LabelCap + "  |  " + GrayMechDrydockTabText.BuildSlotTypeSummary(resolvedSlot.slot);
        string currentText = "Current: " + (currentModule?.LabelCap.ToString() ?? "None");
        float headerX = slotIconRect.xMax + 8f;
        float headerWidth = inner.width - slotIconRect.width - 8f;
        float titleHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(titleText, headerWidth, GameFont.Small);
        float metaHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(metaText, headerWidth, GameFont.Small);
        float currentHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(currentText, headerWidth, GameFont.Small);
        float headerY = inner.y;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(headerX, headerY, headerWidth, titleHeight), titleText);
        headerY += titleHeight + 2f;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(headerX, headerY, headerWidth, metaHeight), metaText);
        headerY += metaHeight + 2f;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(headerX, headerY, headerWidth, currentHeight), currentText);
        headerY = Mathf.Max(slotIconRect.yMax, headerY + currentHeight) + 8f;

        Rect listRect = new Rect(inner.x, headerY, inner.width, inner.yMax - headerY);
        float viewHeight = context.State.GetModuleOptionHeight(null, listRect.width - 16f) + 8f;
        for (int i = 0; i < context.State.CompatibleModules.Count; i++)
        {
            viewHeight += context.State.GetModuleOptionHeight(context.State.CompatibleModules[i], listRect.width - 16f) + 8f;
        }

        GrayMechDrydockTabState state = context.State;
        Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(listRect.height, viewHeight));
        Widgets.BeginScrollView(listRect, ref state.FocusScrollPosition, viewRect);

        float emptyHeight = state.GetModuleOptionHeight(null, viewRect.width);
        DrawModuleOptionRow(context, new Rect(0f, 0f, viewRect.width, emptyHeight), resolvedSlot, null, currentModule == null, accent);

        float y = emptyHeight + 8f;
        for (int i = 0; i < state.CompatibleModules.Count; i++)
        {
            GRMechModuleDef module = state.CompatibleModules[i];
            float rowHeight = state.GetModuleOptionHeight(module, viewRect.width);
            DrawModuleOptionRow(context, new Rect(0f, y, viewRect.width, rowHeight), resolvedSlot, module, currentModule == module, accent);
            y += rowHeight + 8f;
        }

        Widgets.EndScrollView();
    }

    private void DrawModuleOptionRow(GrayMechDrydockTabContext context, Rect rowRect, GrayMechResolvedSlot resolvedSlot, GRMechModuleDef module, bool selected, Color accent)
    {
        bool available = module == null || GrayMechDesignUtility.IsResearchAvailable(module);
        Widgets.DrawBoxSolidWithOutline(
            rowRect,
            selected
                ? new Color(GrayMechDrydockTabStyle.SelectedColor.r, GrayMechDrydockTabStyle.SelectedColor.g, GrayMechDrydockTabStyle.SelectedColor.b, 0.11f)
                : new Color(accent.r, accent.g, accent.b, 0.04f),
            selected ? GrayMechDrydockTabStyle.SelectedColor : accent);

        if (available && Widgets.ButtonInvisible(rowRect))
        {
            context.Controller.SetModule(context.Dock, resolvedSlot.slot.key, module);
        }

        Rect iconRect = new Rect(rowRect.x + 6f, rowRect.y + 6f, 36f, 36f);
        canvasRenderer.DrawModuleIconTile(iconRect, module, resolvedSlot.slot, accent);

        float leftWidth = rowRect.width - 106f;
        float leftX = iconRect.xMax + 8f;
        string title = module?.LabelCap.ToString() ?? "Empty Slot";
        string description = module?.description ?? "Remove the installed module from this slot.";
        string cost = module == null ? "Cost: None" : GrayMechDrydockTabText.BuildModuleCostSummary(module, context.State.TextBuilder);
        string stateText = selected ? "Installed" : (available ? "Available" : "Locked");

        string titleText = "<b>" + title + "</b>";
        float titleH = GrayMechDrydockTabText.MeasureWrappedTextHeight(titleText, leftWidth, GameFont.Small);
        float descH = GrayMechDrydockTabText.MeasureWrappedTextHeight(description, leftWidth, GameFont.Small);
        float costH = GrayMechDrydockTabText.MeasureWrappedTextHeight(cost, leftWidth, GameFont.Small);
        float localY = rowRect.y + 4f;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(leftX, localY, leftWidth, titleH), titleText);
        localY += titleH + 2f;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(leftX, localY, leftWidth, descH), description);
        localY += descH + 2f;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(leftX, localY, leftWidth, costH), cost);

        Rect stateRect = new Rect(rowRect.xMax - 62f, rowRect.y + 8f, 56f, 18f);
        GrayMechDrydockPanelWidgets.DrawPill(stateRect, stateText, selected ? GrayMechDrydockTabStyle.SelectedColor : (available ? GrayMechDrydockTabStyle.ReadyColor : GrayMechDrydockTabStyle.LockedColor));
    }

    private void DrawChassisThumb(Rect rect, GRMechChassisDef chassis)
    {
        Widgets.DrawBoxSolidWithOutline(rect, new Color(GrayMechDrydockTabStyle.HullAccentColor.r, GrayMechDrydockTabStyle.HullAccentColor.g, GrayMechDrydockTabStyle.HullAccentColor.b, 0.08f), GrayMechDrydockTabStyle.HullOutline);
        Rect inner = rect.ContractedBy(4f);
        Widgets.DrawBoxSolid(inner, GrayMechDrydockTabStyle.BgDark);

        Texture2D preview = GrayMechDrydockTabStyle.GetChassisPreview(chassis);
        if (preview != null)
        {
            Widgets.DrawTextureFitted(inner, preview, 1f);
        }
        else if (chassis?.ProducedRace != null)
        {
            Widgets.ThingIcon(inner, chassis.ProducedRace);
        }
    }
}

internal sealed class GrayMechDrydockBottomBarPanel
{
    internal void Draw(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Widgets.DrawBoxSolid(rect.ContractedBy(1f), GrayMechDrydockTabStyle.BgPanelAlt);
        Rect inner = rect.ContractedBy(10f);

        Widgets.Label(new Rect(inner.x, inner.y, inner.width, 20f), "<b>Blueprint Library</b>");
        Rect contentRect = new Rect(inner.x, inner.y + 24f, inner.width, inner.height - 24f);

        Rect actionRect = new Rect(contentRect.x, contentRect.y, GrayMechDrydockTabStyle.LibraryActionWidth, contentRect.height);
        DrawActionTile(context, actionRect);

        Rect listRect = new Rect(actionRect.xMax + 12f, contentRect.y, contentRect.width - actionRect.width - 12f, contentRect.height);
        DrawLibraryCards(context, listRect);
    }

    private void DrawActionTile(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFillMuted, GrayMechDrydockTabStyle.HullOutline);
        Rect inner = rect.ContractedBy(8f);
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(inner.x, inner.y, inner.width, 18f), "<b>Active Draft</b>");

        string draftName = context.Draft?.designLabel ?? "No design";
        float draftHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(draftName, inner.width, GameFont.Small);
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(inner.x, inner.y + 20f, inner.width, draftHeight), draftName);

        string subText = context.Draft?.chassis?.LabelCap.ToString() ?? "No chassis";
        float subHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(subText, inner.width, GameFont.Small);
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(inner.x, inner.y + 24f + draftHeight, inner.width, subHeight), subText);

        float buttonY = rect.yMax - 62f;
        if (GrayMechDrydockPanelWidgets.DrawButton(new Rect(inner.x, buttonY, inner.width, 24f), "Queue Bill", true, true))
        {
            context.Controller.QueueAssemblyBill(context.Dock);
        }

        if (GrayMechDrydockPanelWidgets.DrawButton(new Rect(inner.x, buttonY + 28f, inner.width, 22f), context.Dock.IsEditingSavedDesign ? "Overwrite" : "Save As", true))
        {
            if (context.Dock.IsEditingSavedDesign)
            {
                context.Controller.SaveCurrentDesign(context.Dock);
            }
            else
            {
                context.Controller.SaveAsNewDesign(context.Dock);
            }
        }
    }

    private void DrawLibraryCards(GrayMechDrydockTabContext context, Rect rect)
    {
        GrayMechDrydockTabState state = context.State;
        int cardCount = state.PresetCache.Count + state.DesignCache.Count;
        float contentWidth = 8f + cardCount * (GrayMechDrydockTabStyle.LibraryCardWidth + 8f);
        Rect viewRect = new Rect(0f, 0f, Mathf.Max(rect.width - 16f, contentWidth), rect.height - 16f);

        Widgets.BeginScrollView(rect, ref state.LibraryScrollPosition, viewRect);
        float x = 8f;
        for (int i = 0; i < state.PresetCache.Count; i++)
        {
            GRMechPresetDef preset = state.PresetCache[i];
            Rect cardRect = new Rect(x, 4f, GrayMechDrydockTabStyle.LibraryCardWidth, GrayMechDrydockTabStyle.LibraryCardHeight);
            bool selected = !context.Dock.IsEditingSavedDesign && context.Dock.SourcePresetDefName == preset.defName;
            DrawPresetCard(context, cardRect, preset, selected);
            x += GrayMechDrydockTabStyle.LibraryCardWidth + 8f;
        }

        for (int i = 0; i < state.DesignCache.Count; i++)
        {
            GrayMechDesignRecord design = state.DesignCache[i];
            Rect cardRect = new Rect(x, 4f, GrayMechDrydockTabStyle.LibraryCardWidth, GrayMechDrydockTabStyle.LibraryCardHeight);
            bool selected = context.Dock.IsEditingSavedDesign && context.Dock.EditingDesignId == design.id;
            DrawDesignCard(context, cardRect, design, selected);
            x += GrayMechDrydockTabStyle.LibraryCardWidth + 8f;
        }

        Widgets.EndScrollView();
    }

    private void DrawPresetCard(GrayMechDrydockTabContext context, Rect rect, GRMechPresetDef preset, bool selected)
    {
        bool available = GrayMechDesignUtility.IsResearchAvailable(preset?.chassis);
        DrawLibraryCardChrome(rect, selected, available ? GrayMechDrydockTabStyle.AuxiliaryColor : GrayMechDrydockTabStyle.LockedColor);

        if (Widgets.ButtonInvisible(rect))
        {
            context.Controller.LoadPreset(context.Dock, preset);
        }

        Rect previewRect = new Rect(rect.x + 4f, rect.y + 16f, rect.width - 8f, 52f);
        DrawChassisPreview(previewRect, preset?.chassis);
        Widgets.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 12f), "Template");

        string title = preset?.LabelCap.ToString() ?? "Unnamed";
        string chassis = preset?.chassis?.LabelCap.ToString() ?? "No chassis";
        string footer = available ? chassis : chassis + " (Locked)";
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(rect.x + 6f, previewRect.yMax + 4f, rect.width - 12f, 18f), "<b>" + title + "</b>");
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(rect.x + 6f, previewRect.yMax + 24f, rect.width - 12f, 28f), footer);
        TooltipHandler.TipRegion(rect, preset?.description ?? string.Empty);
    }

    private void DrawDesignCard(GrayMechDrydockTabContext context, Rect rect, GrayMechDesignRecord design, bool selected)
    {
        DrawLibraryCardChrome(rect, selected, GrayMechDrydockTabStyle.MainWeaponColor);

        if (Widgets.ButtonInvisible(rect))
        {
            context.Controller.LoadSavedDesign(context.Dock, design);
        }

        GrayMechDesignSnapshot snapshot = design?.snapshot;
        Rect previewRect = new Rect(rect.x + 4f, rect.y + 16f, rect.width - 8f, 52f);
        DrawChassisPreview(previewRect, snapshot?.chassis);
        Widgets.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 12f), "Saved");

        string title = design?.label ?? "Unnamed";
        string chassis = snapshot?.chassis?.LabelCap.ToString() ?? "No chassis";
        string footer = chassis + "   Modules " + CountInstalledModules(snapshot);
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(rect.x + 6f, previewRect.yMax + 4f, rect.width - 12f, 18f), "<b>" + title + "</b>");
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(rect.x + 6f, previewRect.yMax + 24f, rect.width - 12f, 28f), footer);
        TooltipHandler.TipRegion(rect, GrayMechDrydockTabText.BuildDesignTooltip(design));
    }

    private void DrawLibraryCardChrome(Rect rect, bool selected, Color accent)
    {
        Color fill = selected
            ? new Color(GrayMechDrydockTabStyle.SelectedColor.r, GrayMechDrydockTabStyle.SelectedColor.g, GrayMechDrydockTabStyle.SelectedColor.b, 0.12f)
            : GrayMechDrydockTabStyle.CardFill;
        Widgets.DrawBoxSolidWithOutline(rect, fill, selected ? GrayMechDrydockTabStyle.SelectedColor : accent, selected ? 2 : 1);
        Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 4f), accent);
        if (!selected && Mouse.IsOver(rect))
        {
            Widgets.DrawHighlight(rect);
        }
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

    private static int CountInstalledModules(GrayMechDesignSnapshot snapshot)
    {
        if (snapshot?.modules == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            if (snapshot.modules[i]?.module != null)
            {
                count++;
            }
        }

        return count;
    }
}

internal static class GrayMechDrydockPanelWidgets
{
    internal static bool DrawButton(Rect rect, string label, bool enabled = true, bool highlighted = false)
    {
        Color oldColor = GUI.color;
        if (!enabled)
        {
            GUI.color = Color.gray;
        }
        else if (highlighted)
        {
            GUI.color = new Color(GrayMechDrydockTabStyle.ReadyColor.r, GrayMechDrydockTabStyle.ReadyColor.g, GrayMechDrydockTabStyle.ReadyColor.b, 0.92f);
        }

        bool pressed = Widgets.ButtonText(rect, label);
        GUI.color = oldColor;
        if (!pressed)
        {
            return false;
        }

        if (enabled)
        {
            return true;
        }

        SoundDefOf.ClickReject.PlayOneShotOnCamera();
        return false;
    }

    internal static void DrawPill(Rect rect, string label, Color color)
    {
        Widgets.DrawBoxSolidWithOutline(
            rect,
            new Color(color.r, color.g, color.b, 0.18f),
            new Color(color.r, color.g, color.b, 0.92f));

        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Tiny;
        Widgets.Label(rect, label);
        Text.Anchor = oldAnchor;
        Text.Font = oldFont;
    }
}
