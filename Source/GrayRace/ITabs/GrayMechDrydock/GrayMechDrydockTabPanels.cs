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
        const float closeButtonSafePadding = 18f;
        const float primaryButtonWidth = 106f;
        const float smallButtonWidth = 82f;
        const float buttonGap = 8f;
        const float buttonRowWidth = primaryButtonWidth + smallButtonWidth * 4f + buttonGap * 4f;
        Widgets.DrawMenuSection(rect);
        Widgets.DrawBoxSolid(rect.ContractedBy(1f), GrayMechDrydockTabStyle.BgPanel);

        Rect inner = rect.ContractedBy(12f);
        Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 5f), GrayMechDrydockTabStyle.HeaderLineColor);
        Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.yMax - 6f, rect.width - 2f, 2f), new Color(GrayMechDrydockTabStyle.HeaderLineColor.r, GrayMechDrydockTabStyle.HeaderLineColor.g, GrayMechDrydockTabStyle.HeaderLineColor.b, 0.22f));

        string draftName = context.Draft?.designLabel ?? "No design";
        string chassisName = context.Draft?.chassis?.LabelCap.ToString() ?? "No chassis";
        string titleText = "<b>Ship Designer</b>    " + draftName;
        string subText = "Chassis: " + chassisName + "    Source: " + GrayMechDrydockTabText.GetSourceLabel(context.Dock);

        float textWidth = Mathf.Max(220f, rect.width - buttonRowWidth - 48f - closeButtonSafePadding);
        float titleHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(titleText, textWidth, GameFont.Small);
        float subHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(subText, textWidth, GameFont.Small);

        Rect titleRect = new Rect(inner.x, inner.y + 2f, textWidth, titleHeight);
        GrayMechDrydockTabText.DrawWrappedLabel(titleRect, titleText);

        float infoRowY = titleRect.yMax + 4f;
        Rect subRect = new Rect(inner.x, infoRowY, textWidth, subHeight);
        GrayMechDrydockTabText.DrawWrappedLabel(subRect, subText);

        float buttonY = inner.y + 8f;
        float x = rect.xMax - 12f - closeButtonSafePadding - primaryButtonWidth;

        if (GrayMechDrydockPanelWidgets.DrawButton(new Rect(x, buttonY, primaryButtonWidth, 26f), "Add To Queue", context.State.CachedCanQueueOrder, true))
        {
            context.Controller.QueueAssemblyOrder(context.Dock);
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
    private const int AnchoredSectionColumnCount = 3;
    private readonly GrayMechDrydockCanvasRenderer canvasRenderer = new();

    internal void Draw(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Widgets.DrawBoxSolid(rect.ContractedBy(1f), GrayMechDrydockTabStyle.BgPanelAlt);
        Rect canvasRect = rect.ContractedBy(8f);

        DrawShipCanvas(context, canvasRect);
    }

    private void DrawShipCanvas(GrayMechDrydockTabContext context, Rect canvasRect)
    {
        canvasRenderer.DrawSlotBay(canvasRect);
        canvasRenderer.DrawCanvasFrame(canvasRect);
        canvasRenderer.DrawGrid(canvasRect);

        const float bandInset = 32f;
        const float topMargin = 12f;
        const float bottomMargin = 12f;
        const float sectionGap = 14f;
        const float bandGap = 8f;
        const float previewGap = 18f;
        float availableWidth = canvasRect.width - bandInset * 2f;
        int regularSectionCount = 0;

        float slotAreaHeight = GetSlotAreaHeight();
        if (GRMechSectionLayoutCatalog.TryGetSectionSlots(context.Draft?.chassis, out List<GRMechSectionSlotDef> sectionSlots))
        {
            for (int i = 0; i < sectionSlots.Count; i++)
            {
                GRMechSectionSlotDef sectionSlot = sectionSlots[i];
                if (sectionSlot == null)
                {
                    continue;
                }

                regularSectionCount++;
            }
        }

        float headerHeight = GetSectionHeaderHeight(context, availableWidth, sectionGap);
        Rect headerBandRect = new Rect(canvasRect.x + bandInset, canvasRect.y + topMargin, availableWidth, headerHeight);
        Rect topSlotBandRect = new Rect(headerBandRect.x, headerBandRect.yMax + bandGap, headerBandRect.width, slotAreaHeight);
        Rect bottomSlotBandRect = new Rect(headerBandRect.x, canvasRect.yMax - bottomMargin - slotAreaHeight, headerBandRect.width, slotAreaHeight);

        float previewY = topSlotBandRect.yMax + previewGap;
        float previewBottom = bottomSlotBandRect.y - previewGap;
        float previewHeight = Mathf.Max(120f, previewBottom - previewY);
        float previewWidth = Mathf.Min(headerBandRect.width * 0.46f, 360f);
        Rect previewRect = new Rect(headerBandRect.center.x - previewWidth * 0.5f, previewY, previewWidth, previewHeight);

        canvasRenderer.DrawShipPreview(previewRect, context.Draft);

        if (GRMechSectionLayoutCatalog.TryGetSectionSlots(context.Draft?.chassis, out List<GRMechSectionSlotDef> allSectionSlots) && regularSectionCount > 0)
        {
            float columnWidth = GetSectionColumnWidth(headerBandRect.width, sectionGap, regularSectionCount);
            int occupiedColumnMask = 0;
            int fallbackColumnIndex = 0;
            int visibleIndex = 0;
            for (int i = 0; i < allSectionSlots.Count; i++)
            {
                GRMechSectionSlotDef sectionSlot = allSectionSlots[i];
                if (sectionSlot == null)
                {
                    continue;
                }

                int columnIndex = ResolveSectionColumnIndex(sectionSlot, regularSectionCount, visibleIndex, ref occupiedColumnMask, ref fallbackColumnIndex);
                float columnX = headerBandRect.x + columnIndex * (columnWidth + sectionGap);
                Rect headerRect = new Rect(columnX, headerBandRect.y, columnWidth, headerBandRect.height);
                Rect topAreaRect = new Rect(columnX, topSlotBandRect.y, columnWidth, topSlotBandRect.height);
                Rect bottomAreaRect = new Rect(columnX, bottomSlotBandRect.y, columnWidth, bottomSlotBandRect.height);
                bool selected = GRMechSectionSlotUtility.Matches(context.State.SelectedSectionSlot, sectionSlot) && context.State.SelectedSlotKey.NullOrEmpty();
                bool ownsSelectedSlot = context.State.SectionOwnsSelectedSlot(sectionSlot);
                GrayMechDesignUtility.TryGetSelectedLayout(context.Draft, sectionSlot, out GRMechSectionLayoutDef layout);
                DrawSectionColumn(context, sectionSlot, layout, headerRect, topAreaRect, bottomAreaRect, selected, ownsSelectedSlot);
                visibleIndex++;
            }
        }

        if (context.Draft?.chassis?.requiredComponentSlots != null && context.Draft.chassis.requiredComponentSlots.Count > 0)
        {
            Rect corePanelRect = GetCoreSystemsPanelRect(context, canvasRect, previewRect);
            DrawCoreSystemsColumn(context, corePanelRect);
        }
    }

    private static float GetSectionHeaderHeight(GrayMechDrydockTabContext context, float totalWidth, float sectionGap)
    {
        if (!GRMechSectionLayoutCatalog.TryGetSectionSlots(context.Draft?.chassis, out List<GRMechSectionSlotDef> sectionSlots) || sectionSlots.Count == 0)
        {
            return 28f;
        }

        int regularSectionCount = 0;
        for (int i = 0; i < sectionSlots.Count; i++)
        {
            if (sectionSlots[i] != null)
            {
                regularSectionCount++;
            }
        }

        if (regularSectionCount == 0)
        {
            return 28f;
        }

        float columnWidth = GetSectionColumnWidth(totalWidth, sectionGap, regularSectionCount);
        float maxHeight = 0f;
        for (int i = 0; i < sectionSlots.Count; i++)
        {
            GRMechSectionSlotDef sectionSlot = sectionSlots[i];
            if (sectionSlot == null)
            {
                continue;
            }

            GrayMechDesignUtility.TryGetSelectedLayout(context.Draft, sectionSlot, out GRMechSectionLayoutDef layout);
            string headerText = "<b>" + (layout?.LabelCap.ToString() ?? sectionSlot?.LabelCap.ToString() ?? "Unknown") + "</b>";
            maxHeight = Mathf.Max(maxHeight, GrayMechDrydockTabText.MeasureWrappedTextHeight(headerText, columnWidth - 16f, GameFont.Small));
        }

        return Mathf.Max(28f, maxHeight + 10f);
    }

    private static float GetSectionColumnWidth(float totalWidth, float sectionGap, int regularSectionCount)
    {
        int columnCount = GetSectionCanvasColumnCount(regularSectionCount);
        return (totalWidth - Mathf.Max(0f, columnCount - 1) * sectionGap) / Mathf.Max(1, columnCount);
    }

    private static int GetSectionCanvasColumnCount(int regularSectionCount)
    {
        if (regularSectionCount <= 0)
        {
            return AnchoredSectionColumnCount;
        }

        return regularSectionCount < AnchoredSectionColumnCount ? AnchoredSectionColumnCount : regularSectionCount;
    }

    private static int ResolveSectionColumnIndex(GRMechSectionSlotDef sectionSlot, int regularSectionCount, int visibleIndex, ref int occupiedColumnMask, ref int fallbackColumnIndex)
    {
        if (regularSectionCount > AnchoredSectionColumnCount)
        {
            return visibleIndex;
        }

        int preferredIndex = GetPreferredSectionColumnIndex(sectionSlot);
        if (preferredIndex >= 0)
        {
            int preferredMask = 1 << preferredIndex;
            if ((occupiedColumnMask & preferredMask) == 0)
            {
                occupiedColumnMask |= preferredMask;
                return preferredIndex;
            }
        }

        while (fallbackColumnIndex < AnchoredSectionColumnCount)
        {
            int columnIndex = fallbackColumnIndex;
            int columnMask = 1 << columnIndex;
            fallbackColumnIndex++;
            if ((occupiedColumnMask & columnMask) != 0)
            {
                continue;
            }

            occupiedColumnMask |= columnMask;
            return columnIndex;
        }

        return visibleIndex;
    }

    private static int GetPreferredSectionColumnIndex(GRMechSectionSlotDef sectionSlot)
    {
        if (GRMechSectionSlotUtility.IsBow(sectionSlot))
        {
            return 0;
        }

        if (GRMechSectionSlotUtility.IsMid(sectionSlot))
        {
            return 1;
        }

        if (GRMechSectionSlotUtility.IsStern(sectionSlot))
        {
            return 2;
        }

        return -1;
    }

    private void DrawSectionColumn(
        GrayMechDrydockTabContext context,
        GRMechSectionSlotDef sectionSlot,
        GRMechSectionLayoutDef layout,
        Rect headerRect,
        Rect topAreaRect,
        Rect bottomAreaRect,
        bool selected,
        bool ownsSelectedSlot)
    {
        GrayMechDrydockTabState state = context.State;
        state.PrepareSectionSlotBuffers(sectionSlot);
        Color accent = GrayMechDrydockTabText.GetSectionAccentColor(sectionSlot);

        Color outline = selected ? GrayMechDrydockTabStyle.SelectedColor : (ownsSelectedSlot ? accent : new Color(accent.r, accent.g, accent.b, 0.65f));
        Color fill = selected
            ? new Color(GrayMechDrydockTabStyle.SelectedColor.r, GrayMechDrydockTabStyle.SelectedColor.g, GrayMechDrydockTabStyle.SelectedColor.b, 0.12f)
            : new Color(accent.r, accent.g, accent.b, Mouse.IsOver(headerRect) ? 0.08f : 0.04f);
        Widgets.DrawBoxSolidWithOutline(headerRect, fill, outline, selected ? 2 : 1);
        Widgets.DrawBoxSolid(new Rect(headerRect.x + 2f, headerRect.y + 2f, headerRect.width - 4f, 4f), accent);
        if (Widgets.ButtonInvisible(headerRect))
        {
            context.Controller.SelectSection(sectionSlot);
        }

        string headerText = layout?.LabelCap.ToString() ?? sectionSlot.LabelCap.ToString();
        GrayMechDrydockTabText.DrawWrappedLabelCentered(new Rect(headerRect.x + 8f, headerRect.y + 5f, headerRect.width - 16f, headerRect.height - 8f), "<b>" + headerText + "</b>");

        DrawSlotArea(context, topAreaRect, state.WeaponSlotBuffer, GrayMechDrydockTabStyle.MainWeaponColor);
        DrawSlotArea(context, bottomAreaRect, state.SupportSlotBuffer, GrayMechDrydockTabStyle.AuxiliaryColor);
    }

    private void DrawCoreSystemsColumn(GrayMechDrydockTabContext context, Rect panelRect)
    {
        GrayMechDrydockTabState state = context.State;
        state.PrepareRequiredSlotBuffer();
        Color accent = GrayMechDrydockTabStyle.UtilitySlotColor;
        bool ownsSelectedSlot = context.State.SelectedSectionSlot == null && !context.State.SelectedSlotKey.NullOrEmpty();

        bool hovered = Mouse.IsOver(panelRect);
        Color railColor = ownsSelectedSlot
            ? GrayMechDrydockTabStyle.SelectedColor
            : new Color(accent.r, accent.g, accent.b, hovered ? 0.55f : 0.26f);
        float railX = panelRect.center.x - 1f;
        Widgets.DrawBoxSolid(new Rect(railX, panelRect.y + 6f, 2f, panelRect.height - 12f), new Color(railColor.r, railColor.g, railColor.b, 0.28f));
        Widgets.DrawBoxSolid(new Rect(railX - 4f, panelRect.y + 2f, 10f, 2f), railColor);
        Widgets.DrawBoxSolid(new Rect(railX - 4f, panelRect.yMax - 4f, 10f, 2f), railColor);

        bool slotHovered = DrawVerticalSlotList(context, panelRect, state.RequiredSlotBuffer);
        if (!slotHovered && state.RequiredSlotBuffer.Count > 0 && Widgets.ButtonInvisible(panelRect))
        {
            context.Controller.SelectSlot(context.Dock, state.RequiredSlotBuffer[0]);
        }
    }

    private Rect GetSlotGridRect(Rect areaRect)
    {
        float width = GrayMechDrydockTabStyle.SlotGridColumns * GrayMechDrydockTabStyle.SlotButtonSize + (GrayMechDrydockTabStyle.SlotGridColumns - 1) * GrayMechDrydockTabStyle.SlotGridGap;
        float height = 2f * GrayMechDrydockTabStyle.SlotButtonSize + GrayMechDrydockTabStyle.SlotGridGap;
        float x = Mathf.Clamp(areaRect.center.x - width * 0.5f, areaRect.x, areaRect.xMax - width);
        float y = Mathf.Clamp(areaRect.center.y - height * 0.5f, areaRect.y, areaRect.yMax - height);
        return new Rect(x, y, width, height);
    }

    private void DrawSlotArea(
        GrayMechDrydockTabContext context,
        Rect areaRect,
        List<GrayMechResolvedSlot> slots,
        Color accent)
    {
        Widgets.DrawBoxSolidWithOutline(areaRect, new Color(accent.r, accent.g, accent.b, 0.035f), new Color(accent.r, accent.g, accent.b, 0.28f));
        Widgets.DrawBoxSolid(new Rect(areaRect.x + 2f, areaRect.y + 2f, areaRect.width - 4f, 4f), new Color(accent.r, accent.g, accent.b, 0.75f));
        Rect gridRect = GetSlotGridRect(areaRect.ContractedBy(8f));
        DrawSlotGrid(context, gridRect, slots, accent);
    }

    private static float GetSlotAreaHeight()
    {
        return GrayMechDrydockTabStyle.SlotButtonSize * 2f + GrayMechDrydockTabStyle.SlotGridGap + 16f;
    }

    private Rect GetCoreSystemsPanelRect(GrayMechDrydockTabContext context, Rect canvasRect, Rect previewRect)
    {
        const float sideMargin = 4f;
        const float topBottomMargin = 72f;
        GrayMechDrydockTabState state = context.State;
        state.PrepareRequiredSlotBuffer();
        float availableHeight = Mathf.Max(40f, canvasRect.height - topBottomMargin * 2f);
        GetCoreSlotMetrics(availableHeight, state.RequiredSlotBuffer.Count, out float coreSlotSize, out float rowGap);
        float listHeight = GetVerticalSlotListHeight(state.RequiredSlotBuffer.Count, coreSlotSize, rowGap);
        float panelHeight = Mathf.Max(listHeight + 6f, coreSlotSize + 6f);
        float panelWidth = coreSlotSize + 2f;
        float x = canvasRect.xMax - sideMargin - panelWidth;
        float y = Mathf.Clamp(previewRect.center.y - panelHeight * 0.5f, canvasRect.y + topBottomMargin, canvasRect.yMax - panelHeight - topBottomMargin);
        if (state.RequiredSlotBuffer.Count == 0)
        {
            y = previewRect.center.y - panelHeight * 0.5f;
        }

        y = Mathf.Clamp(y, canvasRect.y + topBottomMargin, canvasRect.yMax - panelHeight - topBottomMargin);
        return new Rect(x, y, panelWidth, panelHeight);
    }

    private static void GetCoreSlotMetrics(float availableHeight, int slotCount, out float coreSlotSize, out float rowGap)
    {
        const float maxCoreSlotSize = 34f;
        const float minCoreSlotSize = 14f;
        const float maxRowGap = 6f;

        if (slotCount <= 0)
        {
            coreSlotSize = maxCoreSlotSize;
            rowGap = 0f;
            return;
        }

        if (slotCount == 1)
        {
            coreSlotSize = Mathf.Min(maxCoreSlotSize, availableHeight);
            rowGap = 0f;
            return;
        }

        float rawSlotSize = (availableHeight - maxRowGap * (slotCount - 1)) / slotCount;
        coreSlotSize = Mathf.Min(maxCoreSlotSize, rawSlotSize);
        coreSlotSize = Mathf.Max(minCoreSlotSize, coreSlotSize);

        float remainingHeight = Mathf.Max(0f, availableHeight - coreSlotSize * slotCount);
        rowGap = Mathf.Min(maxRowGap, remainingHeight / (slotCount - 1));
    }

    private static float GetVerticalSlotListHeight(int slotCount, float coreSlotSize, float rowGap)
    {
        if (slotCount <= 0)
        {
            return 0f;
        }

        return slotCount * coreSlotSize + Mathf.Max(0, slotCount - 1) * rowGap;
    }

    private bool DrawVerticalSlotList(GrayMechDrydockTabContext context, Rect listRect, List<GrayMechResolvedSlot> slots)
    {
        GetCoreSlotMetrics(listRect.height, slots.Count, out float coreSlotSize, out float rowGap);
        int visibleCount = slots.Count;
        if (visibleCount <= 0)
        {
            return false;
        }

        float totalHeight = visibleCount * coreSlotSize + Mathf.Max(0, visibleCount - 1) * rowGap;
        float startY = Mathf.Clamp(listRect.center.y - totalHeight * 0.5f, listRect.y, listRect.yMax - totalHeight);
        float slotX = listRect.center.x - coreSlotSize * 0.5f;
        bool slotHovered = false;

        for (int i = 0; i < visibleCount; i++)
        {
            GrayMechResolvedSlot resolvedSlot = slots[i];
            if (resolvedSlot?.slot == null)
            {
                continue;
            }

            float y = startY + i * (coreSlotSize + rowGap);
            Rect slotRect = new Rect(slotX, y, coreSlotSize, coreSlotSize);
            if (Mouse.IsOver(slotRect))
            {
                slotHovered = true;
            }

            GrayMechDesignUtility.TryGetSelectedModule(context.Draft, resolvedSlot, out GRMechModuleDef module);
            bool selected = GRMechSectionSlotUtility.Matches(context.State.SelectedSectionSlot, resolvedSlot.sectionSlot) && context.State.SelectedSlotKey == resolvedSlot.slot.key;
            canvasRenderer.DrawSlotWidget(slotRect, resolvedSlot.slot, module, selected);
            if (Widgets.ButtonInvisible(slotRect))
            {
                context.Controller.SelectSlot(context.Dock, resolvedSlot);
            }

            TooltipHandler.TipRegion(slotRect, GrayMechDrydockTabText.BuildSlotTooltip(resolvedSlot, module));
        }

        return slotHovered;
    }

    private static bool WasSecondaryClick(Rect rect)
    {
        Event current = Event.current;
        if (current.type == EventType.MouseDown && current.button == 1 && Mouse.IsOver(rect))
        {
            current.Use();
            return true;
        }

        return false;
    }

    private void DrawSlotGrid(
        GrayMechDrydockTabContext context,
        Rect gridRect,
        List<GrayMechResolvedSlot> slots,
        Color accent)
    {
        int activeSlotCount = Mathf.Min(GrayMechDrydockTabStyle.FixedDisplaySlotCount, slots.Count);
        for (int i = 0; i < GrayMechDrydockTabStyle.FixedDisplaySlotCount; i++)
        {
            int row = i / GrayMechDrydockTabStyle.SlotGridColumns;
            int column = i % GrayMechDrydockTabStyle.SlotGridColumns;
            float x = gridRect.x + column * (GrayMechDrydockTabStyle.SlotButtonSize + GrayMechDrydockTabStyle.SlotGridGap);
            float y = gridRect.y + row * (GrayMechDrydockTabStyle.SlotButtonSize + GrayMechDrydockTabStyle.SlotGridGap);
            Rect slotRect = new Rect(x, y, GrayMechDrydockTabStyle.SlotButtonSize, GrayMechDrydockTabStyle.SlotButtonSize);
            if (i < activeSlotCount && slots[i]?.slot != null)
            {
                GrayMechResolvedSlot resolvedSlot = slots[i];
                GrayMechDesignUtility.TryGetSelectedModule(context.Draft, resolvedSlot, out GRMechModuleDef module);
                bool selected = GRMechSectionSlotUtility.Matches(context.State.SelectedSectionSlot, resolvedSlot.sectionSlot) && context.State.SelectedSlotKey == resolvedSlot.slot.key;
                canvasRenderer.DrawSlotWidget(slotRect, resolvedSlot.slot, module, selected);
                if (WasSecondaryClick(slotRect))
                {
                    if (module != null)
                    {
                        context.Controller.ClearSlotModule(context.Dock, resolvedSlot);
                    }
                }
                else if (Widgets.ButtonInvisible(slotRect))
                {
                    context.Controller.SelectSlot(context.Dock, resolvedSlot);
                }

                TooltipHandler.TipRegion(slotRect, GrayMechDrydockTabText.BuildSlotTooltip(resolvedSlot, module));
            }
            else
            {
                canvasRenderer.DrawDisabledSlotWidget(slotRect, accent);
            }
        }
    }

}
internal sealed class GrayMechDrydockFocusPanel
{
    private const string CombatExtendedPackageId = "CETeam.CombatExtended";
    private readonly GrayMechDrydockCanvasRenderer canvasRenderer = new();

