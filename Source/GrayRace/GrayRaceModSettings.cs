using Verse;

namespace SD.GrayRace
{
    public class GrayRaceModSettings: ModSettings
    {
        /// <summary>
        /// 舰船配装设计器窗口
        /// </summary>
        public float designerWindowWidth;

        public float designerWindowHeight;

        public float designerWindowX;

        public float designerWindowY;

        public void ClearDesignerWindowGeometry()
        {
            designerWindowWidth = 0f;
            designerWindowHeight = 0f;
            designerWindowX = 0f;
            designerWindowY = 0f;
        }

        public void SetDesignerWindowGeometry(float x, float y, float width, float height)
        {
            designerWindowX = x;
            designerWindowY = y;
            designerWindowWidth = width;
            designerWindowHeight = height;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref designerWindowWidth, "designerWindowWidth", 0f);
            Scribe_Values.Look(ref designerWindowHeight, "designerWindowHeight", 0f);
            Scribe_Values.Look(ref designerWindowX, "designerWindowX", 0f);
            Scribe_Values.Look(ref designerWindowY, "designerWindowY", 0f);
        }
    }
}
