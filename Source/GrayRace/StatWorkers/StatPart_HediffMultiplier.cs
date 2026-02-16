using RimWorld;
using Verse;

namespace SD.GrayRace.StatWorkers;

// 备用
public class StatPart_HediffMultiplier: StatPart
{
    public HediffDef hediff;
    public SimpleCurve multiplierCurve;
    public override void TransformValue(StatRequest req, ref float val)
    {
        if (req.Thing is Pawn pawn)
        {
            Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(hediff);
            if (h != null)
            {
                float factor = multiplierCurve.Evaluate(h.Severity);
                val *= factor;
            }
        }
    }

    public override string ExplanationPart(StatRequest req)
    {
        if (req.Thing is Pawn pawn)
        {
            Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(hediff);
            if (h != null)
            {
                float factor = multiplierCurve.Evaluate(h.Severity);
                if (factor >= 0.99f) return string.Empty;

                return $"{h.LabelCap} ({h.Severity:P0}): x{factor.ToStringPercent()}";
            }
        }

        return string.Empty;
    }
}
