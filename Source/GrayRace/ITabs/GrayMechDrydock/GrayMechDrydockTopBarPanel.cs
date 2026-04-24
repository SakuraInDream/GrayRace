using UnityEngine;
using Verse;

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
