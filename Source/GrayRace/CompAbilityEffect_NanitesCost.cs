using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SD.GrayRace
{
    public class CompAbilityEffect_NanitesCost: CompAbilityEffect
    {
        public new CompProperties_AbilityNanitesCost Props => (CompProperties_AbilityNanitesCost)props;

        private bool HasEnoughNanites
        {
            get
            {
                var comp = parent.pawn.TryGetComp<CompResource_Nanites>();
                return comp?.Value >= Props.nanitesCost;
            }
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return HasEnoughNanites;
        }

        public override void PostApplied(List<LocalTargetInfo> targets, Map map)
        {
            GRUtils.OffsetNanites(parent.pawn, 0f - Props.nanitesCost);
        }

        public override string ExtraTooltipPart()
        {
            return $"消耗纳米机械：{Props.nanitesCost * 100f}";
        }

        public override bool GizmoDisabled(out string reason)
        {
            var resource = parent.pawn.TryGetComp<CompResource_Nanites>();
            if (resource == null)
            {
                reason = "无使用纳米机械能力";
                return true;
            }

            if (resource.Value < Props.nanitesCost || Props.nanitesCost > float.Epsilon && Props.nanitesCost > resource.Value)
            {
                reason = "纳米机械不足";
                return true;
            }

            reason = null;
            return false;
        }
    }
}
