using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SD.GrayRace.DefModExtensions;

//   <modExtensions>
//     <li Class="SD.GrayRace.DefModExtensions.PawnKindPassionExtension">
//       <skills>
//         <li>
//           <skill>Shooting</skill>
//           <passion>Major</passion>
//         </li>
//       </skills>
//     </li>
//   </modExtensions>
public class PawnKindPassionExtension : DefModExtension
{
    public List<PawnKindPassionEntry> skills;

    public override IEnumerable<string> ConfigErrors()
    {
        if (skills.NullOrEmpty())
        {
            yield return "PawnKindPassionExtension requires at least one skill entry.";
            yield break;
        }

        var seen = new HashSet<SkillDef>();
        for (int i = 0; i < skills.Count; i++)
        {
            PawnKindPassionEntry entry = skills[i];
            if (entry == null)
            {
                yield return $"skills[{i}] is null.";
                continue;
            }

            if (entry.skill == null)
            {
                yield return $"skills[{i}].skill is null.";
                continue;
            }

            if (!seen.Add(entry.skill))
            {
                yield return $"skills[{i}] lists skill {entry.skill.defName} more than once.";
            }

            if (!entry.passion.HasValue)
            {
                yield return $"skills[{i}] ({entry.skill.defName}) must declare a passion (None/Minor/Major).";
            }
        }
    }
}

public class PawnKindPassionEntry
{
    public SkillDef skill;

    // None / Minor / Major，必填
    public Passion? passion;
}
