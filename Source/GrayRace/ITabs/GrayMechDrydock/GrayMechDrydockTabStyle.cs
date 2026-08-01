using System;
using System.Collections.Generic;
using SD.GrayRace.Defs;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

internal static class GrayMechDrydockTabStyle
{
    private static readonly Dictionary<string, Texture2D> CachedTextures = new(StringComparer.Ordinal);

    internal static readonly Vector2 WindowSize = new(1780f, 820f);
    internal static readonly Vector2 QueueWindowSize = new(560f, 620f);

    internal const float Margin = 10f;
    internal const float CloseButtonReserveTop = 4f;
    internal const float CloseButtonReserveRight = 22f;
    internal const float MinTopBarHeight = 68f;
    internal const float MinBottomBarHeight = 96f;
    internal const float LeftPanelWidth = 360f;
    internal const float RightPanelWidth = 360f;
    internal const float PanelGap = 12f;
    internal const float RowHeight = 30f;
    internal const float SlotButtonSize = 50f;
    internal const float SlotGridGap = 3f;
    internal const int SlotGridColumns = 5;
    internal const int MinimumDisplaySlotCount = 10;
    internal const float LibraryCardWidth = 164f;
    internal const float LibraryCardHeight = 78f;
    internal const float LibraryCardGap = 8f;
    internal const float LibraryGroupGap = 14f;
    internal const float LibraryNewDesignWidth = 94f;
    internal const float LibraryNewDesignGap = 14f;
    internal const float SummaryPanelHeight = 200f;
    internal const float SummaryThumbWidth = 64f;
    internal const float SummaryThumbHeight = 48f;
    internal const float SummaryStatusMaxWidth = 136f;
    internal const float MinSectionColumnWidth = SlotButtonSize * SlotGridColumns + SlotGridGap * (SlotGridColumns - 1) + 16f;
    internal const float MinCenterCanvasWidth = MinSectionColumnWidth * 3f + PanelGap * 2f;

    internal static readonly Color BgDark = new(0.04f, 0.07f, 0.08f);
    internal static readonly Color BgPanel = new(0.07f, 0.11f, 0.12f);
    internal static readonly Color BgPanelAlt = new(0.09f, 0.15f, 0.16f);
    internal static readonly Color CardFill = new(0.08f, 0.13f, 0.14f, 0.96f);
    internal static readonly Color CardFillMuted = new(0.06f, 0.09f, 0.1f, 0.96f);
    internal static readonly Color HullColor = new(0.15f, 0.2f, 0.21f, 0.78f);
    internal static readonly Color HullOutline = new(0.33f, 0.76f, 0.74f);
    internal static readonly Color SelectedColor = new(0.96f, 0.73f, 0.29f);
    internal static readonly Color LockedColor = new(0.65f, 0.31f, 0.29f);
    internal static readonly Color ReadyColor = new(0.45f, 0.86f, 0.57f);
    internal static readonly Color MainWeaponColor = new(0.96f, 0.53f, 0.22f);
    internal static readonly Color AuxiliaryColor = new(0.3f, 0.86f, 0.79f);
    internal static readonly Color EngineColor = new(0.3f, 0.76f, 1f);
    internal static readonly Color UtilitySlotColor = new(0.46f, 0.86f, 0.79f);
    internal static readonly Color HeaderLineColor = new(0.31f, 0.78f, 0.76f, 0.44f);
    internal static readonly Color HullPlateColor = new(0.13f, 0.18f, 0.2f, 0.75f);
    internal static readonly Color HullInnerPanelColor = new(0.08f, 0.12f, 0.13f, 0.88f);
    internal static readonly Color HullAccentColor = new(0.72f, 0.97f, 0.9f, 0.78f);
    internal static readonly Color EngineGlowColor = new(0.24f, 0.72f, 1f, 0.42f);
    internal static readonly Color WeaponGlowColor = new(1f, 0.62f, 0.24f, 0.34f);
    internal static readonly Color AuxGlowColor = new(0.27f, 0.86f, 0.75f, 0.3f);
    internal static readonly Color NebulaLeftColor = new(0.16f, 0.52f, 0.56f, 0.12f);
    internal static readonly Color NebulaCenterColor = new(0.22f, 0.76f, 0.64f, 0.1f);
    internal static readonly Color NebulaRightColor = new(0.11f, 0.34f, 0.68f, 0.1f);
    internal static readonly Color SlotInnerColor = new(0.05f, 0.08f, 0.09f);
    internal static Texture2D GetTexture(string texPath)
    {
        if (texPath.NullOrEmpty())
        {
            return null;
        }

        if (!CachedTextures.TryGetValue(texPath, out Texture2D texture))
        {
            texture = ContentFinder<Texture2D>.Get(texPath, false);
            CachedTextures[texPath] = texture;
        }

        return texture;
    }

    internal static Texture2D GetChassisPreview(GRMechChassisDef chassis)
    {
        if (chassis == null)
        {
            return null;
        }

        return GetTexture(chassis.designerPreviewPath) ?? chassis.ProducedRace?.uiIcon;
    }

    internal static Texture2D GetModuleIcon(GRMechModuleDef module)
    {
        if (module == null)
        {
            return null;
        }

        if (module.uiIcon != null && module.uiIcon != BaseContent.BadTex)
        {
            return module.uiIcon;
        }

        if (module.equipmentDef?.uiIcon != null)
        {
            return module.equipmentDef.uiIcon;
        }

        return null;
    }

    internal static Color GetSlotColor(GRMechSlotEntry slot)
    {
        if (slot == null)
        {
            return AuxiliaryColor;
        }

        if (slot.slotCategory == GRMechSlotCategory.CoreSystem)
        {
            switch (slot.coreRole)
            {
                case GRMechCoreComponentRole.PowerCore:
                    return UtilitySlotColor;
                case GRMechCoreComponentRole.Thruster:
                    return EngineColor;
                case GRMechCoreComponentRole.Sensor:
                    return AuxiliaryColor;
                case GRMechCoreComponentRole.CombatComputer:
                    return SelectedColor;
            }
        }

        if (slot.slotDef != null)
        {
            return slot.designerColor;
        }

        switch (slot.slotCategory)
        {
            case GRMechSlotCategory.Weapon:
            case GRMechSlotCategory.Utility:
            case GRMechSlotCategory.Auxiliary:
                return AuxiliaryColor;
            case GRMechSlotCategory.CoreSystem:
                switch (slot.coreRole)
                {
                    case GRMechCoreComponentRole.PowerCore:
                        return UtilitySlotColor;
                    case GRMechCoreComponentRole.Thruster:
                        return EngineColor;
                    case GRMechCoreComponentRole.Sensor:
                        return AuxiliaryColor;
                    case GRMechCoreComponentRole.CombatComputer:
                        return SelectedColor;
                }

                return AuxiliaryColor;
        }

        return AuxiliaryColor;
    }
}
