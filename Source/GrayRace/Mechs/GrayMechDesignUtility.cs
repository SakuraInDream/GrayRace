using System.Collections.Generic;
using System.Text;
using SD.GrayRace.Defs;
using RimWorld;
using Verse;

namespace SD.GrayRace.Mechs;

public static class GrayMechDesignUtility
{
    private static readonly Dictionary<ThingDef, int> tmpCostCounts = new();
    private static readonly List<ThingDef> tmpCostOrder = new();
    private static readonly List<GrayMechModuleAssignment> tmpAssignmentsToRemove = new();

    public static GrayMechDesignSnapshot CreateSnapshot(GRMechPresetDef preset)
    {
        if (preset?.chassis == null)
        {
            return null;
        }

        GrayMechDesignSnapshot snapshot = new()
        {
            designLabel = preset.label,
            chassis = preset.chassis
        };

        if (preset.sections != null)
        {
            for (int i = 0; i < preset.sections.Count; i++)
            {
                GRMechPresetSectionDef section = preset.sections[i];
                if (section?.role == null || section.layout == null)
                {
                    continue;
                }

                snapshot.sections.Add(new GrayMechSectionSelection
                {
                    role = section.role,
                    layout = section.layout
                });
            }
        }

        if (preset.modules != null)
        {
            for (int i = 0; i < preset.modules.Count; i++)
            {
                GRMechPresetModuleDef assignment = preset.modules[i];
                if (assignment == null || assignment.slotKey.NullOrEmpty() || assignment.module == null)
                {
                    continue;
                }

                snapshot.modules.Add(new GrayMechModuleAssignment
                {
                    slotKey = assignment.slotKey,
                    module = assignment.module
                });
            }
        }

        return snapshot;
    }

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
                    role = section.role,
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

        if (chassis.sections != null)
        {
            for (int i = 0; i < chassis.sections.Count; i++)
            {
                GRMechChassisSectionDef section = chassis.sections[i];
                if (section?.role == null || section.defaultLayout == null)
                {
                    continue;
                }

                snapshot.sections.Add(new GrayMechSectionSelection
                {
                    role = section.role,
                    layout = section.defaultLayout
                });
            }
        }

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

        if (snapshot.chassis.sections != null)
        {
            for (int i = 0; i < snapshot.chassis.sections.Count; i++)
            {
                GRMechChassisSectionDef section = snapshot.chassis.sections[i];
                if (section?.role == null)
                {
                    continue;
                }

                GrayMechSectionSelection selection = GetSectionSelection(snapshot, section.role);
                if (selection == null)
                {
                    if (section.defaultLayout != null)
                    {
                        snapshot.sections.Add(new GrayMechSectionSelection
                        {
                            role = section.role,
                            layout = section.defaultLayout
                        });
                    }

                    continue;
                }

                if (selection.layout == null || section.layouts == null || !section.layouts.Contains(selection.layout))
                {
                    selection.layout = section.defaultLayout;
                }
            }
        }

