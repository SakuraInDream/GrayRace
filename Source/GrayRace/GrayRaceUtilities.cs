using System;
using System.Linq;
using System.Reflection;
using SD.GrayRace.Attributes;
using UnityEngine;
using Verse;

namespace SD.GrayRace
{
    // 一些会用到的神奇妙妙工具
    [StaticConstructorOnStartup]
    public static class GrayRaceUtilities
    {
        public static bool IsGrayRace(this Pawn pawn)
        {
            return pawn?.kindDef.race == DefDatabase<ThingDef>.GetNamedSilentFail("Gray_Race");
        }
        public static void DrawWindowBackgroundWithTexture(Rect rect, Texture2D texture)
        {
            GUI.DrawTexture(rect, texture);
            // Widgets.DrawBox(rect);
        }

        // 消耗资源
        public static void OffsetNanites(Pawn pawn, float offset)
        {
            var CompNanites = pawn.TryGetComp<CompResource_Nanites>();
            if (CompNanites != null)
            {
                CompNanites.Value += offset;
                if(CompNanites.Value > CompNanites.Max)
                {
                    CompNanites.Value = CompNanites.Max;
                }

                if (CompNanites.Value <= 0.01f)
                {
                    CompNanites.Value = 0f;
                }
            }
        }

        public static bool TryConsumeNanites(Pawn pawn, float amount)
        {
            var comp = pawn.TryGetComp<CompResource_Nanites>();
            if (comp == null) return false;

            if (!comp.HasEnoughResource(amount)) return false;

            OffsetNanites(pawn, 0f - amount);

            return true;
        }

        // 本地化枚举
        // 在枚举字段上添加 [Localized("本地化文本")] 特性
        // 如果没有该特性，则使用枚举名称进行本地化
        // 使用示例： myEnumValue.ToLocalizedString();
        public static string ToLocalizedString(this Enum value)
        {
            if (value == null) return string.Empty;
            var field = value.GetType().GetField(value.ToString());
            if (field != null)
            {
                var attr = field.GetCustomAttribute<LocalizedAttribute>();
                if(attr != null && !string.IsNullOrEmpty(attr.Text))
                {
                    return attr.Text.Translate();
                }
            }
            return value.ToString().Translate();
        }
    }
}