    internal void DrawSummary(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Widgets.DrawBoxSolid(rect.ContractedBy(1f), GrayMechDrydockTabStyle.BgPanel);
        DrawSummaryPanel(context, rect.ContractedBy(8f));
    }

    internal void DrawSelection(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Widgets.DrawBoxSolid(rect.ContractedBy(1f), GrayMechDrydockTabStyle.BgPanel);
        Rect inner = rect.ContractedBy(8f);

        if (context.State.TryGetResolvedSlot(context.State.SelectedSectionSlot, context.State.SelectedSlotKey, out GrayMechResolvedSlot resolvedSlot))
        {
            DrawSlotFocusPanel(context, resolvedSlot, inner);
            return;
        }

        DrawSectionFocusPanel(context, inner);
    }

    private void DrawSummaryPanel(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFillMuted, GrayMechDrydockTabStyle.HullOutline);
        GrayMechDrydockTabState state = context.State;
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

        bool ready = context.State.CachedCanQueueOrder;
        string queueStatus = context.State.CachedHasActiveOrder
            ? context.State.CachedProductionStatus
            : (ready ? "Ready for assembly" : context.State.CachedQueueReason);
        Color queueColor = context.State.CachedHasActiveOrder
            ? GrayMechDrydockTabStyle.EngineColor
            : (ready ? GrayMechDrydockTabStyle.ReadyColor : GrayMechDrydockTabStyle.LockedColor);
        float statusWidth = Mathf.Min(160f, headerRect.width * 0.36f);
        float statusHeight = GrayMechDrydockTabText.GetPillHeight(queueStatus, statusWidth);
        Rect statusRect = new Rect(headerRect.xMax - statusWidth, headerRect.y, statusWidth, statusHeight);
        GrayMechDrydockPanelWidgets.DrawPill(statusRect, queueStatus, queueColor);

