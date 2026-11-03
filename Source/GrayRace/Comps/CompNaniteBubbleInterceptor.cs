using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SD.GrayRace.Comps;

/// <summary>
/// 纳米群体护盾：完全沿用原版 <see cref="CompProjectileInterceptor"/>。
/// 拦截、气泡渲染、无血条（hitPoints = -1）、不拦己方射弹、EMP 免疫、跟随持有者，
/// 全部是原版已有行为，本类只补一个开关：
/// currentHitPoints 只当开关用（-1 开 / 0 关），不参与耐久计算。
///
/// 半径、颜色、是否拦飞行弹等全部在 ThingDef 的 comp 里配置。
/// </summary>
public class CompNaniteBubbleInterceptor : CompProjectileInterceptor
{
    /// <summary>护盾是否处于开启状态。</summary>
    public bool ShieldEnabled => currentHitPoints != 0;

    /// <summary>由 Hediff 调用的开关。-1 = 开启（无限耐久），0 = 关闭。</summary>
    public void SetShieldEnabled(bool enabled)
    {
        currentHitPoints = enabled ? -1 : 0;
    }

    public override void PostPostMake()
    {
        base.PostPostMake();
        // 种族 ThingDef 上常驻这个 comp，必须默认关闭，只有 Hediff 才能打开。
        currentHitPoints = 0;
    }

    /// <summary>
    /// 老存档的 Pawn 生成时还没有这个 comp，这里按 def 上的配置动态补一个。
    /// 找不到配置或创建失败时返回 null（护盾静默不生效）。
    /// </summary>
    public static CompNaniteBubbleInterceptor TryAttachTo(Pawn pawn)
    {
        ThingDef def = pawn?.def;
        if (def?.comps == null || pawn.AllComps == null)
        {
            return null;
        }

        List<CompProperties> propsList = def.comps;
        for (int i = 0; i < propsList.Count; i++)
        {
            if (propsList[i] is CompProperties_ProjectileInterceptor props
                && typeof(CompNaniteBubbleInterceptor).IsAssignableFrom(props.compClass))
            {
                try
                {
                    CompNaniteBubbleInterceptor comp =
                        (CompNaniteBubbleInterceptor)Activator.CreateInstance(props.compClass);
                    comp.Initialize(props);
                    comp.parent = pawn;
                    pawn.AllComps.Add(comp);
                    comp.PostPostMake();
                    return comp;
                }
                catch (Exception ex)
                {
                    Log.Error($"[GrayRace] Failed to attach {nameof(CompNaniteBubbleInterceptor)} to {pawn}: {ex}");
                    return null;
                }
            }
        }

        return null;
    }
}
