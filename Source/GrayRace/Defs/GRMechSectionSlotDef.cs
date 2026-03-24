using System.Collections.Generic;
using Verse;

namespace SD.GrayRace.Defs;

public class GRMechSectionSlotDef : Def
{
    [NoTranslate]
    public string slotId;
    public int uiOrder;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
        {
            yield return error;
        }

        if (slotId.NullOrEmpty())
        {
            yield return defName + " has empty slotId.";
        }
    }
}
