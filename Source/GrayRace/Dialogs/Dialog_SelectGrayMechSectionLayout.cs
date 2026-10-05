using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.ITabs;
using SD.GrayRace.Mechs;
using SD.GrayRace.ThingClasses;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.Dialogs;

public class Dialog_SelectGrayMechSectionLayout : Window
{
    private const int ColumnCount = 3;
    private const float TitleHeight = 34f;
    private const float CardHeight = 142f;
    private const float CardGap = 10f;
    private const float CardPadding = 8f;
    private const float RowLabelWidth = 58f;
    private const float SlotIconSize = 32f;
    private const float SlotQuantityGap = 3f;
    private const float SlotQuantityWidth = 28f;
    private const float SlotIconGap = 6f;

    private static readonly Color LockedTextColor = new(0.62f, 0.62f, 0.62f);

    private readonly Building_GR_Drydock dock;
    private readonly GRMechSectionSlotDef sectionSlot;
    private readonly Action<Building_GR_Drydock, GRMechSectionSlotDef, GRMechSectionLayoutDef> onSelected;
    private readonly LayoutOption[] options;
    private readonly string title;
    private Vector2 scrollPosition = Vector2.zero;

    public override Vector2 InitialSize => new(1120f, 560f);

    internal Dialog_SelectGrayMechSectionLayout(
        Building_GR_Drydock dock,
        GRMechSectionSlotDef sectionSlot,
        List<GRMechSectionLayoutDef> layouts,
        Action<Building_GR_Drydock, GRMechSectionSlotDef, GRMechSectionLayoutDef> onSelected)
    {
        this.dock = dock;
        this.sectionSlot = sectionSlot;
        this.onSelected = onSelected;
        title = "选择区段";

        GrayMechDesignUtility.TryGetSelectedLayout(dock?.DesignDraft, sectionSlot, out GRMechSectionLayoutDef currentLayout);
        List<GRMechSlotSizeDef> orderedSlotSizes = new(DefDatabase<GRMechSlotSizeDef>.AllDefsListForReading);
        orderedSlotSizes.Sort(CompareSlotSizes);

        List<LayoutOption> builtOptions = new(layouts?.Count ?? 0);
        if (layouts != null)
        {
            for (int i = 0; i < layouts.Count; i++)
            {
                GRMechSectionLayoutDef layout = layouts[i];
                if (layout == null)
                {
                    continue;
                }

                builtOptions.Add(BuildOption(layout, currentLayout, orderedSlotSizes));
            }
        }

        options = builtOptions.ToArray();

        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
        closeOnAccept = false;
        closeOnCancel = true;
        doCloseX = true;
        doWindowBackground = true;
        draggable = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        Color oldColor = GUI.color;

        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(new Rect(0f, 0f, inRect.width, TitleHeight), title);

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        Rect contentRect = new(0f, TitleHeight + 8f, inRect.width, inRect.height - TitleHeight - 8f);
        int rowCount = Mathf.CeilToInt(options.Length / (float)ColumnCount);
        float viewHeight = rowCount * CardHeight + Mathf.Max(0, rowCount - 1) * CardGap;
        Rect viewRect = new(0f, 0f, contentRect.width - 16f, Mathf.Max(contentRect.height, viewHeight));
        float cardWidth = (viewRect.width - CardGap * (ColumnCount - 1)) / ColumnCount;

        Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);
        for (int i = 0; i < options.Length; i++)
        {
            int row = i / ColumnCount;
            int column = i % ColumnCount;
            Rect cardRect = new(
                column * (cardWidth + CardGap),
                row * (CardHeight + CardGap),
                cardWidth,
                CardHeight);
            if (DrawLayoutCard(cardRect, options[i]))
            {
                Widgets.EndScrollView();
                RestoreGuiState(oldFont, oldAnchor, oldColor);
                return;
            }
        }

