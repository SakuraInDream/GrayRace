using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using SD.GrayRace;
using SD.GrayRace.Defs;
using SD.GrayRace.Mechs;
using SD.GrayRace.ThingClasses;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

internal sealed class GrayMechLibraryGroup
{
    internal GRMechChassisDef Chassis;
    internal readonly List<GrayMechDesignRecord> Designs = new();

    internal bool ShowChassisCard => Chassis != null && Designs.Count == 0;

    internal int VisibleCardCount => ShowChassisCard ? 1 : Designs.Count;
}

internal sealed class GrayMechDrydockTabState
{
    private Building_GR_Drydock cachedDock;
    private int cachedLibraryVersion = -1;
    private int cachedDraftRevision = -1;
    private int cachedCompatibleRevision = -1;
    private int cachedCoreCompatibleRevision = -1;
    private int cachedVisibleChassisHash = int.MinValue;
    private GRMechSectionSlotDef cachedCompatibleSectionSlot;
    private string cachedCompatibleSlotKey;
    private bool cachedCompatibleShowObsolete;
    private string cachedCoreCompatibleSlotKey;

    internal GRMechSectionSlotDef SelectedSectionSlot;
    internal string SelectedSlotKey;
    internal string SelectedCoreSlotKey;
    internal GRMechModuleDef ArmedModule;
    internal bool ShowObsoleteModules;
    internal bool HasArmedModule => ArmedModule != null;

    internal readonly List<GRMechChassisDef> ChassisCache = new();
    internal readonly List<GrayMechDesignRecord> DesignCache = new();
    internal readonly List<GrayMechLibraryGroup> LibraryGroups = new();
    internal readonly List<GRMechResolvedSlot> SlotCache = new();
    internal readonly List<GRMechModuleDef> CompatibleModules = new();
    internal readonly List<GRMechModuleDef> CoreCompatibleModules = new();
    internal readonly List<ThingDefCountClass> CostCache = new();
    internal readonly StringBuilder TextBuilder = new();
    internal readonly List<GRMechResolvedSlot> WeaponSlotBuffer = new();
    internal readonly List<GRMechResolvedSlot> SmallUtilitySlotBuffer = new();
    internal readonly List<GRMechResolvedSlot> MediumUtilitySlotBuffer = new();
    internal readonly List<GRMechResolvedSlot> LargeUtilitySlotBuffer = new();
    internal readonly List<GRMechResolvedSlot> AuxSlotBuffer = new();
    internal readonly List<GRMechResolvedSlot> SupportSlotBuffer = new();
    internal readonly List<GRMechResolvedSlot> SectionSlotBuffer = new();
    internal readonly List<GRMechResolvedSlot> RequiredSlotBuffer = new();

    internal Vector2 FocusScrollPosition = Vector2.zero;
    internal Vector2 SummaryScrollPosition = Vector2.zero;
    internal Vector2 QueueScrollPosition = Vector2.zero;
    internal Vector2 LibraryScrollPosition = Vector2.zero;

    internal string CachedCostSummary = string.Empty;
    internal int CachedFilledSlotCount;
    internal int CachedTotalSlotCount;
    internal int CachedFixedWorkTicks;
    internal int CachedPowerGeneration;
    internal int CachedPowerConsumption;
    internal int CachedPowerNet;
    internal float CachedBandwidthCost;
    internal float CachedMoveSpeed;
    internal float CachedArmorSharp;
    internal float CachedArmorBlunt;
    internal float CachedArmorHeat;
    internal float CachedShieldEnergyMax;
    internal float CachedShieldRechargeRate;
    internal float CachedRangedCooldownFactor;
    internal float CachedShootingAccuracyPawn;
    internal float CachedLongAccuracyFactor;
    internal bool CachedCanQueueOrder;
    internal string CachedQueueReason = string.Empty;
    internal bool CachedHasActiveOrder;
    internal string CachedProductionStatus = string.Empty;
    internal string CachedBuildTimeLabel = string.Empty;
    internal string CachedQueueCountLabel = "0";
    internal float CachedLibraryMaxCardHeight = GrayMechDrydockTabStyle.LibraryCardHeight;

