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
        string titleText = "<b>舰船设计</b>    " + draftName;
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

        if (GrayMechDrydockPanelWidgets.DrawButton(new Rect(x, buttonY, primaryButtonWidth, 26f), "添加到建造队列", context.State.CachedCanQueueOrder, true))
        {
            context.Controller.QueueAssemblyOrder(context.Dock);
        }

        x -= smallButtonWidth + 8f;
        if (GrayMechDrydockPanelWidgets.DrawButton(new Rect(x, buttonY, smallButtonWidth, 26f), "保存", context.Dock.IsEditingSavedDesign))
        {
            context.Controller.SaveCurrentDesign(context.Dock);
        }

        x -= smallButtonWidth + 8f;
        if (GrayMechDrydockPanelWidgets.DrawButton(new Rect(x, buttonY, smallButtonWidth, 26f), "另存为"))
        {
            context.Controller.SaveAsNewDesign(context.Dock);
        }

        x -= smallButtonWidth + 8f;
        if (GrayMechDrydockPanelWidgets.DrawButton(new Rect(x, buttonY, smallButtonWidth, 26f), "重命名", context.Dock.IsEditingSavedDesign))
        {
            context.Controller.RenameCurrentDesign(context.Dock);
        }

        x -= smallButtonWidth + 8f;
        if (GrayMechDrydockPanelWidgets.DrawButton(new Rect(x, buttonY, smallButtonWidth, 26f), "删除", context.Dock.IsEditingSavedDesign))
        {
            context.Controller.DeleteCurrentDesign(context.Dock);
        }
    }
}

internal sealed class GrayMechDrydockDesignerPanel
{
    private const int AnchoredSectionColumnCount = 3;
    private const int SectionGridRowCount = 2;
    private const float PreferredSectionBandInset = 32f;
    private const float MinSectionBandInset = 8f;
    private readonly GrayMechSectionCanvasPanel sectionCanvasPanel = new();
    private readonly GrayMechDrydockDesignerCanvasHost canvasHost = new();
    private readonly GrayMechDrydockCanvasRenderer canvasRenderer = new();

    internal void Draw(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Widgets.DrawBoxSolid(rect.ContractedBy(1f), GrayMechDrydockTabStyle.BgPanelAlt);
        Rect canvasRect = rect.ContractedBy(8f);
        canvasHost.Bind(context);
        sectionCanvasPanel.Draw(canvasRect, canvasHost);
    }

