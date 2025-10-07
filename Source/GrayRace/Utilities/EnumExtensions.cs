using System;
using System.Collections.Generic;
using System.Reflection;
using SD.GrayRace.Attributes;
using Verse;

namespace SD.GrayRace.Utilities
{
    public class EnumMemberLocalization
    {
        public readonly string key;
        public readonly string localizedText;

        public EnumMemberLocalization(string memberName, LocalizedAttribute localizedAttribute = null)
        {
            key = localizedAttribute?.Text ?? memberName;
            localizedText = key.Translate();
        }
    }

    public static class EnumExtensions
    {
        private static readonly Dictionary<Type, Dictionary<string, EnumMemberLocalization>> s_localizationCache = new Dictionary<Type, Dictionary<string, EnumMemberLocalization>>();

        private static bool TryGetEnumMemberText(Enum enumValue, out EnumMemberLocalization enumMemberLocalization)
        {
            var enumType = enumValue.GetType();
            string memberName = enumValue.ToString();

            if (!s_localizationCache.TryGetValue(enumType, out Dictionary<string, EnumMemberLocalization> enumLocalizations))
            {
                enumLocalizations = new Dictionary<string, EnumMemberLocalization>();
                string[] names = enumType.GetEnumNames();

                foreach (string enumMemberName in names)
                {
                    MemberInfo[] enumMembers = enumType.GetMember(enumMemberName);

                    if (enumMembers.Length == 0) continue;

                    LocalizedAttribute localizedAttribute = enumMembers[0].GetCustomAttribute<LocalizedAttribute>();
                    enumLocalizations.Add(enumMemberName, new EnumMemberLocalization(enumMemberName, localizedAttribute));
                }

                s_localizationCache.Add(enumType, enumLocalizations);
            }

            return enumLocalizations.TryGetValue(memberName, out enumMemberLocalization);
        }

        // 本地化枚举
        // 在枚举字段上添加 [Localized("本地化文本")] 特性
        // 如果没有该特性，则使用枚举名称进行本地化
        // 使用示例： myEnumValue.ToLocalizedString();
        public static string ToLocalizedString(this Enum e)
        {
            if(!TryGetEnumMemberText(e, out EnumMemberLocalization enumMemberLocalization))
                return e.ToString().Translate();

            return enumMemberLocalization.localizedText;
        }


    }
}
