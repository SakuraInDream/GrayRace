using Verse;

namespace SD.GrayRace
{
    [StaticConstructorOnStartup]
    public static class GRUtils
    {
        static GRUtils()
        {
            
        }

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

                if (CompNanites.Value <= 0.1f)
                {
                    CompNanites.Value = 0f;
                }
            }
        }
    }
}
