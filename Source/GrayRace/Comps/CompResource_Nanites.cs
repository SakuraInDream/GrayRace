using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace SD.GrayRace
{
    // 纳米机械资源实现
    public class CompResource_Nanites: ThingComp
    {
        protected float cur;
        
        protected float max;

        protected Gizmo_NaniteResources gizmo;

        public virtual string ResourceLabel => Props.resourceLabel;
        
        public Pawn Pawn => parent as Pawn;
        
        public CompProperties_Nanites Props => (CompProperties_Nanites)props;

        public virtual float InitialResourceMax => Props.maxResource;

        public bool HasEnoughResource(float cost)
        {
            return cur >= cost;
        }

        public float CurResource => cur;

        public bool CanRegenNanites
        {
            get
            {
                if (Pawn.InMentalState || Pawn.Dead || Pawn.Deathresting) return false;

                return Pawn != null && !Pawn.needs.food.Starving; // 随时换掉food的判断
            }
        }
        // 当前资源显示
        public virtual int ValueForDisplay => PostProcessValue(cur);
        
        // 最大资源显示
        public virtual int MaxForDisplay => PostProcessValue(max);
        // 获取最大资源量
        public virtual float Max => max;
        
        // 获取当前资源量
        public virtual float Value
        {
            get => cur;
            set => cur = Mathf.Clamp(value, 0f, max);
        }
        // 获取当前资源百分比
        public virtual float ValuePercent
        {
            get => cur / Props.maxResource;
            set => cur = value * Props.maxResource;
        }

        protected virtual void Reset()
        {
            max = InitialResourceMax;
        }

        public virtual int PostProcessValue(float value) => Mathf.RoundToInt(value * 100f);


        public override void Initialize(CompProperties prop)
        {
            base.Initialize(prop);
            gizmo = new Gizmo_NaniteResources(this);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref cur, "cur");
            Scribe_Values.Look(ref max, "max");
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            Reset();
        }

        public override void CompTickInterval(int delta)
        {
            if (!parent.IsHashIntervalTick(60, delta) || CurResource >= Props.maxResource) return;
            
            // 还需要一个根据 NeedDef 影响回复速度的判断
            if (CanRegenNanites)
            {
                cur += Props.regenPerSecond;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return gizmo;
            
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
