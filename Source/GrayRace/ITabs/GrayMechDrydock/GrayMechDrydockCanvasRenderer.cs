using System.Text;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

internal sealed class GrayMechDrydockCanvasRenderer
{
    internal void DrawGrid(Rect rect)
    {
        const float cellSize = 34f;
        int verticalLines = Mathf.FloorToInt(rect.width / cellSize);
        int horizontalLines = Mathf.FloorToInt(rect.height / cellSize);
        for (int i = 1; i < verticalLines; i++)
        {
            float x = rect.x + i * cellSize;
            Color color = i % 4 == 0
                ? new Color(GrayMechDrydockTabStyle.GridLineColor.r, GrayMechDrydockTabStyle.GridLineColor.g, GrayMechDrydockTabStyle.GridLineColor.b, 0.09f)
                : GrayMechDrydockTabStyle.GridLineColor;
            Widgets.DrawLine(new Vector2(x, rect.y), new Vector2(x, rect.yMax), color, 1f);
        }

        for (int i = 1; i < horizontalLines; i++)
        {
            float y = rect.y + i * cellSize;
            Color color = i % 4 == 0
                ? new Color(GrayMechDrydockTabStyle.GridLineColor.r, GrayMechDrydockTabStyle.GridLineColor.g, GrayMechDrydockTabStyle.GridLineColor.b, 0.09f)
                : GrayMechDrydockTabStyle.GridLineColor;
            Widgets.DrawLine(new Vector2(rect.x, y), new Vector2(rect.xMax, y), color, 1f);
        }
    }

    internal void DrawNebulaBackdrop(Rect rect)
    {
        Widgets.DrawBoxSolid(new Rect(rect.x + rect.width * 0.03f, rect.y + rect.height * 0.26f, rect.width * 0.24f, rect.height * 0.44f), GrayMechDrydockTabStyle.NebulaLeftColor);
        Widgets.DrawBoxSolid(new Rect(rect.x + rect.width * 0.22f, rect.y + rect.height * 0.08f, rect.width * 0.19f, rect.height * 0.24f), GrayMechDrydockTabStyle.NebulaCenterColor);
        Widgets.DrawBoxSolid(new Rect(rect.x + rect.width * 0.36f, rect.y + rect.height * 0.18f, rect.width * 0.28f, rect.height * 0.52f), GrayMechDrydockTabStyle.NebulaCenterColor);
        Widgets.DrawBoxSolid(new Rect(rect.x + rect.width * 0.69f, rect.y + rect.height * 0.14f, rect.width * 0.18f, rect.height * 0.28f), GrayMechDrydockTabStyle.NebulaRightColor);
        Widgets.DrawBoxSolid(new Rect(rect.x + rect.width * 0.62f, rect.y + rect.height * 0.58f, rect.width * 0.24f, rect.height * 0.18f), GrayMechDrydockTabStyle.NebulaRightColor);
    }

