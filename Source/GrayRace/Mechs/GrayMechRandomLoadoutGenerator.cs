using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Defs;
using Verse;

namespace SD.GrayRace.Mechs;

public static class GrayMechRandomLoadoutGenerator
{
    private static readonly List<GRMechChassisDef> tmpChassisCandidates = new();
    private static readonly List<GRMechSectionLayoutDef> tmpLayoutCandidates = new();
    private static readonly List<GRMechResolvedSlot> tmpResolvedSlots = new();
    private static readonly List<GRMechModuleDef> tmpModuleCandidates = new();
    private static readonly List<GrayMechModuleAssignment> tmpAssignmentsToPrune = new();

    public static bool TryCreateSnapshotFor(Pawn pawn, out GrayMechDesignSnapshot snapshot)
    {
        snapshot = null;
        if (pawn?.kindDef == null)
        {
            return false;
        }

        Rand.PushState(pawn.thingIDNumber ^ 0x51A73F2);
        try
        {
            FillChassisCandidates(pawn.kindDef);
            if (tmpChassisCandidates.Count == 0)
            {
                return false;
            }

            GRMechChassisDef chassis = tmpChassisCandidates.RandomElement();
            snapshot = GrayMechDesignUtility.CreateDefaultSnapshot(chassis);
            if (snapshot == null)
            {
                return false;
            }

            RandomizeSectionLayouts(snapshot);
            GrayMechDesignUtility.FillResolvedSlots(snapshot, tmpResolvedSlots);
            FillRandomModules(snapshot);
            ResolvePowerBudget(snapshot);
            GrayMechDesignUtility.EnsureSnapshotDefaults(snapshot);
            snapshot.designLabel = chassis.LabelCap.ToString();
            return true;
        }
        finally
        {
            Rand.PopState();
            tmpChassisCandidates.Clear();
            tmpLayoutCandidates.Clear();
            tmpResolvedSlots.Clear();
            tmpModuleCandidates.Clear();
            tmpAssignmentsToPrune.Clear();
        }
    }

    private static void FillChassisCandidates(PawnKindDef pawnKindDef)
    {
        tmpChassisCandidates.Clear();
        List<GRMechChassisDef> allChassis = DefDatabase<GRMechChassisDef>.AllDefsListForReading;
        for (int i = 0; i < allChassis.Count; i++)
        {
            GRMechChassisDef chassis = allChassis[i];
            if (chassis != null && chassis.pawnKindDef == pawnKindDef)
            {
                tmpChassisCandidates.Add(chassis);
            }
        }
    }

    private static void RandomizeSectionLayouts(GrayMechDesignSnapshot snapshot)
    {
        if (snapshot?.chassis == null || !GRMechSectionLayoutCatalog.TryGetSectionSlots(snapshot.chassis, out List<GRMechSectionSlotDef> sectionSlots))
        {
            return;
        }

        for (int i = 0; i < sectionSlots.Count; i++)
        {
            GRMechSectionSlotDef sectionSlot = sectionSlots[i];
            if (sectionSlot == null || !GRMechSectionLayoutCatalog.TryGetLayouts(snapshot.chassis, sectionSlot, out List<GRMechSectionLayoutDef> layouts) || layouts.Count == 0)
            {
                continue;
            }

            tmpLayoutCandidates.Clear();
            for (int j = 0; j < layouts.Count; j++)
            {
                GRMechSectionLayoutDef layout = layouts[j];
                if (layout != null)
                {
                    tmpLayoutCandidates.Add(layout);
                }
            }

            if (tmpLayoutCandidates.Count > 0)
            {
                GrayMechDesignUtility.SetSectionLayout(snapshot, sectionSlot, tmpLayoutCandidates.RandomElement());
            }
        }
    }

    private static void FillRandomModules(GrayMechDesignSnapshot snapshot)
    {
        if (snapshot?.chassis == null)
        {
            return;
        }

        GRMechSlotEntry powerCoreSlot = null;
        for (int i = 0; i < tmpResolvedSlots.Count; i++)
        {
            GRMechResolvedSlot resolvedSlot = tmpResolvedSlots[i];
            if (resolvedSlot?.slot == null)
            {
                continue;
            }

            if (resolvedSlot.sectionSlot == null && resolvedSlot.slot.coreRole == GRMechCoreComponentRole.PowerCore)
            {
                powerCoreSlot = resolvedSlot.slot;
                continue;
            }

            if (TryPickRandomModule(snapshot.chassis, resolvedSlot.slot, out GRMechModuleDef module))
            {
                GrayMechDesignUtility.SetModule(snapshot, resolvedSlot.sectionSlot, resolvedSlot.slot.key, module);
            }
        }

        if (powerCoreSlot != null && TryPickBestPowerCore(snapshot, powerCoreSlot, out GRMechModuleDef powerCore))
        {
            GrayMechDesignUtility.SetModule(snapshot, null, powerCoreSlot.key, powerCore);
        }
    }

