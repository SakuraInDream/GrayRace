using RimWorld;
using SD.GrayRace.ThingClasses;
using UnityEngine;
using Verse;

namespace SD.GrayRace.PlaceWorkers
{
    public class PlaceWorker_IncubatorTop: PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            GhostUtility.GhostGraphicFor(GraphicDatabase.Get<Graphic_Single>(
                def.building.mechGestatorTopGraphic.texPath,
                ShaderDatabase.Cutout,
                def.building.mechGestatorTopGraphic.drawSize,
                Color.white), def, ghostCol).
                DrawFromDef(GenThing.TrueCenter(center, rot, def.Size, AltitudeLayer.BuildingOnTop.AltitudeFor()) + def.graphicData.drawOffset, rot, def);
        }
    }
}
