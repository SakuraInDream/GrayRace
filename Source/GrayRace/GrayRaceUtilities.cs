using SD.GrayRace.Comps;
using UnityEngine;
using Verse;

namespace SD.GrayRace
{
    public static class GrayRaceUtilities
    {
        public static bool IsGrayRace(this Pawn pawn)
        {
            return pawn?.def == GrayRaceDefOf.Gray_Race;
        }

        public static void DrawWindowBackgroundWithTexture(Rect rect, Texture2D texture)
        {
            GUI.DrawTexture(rect, texture);
        }

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
