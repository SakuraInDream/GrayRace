using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

public class GrayMechDrydockViewState
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

        // 必须与 SelectSlot 对称。这两个「正在编辑的目标」是互斥的：
        // SelectSlot 会清 SelectedCoreSlotKey，这里就必须清 SelectedSlotKey。
        // 漏掉的话左侧栏会继续显示旧区段槽位的模块列表——托盘开着（核心槽）
        // 的同时还能从左栏拿起笔刷，两个编辑目标并存，属于非法状态。
        SelectedCoreSlotKey = resolvedSlot.slot.key;
        SelectedSlotKey = null;
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