    private void DrawShipCanvas(GrayMechDrydockTabContext context, Rect canvasRect)
    {
        canvasRenderer.DrawSlotBay(canvasRect);
        canvasRenderer.DrawCanvasFrame(canvasRect);
        canvasRenderer.DrawGrid(canvasRect);

        const float topMargin = 12f;
        const float bottomMargin = 12f;
        const float sectionGap = 14f;
        const float bandGap = 8f;
        const float previewGap = 18f;
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

        float sectionBandInset = GetSectionBandInset(canvasRect.width, sectionGap, regularSectionCount);
        float sectionBandWidth = Mathf.Max(0f, canvasRect.width - sectionBandInset * 2f);
        float headerHeight = GetSectionHeaderHeight(context, sectionBandWidth, sectionGap);
        Rect headerBandRect = new Rect(canvasRect.x + sectionBandInset, canvasRect.y + topMargin, sectionBandWidth, headerHeight);
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

    private static float GetSectionBandInset(float canvasWidth, float sectionGap, int regularSectionCount)
    {
        if (regularSectionCount <= 0)
        {
            return PreferredSectionBandInset;
        }

        float requiredWidth = GetMinimumSectionBandWidth(sectionGap, regularSectionCount);
        float preferredWidth = canvasWidth - PreferredSectionBandInset * 2f;
        if (preferredWidth >= requiredWidth)
        {
            return PreferredSectionBandInset;
        }

        return Mathf.Clamp((canvasWidth - requiredWidth) * 0.5f, MinSectionBandInset, PreferredSectionBandInset);
    }

    private static float GetMinimumSectionBandWidth(float sectionGap, int regularSectionCount)
    {
        int columnCount = GetSectionCanvasColumnCount(regularSectionCount);
        return GrayMechDrydockTabStyle.MinSectionColumnWidth * columnCount + Mathf.Max(0f, columnCount - 1) * sectionGap;
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

    private Rect GetSlotGridRect(Rect areaRect, out float slotButtonSize)
    {
        slotButtonSize = GetSlotButtonSize(areaRect);
        float width = GrayMechDrydockTabStyle.SlotGridColumns * slotButtonSize + (GrayMechDrydockTabStyle.SlotGridColumns - 1) * GrayMechDrydockTabStyle.SlotGridGap;
        float height = SectionGridRowCount * slotButtonSize + (SectionGridRowCount - 1) * GrayMechDrydockTabStyle.SlotGridGap;
        float x = Mathf.Clamp(areaRect.center.x - width * 0.5f, areaRect.x, areaRect.xMax - width);
        float y = Mathf.Clamp(areaRect.center.y - height * 0.5f, areaRect.y, areaRect.yMax - height);
        return new Rect(x, y, width, height);
    }

    private static float GetSlotButtonSize(Rect areaRect)
    {
        float widthPerSlot = (areaRect.width - GrayMechDrydockTabStyle.SlotGridGap * (GrayMechDrydockTabStyle.SlotGridColumns - 1)) / GrayMechDrydockTabStyle.SlotGridColumns;
        float heightPerSlot = (areaRect.height - GrayMechDrydockTabStyle.SlotGridGap * (SectionGridRowCount - 1)) / SectionGridRowCount;
        float slotButtonSize = Mathf.Min(GrayMechDrydockTabStyle.SlotButtonSize, widthPerSlot, heightPerSlot);
        return Mathf.Max(1f, slotButtonSize);
    }

    private void DrawSlotArea(
        GrayMechDrydockTabContext context,
        Rect areaRect,
        List<GRMechResolvedSlot> slots,
        Color accent)
    {
        Widgets.DrawBoxSolidWithOutline(areaRect, new Color(accent.r, accent.g, accent.b, 0.035f), new Color(accent.r, accent.g, accent.b, 0.28f));
        Widgets.DrawBoxSolid(new Rect(areaRect.x + 2f, areaRect.y + 2f, areaRect.width - 4f, 4f), new Color(accent.r, accent.g, accent.b, 0.75f));
        Rect gridRect = GetSlotGridRect(areaRect.ContractedBy(8f), out float slotButtonSize);
        DrawSlotGrid(context, gridRect, slotButtonSize, slots, accent);
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

    private bool DrawVerticalSlotList(GrayMechDrydockTabContext context, Rect listRect, List<GRMechResolvedSlot> slots)
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
            GRMechResolvedSlot resolvedSlot = slots[i];
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

            TooltipHandler.TipRegion(slotRect, GrayMechDrydockTabText.BuildSlotTooltip(context.Draft?.chassis, resolvedSlot, module));
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
        float slotButtonSize,
        List<GRMechResolvedSlot> slots,
        Color accent)
    {
        int activeSlotCount = Mathf.Min(GrayMechDrydockTabStyle.FixedDisplaySlotCount, slots.Count);
        for (int i = 0; i < GrayMechDrydockTabStyle.FixedDisplaySlotCount; i++)
        {
            int row = i / GrayMechDrydockTabStyle.SlotGridColumns;
            int column = i % GrayMechDrydockTabStyle.SlotGridColumns;
            float x = gridRect.x + column * (slotButtonSize + GrayMechDrydockTabStyle.SlotGridGap);
            float y = gridRect.y + row * (slotButtonSize + GrayMechDrydockTabStyle.SlotGridGap);
            Rect slotRect = new Rect(x, y, slotButtonSize, slotButtonSize);
            if (i < activeSlotCount && slots[i]?.slot != null)
            {
                GRMechResolvedSlot resolvedSlot = slots[i];
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

                TooltipHandler.TipRegion(slotRect, GrayMechDrydockTabText.BuildSlotTooltip(context.Draft?.chassis, resolvedSlot, module));
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

    internal void DrawQueue(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Widgets.DrawBoxSolid(rect.ContractedBy(1f), GrayMechDrydockTabStyle.BgPanel);
        DrawQueuePanel(context, rect.ContractedBy(8f));
    }

    internal void DrawSelection(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Widgets.DrawBoxSolid(rect.ContractedBy(1f), GrayMechDrydockTabStyle.BgPanel);
        Rect inner = rect.ContractedBy(8f);

        if (context.State.TryGetResolvedSlot(context.State.SelectedSectionSlot, context.State.SelectedSlotKey, out GRMechResolvedSlot resolvedSlot)
            && resolvedSlot.sectionSlot != null)
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
        y = DrawSummaryInfoRow(statsRect, y, "模块使用", $"{context.State.CachedFilledSlotCount} / {context.State.CachedTotalSlotCount}", GrayMechDrydockTabStyle.AuxiliaryColor);
        y = DrawSummaryInfoRow(statsRect, y, "电力消耗", BuildPowerSummary(context), GetPowerBudgetColor(context.State));
        y = DrawSummaryInfoRow(statsRect, y, "材料需求", context.State.CachedCostSummary.NullOrEmpty() ? "-" : context.State.CachedCostSummary, GrayMechDrydockTabStyle.SelectedColor, allowWrap: true);
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
        y = DrawSummaryInfoRow(statsRect, y, "ShieldEnergy".Translate(), BuildShieldSummary(context), GrayMechDrydockTabStyle.EngineColor);
        y = DrawSummaryInfoRow(statsRect, y, "WalkSpeedProperty".Translate(), context.State.CachedMoveSpeed.ToString("0.#"), GrayMechDrydockTabStyle.EngineColor);
        y = DrawSummaryInfoRow(statsRect, y, "Bandwidth".Translate(), context.State.CachedBandwidthCost.ToString("0.#"), new Color(0.74f, 0.52f, 0.95f));
        // y = DrawSummaryInfoRow(statsRect, y, "CooldownTime".Translate(), FormatDeltaPercent(1f - context.State.CachedRangedCooldownFactor), GrayMechDrydockTabStyle.MainWeaponColor);
        // y = DrawSummaryInfoRow(statsRect, y, "Accuracy", BuildAccuracySummary(context), GrayMechDrydockTabStyle.SelectedColor);
    }

    private void DrawQueuePanel(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFillMuted, GrayMechDrydockTabStyle.EngineColor);
        GrayMechDrydockTabState state = context.State;
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
        GrayMechDrydockTabState state = context.State;
        y = DrawSummaryInfoRow(areaRect, y, "ArmorSharp".Translate(), state.CachedArmorSharp.ToString("0.##"), GrayMechDrydockTabStyle.MainWeaponColor);
        y = DrawSummaryInfoRow(areaRect, y, "ArmorBlunt".Translate(), state.CachedArmorBlunt.ToString("0.##"), GrayMechDrydockTabStyle.MainWeaponColor);
        y = DrawSummaryInfoRow(areaRect, y, "ArmorHeat".Translate(), state.CachedArmorHeat.ToString("0.##"), GrayMechDrydockTabStyle.MainWeaponColor);
        return y;
    }

    private static float DrawCombatExtendedArmorRows(Rect areaRect, float y, GrayMechDrydockTabContext context)
    {
        // 为 CE 预留的
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

    private static string BuildPowerSummary(GrayMechDrydockTabContext context)
    {
        GrayMechDrydockTabState state = context.State;
        string net = state.CachedPowerNet > 0 ? "+" + state.CachedPowerNet : state.CachedPowerNet.ToString();
        return net + "  (" + state.CachedPowerGeneration + " / " + state.CachedPowerConsumption + ")";
    }

    private static Color GetPowerBudgetColor(GrayMechDrydockTabState state)
    {
        return state.CachedPowerNet >= 0 ? GrayMechDrydockTabStyle.ReadyColor : GrayMechDrydockTabStyle.LockedColor;
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

    private void DrawSlotFocusPanel(GrayMechDrydockTabContext context, GRMechResolvedSlot resolvedSlot, Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFill, GrayMechDrydockTabStyle.HullOutline);
        Rect inner = rect.ContractedBy(8f);
        Color accent = GrayMechDrydockTabText.GetSectionAccentColor(resolvedSlot.sectionSlot);

        GrayMechDesignUtility.TryGetSelectedModule(context.Draft, resolvedSlot, out GRMechModuleDef currentModule);
        Rect slotIconRect = new Rect(inner.x, inner.y + 2f, 42f, 42f);
        canvasRenderer.DrawModuleIconTile(slotIconRect, currentModule, resolvedSlot.slot);

        string slotDisplayName = GrayMechDrydockTabText.GetSlotDisplayName(resolvedSlot.slot);
        string titleText = slotDisplayName.NullOrEmpty() ? string.Empty : "<b>" + slotDisplayName + "</b>";
        string metaText = GrayMechDrydockTabText.GetSlotOwnerLabel(resolvedSlot) + "  |  " + GrayMechDrydockTabText.BuildSlotTypeSummary(resolvedSlot.slot);
        float headerX = slotIconRect.xMax + 8f;
        float headerWidth = inner.width - slotIconRect.width - 8f;
        float titleHeight = titleText.NullOrEmpty() ? 0f : GrayMechDrydockTabText.MeasureWrappedTextHeight(titleText, headerWidth, GameFont.Small);
        float metaHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(metaText, headerWidth, GameFont.Small);
        float headerY = inner.y;
        if (!titleText.NullOrEmpty())
        {
            GrayMechDrydockTabText.DrawWrappedLabel(new Rect(headerX, headerY, headerWidth, titleHeight), titleText);
            headerY += titleHeight + 2f;
        }

        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(headerX, headerY, headerWidth, metaHeight), metaText);
        headerY = Mathf.Max(slotIconRect.yMax, headerY + metaHeight) + 8f;

        GrayMechDrydockTabState state = context.State;
        Rect toggleRect = new Rect(inner.x, headerY, inner.width, 24f);
        bool showObsolete = state.ShowObsoleteModules;
        Widgets.CheckboxLabeled(toggleRect, "显示淘汰部件", ref showObsolete);
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

    private void DrawModuleOptionRow(GrayMechDrydockTabContext context, Rect rowRect, GRMechResolvedSlot resolvedSlot, GRMechModuleDef module, bool selected, Color accent)
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
        string cost = module == null ? "Cost: None" : GrayMechDrydockTabText.BuildModuleCostSummary(module, context.Draft?.chassis, context.State.TextBuilder);

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
        float contentHeight = GrayMechDrydockTabStyle.LibraryHeaderHeight
                              + GrayMechDrydockTabStyle.LibraryHeaderGap
                              + context.State.CachedLibraryMaxCardHeight;
        Rect newDesignRect = new Rect(inner.x, inner.y + 4f, GrayMechDrydockTabStyle.LibraryNewDesignWidth, contentHeight);
        float groupAreaX = newDesignRect.xMax + GrayMechDrydockTabStyle.LibraryNewDesignGap;
        Rect groupScrollRect = new Rect(
            groupAreaX,
            inner.y + 4f,
            Mathf.Max(0f, inner.xMax - groupAreaX),
            Mathf.Max(0f, inner.height - 4f));

        DrawNewDesignButton(context, newDesignRect);
        if (groupScrollRect.width > 0f)
        {
            DrawLibraryGroups(context, groupScrollRect, contentHeight);
        }
    }

    private void DrawNewDesignButton(GrayMechDrydockTabContext context, Rect rect)
    {
        GrayMechDrydockTabState state = context.State;
        bool hasUnlockedChassis = state.ChassisCache.Count > 0;
        Color accent = hasUnlockedChassis ? GrayMechDrydockTabStyle.UtilitySlotColor : GrayMechDrydockTabStyle.LockedColor;
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFillMuted, accent);
        Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 4f), accent);
        if (Mouse.IsOver(rect))
        {
            Widgets.DrawHighlight(rect);
        }

        if (Widgets.ButtonInvisible(rect))
        {
            context.Controller.OpenNewDesignMenu(context.Dock, state.ChassisCache);
        }

        Rect titleRect = new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height);
        DrawLibraryTextLine(titleRect, "新设计", GameFont.Small, TextAnchor.MiddleCenter, hasUnlockedChassis ? Color.white : Color.gray);
    }

