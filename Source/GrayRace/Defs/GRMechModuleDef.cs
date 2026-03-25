using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Defs;

public class GRMechModuleDef : Def
{
    public List<GRMechSlotDef> compatibleSlots = new();
    public GRMechModuleDef upgradeFrom;
    public GRMechModuleDef upgradeTo;
    public GRMechCombatComputerBehavior combatComputerBehavior;
    public GRMechCombatComputerWeaponSelectionMode combatComputerWeaponSelection;
    public GRMechCombatComputerPositioningMode combatComputerPositioning;
    public GRMechCombatComputerCoverPreference combatComputerCoverPreference;
    public float combatComputerPreferredRangeFactor;
    public int combatComputerPowerDraw;
    public float combatComputerFireRateBonus;
    public float combatComputerAccuracyBonus;
    public float combatComputerTrackingBonus;
    public float combatComputerEvasionBonus;
    public float combatComputerWeaponRangeBonus;
    public float combatComputerEngagementRangeBonus;
    public float combatComputerExplosiveDamageBonus;
    public float combatComputerStarbaseDamageBonus;
    public float combatComputerOrbitalBombardmentBonus;
    public List<ThingDefCountClass> costList = new();
    public List<ResearchProjectDef> researchPrerequisites = new();
    [NoTranslate]
    public string iconPath;
    [Unsaved(false)]
    public Texture2D uiIcon = BaseContent.BadTex;
    public ThingDef equipmentDef;
    public ThingDef equipmentStuff;
    public List<StatModifier> statOffsets = new();
    public List<StatModifier> statFactors = new();
    public BodyPartDef anchorBodyPart;
    public int uiOrder;

