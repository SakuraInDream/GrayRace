using SD.GrayRace.Defs;
using Verse;

namespace SD.GrayRace.Comps;

// 待测试
public class CompUpgrades : ThingComp
{
    public Pawn Pawn => parent as Pawn;

    public bool CanApplyUpgrade(GRUpgradeDef def, BodyPartRecord part, out string reason)
    {
        reason = "";

        if (def.researchPrerequisite != null && !def.researchPrerequisite.IsFinished)
        {
            reason = $"未解锁科技: {def.researchPrerequisite.LabelCap}";
            return false;
        }

        if (part.def != def.targetBodyPart)
        {
            reason = "部位不匹配";
            return false;
        }

        if (Pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(part))
        {
            reason = "无法在非原生部位上使用";
            return false;
        }

        return true;
    }

    public bool IsUpgradeActive(GRUpgradeDef def, BodyPartRecord part)
    {
        return Pawn.health.hediffSet.hediffs.Any(h => h.def == def.hediffToApply && h.Part == part);
    }

    public void ToggleUpgrade(GRUpgradeDef def, BodyPartRecord part)
    {
        if (IsUpgradeActive(def, part))
        {
            Hediff hediff = Pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.def == def.hediffToApply && h.Part == part);
            if (hediff != null)
            {
                Pawn.health.RemoveHediff(hediff);
            }
        }
        else
        {
            RemoveExistingTransformations(part);

            Pawn.health.AddHediff(def.hediffToApply, part);
        }
    }

    private void RemoveExistingTransformations(BodyPartRecord part)
    {
        var allUpgrades = DefDatabase<GRUpgradeDef>.AllDefsListForReading;
        foreach (var upgrade in allUpgrades)
        {
            // 如果是针对该部位的变形 Hediff
            if (upgrade.targetBodyPart == part.def)
            {
                Hediff existing = Pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.def == upgrade.hediffToApply && h.Part == part);
                if (existing != null)
                {
                    Pawn.health.RemoveHediff(existing);
                }
            }
        }
    }
}
