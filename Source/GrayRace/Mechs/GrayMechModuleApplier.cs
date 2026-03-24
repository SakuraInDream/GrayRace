using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.Defs;
using Verse;

namespace SD.GrayRace.Mechs;

public static class GrayMechModuleApplier
{
    public static void ApplyLoadout(Pawn pawn, GrayMechDesignSnapshot snapshot)
    {
        if (pawn == null)
        {
            return;
        }

        if (pawn.equipment != null && snapshot != null && GrayMechDesignUtility.HasAnyEquipmentModules(snapshot))
        {
            pawn.equipment.DestroyAllEquipment();
        }

        CompGrayMechTurretBank turretBank = pawn.TryGetComp<CompGrayMechTurretBank>();
        if (snapshot?.modules == null)
        {
            turretBank?.RebuildFromSnapshot(snapshot);
            return;
        }

        for (int i = 0; i < snapshot.modules.Count; i++)
        {
            GrayMechModuleAssignment assignment = snapshot.modules[i];
            GRMechModuleDef module = assignment?.module;
            if (module == null)
            {
                continue;
            }

            ApplyEquipment(pawn, module);
            ApplyHediff(pawn, snapshot, assignment, module);
        }

        turretBank?.RebuildFromSnapshot(snapshot);
    }

    private static void ApplyEquipment(Pawn pawn, GRMechModuleDef module)
    {
        if (module?.equipmentDef == null || pawn?.equipment == null || ShouldUseInternalTurret(module))
        {
            return;
        }

        if (module.equipmentDef.equipmentType == EquipmentType.Primary && pawn.equipment.Primary != null)
        {
            return;
        }

        ThingDef stuff = null;
        if (module.equipmentDef.MadeFromStuff)
        {
            stuff = module.equipmentStuff ?? GenStuff.DefaultStuffFor(module.equipmentDef);
        }

        ThingWithComps equipment = ThingMaker.MakeThing(module.equipmentDef, stuff) as ThingWithComps;
        if (equipment == null)
        {
            return;
        }

        PawnGenerator.PostProcessGeneratedGear(equipment, pawn);
        pawn.equipment.AddEquipment(equipment);
    }

    private static bool ShouldUseInternalTurret(GRMechModuleDef module)
    {
        return module?.equipmentDef != null && module.UsesSlotCategory(GRMechSlotCategory.Weapon);
    }

    private static void ApplyHediff(Pawn pawn, GrayMechDesignSnapshot snapshot, GrayMechModuleAssignment assignment, GRMechModuleDef module)
    {
        if (module?.hediffToApply == null || pawn?.health?.hediffSet == null)
        {
            return;
        }

        BodyPartRecord targetPart = ResolveTargetPart(pawn, snapshot, assignment, module);
        if (targetPart != null)
        {
            if (!pawn.health.hediffSet.HasHediff(module.hediffToApply, targetPart))
            {
                pawn.health.AddHediff(module.hediffToApply, targetPart);
            }

            return;
        }

        if (!pawn.health.hediffSet.HasHediff(module.hediffToApply))
        {
            pawn.health.AddHediff(module.hediffToApply);
        }
    }

    private static BodyPartRecord ResolveTargetPart(Pawn pawn, GrayMechDesignSnapshot snapshot, GrayMechModuleAssignment assignment, GRMechModuleDef module)
    {
        if (module?.anchorBodyPart != null
            && pawn.health.hediffSet.TryGetBodyPartRecord(module.anchorBodyPart, out BodyPartRecord anchoredPart)
            && !pawn.health.hediffSet.PartIsMissing(anchoredPart))
        {
            return anchoredPart;
        }

        if (assignment == null || assignment.slotKey.NullOrEmpty())
        {
            return null;
        }

        if (!GrayMechDesignUtility.TryResolveSlot(snapshot, assignment.sectionSlot, assignment.slotKey, out GRMechSlotEntry slot, out _))
        {
            return null;
        }

        if (slot?.anchorBodyPart == null)
        {
            return null;
        }

        if (!pawn.health.hediffSet.TryGetBodyPartRecord(slot.anchorBodyPart, out BodyPartRecord slotPart))
        {
            return null;
        }

        return pawn.health.hediffSet.PartIsMissing(slotPart) ? null : slotPart;
    }
}
