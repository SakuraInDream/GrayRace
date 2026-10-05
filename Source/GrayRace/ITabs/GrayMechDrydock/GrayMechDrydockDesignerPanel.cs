using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

public class GrayMechDrydockDesignerPanel
{
    private readonly GrayMechSectionCanvasPanel sectionCanvasPanel = new();
    private readonly GrayMechDrydockDesignerCanvasHost canvasHost = new();

    internal void Draw(GrayMechDrydockTabContext context, Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Widgets.DrawBoxSolid(rect.ContractedBy(1f), GrayMechDrydockTabStyle.BgPanelAlt);
        Rect canvasRect = rect.ContractedBy(8f);
        canvasHost.Bind(context);
        sectionCanvasPanel.Draw(canvasRect, canvasHost);
    }
}
