using RimWorld;
using SD.GrayRace.ThingClasses;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

public class ITab_GrayMechDrydock : ITab
{
    private readonly GrayMechDrydockTabState state = new();
    private readonly GrayMechDrydockTabController controller;
    private readonly GrayMechDrydockTopBarPanel topBarPanel = new();
    private readonly GrayMechDrydockDesignerPanel designerPanel = new();
    private readonly GrayMechDrydockFocusPanel focusPanel = new();
    private readonly GrayMechDrydockBottomBarPanel bottomBarPanel = new();

    public ITab_GrayMechDrydock()
    {
        controller = new GrayMechDrydockTabController(state);
        size = GrayMechDrydockTabStyle.WindowSize;
        labelKey = "Designer";
    }

    private Building_GR_Drydock SelDock => SelThing as Building_GR_Drydock;

    public override bool IsVisible => base.IsVisible && SelDock != null;

    protected override void FillTab()
    {
        Building_GR_Drydock dock = SelDock;
        if (dock == null)
        {
            return;
        }

        state.EnsureCaches(dock);
        GrayMechDrydockTabContext context = new(dock, state, controller);

        Rect root = new Rect(0f, 0f, size.x, size.y).ContractedBy(GrayMechDrydockTabStyle.Margin);
        float topBarHeight = state.GetTopBarHeight(dock, root.width);
        float bottomBarHeight = state.GetBottomBarHeight(dock, root.width);

        Rect topRect = new Rect(root.x, root.y, root.width, topBarHeight);
        Rect contentRect = new Rect(root.x, topRect.yMax + GrayMechDrydockTabStyle.PanelGap, root.width, root.height - topBarHeight - bottomBarHeight - GrayMechDrydockTabStyle.PanelGap * 2f);
        Rect bottomRect = new Rect(root.x, contentRect.yMax + GrayMechDrydockTabStyle.PanelGap, root.width, bottomBarHeight);

        Rect centerRect = new Rect(contentRect.x, contentRect.y, contentRect.width - GrayMechDrydockTabStyle.RightPanelWidth - GrayMechDrydockTabStyle.PanelGap, contentRect.height);
        Rect rightRect = new Rect(centerRect.xMax + GrayMechDrydockTabStyle.PanelGap, contentRect.y, GrayMechDrydockTabStyle.RightPanelWidth, contentRect.height);

        topBarPanel.Draw(context, topRect);
        designerPanel.Draw(context, centerRect);
        focusPanel.Draw(context, rightRect);
        bottomBarPanel.Draw(context, bottomRect);
    }
}
