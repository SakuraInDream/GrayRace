using Verse;

namespace SD.GrayRace.Comps;

public class CompPropertiesMultiTurretGun : CompProperties
{
    public float floatingWeaponOffsetScale = 1f;
    public float floatingWeaponMinRadius = 0.5f;
    public float floatingWeaponBobAmplitude;
    public int floatingWeaponBobPeriodTicks = 120;

    public CompPropertiesMultiTurretGun()
    {
        compClass = typeof(CompMultiTurretGun);
    }
}
