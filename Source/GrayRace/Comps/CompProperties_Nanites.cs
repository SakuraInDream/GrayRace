using System.Collections.Generic;
using Verse;

namespace SD.GrayRace.Comps
{
    public class CompProperties_Nanites: CompProperties
    {
        public float maxResource;

        public float regenPerSecond = 0.01f;

        [MustTranslate]
        public string resourceLabel;

        public List<float> resourceGizmoThresholds;

        public CompProperties_Nanites()
        {
            // compClass = typeof(CompResource_Nanites);
            compClass = typeof(CompResource_Nanites);
        }
    }
}
