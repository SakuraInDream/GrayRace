using System.Collections.Generic;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;

namespace SD.GrayRace.ITabs;

internal interface IGrayMechSectionCanvasHost
{
    GrayMechDesignSnapshot Snapshot { get; }

    bool TryGetSectionSlots(out List<GRMechSectionSlotDef> sectionSlots);

    string GetSectionHeaderText(GRMechSectionSlotDef sectionSlot);

    bool IsSectionSelected(GRMechSectionSlotDef sectionSlot);

    bool SectionOwnsSelectedSlot(GRMechSectionSlotDef sectionSlot);

    bool AllowSectionInteraction { get; }

    void OnSectionHeaderActivated(GRMechSectionSlotDef sectionSlot);

    void PrepareSectionSlots(GRMechSectionSlotDef sectionSlot);

    List<GRMechResolvedSlot> TopSlots { get; }

    List<GRMechResolvedSlot> BottomSlots { get; }

    void PrepareCoreSlots();

    List<GRMechResolvedSlot> CoreSlots { get; }

    bool CoreOwnsSelectedSlot { get; }

    bool AllowCoreBackgroundInteraction { get; }

    void OnCoreBackgroundActivated();

    void ResolveSlotVisuals(GRMechResolvedSlot resolvedSlot, out GRMechModuleDef module, out string tooltip, out bool selected, out bool drawSlotMarker);

    bool AllowSlotInteraction { get; }

    bool AllowSecondarySlotAction { get; }

    void OnSlotActivated(GRMechResolvedSlot resolvedSlot);

    void OnSlotSecondaryActivated(GRMechResolvedSlot resolvedSlot);

    bool TryGetCoreSlotPickerData(GRMechResolvedSlot resolvedSlot, out List<GRMechModuleDef> compatibleModules, out GRMechModuleDef currentModule);

    void OnCoreSlotModuleActivated(GRMechResolvedSlot resolvedSlot, GRMechModuleDef module);
}
