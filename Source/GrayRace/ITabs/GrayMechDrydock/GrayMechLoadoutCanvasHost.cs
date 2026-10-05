using System.Collections.Generic;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using Verse;

namespace SD.GrayRace.ITabs;

public class GrayMechLoadoutCanvasHost : IGrayMechSectionCanvasHost
{
    private readonly ITab_GrayMechLoadout owner;
    private GrayMechDesignSnapshot snapshot;

    internal GrayMechLoadoutCanvasHost(ITab_GrayMechLoadout owner)
    {
        this.owner = owner;
    }

    internal void Bind(GrayMechDesignSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }

    public GrayMechDesignSnapshot Snapshot => snapshot;

    public bool AllowCoreBackgroundInteraction => false;

    public bool AllowSectionInteraction => false;

    public bool AllowSecondarySlotAction => false;

    public bool AllowSlotInteraction => false;

    public List<GRMechResolvedSlot> BottomSlots => owner.SupportSlots;

    public bool CoreOwnsSelectedSlot => false;

    public List<GRMechResolvedSlot> CoreSlots => owner.RequiredSlots;

    public int MaximumBottomSlotCount => owner.MaximumSupportSlotCount;

    public int MaximumTopSlotCount => owner.MaximumWeaponSlotCount;

    public List<GRMechResolvedSlot> TopSlots => owner.WeaponSlots;

    public string GetSectionHeaderText(GRMechSectionSlotDef sectionSlot)
    {
        return owner.GetSectionHeaderTextForCanvas(snapshot, sectionSlot);
    }

    public bool IsSectionSelected(GRMechSectionSlotDef sectionSlot)
    {
        return false;
    }

    public void OnCoreBackgroundActivated()
    {
    }

    public void OnSectionHeaderActivated(GRMechSectionSlotDef sectionSlot)
    {
    }

    public void OnSlotActivated(GRMechResolvedSlot resolvedSlot)
    {
    }

    public void OnSlotSecondaryActivated(GRMechResolvedSlot resolvedSlot)
    {
    }

    public void PrepareCoreSlots()
    {
        owner.PrepareCoreSlotsForCanvas();
    }

    public void PrepareSectionSlots(GRMechSectionSlotDef sectionSlot)
    {
        owner.PrepareSectionSlotsForCanvas(sectionSlot);
    }

    public void ResolveSlotVisuals(GRMechResolvedSlot resolvedSlot, out GRMechModuleDef module, out string tooltip, out bool selected, out bool drawSlotMarker)
    {
        owner.ResolveSlotVisualsForCanvas(resolvedSlot, out module, out tooltip);
        selected = false;
        drawSlotMarker = false;
    }

    public bool SectionOwnsSelectedSlot(GRMechSectionSlotDef sectionSlot)
    {
        return false;
    }

    public bool TryGetSectionSlots(out List<GRMechSectionSlotDef> sectionSlots)
    {
        return GRMechSectionLayoutCatalog.TryGetSectionSlots(snapshot?.chassis, out sectionSlots);
    }

    public bool TryGetCoreSlotPickerData(GRMechResolvedSlot resolvedSlot, out List<GRMechModuleDef> compatibleModules, out GRMechModuleDef currentModule)
    {
        compatibleModules = null;
        currentModule = null;
        return false;
    }

    public void OnCoreSlotModuleActivated(GRMechResolvedSlot resolvedSlot, GRMechModuleDef module)
    {
    }
}
