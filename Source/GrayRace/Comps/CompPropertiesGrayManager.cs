using System.Collections.Generic;
using SD.GrayRace.Comps.PropertiesSettings;
using Verse;

namespace SD.GrayRace.Comps;

public class CompPropertiesGrayManager: CompProperties
{
    public NanitesSetting nanitesSetting = new NanitesSetting();
    public OverclockSetting overclockSetting = new OverclockSetting();

    public CompPropertiesGrayManager()
    {
        compClass = typeof(CompGrayManager);
    }
}
