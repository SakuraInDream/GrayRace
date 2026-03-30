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
    private readonly GrayMechDrydockTabState state;

    internal GrayMechDrydockTabController(GrayMechDrydockTabState state)
    {
        this.state = state;
    }

    internal void DeleteCurrentDesign(Building_GR_Drydock dock)
    {
        GrayMechDesignRecord record = dock.EditingDesignRecord;
        if (record == null)
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            return;
        }

        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
            "移除设计 \"" + record.label + "\"?",
            delegate
            {
                if (dock.DeleteCurrentDesign())
                {
                    SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                    Messages.Message("设计蓝图已删除.", dock, MessageTypeDefOf.PositiveEvent);
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

    internal void SelectSection(GRMechSectionSlotDef sectionSlot)
    {
        if (sectionSlot == null)
        {
            return;
        }

        state.SelectSection(sectionSlot);
        SoundDefOf.Click.PlayOneShotOnCamera();
    }

    internal void SelectSlot(Building_GR_Drydock dock, GRMechResolvedSlot resolvedSlot)
    {
        if (resolvedSlot?.slot == null)
        {
            return;
        }

        state.SelectSlot(resolvedSlot);
        state.EnsureCompatibleModuleCache(dock);
        SoundDefOf.Click.PlayOneShotOnCamera();
    }

    internal void ClearSlotModule(Building_GR_Drydock dock, GRMechResolvedSlot resolvedSlot)
    {
        if (resolvedSlot?.slot == null)
        {
            return;
        }

        if (dock.SetModule(resolvedSlot.sectionSlot, resolvedSlot.slot.key, null))
        {
            state.SelectSlot(resolvedSlot);
            state.InvalidateDraft();
            state.EnsureCompatibleModuleCache(dock);
            SoundDefOf.Click.PlayOneShotOnCamera();
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
            SoundDefOf.Click.PlayOneShotOnCamera();
        }
    }
}