    internal void DrawCanvasFrame(Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, new Color(0f, 0f, 0f, 0f), GrayMechDrydockTabStyle.HullOutline, 2);
        Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 5f), GrayMechDrydockTabStyle.HeaderLineColor);
        Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.yMax - 6f, rect.width - 2f, 3f), new Color(GrayMechDrydockTabStyle.HeaderLineColor.r, GrayMechDrydockTabStyle.HeaderLineColor.g, GrayMechDrydockTabStyle.HeaderLineColor.b, 0.3f));

        DrawCornerBracket(rect.x + 9f, rect.y + 9f, 20f, true, true);
        DrawCornerBracket(rect.xMax - 9f, rect.y + 9f, 20f, false, true);
        DrawCornerBracket(rect.x + 9f, rect.yMax - 9f, 20f, true, false);
        DrawCornerBracket(rect.xMax - 9f, rect.yMax - 9f, 20f, false, false);
    }

    internal Rect GetPreviewRect(Rect rect)
    {
        return new Rect(rect.x + rect.width * 0.08f, rect.y + rect.height * 0.08f, rect.width * 0.84f, rect.height * 0.84f);
    }

    internal Rect GetSectionVisualRect(GrayMechDesignSnapshot draft, GRMechSectionRoleDef role, Rect rect)
    {
        int index = 0;
        int total = 0;
        if (draft?.chassis?.sections != null)
        {
            total = draft.chassis.sections.Count;
            for (int i = 0; i < draft.chassis.sections.Count; i++)
            {
                if (draft.chassis.sections[i]?.role == role)
                {
                    index = i;
                    break;
                }
            }
        }

        if (total <= 1)
        {
            return rect.ContractedBy(30f);
        }

        float centerY = rect.center.y;
        float spacing = 16f;
        if (total == 3)
        {
            float bowWidth = rect.width * 0.28f;
            float coreWidth = rect.width * 0.27f;
            float sternWidth = rect.width * 0.24f;
            float totalWidth = bowWidth + coreWidth + sternWidth + spacing * 2f;
            float startX = rect.x + (rect.width - totalWidth) * 0.5f;
            float bowHeight = rect.height * 0.34f;
            float coreHeight = rect.height * 0.42f;
            float sternHeight = rect.height * 0.28f;

            if (index == 0)
            {
                return new Rect(startX, centerY - bowHeight * 0.5f, bowWidth, bowHeight);
            }

            if (index == 1)
            {
                return new Rect(startX + bowWidth + spacing, centerY - coreHeight * 0.5f, coreWidth, coreHeight);
            }

            return new Rect(startX + bowWidth + spacing + coreWidth + spacing, centerY - sternHeight * 0.5f, sternWidth, sternHeight);
        }

        float width = (rect.width - spacing * (total - 1)) / total;
        return new Rect(rect.x + index * (width + spacing), centerY - rect.height * 0.18f, width, rect.height * 0.36f);
    }

    internal void DrawShipPreview(Rect rect, GrayMechDesignSnapshot draft)
    {
        Rect glowRect = new Rect(rect.x + rect.width * 0.1f, rect.center.y - rect.height * 0.16f, rect.width * 0.8f, rect.height * 0.32f);
        Widgets.DrawBoxSolid(glowRect, new Color(GrayMechDrydockTabStyle.AuxGlowColor.r, GrayMechDrydockTabStyle.AuxGlowColor.g, GrayMechDrydockTabStyle.AuxGlowColor.b, 0.12f));

        Texture2D preview = GrayMechDrydockTabStyle.GetChassisPreview(draft?.chassis);
        if (preview != null)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.92f);
            Widgets.DrawTextureFitted(rect, preview, 1f);
            GUI.color = oldColor;
        }
        else
        {
            DrawHullBackdrop(rect);
        }

        DrawHullBackdrop(rect);
        DrawMidline(rect);
    }

    internal void DrawHullBackdrop(Rect rect)
    {
        Rect spineRect = new Rect(rect.x + rect.width * 0.14f, rect.center.y - rect.height * 0.07f, rect.width * 0.7f, rect.height * 0.14f);
        Widgets.DrawBoxSolidWithOutline(
            spineRect,
            new Color(GrayMechDrydockTabStyle.HullPlateColor.r, GrayMechDrydockTabStyle.HullPlateColor.g, GrayMechDrydockTabStyle.HullPlateColor.b, 0.22f),
            new Color(GrayMechDrydockTabStyle.HullAccentColor.r, GrayMechDrydockTabStyle.HullAccentColor.g, GrayMechDrydockTabStyle.HullAccentColor.b, 0.14f));

        Vector2 nose = new(spineRect.x - rect.width * 0.11f, spineRect.center.y);
        Vector2 sternTop = new(spineRect.xMax + rect.width * 0.08f, spineRect.y + 4f);
        Vector2 sternBottom = new(spineRect.xMax + rect.width * 0.08f, spineRect.yMax - 4f);
        Widgets.DrawLine(new Vector2(spineRect.x, spineRect.y), nose, new Color(GrayMechDrydockTabStyle.HullAccentColor.r, GrayMechDrydockTabStyle.HullAccentColor.g, GrayMechDrydockTabStyle.HullAccentColor.b, 0.2f), 2f);
        Widgets.DrawLine(new Vector2(spineRect.x, spineRect.yMax), nose, new Color(GrayMechDrydockTabStyle.HullAccentColor.r, GrayMechDrydockTabStyle.HullAccentColor.g, GrayMechDrydockTabStyle.HullAccentColor.b, 0.2f), 2f);
        Widgets.DrawLine(new Vector2(spineRect.xMax, spineRect.y), sternTop, new Color(GrayMechDrydockTabStyle.HullAccentColor.r, GrayMechDrydockTabStyle.HullAccentColor.g, GrayMechDrydockTabStyle.HullAccentColor.b, 0.18f), 2f);
        Widgets.DrawLine(new Vector2(spineRect.xMax, spineRect.yMax), sternBottom, new Color(GrayMechDrydockTabStyle.HullAccentColor.r, GrayMechDrydockTabStyle.HullAccentColor.g, GrayMechDrydockTabStyle.HullAccentColor.b, 0.18f), 2f);

        Rect dorsalRect = new Rect(rect.center.x - rect.width * 0.1f, rect.y + rect.height * 0.2f, rect.width * 0.2f, rect.height * 0.12f);
        Rect ventralRect = new Rect(rect.center.x - rect.width * 0.1f, rect.yMax - rect.height * 0.32f, rect.width * 0.2f, rect.height * 0.12f);
        Widgets.DrawBoxSolidWithOutline(dorsalRect, new Color(GrayMechDrydockTabStyle.HullPlateColor.r, GrayMechDrydockTabStyle.HullPlateColor.g, GrayMechDrydockTabStyle.HullPlateColor.b, 0.18f), new Color(GrayMechDrydockTabStyle.HullAccentColor.r, GrayMechDrydockTabStyle.HullAccentColor.g, GrayMechDrydockTabStyle.HullAccentColor.b, 0.12f));
        Widgets.DrawBoxSolidWithOutline(ventralRect, new Color(GrayMechDrydockTabStyle.HullPlateColor.r, GrayMechDrydockTabStyle.HullPlateColor.g, GrayMechDrydockTabStyle.HullPlateColor.b, 0.18f), new Color(GrayMechDrydockTabStyle.HullAccentColor.r, GrayMechDrydockTabStyle.HullAccentColor.g, GrayMechDrydockTabStyle.HullAccentColor.b, 0.12f));
    }

    internal void DrawSectionOverlay(GRMechSectionRoleDef role, Rect rect, bool selected, bool ownsSelectedSlot)
    {
        Color accent = GrayMechDrydockTabText.GetSectionAccentColor(role);
        Color outline = selected ? GrayMechDrydockTabStyle.SelectedColor : (ownsSelectedSlot ? accent : new Color(accent.r, accent.g, accent.b, 0.55f));
        Color fill = selected
            ? new Color(GrayMechDrydockTabStyle.SelectedColor.r, GrayMechDrydockTabStyle.SelectedColor.g, GrayMechDrydockTabStyle.SelectedColor.b, 0.12f)
            : new Color(accent.r, accent.g, accent.b, ownsSelectedSlot ? 0.08f : 0.03f);

        Widgets.DrawBoxSolidWithOutline(rect, fill, outline, selected ? 2 : 1);
        Widgets.DrawBoxSolid(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, 4f), accent);
        Widgets.DrawBoxSolid(new Rect(rect.x + 12f, rect.yMax - 8f, rect.width - 24f, 3f), new Color(accent.r, accent.g, accent.b, 0.65f));
    }

    internal void DrawSectionCaption(GRMechSectionRoleDef role, Rect rect, GRMechSectionLayoutDef layout, StringBuilder textBuilder)
    {
        Rect roleRect = new Rect(rect.x + 8f, rect.y + 10f, rect.width - 16f, 18f);
        Rect layoutRect = new Rect(rect.x + 8f, rect.yMax - 34f, rect.width - 16f, 18f);
        string slotExpr = GrayMechDrydockTabText.BuildLayoutSlotExpression(layout, textBuilder);
        float slotExprHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(slotExpr, rect.width - 16f, GameFont.Tiny);
        Rect slotRect = new Rect(rect.x + 8f, layoutRect.yMax + 2f, rect.width - 16f, slotExprHeight);

        Widgets.DrawBoxSolid(roleRect, new Color(0f, 0f, 0f, 0.34f));
        Widgets.DrawBoxSolid(layoutRect, new Color(0f, 0f, 0f, 0.3f));
        Widgets.DrawBoxSolid(slotRect, new Color(0f, 0f, 0f, 0.22f));
        Widgets.Label(roleRect, role.LabelCap.ToString());
        Widgets.Label(layoutRect, layout?.LabelCap.ToString() ?? "No layout");

        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        GrayMechDrydockTabText.DrawWrappedLabel(slotRect, slotExpr);
        Text.Font = oldFont;
    }

    internal void DrawSlotBay(Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, new Color(GrayMechDrydockTabStyle.CardFill.r, GrayMechDrydockTabStyle.CardFill.g, GrayMechDrydockTabStyle.CardFill.b, 0.84f), GrayMechDrydockTabStyle.HullOutline);
        Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + rect.height * 0.5f - 1f, rect.width - 2f, 2f), new Color(GrayMechDrydockTabStyle.HullAccentColor.r, GrayMechDrydockTabStyle.HullAccentColor.g, GrayMechDrydockTabStyle.HullAccentColor.b, 0.18f));
    }

    internal void DrawSectionConnector(Rect sectionRect, Rect slotRect, bool slotAboveSection, Color color)
    {
        Vector2 from = new(sectionRect.center.x, slotAboveSection ? sectionRect.y : sectionRect.yMax);
        Vector2 to = new(slotRect.center.x, slotAboveSection ? slotRect.yMax : slotRect.y);
        Widgets.DrawLine(from, to, new Color(color.r, color.g, color.b, 0.32f), 2f);
    }

    internal void DrawSlotWidget(Rect rect, GRMechSlotDef slot, GRMechModuleDef module, bool selected, Color accent)
    {
        Rect shadowRect = rect;
        shadowRect.x += 2f;
        shadowRect.y += 2f;
        Widgets.DrawBoxSolid(shadowRect, new Color(0f, 0f, 0f, 0.28f));

        Color outline = selected ? GrayMechDrydockTabStyle.SelectedColor : accent;
        Color fill = module == null ? new Color(accent.r, accent.g, accent.b, 0.08f) : new Color(accent.r, accent.g, accent.b, 0.18f);
        Widgets.DrawBoxSolidWithOutline(rect, fill, outline, selected ? 2 : 1);
        Widgets.DrawBoxSolid(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 5f), accent);

        Rect innerRect = rect.ContractedBy(5f);
        Widgets.DrawBoxSolid(innerRect, GrayMechDrydockTabStyle.SlotInnerColor);

        if (module != null && module.equipmentDef != null)
        {
            Widgets.ThingIcon(new Rect(innerRect.x + 2f, innerRect.y + 2f, innerRect.width - 4f, innerRect.height - 4f), module.equipmentDef, module.equipmentStuff, null, 0.9f);
        }
        else
        {
            TextAnchor oldAnchor = Text.Anchor;
            GameFont oldFont = Text.Font;
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
            Widgets.Label(innerRect, module == null ? GrayMechDrydockTabText.GetSlotTypeGlyph(slot) : GetModuleGlyph(module));
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
        }

        if (module != null)
        {
            Widgets.DrawBoxSolid(new Rect(rect.x + 4f, rect.yMax - 7f, rect.width - 8f, 3f), GrayMechDrydockTabStyle.SlotInstalledColor);
        }

        string sizeGlyph = GrayMechDrydockTabText.GetSlotSizeGlyph(slot);
        if (!sizeGlyph.NullOrEmpty())
        {
            Rect badgeRect = new Rect(rect.xMax - 14f, rect.yMax - 14f, 12f, 12f);
            Widgets.DrawBoxSolidWithOutline(badgeRect, GrayMechDrydockTabStyle.SlotBadgeColor, outline);
            TextAnchor oldAnchor = Text.Anchor;
            GameFont oldFont = Text.Font;
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(badgeRect, sizeGlyph);
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
        }
    }

    internal void DrawModuleIconTile(Rect rect, GRMechModuleDef module, GRMechSlotDef slot, Color accent)
    {
        Widgets.DrawBoxSolidWithOutline(rect, new Color(accent.r, accent.g, accent.b, 0.12f), accent);
        Rect innerRect = rect.ContractedBy(4f);
        Widgets.DrawBoxSolid(innerRect, GrayMechDrydockTabStyle.SlotInnerColor);
        if (module?.equipmentDef != null)
        {
            Widgets.ThingIcon(innerRect, module.equipmentDef, module.equipmentStuff, null, 0.85f);
            return;
        }

        string glyph = module == null ? GrayMechDrydockTabText.GetSlotTypeGlyph(slot) : GetModuleGlyph(module);
        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Small;
        Widgets.Label(innerRect, glyph);
        Text.Anchor = oldAnchor;
        Text.Font = oldFont;
    }

    private static string GetModuleGlyph(GRMechModuleDef module)
    {
        if (module?.label.NullOrEmpty() ?? true)
        {
            return "?";
        }

        return module.label.Substring(0, 1).ToUpperInvariant();
    }

    private void DrawMidline(Rect rect)
    {
        float y = rect.center.y;
        Widgets.DrawLine(new Vector2(rect.x + 24f, y), new Vector2(rect.xMax - 24f, y), new Color(GrayMechDrydockTabStyle.HullAccentColor.r, GrayMechDrydockTabStyle.HullAccentColor.g, GrayMechDrydockTabStyle.HullAccentColor.b, 0.22f), 2f);
        Widgets.DrawLine(new Vector2(rect.center.x, rect.y + rect.height * 0.16f), new Vector2(rect.center.x, rect.y + rect.height * 0.84f), new Color(GrayMechDrydockTabStyle.HullAccentColor.r, GrayMechDrydockTabStyle.HullAccentColor.g, GrayMechDrydockTabStyle.HullAccentColor.b, 0.12f), 1.5f);
    }

    private static void DrawCornerBracket(float x, float y, float size, bool left, bool top)
    {
        float x2 = left ? x + size : x - size;
        float y2 = top ? y + size : y - size;
        Widgets.DrawLine(new Vector2(x, y), new Vector2(x2, y), GrayMechDrydockTabStyle.HeaderLineColor, 2f);
        Widgets.DrawLine(new Vector2(x, y), new Vector2(x, y2), GrayMechDrydockTabStyle.HeaderLineColor, 2f);
    }
}
