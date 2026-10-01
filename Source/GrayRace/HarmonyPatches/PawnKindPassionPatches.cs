using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using SD.GrayRace.DefModExtensions;
using Verse;

namespace SD.GrayRace.HarmonyPatches;

// 在 pawn 的技能生成（等级 + 随机激情 + 特质强制激情）全部完成之后，
// 按 PawnKindDef 上的 PawnKindPassionExtension 覆盖指定技能的激情。
// 这是最终覆盖：会无视特质 RequiresPassion / 基因 DropAll 的结果，以 XML 配置为准。
[HarmonyPatch(typeof(PawnGenerator))]
internal static class Patch_PawnGenerator_GenerateSkills
{
    [HarmonyPatch("GenerateSkills")]
    [HarmonyPostfix]
    public static void Postfix(Pawn pawn)
    {
        if (pawn?.skills == null)
        {
            return;
        }

        PawnKindPassionExtension extension = pawn.kindDef?.GetModExtension<PawnKindPassionExtension>();
        if (extension == null || extension.skills.NullOrEmpty())
        {
            return;
        }

        List<SkillRecord> records = pawn.skills.skills;
        for (int i = 0; i < extension.skills.Count; i++)
        {
            PawnKindPassionEntry entry = extension.skills[i];
            if (entry?.skill == null || !entry.passion.HasValue)
            {
                continue;
            }

            // 手动查找，避免 Pawn_SkillTracker.GetSkill 找不到时打 Log.Error
            SkillRecord record = null;
            for (int j = 0; j < records.Count; j++)
            {
                if (records[j].def == entry.skill)
                {
                    record = records[j];
                    break;
                }
            }

            // 技能被完全禁用时（特质/基因等）不写入激情
            if (record == null || record.TotallyDisabled)
            {
                continue;
            }

            record.passion = entry.passion.Value;
        }
    }
}
