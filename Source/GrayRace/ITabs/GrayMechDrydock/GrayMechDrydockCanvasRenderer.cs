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

    internal void DrawShipPreview(Rect rect, GrayMechDesignSnapshot draft)
    {
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
            DrawMidline(rect);
        }
    }

    internal void DrawSlotBay(Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, new Color(GrayMechDrydockTabStyle.CardFill.r, GrayMechDrydockTabStyle.CardFill.g, GrayMechDrydockTabStyle.CardFill.b, 0.84f), GrayMechDrydockTabStyle.HullOutline);
        Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + rect.height * 0.5f - 1f, rect.width - 2f, 2f), new Color(GrayMechDrydockTabStyle.HullAccentColor.r, GrayMechDrydockTabStyle.HullAccentColor.g, GrayMechDrydockTabStyle.HullAccentColor.b, 0.18f));
    }

    internal void DrawDisabledSlotWidget(Rect rect, Color accent)
    {
        Rect shadowRect = rect;
        shadowRect.x += 2f;
        shadowRect.y += 2f;
        Widgets.DrawBoxSolid(shadowRect, new Color(0f, 0f, 0f, 0.2f));

        Color outline = new Color(accent.r, accent.g, accent.b, 0.22f);
        Color fill = new Color(accent.r, accent.g, accent.b, 0.04f);
        Widgets.DrawBoxSolidWithOutline(rect, fill, outline, 1);
        Widgets.DrawBoxSolid(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 5f), new Color(accent.r, accent.g, accent.b, 0.08f));

        Rect innerRect = rect.ContractedBy(5f);
        Widgets.DrawBoxSolid(innerRect, new Color(GrayMechDrydockTabStyle.SlotInnerColor.r, GrayMechDrydockTabStyle.SlotInnerColor.g, GrayMechDrydockTabStyle.SlotInnerColor.b, 0.92f));
        Widgets.DrawLine(innerRect.min, innerRect.max, new Color(accent.r, accent.g, accent.b, 0.12f), 1f);
        Widgets.DrawLine(new Vector2(innerRect.x, innerRect.yMax), new Vector2(innerRect.xMax, innerRect.y), new Color(accent.r, accent.g, accent.b, 0.12f), 1f);
    }

    internal void DrawSlotWidget(Rect rect, GRMechSlotEntry slot, GRMechModuleDef module, bool selected, bool drawSlotMarker = true)
    {
        Color accent = GrayMechDrydockTabStyle.GetSlotColor(slot);

        Rect shadowRect = rect;
        shadowRect.x += 2f;
        shadowRect.y += 2f;
        Widgets.DrawBoxSolid(shadowRect, new Color(0f, 0f, 0f, 0.28f));

        bool hovered = Mouse.IsOver(rect);
        Color outline = selected ? GrayMechDrydockTabStyle.SelectedColor : (hovered ? Color.white : accent);
        float fillAlpha = module == null ? (hovered ? 0.15f : 0.08f) : (hovered ? 0.25f : 0.18f);
        Color fill = selected
            ? new Color(GrayMechDrydockTabStyle.SelectedColor.r, GrayMechDrydockTabStyle.SelectedColor.g, GrayMechDrydockTabStyle.SelectedColor.b, fillAlpha)
            : new Color(accent.r, accent.g, accent.b, fillAlpha);

        Widgets.DrawBoxSolidWithOutline(rect, fill, outline, selected || hovered ? 2 : 1);
        Widgets.DrawBoxSolid(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 5f), accent);
        Rect innerRect = rect.ContractedBy(5f);
        Widgets.DrawBoxSolid(innerRect, GrayMechDrydockTabStyle.SlotInnerColor);
        DrawModuleIconContent(new Rect(innerRect.x + 2f, innerRect.y + 2f, innerRect.width - 4f, innerRect.height - 4f), module);

        if (module != null)
        {
            Widgets.DrawBoxSolid(new Rect(rect.x + 4f, rect.yMax - 7f, rect.width - 8f, 3f), GrayMechDrydockTabStyle.SlotInstalledColor);
        }

        if (drawSlotMarker)
        {
            DrawSlotMarkerBadge(rect, slot, accent);
        }

        if (hovered && !selected)
        {
            Widgets.DrawHighlight(rect);
        }
    }

    internal void DrawModuleIconTile(Rect rect, GRMechModuleDef module, GRMechSlotEntry slot)
    {
        Color accent = GrayMechDrydockTabStyle.GetSlotColor(slot);
        Widgets.DrawBoxSolidWithOutline(rect, new Color(accent.r, accent.g, accent.b, 0.12f), accent);
        Rect innerRect = rect.ContractedBy(4f);
        Widgets.DrawBoxSolid(innerRect, GrayMechDrydockTabStyle.SlotInnerColor);
        DrawModuleIconContent(innerRect, module);
    }

    internal void DrawInlineModuleOption(Rect rect, GRMechSlotEntry slot, GRMechModuleDef module, bool selected)
    {
        Color accent = GrayMechDrydockTabStyle.GetSlotColor(slot);
        bool hovered = Mouse.IsOver(rect);
        Color outline = selected
            ? GrayMechDrydockTabStyle.SelectedColor
            : (hovered ? Color.white : new Color(accent.r, accent.g, accent.b, 0.78f));
        Color fill = selected
            ? new Color(GrayMechDrydockTabStyle.SelectedColor.r, GrayMechDrydockTabStyle.SelectedColor.g, GrayMechDrydockTabStyle.SelectedColor.b, hovered ? 0.22f : 0.15f)
            : new Color(accent.r, accent.g, accent.b, hovered ? 0.16f : 0.1f);
        Widgets.DrawBoxSolidWithOutline(rect, fill, outline, selected || hovered ? 2 : 1);

        Rect innerRect = rect.ContractedBy(4f);
        Widgets.DrawBoxSolid(innerRect, GrayMechDrydockTabStyle.SlotInnerColor);
        DrawModuleIconContent(innerRect, module);

        if (selected)
        {
            Widgets.DrawBoxSolid(new Rect(rect.x + 4f, rect.yMax - 6f, rect.width - 8f, 2f), GrayMechDrydockTabStyle.SlotInstalledColor);
        }

        if (hovered && !selected)
        {
            Widgets.DrawHighlight(rect);
        }
    }

    private static void DrawSlotMarkerBadge(Rect rect, GRMechSlotEntry slot, Color accent)
    {
        if ((slot?.slotCategory ?? GRMechSlotCategory.Undefined) == GRMechSlotCategory.CoreSystem)
        {
            return;
        }

        string marker = GetSlotMarker(slot);
        if (marker.NullOrEmpty())
        {
            return;
        }

        bool compactMarker = marker.Length > 1;
        float badgeWidth = compactMarker ? 22f : 18f;
        float badgeHeight = 18f;
        Rect badgeRect = new Rect(rect.x + 2f, rect.y + 2f, badgeWidth, badgeHeight);
        Widgets.DrawBoxSolidWithOutline(badgeRect, accent, new Color(0f, 0f, 0f, 0.65f));

        Color oldColor = GUI.color;
        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        GUI.color = Color.black;
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = compactMarker ? GameFont.Tiny : GameFont.Small;
        Widgets.Label(badgeRect, marker);
        GUI.color = oldColor;
        Text.Anchor = oldAnchor;
        Text.Font = oldFont;
    }

    private static string GetSlotMarker(GRMechSlotEntry slot)
    {
        return slot?.glyph ?? string.Empty;
    }

    private void DrawModuleIconContent(Rect rect, GRMechModuleDef module)
    {
        Texture2D moduleIcon = GrayMechDrydockTabStyle.GetModuleIcon(module);
        if (moduleIcon != null)
        {
            Widgets.DrawTextureFitted(rect, moduleIcon, 1f);
            return;
        }

        if (module?.equipmentDef != null)
        {
            Widgets.ThingIcon(rect, module.equipmentDef, module.equipmentStuff, null, 0.85f);
            return;
        }

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
