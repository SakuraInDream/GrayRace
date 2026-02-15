using RimWorld;
using SD.GrayRace.Needs;
using Verse;

namespace SD.GrayRace.StatWorkers;

// 根据能量调整再生速率，为 0 时不再生
public class StatPart_NaniteGen: StatPart_Curve
{
    protected override bool AppliesTo(StatRequest req)
    {
        return req.Thing is Pawn pawn && pawn.needs.TryGetNeed<Need_GrayRaceEnergy>() != null;
    }

    protected override float CurveXGetter(StatRequest req)
    {
        if (req.Thing is Pawn pawn && pawn.needs.TryGetNeed<Need_GrayRaceEnergy>() != null)
        {
            return pawn.needs.TryGetNeed<Need_GrayRaceEnergy>().CurLevelPercentage;
        }

        return 0f;
    }

    protected override string ExplanationLabel(StatRequest req)
    {
        if (req.Thing is Pawn pawn)
        {
            var need = pawn.needs.TryGetNeed<Need_GrayRaceEnergy>();
            if (need != null)
            {
                return need.LabelCap;
            }
        }
        return string.Empty;
    }
}
