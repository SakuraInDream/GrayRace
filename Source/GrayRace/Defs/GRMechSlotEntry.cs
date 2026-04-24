using UnityEngine;
using Verse;

namespace SD.GrayRace.Defs;

public class GRMechSlotEntry
{
    public string key;
    public string label;
    public GRMechSlotDef slotDef;
    public BodyPartDef anchorBodyPart;
    public Vector3 hardpointOffset = Vector3.zero;
    public int uiOrder;

    public GRMechSlotComponentType componentType => slotDef?.componentType ?? GRMechSlotComponentType.Undefined;

    public GRMechCoreComponentRole coreRole => slotDef?.coreRole ?? GRMechCoreComponentRole.Undefined;

    public GRMechSlotCategory slotCategory => slotDef?.slotCategory ?? GRMechSlotCategory.Undefined;

    public GRMechSlotSizeDef slotSize => slotDef?.slotSize;

    public bool HasSizedSlot => slotDef?.HasSizedSlot ?? false;

    public bool isFixed => slotDef?.isFixed ?? false;

    public string glyph => slotDef?.DisplayGlyph ?? string.Empty;

    public Color designerColor => slotDef?.designerColor ?? Color.white;

    public bool HasHardpointOffset => hardpointOffset.sqrMagnitude > 0.0001f;
}
