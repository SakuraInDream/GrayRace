using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Mechs;

public static class TurretVerbDrawUtility
{
    private static readonly Dictionary<Thing, Vector3> ProjectileOrigins = new();

    public static void BeginProjectileOriginOverride(Thing caster, Vector3 origin)
    {
        if (caster != null)
        {
            ProjectileOrigins[caster] = origin;
        }
    }

    public static void EndProjectileOriginOverride(Thing caster)
    {
        if (caster != null)
        {
            ProjectileOrigins.Remove(caster);
        }
    }

    public static bool TryGetProjectileOrigin(Thing caster, out Vector3 origin)
    {
        if (caster != null && ProjectileOrigins.TryGetValue(caster, out origin))
        {
            return true;
        }

        origin = default;
        return false;
    }
}
