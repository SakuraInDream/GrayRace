using System.Reflection;
using HarmonyLib;
using RimWorld;
using SD.GrayRace.HarmonyPatches;
using Verse;

namespace SD.GrayRace
{
    // 预留 Mod 配置
    // 注：在 Mod 的 Harmony 补丁相较于 StaticConstructorOnStartup 要晚
    public class GrayRaceMod : Mod
    {
        public static Harmony HarmonyInstance;

        public GrayRaceMod(ModContentPack content) : base(content)
        {
#if DEBUG
            Harmony.DEBUG = true;
#endif
            HarmonyInstance = new Harmony("sd.grayrace.mod");
            MethodInfo originalMethod = AccessTools.PropertyGetter(typeof(ResearchProjectDef), nameof(ResearchProjectDef.UnlockedDefs));
            HarmonyMethod transpiler = new HarmonyMethod(typeof(ResearchProjectDefPatches), nameof(ResearchProjectDefPatches.UnlockedDefsTranspiler));
            HarmonyInstance.Patch(original: originalMethod, transpiler: transpiler);
        }
    }
}