        PruneInvalidModuleAssignments(snapshot);
    }

    public static GrayMechSectionSelection GetSectionSelection(GrayMechDesignSnapshot snapshot, GRMechSectionRoleDef role)
    {
        if (snapshot?.sections == null || role == null)
        {
            return null;
        }

        for (int i = 0; i < snapshot.sections.Count; i++)
        {
            GrayMechSectionSelection selection = snapshot.sections[i];
            if (selection?.role == role)
            {
                return selection;
            }
        }

        return null;
    }

    public static bool TryGetSelectedLayout(GrayMechDesignSnapshot snapshot, GRMechSectionRoleDef role, out GRMechSectionLayoutDef layout)
    {
        GrayMechSectionSelection selection = GetSectionSelection(snapshot, role);
        layout = selection?.layout;
        return layout != null;
    }

    public static bool SetSectionLayout(GrayMechDesignSnapshot snapshot, GRMechSectionRoleDef role, GRMechSectionLayoutDef layout)
    {
        if (snapshot?.chassis == null || role == null || layout == null)
        {
            return false;
        }

        if (!snapshot.chassis.TryGetSection(role, out GRMechChassisSectionDef section) || section.layouts == null || !section.layouts.Contains(layout))
        {
            return false;
        }

        GrayMechSectionSelection selection = GetSectionSelection(snapshot, role);
        if (selection == null)
        {
            selection = new GrayMechSectionSelection
            {
                role = role,
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

    public static bool TryGetSelectedModule(GrayMechDesignSnapshot snapshot, string slotKey, out GRMechModuleDef module)
    {
        if (snapshot?.modules != null && !slotKey.NullOrEmpty())
        {
            for (int i = 0; i < snapshot.modules.Count; i++)
            {
                GrayMechModuleAssignment assignment = snapshot.modules[i];
                if (assignment != null && assignment.slotKey == slotKey)
                {
                    module = assignment.module;
                    return module != null;
                }
            }
        }

        module = null;
        return false;
    }

    public static bool SetModule(GrayMechDesignSnapshot snapshot, string slotKey, GRMechModuleDef module)
    {
        if (snapshot == null || slotKey.NullOrEmpty())
        {
            return false;
        }

        snapshot.modules ??= new List<GrayMechModuleAssignment>();

        for (int i = snapshot.modules.Count - 1; i >= 0; i--)
        {
            GrayMechModuleAssignment assignment = snapshot.modules[i];
            if (assignment != null && assignment.slotKey == slotKey)
            {
                if (module == null)
                {
                    snapshot.modules.RemoveAt(i);
                    return true;
                }

                if (!TryResolveSlot(snapshot, slotKey, out GRMechSlotDef slot, out _) || !module.Matches(snapshot.chassis, slot))
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

        if (!TryResolveSlot(snapshot, slotKey, out GRMechSlotDef resolvedSlot, out _) || !module.Matches(snapshot.chassis, resolvedSlot))
        {
            return false;
        }

        snapshot.modules.Add(new GrayMechModuleAssignment
        {
            slotKey = slotKey,
            module = module
        });
        return true;
    }

    public static bool TryResolveSlot(GrayMechDesignSnapshot snapshot, string slotKey, out GRMechSlotDef slot, out GRMechSectionLayoutDef layout)
    {
        if (snapshot?.sections != null && !slotKey.NullOrEmpty())
        {
            for (int i = 0; i < snapshot.sections.Count; i++)
            {
                GRMechSectionLayoutDef currentLayout = snapshot.sections[i]?.layout;
                if (currentLayout == null)
                {
                    continue;
                }

                foreach (GRMechSlotDef currentSlot in GRMechLayoutSlotUtility.EnumerateSlots(currentLayout))
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

    public static void FillResolvedSlots(GrayMechDesignSnapshot snapshot, List<GrayMechResolvedSlot> buffer)
    {
        buffer.Clear();
        if (snapshot?.sections == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.sections.Count; i++)
        {
            GrayMechSectionSelection selection = snapshot.sections[i];
            GRMechSectionLayoutDef layout = selection?.layout;
            if (layout == null)
            {
                continue;
            }

            foreach (GRMechSlotDef slot in GRMechLayoutSlotUtility.EnumerateSlots(layout))
            {
                if (slot == null)
                {
                    continue;
                }

                buffer.Add(new GrayMechResolvedSlot
                {
                    role = selection.role,
                    layout = layout,
                    slot = slot
                });
            }
        }

        buffer.Sort(CompareResolvedSlots);
    }

    public static void FillCompatibleModules(GrayMechDesignSnapshot snapshot, GRMechSlotDef slot, List<GRMechModuleDef> buffer)
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
            if (module != null && module.Matches(snapshot.chassis, slot))
            {
                buffer.Add(module);
            }
        }

        buffer.Sort(CompareModules);
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

        AddCostRange(snapshot.chassis.baseCostList);

        if (snapshot.sections != null)
        {
            for (int i = 0; i < snapshot.sections.Count; i++)
            {
                GRMechSectionLayoutDef layout = snapshot.sections[i]?.layout;
                if (layout != null)
                {
                    AddCostRange(layout.additionalCostList);
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

            if (!TryResolveSlot(snapshot, assignment.slotKey, out GRMechSlotDef slot, out _) || !assignment.module.Matches(snapshot.chassis, slot))
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

    private static int CompareResolvedSlots(GrayMechResolvedSlot left, GrayMechResolvedSlot right)
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

        int roleCompare = CompareRole(left.role, right.role);
        if (roleCompare != 0)
        {
            return roleCompare;
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

    private static int CompareRole(GRMechSectionRoleDef left, GRMechSectionRoleDef right)
    {
        string leftName = left?.defName ?? string.Empty;
        string rightName = right?.defName ?? string.Empty;
        return string.CompareOrdinal(leftName, rightName);
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
