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

    private void DrawDesignManagementSection(GrayMechDrydockTabContext context, Rect rect)
    {
        float y = rect.y;
        y = DrawSummarySectionHeader(new Rect(rect.x, y, rect.width, 22f), "设计管理", GrayMechDrydockTabStyle.SelectedColor);
        y += 4f;

        y = DrawDesignNameField(context, rect, y);

        Rect checkboxRect = new Rect(rect.x, y, rect.width, 24f);
        bool autoUpgrade = context.Dock.AutoUpgradeEnabled;
        Widgets.CheckboxLabeled(checkboxRect, "自动升级(未实装)", ref autoUpgrade);
        if (autoUpgrade != context.Dock.AutoUpgradeEnabled)
        {
            context.Dock.AutoUpgradeEnabled = autoUpgrade;
        }

        y = checkboxRect.yMax + 8f;
        float buttonGap = 8f;
        float smallButtonWidth = (rect.width - buttonGap) * 0.5f;
        Rect clearRect = new Rect(rect.x, y, smallButtonWidth, 28f);
        Rect queueRect = new Rect(clearRect.xMax + buttonGap, y, smallButtonWidth, 28f);
        if (GrayMechDrydockPanelWidgets.DrawButton(clearRect, "清除设计"))
        {
            context.Controller.ClearDesign(context.Dock);
        }

        if (GrayMechDrydockPanelWidgets.DrawButton(queueRect, "加入队列", context.State.CachedCanQueueOrder, true))
        {
            context.Controller.QueueAssemblyOrder(context.Dock);
        }

        y = clearRect.yMax + 8f;
        Rect saveRect = new Rect(rect.x, y, rect.width, 54f);
        if (GrayMechDrydockPanelWidgets.DrawButton(saveRect, "保存", highlighted: true))
        {
            context.Controller.SaveDesign(context.Dock);
        }
    }

    private static float DrawDesignNameField(GrayMechDrydockTabContext context, Rect areaRect, float y)
    {
        Rect fieldRect = new Rect(areaRect.x, y, areaRect.width, 30f);
        Color fieldBg = new Color(0.05f, 0.08f, 0.09f, 0.98f);
        Widgets.DrawBoxSolid(fieldRect, fieldBg);
        Widgets.DrawBoxSolidWithOutline(fieldRect, Color.clear, new Color(GrayMechDrydockTabStyle.SelectedColor.r, GrayMechDrydockTabStyle.SelectedColor.g, GrayMechDrydockTabStyle.SelectedColor.b, 0.4f));

        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        Color oldColor = GUI.color;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = Color.white;
        string currentName = context.Draft?.designLabel ?? string.Empty;
        string editedName = Widgets.TextField(new Rect(fieldRect.x + 6f, fieldRect.y + 4f, fieldRect.width - 12f, fieldRect.height - 8f), currentName);
        if (editedName != currentName)
        {
            context.Dock.SetDraftLabel(editedName);
        }

        GUI.color = oldColor;
        Text.Anchor = oldAnchor;
        Text.Font = oldFont;
        return y + fieldRect.height + 4f;
    }

    private static void DrawSectionFocusPanel(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFill, GrayMechDrydockTabStyle.HullOutline);
        Rect inner = rect.ContractedBy(8f);
        string prompt = context.Draft?.chassis == null
            ? "请先选择设计。"
            : "点击槽位安装或更换模块";
        GrayMechDrydockTabText.DrawWrappedLabelCentered(inner, prompt);
    }

    private void DrawSlotFocusPanel(GrayMechDrydockTabContext context, GRMechResolvedSlot resolvedSlot, Rect rect)
    {
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFill, GrayMechDrydockTabStyle.HullOutline);
        Rect inner = rect.ContractedBy(8f);
        Color accent = GrayMechDrydockTabText.GetSectionAccentColor(resolvedSlot.sectionSlot);

        GrayMechDesignUtility.TryGetSelectedModule(context.Draft, resolvedSlot, out GRMechModuleDef currentModule);
        Rect slotIconRect = new Rect(inner.x, inner.y + 2f, 42f, 42f);
        canvasRenderer.DrawModuleIconTile(slotIconRect, currentModule, resolvedSlot.slot);

        string titleText = context.State.GetSlotTitleText(resolvedSlot);
        string metaText = context.State.GetSlotMetaText(resolvedSlot);
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

        GrayMechDrydockPresenter state = context.State;
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
        float viewHeight = 0f;
        for (int i = 0; i < context.State.CompatibleModules.Count; i++)
        {
            viewHeight += context.State.GetModuleOptionHeight(context.State.CompatibleModules[i], listRect.width - 16f) + 8f;
        }

        Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(listRect.height, viewHeight));
        Widgets.BeginScrollView(listRect, ref state.FocusScrollPosition, viewRect);

        float y = 0f;
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

            DrawModuleOptionRow(context, new Rect(0f, y, viewRect.width, rowHeight), resolvedSlot, module, state.ArmedModule == module, accent);
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
            context.Controller.ArmModule(module);
        }
        float iconSize = rowRect.height - 2f;
        Rect iconRect = new Rect(rowRect.x + 1f, rowRect.y + 1f, iconSize, iconSize);
        canvasRenderer.DrawModuleIconTile(iconRect, module, resolvedSlot.slot);

        float leftX = iconRect.xMax + 8f;
        float leftWidth = rowRect.width - (leftX - rowRect.x) - 8f;
        string titleText = context.State.GetModuleTitleText(module);
        string cost = context.State.GetModuleCostText(module);
        float titleH = GrayMechDrydockTabText.MeasureWrappedTextHeight(titleText, leftWidth, GameFont.Small);
        float costH = GrayMechDrydockTabText.MeasureWrappedTextHeight(cost, leftWidth, GameFont.Small);
        float totalTextH = titleH + 2f + costH;
        float localY = rowRect.y + Mathf.Round((rowRect.height - totalTextH) * 0.5f);
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
