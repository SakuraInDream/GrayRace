using RimWorld;
using SD.GrayRace.Comps;
using Verse;

namespace SD.GrayRace.StatWorkers;

public class StatPart_OverclockDrain: StatPart
{
    public override void TransformValue(StatRequest req, ref float val)
    {
        if (req.Thing is Pawn pawn && pawn.IsGrayRace())
        {
            var comp = pawn.TryGetComp<CompOverclock>();
            if (comp != null)
            {

            }
        }
    }

    public override string ExplanationPart(StatRequest req)
    {

        return string.Empty;
    }
}
