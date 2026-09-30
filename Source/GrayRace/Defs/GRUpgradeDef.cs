using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Defs;

[StaticConstructorOnStartup]
public class GRUpgradeDef : Def
{
    /// <summary>
    /// 精确目标部位（优先级最高）
    /// </summary>
    public BodyPartDef targetBodyPart;

    /// <summary>
    /// 图标
    /// </summary>
    [NoTranslate]
    public string iconPath;

    [Unsaved(false)]
    public Texture2D uiIcon = BaseContent.BadTex;

    /// <summary>
    /// 目标部位标签（例如 SightSource、BloodPumpingSource）
    /// </summary>
    public List<BodyPartTagDef> targetBodyPartTags = new List<BodyPartTagDef>();

    /// <summary>
    /// 目标部位分组（例如 Hands、Legs）
    /// </summary>
    public List<BodyPartGroupDef> targetBodyPartGroups = new List<BodyPartGroupDef>();

    /// <summary>
    /// 排除的精确部位
    /// </summary>
    public List<BodyPartDef> excludedBodyParts = new List<BodyPartDef>();

    /// <summary>
    /// 排除的部位标签
    /// </summary>
    public List<BodyPartTagDef> excludedBodyPartTags = new List<BodyPartTagDef>();

    /// <summary>
    /// 排除的部位分组
    /// </summary>
    public List<BodyPartGroupDef> excludedBodyPartGroups = new List<BodyPartGroupDef>();

    public HediffDef hediffToApply;
    public List<ResearchProjectDef> researchPrerequisites = new List<ResearchProjectDef>();

    /// <summary>
    /// 插件类型需要的材料（仅插件类型使用）
    /// </summary>
    public List<ThingDefCountClass> requiredMaterials = new List<ThingDefCountClass>();

    /// <summary>
    /// 制作所需工作量（仅插件类型使用）
    /// </summary>
    public float workAmount = 1000f;

    /// <summary>
    /// 可选安装建筑（仅插件类型使用，为空则原地安装）
    /// </summary>
    public ThingDef requiredInstallationBuilding;

    /// <summary>
    /// 制作所需技能（仅插件类型使用）
    /// </summary>
    public SkillDef requiredSkill;

    /// <summary>
    /// 所需最低技能等级（仅插件类型使用）
    /// </summary>
    public int minSkillLevel = 5;

    /// <summary>
    /// 是否允许安装到非原生部位（AddedPart）
    /// </summary>
    public bool allowOnAddedParts = false;

    /// <summary>
    /// 列表排序权重，越小越靠前
    /// </summary>
    public int uiOrder = 0;

    /// <summary>
    /// 互斥列表，仿原版 GeneDef 实现，当两个升级插件/变形存在
    /// </summary>
    public List<string> exclusionTags = new List<string>();

    public bool IsPlugin => requiredMaterials != null && requiredMaterials.Count > 0;

    public bool IsTransformation => !IsPlugin;

    public bool ConflictsWith(GRUpgradeDef other)
    {
        if (this == other) return true;

        if (exclusionTags == null || other.exclusionTags == null)
        {
            return false;
        }

        for (int i = 0; i < exclusionTags.Count; i++)
        {
            if (other.exclusionTags.Contains(exclusionTags[i]))
            {
                return true;
            }
        }

        return false;
    }

    public override void PostLoad()
    {
        base.PostLoad();
        if (!iconPath.NullOrEmpty())
        {
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                uiIcon = ContentFinder<Texture2D>.Get(iconPath);
            });
        }
    }

    public IEnumerable<ResearchProjectDef> EnumerateResearchPrerequisites()
    {
        if (researchPrerequisites == null || researchPrerequisites.Count == 0)
        {
            yield break;
        }

        HashSet<ResearchProjectDef> seen = new HashSet<ResearchProjectDef>();
        foreach (ResearchProjectDef project in researchPrerequisites)
        {
            if (project == null) continue;
            if (seen.Add(project))
            {
                yield return project;
            }
        }
    }

    public bool AreResearchPrerequisitesMet(out ResearchProjectDef firstMissing)
    {
        foreach (ResearchProjectDef project in EnumerateResearchPrerequisites())
        {
            if (!project.IsFinished)
            {
                firstMissing = project;
                return false;
            }
        }

        firstMissing = null;
        return true;
    }
}