    private void DrawLibraryGroups(GrayMechDrydockTabContext context, Rect rect, float contentHeight)
    {
        GrayMechDrydockTabState state = context.State;
        float contentWidth = 0f;
        for (int i = 0; i < state.LibraryGroups.Count; i++)
        {
            float groupWidth = state.GetLibraryGroupWidth(state.LibraryGroups[i]);
            if (groupWidth <= 0f)
            {
                continue;
            }

            if (contentWidth > 0f)
            {
                contentWidth += GrayMechDrydockTabStyle.LibraryGroupGap;
            }

            contentWidth += groupWidth;
        }

        Rect viewRect = new Rect(
            0f,
            0f,
            Mathf.Max(rect.width - 16f, contentWidth),
            Mathf.Max(rect.height - 16f, contentHeight));

        if (viewRect.width > rect.width)
        {
            Widgets.ScrollHorizontal(rect, ref state.LibraryScrollPosition, viewRect);
        }

        Widgets.BeginScrollView(rect, ref state.LibraryScrollPosition, viewRect);
        float x = 0f;
        float minVisibleX = state.LibraryScrollPosition.x - GrayMechDrydockTabStyle.LibraryCardWidth - 32f;
        float maxVisibleX = state.LibraryScrollPosition.x + rect.width + GrayMechDrydockTabStyle.LibraryCardWidth + 32f;
        for (int i = 0; i < state.LibraryGroups.Count; i++)
        {
            GrayMechLibraryGroup group = state.LibraryGroups[i];
            float groupWidth = state.GetLibraryGroupWidth(group);
            if (groupWidth <= 0f)
            {
                continue;
            }

            Rect groupRect = new Rect(x, 0f, groupWidth, contentHeight);
            if (groupRect.xMax >= minVisibleX && groupRect.x <= maxVisibleX)
            {
                DrawLibraryGroup(context, group, groupRect, i > 0);
            }

            x += groupWidth + GrayMechDrydockTabStyle.LibraryGroupGap;
        }

        Widgets.EndScrollView();
    }

