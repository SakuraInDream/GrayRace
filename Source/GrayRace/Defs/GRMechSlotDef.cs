using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Defs;

public class GRMechSlotDef : Def
{
    public GRMechSlotComponentType componentType;
    public GRMechComponentSetDef requiredComponentSet;
    public GRMechSlotSizeDef slotSize;
    public bool isFixed;
    public Color designerColor = Color.white;
    [NoTranslate]
    public string glyph;

    public GRMechCoreComponentRole coreRole => requiredComponentSet?.coreRole ?? GRMechCoreComponentRole.Undefined;

    public GRMechSlotCategory slotCategory => requiredComponentSet != null
        ? GRMechSlotCategory.CoreSystem
        : componentType switch
        {
            GRMechSlotComponentType.Weapon => GRMechSlotCategory.Weapon,
            GRMechSlotComponentType.StrikeCraft => GRMechSlotCategory.Weapon,
            GRMechSlotComponentType.Utility => GRMechSlotCategory.Utility,
            GRMechSlotComponentType.Auxiliary => GRMechSlotCategory.Auxiliary,
            _ => GRMechSlotCategory.Undefined
        };

    public bool HasSizedSlot => slotSize != null;

    public string DisplayGlyph => glyph ?? requiredComponentSet?.glyph ?? slotSize?.glyph ?? string.Empty;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
        {
            yield return error;
        }

        bool usesCoreSet = requiredComponentSet != null;
        bool usesComponentType = componentType != GRMechSlotComponentType.Undefined;
        if (!usesCoreSet && !usesComponentType)
        {
            yield return defName + " must define componentType or requiredComponentSet.";
            yield break;
        }

        if (usesCoreSet && usesComponentType)
        {
            yield return defName + " cannot define both componentType and requiredComponentSet.";
        }

        if (usesCoreSet)
        {
            if (requiredComponentSet.coreRole == GRMechCoreComponentRole.Undefined)
            {
                yield return defName + " uses requiredComponentSet without coreRole.";
            }

            if (slotSize != null)
            {
                yield return defName + " core slot must leave slotSize empty.";
            }
        }
        else
        {
            if (slotSize == null)
            {
                yield return defName + " has null slotSize.";
            }
            else if (!slotSize.Allows(componentType))
            {
                yield return defName + " uses slotSize " + slotSize.defName + " which does not allow componentType " + componentType + ".";
            }
        }
    }
}
