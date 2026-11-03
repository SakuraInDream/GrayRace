using SD.GrayRace.Comps;
using System.Collections.Generic;
using Verse;

namespace SD.GrayRace.Hediffs;

public class HediffCompProperties_NaniteBubbleShield : HediffCompProperties
{
    public HediffCompProperties_NaniteBubbleShield()
    {
        compClass = typeof(HediffComp_NaniteBubbleShield);
    }
}

/// <summary>
/// 把 Hediff 的存续映射为 <see cref="CompNaniteBubbleInterceptor"/> 的开关。
/// Hediff 在 = 护盾开（持续消耗源质），Hediff 消失/死亡 = 护盾关。
/// 护盾本身的半径、颜色、拦截规则全部写在 ThingDef 的 comp 里，这里不写死任何数值。
/// </summary>
public class HediffComp_NaniteBubbleShield : HediffComp
{
    /// <summary>周期性校对间隔，防止外部（开发者模式等）改坏 comp 状态。</summary>
    private const int SyncIntervalTicks = 60;

    private CompNaniteBubbleInterceptor Shield
    {
        get
        {
            // 线性遍历，避免 ThingWithComps.compsByType 缓存对运行时新增 comp 的干扰。
            List<ThingComp> comps = Pawn?.AllComps;
            if (comps != null)
            {
                for (int i = 0; i < comps.Count; i++)
                {
                    if (comps[i] is CompNaniteBubbleInterceptor shield)
                    {
                        return shield;
                    }
                }
            }

            return null;
        }
    }

    public override void CompPostPostAdd(DamageInfo? dinfo)
    {
        base.CompPostPostAdd(dinfo);
        SetShieldEnabled(true);
    }

    public override void CompExposeData()
    {
        base.CompExposeData();
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            SetShieldEnabled(true);
        }
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || !pawn.IsHashIntervalTick(SyncIntervalTicks))
        {
            return;
        }

        CompNaniteBubbleInterceptor shield = Shield;
        if (shield == null)
        {
            // 老存档补挂，或者 def 被改过。
            shield = CompNaniteBubbleInterceptor.TryAttachTo(pawn);
        }

        if (shield != null && !shield.ShieldEnabled)
        {
            shield.SetShieldEnabled(true);
        }
    }

    public override void CompPostPostRemoved()
    {
        base.CompPostPostRemoved();
        SetShieldEnabled(false);
    }

    public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit)
    {
        base.Notify_PawnDied(dinfo, culprit);
        SetShieldEnabled(false);
    }

    private void SetShieldEnabled(bool enabled)
    {
        CompNaniteBubbleInterceptor shield = Shield ?? CompNaniteBubbleInterceptor.TryAttachTo(Pawn);
        shield?.SetShieldEnabled(enabled);
    }
}