        Rect thumbRect = new Rect(headerRect.x, headerRect.y, 78f, 56f);
        DrawChassisThumb(thumbRect, context.Draft?.chassis);

        float textWidth = Mathf.Max(1f, headerRect.width - thumbRect.width - statusWidth - 24f);
        float textX = thumbRect.xMax + 12f;
        string nameText = "<b>" + (context.Draft?.designLabel ?? "No design") + "</b>";
        string chassisText = "Chassis: " + (context.Draft?.chassis?.LabelCap.ToString() ?? "None");
        string sourceText = GrayMechDrydockTabText.GetSourceLabel(context.Dock);
        float nameHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(nameText, textWidth, GameFont.Small);
        float chassisHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(chassisText, textWidth, GameFont.Small);
        float sourceHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(sourceText, textWidth, GameFont.Small);
        float textY = headerRect.y;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(textX, textY, textWidth, nameHeight), nameText);
        textY += nameHeight + 2f;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(textX, textY, textWidth, chassisHeight), chassisText);
        textY += chassisHeight + 2f;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(textX, textY, textWidth, sourceHeight), sourceText);

        float headerHeight = Mathf.Max(thumbRect.height, Mathf.Max(statusRect.height, textY + sourceHeight - headerRect.y));
        float dividerY = rect.y + 8f + headerHeight + 8f;
        Widgets.DrawLineHorizontal(rect.x + 4f, dividerY, rect.width - 8f);

        Rect statsRect = new Rect(rect.x, dividerY + 8f, rect.width, rect.yMax - dividerY - 8f);
        float y = statsRect.y;

        y = DrawSummarySectionHeader(new Rect(statsRect.x, y, statsRect.width, 22f), "Production", GrayMechDrydockTabStyle.AuxiliaryColor);
        y += 4f;
        y = DrawSummaryInfoRow(statsRect, y, "Build Time", context.State.CachedBuildTimeLabel, GrayMechDrydockTabStyle.EngineColor);
        y = DrawSummaryInfoRow(statsRect, y, "Queue", context.State.CachedQueueCountLabel, GrayMechDrydockTabStyle.UtilitySlotColor);
        y = DrawSummaryInfoRow(statsRect, y, "Module Load", $"{context.State.CachedFilledSlotCount} / {context.State.CachedTotalSlotCount}", GrayMechDrydockTabStyle.AuxiliaryColor);
        y = DrawSummaryInfoRow(statsRect, y, "Build Cost", context.State.CachedCostSummary.NullOrEmpty() ? "None" : context.State.CachedCostSummary, GrayMechDrydockTabStyle.SelectedColor, allowWrap: true);

        y += 6f;
        y = DrawSummarySectionHeader(new Rect(statsRect.x, y, statsRect.width, 22f), "Ship Stats", GrayMechDrydockTabStyle.HullOutline);
        y += 4f;
        y = DrawArmorRows(statsRect, y, context);
        y = DrawSummaryInfoRow(statsRect, y, "Shield", BuildShieldSummary(context), GrayMechDrydockTabStyle.EngineColor);
        y = DrawSummaryInfoRow(statsRect, y, "Speed", context.State.CachedMoveSpeed.ToString("0.#"), GrayMechDrydockTabStyle.EngineColor);
        y = DrawSummaryInfoRow(statsRect, y, "Bandwidth", context.State.CachedBandwidthCost.ToString("0.#"), new Color(0.74f, 0.52f, 0.95f));
        y = DrawSummaryInfoRow(statsRect, y, "Fire Rate", FormatDeltaPercent(1f - context.State.CachedRangedCooldownFactor), GrayMechDrydockTabStyle.MainWeaponColor);
        y = DrawSummaryInfoRow(statsRect, y, "Accuracy", BuildAccuracySummary(context), GrayMechDrydockTabStyle.SelectedColor);
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
        GrayMechDrydockTabState state = context.State;
        y = DrawSummaryInfoRow(areaRect, y, "Sharp Armor", state.CachedArmorSharp.ToString("0.##"), GrayMechDrydockTabStyle.MainWeaponColor);
        y = DrawSummaryInfoRow(areaRect, y, "Blunt Armor", state.CachedArmorBlunt.ToString("0.##"), GrayMechDrydockTabStyle.MainWeaponColor);
        y = DrawSummaryInfoRow(areaRect, y, "Heat Armor", state.CachedArmorHeat.ToString("0.##"), GrayMechDrydockTabStyle.MainWeaponColor);
        return y;
    }

    private static float DrawCombatExtendedArmorRows(Rect areaRect, float y, GrayMechDrydockTabContext context)
    {
        // CE currently overrides the same armor stat defs through XML patches in this project.
        // Keep a dedicated code path here so we can swap to CE-specific presentation later
        // without having to touch the vanilla summary layout again.
        return DrawVanillaArmorRows(areaRect, y, context);
    }

    private static string BuildShieldSummary(GrayMechDrydockTabContext context)
    {
        GrayMechDrydockTabState state = context.State;
        if (state.CachedShieldEnergyMax <= 0.001f)
        {
            return "None";
        }

        return state.CachedShieldEnergyMax.ToString("0.##") + "  (+" + state.CachedShieldRechargeRate.ToString("0.###") + "/s)";
    }

    private static string BuildAccuracySummary(GrayMechDrydockTabContext context)
    {
        GrayMechDrydockTabState state = context.State;
        bool hasPawnAcc = Mathf.Abs(state.CachedShootingAccuracyPawn) > 0.001f;
        bool hasLongAcc = Mathf.Abs(state.CachedLongAccuracyFactor - 1f) > 0.001f;
        if (hasPawnAcc && hasLongAcc)
        {
            return FormatSignedValue(state.CachedShootingAccuracyPawn) + "  |  L " + FormatDeltaPercent(state.CachedLongAccuracyFactor - 1f);
        }

        if (hasPawnAcc)
        {
            return FormatSignedValue(state.CachedShootingAccuracyPawn);
        }

        if (hasLongAcc)
        {
            return "L " + FormatDeltaPercent(state.CachedLongAccuracyFactor - 1f);
        }

        return "Baseline";
    }

    private static string FormatDeltaPercent(float value)
    {
        float percent = value * 100f;
        if (percent > 0.001f)
        {
            return "+" + percent.ToString("0.#") + "%";
        }

        if (percent < -0.001f)
        {
            return percent.ToString("0.#") + "%";
        }

        return "0%";
    }

    private static string FormatSignedValue(float value)
    {
        if (value > 0.001f)
        {
            return "+" + value.ToString("0.#");
        }

        if (value < -0.001f)
        {
            return value.ToString("0.#");
        }

        return "0";
    }

    private void DrawSectionFocusPanel(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFill, GrayMechDrydockTabStyle.HullOutline);
        Rect inner = rect.ContractedBy(8f);

        GRMechSectionSlotDef sectionSlot = context.State.SelectedSectionSlot;
        if (sectionSlot == null || !GRMechSectionLayoutCatalog.TryGetLayouts(context.Draft?.chassis, sectionSlot, out List<GRMechSectionLayoutDef> layouts))
        {
            Widgets.Label(inner, sectionSlot == null ? "Select a ship section to edit its layout." : "No layouts are available for the selected section.");
            return;
        }

        GrayMechDesignUtility.TryGetSelectedLayout(context.Draft, sectionSlot, out GRMechSectionLayoutDef currentLayout);
        string titleText = "<b>" + sectionSlot.LabelCap + "</b>";
        float titleHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(titleText, inner.width, GameFont.Small);
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(inner.x, inner.y, inner.width, titleHeight), titleText);

        float listY = inner.y + titleHeight + 10f;
        Rect listRect = new Rect(inner.x, listY, inner.width, inner.yMax - listY);
        float viewHeight = 0f;
        for (int i = 0; i < layouts.Count; i++)
        {
            GRMechSectionLayoutDef layout = layouts[i];
            if (layout != null)
            {
                viewHeight += context.State.GetLayoutOptionHeight(layout, listRect.width - 16f) + 8f;
            }
        }

        GrayMechDrydockTabState state = context.State;
        Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(listRect.height, viewHeight));
        Widgets.BeginScrollView(listRect, ref state.FocusScrollPosition, viewRect);

        float y = 0f;
        Color accent = GrayMechDrydockTabText.GetSectionAccentColor(sectionSlot);
        float minVisibleY = state.FocusScrollPosition.y - 64f;
        float maxVisibleY = state.FocusScrollPosition.y + listRect.height + 64f;
        for (int i = 0; i < layouts.Count; i++)
        {
            GRMechSectionLayoutDef layout = layouts[i];
            if (layout == null)
            {
                continue;
            }

            bool selected = currentLayout == layout;
            bool available = GrayMechDesignUtility.IsResearchAvailable(layout);
            float rowHeight = state.GetLayoutOptionHeight(layout, viewRect.width);
            if (y + rowHeight < minVisibleY || y > maxVisibleY)
            {
                y += rowHeight + 8f;
                continue;
            }

            Rect rowRect = new Rect(0f, y, viewRect.width, rowHeight);
            bool hovered = Mouse.IsOver(rowRect);

            Color fill = selected
                ? new Color(GrayMechDrydockTabStyle.SelectedColor.r, GrayMechDrydockTabStyle.SelectedColor.g, GrayMechDrydockTabStyle.SelectedColor.b, 0.11f)
                : new Color(accent.r, accent.g, accent.b, hovered ? 0.08f : 0.04f);
            Color outline = selected ? GrayMechDrydockTabStyle.SelectedColor : (hovered ? Color.white : accent);

            Widgets.DrawBoxSolidWithOutline(rowRect, fill, outline);

            if (available && Widgets.ButtonInvisible(rowRect))
            {
                context.Controller.SetSectionLayout(context.Dock, sectionSlot, layout);
            }

            Rect iconRect = new Rect(rowRect.x + 6f, rowRect.y + 6f, 36f, 36f);
            GRMechLayoutSlotUtility.TryGetFirstSlot(layout, out GRMechSlotEntry previewSlot);
            canvasRenderer.DrawModuleIconTile(iconRect, null, previewSlot);

            float leftWidth = rowRect.width - 56f;
            float leftX = iconRect.xMax + 8f;
            string layoutTitle = "<b>" + layout.LabelCap + "</b>";
            string slotsText = "Slots: " + GrayMechDrydockTabText.BuildLayoutSlotExpression(layout, state.TextBuilder);
            float titleH = GrayMechDrydockTabText.MeasureWrappedTextHeight(layoutTitle, leftWidth, GameFont.Small);
            float slotsH = GrayMechDrydockTabText.MeasureWrappedTextHeight(slotsText, leftWidth, GameFont.Small);
            float localY = rowRect.y + 4f;
            GrayMechDrydockTabText.DrawWrappedLabel(new Rect(leftX, localY, leftWidth, titleH), layoutTitle);
            localY += titleH + 2f;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            GrayMechDrydockTabText.DrawWrappedLabel(new Rect(leftX, localY, leftWidth, slotsH), slotsText);
            GUI.color = Color.white;
            y += rowHeight + 8f;
        }

        Widgets.EndScrollView();
    }

    private void DrawSlotFocusPanel(GrayMechDrydockTabContext context, GrayMechResolvedSlot resolvedSlot, Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFill, GrayMechDrydockTabStyle.HullOutline);
        Rect inner = rect.ContractedBy(8f);
        Color accent = GrayMechDrydockTabText.GetSectionAccentColor(resolvedSlot.sectionSlot);

        GrayMechDesignUtility.TryGetSelectedModule(context.Draft, resolvedSlot, out GRMechModuleDef currentModule);
        Rect slotIconRect = new Rect(inner.x, inner.y + 2f, 42f, 42f);
        canvasRenderer.DrawModuleIconTile(slotIconRect, currentModule, resolvedSlot.slot);

        string titleText = "<b>" + GrayMechDrydockTabText.GetSlotDisplayName(resolvedSlot.slot) + "</b>";
        string metaText = GrayMechDrydockTabText.GetSlotOwnerLabel(resolvedSlot) + "  |  " + GrayMechDrydockTabText.BuildSlotTypeSummary(resolvedSlot.slot);
        float headerX = slotIconRect.xMax + 8f;
        float headerWidth = inner.width - slotIconRect.width - 8f;
        float titleHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(titleText, headerWidth, GameFont.Small);
        float metaHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(metaText, headerWidth, GameFont.Small);
        float headerY = inner.y;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(headerX, headerY, headerWidth, titleHeight), titleText);
        headerY += titleHeight + 2f;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(headerX, headerY, headerWidth, metaHeight), metaText);
        headerY = Mathf.Max(slotIconRect.yMax, headerY + metaHeight) + 8f;

        GrayMechDrydockTabState state = context.State;
        Rect toggleRect = new Rect(inner.x, headerY, inner.width, 24f);
        bool showObsolete = state.ShowObsoleteModules;
        Widgets.CheckboxLabeled(toggleRect, "Show obsolete components", ref showObsolete);
        if (showObsolete != state.ShowObsoleteModules)
        {
            state.SetShowObsoleteModules(showObsolete);
            state.EnsureCompatibleModuleCache(context.Dock);
        }

        headerY = toggleRect.yMax + 6f;
        Rect listRect = new Rect(inner.x, headerY, inner.width, inner.yMax - headerY);
        float viewHeight = context.State.GetModuleOptionHeight(null, listRect.width - 16f) + 8f;
        for (int i = 0; i < context.State.CompatibleModules.Count; i++)
        {
            viewHeight += context.State.GetModuleOptionHeight(context.State.CompatibleModules[i], listRect.width - 16f) + 8f;
        }

        Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(listRect.height, viewHeight));
        Widgets.BeginScrollView(listRect, ref state.FocusScrollPosition, viewRect);

        float emptyHeight = state.GetModuleOptionHeight(null, viewRect.width);
        DrawModuleOptionRow(context, new Rect(0f, 0f, viewRect.width, emptyHeight), resolvedSlot, null, currentModule == null, accent);

        float y = emptyHeight + 8f;
        float minVisibleY = state.FocusScrollPosition.y - 64f;
        float maxVisibleY = state.FocusScrollPosition.y + listRect.height + 64f;
        for (int i = 0; i < state.CompatibleModules.Count; i++)
        {
            GRMechModuleDef module = state.CompatibleModules[i];
            float rowHeight = state.GetModuleOptionHeight(module, viewRect.width);
            if (y + rowHeight < minVisibleY || y > maxVisibleY)
            {
                y += rowHeight + 8f;
                continue;
            }

            DrawModuleOptionRow(context, new Rect(0f, y, viewRect.width, rowHeight), resolvedSlot, module, currentModule == module, accent);
            y += rowHeight + 8f;
        }

        Widgets.EndScrollView();
    }

    private void DrawModuleOptionRow(GrayMechDrydockTabContext context, Rect rowRect, GrayMechResolvedSlot resolvedSlot, GRMechModuleDef module, bool selected, Color accent)
    {
        bool hovered = Mouse.IsOver(rowRect);

        Color fill = selected
            ? new Color(GrayMechDrydockTabStyle.SelectedColor.r, GrayMechDrydockTabStyle.SelectedColor.g, GrayMechDrydockTabStyle.SelectedColor.b, 0.11f)
            : new Color(accent.r, accent.g, accent.b, hovered ? 0.08f : 0.04f);
        Color outline = selected ? GrayMechDrydockTabStyle.SelectedColor : (hovered ? Color.white : accent);

        Widgets.DrawBoxSolidWithOutline(rowRect, fill, outline);

        if (Widgets.ButtonInvisible(rowRect))
        {
            context.Controller.SetModule(context.Dock, resolvedSlot.sectionSlot, resolvedSlot.slot.key, module);
        }
        Rect iconRect = new Rect(rowRect.x + 6f, rowRect.y + 6f, 36f, 36f);
        canvasRenderer.DrawModuleIconTile(iconRect, module, resolvedSlot.slot);

        float leftWidth = rowRect.width - 58f;
        float leftX = iconRect.xMax + 8f;
        string title = module?.LabelCap.ToString() ?? "Empty Slot";
        string cost = module == null ? "Cost: None" : GrayMechDrydockTabText.BuildModuleCostSummary(module, context.State.TextBuilder);

        string titleText = "<b>" + title + "</b>";
        float titleH = GrayMechDrydockTabText.MeasureWrappedTextHeight(titleText, leftWidth, GameFont.Small);
        float costH = GrayMechDrydockTabText.MeasureWrappedTextHeight(cost, leftWidth, GameFont.Small);
        float localY = rowRect.y + 4f;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(leftX, localY, leftWidth, titleH), titleText);
        localY += titleH + 2f;
        GUI.color = new Color(0.8f, 0.8f, 0.8f);
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(leftX, localY, leftWidth, costH), cost);
        GUI.color = Color.white;
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

        Rect contentRect = new Rect(inner.x, inner.y + 4f, inner.width, inner.height - 4f);
        DrawLibraryCards(context, contentRect);
    }

    private void DrawLibraryCards(GrayMechDrydockTabContext context, Rect rect)
    {
        GrayMechDrydockTabState state = context.State;
        int cardCount = state.ChassisCache.Count + state.DesignCache.Count;
        float contentWidth = 8f + cardCount * (GrayMechDrydockTabStyle.LibraryCardWidth + 8f);
        float maxCardHeight = 0f;
        for (int i = 0; i < state.ChassisCache.Count; i++)
        {
            maxCardHeight = Mathf.Max(maxCardHeight, state.GetLibraryCardHeight(state.ChassisCache[i]));
        }

        for (int i = 0; i < state.DesignCache.Count; i++)
        {
            maxCardHeight = Mathf.Max(maxCardHeight, state.GetLibraryCardHeight(state.DesignCache[i]));
        }

        Rect viewRect = new Rect(0f, 0f, Mathf.Max(rect.width - 16f, contentWidth), Mathf.Max(rect.height - 16f, maxCardHeight + 8f));

        Widgets.BeginScrollView(rect, ref state.LibraryScrollPosition, viewRect);
        float x = 8f;
        float minVisibleX = state.LibraryScrollPosition.x - GrayMechDrydockTabStyle.LibraryCardWidth - 24f;
        float maxVisibleX = state.LibraryScrollPosition.x + rect.width + GrayMechDrydockTabStyle.LibraryCardWidth + 24f;
        for (int i = 0; i < state.ChassisCache.Count; i++)
        {
            GRMechChassisDef chassis = state.ChassisCache[i];
            Rect cardRect = new Rect(x, 4f, GrayMechDrydockTabStyle.LibraryCardWidth, state.GetLibraryCardHeight(chassis));
            if (cardRect.xMax >= minVisibleX && cardRect.x <= maxVisibleX)
            {
                bool selected = !context.Dock.IsEditingSavedDesign
                    && context.Draft?.chassis == chassis
                    && context.Draft?.designLabel == chassis?.LabelCap.ToString();
                DrawChassisCard(context, cardRect, chassis, selected);
            }

            x += GrayMechDrydockTabStyle.LibraryCardWidth + 8f;
        }

        for (int i = 0; i < state.DesignCache.Count; i++)
        {
            GrayMechDesignRecord design = state.DesignCache[i];
            Rect cardRect = new Rect(x, 4f, GrayMechDrydockTabStyle.LibraryCardWidth, state.GetLibraryCardHeight(design));
            if (cardRect.xMax >= minVisibleX && cardRect.x <= maxVisibleX)
            {
                bool selected = context.Dock.IsEditingSavedDesign && context.Dock.EditingDesignId == design.id;
                DrawDesignCard(context, cardRect, design, selected);
            }

            x += GrayMechDrydockTabStyle.LibraryCardWidth + 8f;
        }

        Widgets.EndScrollView();
    }

    private void DrawChassisCard(GrayMechDrydockTabContext context, Rect rect, GRMechChassisDef chassis, bool selected)
    {
        bool available = GrayMechDesignUtility.IsResearchAvailable(chassis);
        DrawLibraryCardChrome(rect, selected, available ? GrayMechDrydockTabStyle.UtilitySlotColor : GrayMechDrydockTabStyle.LockedColor);

        if (Widgets.ButtonInvisible(rect))
        {
            context.Controller.LoadChassis(context.Dock, chassis);
        }

        float headerHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight("Chassis", rect.width - 12f, GameFont.Tiny);
        Rect previewRect = new Rect(rect.x + 4f, rect.y + 4f + headerHeight + 2f, rect.width - 8f, 36f);
        DrawChassisPreview(previewRect, chassis);
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, headerHeight), "Chassis");
        Text.Font = oldFont;

        string title = chassis?.LabelCap.ToString() ?? "Unnamed";
        string footer = available ? "Start new draft" : "Locked";
        float titleHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight("<b>" + title + "</b>", rect.width - 12f, GameFont.Small);
        float footerHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(footer, rect.width - 12f, GameFont.Tiny);
        float textY = previewRect.yMax + 2f;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(rect.x + 6f, textY, rect.width - 12f, titleHeight), "<b>" + title + "</b>");
        textY += titleHeight + 2f;
        oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(rect.x + 6f, textY, rect.width - 12f, footerHeight), footer);
        Text.Font = oldFont;
        TooltipHandler.TipRegion(rect, chassis?.description ?? string.Empty);
    }

    private void DrawDesignCard(GrayMechDrydockTabContext context, Rect rect, GrayMechDesignRecord design, bool selected)
    {
        DrawLibraryCardChrome(rect, selected, GrayMechDrydockTabStyle.MainWeaponColor);

        if (Widgets.ButtonInvisible(rect))
        {
            context.Controller.LoadSavedDesign(context.Dock, design);
        }

        GrayMechDesignSnapshot snapshot = design?.snapshot;
        float headerHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight("Saved", rect.width - 12f, GameFont.Tiny);
        Rect previewRect = new Rect(rect.x + 4f, rect.y + 4f + headerHeight + 2f, rect.width - 8f, 36f);
        DrawChassisPreview(previewRect, snapshot?.chassis);
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, headerHeight), "Saved");
        Text.Font = oldFont;

        string title = design?.label ?? "Unnamed";
        string chassis = snapshot?.chassis?.LabelCap.ToString() ?? "No chassis";
        string footer = chassis + "   Modules " + CountInstalledModules(snapshot);
        float titleHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight("<b>" + title + "</b>", rect.width - 12f, GameFont.Small);
        float footerHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(footer, rect.width - 12f, GameFont.Tiny);
        float textY = previewRect.yMax + 2f;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(rect.x + 6f, textY, rect.width - 12f, titleHeight), "<b>" + title + "</b>");
        textY += titleHeight + 2f;
        oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(rect.x + 6f, textY, rect.width - 12f, footerHeight), footer);
        Text.Font = oldFont;
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
        bool oldWrap = Text.WordWrap;
        Text.Anchor = TextAnchor.UpperCenter;
        Text.Font = GameFont.Tiny;
        Text.WordWrap = true;
        float labelHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(label, rect.width - 8f, GameFont.Tiny);
        Rect labelRect = new Rect(rect.x + 4f, rect.y + (rect.height - labelHeight) * 0.5f, rect.width - 8f, labelHeight);
        Widgets.Label(labelRect, label);
        Text.WordWrap = oldWrap;
        Text.Anchor = oldAnchor;
        Text.Font = oldFont;
    }
}
