using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using SD.GrayRace.Defs;
using Verse;

namespace SD.GrayRace.HarmonyPatches;

public static class ResearchProjectDefPatches
{
    // 缓存
    private static Dictionary<ResearchProjectDef, List<Def>> extraByProject;

    public static IEnumerable<CodeInstruction> UnlockedDefsTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        FieldInfo cachedUnlockedDefsField = AccessTools.Field(typeof(ResearchProjectDef), "cachedUnlockedDefs");
        MethodInfo injectMethod = AccessTools.Method(typeof(ResearchProjectDefPatches), nameof(InjectGrayRaceUpgrades));

        bool injected = false;
        // Log.Message("I'm in 000");

        foreach (var instruction in instructions)
        {
            yield return instruction;

            if (!injected && instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo { Name: "cachedUnlockedDefs" })
            {
                // Log.Message("I'm in 001");
                // __instance
                yield return new CodeInstruction(OpCodes.Ldarg_0);

                // __instance.cachedUnlockedDefs
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, cachedUnlockedDefsField);

                // InjectGrayRaceUpgrades(__instance, __instance.cachedUnlockedDefs)
                yield return new CodeInstruction(OpCodes.Call, injectMethod);

                injected = true;
            }
        }
        // Log.Message("I'm in 002");
    }

    public static void InjectGrayRaceUpgrades(ResearchProjectDef project, List<Def> list)
    {
        EnsureCache();

        if (extraByProject.TryGetValue(project, out List<Def> extras))
        {
            list.AddRange(extras);
            list.SortBy(x => x.label);
        }
    }

    private static void EnsureCache()
    {
        if (extraByProject != null) return;

        extraByProject = new Dictionary<ResearchProjectDef, List<Def>>();
        foreach (GRUpgradeDef up in DefDatabase<GRUpgradeDef>.AllDefsListForReading)
        {
            foreach (ResearchProjectDef project in up.EnumerateResearchPrerequisites())
            {
                if (!extraByProject.TryGetValue(project, out List<Def> list))
                {
                    list = new List<Def>();
                    extraByProject.Add(project, list);
                }

                if (!list.Contains(up))
                {
                    list.Add(up);
                }
            }
        }
    }
}
