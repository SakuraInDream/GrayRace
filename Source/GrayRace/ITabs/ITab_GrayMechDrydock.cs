using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.ThingClasses;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

public class ITab_GrayMechDrydock : ITab
{
    private readonly GrayMechDrydockViewState viewState = new();
    private readonly GrayMechDrydockPresenter state = new();
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

        state.EnsureCaches(dock, viewState);
        GrayMechDrydockTabContext context = new(dock, state, viewState, controller);

        Rect root = new Rect(
            GrayMechDrydockTabStyle.Margin,
            GrayMechDrydockTabStyle.Margin + GrayMechDrydockTabStyle.CloseButtonReserveTop,
            size.x - GrayMechDrydockTabStyle.Margin * 2f - GrayMechDrydockTabStyle.CloseButtonReserveRight,
            size.y - GrayMechDrydockTabStyle.Margin * 2f - GrayMechDrydockTabStyle.CloseButtonReserveTop);
        Rect bodyRect = root;
        Rect mainBodyRect = new Rect(bodyRect.x, bodyRect.y, bodyRect.width - GrayMechDrydockTabStyle.RightPanelWidth - GrayMechDrydockTabStyle.PanelGap, bodyRect.height);
        Rect summaryRect = new Rect(mainBodyRect.xMax + GrayMechDrydockTabStyle.PanelGap, bodyRect.y, GrayMechDrydockTabStyle.RightPanelWidth, bodyRect.height);

        float bottomBarHeight = state.GetBottomBarHeight(mainBodyRect.width);
        float centerHeight = Mathf.Max(120f, mainBodyRect.height - bottomBarHeight - GrayMechDrydockTabStyle.PanelGap);
        Rect upperRect = new Rect(mainBodyRect.x, mainBodyRect.y, mainBodyRect.width, centerHeight);
        float maxLeftPanelWidth = Mathf.Max(240f, upperRect.width - GrayMechDrydockTabStyle.MinCenterCanvasWidth - GrayMechDrydockTabStyle.PanelGap);
        float leftPanelWidth = Mathf.Min(GrayMechDrydockTabStyle.LeftPanelWidth, maxLeftPanelWidth);
        Rect leftRect = new Rect(upperRect.x, upperRect.y, leftPanelWidth, upperRect.height);
        Rect centerRect = new Rect(leftRect.xMax + GrayMechDrydockTabStyle.PanelGap, upperRect.y, upperRect.width - leftPanelWidth - GrayMechDrydockTabStyle.PanelGap, upperRect.height);
        Rect bottomRect = new Rect(mainBodyRect.x, upperRect.yMax + GrayMechDrydockTabStyle.PanelGap, mainBodyRect.width, bottomBarHeight);

        designerPanel.Draw(context, centerRect);
        focusPanel.DrawSelection(context, leftRect);
        focusPanel.DrawSummary(context, summaryRect);
        bottomBarPanel.Draw(context, bottomRect);
        DrawArmedModuleCursor(context);
    }

    private void DrawArmedModuleCursor(GrayMechDrydockTabContext context)
    {
        GRMechModuleDef armedModule = context.State.ArmedModule;
        if (armedModule == null)
        {
            return;
        }

        Vector2 mousePosition = Event.current.mousePosition;
        Rect tabRect = new Rect(0f, 0f, size.x, size.y);
        if (!tabRect.Contains(mousePosition))
        {
            return;
        }

        const float cursorOffset = 14f;
        const float iconSize = 32f;
        Rect iconRect = new Rect(mousePosition.x + cursorOffset, mousePosition.y + cursorOffset, iconSize, iconSize);
        iconRect.x = Mathf.Min(iconRect.x, size.x - iconRect.width - 4f);
        iconRect.y = Mathf.Min(iconRect.y, size.y - iconRect.height - 4f);

        Texture2D icon = GrayMechDrydockTabStyle.GetModuleIcon(armedModule);
        if (icon != null)
        {
            Widgets.DrawTextureFitted(iconRect, icon, 1f);
        }
        else if (armedModule.equipmentDef != null)
        {
            Widgets.ThingIcon(iconRect, armedModule.equipmentDef, armedModule.equipmentStuff);
        }
    }
}
