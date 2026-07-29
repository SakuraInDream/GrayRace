using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using SD.GrayRace.Defs;
using Verse;

namespace SD.GrayRace.Mechs;

public static class GrayMechDesignUtility
{
    private static readonly Dictionary<ThingDef, int> tmpCostCounts = new();
    private static readonly List<ThingDef> tmpCostOrder = new();
    private static readonly List<GrayMechModuleAssignment> tmpAssignmentsToRemove = new();

    public static GrayMechDesignSnapshot CloneSnapshot(GrayMechDesignSnapshot source)
    {
        if (source == null)
        {
            return null;
        }

        GrayMechDesignSnapshot clone = new()
        {
            designLabel = source.designLabel,
            chassis = source.chassis
        };

        if (source.sections != null)
        {
            for (int i = 0; i < source.sections.Count; i++)
            {
                GrayMechSectionSelection section = source.sections[i];
                if (section == null)
                {
                    continue;
                }

                clone.sections.Add(new GrayMechSectionSelection
                {
                    sectionSlot = section.sectionSlot,
                    layout = section.layout
                });
            }
        }

        if (source.modules != null)
        {
            for (int i = 0; i < source.modules.Count; i++)
            {
                GrayMechModuleAssignment module = source.modules[i];
                if (module == null)
                {
                    continue;
                }

                clone.modules.Add(new GrayMechModuleAssignment
                {
                    sectionSlot = module.sectionSlot,
                    slotKey = module.slotKey,
                    module = module.module
                });
            }
        }

        return clone;
    }

    public static GrayMechDesignSnapshot CreateDefaultSnapshot(GRMechChassisDef chassis, string label = null)
    {
        if (chassis == null)
        {
            return null;
        }

        GrayMechDesignSnapshot snapshot = new()
        {
            designLabel = label ?? chassis.LabelCap.ToString(),
            chassis = chassis
        };

        if (GRMechSectionLayoutCatalog.TryGetSectionSlots(chassis, out List<GRMechSectionSlotDef> sectionSlots))
        {
            for (int i = 0; i < sectionSlots.Count; i++)
            {
                GRMechSectionSlotDef sectionSlot = sectionSlots[i];
                if (sectionSlot == null || !GRMechSectionLayoutCatalog.TryGetDefaultLayout(chassis, sectionSlot, out GRMechSectionLayoutDef defaultLayout))
                {
                    continue;
                }

                snapshot.sections.Add(new GrayMechSectionSelection
                {
                    sectionSlot = sectionSlot,
                    layout = defaultLayout
                });
            }
        }

        EnsureRequiredCoreModuleAssignments(snapshot);
        return snapshot;
    }

    public static void EnsureSnapshotDefaults(GrayMechDesignSnapshot snapshot)
    {
        if (snapshot?.chassis == null)
        {
            return;
        }

        snapshot.sections ??= new List<GrayMechSectionSelection>();
        snapshot.modules ??= new List<GrayMechModuleAssignment>();

        if (snapshot.designLabel.NullOrEmpty())
        {
            snapshot.designLabel = snapshot.chassis.LabelCap;
        }

        for (int i = snapshot.sections.Count - 1; i >= 0; i--)
        {
            GrayMechSectionSelection selection = snapshot.sections[i];
            if (selection?.sectionSlot == null
                || selection.layout == null
                || !GRMechSectionLayoutCatalog.TryGetLayouts(snapshot.chassis, selection.sectionSlot, out List<GRMechSectionLayoutDef> layouts)
                || !layouts.Contains(selection.layout))
            {
                snapshot.sections.RemoveAt(i);
            }
        }

        if (GRMechSectionLayoutCatalog.TryGetSectionSlots(snapshot.chassis, out List<GRMechSectionSlotDef> sectionSlots))
        {
            for (int i = 0; i < sectionSlots.Count; i++)
            {
                GRMechSectionSlotDef sectionSlot = sectionSlots[i];
                if (sectionSlot == null)
                {
                    continue;
                }

                GrayMechSectionSelection selection = GetSectionSelection(snapshot, sectionSlot);
                if (selection == null)
                {
                    if (GRMechSectionLayoutCatalog.TryGetDefaultLayout(snapshot.chassis, sectionSlot, out GRMechSectionLayoutDef defaultLayout))
                    {
                        snapshot.sections.Add(new GrayMechSectionSelection
                        {
                            sectionSlot = sectionSlot,
                            layout = defaultLayout
                        });
                    }

                    continue;
                }

                if (selection.layout == null
                    || selection.layout.shipSize != snapshot.chassis
                    || !GRMechSectionSlotUtility.Matches(selection.layout.sectionSlot, sectionSlot))
                {
                    if (GRMechSectionLayoutCatalog.TryGetDefaultLayout(snapshot.chassis, sectionSlot, out GRMechSectionLayoutDef defaultLayout))
                    {
                        selection.layout = defaultLayout;
                    }
                }
            }
        }

        PruneInvalidModuleAssignments(snapshot);
        EnsureRequiredCoreModuleAssignments(snapshot);
    }