    internal void EnsureCaches(Building_GR_Drydock dock)
    {
        if (cachedDock != dock)
        {
            cachedDock = dock;
            cachedLibraryVersion = -1;
            cachedDraftRevision = -1;
            cachedCompatibleRevision = -1;
            cachedCoreCompatibleRevision = -1;
            cachedVisibleChassisHash = int.MinValue;
            cachedCompatibleSectionSlot = null;
            cachedCompatibleSlotKey = null;
            cachedCompatibleShowObsolete = ShowObsoleteModules;
            cachedCoreCompatibleSlotKey = null;
            ResetFocusState();
            SummaryScrollPosition = Vector2.zero;
            QueueScrollPosition = Vector2.zero;
        }

        int visibleChassisHash = GetVisibleChassisHash();
        if (visibleChassisHash != cachedVisibleChassisHash)
        {
            BuildChassisCache();
            cachedVisibleChassisHash = visibleChassisHash;
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
            RebuildLibraryGroupCache();
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
        EnsureCoreCompatibleModuleCache(dock);
        CachedCanQueueOrder = dock.CanQueueAssemblyOrder(out CachedQueueReason);
        CachedHasActiveOrder = dock.HasActiveOrder;
        CachedProductionStatus = dock.HasActiveOrder
            ? dock.CurrentOrderStatus + ": " + dock.CurrentOrder.Label + (dock.CanProgressNow ? "  " + dock.CurrentOrderTicksRemaining.ToStringTicksToPeriod() : string.Empty)
            : string.Empty;
        CachedBuildTimeLabel = dock.HasActiveOrder
            ? dock.CurrentOrderTicksRemaining.ToStringTicksToPeriod() + " / " + dock.CurrentOrderTotalTicks.ToStringTicksToPeriod()
            : CachedFixedWorkTicks.ToStringTicksToPeriod();
        CachedQueueCountLabel = dock.TotalQueuedOrderCount.ToString();
    }

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

    private void EnsureFocusState(Building_GR_Drydock dock)
    {
        GrayMechDesignSnapshot draft = dock.DesignDraft;
        if (!SelectedSlotKey.NullOrEmpty() && TryGetResolvedSlot(SelectedSectionSlot, SelectedSlotKey, out GRMechResolvedSlot resolvedSlot1))
        {
            SelectedSectionSlot = resolvedSlot1.sectionSlot;
            return;
        }

        if (!GRMechSectionLayoutCatalog.TryGetSectionSlots(draft?.chassis, out List<GRMechSectionSlotDef> sectionSlots) || sectionSlots.Count == 0)
        {
            SelectedSectionSlot = null;
            SelectedSlotKey = null;
            return;
        }

        if (!IsSectionPresent(draft, SelectedSectionSlot))
        {
            SelectedSectionSlot = sectionSlots[0];
        }

        if (!SelectedSlotKey.NullOrEmpty())
        {
            if (TryGetResolvedSlot(SelectedSectionSlot, SelectedSlotKey, out GRMechResolvedSlot resolvedSlot2))
            {
                SelectedSectionSlot = resolvedSlot2.sectionSlot;
            }
            else
            {
                SelectedSlotKey = null;
            }
        }
    }

    private void EnsureCoreSelectionState()
    {
        if (SelectedCoreSlotKey.NullOrEmpty())
        {
            return;
        }

        for (int i = 0; i < SlotCache.Count; i++)
        {
            GRMechResolvedSlot slot = SlotCache[i];
            if (slot?.slot != null && slot.sectionSlot == null && slot.slot.key == SelectedCoreSlotKey)
            {
                return;
            }
        }

        SelectedCoreSlotKey = null;
    }

    internal void EnsureCompatibleModuleCache(Building_GR_Drydock dock)
    {
        if (SelectedSlotKey.NullOrEmpty())
        {
            CompatibleModules.Clear();
            cachedCompatibleSectionSlot = null;
            cachedCompatibleSlotKey = null;
            cachedCompatibleRevision = dock.DesignRevision;
            return;
        }

        if (cachedCompatibleRevision == dock.DesignRevision
            && GRMechSectionSlotUtility.Matches(cachedCompatibleSectionSlot, SelectedSectionSlot)
            && cachedCompatibleSlotKey == SelectedSlotKey
            && cachedCompatibleShowObsolete == ShowObsoleteModules)
        {
            return;
        }

        if (TryGetResolvedSlot(SelectedSectionSlot, SelectedSlotKey, out GRMechResolvedSlot resolvedSlot))
        {
            GrayMechDesignUtility.FillCompatibleModules(dock.DesignDraft, resolvedSlot.slot, CompatibleModules, ShowObsoleteModules);
        }
        else
        {
            CompatibleModules.Clear();
        }

        cachedCompatibleSectionSlot = SelectedSectionSlot;
        cachedCompatibleSlotKey = SelectedSlotKey;
        cachedCompatibleShowObsolete = ShowObsoleteModules;
        cachedCompatibleRevision = dock.DesignRevision;
    }

    internal void EnsureCoreCompatibleModuleCache(Building_GR_Drydock dock)
    {
        EnsureCoreSelectionState();
        if (SelectedCoreSlotKey.NullOrEmpty())
        {
            CoreCompatibleModules.Clear();
            cachedCoreCompatibleSlotKey = null;
            cachedCoreCompatibleRevision = dock.DesignRevision;
            return;
        }

        if (cachedCoreCompatibleRevision == dock.DesignRevision
            && cachedCoreCompatibleSlotKey == SelectedCoreSlotKey)
        {
            return;
        }

        if (TryGetResolvedSlot(null, SelectedCoreSlotKey, out GRMechResolvedSlot resolvedSlot))
        {
            GrayMechDesignUtility.FillCompatibleModules(dock.DesignDraft, resolvedSlot.slot, CoreCompatibleModules, true);
        }
        else
        {
            CoreCompatibleModules.Clear();
        }

        cachedCoreCompatibleSlotKey = SelectedCoreSlotKey;
        cachedCoreCompatibleRevision = dock.DesignRevision;
    }

    private void RebuildDerivedCache(Building_GR_Drydock dock)
    {
        GrayMechDesignSnapshot draft = dock.DesignDraft;
        GrayMechDesignUtility.BuildCostList(draft, CostCache);
        CachedCostSummary = GrayMechDrydockTabText.BuildCostSummary(CostCache, TextBuilder);
        GrayMechDesignUtility.GetPowerBudget(draft, out CachedPowerGeneration, out CachedPowerConsumption, out CachedPowerNet);

        CachedTotalSlotCount = SlotCache.Count;
        CachedFilledSlotCount = 0;
        for (int i = 0; i < SlotCache.Count; i++)
        {
            if (SlotCache[i]?.slot == null)
            {
                continue;
            }

            if (GrayMechDesignUtility.TryGetSelectedModule(draft, SlotCache[i], out GRMechModuleDef _))
            {
                CachedFilledSlotCount++;
            }
        }

        ThingDef producedRace = draft?.chassis?.ProducedRace;
        CachedFixedWorkTicks = draft?.chassis?.fixedWorkTicks ?? 0;
        CachedBandwidthCost = GetDraftStatValue(draft, producedRace, StatDefOf.BandwidthCost);
        CachedMoveSpeed = GetDraftStatValue(draft, producedRace, StatDefOf.MoveSpeed);
        CachedArmorSharp = GetDraftStatValue(draft, producedRace, StatDefOf.ArmorRating_Sharp);
        CachedArmorBlunt = GetDraftStatValue(draft, producedRace, StatDefOf.ArmorRating_Blunt);
        CachedArmorHeat = GetDraftStatValue(draft, producedRace, StatDefOf.ArmorRating_Heat);
        CachedShieldEnergyMax = GetDraftStatValue(draft, producedRace, StatDefOf.EnergyShieldEnergyMax);
        CachedShieldRechargeRate = GetDraftStatValue(draft, producedRace, StatDefOf.EnergyShieldRechargeRate);
        CachedRangedCooldownFactor = GetDraftStatValue(draft, producedRace, StatDefOf.RangedCooldownFactor);
        CachedShootingAccuracyPawn = GetDraftStatValue(draft, producedRace, StatDefOf.ShootingAccuracyPawn);
        CachedLongAccuracyFactor = GetDraftStatValue(draft, producedRace, StatDefOf.ShootingAccuracyFactor_Long);
    }

    private static float GetDraftStatValue(GrayMechDesignSnapshot snapshot, ThingDef producedRace, StatDef stat)
    {
        float value = producedRace != null ? producedRace.GetStatValueAbstract(stat) : stat?.defaultBaseValue ?? 0f;
        if (snapshot?.modules == null || stat == null)
        {
            return value;
        }

        float totalOffset = 0f;
        float totalFactor = 1f;
        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            GRMechModuleDef module = snapshot.modules[i]?.module;
            if (module == null)
            {
                continue;
            }

            List<StatModifier> statOffsets = module.statOffsets;
            if (statOffsets == null)
            {
                continue;
            }

            for (int j = 0; j < statOffsets.Count; j++)
            {
                StatModifier modifier = statOffsets[j];
                if (modifier?.stat == stat)
                {
                    totalOffset += modifier.value;
                }
            }

            List<StatModifier> statFactors = module.statFactors;
            if (statFactors == null)
            {
                continue;
            }

            for (int j = 0; j < statFactors.Count; j++)
            {
                StatModifier modifier = statFactors[j];
                if (modifier?.stat == stat)
                {
                    totalFactor *= modifier.value;
                }
            }
        }

        return (value + totalOffset) * totalFactor;
    }

