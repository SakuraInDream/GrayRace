using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SD.GrayRace.Defs;

/// <summary>
/// 升级类型枚举
/// </summary>
public enum UpgradeType
{
    Transformation,  // 变形 - 免费解锁科技后使用
    Plugin          // 插件 - 需要材料制作
}

public class GRUpgradeDef : Def
{
    /// <summary>
    /// 精确目标部位（优先级最高）
    /// </summary>
    public List<BodyPartDef> targetBodyParts = new List<BodyPartDef>();

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
    public ResearchProjectDef researchPrerequisite;

    /// <summary>
    /// 升级类型：变形（免费）或插件（需要制作）
    /// </summary>
    public UpgradeType upgradeType = UpgradeType.Transformation;

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
    /// 可共存的升级类型。仅当双方都声明互相可共存时，才允许同部位共存。
    /// </summary>
    public List<UpgradeType> coexistWithUpgradeTypes = new List<UpgradeType>();

    /// <summary>
    /// 列表排序权重，越小越靠前
    /// </summary>
    public int uiOrder = 0;
}