        Widgets.EndScrollView();
        RestoreGuiState(oldFont, oldAnchor, oldColor);
    }

    private bool DrawLayoutCard(Rect rect, LayoutOption option)
    {
        bool hovered = Mouse.IsOver(rect);
        Color accent = option.Selected
            ? GrayMechDrydockTabStyle.SelectedColor
            : option.Available
                ? GrayMechDrydockTabText.GetSectionAccentColor(sectionSlot)
                : GrayMechDrydockTabStyle.LockedColor;
        Color fill = option.Selected
            ? new Color(accent.r, accent.g, accent.b, 0.14f)
            : new Color(accent.r, accent.g, accent.b, hovered && option.Available ? 0.1f : 0.05f);

        Widgets.DrawBoxSolidWithOutline(rect, fill, hovered && option.Available ? Color.white : accent, option.Selected ? 2 : 1);
        Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 4f), accent);

        Color oldColor = GUI.color;
        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;

        GUI.color = option.Available ? Color.white : LockedTextColor;
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(rect.x + CardPadding, rect.y + 6f, rect.width - CardPadding * 2f, 24f), option.Label);

        Text.Anchor = TextAnchor.UpperLeft;
        Rect inner = new(rect.x + CardPadding, rect.y + 34f, rect.width - CardPadding * 2f, rect.height - 42f);
        float rowHeight = inner.height * 0.5f;
        DrawSlotGroupRow(new Rect(inner.x, inner.y, inner.width, rowHeight), "武器槽", option.WeaponTokens, option.Available);
        DrawSlotGroupRow(new Rect(inner.x, inner.y + rowHeight, inner.width, rowHeight), "通用槽", option.UtilityTokens, option.Available);

        GUI.color = oldColor;
        Text.Anchor = oldAnchor;
        Text.Font = oldFont;

        if (hovered && option.Available && !option.Selected)
        {
            Widgets.DrawHighlight(rect);
        }

        TooltipHandler.TipRegion(rect, option.Tooltip);
        if (!Widgets.ButtonInvisible(rect))
        {
            return false;
        }

        if (!option.Available)
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            return false;
        }

        onSelected?.Invoke(dock, sectionSlot, option.Layout);
        Close();
        return true;
    }

    private static void DrawSlotGroupRow(Rect rect, string label, SlotCountToken[] tokens, bool available)
    {
        Color oldColor = GUI.color;
        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;

        Text.Anchor = TextAnchor.MiddleLeft;
        Text.Font = GameFont.Small;
        GUI.color = available ? Color.white : LockedTextColor;
        Widgets.Label(new Rect(rect.x, rect.y, RowLabelWidth, rect.height), label);

        Rect tokenArea = new(rect.x + RowLabelWidth, rect.y, rect.width - RowLabelWidth, rect.height);
        if (tokens.Length == 0)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(tokenArea, "-");
            RestoreGuiState(oldFont, oldAnchor, oldColor);
            return;
        }

        float fixedWidth = (SlotQuantityGap + SlotQuantityWidth) * tokens.Length
                           + SlotIconGap * Mathf.Max(0, tokens.Length - 1);
        float maxSizeForCount = (tokenArea.width - fixedWidth) / tokens.Length;
        float iconSize = Mathf.Min(SlotIconSize, maxSizeForCount);
        float tokenWidth = iconSize + SlotQuantityGap + SlotQuantityWidth;
        float totalWidth = tokenWidth * tokens.Length + SlotIconGap * Mathf.Max(0, tokens.Length - 1);
        float x = tokenArea.x + Mathf.Max(0f, (tokenArea.width - totalWidth) * 0.5f);
        float y = tokenArea.y + (tokenArea.height - iconSize) * 0.5f;

        for (int i = 0; i < tokens.Length; i++)
        {
            DrawSlotCountToken(new Rect(x, y, iconSize, iconSize), tokens[i], available);
            x += tokenWidth + SlotIconGap;
        }

        RestoreGuiState(oldFont, oldAnchor, oldColor);
    }

    private static void DrawSlotCountToken(Rect rect, SlotCountToken token, bool available)
    {
        Color accent = available
            ? token.Color
            : new Color(token.Color.r * 0.55f, token.Color.g * 0.55f, token.Color.b * 0.55f, token.Color.a);
        Widgets.DrawBoxSolidWithOutline(rect, new Color(accent.r, accent.g, accent.b, 0.14f), accent, 2);
        Widgets.DrawBoxSolid(rect.ContractedBy(4f), GrayMechDrydockTabStyle.SlotInnerColor);

        Color oldColor = GUI.color;
        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;

        GUI.color = available ? Color.white : LockedTextColor;
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Small;
        Widgets.Label(rect.ContractedBy(2f), token.Glyph);

        Rect countRect = new(rect.xMax + SlotQuantityGap, rect.y, SlotQuantityWidth, rect.height);
        GUI.color = available ? GrayMechDrydockTabStyle.SelectedColor : LockedTextColor;
        Text.Anchor = TextAnchor.MiddleLeft;
        Text.Font = GameFont.Tiny;
        Widgets.Label(countRect, token.QuantityText);

        RestoreGuiState(oldFont, oldAnchor, oldColor);
    }

    private static LayoutOption BuildOption(
        GRMechSectionLayoutDef layout,
        GRMechSectionLayoutDef currentLayout,
        List<GRMechSlotSizeDef> orderedSlotSizes)
    {
        bool available = GrayMechDesignUtility.IsResearchAvailable(layout);
        return new LayoutOption
        {
            Layout = layout,
            Label = layout.LabelCap.ToString(),
            Tooltip = BuildTooltip(layout, available),
            Available = available,
            Selected = layout == currentLayout,
            WeaponTokens = BuildTokens(layout, orderedSlotSizes, weaponGroup: true),
            UtilityTokens = BuildTokens(layout, orderedSlotSizes, weaponGroup: false)
        };
    }

    private static SlotCountToken[] BuildTokens(
        GRMechSectionLayoutDef layout,
        List<GRMechSlotSizeDef> orderedSlotSizes,
        bool weaponGroup)
    {
        List<SlotCountToken> tokens = new();
        List<GRMechSlotEntry> slots = layout.ResolvedSlots;
        for (int i = 0; i < orderedSlotSizes.Count; i++)
        {
            GRMechSlotSizeDef slotSize = orderedSlotSizes[i];
            if (slotSize == null)
            {
                continue;
            }

            int count = 0;
            GRMechSlotEntry representative = null;
            for (int j = 0; j < slots.Count; j++)
            {
                GRMechSlotEntry slot = slots[j];
                if (slot?.slotSize != slotSize || !BelongsToGroup(slot.slotCategory, weaponGroup))
                {
                    continue;
                }

                representative ??= slot;
                count++;
            }

            if (count <= 0)
            {
                continue;
            }

            tokens.Add(new SlotCountToken(
                slotSize.glyph.NullOrEmpty() ? "?" : slotSize.glyph,
                "x" + count,
                GrayMechDrydockTabStyle.GetSlotColor(representative)));
        }

        return tokens.ToArray();
    }

    private static bool BelongsToGroup(GRMechSlotCategory category, bool weaponGroup)
    {
        if (weaponGroup)
        {
            return category == GRMechSlotCategory.Weapon;
        }

        return category == GRMechSlotCategory.Utility || category == GRMechSlotCategory.Auxiliary;
    }

    private static string BuildTooltip(GRMechSectionLayoutDef layout, bool available)
    {
        StringBuilder buffer = new();
        if (!layout.description.NullOrEmpty())
        {
            buffer.Append(layout.description);
        }

        if (!available && layout.researchPrerequisites != null)
        {
            for (int i = 0; i < layout.researchPrerequisites.Count; i++)
            {
                ResearchProjectDef project = layout.researchPrerequisites[i];
                if (project == null || project.IsFinished)
                {
                    continue;
                }

                if (buffer.Length > 0)
                {
                    buffer.AppendLine();
                    buffer.AppendLine();
                }

                buffer.Append("需要研究: ");
                buffer.Append(project.LabelCap);
                break;
            }
        }

        return buffer.ToString();
    }

    private static int CompareSlotSizes(GRMechSlotSizeDef left, GRMechSlotSizeDef right)
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
        return orderCompare != 0 ? orderCompare : string.CompareOrdinal(left.defName, right.defName);
    }

    private static void RestoreGuiState(GameFont font, TextAnchor anchor, Color color)
    {
        Text.Font = font;
        Text.Anchor = anchor;
        GUI.color = color;
    }

    private class LayoutOption
    {
        internal GRMechSectionLayoutDef Layout;
        internal string Label;
        internal string Tooltip;
        internal bool Available;
        internal bool Selected;
        internal SlotCountToken[] WeaponTokens;
        internal SlotCountToken[] UtilityTokens;
    }

    private readonly struct SlotCountToken
    {
        internal readonly string Glyph;
        internal readonly string QuantityText;
        internal readonly Color Color;

        internal SlotCountToken(string glyph, string quantityText, Color color)
        {
            Glyph = glyph;
            QuantityText = quantityText;
            Color = color;
        }
    }
}
