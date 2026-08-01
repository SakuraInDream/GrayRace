using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.Dialogs;
using SD.GrayRace.Mechs;
using SD.GrayRace.ThingClasses;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.ITabs;

internal sealed class GrayMechDrydockTabController
{
    private readonly GrayMechDrydockPresenter state;

    internal GrayMechDrydockTabController(GrayMechDrydockPresenter state)
    {
        this.state = state;
    }

    internal void DeleteCurrentDesign(Building_GR_Drydock dock)
    {
        DeleteDesign(dock, dock?.EditingDesignRecord);
    }

    internal void DeleteDesign(Building_GR_Drydock dock, GrayMechDesignRecord record)
    {
        if (dock == null || record == null)
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            return;
        }

        int designId = record.id;
        string designLabel = record.label ?? "未命名";
        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
            "删除设计 \"" + designLabel + "\"?\n\n此操作无法撤销。",
            delegate
            {
                if (dock.DeleteDesign(designId))
                {
                    SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                    Messages.Message("已删除设计: " + designLabel, dock, MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    Messages.Message("无法删除设计，该记录可能已不存在。", dock, MessageTypeDefOf.RejectInput);
                }
            },
            destructive: true));
    }

    internal void RenameCurrentDesign(Building_GR_Drydock dock)
    {
        WorldComponent_GrayMechDesignLibrary library = Find.World?.GetComponent<WorldComponent_GrayMechDesignLibrary>();
        GrayMechDesignRecord record = dock.EditingDesignRecord;
        if (record == null || library == null)
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            return;
        }

        Find.WindowStack.Add(new Dialog_RenameGrayMechDesign(record, library));
    }

    internal void SaveAsNewDesign(Building_GR_Drydock dock)
    {
        WorldComponent_GrayMechDesignLibrary library = Find.World?.GetComponent<WorldComponent_GrayMechDesignLibrary>();
        GrayMechDesignRecord record = dock.SaveAsNewDesign();
        if (record == null || library == null)
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            Messages.Message("Unable to create saved design.", dock, MessageTypeDefOf.RejectInput);
            return;
        }

        SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        Find.WindowStack.Add(new Dialog_RenameGrayMechDesign(record, library));
    }

    internal void SaveCurrentDesign(Building_GR_Drydock dock)
    {
        if (!dock.SaveToCurrentDesign(out GrayMechDesignRecord _))
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            Messages.Message("No saved design selected to overwrite.", dock, MessageTypeDefOf.RejectInput);
            return;
        }

        SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        Messages.Message("设计蓝图已更新", dock, MessageTypeDefOf.PositiveEvent);
    }

    internal void SaveDesign(Building_GR_Drydock dock)
    {
        if (dock == null)
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            return;
        }

        if (!dock.SaveDesignByDraftLabel(out GrayMechDesignRecord record, out bool createdNew, out string failureReason))
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            Messages.Message(failureReason.NullOrEmpty() ? "无法保存当前设计。" : failureReason, dock, MessageTypeDefOf.RejectInput);
            return;
        }

        SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        if (createdNew)
        {
            Messages.Message("已保存新设计: " + (record?.label ?? "未命名"), dock, MessageTypeDefOf.PositiveEvent);
        }
        else
        {
            Messages.Message("设计蓝图已更新: " + (record?.label ?? "未命名"), dock, MessageTypeDefOf.PositiveEvent);
        }
    }

    internal void QueueAssemblyOrder(Building_GR_Drydock dock)
    {
        if (dock.TryQueueAssemblyOrder(out string reason))
        {
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            Messages.Message("Added design to construction queue.", dock, MessageTypeDefOf.PositiveEvent);
        }
        else
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            Messages.Message(reason, dock, MessageTypeDefOf.RejectInput);
        }
    }

    internal void CancelQueuedOrder(Building_GR_Drydock dock, int index)
    {
        if (dock.TryCancelQueuedOrder(index, out GrayMechAssemblyOrder removedOrder))
        {
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            Messages.Message("已取消队列项: " + (removedOrder?.Label ?? "Unnamed Order"), dock, MessageTypeDefOf.PositiveEvent);
            return;
        }

        SoundDefOf.ClickReject.PlayOneShotOnCamera();
        Messages.Message("无法取消该建造队列项。", dock, MessageTypeDefOf.RejectInput);
    }

    internal void CancelCurrentOrder(Building_GR_Drydock dock)
    {
        if (dock.TryCancelCurrentOrder(out GrayMechAssemblyOrder removedOrder))
        {
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            Messages.Message("已取消当前建造: " + (removedOrder?.Label ?? "Unnamed Order"), dock, MessageTypeDefOf.PositiveEvent);
            return;
        }

        SoundDefOf.ClickReject.PlayOneShotOnCamera();
        Messages.Message("无法取消当前建造。", dock, MessageTypeDefOf.RejectInput);
    }

    internal void LoadChassis(Building_GR_Drydock dock, GRMechChassisDef chassis)
    {
        if (chassis == null)
        {
            return;
        }

        dock.LoadFromChassis(chassis);
        state.ResetFocusState();
        state.InvalidateDraft();
        SoundDefOf.Click.PlayOneShotOnCamera();
    }

    internal void OpenNewDesignMenu(Building_GR_Drydock dock, List<GRMechChassisDef> chassisOptions)
    {
        if (dock == null || chassisOptions == null || chassisOptions.Count == 0)
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            Messages.Message("当前没有已解锁的底盘。", dock, MessageTypeDefOf.RejectInput);
            return;
        }

        Find.WindowStack.Add(new Dialog_SelectGrayMechChassis(dock, chassisOptions, LoadChassis));
    }

    internal void LoadSavedDesign(Building_GR_Drydock dock, GrayMechDesignRecord design)
    {
        if (design == null)
        {
            return;
        }

        dock.LoadFromLibrary(design);
        state.ResetFocusState();
        state.InvalidateDraft();
        SoundDefOf.Click.PlayOneShotOnCamera();
    }

    internal void OpenSectionLayoutMenu(Building_GR_Drydock dock, GRMechSectionSlotDef sectionSlot)
    {
        if (dock?.DesignDraft?.chassis == null || sectionSlot == null)
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            return;
        }

        state.SelectSection(sectionSlot);
        if (!GRMechSectionLayoutCatalog.TryGetLayouts(dock.DesignDraft.chassis, sectionSlot, out List<GRMechSectionLayoutDef> layouts)
            || layouts == null
            || layouts.Count == 0)
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            Messages.Message("当前区段没有可用布局。", dock, MessageTypeDefOf.RejectInput);
            return;
        }

        SoundDefOf.Click.PlayOneShotOnCamera();
        Find.WindowStack.Add(new Dialog_SelectGrayMechSectionLayout(dock, sectionSlot, layouts, SetSectionLayout));
    }

    internal void SelectSlot(Building_GR_Drydock dock, GRMechResolvedSlot resolvedSlot)
    {
        if (resolvedSlot?.slot == null)
        {
            return;
        }

        if (resolvedSlot.sectionSlot == null)
        {
            state.SelectCoreSlot(resolvedSlot);
            state.EnsureCoreCompatibleModuleCache(dock);
        }
        else
        {
            state.SelectSlot(resolvedSlot);
            state.EnsureCompatibleModuleCache(dock);
        }

        SoundDefOf.Click.PlayOneShotOnCamera();
    }

    internal void ArmModule(GRMechModuleDef module)
    {
        if (module == null)
        {
            return;
        }

        state.SetArmedModule(module);
        SoundDefOf.Click.PlayOneShotOnCamera();
    }

    internal bool TryApplyArmedModuleToSlot(Building_GR_Drydock dock, GRMechResolvedSlot resolvedSlot)
    {
        GRMechModuleDef armedModule = state.ArmedModule;
        if (armedModule == null || resolvedSlot?.slot == null)
        {
            return false;
        }

        if (resolvedSlot.sectionSlot == null
            || !GrayMechDesignUtility.TryResolveBrushModuleForSlot(dock.DesignDraft?.chassis, resolvedSlot.slot, armedModule, out GRMechModuleDef moduleToInstall))
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            return true;
        }

        if (!dock.SetModule(resolvedSlot.sectionSlot, resolvedSlot.slot.key, moduleToInstall))
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            return true;
        }

        state.InvalidateDraft();
        state.EnsureCompatibleModuleCache(dock);
        SoundDefOf.Click.PlayOneShotOnCamera();
        return true;
    }

    internal bool CancelModuleBrush(bool playSound = false)
    {
        if (!state.HasArmedModule)
        {
            return false;
        }

        state.ClearArmedModule();
        if (playSound)
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        return true;
    }

    internal bool CloseFocusedSlotUi(bool playSound = false)
    {
        if (!state.ClearFocusedSlotSelection())
        {
            return false;
        }

        if (playSound)
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        return true;
    }

    internal void ClearDesign(Building_GR_Drydock dock)
    {
        if (dock == null)
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            return;
        }

        if (!dock.ClearSectionModules())
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            Messages.Message("当前设计没有可清除的区段模块。", dock, MessageTypeDefOf.RejectInput);
            return;
        }

        state.ClearArmedModule();
        state.InvalidateDraft();
        SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        Messages.Message("已清除当前设计中的区段模块。", dock, MessageTypeDefOf.PositiveEvent);
    }

    internal void ClearSlotModule(Building_GR_Drydock dock, GRMechResolvedSlot resolvedSlot)
    {
        if (resolvedSlot?.slot == null)
        {
            return;
        }

        if (dock.SetModule(resolvedSlot.sectionSlot, resolvedSlot.slot.key, null))
        {
            state.InvalidateDraft();
            if (resolvedSlot.sectionSlot == null)
            {
                state.ClearCoreSelection();
            }
            else
            {
                state.SelectSlot(resolvedSlot);
                state.EnsureCompatibleModuleCache(dock);
            }

            SoundDefOf.Click.PlayOneShotOnCamera();
        }
        else
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
        }
    }

    internal void SetSectionLayout(Building_GR_Drydock dock, GRMechSectionSlotDef sectionSlot, GRMechSectionLayoutDef layout)
    {
        if (sectionSlot == null || layout == null)
        {
            return;
        }

        if (dock.SetSectionLayout(sectionSlot, layout))
        {
            state.SelectSection(sectionSlot);
            state.InvalidateDraft();
            SoundDefOf.Click.PlayOneShotOnCamera();
        }
    }

    internal void SetModule(Building_GR_Drydock dock, GRMechSectionSlotDef sectionSlot, string slotKey, GRMechModuleDef module)
    {
        if (slotKey.NullOrEmpty())
        {
            return;
        }

        if (dock.SetModule(sectionSlot, slotKey, module))
        {
            state.InvalidateDraft();
            if (sectionSlot == null)
            {
                state.ClearCoreSelection();
            }
            else
            {
                state.EnsureCompatibleModuleCache(dock);
            }

            SoundDefOf.Click.PlayOneShotOnCamera();
        }
    }
}
