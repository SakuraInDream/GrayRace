using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Abilities;

public class CompProperties_AbilityNanitesCost : AbilityCompProperties
{
    public float nanitesCost;

    public CompProperties_AbilityNanitesCost()
    {
        compClass = typeof(AbilityComp_NanitesCost);
    }

    public override IEnumerable<string> ExtraStatSummary()
    {
        if (nanitesCost > float.Epsilon)
        {
            yield return "GR_AbilityNanitesCost".Translate() + ": " + Mathf.RoundToInt(nanitesCost).ToString();
        }
    }

    public override IEnumerable<string> ConfigErrors(AbilityDef parentDef)
    {
        foreach (string error in base.ConfigErrors(parentDef))
        {
            yield return error;
        }

        if (nanitesCost < 0f)
        {
            yield return "nanitesCost cannot be negative";
        }

        if (!typeof(Ability_NaniteCost).IsAssignableFrom(parentDef.abilityClass))
        {
            yield return "CompProperties_AbilityNanitesCost requires abilityClass assignable to SD.GrayRace.Abilities.Ability_NaniteCost";
        }
    }
}
