using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using SD.GrayRace.ThingClasses;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

internal sealed class GrayMechDrydockTabState
{
    private Building_GR_Drydock cachedDock;
    private int cachedLibraryVersion = -1;
    private int cachedDraftRevision = -1;
    private int cachedCompatibleRevision = -1;
    private string cachedCompatibleSlotKey;

    internal GRMechSectionRoleDef SelectedSectionRole;
    internal string SelectedSlotKey;

    internal readonly List<GRMechPresetDef> PresetCache = new();
    internal readonly List<GrayMechDesignRecord> DesignCache = new();
    internal readonly List<GrayMechResolvedSlot> SlotCache = new();
    internal readonly List<GRMechModuleDef> CompatibleModules = new();
    internal readonly List<ThingDefCountClass> CostCache = new();
    internal readonly StringBuilder TextBuilder = new();
    internal readonly List<GrayMechResolvedSlot> WeaponSlotBuffer = new();
    internal readonly List<GrayMechResolvedSlot> SmallUtilitySlotBuffer = new();
    internal readonly List<GrayMechResolvedSlot> MediumUtilitySlotBuffer = new();
    internal readonly List<GrayMechResolvedSlot> LargeUtilitySlotBuffer = new();
    internal readonly List<GrayMechResolvedSlot> AuxSlotBuffer = new();

    internal Vector2 PresetScrollPosition = Vector2.zero;
    internal Vector2 DesignScrollPosition = Vector2.zero;
    internal Vector2 FocusScrollPosition = Vector2.zero;
    internal Vector2 LibraryScrollPosition = Vector2.zero;

    internal string CachedCostSummary = string.Empty;
    internal string CachedModuleSummary = string.Empty;
    internal string CachedSectionSummary = string.Empty;
    internal int CachedFilledSlotCount;
    internal int CachedTotalSlotCount;
    internal int CachedFixedWorkTicks;
    internal float CachedBandwidthCost;
    internal float CachedMoveSpeed;
    internal float CachedArmorSharp;

    internal void EnsureCaches(Building_GR_Drydock dock)
    {
        if (cachedDock != dock)
        {
            cachedDock = dock;
            cachedLibraryVersion = -1;
            cachedDraftRevision = -1;
            cachedCompatibleRevision = -1;
            cachedCompatibleSlotKey = null;
            BuildPresetCache();
            ResetFocusState();
        }

        WorldComponent_GrayMechDesignLibrary library = Find.World?.GetComponent<WorldComponent_GrayMechDesignLibrary>();
        if (library != null)
        {
            if (library.Version != cachedLibraryVersion)
            {
                BuildDesignCache(library);
                cachedLibraryVersion = library.Version;
                dock.SyncDraftLabelFromSavedDesign();
            }
        }
        else if (cachedLibraryVersion != -1 || DesignCache.Count > 0)
        {
            DesignCache.Clear();
            cachedLibraryVersion = -1;
        }

        if (dock.DesignRevision != cachedDraftRevision)
        {
            GrayMechDesignUtility.EnsureSnapshotDefaults(dock.DesignDraft);
            GrayMechDesignUtility.FillResolvedSlots(dock.DesignDraft, SlotCache);
            RebuildDerivedCache(dock);
            cachedDraftRevision = dock.DesignRevision;
        }

        EnsureFocusState(dock);
        EnsureCompatibleModuleCache(dock);
    }

    internal void ResetFocusState()
    {
        SelectedSectionRole = null;
        SelectedSlotKey = null;
        FocusScrollPosition = Vector2.zero;
    }

    private void EnsureFocusState(Building_GR_Drydock dock)
    {
        GrayMechDesignSnapshot draft = dock.DesignDraft;
        if (draft?.chassis?.sections == null || draft.chassis.sections.Count == 0)
        {
            SelectedSectionRole = null;
            SelectedSlotKey = null;
            return;
        }

        if (!IsSectionPresent(draft, SelectedSectionRole))
        {
            SelectedSectionRole = draft.chassis.sections[0]?.role;
        }

        if (!SelectedSlotKey.NullOrEmpty())
        {
            if (TryGetResolvedSlot(SelectedSlotKey, out GrayMechResolvedSlot resolvedSlot))
            {
                SelectedSectionRole = resolvedSlot.role;
            }
            else
            {
                SelectedSlotKey = null;
            }
        }
    }

    internal void EnsureCompatibleModuleCache(Building_GR_Drydock dock)
    {
        if (SelectedSlotKey.NullOrEmpty())
        {
            CompatibleModules.Clear();
            cachedCompatibleSlotKey = null;
            cachedCompatibleRevision = dock.DesignRevision;
            return;
        }

        if (cachedCompatibleRevision == dock.DesignRevision && cachedCompatibleSlotKey == SelectedSlotKey)
        {
            return;
        }

        if (TryGetResolvedSlot(SelectedSlotKey, out GrayMechResolvedSlot resolvedSlot))
        {
            GrayMechDesignUtility.FillCompatibleModules(dock.DesignDraft, resolvedSlot.slot, CompatibleModules);
        }
        else
        {
            CompatibleModules.Clear();
        }

        cachedCompatibleSlotKey = SelectedSlotKey;
        cachedCompatibleRevision = dock.DesignRevision;
    }

