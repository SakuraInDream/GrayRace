using UnityEngine;
using Verse;

namespace SD.GrayRace
{
    [StaticConstructorOnStartup]
    public static class GRUtils
    {
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
    }
}
