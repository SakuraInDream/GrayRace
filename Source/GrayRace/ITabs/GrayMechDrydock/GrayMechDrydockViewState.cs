using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

internal sealed class GrayMechDrydockViewState
{
    internal GRMechSectionSlotDef SelectedSectionSlot;
    internal string SelectedSlotKey;
    internal string SelectedCoreSlotKey;
    internal GRMechModuleDef ArmedModule;
    internal bool ShowObsoleteModules;
    internal bool HasArmedModule => ArmedModule != null;

    internal Vector2 FocusScrollPosition = Vector2.zero;
    internal Vector2 SummaryScrollPosition = Vector2.zero;
    internal Vector2 QueueScrollPosition = Vector2.zero;
    internal Vector2 LibraryScrollPosition = Vector2.zero;

    internal void ResetFocusState()
    {
        SelectedSectionSlot = null;
        SelectedSlotKey = null;
        SelectedCoreSlotKey = null;
        ArmedModule = null;
        FocusScrollPosition = Vector2.zero;
    }

    internal void SetArmedModule(GRMechModuleDef module)
    {
        ArmedModule = module;
    }

    internal void ClearArmedModule()
    {
        ArmedModule = null;
    }

    internal void SetShowObsoleteModules(bool value)
    {
        if (ShowObsoleteModules == value)
        {
            return;
        }

        ShowObsoleteModules = value;
    }

    internal void SelectSection(GRMechSectionSlotDef sectionSlot)
    {
        if (sectionSlot == null)
        {
            return;
        }

        SelectedCoreSlotKey = null;
        SelectedSectionSlot = sectionSlot;
        SelectedSlotKey = null;
        FocusScrollPosition = Vector2.zero;
    }

    internal void SelectSlot(GRMechResolvedSlot resolvedSlot)
    {
        if (resolvedSlot?.slot == null)
        {
            return;
        }

        SelectedCoreSlotKey = null;
        SelectedSectionSlot = resolvedSlot.sectionSlot;
        SelectedSlotKey = resolvedSlot.slot.key;
        FocusScrollPosition = Vector2.zero;
    }

    internal void SelectCoreSlot(GRMechResolvedSlot resolvedSlot)
    {
        if (resolvedSlot?.slot == null || resolvedSlot.sectionSlot != null)
        {
            return;
        }

        SelectedCoreSlotKey = resolvedSlot.slot.key;
    }

    internal void ClearCoreSelection()
    {
        SelectedCoreSlotKey = null;
    }

    internal bool ClearFocusedSlotSelection()
    {
        bool hadSelection = !SelectedSlotKey.NullOrEmpty() || !SelectedCoreSlotKey.NullOrEmpty();
        if (!hadSelection)
        {
            return false;
        }

        SelectedSlotKey = null;
        SelectedCoreSlotKey = null;
        FocusScrollPosition = Vector2.zero;
        return true;
    }
}