    private void BuildChassisCache()
    {
        ChassisCache.Clear();
        List<GRMechChassisDef> chassisDefs = DefDatabase<GRMechChassisDef>.AllDefsListForReading;
        for (int i = 0; i < chassisDefs.Count; i++)
        {
            GRMechChassisDef chassis = chassisDefs[i];
            if (chassis != null && GrayMechDesignUtility.IsResearchAvailable(chassis))
            {
                ChassisCache.Add(chassis);
            }
        }

        ChassisCache.Sort(GrayMechDrydockTabText.CompareChassisDefs);
        RebuildLibraryGroupCache();
    }

    private void BuildDesignCache(WorldComponent_GrayMechDesignLibrary library)
    {
        DesignCache.Clear();
        if (library?.Designs == null)
        {
            RebuildLibraryGroupCache();
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
        RebuildLibraryGroupCache();
    }

    private static int GetVisibleChassisHash()
    {
        unchecked
        {
            int hash = 17;
            List<GRMechChassisDef> chassisDefs = DefDatabase<GRMechChassisDef>.AllDefsListForReading;
            for (int i = 0; i < chassisDefs.Count; i++)
            {
                GRMechChassisDef chassis = chassisDefs[i];
                if (chassis != null && GrayMechDesignUtility.IsResearchAvailable(chassis))
                {
                    hash = hash * 31 + chassis.shortHash;
                }
            }

            return hash;
        }
    }

    private void RebuildLibraryGroupCache()
    {
        LibraryGroups.Clear();
        CachedLibraryMaxCardHeight = GrayMechDrydockTabStyle.LibraryCardHeight;
        for (int i = 0; i < ChassisCache.Count; i++)
        {
            GRMechChassisDef chassis = ChassisCache[i];
            if (chassis == null)
            {
                continue;
            }

            GrayMechLibraryGroup group = new()
            {
                Chassis = chassis
            };
            for (int j = 0; j < DesignCache.Count; j++)
            {
                GrayMechDesignRecord design = DesignCache[j];
                if (design?.snapshot?.chassis != chassis)
                {
                    continue;
                }

                group.Designs.Add(design);
                CachedLibraryMaxCardHeight = Mathf.Max(CachedLibraryMaxCardHeight, GetLibraryCardHeight(design));
            }

            if (group.ShowChassisCard)
            {
                CachedLibraryMaxCardHeight = Mathf.Max(CachedLibraryMaxCardHeight, GetLibraryCardHeight(chassis));
            }

            LibraryGroups.Add(group);
        }
    }

    internal bool TryGetResolvedSlot(GRMechSectionSlotDef sectionSlot, string slotKey, out GRMechResolvedSlot resolvedSlot)
    {
        if (!slotKey.NullOrEmpty())
        {
            for (int i = 0; i < SlotCache.Count; i++)
            {
                GRMechResolvedSlot current = SlotCache[i];
                if (current != null
                    && GRMechSectionSlotUtility.Matches(current.sectionSlot, sectionSlot)
                    && current.slot != null
                    && current.slot.key == slotKey)
                {
                    resolvedSlot = current;
                    return true;
                }
            }
        }

        resolvedSlot = null;
        return false;
    }

    private static bool IsSectionPresent(GrayMechDesignSnapshot draft, GRMechSectionSlotDef sectionSlot)
    {
        return sectionSlot != null
            && GRMechSectionLayoutCatalog.TryGetSectionSlots(draft?.chassis, out List<GRMechSectionSlotDef> sectionSlots)
            && sectionSlots.Contains(sectionSlot);
    }

    internal void InvalidateDraft()
    {
        cachedDraftRevision = -1;
        cachedCompatibleRevision = -1;
        cachedCoreCompatibleRevision = -1;
        cachedCompatibleSectionSlot = null;
        cachedCompatibleSlotKey = null;
        cachedCompatibleShowObsolete = ShowObsoleteModules;
        cachedCoreCompatibleSlotKey = null;
    }

    internal void SetShowObsoleteModules(bool value)
    {
        if (ShowObsoleteModules == value)
        {
            return;
        }

        ShowObsoleteModules = value;
        cachedCompatibleRevision = -1;
        cachedCompatibleSectionSlot = null;
        cachedCompatibleSlotKey = null;
        cachedCompatibleShowObsolete = value;
    }

    internal float GetTopBarHeight(Building_GR_Drydock dock, float width)
    {
        GrayMechDesignSnapshot draft = dock.DesignDraft;
        string draftName = draft?.designLabel ?? "No design";
        string chassisName = draft?.chassis?.LabelCap.ToString() ?? "No chassis";
        string titleText = "<b>Ship Designer</b>    " + draftName;
        string subText = "Chassis: " + chassisName + "    Source: " + GrayMechDrydockTabText.GetSourceLabel(dock);
        float textWidth = Mathf.Max(220f, width - 560f);
        float subWidth = Mathf.Max(120f, textWidth);
        float titleHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(titleText, textWidth, GameFont.Small);
        float subHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(subText, subWidth, GameFont.Small);
        return Mathf.Max(GrayMechDrydockTabStyle.MinTopBarHeight, 8f + titleHeight + 4f + subHeight + 8f);
    }

    internal float GetBottomBarHeight(float width)
    {
        float contentHeight = GrayMechDrydockTabStyle.LibraryHeaderHeight
                              + GrayMechDrydockTabStyle.LibraryHeaderGap
                              + CachedLibraryMaxCardHeight;
        const float panelChromeHeight = 24f;
        const float horizontalScrollbarHeight = 16f;
        return Mathf.Max(
            GrayMechDrydockTabStyle.MinBottomBarHeight,
            contentHeight + panelChromeHeight + horizontalScrollbarHeight);
    }

    internal float GetSummaryPanelHeight(float width)
    {
        float innerWidth = width - 16f;
        float pillWidth = Mathf.Min(GrayMechDrydockTabStyle.SummaryStatusMaxWidth, innerWidth * 0.34f);
        string queueStatus = CachedHasActiveOrder ? CachedProductionStatus : (CachedCanQueueOrder ? "Ready for assembly" : CachedQueueReason);
        float pillHeight = GrayMechDrydockTabText.GetPillHeight(queueStatus, pillWidth);
        float headerHeight = Mathf.Max(GrayMechDrydockTabStyle.SummaryThumbHeight, pillHeight);

        float rowHeight = 26f;
        float sectionHeaderHeight = 22f;
        float sectionGap = 8f;
        float rowGap = 4f;
        float labelWidth = Mathf.Min(120f, innerWidth * 0.38f);
        float valueWidth = innerWidth - labelWidth - 8f;
        string costText = CachedCostSummary.NullOrEmpty() ? "None" : CachedCostSummary;
        float costHeight = Mathf.Max(rowHeight, GrayMechDrydockTabText.MeasureWrappedTextHeight(costText, valueWidth - 12f, GameFont.Tiny) + 8f);
        float productionHeight = sectionHeaderHeight + rowGap + rowHeight + rowGap + rowHeight + rowGap + rowHeight + rowGap + costHeight;
        float shipStatsHeight = sectionHeaderHeight + rowGap + rowHeight * 8f + rowGap * 7f;
        float designManagementHeight = GetDesignManagementSectionHeight(innerWidth);
        float totalHeight = 8f + headerHeight + 8f + 8f + productionHeight + sectionGap + shipStatsHeight + sectionGap + designManagementHeight + 10f;
        return Mathf.Max(GrayMechDrydockTabStyle.SummaryPanelHeight, totalHeight);
    }

    internal float GetDesignManagementSectionHeight(float width)
    {
        const float sectionHeaderHeight = 22f;
        const float nameGap = 4f;
        const float rowGap = 8f;
        const float nameFieldHeight = 30f;
        const float checkboxHeight = 24f;
        const float buttonRowHeight = 28f;
        const float saveButtonHeight = 54f;
        return sectionHeaderHeight + nameGap + nameFieldHeight + rowGap + checkboxHeight + rowGap + buttonRowHeight + rowGap + saveButtonHeight;
    }

    internal float GetLibraryCardHeight(GRMechChassisDef chassis)
    {
        float textWidth = GrayMechDrydockTabStyle.LibraryCardWidth - 12f;
        string titleText = "<b>" + (chassis?.LabelCap.ToString() ?? "Unnamed") + "</b>";
        const string footerText = "开始新设计";
        float titleHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(titleText, textWidth, GameFont.Small);
        float footerHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(footerText, textWidth, GameFont.Tiny);
        return Mathf.Max(
            GrayMechDrydockTabStyle.LibraryCardHeight,
            4f + titleHeight + 2f + 36f + 2f + footerHeight + 4f);
    }

    internal float GetLibraryCardHeight(GrayMechDesignRecord design)
    {
        float textWidth = GrayMechDrydockTabStyle.LibraryCardWidth - 12f;
        string kindText = "Saved";
        string titleText = "<b>" + (design?.label ?? "Unnamed") + "</b>";
        GrayMechDesignSnapshot snapshot = design?.snapshot;
        string footerText = (snapshot?.chassis?.LabelCap.ToString() ?? "No chassis") + "   Modules " + CountInstalledModules(snapshot);
        float kindHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(kindText, textWidth, GameFont.Tiny);
        float titleHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(titleText, textWidth, GameFont.Small);
        float footerHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(footerText, textWidth, GameFont.Tiny);
        return Mathf.Max(
            GrayMechDrydockTabStyle.LibraryCardHeight,
            4f + kindHeight + 2f + 36f + 2f + titleHeight + 2f + footerHeight + 4f);
    }

    internal float GetLibraryGroupWidth(GrayMechLibraryGroup group)
    {
        int cardCount = group?.VisibleCardCount ?? 0;
        if (cardCount <= 0)
        {
            return 0f;
        }

        return cardCount * GrayMechDrydockTabStyle.LibraryCardWidth
               + Mathf.Max(0, cardCount - 1) * GrayMechDrydockTabStyle.LibraryCardGap;
    }

    internal float GetLayoutOptionHeight(GRMechSectionLayoutDef layout, float width)
    {
        float leftWidth = width - 90f;
        string title = "<b>" + layout.LabelCap + "</b>";
        string slotsText = "Slots: " + GrayMechDrydockTabText.BuildLayoutSlotExpression(layout, TextBuilder);
        return 8f
               + GrayMechDrydockTabText.MeasureWrappedTextHeight(title, leftWidth, GameFont.Small)
               + 2f
               + GrayMechDrydockTabText.MeasureWrappedTextHeight(slotsText, leftWidth, GameFont.Small)
               + 8f;
    }

    internal float GetModuleOptionHeight(GRMechModuleDef module, float width)
    {
        float leftWidth = width - 84f;
        string title = "<b>" + (module?.LabelCap.ToString() ?? "Empty Slot") + "</b>";
        string cost = module == null ? "Cost: None" : GrayMechDrydockTabText.BuildModuleCostSummary(module, cachedDock?.DesignDraft?.chassis, TextBuilder);
        return 8f
               + GrayMechDrydockTabText.MeasureWrappedTextHeight(title, leftWidth, GameFont.Small)
               + 2f
               + GrayMechDrydockTabText.MeasureWrappedTextHeight(cost, leftWidth, GameFont.Small)
               + 8f;
    }

    private static int CountInstalledModules(GrayMechDesignSnapshot snapshot)
    {
        if (snapshot?.modules == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            if (snapshot.modules[i]?.module != null)
            {
                count++;
            }
        }

        return count;
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
        cachedCompatibleRevision = -1;
        cachedCompatibleSectionSlot = null;
        cachedCompatibleSlotKey = null;
    }

    internal void SelectCoreSlot(GRMechResolvedSlot resolvedSlot)
    {
        if (resolvedSlot?.slot == null || resolvedSlot.sectionSlot != null)
        {
            return;
        }

        SelectedCoreSlotKey = resolvedSlot.slot.key;
        cachedCoreCompatibleRevision = -1;
        cachedCoreCompatibleSlotKey = null;
    }

    internal void ClearCoreSelection()
    {
        SelectedCoreSlotKey = null;
        cachedCoreCompatibleRevision = -1;
        cachedCoreCompatibleSlotKey = null;
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
        cachedCompatibleRevision = -1;
        cachedCompatibleSectionSlot = null;
        cachedCompatibleSlotKey = null;
        cachedCoreCompatibleRevision = -1;
        cachedCoreCompatibleSlotKey = null;
        FocusScrollPosition = Vector2.zero;
        return true;
    }

    internal void PrepareSectionSlotBuffers(GRMechSectionSlotDef sectionSlot)
    {
        WeaponSlotBuffer.Clear();
        SmallUtilitySlotBuffer.Clear();
        MediumUtilitySlotBuffer.Clear();
        LargeUtilitySlotBuffer.Clear();
        AuxSlotBuffer.Clear();
        SupportSlotBuffer.Clear();
        SectionSlotBuffer.Clear();

        for (int i = 0; i < SlotCache.Count; i++)
        {
            GRMechResolvedSlot slot = SlotCache[i];
            if (slot == null || !GRMechSectionSlotUtility.Matches(slot.sectionSlot, sectionSlot) || slot.slot == null)
            {
                continue;
            }

            SectionSlotBuffer.Add(slot);
            switch (slot.slot.slotCategory)
            {
                case GRMechSlotCategory.Weapon:
                    WeaponSlotBuffer.Add(slot);
                    break;
                case GRMechSlotCategory.Utility:
                    SupportSlotBuffer.Add(slot);
                    if (slot.slot.slotSize == GrayRaceDefOf.GR_MechSlotSize_Medium)
                    {
                        MediumUtilitySlotBuffer.Add(slot);
                    }
                    else if (slot.slot.slotSize == GrayRaceDefOf.GR_MechSlotSize_Large)
                    {
                        LargeUtilitySlotBuffer.Add(slot);
                    }
                    else
                    {
                        SmallUtilitySlotBuffer.Add(slot);
                    }

                    break;
                case GRMechSlotCategory.Auxiliary:
                case GRMechSlotCategory.CoreSystem:
                    AuxSlotBuffer.Add(slot);
                    SupportSlotBuffer.Add(slot);
                    break;
            }
        }
    }

    internal void PrepareRequiredSlotBuffer()
    {
        RequiredSlotBuffer.Clear();
        for (int i = 0; i < SlotCache.Count; i++)
        {
            GRMechResolvedSlot slot = SlotCache[i];
            if (slot?.slot == null || slot.sectionSlot != null)
            {
                continue;
            }

            RequiredSlotBuffer.Add(slot);
        }
    }

    internal bool SectionOwnsSelectedSlot(GRMechSectionSlotDef sectionSlot)
    {
        if (SelectedSectionSlot == null || SelectedSlotKey.NullOrEmpty())
        {
            return false;
        }

        return TryGetResolvedSlot(SelectedSectionSlot, SelectedSlotKey, out GRMechResolvedSlot resolvedSlot)
            && GRMechSectionSlotUtility.Matches(resolvedSlot.sectionSlot, sectionSlot);
    }
}
