using System.Collections.Generic;
using SD.GrayRace.Defs;
using Verse;

namespace SD.GrayRace.Utilities;

public static class UpgradeTargetMatcher
{
    public static bool HasAnyTargetRule(GRUpgradeDef def)
    {
        if (def == null) return false;

        return HasItems(def.targetBodyParts)
               || HasItems(def.targetBodyPartTags)
               || HasItems(def.targetBodyPartGroups);
    }

    public static bool Matches(GRUpgradeDef def, BodyPartRecord part)
    {
        if (def == null || part?.def == null) return false;
        if (!HasAnyTargetRule(def)) return false;

        bool matched = MatchesIncludeRule(def, part);
        if (!matched) return false;

        if (HasItems(def.excludedBodyParts) && def.excludedBodyParts.Contains(part.def)) return false;
        if (HasItems(def.excludedBodyPartTags) && HasAnyTag(part, def.excludedBodyPartTags)) return false;
        if (HasItems(def.excludedBodyPartGroups) && HasAnyGroup(part, def.excludedBodyPartGroups)) return false;

        return true;
    }

    private static bool MatchesIncludeRule(GRUpgradeDef def, BodyPartRecord part)
    {
        if (HasItems(def.targetBodyParts) && def.targetBodyParts.Contains(part.def)) return true;
        if (HasItems(def.targetBodyPartTags) && HasAnyTag(part, def.targetBodyPartTags)) return true;
        if (HasItems(def.targetBodyPartGroups) && HasAnyGroup(part, def.targetBodyPartGroups)) return true;

        return false;
    }

    private static bool HasAnyTag(BodyPartRecord part, List<BodyPartTagDef> tags)
    {
        if (part?.def?.tags == null || tags == null || tags.Count == 0) return false;
        return tags.Any(tag => tag != null && part.def.tags.Contains(tag));
    }

    private static bool HasAnyGroup(BodyPartRecord part, List<BodyPartGroupDef> groups)
    {
        if (part?.groups == null || groups == null || groups.Count == 0) return false;
        return groups.Any(group => group != null && part.groups.Contains(group));
    }

    private static bool HasItems<T>(List<T> list)
    {
        return list != null && list.Count > 0;
    }
}
