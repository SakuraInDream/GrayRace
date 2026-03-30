using RimWorld;
using SD.GrayRace.ThingClasses;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

public class ITab_GrayMechDrydockQueue : ITab
{
    private readonly GrayMechDrydockTabState state = new();
    private readonly GrayMechDrydockTabController controller;
    private readonly GrayMechDrydockFocusPanel focusPanel = new();

    public ITab_GrayMechDrydockQueue()
    {
        controller = new GrayMechDrydockTabController(state);
        size = GrayMechDrydockTabStyle.QueueWindowSize;
        labelKey = "建造队列";
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

        Rect root = new Rect(
            GrayMechDrydockTabStyle.Margin,
            GrayMechDrydockTabStyle.Margin + GrayMechDrydockTabStyle.CloseButtonReserveTop,
            size.x - GrayMechDrydockTabStyle.Margin * 2f - GrayMechDrydockTabStyle.CloseButtonReserveRight,
            size.y - GrayMechDrydockTabStyle.Margin * 2f - GrayMechDrydockTabStyle.CloseButtonReserveTop);

        focusPanel.DrawQueue(context, root);
    }
}
