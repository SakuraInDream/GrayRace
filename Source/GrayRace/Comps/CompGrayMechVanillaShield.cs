using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SD.GrayRace.Comps;

public class CompGrayMechVanillaShield : CompShield
{
    private float EnergyMaxStat => parent?.GetStatValue(StatDefOf.EnergyShieldEnergyMax) ?? 0f;

    private bool HasShieldCapacity => EnergyMaxStat > 0.001f;

    public void Notify_LoadoutChanged()
    {
        if (!HasShieldCapacity)
        {
            energy = 0f;
            ticksToReset = -1;
            return;
        }

        if (ticksToReset > 0)
        {
            return;
        }

        if (energy <= 0.001f)
        {
            energy = EnergyMaxStat;
            return;
        }

        if (energy > EnergyMaxStat)
        {
            energy = EnergyMaxStat;
        }
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (!HasShieldCapacity)
        {
            yield break;
        }

        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }
    }

    public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
    {
        if (!HasShieldCapacity)
        {
            yield break;
        }

        foreach (Gizmo gizmo in base.CompGetWornGizmosExtra())
        {
            yield return gizmo;
        }
    }

    public override void CompTick()
    {
        if (!HasShieldCapacity)
        {
            energy = 0f;
            ticksToReset = -1;
            return;
        }

        base.CompTick();
        if (energy > EnergyMaxStat)
        {
            energy = EnergyMaxStat;
        }
    }

    public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
    {
        if (!HasShieldCapacity)
        {
            absorbed = false;
            return;
        }

        base.PostPreApplyDamage(ref dinfo, out absorbed);
    }

    public override void PostDraw()
    {
        if (!HasShieldCapacity)
        {
            return;
        }

        base.PostDraw();
    }

    public override void CompDrawWornExtras()
    {
        if (!HasShieldCapacity)
        {
            return;
        }

        base.CompDrawWornExtras();
    }
}
