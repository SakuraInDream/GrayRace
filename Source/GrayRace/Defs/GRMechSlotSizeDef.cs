using System.Collections.Generic;
using Verse;

namespace SD.GrayRace.Defs;

public class GRMechSlotSizeDef : Def
{
    [NoTranslate]
    public string stellarisSizeKey;

    [NoTranslate]
    public string glyph;

    public int uiOrder;

    public List<GRMechSlotComponentType> allowedComponentTypes = new();

    public bool Allows(GRMechSlotComponentType componentType)
    {
        if (componentType == GRMechSlotComponentType.Undefined)
        {
            return false;
        }

        if (allowedComponentTypes == null || allowedComponentTypes.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < allowedComponentTypes.Count; i++)
        {
            if (allowedComponentTypes[i] == componentType)
            {
                return true;
            }
        }

        return false;
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
        {
            yield return error;
        }

        if (stellarisSizeKey.NullOrEmpty())
        {
            yield return defName + " has empty stellarisSizeKey.";
        }

        if (glyph.NullOrEmpty())
        {
            yield return defName + " has empty glyph.";
        }

        if (allowedComponentTypes == null || allowedComponentTypes.Count == 0)
        {
            yield return defName + " must allow at least one componentType.";
            yield break;
        }

        HashSet<GRMechSlotComponentType> seen = new();
        for (int i = 0; i < allowedComponentTypes.Count; i++)
        {
            GRMechSlotComponentType componentType = allowedComponentTypes[i];
            if (componentType == GRMechSlotComponentType.Undefined)
            {
                yield return defName + " contains undefined componentType.";
                continue;
            }

            if (!seen.Add(componentType))
            {
                yield return defName + " contains duplicate componentType " + componentType + ".";
            }
        }
    }
}
