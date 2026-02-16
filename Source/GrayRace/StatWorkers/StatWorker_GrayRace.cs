using RimWorld;
using Verse;

namespace SD.GrayRace.StatWorkers
{
    public class StatWorker_GrayRace: StatWorker
    {
        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            if (req.Thing is Pawn pawn && pawn.IsGrayRace())
            {
                return base.GetValueUnfinalized(req, applyPostProcess);
            }

            return 0f;
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            if (req.Thing is Pawn pawn && pawn.IsGrayRace())
            {
                return base.GetExplanationUnfinalized(req, numberSense);
            }
            return string.Empty;
        }

        public override bool ShouldShowFor(StatRequest req)
        {
            if (!base.ShouldShowFor(req)) return false;

            return req.Thing is Pawn pawn && pawn.IsGrayRace();
        }


    }
}
