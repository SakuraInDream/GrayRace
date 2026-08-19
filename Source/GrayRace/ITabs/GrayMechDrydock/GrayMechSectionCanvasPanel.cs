using System.Collections.Generic;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

internal sealed class GrayMechSectionCanvasPanel
{
    private const int AnchoredSectionColumnCount = 3;
    private const float PreferredSectionBandInset = 32f;
    private const float MinSectionBandInset = 8f;
    private const float TopMargin = 12f;
    private const float BottomMargin = 12f;
    private const float SectionGap = 14f;
    private const float BandGap = 8f;
    private const float PreviewGap = 18f;
    private const float MinimumPreviewHeight = 120f;
    private const float ScrollbarWidth = 16f;
    private const float CorePanelSideMargin = 4f;
    private const float CorePanelTopBottomMargin = 72f;
    private const float MaxCoreSlotSize = 44f;
    private const float MinCoreSlotSize = 18f;
    private const float MaxCoreRowGap = 6f;
    private const float CorePickerGap = 12f;
    private const float CorePickerOuterPadding = 6f;
    private const float CorePickerTileGap = 4f;
    private const float MaxCorePickerTileSize = 42f;
    private const float MinCorePickerTileSize = 28f;
    private const int MaxCorePickerColumns = 6;

    private readonly GrayMechDrydockCanvasRenderer canvasRenderer = new();
    private readonly GRMechResolvedSlot[] slotGridBuffer;
    private Vector2 scrollPosition = Vector2.zero;

    internal GrayMechSectionCanvasPanel()
    {
        slotGridBuffer = new GRMechResolvedSlot[GrayMechSectionCanvasMetrics.GetMaximumConfiguredCellCount()];
    }

    internal void Draw(Rect canvasRect, IGrayMechSectionCanvasHost host)
    {
        if (!host.TryGetSectionSlots(out List<GRMechSectionSlotDef> sectionSlots))
        {
            sectionSlots = null;
        }

        int regularSectionCount = CountVisibleSections(sectionSlots);
        SectionCanvasLayout layout = CalculateLayout(canvasRect.width, host, sectionSlots, regularSectionCount);
        if (layout.RequiredHeight <= canvasRect.height)
        {
            scrollPosition = Vector2.zero;
            DrawContent(canvasRect, host, sectionSlots, regularSectionCount, layout);
        }
        else
        {
            float viewWidth = Mathf.Max(1f, canvasRect.width - ScrollbarWidth);
            layout = CalculateLayout(viewWidth, host, sectionSlots, regularSectionCount);
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(canvasRect.height, layout.RequiredHeight));
            Widgets.BeginScrollView(canvasRect, ref scrollPosition, viewRect);
            DrawContent(viewRect, host, sectionSlots, regularSectionCount, layout);
            Widgets.EndScrollView();
        }

