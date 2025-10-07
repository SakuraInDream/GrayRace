using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
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
            // return pawn.HasComp<CompResource_Nanites>();
        }
        public static void DrawWindowBackgroundWithTexture(Rect rect, Texture2D texture)
        {
            GUI.DrawTexture(rect, texture);
            // Widgets.DrawBox(rect);
        }

        // 消耗资源
        public static void OffsetNanites(Pawn pawn, float offset)
        {
            var compNanites = pawn.TryGetComp<CompResource_Nanites>();
            if (compNanites != null)
            {
                compNanites.Value += offset;
                if (compNanites.Value > compNanites.Max)
                {
                    compNanites.Value = compNanites.Max;
                }

                if (compNanites.Value <= 0.01f)
                {
                    compNanites.Value = 0f;
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
    }
}
