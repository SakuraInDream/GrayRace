using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SD.GrayRace.HarmonyPatches;

[HarmonyPatch(typeof(WorkGiver_HaulMechToCharger), nameof(WorkGiver_HaulMechToCharger.HasJobOnThing))]
internal static class Patch_WorkGiver_HaulMechToCharger
{
    private static bool Prefix(Thing t, ref bool __result)
    {
        if (t is Pawn mech && mech.needs?.energy == null)
        {
            __result = false;
            return false;
        }
        return true;
    }
}

// 底部机械体标签。
[HarmonyPatch(typeof(PawnColumnWorker_Energy), nameof(PawnColumnWorker_Energy.DoCell))]
internal static class Patch_PawnColumnWorker_Energy
{
    private static bool Prefix(Pawn pawn)
    {
        return pawn.needs?.energy != null;
    }
}

[HarmonyPatch(typeof(Building_MechCharger), nameof(Building_MechCharger.StartCharging))]
internal static class Patch_Building_MechCharger_StartCharging
{
    private static bool Prefix(Pawn mech)
    {
        return mech.needs?.energy != null;
    }
}


[HarmonyPatch(typeof(WorkGiver_RepairMech), nameof(WorkGiver_RepairMech.HasJobOnThing))]
internal static class Patch_WorkGiver_RepairMech
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        int i = codes.FindIndex(ci =>
            ci.operand is FieldInfo fi && fi.Name == "energy" && fi.DeclaringType == typeof(Pawn_NeedsTracker));

        bool patternOk = i >= 2 && i + 3 < codes.Count
            && codes[i - 1].operand is FieldInfo nf && nf.Name == "needs"
            && codes[i - 2].opcode.Name != null && codes[i - 2].opcode.Name.StartsWith("ldloc")
            && (codes[i + 1].opcode == OpCodes.Brtrue_S || codes[i + 1].opcode == OpCodes.Brtrue)
            && codes[i + 2].opcode == OpCodes.Ldc_I4_0
            && codes[i + 3].opcode == OpCodes.Ret;

        if (!patternOk)
        {
            throw new Exception("Patch_WorkGiver_RepairMech: 未找到预期的 needs.energy==null 拒绝分支（游戏版本不匹配？）");
        }

        for (int k = i - 2; k <= i + 3; k++)
        {
            codes[k].opcode = OpCodes.Nop;
            codes[k].operand = null;
        }
        return codes;
    }
}

[HarmonyPatch]
internal static class Patch_JobDriver_RepairMech_EnergyCost
{
    private static MethodBase TargetMethod()
    {
        MethodBase target = AccessTools.GetDeclaredMethods(typeof(JobDriver_RepairMech))
            .SingleOrDefault(m =>
                m.Name.Contains("b__") &&
                m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType == typeof(int));
        return target ?? throw new Exception("Patch_JobDriver_RepairMech_EnergyCost: 未找到维修 tick lambda（游戏版本不匹配？）");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        int i = codes.FindIndex(ci =>
            ci.operand is FieldInfo fi && fi.Name == "energy" && fi.DeclaringType == typeof(Pawn_NeedsTracker));

        bool headOk = i >= 3 && i + 1 < codes.Count
            && codes[i - 3].opcode == OpCodes.Ldarg_0
            && (codes[i - 2].opcode == OpCodes.Call || codes[i - 2].opcode == OpCodes.Callvirt)
            && codes[i - 2].operand is MethodInfo gm && gm.Name == "get_Mech"
            && codes[i - 1].operand is FieldInfo nf && nf.Name == "needs"
            && codes[i + 1].opcode == OpCodes.Dup;
        if (!headOk)
        {
            throw new Exception("Patch_JobDriver_RepairMech_EnergyCost: 扣电语句头部指令模式不匹配。");
        }

        int j = -1;
        for (int k = i + 1; k < codes.Count && k < i + 15; k++)
        {
            if (codes[k].operand is MethodInfo mi && mi.Name == "set_CurLevel")
            {
                j = k;
                break;
            }
        }
        if (j < 0 || codes[j - 1].opcode != OpCodes.Sub)
        {
            throw new Exception("Patch_JobDriver_RepairMech_EnergyCost: 未找到 `CurLevel -= ...` 语句范围。");
        }

        // 语句结构 [i-3..j]：ldarg.0; get_Mech; ldfld needs; ldfld energy; ...; set_CurLevel
        // 保留 ldarg.0 + get_Mech（栈上得到 mech），把 [i-1..j] 替换为 ApplyRepairEnergyCost(mech)。
        List<CodeInstruction> removed = codes.GetRange(i - 1, j - i + 2);
        foreach (CodeInstruction code in codes)
        {
            if (code.operand is CodeInstruction target && removed.Contains(target))
            {
                throw new Exception("Patch_JobDriver_RepairMech_EnergyCost: 语句范围内存在被分支引用的指令，为安全起见放弃替换。");
            }
        }

        codes.RemoveRange(i - 1, j - i + 2);
        codes.Insert(i - 1, CodeInstruction.Call(
            typeof(Patch_JobDriver_RepairMech_EnergyCost), nameof(ApplyRepairEnergyCost)));
        return codes;
    }

    // 与原版扣电相同的逻辑；energy 不存在（GR 机械体）时整条跳过，即维修完全不耗电。
    internal static void ApplyRepairEnergyCost(Pawn mech)
    {
        if (mech.needs?.energy != null)
        {
            mech.needs.energy.CurLevel -= mech.GetStatValue(StatDefOf.MechEnergyLossPerHP);
        }
    }
}