    private void RebuildDerivedCache(Building_GR_Drydock dock)
    {
        GrayMechDesignSnapshot draft = dock.DesignDraft;
        GrayMechDesignUtility.BuildCostList(draft, CostCache);
        CachedCostSummary = GrayMechDrydockTabText.BuildCostSummary(CostCache, TextBuilder);
        CachedModuleSummary = GrayMechDesignUtility.BuildModuleSummary(draft);
        CachedSectionSummary = GrayMechDrydockTabText.BuildSectionSummary(draft, TextBuilder);

        CachedTotalSlotCount = SlotCache.Count;
        CachedFilledSlotCount = 0;
        for (int i = 0; i < SlotCache.Count; i++)
        {
            if (SlotCache[i]?.slot == null)
            {
                continue;
            }

            if (GrayMechDesignUtility.TryGetSelectedModule(draft, SlotCache[i].slot.key, out GRMechModuleDef _))
            {
                CachedFilledSlotCount++;
            }
        }

        ThingDef producedRace = draft?.chassis?.ProducedRace;
        CachedFixedWorkTicks = draft?.chassis?.fixedWorkTicks ?? 0;
        CachedBandwidthCost = producedRace?.GetStatValueAbstract(StatDefOf.BandwidthCost) ?? 0f;
        CachedMoveSpeed = producedRace?.GetStatValueAbstract(StatDefOf.MoveSpeed) ?? 0f;
        CachedArmorSharp = producedRace?.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp) ?? 0f;
    }

    private void BuildPresetCache()
    {
        PresetCache.Clear();
        List<GRMechPresetDef> presets = DefDatabase<GRMechPresetDef>.AllDefsListForReading;
        for (int i = 0; i < presets.Count; i++)
        {
            GRMechPresetDef preset = presets[i];
            if (preset != null)
            {
                PresetCache.Add(preset);
            }
        }

        PresetCache.Sort(GrayMechDrydockTabText.ComparePresetDefs);
    }

    private void BuildDesignCache(WorldComponent_GrayMechDesignLibrary library)
    {
        DesignCache.Clear();
        if (library?.Designs == null)
        {
            return;
        }

        for (int i = 0; i < library.Designs.Count; i++)
        {
            GrayMechDesignRecord record = library.Designs[i];
            if (record != null)
            {
                DesignCache.Add(record);
            }
        }

        DesignCache.Sort(GrayMechDrydockTabText.CompareSavedDesigns);
    }

    internal bool TryGetResolvedSlot(string slotKey, out GrayMechResolvedSlot resolvedSlot)
    {
        if (!slotKey.NullOrEmpty())
        {
            for (int i = 0; i < SlotCache.Count; i++)
            {
                GrayMechResolvedSlot current = SlotCache[i];
                if (current?.slot != null && current.slot.key == slotKey)
                {
                    resolvedSlot = current;
                    return true;
                }
            }
        }

        resolvedSlot = null;
        return false;
    }

    private static bool IsSectionPresent(GrayMechDesignSnapshot draft, GRMechSectionRoleDef role)
    {
        if (draft?.chassis?.sections == null || role == null)
        {
            return false;
        }

        for (int i = 0; i < draft.chassis.sections.Count; i++)
        {
            if (draft.chassis.sections[i]?.role == role)
            {
                return true;
            }
        }

        return false;
    }

    internal void InvalidateDraft()
    {
        cachedDraftRevision = -1;
        cachedCompatibleRevision = -1;
        cachedCompatibleSlotKey = null;
    }

    internal float GetTopBarHeight(Building_GR_Drydock dock, float width)
    {
        GrayMechDesignSnapshot draft = dock.DesignDraft;
        string draftName = draft?.designLabel ?? "No design";
        string chassisName = draft?.chassis?.LabelCap.ToString() ?? "No chassis";
        string titleText = "<b>Ship Designer</b>    " + draftName;
        string subText = "Chassis: " + chassisName + "    Source: " + GrayMechDrydockTabText.GetSourceLabel(dock);
        float textWidth = width - 540f;
        float titleHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(titleText, textWidth, GameFont.Small);
        float subHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(subText, textWidth, GameFont.Small);
        return Mathf.Max(GrayMechDrydockTabStyle.MinTopBarHeight, 10f + titleHeight + 2f + subHeight + 10f + 30f + 10f);
    }

    internal float GetBottomBarHeight(Building_GR_Drydock dock, float width)
    {
        return GrayMechDrydockTabStyle.MinBottomBarHeight;
    }

    internal float GetDesignerStripHeight(GrayMechDesignSnapshot draft, float width)
    {
        List<GRMechChassisSectionDef> sections = draft?.chassis?.sections;
        if (sections == null || sections.Count == 0)
        {
            return GrayMechDrydockTabStyle.DesignerStripMinHeight;
        }

        return GrayMechDrydockTabStyle.DesignerStripMinHeight;
    }

    internal float GetLayoutOptionHeight(GRMechSectionLayoutDef layout, float width)
    {
        float leftWidth = width - 90f;
        string title = "<b>" + layout.LabelCap + "</b>";
        string description = layout.description ?? string.Empty;
        string slotsText = "Slots: " + GrayMechDrydockTabText.BuildLayoutSlotExpression(layout, TextBuilder);
        return 8f
               + GrayMechDrydockTabText.MeasureWrappedTextHeight(title, leftWidth, GameFont.Small)
               + 2f
               + GrayMechDrydockTabText.MeasureWrappedTextHeight(description, leftWidth, GameFont.Small)
               + 2f
               + GrayMechDrydockTabText.MeasureWrappedTextHeight(slotsText, leftWidth, GameFont.Small)
               + 8f;
    }

    internal float GetModuleOptionHeight(GRMechModuleDef module, float width)
    {
        float leftWidth = width - 84f;
        string title = "<b>" + (module?.LabelCap.ToString() ?? "Empty Slot") + "</b>";
        string description = module?.description ?? "Remove the installed module from this slot.";
        string cost = module == null ? "Cost: None" : GrayMechDrydockTabText.BuildModuleCostSummary(module, TextBuilder);
        return 8f
               + GrayMechDrydockTabText.MeasureWrappedTextHeight(title, leftWidth, GameFont.Small)
               + 2f
               + GrayMechDrydockTabText.MeasureWrappedTextHeight(description, leftWidth, GameFont.Small)
               + 2f
               + GrayMechDrydockTabText.MeasureWrappedTextHeight(cost, leftWidth, GameFont.Small)
               + 8f;
    }

    internal void SelectSection(GRMechSectionRoleDef role)
    {
        if (role == null)
        {
            return;
        }

        SelectedSectionRole = role;
        SelectedSlotKey = null;
        FocusScrollPosition = Vector2.zero;
    }

    internal void SelectSlot(GrayMechResolvedSlot resolvedSlot)
    {
        if (resolvedSlot?.slot == null)
        {
            return;
        }

        SelectedSectionRole = resolvedSlot.role;
        SelectedSlotKey = resolvedSlot.slot.key;
        FocusScrollPosition = Vector2.zero;
        cachedCompatibleRevision = -1;
        cachedCompatibleSlotKey = null;
    }

    internal void PrepareSectionSlotBuffers(GRMechSectionRoleDef role)
    {
        WeaponSlotBuffer.Clear();
        SmallUtilitySlotBuffer.Clear();
        MediumUtilitySlotBuffer.Clear();
        LargeUtilitySlotBuffer.Clear();
        AuxSlotBuffer.Clear();

        for (int i = 0; i < SlotCache.Count; i++)
        {
            GrayMechResolvedSlot slot = SlotCache[i];
            if (slot?.role != role || slot.slot == null)
            {
                continue;
            }

            string typeName = slot.slot.slotType?.defName ?? string.Empty;
            string sizeName = slot.slot.slotSize?.defName ?? string.Empty;
            if (typeName.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                WeaponSlotBuffer.Add(slot);
                continue;
            }

            if (typeName.IndexOf("Aux", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AuxSlotBuffer.Add(slot);
                continue;
            }

            if (typeName.IndexOf("Utility", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (sizeName.IndexOf("_M", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MediumUtilitySlotBuffer.Add(slot);
                }
                else if (sizeName.IndexOf("_L", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LargeUtilitySlotBuffer.Add(slot);
                }
                else
                {
                    SmallUtilitySlotBuffer.Add(slot);
                }
            }
        }
    }

    internal int GetRenderedSlotGroupCount()
    {
        int count = 0;
        if (WeaponSlotBuffer.Count > 0) count++;
        if (SmallUtilitySlotBuffer.Count > 0) count++;
        if (MediumUtilitySlotBuffer.Count > 0) count++;
        if (LargeUtilitySlotBuffer.Count > 0) count++;
        if (AuxSlotBuffer.Count > 0) count++;
        return count;
    }

    internal bool SectionOwnsSelectedSlot(GRMechSectionRoleDef role)
    {
        if (SelectedSlotKey.NullOrEmpty())
        {
            return false;
        }

        return TryGetResolvedSlot(SelectedSlotKey, out GrayMechResolvedSlot resolvedSlot) && resolvedSlot.role == role;
    }
}