    public static GrayMechSectionSelection GetSectionSelection(GrayMechDesignSnapshot snapshot, GRMechSectionSlotDef sectionSlot)
    {
        if (snapshot?.sections == null || sectionSlot == null)
        {
            return null;
        }

        for (int i = 0; i < snapshot.sections.Count; i++)
        {
            GrayMechSectionSelection selection = snapshot.sections[i];
            if (selection != null && GRMechSectionSlotUtility.Matches(selection.sectionSlot, sectionSlot))
            {
                return selection;
            }
        }

        return null;
    }

    public static bool TryGetSelectedLayout(GrayMechDesignSnapshot snapshot, GRMechSectionSlotDef sectionSlot, out GRMechSectionLayoutDef layout)
    {
        GrayMechSectionSelection selection = GetSectionSelection(snapshot, sectionSlot);
        layout = selection?.layout;
        return layout != null;
    }

    public static bool SetSectionLayout(GrayMechDesignSnapshot snapshot, GRMechSectionSlotDef sectionSlot, GRMechSectionLayoutDef layout)
    {
        if (snapshot?.chassis == null || sectionSlot == null || layout == null)
        {
            return false;
        }

        if (layout.shipSize != snapshot.chassis || !GRMechSectionSlotUtility.Matches(layout.sectionSlot, sectionSlot))
        {
            return false;
        }

        GrayMechSectionSelection selection = GetSectionSelection(snapshot, sectionSlot);
        if (selection == null)
        {
            selection = new GrayMechSectionSelection
            {
                sectionSlot = sectionSlot,
                layout = layout
            };
            snapshot.sections.Add(selection);
        }
        else
        {
            selection.layout = layout;
        }

        PruneInvalidModuleAssignments(snapshot);
        return true;
    }

    public static bool TryGetSelectedModule(GrayMechDesignSnapshot snapshot, GRMechSectionSlotDef sectionSlot, string slotKey, out GRMechModuleDef module)
    {
        if (snapshot?.modules != null && !slotKey.NullOrEmpty())
        {
            for (int i = 0; i < snapshot.modules.Count; i++)
            {
                GrayMechModuleAssignment assignment = snapshot.modules[i];
                if (assignment != null && GRMechSectionSlotUtility.Matches(assignment.sectionSlot, sectionSlot) && assignment.slotKey == slotKey)
                {
                    module = assignment.module;
                    return module != null;
                }
            }
        }

        module = null;
        return false;
    }

    public static bool TryGetSelectedModule(GrayMechDesignSnapshot snapshot, GRMechResolvedSlot resolvedSlot, out GRMechModuleDef module)
    {
        if (resolvedSlot?.slot == null)
        {
            module = null;
            return false;
        }

        return TryGetSelectedModule(snapshot, resolvedSlot.sectionSlot, resolvedSlot.slot.key, out module);
    }