    private static bool TryPickRandomModule(GRMechChassisDef chassis, GRMechSlotEntry slot, out GRMechModuleDef module)
    {
        tmpModuleCandidates.Clear();
        FillMatchingModules(chassis, slot, tmpModuleCandidates);
        if (tmpModuleCandidates.Count == 0)
        {
            module = null;
            return false;
        }

        module = tmpModuleCandidates.RandomElement();
        return true;
    }

    private static bool TryPickBestPowerCore(GrayMechDesignSnapshot snapshot, GRMechSlotEntry powerCoreSlot, out GRMechModuleDef module)
    {
        module = null;
        tmpModuleCandidates.Clear();
        FillMatchingModules(snapshot?.chassis, powerCoreSlot, tmpModuleCandidates);
        if (tmpModuleCandidates.Count == 0)
        {
            return false;
        }

        GrayMechDesignUtility.TryGetPowerDeficit(snapshot, out int deficit);
        int bestIndex = -1;
        int bestAdequateGeneration = int.MaxValue;
        int bestFallbackGeneration = int.MinValue;
        for (int i = 0; i < tmpModuleCandidates.Count; i++)
        {
            GRMechModuleDef candidate = tmpModuleCandidates[i];
            int generation = candidate.GetPowerGeneration(snapshot.chassis);
            if (generation >= deficit && generation < bestAdequateGeneration)
            {
                bestAdequateGeneration = generation;
                bestIndex = i;
            }

            if (generation > bestFallbackGeneration)
            {
                bestFallbackGeneration = generation;
                if (bestIndex < 0)
                {
                    bestIndex = i;
                }
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        module = tmpModuleCandidates[bestIndex];
        return true;
    }

    private static void ResolvePowerBudget(GrayMechDesignSnapshot snapshot)
    {
        if (snapshot?.modules == null)
        {
            return;
        }

        while (GrayMechDesignUtility.TryGetPowerDeficit(snapshot, out int _) && TryRemoveWorstOptionalModule(snapshot))
        {
        }
    }

    private static bool TryRemoveWorstOptionalModule(GrayMechDesignSnapshot snapshot)
    {
        tmpAssignmentsToPrune.Clear();
        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            GrayMechModuleAssignment assignment = snapshot.modules[i];
            if (assignment?.module == null || assignment.sectionSlot == null)
            {
                continue;
            }

            tmpAssignmentsToPrune.Add(assignment);
        }

        if (tmpAssignmentsToPrune.Count == 0)
        {
            return false;
        }

        int bestIndex = -1;
        int bestPriority = int.MinValue;
        int bestConsumption = int.MinValue;
        for (int i = 0; i < tmpAssignmentsToPrune.Count; i++)
        {
            GrayMechModuleAssignment assignment = tmpAssignmentsToPrune[i];
            if (!GrayMechDesignUtility.TryResolveSlot(snapshot, assignment.sectionSlot, assignment.slotKey, out GRMechSlotEntry slot, out _))
            {
                continue;
            }

            int priority = slot.slotCategory == GRMechSlotCategory.Weapon ? 0 : 1;
            int consumption = assignment.module.GetPowerConsumption(snapshot.chassis);
            if (priority > bestPriority || (priority == bestPriority && consumption > bestConsumption))
            {
                bestPriority = priority;
                bestConsumption = consumption;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        GrayMechModuleAssignment remove = tmpAssignmentsToPrune[bestIndex];
        return GrayMechDesignUtility.SetModule(snapshot, remove.sectionSlot, remove.slotKey, null);
    }

    private static void FillMatchingModules(GRMechChassisDef chassis, GRMechSlotEntry slot, List<GRMechModuleDef> buffer)
    {
        buffer.Clear();
        if (chassis == null || slot == null)
        {
            return;
        }

        List<GRMechModuleDef> defs = DefDatabase<GRMechModuleDef>.AllDefsListForReading;
        for (int i = 0; i < defs.Count; i++)
        {
            GRMechModuleDef module = defs[i];
            if (module != null && module.Matches(chassis, slot))
            {
                buffer.Add(module);
            }
        }
    }
}
