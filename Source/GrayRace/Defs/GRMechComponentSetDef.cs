using System.Collections.Generic;
using Verse;

namespace SD.GrayRace.Defs;

public class GRMechComponentSetDef : Def
{
    public GRMechCoreComponentRole coreRole;
    [NoTranslate]
    public string glyph;
    public int uiOrder;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
        {
            yield return error;
        }

        if (coreRole == GRMechCoreComponentRole.Undefined)
        {
            yield return defName + " has undefined coreRole.";
        }

        if (glyph.NullOrEmpty())
        {
            yield return defName + " has empty glyph.";
        }
    }
}
