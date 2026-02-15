using RimWorld;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Defs;

// 变形机制 - 待测试
public class GRUpgradeDef: Def
{
    public BodyPartDef targetBodyPart;

    public HediffDef hediffToApply;

    public ResearchProjectDef researchPrerequisite;
}
