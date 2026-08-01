using RimWorld;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Projectiles;

// 噶人焖谁懂啊，实现个激光从武器发射还要单独写一个 Bullet
public class Projectile_GrayBeam : Bullet
{
    public override Vector3 ExactPosition => destination + Vector3.up * def.Altitude;

    public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
    {
        base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);

        if (def.projectile.beamMoteDef != null)
        {
            Vector3 sourceOffset = (origin - launcher.DrawPos).Yto0();
            MoteMaker.MakeInteractionOverlay(
                def.projectile.beamMoteDef,
                launcher,
                usedTarget.ToTargetInfo(Map),
                sourceOffset,
                Vector3.zero);
        }

        ImpactSomething();
    }
}
