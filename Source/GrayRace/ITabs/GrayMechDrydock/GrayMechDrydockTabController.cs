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
            "Delete saved design \"" + record.label + "\"?",
            delegate
            {
                if (dock.DeleteCurrentDesign())
                {
                    SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                    Messages.Message("Design deleted.", dock, MessageTypeDefOf.PositiveEvent);
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
        Messages.Message("Design updated.", dock, MessageTypeDefOf.PositiveEvent);
    }

    internal void QueueAssemblyBill(Building_GR_Drydock dock)
    {
        if (dock.TryQueueAssemblyBill(out string reason))
        {
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            Messages.Message("Queued mech assembly bill.", dock, MessageTypeDefOf.PositiveEvent);
        }
        else
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            Messages.Message(reason, dock, MessageTypeDefOf.RejectInput);
        }
    }

    internal void LoadPreset(Building_GR_Drydock dock, GRMechPresetDef preset)
    {
        if (preset == null)
        {
            return;
        }

        dock.LoadFromPreset(preset);
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

    internal void SelectSection(GRMechSectionRoleDef role)
    {
        if (role == null)
        {
            return;
        }

        state.SelectSection(role);
        SoundDefOf.Click.PlayOneShotOnCamera();
    }

    internal void SelectSlot(Building_GR_Drydock dock, GrayMechResolvedSlot resolvedSlot, bool openFloatMenu)
    {
        if (resolvedSlot?.slot == null)
        {
            return;
        }

        state.SelectSlot(resolvedSlot);
        state.EnsureCompatibleModuleCache(dock);
        SoundDefOf.Click.PlayOneShotOnCamera();

        if (openFloatMenu)
        {
            OpenSlotFloatMenu(dock, resolvedSlot);
        }
    }

    internal void SetSectionLayout(Building_GR_Drydock dock, GRMechSectionRoleDef role, GRMechSectionLayoutDef layout)
    {
        if (role == null || layout == null)
        {
            return;
        }

        if (dock.SetSectionLayout(role, layout))
        {
            state.SelectSection(role);
            state.InvalidateDraft();
            SoundDefOf.Click.PlayOneShotOnCamera();
        }
    }

    internal void SetModule(Building_GR_Drydock dock, string slotKey, GRMechModuleDef module)
    {
        if (slotKey.NullOrEmpty())
        {
            return;
        }

        if (dock.SetModule(slotKey, module))
        {
            state.InvalidateDraft();
            SoundDefOf.Click.PlayOneShotOnCamera();
        }
    }

    internal void OpenSlotFloatMenu(Building_GR_Drydock dock, GrayMechResolvedSlot resolvedSlot)
    {
        if (dock?.DesignDraft == null || resolvedSlot?.slot == null)
        {
            return;
        }

        state.EnsureCompatibleModuleCache(dock);
        GrayMechDesignUtility.TryGetSelectedModule(dock.DesignDraft, resolvedSlot.slot.key, out GRMechModuleDef currentModule);

        List<FloatMenuOption> options = new()
        {
            new FloatMenuOption("None", delegate
            {
                SetModule(dock, resolvedSlot.slot.key, null);
            })
        };

        for (int i = 0; i < state.CompatibleModules.Count; i++)
        {
            GRMechModuleDef module = state.CompatibleModules[i];
            if (module == null)
            {
                continue;
            }

            bool available = GrayMechDesignUtility.IsResearchAvailable(module);
            string label = module.LabelCap.ToString();
            if (module == currentModule)
            {
                label += " (Installed)";
            }
            else if (!available)
            {
                label += " (Locked)";
            }

            if (available || module == currentModule)
            {
                options.Add(new FloatMenuOption(label, delegate
                {
                    SetModule(dock, resolvedSlot.slot.key, module);
                }));
            }
            else
            {
                options.Add(new FloatMenuOption(label, null));
            }
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }
}
