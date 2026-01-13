using RimWorld;
using Verse;

namespace SD.GrayRace.StatWorkers
{
    public class StatWorker_GrayRace: StatWorker
    {
        public override bool ShouldShowFor(StatRequest req)
        {
            if (!base.ShouldShowFor(req)) return false;
            Pawn pawn = req.Thing as Pawn;

            return pawn.IsGrayRace();
        }
    }
}
