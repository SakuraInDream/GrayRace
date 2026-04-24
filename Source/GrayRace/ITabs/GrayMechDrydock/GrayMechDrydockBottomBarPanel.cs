using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.ITabs;

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
        GrayMechDrydockPresenter state = context.State;
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
        GrayMechDrydockPresenter state = context.State;
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
