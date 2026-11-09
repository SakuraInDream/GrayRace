using CombatExtended;
using SD.GrayRace.Comps;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;

namespace SD.GrayRace.CE.Comps;

/// <summary>
/// CE 专用多硬点炮塔 Comp。
/// 主程序集的 CompMultiTurretGun 不知道 CE 的存在；
/// 这个派生类只负责把 CE 投射物交给 CE 的 ProjectileCE 发射逻辑。
/// </summary>
public class CompMultiTurretGunCE : CompMultiTurretGun
{
    protected override bool TryLaunchProjectile(
        MechHardpoint hp,
        Pawn pawn,
        Verb verb,
        ThingDef projectileDef,
        LocalTargetInfo target,
        ShootLine shootLine,
        Vector3 origin)
    {
        // 只做一次轻量的类型判断；命中 CE 投射物后直接走下面的专用发射器，
        // 不使用 Harmony / 反射。
        if (projectileDef?.thingClass != null
            && typeof(ProjectileCE).IsAssignableFrom(projectileDef.thingClass)
            && projectileDef.projectile is ProjectilePropertiesCE props
            && verb is Verb_LaunchProjectileCE ceVerb)
        {
            return CEHardpointProjectileLauncher.TryLaunch(hp, pawn, ceVerb, projectileDef, props, target, origin);
        }

        // 未走 CE 投射物的模块继续使用主程序集的原版逻辑。
        return base.TryLaunchProjectile(hp, pawn, verb, projectileDef, target, shootLine, origin);
    }
}
