using System.Collections.Generic;
using Verse;

namespace SD.GrayRace.Comps.PropertiesSettings;

public class OverclockSetting
{
    public class OverclockMap
    {
        public BodyPartTagDef tag;
        public BodyPartDef part;
        public HediffDef hediff;
    }

    public List<OverclockMap> overclockMaps = new List<OverclockMap>();
}