    private void DrawLibraryGroup(GrayMechDrydockTabContext context, GrayMechLibraryGroup group, Rect rect, bool drawLeadingSeparator)
    {
        Color separatorColor = new Color(
            GrayMechDrydockTabStyle.HeaderLineColor.r,
            GrayMechDrydockTabStyle.HeaderLineColor.g,
            GrayMechDrydockTabStyle.HeaderLineColor.b,
            0.48f);
        if (drawLeadingSeparator)
        {
            float separatorX = rect.x - GrayMechDrydockTabStyle.LibraryGroupGap * 0.5f;
            Widgets.DrawBoxSolid(new Rect(separatorX, rect.y + 4f, 1f, rect.height - 8f), separatorColor);
        }

        Rect headerRect = new Rect(rect.x, rect.y, rect.width, GrayMechDrydockTabStyle.LibraryHeaderHeight);
        DrawLibraryGroupHeader(headerRect, group.Chassis?.LabelCap.ToString() ?? "Unknown");

        float cardX = rect.x;
        float cardY = headerRect.yMax + GrayMechDrydockTabStyle.LibraryHeaderGap;
        float cardHeight = context.State.CachedLibraryMaxCardHeight;
        if (group.ShowChassisCard)
        {
            Rect chassisCardRect = new Rect(cardX, cardY, GrayMechDrydockTabStyle.LibraryCardWidth, cardHeight);
            DrawChassisCard(context, chassisCardRect, group.Chassis, selected: false);
            return;
        }

        for (int i = 0; i < group.Designs.Count; i++)
        {
            GrayMechDesignRecord design = group.Designs[i];
            Rect cardRect = new Rect(cardX, cardY, GrayMechDrydockTabStyle.LibraryCardWidth, cardHeight);
            bool selected = context.Dock.IsEditingSavedDesign && context.Dock.EditingDesignId == design.id;
            DrawDesignCard(context, cardRect, design, selected);
            cardX += GrayMechDrydockTabStyle.LibraryCardWidth + GrayMechDrydockTabStyle.LibraryCardGap;
        }
    }

