using System.Collections.Generic;
using SD.GrayRace.ThingClasses;
using Verse;

namespace SD.GrayRace.ITabs;

public class Command_GrayMechDesigner : Command_Action
{
    /// <summary>发起命令的干船坞；右键“重置窗口尺寸与位置”只作用于它。</summary>
    public Building_GR_Drydock Dock;

    public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
    {
        get
        {
            yield return new FloatMenuOption("重置窗口尺寸与位置", () => Window_GrayMechDrydockDesigner.ResetGeometry(Dock));
        }
    }
}
