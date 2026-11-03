using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Hediffs;
using Verse;

namespace SD.GrayRace.Abilities;

/// <summary>
/// 无需目标的开关型纳米能力：
/// - 未激活时点击：扣除 Ability 自身消耗（CompProperties_AbilityNanitesCost）并施加效果 Hediff；
/// - 已激活时点击：直接移除效果 Hediff（关闭不再收费）。
/// 立即生效，不走 Job/Verb 流程。
/// </summary>
public class Ability_NaniteToggle : Ability_NaniteCost
{
    private Command_NaniteToggle _gizmo;
    public Ability_NaniteToggle()
    {
    }

    public Ability_NaniteToggle(Pawn pawn) : base(pawn)
    {
    }

    public Ability_NaniteToggle(Pawn pawn, Precept sourcePrecept) : base(pawn, sourcePrecept)
    {
    }

    public Ability_NaniteToggle(Pawn pawn, AbilityDef def) : base(pawn, def)
    {
    }

    public Ability_NaniteToggle(Pawn pawn, Precept sourcePrecept, AbilityDef def) : base(pawn, sourcePrecept, def)
    {
    }

    /// <summary>开关目标的 Hediff（由 AbilityComp_ApplyHediffToSelf 提供）。</summary>
    public HediffDef ToggleHediffDef
    {
        get
        {
            AbilityComp_ApplyHediffToSelf comp = CompOfType<AbilityComp_ApplyHediffToSelf>();
            return comp?.Props.hediffDef;
        }
    }

    public bool EffectActive
    {
        get
        {
            HediffDef def = ToggleHediffDef;
            return def != null && pawn?.health != null && pawn.health.hediffSet.HasHediff(def);
        }
    }

    /// <summary>悬浮在能力 gizmo 上时追加持续消耗，来源是目标 Hediff 上的 HediffComp_NaniteUpkeep。</summary>
    public override string Tooltip
    {
        get
        {
            string text = base.Tooltip;
            float rate = UpkeepNanitesPerSecond;
            if (rate > 0f)
            {
                text += "\n" + "GR_NaniteUpkeepRate".Translate(rate.ToString("0.#"));
            }

            return text;
        }
    }

    private float UpkeepNanitesPerSecond
    {
        get
        {
            HediffDef hediffDef = ToggleHediffDef;
            if (hediffDef?.comps == null)
            {
                return 0f;
            }

            for (int i = 0; i < hediffDef.comps.Count; i++)
            {
                if (hediffDef.comps[i] is HediffCompProperties_NaniteUpkeep upkeep)
                {
                    return upkeep.naniteCostPerSecond;
                }
            }

            return 0f;
        }
    }

    public override void QueueCastingJob(LocalTargetInfo target, LocalTargetInfo destination)
    {
        Toggle();
    }

    /// <summary>开/关效果。开启时扣除 Ability 自身消耗；关闭不收费。</summary>
    public void Toggle()
    {
        if (EffectActive)
        {
            RemoveEffect();
            return;
        }

        if (!CanCast)
        {
            return;
        }

        if (Activate(new LocalTargetInfo(pawn), LocalTargetInfo.Invalid))
        {
            StartCooldown(def.cooldownTicksRange.RandomInRange);
        }
    }

    public override IEnumerable<Command> GetGizmos()
    {
        _gizmo ??= new Command_NaniteToggle(this);
        yield return _gizmo;
    }

    private void RemoveEffect()
    {
        Hediff active = pawn?.health?.hediffSet?.GetFirstHediffOfDef(ToggleHediffDef);
        if (active != null)
        {
            pawn.health.RemoveHediff(active);
        }
    }

    public override bool GizmoDisabled(out string reason)
    {
        // 已开启时允许随时关闭，不受激活冷却限制
        if (EffectActive)
        {
            reason = null;
            return false;
        }

        return base.GizmoDisabled(out reason);
    }
}
