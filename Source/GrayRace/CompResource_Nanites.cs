using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace SD.GrayRace
{
    // 纳米机械资源实现
    public class CompResource_Nanites: ThingComp
    {
        public float targetValue = 0.5f;
        
        protected float cur;
        
        protected float max;

        protected Gizmo_NaniteResources gizmo;

        public virtual string ResourceLabel => Props.resourceLabel;
        public Pawn Pawn => parent as Pawn;
        public CompProperties_Nanites Props => (CompProperties_Nanites)this.props;
        public virtual float InitialResourceMax => Props.maxResource;

        public float TargetValue
        {
            get => targetValue;
            set => targetValue = value;
        }
        public float CurResource => cur;

        public bool CanRegenNanites
        {
            get
            {
                if (Pawn.InMentalState || Pawn.Dead || Pawn.Deathresting) return false;

                return Pawn != null && !Pawn.needs.food.Starving && cur <= targetValue;
            }
        }
        
        public virtual int ValueForDisplay => PostProcessValue(cur);

        public virtual int MaxForDisplay => PostProcessValue(max);

        public virtual float Max => max;

        public virtual float Value
        {
            get => cur;
            set => cur = Mathf.Clamp(value, 0f, max);
        }

        public virtual float ValuePercent
        {
            get => cur / Props.maxResource;
            set => cur = value * Props.maxResource;
        }

        public void SetMax(float newmax)
        {
            max = newmax;
            cur = Mathf.Clamp(cur, 0f, max);
            SetTargetValuePct(targetValue);
        }

        public void ResetMax()
        {
            max = InitialResourceMax;
            cur = Mathf.Clamp(cur, 0f, max);
            SetTargetValuePct(targetValue);
        }

        protected virtual void Reset()
        {
            max = InitialResourceMax;
            targetValue = 0.5f * max;
        }
        
        public virtual void SetTargetValuePct(float value)
        {
            targetValue = value * Max;
        }

        public virtual int PostProcessValue(float value) => Mathf.RoundToInt(value * 100f);


        public override void Initialize(CompProperties prop)
        {
            base.Initialize(prop);
            targetValue = Props.maxResource * 0.5f;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref cur, "cur");
            Scribe_Values.Look(ref max, "max");
            Scribe_Values.Look(ref targetValue, "targetValue", 0.5f * max);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            Reset();
        }

        public override void CompTickInterval(int delta)
        {
            if (!parent.IsHashIntervalTick(120, delta) || CurResource >= targetValue) return;

            if (CanRegenNanites)
            {
                cur += Props.regenPerTick;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Gizmo_NaniteResources(this);
            
            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = $"-20% {ResourceLabel}",
                    action = () =>
                    {
                        GRUtils.OffsetNanites(Pawn, -max * 0.2f);
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = $"+20% {ResourceLabel}",
                    action = () =>
                    {
                        GRUtils.OffsetNanites(Pawn, max * 0.2f);
                    }
                };
            }
        }
    }
}