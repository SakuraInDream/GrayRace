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
                return stat.defaultBaseValue;
            }

            return 0f;
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            if (req.Thing is Pawn pawn && pawn.IsGrayRace())
            {
                // 基础值: x
                return "StatsReport_BaseValue".Translate() + ": " + stat.defaultBaseValue.ToStringByStyle(stat.toStringStyle);
            }
            return string.Empty;
        }

        public override bool ShouldShowFor(StatRequest req)
        {
            if (!base.ShouldShowFor(req)) return false;

            Pawn pawn = req.Thing as Pawn;

            return pawn.IsGrayRace();
        }


    }
}