        if (host.AllowSecondarySlotAction && WasSecondaryClick(canvasRect))
        {
            host.OnSlotSecondaryActivated(null);
        }
    }

    private void DrawContent(
        Rect canvasRect,
        IGrayMechSectionCanvasHost host,
        List<GRMechSectionSlotDef> sectionSlots,
        int regularSectionCount,
        SectionCanvasLayout layout)
    {
        canvasRenderer.DrawSlotBay(canvasRect);
        canvasRenderer.DrawCanvasFrame(canvasRect);

        Rect headerBandRect = new Rect(canvasRect.x + layout.SectionBandInset, canvasRect.y + TopMargin, layout.SectionBandWidth, layout.HeaderHeight);
        Rect topSlotBandRect = new Rect(headerBandRect.x, headerBandRect.yMax + BandGap, headerBandRect.width, layout.TopAreaHeight);
        Rect bottomSlotBandRect = new Rect(headerBandRect.x, canvasRect.yMax - BottomMargin - layout.BottomAreaHeight, headerBandRect.width, layout.BottomAreaHeight);

        float previewY = topSlotBandRect.yMax + PreviewGap;
        float previewBottom = bottomSlotBandRect.y - PreviewGap;
        float previewHeight = Mathf.Max(MinimumPreviewHeight, previewBottom - previewY);
        float previewWidth = Mathf.Min(headerBandRect.width * 0.46f, 360f);
        Rect previewRect = new Rect(headerBandRect.center.x - previewWidth * 0.5f, previewY, previewWidth, previewHeight);
        canvasRenderer.DrawShipPreview(previewRect, host.Snapshot);

        if (sectionSlots != null && regularSectionCount > 0)
        {
            float totalColumnsWidth = regularSectionCount * layout.ColumnWidth + Mathf.Max(0, regularSectionCount - 1) * SectionGap;
            float bandStartX = headerBandRect.x + (headerBandRect.width - totalColumnsWidth) * 0.5f;
            int visibleIndex = 0;
            for (int i = 0; i < sectionSlots.Count; i++)
            {
                GRMechSectionSlotDef sectionSlot = sectionSlots[i];
                if (sectionSlot == null)
                {
                    continue;
                }

                host.PrepareSectionSlots(sectionSlot);
                float columnX = bandStartX + visibleIndex * (layout.ColumnWidth + SectionGap);
                Rect headerRect = new Rect(columnX, headerBandRect.y, layout.ColumnWidth, headerBandRect.height);
                Rect topAreaRect = new Rect(columnX, topSlotBandRect.y, layout.ColumnWidth, topSlotBandRect.height);
                Rect bottomAreaRect = new Rect(columnX, bottomSlotBandRect.y, layout.ColumnWidth, bottomSlotBandRect.height);
                DrawSectionColumn(host, sectionSlot, headerRect, topAreaRect, bottomAreaRect, layout.SlotButtonSize);
                visibleIndex++;
            }
        }

        host.PrepareCoreSlots();
        if (host.CoreSlots != null && host.CoreSlots.Count > 0)
        {
            Rect corePanelRect = GetCoreSystemsPanelRect(canvasRect, previewRect, host.CoreSlots.Count);
            DrawCoreSystemsColumn(host, canvasRect, corePanelRect);
        }
    }

    private void DrawSectionColumn(
        IGrayMechSectionCanvasHost host,
        GRMechSectionSlotDef sectionSlot,
        Rect headerRect,
        Rect topAreaRect,
        Rect bottomAreaRect,
        float slotButtonSize)
    {
        Color accent = GrayMechDrydockTabText.GetSectionAccentColor(sectionSlot);
        DrawSectionHeader(host, sectionSlot, headerRect, accent);
        DrawSlotArea(host, topAreaRect, slotButtonSize, host.TopSlots, GrayMechDrydockTabStyle.MainWeaponColor);
        DrawSlotArea(host, bottomAreaRect, slotButtonSize, host.BottomSlots, GrayMechDrydockTabStyle.AuxiliaryColor);
    }

    private void DrawSectionHeader(IGrayMechSectionCanvasHost host, GRMechSectionSlotDef sectionSlot, Rect headerRect, Color accent)
    {
        bool selected = host.IsSectionSelected(sectionSlot);
        bool ownsSelectedSlot = host.SectionOwnsSelectedSlot(sectionSlot);
        bool hovered = host.AllowSectionInteraction && Mouse.IsOver(headerRect);

        Color outline = selected ? GrayMechDrydockTabStyle.SelectedColor : (ownsSelectedSlot ? accent : new Color(accent.r, accent.g, accent.b, 0.65f));
        Color fill = selected
            ? new Color(GrayMechDrydockTabStyle.SelectedColor.r, GrayMechDrydockTabStyle.SelectedColor.g, GrayMechDrydockTabStyle.SelectedColor.b, 0.12f)
            : new Color(accent.r, accent.g, accent.b, hovered ? 0.08f : 0.04f);
        Widgets.DrawBoxSolidWithOutline(headerRect, fill, outline, selected ? 2 : 1);
        Widgets.DrawBoxSolid(new Rect(headerRect.x + 2f, headerRect.y + 2f, headerRect.width - 4f, 4f), accent);
        if (host.AllowSectionInteraction && Widgets.ButtonInvisible(headerRect))
        {
            host.OnSectionHeaderActivated(sectionSlot);
        }

        GrayMechDrydockTabText.DrawWrappedLabelCentered(
            new Rect(headerRect.x + 8f, headerRect.y + 5f, headerRect.width - 16f, headerRect.height - 8f),
            "<b>" + host.GetSectionHeaderText(sectionSlot) + "</b>");
    }

    private void DrawSlotArea(IGrayMechSectionCanvasHost host, Rect areaRect, float slotButtonSize, List<GRMechResolvedSlot> slots, Color accent)
    {
        Widgets.DrawBoxSolidWithOutline(areaRect, new Color(accent.r, accent.g, accent.b, 0.035f), new Color(accent.r, accent.g, accent.b, 0.28f));
        Widgets.DrawBoxSolid(new Rect(areaRect.x + 2f, areaRect.y + 2f, areaRect.width - 4f, 4f), new Color(accent.r, accent.g, accent.b, 0.75f));
        int displayCellCount = GrayMechSectionCanvasMetrics.GetDisplayCellCount(slots?.Count ?? 0);
        int rowCount = displayCellCount / GrayMechDrydockTabStyle.SlotGridColumns;
        Rect gridRect = GetSlotGridRect(areaRect.ContractedBy(8f), slotButtonSize, rowCount);
        DrawSlotGrid(host, gridRect, slotButtonSize, displayCellCount, slots, accent);
    }

    private void DrawCoreSystemsColumn(IGrayMechSectionCanvasHost host, Rect canvasRect, Rect panelRect)
    {
        Color accent = GrayMechDrydockTabStyle.UtilitySlotColor;
        bool hovered = host.AllowCoreBackgroundInteraction && Mouse.IsOver(panelRect);
        Color railColor = host.CoreOwnsSelectedSlot
            ? GrayMechDrydockTabStyle.SelectedColor
            : new Color(accent.r, accent.g, accent.b, hovered ? 0.55f : 0.26f);
        float railX = panelRect.center.x - 1f;
        Widgets.DrawBoxSolid(new Rect(railX, panelRect.y + 6f, 2f, panelRect.height - 12f), new Color(railColor.r, railColor.g, railColor.b, 0.28f));
        Widgets.DrawBoxSolid(new Rect(railX - 4f, panelRect.y + 2f, 10f, 2f), railColor);
        Widgets.DrawBoxSolid(new Rect(railX - 4f, panelRect.yMax - 4f, 10f, 2f), railColor);

        bool slotHovered = DrawVerticalSlotList(host, canvasRect, panelRect, host.CoreSlots);
        if (host.AllowCoreBackgroundInteraction && !slotHovered && host.CoreSlots.Count > 0 && Widgets.ButtonInvisible(panelRect))
        {
            host.OnCoreBackgroundActivated();
        }
    }

    private bool DrawVerticalSlotList(IGrayMechSectionCanvasHost host, Rect canvasRect, Rect listRect, List<GRMechResolvedSlot> slots)
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
        GRMechResolvedSlot pickerSlot = null;
        Rect pickerAnchorRect = default;
        List<GRMechModuleDef> pickerModules = null;
        GRMechModuleDef pickerCurrentModule = null;

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

            host.ResolveSlotVisuals(resolvedSlot, out GRMechModuleDef module, out string tooltip, out bool selected, out bool drawSlotMarker);
            canvasRenderer.DrawSlotWidget(slotRect, resolvedSlot.slot, module, selected, drawSlotMarker);
            if (host.AllowSecondarySlotAction && WasSecondaryClick(slotRect))
            {
                host.OnSlotSecondaryActivated(resolvedSlot);
            }
            else if (host.AllowSlotInteraction && Widgets.ButtonInvisible(slotRect))
            {
                host.OnSlotActivated(resolvedSlot);
            }

            if (!tooltip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(slotRect, tooltip);
            }

            if (selected && host.TryGetCoreSlotPickerData(resolvedSlot, out List<GRMechModuleDef> compatibleModules, out GRMechModuleDef currentModule))
            {
                pickerSlot = resolvedSlot;
                pickerAnchorRect = slotRect;
                pickerModules = compatibleModules;
                pickerCurrentModule = currentModule;
            }
        }

        bool pickerHovered = pickerSlot?.slot != null && DrawCoreSlotPicker(host, canvasRect, pickerAnchorRect, pickerSlot, pickerModules, pickerCurrentModule);
        return slotHovered || pickerHovered;
    }

    private void DrawSlotGrid(
        IGrayMechSectionCanvasHost host,
        Rect gridRect,
        float slotButtonSize,
        int displayCellCount,
        List<GRMechResolvedSlot> slots,
        Color accent)
    {
        BuildSlotGridBuffer(slots, displayCellCount);
        for (int i = 0; i < displayCellCount; i++)
        {
            int row = i / GrayMechDrydockTabStyle.SlotGridColumns;
            int column = i % GrayMechDrydockTabStyle.SlotGridColumns;
            float x = gridRect.x + column * (slotButtonSize + GrayMechDrydockTabStyle.SlotGridGap);
            float y = gridRect.y + row * (slotButtonSize + GrayMechDrydockTabStyle.SlotGridGap);
            Rect slotRect = new Rect(x, y, slotButtonSize, slotButtonSize);
            GRMechResolvedSlot resolvedSlot = slotGridBuffer[i];
            if (resolvedSlot?.slot != null)
            {
                host.ResolveSlotVisuals(resolvedSlot, out GRMechModuleDef module, out string tooltip, out bool selected, out bool drawSlotMarker);
                canvasRenderer.DrawSlotWidget(slotRect, resolvedSlot.slot, module, selected, drawSlotMarker);

                if (host.AllowSecondarySlotAction && WasSecondaryClick(slotRect))
                {
                    host.OnSlotSecondaryActivated(resolvedSlot);
                }
                else if (host.AllowSlotInteraction && Widgets.ButtonInvisible(slotRect))
                {
                    host.OnSlotActivated(resolvedSlot);
                }

                if (!tooltip.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(slotRect, tooltip);
                }
            }
            else
            {
                canvasRenderer.DrawDisabledSlotWidget(slotRect, accent);
            }
        }
    }

    private void BuildSlotGridBuffer(List<GRMechResolvedSlot> slots, int displayCellCount)
    {
        for (int i = 0; i < displayCellCount; i++)
        {
            slotGridBuffer[i] = null;
        }

        if (slots == null || slots.Count == 0)
        {
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            GRMechResolvedSlot resolvedSlot = slots[i];
            if (resolvedSlot?.slot == null)
            {
                continue;
            }

            int preferredIndex = GetPreferredGridCellIndex(resolvedSlot.slot.uiOrder);
            if ((uint)preferredIndex < displayCellCount && slotGridBuffer[preferredIndex] == null)
            {
                slotGridBuffer[preferredIndex] = resolvedSlot;
                continue;
            }

            int fallbackIndex = GetFirstEmptyGridCellIndex(displayCellCount);
            if (fallbackIndex < 0)
            {
                break;
            }

            slotGridBuffer[fallbackIndex] = resolvedSlot;
        }
    }

    private int GetFirstEmptyGridCellIndex(int displayCellCount)
    {
        for (int i = 0; i < displayCellCount; i++)
        {
            if (slotGridBuffer[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    private static int GetPreferredGridCellIndex(int uiOrder)
    {
        if (uiOrder < 0)
        {
            return -1;
        }

        return uiOrder;
    }

    private static SectionCanvasLayout CalculateLayout(
        float canvasWidth,
        IGrayMechSectionCanvasHost host,
        List<GRMechSectionSlotDef> sectionSlots,
        int regularSectionCount)
    {
        float sectionBandInset = GetSectionBandInset(canvasWidth, regularSectionCount);
        float sectionBandWidth = Mathf.Max(0f, canvasWidth - sectionBandInset * 2f);
        float headerHeight = GetSectionHeaderHeight(host, sectionSlots, sectionBandWidth);
        float columnWidth = GetSectionColumnWidth(sectionBandWidth, regularSectionCount);
        float slotButtonSize = GetSlotButtonSize(columnWidth);
        int topRowCount = GrayMechSectionCanvasMetrics.GetDisplayRowCount(host.MaximumTopSlotCount);
        int bottomRowCount = GrayMechSectionCanvasMetrics.GetDisplayRowCount(host.MaximumBottomSlotCount);
        float topAreaHeight = GetSlotAreaHeight(topRowCount, slotButtonSize);
        float bottomAreaHeight = GetSlotAreaHeight(bottomRowCount, slotButtonSize);
        float requiredHeight = TopMargin
                               + headerHeight
                               + BandGap
                               + topAreaHeight
                               + PreviewGap * 2f
                               + MinimumPreviewHeight
                               + bottomAreaHeight
                               + BottomMargin;
        return new SectionCanvasLayout(
            sectionBandInset,
            sectionBandWidth,
            headerHeight,
            columnWidth,
            slotButtonSize,
            topAreaHeight,
            bottomAreaHeight,
            requiredHeight);
    }

    private static float GetSectionHeaderHeight(IGrayMechSectionCanvasHost host, List<GRMechSectionSlotDef> sectionSlots, float totalWidth)
    {
        if (sectionSlots == null || sectionSlots.Count == 0)
        {
            return 28f;
        }

        int regularSectionCount = CountVisibleSections(sectionSlots);
        if (regularSectionCount == 0)
        {
            return 28f;
        }

        float columnWidth = GetSectionColumnWidth(totalWidth, regularSectionCount);
        float maxHeight = 0f;
        for (int i = 0; i < sectionSlots.Count; i++)
        {
            GRMechSectionSlotDef sectionSlot = sectionSlots[i];
            if (sectionSlot == null)
            {
                continue;
            }

            string headerText = "<b>" + host.GetSectionHeaderText(sectionSlot) + "</b>";
            maxHeight = Mathf.Max(maxHeight, GrayMechDrydockTabText.MeasureWrappedTextHeight(headerText, columnWidth - 16f, GameFont.Small));
        }

        return Mathf.Max(28f, maxHeight + 10f);
    }

    private static float GetSectionBandInset(float canvasWidth, int regularSectionCount)
    {
        if (regularSectionCount <= 0)
        {
            return PreferredSectionBandInset;
        }

        float requiredWidth = GetMinimumSectionBandWidth(regularSectionCount);
        float preferredWidth = canvasWidth - PreferredSectionBandInset * 2f;
        if (preferredWidth >= requiredWidth)
        {
            return PreferredSectionBandInset;
        }

        return Mathf.Clamp((canvasWidth - requiredWidth) * 0.5f, MinSectionBandInset, PreferredSectionBandInset);
    }

    private static float GetMinimumSectionBandWidth(int regularSectionCount)
    {
        int columnCount = GetSectionCanvasColumnCount(regularSectionCount);
        return GrayMechDrydockTabStyle.MinSectionColumnWidth * columnCount + Mathf.Max(0f, columnCount - 1) * SectionGap;
    }

    private static float GetSectionColumnWidth(float totalWidth, int regularSectionCount)
    {
        int columnCount = GetSectionCanvasColumnCount(regularSectionCount);
        float available = (totalWidth - Mathf.Max(0f, columnCount - 1) * SectionGap) / Mathf.Max(1, columnCount);
        return Mathf.Min(available, GrayMechDrydockTabStyle.MinSectionColumnWidth);
    }

    private static int GetSectionCanvasColumnCount(int regularSectionCount)
    {
        return Mathf.Max(1, regularSectionCount);
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

    private static Rect GetSlotGridRect(Rect areaRect, float slotButtonSize, int rowCount)
    {
        float width = GrayMechDrydockTabStyle.SlotGridColumns * slotButtonSize + (GrayMechDrydockTabStyle.SlotGridColumns - 1) * GrayMechDrydockTabStyle.SlotGridGap;
        float height = rowCount * slotButtonSize + Mathf.Max(0, rowCount - 1) * GrayMechDrydockTabStyle.SlotGridGap;
        float x = Mathf.Clamp(areaRect.center.x - width * 0.5f, areaRect.x, areaRect.xMax - width);
        float y = Mathf.Clamp(areaRect.center.y - height * 0.5f, areaRect.y, areaRect.yMax - height);
        return new Rect(x, y, width, height);
    }

    private static float GetSlotButtonSize(float columnWidth)
    {
        float innerWidth = Mathf.Max(1f, columnWidth - 16f);
        float widthPerSlot = (innerWidth - GrayMechDrydockTabStyle.SlotGridGap * (GrayMechDrydockTabStyle.SlotGridColumns - 1)) / GrayMechDrydockTabStyle.SlotGridColumns;
        float slotButtonSize = Mathf.Min(GrayMechDrydockTabStyle.SlotButtonSize, widthPerSlot);
        return Mathf.Max(1f, slotButtonSize);
    }

    private static float GetSlotAreaHeight(int rowCount, float slotButtonSize)
    {
        return rowCount * slotButtonSize
               + Mathf.Max(0, rowCount - 1) * GrayMechDrydockTabStyle.SlotGridGap
               + 16f;
    }

    private static Rect GetCoreSystemsPanelRect(Rect canvasRect, Rect previewRect, int slotCount)
    {
        float availableHeight = Mathf.Max(40f, canvasRect.height - CorePanelTopBottomMargin * 2f);
        GetCoreSlotMetrics(availableHeight, slotCount, out float coreSlotSize, out float rowGap);
        float listHeight = GetVerticalSlotListHeight(slotCount, coreSlotSize, rowGap);
        float panelHeight = Mathf.Max(listHeight + 6f, coreSlotSize + 6f);
        float panelWidth = coreSlotSize + 2f;
        float x = canvasRect.xMax - CorePanelSideMargin - panelWidth;
        float y = Mathf.Clamp(previewRect.center.y - panelHeight * 0.5f, canvasRect.y + CorePanelTopBottomMargin, canvasRect.yMax - panelHeight - CorePanelTopBottomMargin);
        if (slotCount == 0)
        {
            y = previewRect.center.y - panelHeight * 0.5f;
        }

        y = Mathf.Clamp(y, canvasRect.y + CorePanelTopBottomMargin, canvasRect.yMax - panelHeight - CorePanelTopBottomMargin);
        return new Rect(x, y, panelWidth, panelHeight);
    }

    private static void GetCoreSlotMetrics(float availableHeight, int slotCount, out float coreSlotSize, out float rowGap)
    {
        if (slotCount <= 0)
        {
            coreSlotSize = MaxCoreSlotSize;
            rowGap = 0f;
            return;
        }

        if (slotCount == 1)
        {
            coreSlotSize = Mathf.Min(MaxCoreSlotSize, availableHeight);
            rowGap = 0f;
            return;
        }

        float rawSlotSize = (availableHeight - MaxCoreRowGap * (slotCount - 1)) / slotCount;
        coreSlotSize = Mathf.Min(MaxCoreSlotSize, rawSlotSize);
        coreSlotSize = Mathf.Max(MinCoreSlotSize, coreSlotSize);

        float remainingHeight = Mathf.Max(0f, availableHeight - coreSlotSize * slotCount);
        rowGap = Mathf.Min(MaxCoreRowGap, remainingHeight / (slotCount - 1));
    }

    private static float GetVerticalSlotListHeight(int slotCount, float coreSlotSize, float rowGap)
    {
        if (slotCount <= 0)
        {
            return 0f;
        }

        return slotCount * coreSlotSize + Mathf.Max(0, slotCount - 1) * rowGap;
    }

    private bool DrawCoreSlotPicker(
        IGrayMechSectionCanvasHost host,
        Rect canvasRect,
        Rect anchorRect,
        GRMechResolvedSlot resolvedSlot,
        List<GRMechModuleDef> compatibleModules,
        GRMechModuleDef currentModule)
    {
        int moduleCount = compatibleModules?.Count ?? 0;
        if (moduleCount <= 0)
        {
            return false;
        }

        BuildPickerGroups(compatibleModules);
        int groupCount = pickerGroupBuffer.Count;
        if (groupCount <= 0)
        {
            return false;
        }

        float tileSize = Mathf.Clamp(anchorRect.width + 8f, MinCorePickerTileSize, MaxCorePickerTileSize);

        int maxItemsInRow = 0;
        int totalRows = 0;
        for (int g = 0; g < groupCount; g++)
        {
            int count = pickerGroupBuffer[g].count;
            maxItemsInRow = Mathf.Max(maxItemsInRow, count);
            totalRows += Mathf.CeilToInt((float)count / MaxCorePickerColumns);
        }

        int columnCount = Mathf.Min(maxItemsInRow, MaxCorePickerColumns);
        float groupGap = CorePickerTileGap * 2f;
        float trayWidth = CorePickerOuterPadding * 2f + columnCount * tileSize + Mathf.Max(0, columnCount - 1) * CorePickerTileGap;
        float trayHeight = CorePickerOuterPadding * 2f + totalRows * tileSize + Mathf.Max(0, totalRows - 1) * CorePickerTileGap + Mathf.Max(0, groupCount - 1) * groupGap;
        float minX = canvasRect.x + 8f;
        float maxX = Mathf.Max(minX, anchorRect.xMin - 4f);
        float x = Mathf.Clamp(anchorRect.xMin - CorePickerGap - trayWidth, minX, maxX);
        float y = Mathf.Clamp(anchorRect.center.y - trayHeight * 0.5f, canvasRect.y + 8f, canvasRect.yMax - trayHeight - 8f);
        Rect trayRect = new(x, y, trayWidth, trayHeight);
        Color accent = GrayMechDrydockTabStyle.GetSlotColor(resolvedSlot.slot);

        Widgets.DrawBoxSolidWithOutline(
            trayRect,
            new Color(GrayMechDrydockTabStyle.CardFill.r, GrayMechDrydockTabStyle.CardFill.g, GrayMechDrydockTabStyle.CardFill.b, 0.94f),
            new Color(accent.r, accent.g, accent.b, 0.48f));
        Widgets.DrawBoxSolid(new Rect(trayRect.x + 2f, trayRect.y + 2f, trayRect.width - 4f, 3f), accent);

        float connectorY = Mathf.Clamp(anchorRect.center.y, trayRect.y + 8f, trayRect.yMax - 8f);
        Widgets.DrawLine(new Vector2(trayRect.xMax, connectorY), new Vector2(anchorRect.xMin - 4f, anchorRect.center.y), new Color(accent.r, accent.g, accent.b, 0.52f), 2f);

        bool pickerHovered = Mouse.IsOver(trayRect);
        float trayRight = trayRect.xMax - CorePickerOuterPadding;
        float currentY = trayRect.y + CorePickerOuterPadding;

        for (int g = 0; g < groupCount; g++)
        {
            PickerGroupSpan group = pickerGroupBuffer[g];
            int subRowCount = Mathf.CeilToInt((float)group.count / columnCount);

            for (int subRow = 0; subRow < subRowCount; subRow++)
            {
                int subRowStart = subRow * columnCount;
                int itemsInSubRow = Mathf.Min(columnCount, group.count - subRowStart);

                for (int col = 0; col < itemsInSubRow; col++)
                {
                    int moduleIndex = group.startIndex + subRowStart + col;
                    GRMechModuleDef module = pickerSortBuffer[moduleIndex];

                    int fromRight = itemsInSubRow - 1 - col;
                    float tileX = trayRight - tileSize - fromRight * (tileSize + CorePickerTileGap);
                    Rect optionRect = new(tileX, currentY, tileSize, tileSize);
                    bool selected = currentModule == module;

                    canvasRenderer.DrawInlineModuleOption(optionRect, resolvedSlot.slot, module, selected);
                    if (host.AllowSlotInteraction && Widgets.ButtonInvisible(optionRect))
                    {
                        host.OnCoreSlotModuleActivated(resolvedSlot, module);
                    }

                    TooltipHandler.TipRegion(optionRect, module?.LabelCap.ToString() ?? module?.defName ?? string.Empty);
                }

                currentY += tileSize + CorePickerTileGap;
            }

            if (g < groupCount - 1)
            {
                currentY += groupGap;
            }
        }

        return pickerHovered;
    }

    private static readonly List<GRMechModuleDef> pickerSortBuffer = new();
    private static readonly List<PickerGroupSpan> pickerGroupBuffer = new();

    private static void BuildPickerGroups(List<GRMechModuleDef> modules)
    {
        pickerSortBuffer.Clear();
        pickerGroupBuffer.Clear();
        if (modules == null || modules.Count == 0)
        {
            return;
        }

        for (int i = 0; i < modules.Count; i++)
        {
            pickerSortBuffer.Add(modules[i]);
        }

        pickerSortBuffer.Sort(CompareModulesForPicker);

        pickerGroupBuffer.Add(new PickerGroupSpan(0, pickerSortBuffer.Count));
    }

    private static int CompareModulesForPicker(GRMechModuleDef a, GRMechModuleDef b)
    {
        return (a?.uiOrder ?? 0).CompareTo(b?.uiOrder ?? 0);
    }

    private readonly struct PickerGroupSpan
    {
        public readonly int startIndex;
        public readonly int count;

        public PickerGroupSpan(int startIndex, int count)
        {
            this.startIndex = startIndex;
            this.count = count;
        }
    }

    private readonly struct SectionCanvasLayout
    {
        internal readonly float SectionBandInset;
        internal readonly float SectionBandWidth;
        internal readonly float HeaderHeight;
        internal readonly float ColumnWidth;
        internal readonly float SlotButtonSize;
        internal readonly float TopAreaHeight;
        internal readonly float BottomAreaHeight;
        internal readonly float RequiredHeight;

        internal SectionCanvasLayout(
            float sectionBandInset,
            float sectionBandWidth,
            float headerHeight,
            float columnWidth,
            float slotButtonSize,
            float topAreaHeight,
            float bottomAreaHeight,
            float requiredHeight)
        {
            SectionBandInset = sectionBandInset;
            SectionBandWidth = sectionBandWidth;
            HeaderHeight = headerHeight;
            ColumnWidth = columnWidth;
            SlotButtonSize = slotButtonSize;
            TopAreaHeight = topAreaHeight;
            BottomAreaHeight = bottomAreaHeight;
            RequiredHeight = requiredHeight;
        }
    }

    private static int CountVisibleSections(List<GRMechSectionSlotDef> sectionSlots)
    {
        if (sectionSlots == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < sectionSlots.Count; i++)
        {
            if (sectionSlots[i] != null)
            {
                count++;
            }
        }

        return count;
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
}

