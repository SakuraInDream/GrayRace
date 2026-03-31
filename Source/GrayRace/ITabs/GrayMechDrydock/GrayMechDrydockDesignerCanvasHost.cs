using System.Collections.Generic;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using Verse;

namespace SD.GrayRace.ITabs;

internal sealed class GrayMechDrydockDesignerCanvasHost : IGrayMechSectionCanvasHost
{
    private GrayMechDrydockTabContext context;

    internal void Bind(GrayMechDrydockTabContext context)
    {
        this.context = context;
    }

    public GrayMechDesignSnapshot Snapshot => context.Draft;

    public bool AllowCoreBackgroundInteraction => true;

    public bool AllowSectionInteraction => true;

    public bool AllowSecondarySlotAction => true;

    public bool AllowSlotInteraction => true;

    public List<GRMechResolvedSlot> BottomSlots => context.State.SupportSlotBuffer;

    public bool CoreOwnsSelectedSlot => !context.State.SelectedCoreSlotKey.NullOrEmpty();

    public List<GRMechResolvedSlot> CoreSlots => context.State.RequiredSlotBuffer;

    public List<GRMechResolvedSlot> TopSlots => context.State.WeaponSlotBuffer;

    public string GetSectionHeaderText(GRMechSectionSlotDef sectionSlot)
    {
        if (GrayMechDesignUtility.TryGetSelectedLayout(context.Draft, sectionSlot, out GRMechSectionLayoutDef layout))
        {
            string layoutLabel = layout?.LabelCap.ToString();
            if (!layoutLabel.NullOrEmpty())
            {
                return layoutLabel;
            }
        }

        return sectionSlot?.LabelCap.ToString() ?? "Unknown";
    }

    public bool IsSectionSelected(GRMechSectionSlotDef sectionSlot)
    {
        return GRMechSectionSlotUtility.Matches(context.State.SelectedSectionSlot, sectionSlot) && context.State.SelectedSlotKey.NullOrEmpty();
    }

    public void OnCoreBackgroundActivated()
    {
        if (context.State.RequiredSlotBuffer.Count > 0)
        {
            context.Controller.SelectSlot(context.Dock, context.State.RequiredSlotBuffer[0]);
        }
    }

    public void OnSectionHeaderActivated(GRMechSectionSlotDef sectionSlot)
    {
        context.Controller.SelectSection(sectionSlot);
    }

    public void OnSlotActivated(GRMechResolvedSlot resolvedSlot)
    {
        if (context.Controller.TryApplyArmedModuleToSlot(context.Dock, resolvedSlot))
        {
            return;
        }

        context.Controller.SelectSlot(context.Dock, resolvedSlot);
    }

    public void OnSlotSecondaryActivated(GRMechResolvedSlot resolvedSlot)
    {
        if (context.Controller.CancelModuleBrush(playSound: true))
        {
            return;
        }

        if (resolvedSlot == null)
        {
            context.Controller.CloseFocusedSlotUi(playSound: true);
            return;
        }

        if (GrayMechDesignUtility.TryGetSelectedModule(context.Draft, resolvedSlot, out GRMechModuleDef module) && module != null)
        {
            context.Controller.ClearSlotModule(context.Dock, resolvedSlot);
        }
    }

    public void PrepareCoreSlots()
    {
        context.State.PrepareRequiredSlotBuffer();
    }

    public void PrepareSectionSlots(GRMechSectionSlotDef sectionSlot)
    {
        context.State.PrepareSectionSlotBuffers(sectionSlot);
    }

    public void ResolveSlotVisuals(GRMechResolvedSlot resolvedSlot, out GRMechModuleDef module, out string tooltip, out bool selected, out bool drawSlotMarker)
    {
        GrayMechDesignUtility.TryGetSelectedModule(context.Draft, resolvedSlot, out module);
        tooltip = GrayMechDrydockTabText.BuildSlotTooltip(context.Draft?.chassis, resolvedSlot, module);
        selected = resolvedSlot?.sectionSlot == null
            ? context.State.SelectedCoreSlotKey == resolvedSlot?.slot?.key
            : GRMechSectionSlotUtility.Matches(context.State.SelectedSectionSlot, resolvedSlot.sectionSlot) && context.State.SelectedSlotKey == resolvedSlot.slot?.key;
        drawSlotMarker = true;
    }

    public bool SectionOwnsSelectedSlot(GRMechSectionSlotDef sectionSlot)
    {
        return context.State.SectionOwnsSelectedSlot(sectionSlot);
    }

    public bool TryGetSectionSlots(out List<GRMechSectionSlotDef> sectionSlots)
    {
        return GRMechSectionLayoutCatalog.TryGetSectionSlots(context.Draft?.chassis, out sectionSlots);
    }

    public bool TryGetCoreSlotPickerData(GRMechResolvedSlot resolvedSlot, out List<GRMechModuleDef> compatibleModules, out GRMechModuleDef currentModule)
    {
        compatibleModules = null;
        currentModule = null;
        if (resolvedSlot?.slot == null
            || resolvedSlot.sectionSlot != null
            || context.State.SelectedCoreSlotKey != resolvedSlot.slot.key)
        {
            return false;
        }

        GrayMechDesignUtility.TryGetSelectedModule(context.Draft, resolvedSlot, out currentModule);
        compatibleModules = context.State.CoreCompatibleModules;
        return compatibleModules is { Count: > 0 } || currentModule != null;
    }

    public void OnCoreSlotModuleActivated(GRMechResolvedSlot resolvedSlot, GRMechModuleDef module)
    {
        if (resolvedSlot?.slot == null)
        {
            return;
        }

        context.Controller.SetModule(context.Dock, resolvedSlot.sectionSlot, resolvedSlot.slot.key, module);
    }
}
