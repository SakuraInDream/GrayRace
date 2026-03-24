using Verse;

namespace SD.GrayRace.Comps;

public class CompPropertiesGrayMechShield : CompProperties
{
    public int startingTicksToReset = 3200;
    public float minDrawSize = 1.2f;
    public float maxDrawSize = 1.55f;
    public float energyLossPerDamage = 0.033f;
    public float energyOnReset = 0.2f;

    public CompPropertiesGrayMechShield()
    {
        compClass = typeof(CompGrayMechShield);
    }
}
