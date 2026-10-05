using System.Collections.Generic;
using Verse;

namespace SD.GrayRace.ITabs;

public class Command_GrayMechDesigner : Command_Action
{
    public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
    {
        get
        {
            yield return new FloatMenuOption("重置窗口尺寸与位置", Window_GrayMechDrydockDesigner.ResetGeometry);
        }
    }
}
