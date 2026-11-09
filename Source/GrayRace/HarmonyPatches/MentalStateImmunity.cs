using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SD.GrayRace.HarmonyPatches;

/// <summary>
/// 挂在 ThingDef 上的精神状态免疫配置（Gray_PawnBase 继承给 Gray_Race）。
/// 规则：只放行“原生心情崩溃管线”以及 <see cref="allowedMentalStates"/> 中显式列出的状态。
/// </summary>
public class MentalStateImmunity : DefModExtension
{
    public List<string> allowedMentalStates = new List<string>();

    private HashSet<MentalStateDef> allowedCache;

    public bool Allows(MentalStateDef stateDef)
    {
        if (stateDef == null)
        {
            return false;
        }

        if (allowedCache == null)
        {
            allowedCache = new HashSet<MentalStateDef>();
            if (allowedMentalStates != null)
            {
                foreach (string defName in allowedMentalStates)
                {
                    if (defName.NullOrEmpty())
                    {
                        continue;
                    }

                    MentalStateDef def = DefDatabase<MentalStateDef>.GetNamedSilentFail(defName);
                    if (def != null)
                    {
                        allowedCache.Add(def);
                    }
#if DEBUG
                    else
                    {
                        Log.WarningOnce($"[GrayRace] MentalStateImmunity: unknown MentalStateDef '{defName}'", defName.GetHashCode());
                    }
#endif
                }
            }
        }

        return allowedCache.Contains(stateDef);
    }
}

/// <summary>记录当前线程正在执行的“原生心情崩溃”所属 Pawn。</summary>
internal static class MoodBreakContext
{
    [ThreadStatic]
    internal static Pawn Current;
}

internal static class MentalStateImmunityUtility
{
    internal static bool ImmunityEnabled =>
        GrayRaceMod.Settings == null || GrayRaceMod.Settings.enableMentalStateImmunity;

    internal static MentalStateImmunity GetImmunity(Pawn pawn)
    {
        if (!ImmunityEnabled || pawn == null)
        {
            return null;
        }

        return pawn.def?.GetModExtension<MentalStateImmunity>();
    }

    private static HashSet<MentalStateDef> moodBreakStates;

    /// <summary>该状态是否是某个原版 MentalBreakDef 的目标状态（用于旧存档清理）。</summary>
    internal static bool IsMoodBreakState(MentalStateDef stateDef)
    {
        if (stateDef == null)
        {
            return false;
        }

        if (moodBreakStates == null)
        {
            moodBreakStates = new HashSet<MentalStateDef>();
            foreach (MentalBreakDef breakDef in DefDatabase<MentalBreakDef>.AllDefsListForReading)
            {
                if (breakDef.mentalState != null)
                {
                    moodBreakStates.Add(breakDef.mentalState);
                }
            }
        }

        return moodBreakStates.Contains(stateDef);
    }
}

/// <summary>
/// 标记“当前这次崩溃来自原生心情管线”。
/// 只有这条调用链内的 TryDoMentalBreak / TryStartMentalState 才会被放行。
/// </summary>
[HarmonyPatch(typeof(MentalBreaker), nameof(MentalBreaker.TryDoRandomMoodCausedMentalBreak))]
internal static class Patch_MentalBreaker_MoodBreakContext
{
    [HarmonyPrefix]
    public static void Prefix(Pawn ___pawn, out Pawn __state)
    {
        __state = MoodBreakContext.Current;
        MoodBreakContext.Current = ___pawn;
    }

    [HarmonyFinalizer]
    public static void Finalizer(Pawn __state)
    {
        MoodBreakContext.Current = __state;
    }
}

/// <summary>阻止 Gray 走非心情管线的 MentalBreakDef（含 Catatonic / RunWild / 血怒 / 狂暴场 / mod 强制）。</summary>
[HarmonyPatch(typeof(MentalBreaker), nameof(MentalBreaker.TryDoMentalBreak))]
internal static class Patch_MentalBreaker_TryDoMentalBreak
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn ___pawn)
    {
        // 原生心情管线：直接放行，避免扩展查找。
        if (ReferenceEquals(MoodBreakContext.Current, ___pawn))
        {
            return true;
        }

        return MentalStateImmunityUtility.GetImmunity(___pawn) == null;
    }
}

/// <summary>阻止一切未列入白名单、且不在原生心情管线内的精神状态启动。</summary>
[HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
internal static class Patch_MentalStateHandler_TryStartMentalState
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn ___pawn, MentalStateDef stateDef)
    {
        // 原生心情管线：直接放行，避免扩展查找。
        if (ReferenceEquals(MoodBreakContext.Current, ___pawn))
        {
            return true;
        }

        MentalStateImmunity immunity = MentalStateImmunityUtility.GetImmunity(___pawn);
        if (immunity == null || stateDef == null)
        {
            return true;
        }

        return immunity.Allows(stateDef);
    }
}

/// <summary>旧存档清理：静默移除 Gray 身上不该存在的强制精神状态。</summary>
[HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.ExposeData))]
internal static class Patch_MentalStateHandler_ExposeData
{
    [HarmonyPostfix]
    public static void Postfix(MentalStateHandler __instance, Pawn ___pawn)
    {
        if (Scribe.mode != LoadSaveMode.PostLoadInit)
        {
            return;
        }

        // 绝大多数 Pawn 加载时没有精神状态，先判断可以省掉扩展查找。
        MentalState curState = __instance.CurState;
        if (curState == null)
        {
            return;
        }

        MentalStateDef curStateDef = curState.def;
        if (curStateDef == null)
        {
            return;
        }

        MentalStateImmunity immunity = MentalStateImmunityUtility.GetImmunity(___pawn);
        if (immunity == null)
        {
            return;
        }

        if (immunity.Allows(curStateDef))
        {
            return;
        }

        // 旧存档里由心情管线产生、且确实是心情崩溃的状态保留。
        if (curState.causedByMood && MentalStateImmunityUtility.IsMoodBreakState(curStateDef))
        {
            return;
        }

        __instance.Reset();
    }
}
