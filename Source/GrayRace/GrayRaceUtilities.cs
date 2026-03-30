using SD.GrayRace.Comps;
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
            return pawn?.def == GrayRaceDefOf.Gray_Race;
        }

        public static void DrawWindowBackgroundWithTexture(Rect rect, Texture2D texture)
        {
            GUI.DrawTexture(rect, texture);
            // Widgets.DrawBox(rect);
        }

        // 消耗资源
        // public static void OffsetNanites(Pawn pawn, float offset)
        // {
        //     // var compNanites = pawn.TryGetComp<CompResource_Nanites>();
        //     var compNanites = pawn.TryGetComp<CompResource_Nanites>();
        //     if (compNanites != null)
        //     {
        //         compNanites.CurrentNanites += offset;
        //         if (compNanites.CurrentNanites > compNanites.Max)
        //         {
        //             compNanites.CurrentNanites = compNanites.Max;
        //         }
        //
        //         if (compNanites.CurrentNanites <= 0.01f)
        //         {
        //             compNanites.CurrentNanites = 0f;
        //         }
        //     }
        // }

        // public static bool TryConsumeNanites(Pawn pawn, float amount)
        // {
        //     // var comp = pawn.TryGetComp<CompResource_Nanites>();
        //     var comp = pawn.TryGetComp<CompResource_Nanites>();
        //
        //     if (comp == null) return false;
        //
        //     // if (!comp.HasEnoughResource(amount)) return false;
        //     if(comp.CurrentNanites < amount) return false;
        //
        //     OffsetNanites(pawn, 0f - amount);
        //
        //     return true;
        // }

        public static CompGrayManager GetManager(this Pawn pawn)
        {
            return pawn.TryGetComp<CompGrayManager>();
        }

        public static Dialog_NamePawn NameGrayRaceDialog(this Pawn pawn)
        {
            return new Dialog_NamePawn(pawn, NameFilter.First | NameFilter.Nick | NameFilter.Last, NameFilter.First | NameFilter.Nick | NameFilter.Last, null);
        }
    }
}
