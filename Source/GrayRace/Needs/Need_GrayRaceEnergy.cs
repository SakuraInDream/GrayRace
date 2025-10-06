using RimWorld;
using Verse;

namespace SD.GrayRace.Needs
{
    public class Need_GrayRaceEnergy: Need
    {
        public Need_GrayRaceEnergy(Pawn pawn) : base(pawn)
        {
        }

        public override void NeedInterval()
        {
            if (!IsFrozen)
            {
                CurLevel -= 0.002f;
            }
        }
    }
}