    public static bool SetModule(GrayMechDesignSnapshot snapshot, GRMechSectionSlotDef sectionSlot, string slotKey, GRMechModuleDef module)
    {
        if (snapshot == null || slotKey.NullOrEmpty())
        {
            return false;
        }

        snapshot.modules ??= new List<GrayMechModuleAssignment>();
        if (!TryResolveSlot(snapshot, sectionSlot, slotKey, out GRMechSlotEntry resolvedSlot, out _))
        {
            return false;
        }

        if (module == null
            && resolvedSlot.slotCategory == GRMechSlotCategory.CoreSystem
            && HasAnyAvailableModuleForSlot(snapshot.chassis, resolvedSlot))
        {
            return false;
        }

        for (int i = snapshot.modules.Count - 1; i >= 0; i--)
        {
            GrayMechModuleAssignment assignment = snapshot.modules[i];
            if (assignment != null && GRMechSectionSlotUtility.Matches(assignment.sectionSlot, sectionSlot) && assignment.slotKey == slotKey)
            {
                if (module == null)
                {
                    snapshot.modules.RemoveAt(i);
                    return true;
                }

                if (!module.Matches(snapshot.chassis, resolvedSlot))
                {
                    return false;
                }

                assignment.module = module;
                return true;
            }
        }

        if (module == null)
        {
            return true;
        }

        if (!module.Matches(snapshot.chassis, resolvedSlot))
        {
            return false;
        }

        snapshot.modules.Add(new GrayMechModuleAssignment
        {
            sectionSlot = sectionSlot,
            slotKey = slotKey,
            module = module
        });
        return true;
    }

    public static bool IsWeaponModule(GRMechModuleDef module)
    {
        return module?.equipmentDef != null && module.UsesSlotCategory(GRMechSlotCategory.Weapon);
    }

    public static bool TryResolvePrimaryEquipmentModule(GrayMechDesignSnapshot snapshot, out GRMechModuleDef primaryModule, out string reason)
    {
        primaryModule = null;
        reason = string.Empty;
        if (snapshot?.modules == null)
        {
            return true;
        }

        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            GrayMechModuleAssignment assignment = snapshot.modules[i];
            GRMechModuleDef module = assignment?.module;
            if (module == null
                || !TryResolveSlot(snapshot, assignment.sectionSlot, assignment.slotKey, out GRMechSlotEntry slot, out _)
                || slot.weaponMountMode != GRMechWeaponMountMode.PrimaryEquipment)
            {
                continue;
            }

            if (slot.componentType != GRMechSlotComponentType.Weapon || !IsWeaponModule(module))
            {
                reason = "Primary equipment slot " + slot.key + " must contain a weapon module.";
                return false;
            }

            if (primaryModule != null)
            {
                reason = "A design can install at most one primary weapon.";
                return false;
            }

            ThingDef equipmentDef = module.equipmentDef;
            if (equipmentDef.equipmentType != EquipmentType.Primary)
            {
                reason = "Primary weapon module " + module.LabelCap + " must use equipmentType Primary.";
                return false;
            }

            if (!HasEquippableComp(equipmentDef))
            {
                reason = "Primary weapon module " + module.LabelCap + " must provide CompEquippable.";
                return false;
            }

            primaryModule = module;
        }

