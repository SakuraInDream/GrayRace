using RimWorld;
using SD.GrayRace.ThingClasses;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

[StaticConstructorOnStartup]
public class ITab_GrayMechDrydockQueue : ITab
{
    /// <summary>
    /// 页签尺寸。宽度对齐原版建筑 ITab（ITab_Bills.WinSize.x = 420，反编译实证），高度沿用原设计值 620。
    /// </summary>
    private static readonly Vector2 TabSize = new(420f, 620f);

    private readonly GrayMechDrydockViewState viewState = new();
    private readonly GrayMechDrydockPresenter state = new();
    private readonly GrayMechDrydockTabController controller;
    private readonly GrayMechDrydockFocusPanel focusPanel = new();

    public ITab_GrayMechDrydockQueue()
    {
        controller = new GrayMechDrydockTabController(state);
        size = TabSize;
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

        state.EnsureCaches(dock, viewState);
        GrayMechDrydockTabContext context = new(dock, state, viewState, controller);

        Rect root = new Rect(
            GrayMechDrydockTabStyle.Margin,
            GrayMechDrydockTabStyle.Margin + GrayMechDrydockTabStyle.CloseButtonReserveTop,
            size.x - GrayMechDrydockTabStyle.Margin * 2f - GrayMechDrydockTabStyle.CloseButtonReserveRight,
            size.y - GrayMechDrydockTabStyle.Margin * 2f - GrayMechDrydockTabStyle.CloseButtonReserveTop);

        focusPanel.DrawQueue(context, root);
    }
}
