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
            MethodInfo tryGetAttackVerbMethod = AccessTools.Method(typeof(Pawn), nameof(Pawn.TryGetAttackVerb));
            // HarmonyMethod patch = new HarmonyMethod(typeof(ResearchProjectDefPatches), nameof(ResearchProjectDefPatches.UnlockedDefsPostfix));
            HarmonyMethod transpiler = new HarmonyMethod(typeof(ResearchProjectDefPatches), nameof(ResearchProjectDefPatches.UnlockedDefsTranspiler));
            HarmonyMethod tryGetAttackVerbPostfix = new HarmonyMethod(typeof(PawnPatches), nameof(PawnPatches.TryGetAttackVerbPostfix));

            // HarmonyInstance.Patch(original: originalMethod, postfix: patch);
            HarmonyInstance.Patch(original: originalMethod, transpiler: transpiler);
            HarmonyInstance.Patch(original: tryGetAttackVerbMethod, postfix: tryGetAttackVerbPostfix);
        }
    }
}