    public override void PostLoad()
    {
        base.PostLoad();
        if (!iconPath.NullOrEmpty())
        {
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                Texture2D loadedIcon = ContentFinder<Texture2D>.Get(iconPath, false);
                if (loadedIcon != null)
                {
                    uiIcon = loadedIcon;
                }
            });
        }
    }

    public IEnumerable<ResearchProjectDef> EnumerateResearchPrerequisites()
    {
        if (researchPrerequisites == null)
        {
            yield break;
        }

        HashSet<ResearchProjectDef> seen = new();
        for (int i = 0; i < researchPrerequisites.Count; i++)
        {
            ResearchProjectDef project = researchPrerequisites[i];
            if (project != null && seen.Add(project))
            {
                yield return project;
            }
        }
    }

    public bool Matches(GRMechChassisDef chassis, GRMechSlotEntry slot)
    {
        if (slot?.slotDef == null)
        {
            return false;
        }

        if (compatibleSlots == null)
        {
            return false;
        }

        for (int i = 0; i < compatibleSlots.Count; i++)
        {
            if (compatibleSlots[i] == slot.slotDef)
            {
                return true;
            }
        }

        return false;
    }

    public bool FitsSlot(GRMechSlotDef slotDef)
    {
        if (slotDef == null || compatibleSlots == null)
        {
            return false;
        }

        for (int i = 0; i < compatibleSlots.Count; i++)
        {
            if (compatibleSlots[i] == slotDef)
            {
                return true;
            }
        }

        return false;
    }

    public bool UsesSlotCategory(GRMechSlotCategory category)
    {
        if (compatibleSlots == null)
        {
            return false;
        }

        for (int i = 0; i < compatibleSlots.Count; i++)
        {
            if (compatibleSlots[i]?.slotCategory == category)
            {
                return true;
            }
        }

        return false;
    }

    public bool UsesCoreRole(GRMechCoreComponentRole role)
    {
        if (compatibleSlots == null)
        {
            return false;
        }

        for (int i = 0; i < compatibleSlots.Count; i++)
        {
            if (compatibleSlots[i]?.coreRole == role)
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

        if (compatibleSlots == null || compatibleSlots.Count == 0)
        {
            yield return defName + " must define at least one compatible slot.";
            yield break;
        }

        HashSet<GRMechSlotDef> seenSlots = new();
        GRMechSlotCategory? firstCategory = null;
        for (int i = 0; i < compatibleSlots.Count; i++)
        {
            GRMechSlotDef slot = compatibleSlots[i];
            if (slot == null)
            {
                yield return defName + " has null compatible slot entry.";
                continue;
            }

            if (!seenSlots.Add(slot))
            {
                yield return defName + " contains duplicate compatible slot " + slot.defName + ".";
            }

            if (firstCategory == null)
            {
                firstCategory = slot.slotCategory;
            }
            else if (firstCategory != slot.slotCategory)
            {
                yield return defName + " mixes incompatible slot categories in compatibleSlots.";
            }
        }

        if (equipmentStuff != null && equipmentDef == null)
        {
            yield return defName + " has equipmentStuff without equipmentDef.";
        }

        if (equipmentStuff != null && !equipmentStuff.IsStuff)
        {
            yield return defName + " equipmentStuff is not a stuff ThingDef.";
        }

        if (upgradeFrom == this)
        {
            yield return defName + " cannot upgradeFrom itself.";
        }

        if (upgradeTo == this)
        {
            yield return defName + " cannot upgradeTo itself.";
        }

        if (upgradeFrom != null)
        {
            if (!CompatibleSlotsEqual(upgradeFrom))
            {
                yield return defName + " upgradeFrom has mismatched compatibleSlots.";
            }
        }

        if (upgradeTo != null)
        {
            if (!CompatibleSlotsEqual(upgradeTo))
            {
                yield return defName + " upgradeTo has mismatched compatibleSlots.";
            }
        }

        bool isCombatComputer = UsesCoreRole(GRMechCoreComponentRole.CombatComputer);
        if (isCombatComputer && combatComputerBehavior == GRMechCombatComputerBehavior.Undefined)
        {
            yield return defName + " is a combat computer module but has undefined combatComputerBehavior.";
        }
        else if (!isCombatComputer && combatComputerBehavior != GRMechCombatComputerBehavior.Undefined)
        {
            yield return defName + " defines combatComputerBehavior outside the combat-computer slot.";
        }

        if (isCombatComputer)
        {
            if (combatComputerWeaponSelection == GRMechCombatComputerWeaponSelectionMode.Undefined)
            {
                yield return defName + " is a combat computer module but has undefined combatComputerWeaponSelection.";
            }

            if (combatComputerPositioning == GRMechCombatComputerPositioningMode.Undefined)
            {
                yield return defName + " is a combat computer module but has undefined combatComputerPositioning.";
            }

            if (combatComputerCoverPreference == GRMechCombatComputerCoverPreference.Undefined)
            {
                yield return defName + " is a combat computer module but has undefined combatComputerCoverPreference.";
            }

            if (combatComputerPositioning != GRMechCombatComputerPositioningMode.Vanilla && combatComputerPreferredRangeFactor <= 0f)
            {
                yield return defName + " uses non-vanilla combatComputerPositioning but has invalid combatComputerPreferredRangeFactor.";
            }
        }
        else
        {
            if (combatComputerWeaponSelection != GRMechCombatComputerWeaponSelectionMode.Undefined)
            {
                yield return defName + " defines combatComputerWeaponSelection outside the combat-computer slot.";
            }

            if (combatComputerPositioning != GRMechCombatComputerPositioningMode.Undefined)
            {
                yield return defName + " defines combatComputerPositioning outside the combat-computer slot.";
            }

            if (combatComputerCoverPreference != GRMechCombatComputerCoverPreference.Undefined)
            {
                yield return defName + " defines combatComputerCoverPreference outside the combat-computer slot.";
            }

            if (combatComputerPreferredRangeFactor != 0f)
            {
                yield return defName + " defines combatComputerPreferredRangeFactor outside the combat-computer slot.";
            }
        }
    }

    private bool CompatibleSlotsEqual(GRMechModuleDef other)
    {
        if (other == null || other.compatibleSlots == null)
        {
            return false;
        }

        int thisCount = compatibleSlots?.Count ?? 0;
        int otherCount = other.compatibleSlots.Count;
        if (thisCount != otherCount)
        {
            return false;
        }

        if (thisCount == 0)
        {
            return true;
        }

        HashSet<GRMechSlotDef> otherSlots = new(other.compatibleSlots);
        for (int i = 0; i < thisCount; i++)
        {
            if (!otherSlots.Contains(compatibleSlots[i]))
            {
                return false;
            }
        }

        return true;
    }
}
