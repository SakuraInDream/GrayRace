using SD.GrayRace.Mechs;
using SD.GrayRace.ThingClasses;

namespace SD.GrayRace.ITabs;

internal readonly struct GrayMechDrydockTabContext
{
    internal readonly Building_GR_Drydock Dock;
    internal readonly GrayMechDesignSnapshot Draft;
    internal readonly GrayMechDrydockPresenter State;
    internal readonly GrayMechDrydockViewState ViewState;
    internal readonly GrayMechDrydockTabController Controller;

    internal GrayMechDrydockTabContext(Building_GR_Drydock dock, GrayMechDrydockPresenter state, GrayMechDrydockViewState viewState, GrayMechDrydockTabController controller)
    {
        Dock = dock;
        Draft = dock.DesignDraft;
        State = state;
        ViewState = viewState;
        Controller = controller;
    }
}
