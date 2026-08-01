using UnityEngine;
using Verse;

namespace SD.GrayRace.Defs;

public enum GRMechWeaponMountMode : byte
{
    Hardpoint,
    PrimaryEquipment
}

public class GRMechSlotEntry
{
    public string key;
    public string label;
    public GRMechSlotDef slotDef;
    public GRMechWeaponMountMode weaponMountMode;
    public BodyPartDef anchorBodyPart;
    public Vector2? hardpointAnchor;
    public int uiOrder;

    public GRMechSlotComponentType componentType => slotDef?.componentType ?? GRMechSlotComponentType.Undefined;

    public GRMechCoreComponentRole coreRole => slotDef?.coreRole ?? GRMechCoreComponentRole.Undefined;

    public GRMechSlotCategory slotCategory => slotDef?.slotCategory ?? GRMechSlotCategory.Undefined;

    public GRMechSlotSizeDef slotSize => slotDef?.slotSize;

    public bool HasSizedSlot => slotDef?.HasSizedSlot ?? false;

    public bool isFixed => slotDef?.isFixed ?? false;

    public string glyph => slotDef?.DisplayGlyph ?? string.Empty;

    public Color designerColor => slotDef?.designerColor ?? Color.white;

    public bool TryGetHardpointAnchorConfigError(string owner, out string error)
    {
        bool isHardpointWeapon = componentType == GRMechSlotComponentType.Weapon
            && weaponMountMode == GRMechWeaponMountMode.Hardpoint;
        if (isHardpointWeapon && !hardpointAnchor.HasValue)
        {
            error = owner + " slot " + key + " is a Hardpoint weapon but has no hardpointAnchor.";
            return true;
        }

        if (!isHardpointWeapon && hardpointAnchor.HasValue)
        {
            error = owner + " slot " + key + " must not define hardpointAnchor unless it is a Hardpoint weapon.";
            return true;
        }

        error = null;
        return false;
    }
}
