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

        /// <summary>本 Mod 的持久化设置。UI 窗口几何之类不随存档走的状态放这里。</summary>
        public static GrayRaceModSettings Settings;

        public GrayRaceMod(ModContentPack content) : base(content)
        {
#if DEBUG
            Harmony.DEBUG = true;
#endif
            Settings = GetSettings<GrayRaceModSettings>();

            HarmonyInstance = new Harmony("sd.grayrace.mod");
            MethodInfo originalMethod = AccessTools.PropertyGetter(typeof(ResearchProjectDef), nameof(ResearchProjectDef.UnlockedDefs));
            HarmonyMethod transpiler = new HarmonyMethod(typeof(ResearchProjectDefPatches), nameof(ResearchProjectDefPatches.UnlockedDefsTranspiler));
            HarmonyInstance.Patch(original: originalMethod, transpiler: transpiler);
            HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
