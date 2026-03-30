using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SD.GrayRace.JobDrivers;
using SD.GrayRace.Needs;
using Verse;
using Verse.AI;

namespace SD.GrayRace.JobGivers
{
    public class JobGiver_GetMetal: ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!pawn.IsGrayRace())
            {
                return null;
            }
            if (!pawn.needs.TryGetNeed<Need_GrayRaceEnergy>(out var needGrayRaceEnergy))
            {
                return null;
            }

            if (!needGrayRaceEnergy.stopSeekingMetal)
                return null;

            if (needGrayRaceEnergy.CurLevelPercentage > 0.2f)
            {
                return null;
            }

            Thing steel = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableAlways),
                PathEndMode.Touch,
                TraverseParms.For(pawn),
                9999f,
                x => x.def == ThingDefOf.Steel && !x.IsForbidden(pawn) && pawn.CanReserve(x));

            if (steel != null)
            {
                // 优先找钢铁
                return JobMaker.MakeJob(GrayRaceDefOf.GR_ConsumeMetal, steel);
            }

            // 否则找最大质量的金属
            Thing bestBigMassMetal = IsMaxMassMetal(pawn);
            if (bestBigMassMetal != null)
            {
                return JobMaker.MakeJob(GrayRaceDefOf.GR_ConsumeMetal, bestBigMassMetal);
            }

            return null;
        }

        public override float GetPriority(Pawn pawn)
        {
            var energy = pawn?.needs?.TryGetNeed<Need_GrayRaceEnergy>();
            if(energy == null) return 0f;

            if (energy.CurLevel < 0.2f)
                return 9.6f;

            if (energy.CurLevel > 0.8f)
                return 0f;

            return 8f;
        }

        private Thing IsMaxMassMetal(Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null)
            {
                return null;
            }

            var candidates = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableAlways);
            Thing best = null;
            float bestMass = -1f;

            for (int i = 0; i < candidates.Count; i++)
            {
                Thing t = candidates[i];
                if (!t.def.IsMetal || t.IsForbidden(pawn) || !pawn.CanReserve(t))
                {
                    continue;
                }

                float mass = t.GetStatValue(StatDefOf.Mass);
                if (mass > bestMass)
                {
                    bestMass = mass;
                    best = t;
                }
            }

            return best;
        }
    }
}
