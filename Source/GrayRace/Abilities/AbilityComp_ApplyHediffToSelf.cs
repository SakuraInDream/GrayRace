using RimWorld;
using Verse;

namespace SD.GrayRace.Abilities;

/// <summary>
/// 让能力在自身身上施加一个 Hediff。
/// 具体生效方式（持续时间、持续消耗、护盾等）全部由该 Hediff 的 comps 决定。
/// </summary>
public class CompProperties_AbilityApplyHediffToSelf : CompProperties_AbilityEffect
{
    public HediffDef hediffDef;

    /// <summary>已存在同名 Hediff 时是否阻止再次施加。</summary>
    public bool failIfAlreadyPresent = true;

    public CompProperties_AbilityApplyHediffToSelf()
    {
        compClass = typeof(AbilityComp_ApplyHediffToSelf);
    }
}

public class AbilityComp_ApplyHediffToSelf : CompAbilityEffect
{
    public new CompProperties_AbilityApplyHediffToSelf Props =>
        (CompProperties_AbilityApplyHediffToSelf)props;

    public override bool CanCast =>
        Props.hediffDef != null
        && (!Props.failIfAlreadyPresent || !parent.pawn.health.hediffSet.HasHediff(Props.hediffDef));

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        base.Apply(target, dest);

        Pawn pawn = parent.pawn;
        if (pawn?.health == null || Props.hediffDef == null)
        {
            return;
        }

        if (Props.failIfAlreadyPresent && pawn.health.hediffSet.HasHediff(Props.hediffDef))
        {
            return;
        }

        pawn.health.AddHediff(Props.hediffDef);
    }

    public override bool GizmoDisabled(out string reason)
    {
        reason = null;
        return false;
    }
}