        return true;
    }

    private static bool HasEquippableComp(ThingDef equipmentDef)
    {
        List<CompProperties> comps = equipmentDef?.comps;
        if (comps == null)
        {
            return false;
        }

        for (int i = 0; i < comps.Count; i++)
        {
            Type compClass = comps[i]?.compClass;
            if (compClass != null && typeof(CompEquippable).IsAssignableFrom(compClass))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryResolveSlot(GrayMechDesignSnapshot snapshot, GRMechSectionSlotDef sectionSlot, string slotKey, out GRMechSlotEntry slot, out GRMechSectionLayoutDef layout)
    {
        if (snapshot?.chassis != null && !slotKey.NullOrEmpty())
        {
            if (sectionSlot == null)
            {
                if (snapshot.chassis.TryGetRequiredComponentSlot(slotKey, out slot))
                {
                    layout = null;
                    return true;
                }
            }
            else if (snapshot.sections != null)
            {
                for (int i = 0; i < snapshot.sections.Count; i++)
                {
                    GrayMechSectionSelection selection = snapshot.sections[i];
                    if (selection == null || !GRMechSectionSlotUtility.Matches(selection.sectionSlot, sectionSlot))
                    {
                        continue;
                    }

                    GRMechSectionLayoutDef currentLayout = selection.layout;
                    if (currentLayout == null)
                    {
                        continue;
                    }

                    foreach (GRMechSlotEntry currentSlot in GRMechLayoutSlotUtility.EnumerateSlots(currentLayout))
                    {
                        if (currentSlot != null && currentSlot.key == slotKey)
                        {
                            slot = currentSlot;
                            layout = currentLayout;
                            return true;
                        }
                    }
                }
            }
        }

        slot = null;
        layout = null;
        return false;
    }

    public static bool HasAnyEquipmentModules(GrayMechDesignSnapshot snapshot)
    {
        if (snapshot?.modules == null)
        {
            return false;
        }

        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            if (snapshot.modules[i]?.module?.equipmentDef != null)
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryGetFirstMissingRequiredModule(GrayMechDesignSnapshot snapshot, out GRMechSlotEntry missingSlot)
    {
        if (snapshot?.chassis?.requiredComponentSlots != null)
        {
            for (int i = 0; i < snapshot.chassis.requiredComponentSlots.Count; i++)
            {
                GRMechSlotEntry slot = snapshot.chassis.requiredComponentSlots[i];
                if (slot == null)
                {
                    continue;
                }

                if (!HasAnyAvailableModuleForSlot(snapshot.chassis, slot))
                {
                    continue;
                }

                if (!TryGetSelectedModule(snapshot, null, slot.key, out GRMechModuleDef module) || module == null)
                {
                    missingSlot = slot;
                    return true;
                }
            }
        }

        missingSlot = null;
        return false;
    }

    private static bool HasAnyAvailableModuleForSlot(GRMechChassisDef chassis, GRMechSlotEntry slot)
    {
        return TryGetPreferredAvailableModuleForSlot(chassis, slot, out _);
    }

    private static void EnsureRequiredCoreModuleAssignments(GrayMechDesignSnapshot snapshot)
    {
        if (snapshot?.chassis?.requiredComponentSlots == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.chassis.requiredComponentSlots.Count; i++)
        {
            GRMechSlotEntry slot = snapshot.chassis.requiredComponentSlots[i];
            if (slot == null)
            {
                continue;
            }

            if (TryGetSelectedModule(snapshot, null, slot.key, out GRMechModuleDef selectedModule) && selectedModule != null)
            {
                continue;
            }

            if (TryGetPreferredAvailableModuleForSlot(snapshot.chassis, slot, out GRMechModuleDef preferredModule))
            {
                SetModule(snapshot, null, slot.key, preferredModule);
            }
        }
    }

    private static bool TryGetPreferredAvailableModuleForSlot(GRMechChassisDef chassis, GRMechSlotEntry slot, out GRMechModuleDef module)
    {
        module = null;
        if (chassis == null || slot == null)
        {
            return false;
        }

        GRMechModuleDef fallback = null;
        List<GRMechModuleDef> defs = DefDatabase<GRMechModuleDef>.AllDefsListForReading;
        for (int i = 0; i < defs.Count; i++)
        {
            GRMechModuleDef candidate = defs[i];
            if (candidate == null || !candidate.Matches(chassis, slot) || !IsResearchAvailable(candidate))
            {
                continue;
            }

            if (fallback == null || CompareModules(candidate, fallback) < 0)
            {
                fallback = candidate;
            }

            if (!IsObsoleteForSlot(chassis, slot, candidate) && (module == null || CompareModules(candidate, module) < 0))
            {
                module = candidate;
            }
        }

        if (module == null)
        {
            module = fallback;
        }

        return module != null;
    }

    public static void GetPowerBudget(GrayMechDesignSnapshot snapshot, out int generation, out int consumption, out int net)
    {
        generation = 0;
        consumption = 0;
        net = 0;

        if (snapshot?.modules == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            GRMechModuleDef module = snapshot.modules[i]?.module;
            if (module == null)
            {
                continue;
            }

            generation += module.GetPowerGeneration(snapshot.chassis);
            consumption += module.GetPowerConsumption(snapshot.chassis);
        }

        net = generation - consumption;
    }

    public static bool TryGetPowerDeficit(GrayMechDesignSnapshot snapshot, out int deficit)
    {
        GetPowerBudget(snapshot, out _, out _, out int net);
        if (net < 0)
        {
            deficit = -net;
            return true;
        }

        deficit = 0;
        return false;
    }

    public static void FillResolvedSlots(GrayMechDesignSnapshot snapshot, List<GRMechResolvedSlot> buffer)
    {
        buffer.Clear();
        if (snapshot?.chassis == null)
        {
            return;
        }

        if (snapshot.sections != null)
        {
            for (int i = 0; i < snapshot.sections.Count; i++)
            {
                GrayMechSectionSelection selection = snapshot.sections[i];
                GRMechSectionLayoutDef layout = selection?.layout;
                if (layout == null)
                {
                    continue;
                }

                foreach (GRMechSlotEntry slot in GRMechLayoutSlotUtility.EnumerateSlots(layout))
                {
                    if (slot == null)
                    {
                        continue;
                    }

                    buffer.Add(new GRMechResolvedSlot
                    {
                        sectionSlot = selection.sectionSlot,
                        layout = layout,
                        slot = slot
                    });
                }
            }
        }

        if (snapshot.chassis.requiredComponentSlots != null)
        {
            for (int i = 0; i < snapshot.chassis.requiredComponentSlots.Count; i++)
            {
                GRMechSlotEntry slot = snapshot.chassis.requiredComponentSlots[i];
                if (slot == null)
                {
                    continue;
                }

                buffer.Add(new GRMechResolvedSlot
                {
                    sectionSlot = null,
                    layout = null,
                    slot = slot
                });
            }
        }

        buffer.Sort(CompareResolvedSlots);
    }

    public static void FillCompatibleModules(GrayMechDesignSnapshot snapshot, GRMechSlotEntry slot, List<GRMechModuleDef> buffer, bool includeObsolete)
    {
        buffer.Clear();
        if (snapshot?.chassis == null || slot == null)
        {
            return;
        }

        List<GRMechModuleDef> defs = DefDatabase<GRMechModuleDef>.AllDefsListForReading;
        for (int i = 0; i < defs.Count; i++)
        {
            GRMechModuleDef module = defs[i];
            if (module == null || !module.Matches(snapshot.chassis, slot) || !IsResearchAvailable(module))
            {
                continue;
            }

            if (!includeObsolete && IsObsoleteForSlot(snapshot.chassis, slot, module))
            {
                continue;
            }

            buffer.Add(module);
        }

        buffer.Sort(CompareModules);
    }

    public static bool TryResolveBrushModuleForSlot(GRMechChassisDef chassis, GRMechSlotEntry slot, GRMechModuleDef armedModule, out GRMechModuleDef resolvedModule)
    {
        resolvedModule = null;
        if (chassis == null || slot == null || armedModule == null || slot.slotCategory == GRMechSlotCategory.CoreSystem)
        {
            return false;
        }

        if (IsResearchAvailable(armedModule) && armedModule.Matches(chassis, slot))
        {
            resolvedModule = armedModule;
            return true;
        }

        if (armedModule.slotFamily.NullOrEmpty())
        {
            return false;
        }

        List<GRMechModuleDef> defs = DefDatabase<GRMechModuleDef>.AllDefsListForReading;
        for (int i = 0; i < defs.Count; i++)
        {
            GRMechModuleDef candidate = defs[i];
            if (candidate == null
                || candidate.slotFamily != armedModule.slotFamily
                || !IsResearchAvailable(candidate)
                || !candidate.Matches(chassis, slot))
            {
                continue;
            }

            if (resolvedModule == null || CompareModules(candidate, resolvedModule) < 0)
            {
                resolvedModule = candidate;
            }
        }

        return resolvedModule != null;
    }

    public static bool IsObsoleteForSlot(GRMechChassisDef chassis, GRMechSlotEntry slot, GRMechModuleDef module)
    {
        if (chassis == null || slot == null || module == null)
        {
            return false;
        }

        List<GRMechModuleDef> pending = new() { module };
        HashSet<GRMechModuleDef> visited = new();
        for (int i = 0; i < pending.Count; i++)
        {
            GRMechModuleDef current = pending[i];
            if (current == null || !visited.Add(current))
            {
                continue;
            }

            if (current != module && current.Matches(chassis, slot) && IsResearchAvailable(current))
            {
                return true;
            }

            List<GRMechModuleDef> nextModules = current.upgradesTo;
            if (nextModules == null)
            {
                continue;
            }

            for (int j = 0; j < nextModules.Count; j++)
            {
                GRMechModuleDef candidate = nextModules[j];
                if (candidate != null)
                {
                    pending.Add(candidate);
                }
            }
        }

        return false;
    }

    public static void BuildCostList(GrayMechDesignSnapshot snapshot, List<ThingDefCountClass> buffer)
    {
        buffer.Clear();
        tmpCostCounts.Clear();
        tmpCostOrder.Clear();

        if (snapshot?.chassis == null)
        {
            return;
        }

        if (snapshot.sections != null)
        {
            for (int i = 0; i < snapshot.sections.Count; i++)
            {
                GRMechSectionLayoutDef layout = snapshot.sections[i]?.layout;
                if (layout != null)
                {
                    AddCostRange(layout.costList);
                }
            }
        }

        if (snapshot.modules != null)
        {
            for (int i = 0; i < snapshot.modules.Count; i++)
            {
                GRMechModuleDef module = snapshot.modules[i]?.module;
                if (module != null)
                {
                    AddCostRange(module.costList);
                }
            }
        }

        for (int i = 0; i < tmpCostOrder.Count; i++)
        {
            ThingDef thingDef = tmpCostOrder[i];
            if (thingDef != null && tmpCostCounts.TryGetValue(thingDef, out int count) && count > 0)
            {
                buffer.Add(new ThingDefCountClass(thingDef, count));
            }
        }
    }

    public static void BuildIngredientList(GrayMechDesignSnapshot snapshot, List<IngredientCount> ingredientBuffer, List<ThingDefCountClass> costBuffer)
    {
        ingredientBuffer.Clear();
        costBuffer.Clear();

        BuildCostList(snapshot, costBuffer);
        for (int i = 0; i < costBuffer.Count; i++)
        {
            ThingDefCountClass cost = costBuffer[i];
            if (cost?.thingDef == null || cost.count <= 0)
            {
                continue;
            }

            ingredientBuffer.Add(cost.ToIngredientCount());
        }
    }

    public static string BuildModuleSummary(GrayMechDesignSnapshot snapshot)
    {
        if (snapshot?.modules == null || snapshot.modules.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new();
        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            GRMechModuleDef module = snapshot.modules[i]?.module;
            if (module == null)
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.Append(module.LabelCap);
        }

        return sb.ToString();
    }

    public static bool IsResearchAvailable(GRMechChassisDef chassis)
    {
        return AreResearchPrerequisitesMet(chassis?.EnumerateResearchPrerequisites(), out _);
    }

    public static bool IsResearchAvailable(GRMechSectionLayoutDef layout)
    {
        return AreResearchPrerequisitesMet(layout?.EnumerateResearchPrerequisites(), out _);
    }

    public static bool IsResearchAvailable(GRMechModuleDef module)
    {
        return AreResearchPrerequisitesMet(module?.EnumerateResearchPrerequisites(), out _);
    }

    public static bool TryGetFirstMissingResearch(GrayMechDesignSnapshot snapshot, out ResearchProjectDef missingProject)
    {
        missingProject = null;
        if (snapshot?.chassis == null)
        {
            return false;
        }

        if (!AreResearchPrerequisitesMet(snapshot.chassis.EnumerateResearchPrerequisites(), out missingProject))
        {
            return true;
        }

        if (snapshot.sections != null)
        {
            for (int i = 0; i < snapshot.sections.Count; i++)
            {
                GRMechSectionLayoutDef layout = snapshot.sections[i]?.layout;
                if (layout != null && !AreResearchPrerequisitesMet(layout.EnumerateResearchPrerequisites(), out missingProject))
                {
                    return true;
                }
            }
        }

        if (snapshot.modules != null)
        {
            for (int i = 0; i < snapshot.modules.Count; i++)
            {
                GRMechModuleDef module = snapshot.modules[i]?.module;
                if (module != null && !AreResearchPrerequisitesMet(module.EnumerateResearchPrerequisites(), out missingProject))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool AreResearchPrerequisitesMet(IEnumerable<ResearchProjectDef> projects, out ResearchProjectDef missingProject)
    {
        if (projects != null)
        {
            foreach (ResearchProjectDef project in projects)
            {
                if (project != null && !project.IsFinished)
                {
                    missingProject = project;
                    return false;
                }
            }
        }

        missingProject = null;
        return true;
    }

    private static void PruneInvalidModuleAssignments(GrayMechDesignSnapshot snapshot)
    {
        if (snapshot?.modules == null || snapshot.chassis == null)
        {
            return;
        }

        tmpAssignmentsToRemove.Clear();
        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            GrayMechModuleAssignment assignment = snapshot.modules[i];
            if (assignment == null || assignment.slotKey.NullOrEmpty() || assignment.module == null)
            {
                tmpAssignmentsToRemove.Add(assignment);
                continue;
            }

            if (!TryResolveSlot(snapshot, assignment.sectionSlot, assignment.slotKey, out GRMechSlotEntry slot, out _) || !assignment.module.Matches(snapshot.chassis, slot))
            {
                tmpAssignmentsToRemove.Add(assignment);
            }
        }

        for (int i = 0; i < tmpAssignmentsToRemove.Count; i++)
        {
            snapshot.modules.Remove(tmpAssignmentsToRemove[i]);
        }

        tmpAssignmentsToRemove.Clear();
    }

    private static int CompareModules(GRMechModuleDef left, GRMechModuleDef right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        int order = left.uiOrder.CompareTo(right.uiOrder);
        if (order != 0)
        {
            return order;
        }

        return string.CompareOrdinal(left.label, right.label);
    }

    private static int CompareResolvedSlots(GRMechResolvedSlot left, GRMechResolvedSlot right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        int sectionSlotCompare = CompareSectionSlot(left.sectionSlot, right.sectionSlot);
        if (sectionSlotCompare != 0)
        {
            return sectionSlotCompare;
        }

        int layoutOrder = (left.layout?.uiOrder ?? int.MaxValue).CompareTo(right.layout?.uiOrder ?? int.MaxValue);
        if (layoutOrder != 0)
        {
            return layoutOrder;
        }

        int slotOrder = (left.slot?.uiOrder ?? int.MaxValue).CompareTo(right.slot?.uiOrder ?? int.MaxValue);
        if (slotOrder != 0)
        {
            return slotOrder;
        }

        return string.CompareOrdinal(left.slot?.label, right.slot?.label);
    }

    private static int CompareSectionSlot(GRMechSectionSlotDef left, GRMechSectionSlotDef right)
    {
        return GRMechSectionSlotUtility.Compare(left, right);
    }

    private static void AddCostRange(List<ThingDefCountClass> costs)
    {
        if (costs == null)
        {
            return;
        }

        for (int i = 0; i < costs.Count; i++)
        {
            ThingDefCountClass cost = costs[i];
            if (cost?.thingDef == null || cost.count <= 0)
            {
                continue;
            }

            if (!tmpCostCounts.ContainsKey(cost.thingDef))
            {
                tmpCostCounts.Add(cost.thingDef, cost.count);
                tmpCostOrder.Add(cost.thingDef);
                continue;
            }

            tmpCostCounts[cost.thingDef] += cost.count;
        }
    }
}
