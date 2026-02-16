using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Hediffs;
using SD.GrayRace.Needs;
using Verse;

namespace SD.GrayRace.StatWorkers;

// 可能还要改
public class StatPart_NaniteDrain: StatPart
{
    public override void TransformValue(StatRequest req, ref float val)
    {
        if (req.HasThing && req.Thing is Pawn pawn && pawn.health?.hediffSet != null)
        {
            var hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                var comp = hediffs[i].TryGetComp<HediffComp_NanitesRegeneration>();
                if (comp != null && comp.isRegenerationActive)
                {
                    val -= comp.Props.naniteCostPerSeconds;
                    return;
                }
            }
        }
    }

    public override string ExplanationPart(StatRequest req)
    {
        if (req.HasThing && req.Thing is Pawn pawn && pawn.health?.hediffSet?.hediffs.Count > 0)
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            foreach (Hediff hediff in hediffs)
            {
                var comp = hediff.TryGetComp<HediffComp_NanitesRegeneration>();
                if (comp != null && comp.isRegenerationActive)
                {
                    return $"灰潮再生: -{comp.Props.naniteCostPerSeconds}"; // 待本地化
                }
            }
        }

        return string.Empty;
    }
}