    private static void DrawLibraryGroupHeader(Rect rect, string label)
    {
        DrawLibraryTextLine(rect, label, GameFont.Small, TextAnchor.MiddleLeft, Color.white);
        Widgets.DrawBoxSolid(
            new Rect(rect.x, rect.yMax - 2f, rect.width, 2f),
            new Color(
                GrayMechDrydockTabStyle.HeaderLineColor.r,
                GrayMechDrydockTabStyle.HeaderLineColor.g,
                GrayMechDrydockTabStyle.HeaderLineColor.b,
                0.55f));
    }

    private static void DrawLibraryTextLine(Rect rect, string text, GameFont font, TextAnchor anchor, Color color)
    {
        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        Color oldColor = GUI.color;
        bool oldWrap = Text.WordWrap;

        Text.Anchor = anchor;
        Text.Font = font;
        Text.WordWrap = false;
        GUI.color = color;
        Widgets.Label(rect, (text ?? string.Empty).Truncate(rect.width));

        GUI.color = oldColor;
        Text.WordWrap = oldWrap;
        Text.Font = oldFont;
        Text.Anchor = oldAnchor;
    }

    private void DrawChassisCard(GrayMechDrydockTabContext context, Rect rect, GRMechChassisDef chassis, bool selected)
    {
        bool available = GrayMechDesignUtility.IsResearchAvailable(chassis);
        bool buildableHere = context.Dock.CanBuildChassis(chassis, out string restrictionReason);
        bool canLoad = available && buildableHere;
        Color accent = canLoad ? GrayMechDrydockTabStyle.UtilitySlotColor : GrayMechDrydockTabStyle.LockedColor;
        DrawLibraryCardChrome(rect, selected, accent);

        if (Widgets.ButtonInvisible(rect))
        {
            if (canLoad)
            {
                context.Controller.LoadChassis(context.Dock, chassis);
            }
            else
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
            }
        }

