using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Needs;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SD.GrayRace.JobDrivers
{
    public class JobDriver_ConsumeMetal: JobDriver
    {
        private const float KgToEnergy = 0.1f;
        public Need_GrayRaceEnergy Energy => pawn.needs.TryGetNeed<Need_GrayRaceEnergy>();
        public Thing Metal => job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Metal == null) return false;

            return pawn.Reserve(Metal, job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnForbidden(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.
                Wait(300).
                FailOnDestroyedNullOrForbidden(TargetIndex.A).
                FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch).
                WithProgressBarToilDelay(TargetIndex.A);

            var consumeToil = new Toil
            {
                initAction = () =>
                {
                    if (Metal == null || Metal.Destroyed) return;

                    float metalMass = Metal.GetStatValue(StatDefOf.Mass);

                    if (Energy == null) return;

                    float needDelta = Energy.MaxLevel - Energy.CurLevel;

                    float kgNeedDelta = needDelta / KgToEnergy;

                    int thingcountNeed = Mathf.CeilToInt(kgNeedDelta / metalMass);

                    float energyGained = metalMass * KgToEnergy;

                    if(thingcountNeed > Metal.stackCount)
                    {
                        thingcountNeed = Metal.stackCount;
                        Metal.Destroy();
                    }
                    else
                    {
                        Metal.SplitOff(thingcountNeed).Destroy();
                    }
                    energyGained *= thingcountNeed;

                    Energy.CurLevel += energyGained;
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };

            yield return consumeToil;
        }
    }
}
