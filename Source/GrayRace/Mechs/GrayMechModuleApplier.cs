using System.Collections.Generic;
using RimWorld;
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
            ThingWithComps primary = pawn.equipment.Primary;
            List<ThingWithComps> equipment = pawn.equipment.AllEquipmentListForReading;
            for (int i = equipment.Count - 1; i >= 0; i--)
            {
                if (equipment[i] != primary)
                {
                    pawn.equipment.DestroyEquipment(equipment[i]);
                }
            }
        }

        if (snapshot?.modules == null)
        {
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
        }
    }

    private static void ApplyEquipment(Pawn pawn, GRMechModuleDef module)
    {
        if (module?.equipmentDef == null || pawn?.equipment == null)
        {
            return;
        }

        if (GrayMechDesignUtility.IsWeaponModule(module))
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
}