        string title = chassis?.LabelCap.ToString() ?? "Unnamed";
        string footer = buildableHere ? "开始新设计" : "此处不可用";
        float titleHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight("<b>" + title + "</b>", rect.width - 12f, GameFont.Small);
        float footerHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(footer, rect.width - 12f, GameFont.Tiny);
        Rect titleRect = new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, titleHeight);
        Rect previewRect = new Rect(rect.x + 4f, titleRect.yMax + 2f, rect.width - 8f, 36f);
        Rect footerRect = new Rect(rect.x + 6f, previewRect.yMax + 2f, rect.width - 12f, footerHeight);
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GrayMechDrydockTabText.DrawWrappedLabel(titleRect, "<b>" + title + "</b>");
        DrawChassisPreview(previewRect, chassis);
        Text.Font = GameFont.Tiny;
        Color oldColor = GUI.color;
        GUI.color = new Color(0.82f, 0.9f, 0.94f, 0.92f);
        GrayMechDrydockTabText.DrawWrappedLabel(footerRect, footer);
        GUI.color = oldColor;
        Text.Font = oldFont;
        string tooltip = chassis?.description ?? string.Empty;
        if (!buildableHere && !restrictionReason.NullOrEmpty())
        {
            tooltip = tooltip.NullOrEmpty() ? restrictionReason : tooltip + "\n\n" + restrictionReason;
        }
        TooltipHandler.TipRegion(rect, tooltip);
    }

    private void DrawDesignCard(GrayMechDrydockTabContext context, Rect rect, GrayMechDesignRecord design, bool selected)
    {
        GrayMechDesignSnapshot snapshot = design?.snapshot;
        bool buildableHere = context.Dock.CanBuildChassis(snapshot?.chassis, out string restrictionReason);
        Color accent = buildableHere ? GrayMechDrydockTabStyle.UtilitySlotColor : GrayMechDrydockTabStyle.LockedColor;
        DrawLibraryCardChrome(rect, selected, accent);

        if (Widgets.ButtonInvisible(rect))
        {
            if (buildableHere)
            {
                context.Controller.LoadSavedDesign(context.Dock, design);
            }
            else
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
            }
        }

        float headerHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight("Saved", rect.width - 12f, GameFont.Tiny);
        Rect previewRect = new Rect(rect.x + 4f, rect.y + 4f + headerHeight + 2f, rect.width - 8f, 36f);
        DrawChassisPreview(previewRect, snapshot?.chassis);
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, headerHeight), "Saved");
        Text.Font = oldFont;

        string title = design?.label ?? "Unnamed";
        string footer = buildableHere
            ? (snapshot?.chassis?.LabelCap.ToString() ?? "No chassis") + "   Modules " + CountInstalledModules(snapshot)
            : "此处不可用";
        float titleHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight("<b>" + title + "</b>", rect.width - 12f, GameFont.Small);
        float footerHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(footer, rect.width - 12f, GameFont.Tiny);
        float textY = previewRect.yMax + 2f;
        Color oldColor = GUI.color;
        GUI.color = selected ? GrayMechDrydockTabStyle.SelectedColor : new Color(1f, 0.8f, 0.35f);
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(rect.x + 6f, textY, rect.width - 12f, titleHeight), "<b>" + title + "</b>");
        GUI.color = oldColor;
        textY += titleHeight + 2f;
        oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        GrayMechDrydockTabText.DrawWrappedLabel(new Rect(rect.x + 6f, textY, rect.width - 12f, footerHeight), footer);
        Text.Font = oldFont;
        string tooltip = GrayMechDrydockTabText.BuildDesignTooltip(design);
        if (!buildableHere && !restrictionReason.NullOrEmpty())
        {
            tooltip = tooltip.NullOrEmpty() ? restrictionReason : tooltip + "\n\n" + restrictionReason;
        }
        TooltipHandler.TipRegion(rect, tooltip);
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
