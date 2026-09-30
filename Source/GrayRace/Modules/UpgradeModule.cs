using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.Utilities;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SD.GrayRace.Modules;

public class UpgradeModule : GrayModuleBase
{
    public sealed class MaterialPickupTask
    {
        public Thing thing;
        public ThingDef thingDef;
        public int count;
    }

    private string _pendingPluginUpgradeDefName;
    private int _pendingPluginPartIndex = -1;

    private static Dictionary<HediffDef, GRUpgradeDef> _hediffToUpgrade;

    public bool HasPendingPluginInstall => !_pendingPluginUpgradeDefName.NullOrEmpty() && _pendingPluginPartIndex >= 0;

    private static void EnsureCache()
    {
        if (_hediffToUpgrade?.Count > 0) return;

        _hediffToUpgrade = new Dictionary<HediffDef, GRUpgradeDef>();

        foreach (GRUpgradeDef def in DefDatabase<GRUpgradeDef>.AllDefsListForReading)
        {
            if (def?.hediffToApply == null || _hediffToUpgrade.ContainsKey(def.hediffToApply)) continue;
            _hediffToUpgrade.Add(def.hediffToApply, def);
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref _pendingPluginUpgradeDefName, "pendingPluginUpgradeDefName");
        Scribe_Values.Look(ref _pendingPluginPartIndex, "pendingPluginPartIndex", -1);
    }

    public List<GRUpgradeDef> GetAvailableUpgradesForPart(BodyPartRecord part)
    {
        if (part == null) return new List<GRUpgradeDef>();

        return DefDatabase<GRUpgradeDef>.AllDefsListForReading
            .Where(def => IsValidUpgradeDef(def)
                          && UpgradeTargetMatcher.Matches(def, part)
                          && (IsUpgradeVisible(def) || IsUpgradeActive(def, part)))
            .OrderBy(def => def.uiOrder)
            .ThenBy(def => def.IsPlugin)
            .ThenBy(def => def.label)
            .ToList();
    }

    public bool HasUpgradeDefinitionForPart(BodyPartRecord part)
    {
        if (part == null) return false;

        return DefDatabase<GRUpgradeDef>.AllDefsListForReading
            .Any(def => IsValidUpgradeDef(def)
                        && UpgradeTargetMatcher.Matches(def, part)
                        && IsUpgradeVisible(def));
    }

    public bool HasAnyActiveUpgrade(BodyPartRecord part)
    {
        if (pawn?.health?.hediffSet == null || part == null) return false;

        foreach (GRUpgradeDef def in DefDatabase<GRUpgradeDef>.AllDefsListForReading)
        {
            if (!IsValidUpgradeDef(def) || !UpgradeTargetMatcher.Matches(def, part)) continue;
            if (IsUpgradeActive(def, part)) return true;
        }

        return false;
    }

    public bool IsUpgradeActive(GRUpgradeDef def, BodyPartRecord part)
    {
        return TryGetActiveUpgradeHediff(def, part, out _);
    }

    public bool CanApplyUpgrade(
        GRUpgradeDef def,
        BodyPartRecord part,
        out string reason,
        bool checkMaterials = true,
        bool checkPendingInstall = true
    )
    {
        reason = string.Empty;

        if (pawn == null || part == null || def == null)
        {
            reason = "无效目标";
            return false;
        }

        if (!IsValidUpgradeDef(def))
        {
            reason = "升级配置无效";
            return false;
        }

        if (!UpgradeTargetMatcher.Matches(def, part))
        {
            reason = "部位不匹配";
            return false;
        }

        if (!def.AreResearchPrerequisitesMet(out ResearchProjectDef missingResearch))
        {
            reason = $"未解锁科技: {missingResearch.LabelCap}";
            return false;
        }

        if (!def.allowOnAddedParts
            && pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(part)
            && !HasAnyActiveUpgrade(part))
        {
            reason = "无法在非原生部位上使用";
            return false;
        }

        if (def.IsPlugin)
        {
            if (checkPendingInstall && HasPendingPluginInstall)
            {
                reason = "已有待执行的插件安装任务";
                return false;
            }

            if (!CheckSkillRequirement(def, out reason)) return false;
            if (checkMaterials && !HasRequiredMaterials(def, out reason)) return false;
        }

        return true;
    }

    public bool TryToggleUpgrade(GRUpgradeDef def, BodyPartRecord part, out string feedback, out MessageTypeDef messageType)
    {
        feedback = string.Empty;
        messageType = MessageTypeDefOf.NeutralEvent;

        if (part == null || pawn == null || def == null)
        {
            feedback = "无效目标";
            messageType = MessageTypeDefOf.RejectInput;
            return false;
        }

        if (TryGetActiveUpgradeHediff(def, part, out Hediff activeHediff))
        {
            RemoveUpgradeInstance(def, activeHediff.Part, refundPluginMaterials: def.IsPlugin);

            feedback = $"已停用: {def.LabelCap}";
            messageType = MessageTypeDefOf.NeutralEvent;
            return true;
        }

        if (!CanApplyUpgrade(def, part, out string reason))
        {
            feedback = reason;
            messageType = MessageTypeDefOf.RejectInput;
            return false;
        }

        if (def.IsPlugin)
        {
            if (!TryStartPluginInstallJob(def, part, out reason))
            {
                feedback = reason;
                messageType = MessageTypeDefOf.RejectInput;
                return false;
            }

            feedback = $"已开始安装: {def.LabelCap}";
            messageType = MessageTypeDefOf.PositiveEvent;
            return true;
        }

        RemoveConflictingUpgrades(part, def, refundPluginMaterials: true);
        pawn.health.AddHediff(def.hediffToApply, part);

        feedback = $"已启用: {def.LabelCap}";
        messageType = MessageTypeDefOf.PositiveEvent;
        return true;
    }

    public bool TryGetPendingPluginInstall(out GRUpgradeDef def, out BodyPartRecord part)
    {
        def = null;
        part = null;

        if (!HasPendingPluginInstall || pawn?.RaceProps?.body?.AllParts == null) return false;

        def = DefDatabase<GRUpgradeDef>.GetNamedSilentFail(_pendingPluginUpgradeDefName);
        if (def == null)
        {
            ClearPendingPluginInstall();
            return false;
        }

        List<BodyPartRecord> allParts = pawn.RaceProps.body.AllParts;
        if (_pendingPluginPartIndex < 0 || _pendingPluginPartIndex >= allParts.Count)
        {
            ClearPendingPluginInstall();
            return false;
        }

        part = allParts[_pendingPluginPartIndex];
        return true;
    }

    public void ClearPendingPluginInstall()
    {
        _pendingPluginUpgradeDefName = null;
        _pendingPluginPartIndex = -1;
    }

    public bool TryBuildMaterialCollectionPlan(GRUpgradeDef def, out List<MaterialPickupTask> plan, out string reason)
    {
        plan = new List<MaterialPickupTask>();
        reason = string.Empty;

        if (pawn?.Map == null)
        {
            reason = "不在地图上，无法收集材料";
            return false;
        }

        if (def?.requiredMaterials == null || def.requiredMaterials.Count == 0) return true;

        foreach (ThingDefCountClass material in def.requiredMaterials)
        {
            if (material?.thingDef == null || material.count <= 0) continue;

            int remaining = material.count;
            List<Thing> candidates = pawn.Map.listerThings.ThingsOfDef(material.thingDef)
                .Where(IsGatherableThing)
                .OrderBy(t => t.PositionHeld.DistanceToSquared(pawn.PositionHeld))
                .ToList();

            foreach (Thing candidate in candidates)
            {
                if (remaining <= 0) break;

                int take = Math.Min(remaining, candidate.stackCount);
                if (take <= 0) continue;

                plan.Add(new MaterialPickupTask
                {
                    thing = candidate,
                    thingDef = material.thingDef,
                    count = take
                });

                remaining -= take;
            }

            if (remaining > 0)
            {
                reason = $"材料不足: {material.thingDef.LabelCap}";
                plan.Clear();
                return false;
            }
        }

        return true;
    }

    public Thing TryFindInstallationBuilding(GRUpgradeDef def)
    {
        if (def?.requiredInstallationBuilding == null || pawn?.Map == null) return null;

        return GenClosest.ClosestThingReachable(
            pawn.Position,
            pawn.Map,
            ThingRequest.ForDef(def.requiredInstallationBuilding),
            PathEndMode.InteractionCell,
            TraverseParms.For(pawn),
            validator: thing => thing != null && !thing.IsForbidden(pawn) && pawn.CanReserve(thing)
        );
    }

    public int GetInstallDurationTicks(GRUpgradeDef def)
    {
        int ticks = Mathf.RoundToInt(def?.workAmount ?? 0f);
        return Math.Max(60, ticks);
    }

    public bool TryFinalizePendingPluginInstall(List<MaterialPickupTask> materialPlan, out string reason)
    {
        reason = string.Empty;

        if (!TryGetPendingPluginInstall(out GRUpgradeDef def, out BodyPartRecord part))
        {
            reason = "没有待安装插件任务";
            return false;
        }

        if (!CanApplyUpgrade(def, part, out reason, checkMaterials: false, checkPendingInstall: false)) return false;

        if (!TryConsumeMaterialPlan(materialPlan, out reason)) return false;

        RemoveConflictingUpgrades(part, def, refundPluginMaterials: true);
        pawn.health.AddHediff(def.hediffToApply, part);
        ClearPendingPluginInstall();
        return true;
    }

    public string BuildPluginRequirementSummary(GRUpgradeDef def)
    {
        List<string> segments = new List<string>();

        if (def?.requiredSkill != null)
        {
            segments.Add($"技能 {def.requiredSkill.LabelCap} {def.minSkillLevel}+");
        }

        if (def?.requiredMaterials != null && def.requiredMaterials.Count > 0)
        {
            string materialText = string.Join("、", def.requiredMaterials
                .Where(m => m?.thingDef != null && m.count > 0)
                .Select(m => $"{m.thingDef.LabelCap}x{m.count}"));
            if (!materialText.NullOrEmpty())
            {
                segments.Add(materialText);
            }
        }

        return segments.Count > 0 ? $"需求: {string.Join(" | ", segments)}" : "需求: 无";
    }

    public string GetReplacementHint(GRUpgradeDef def, BodyPartRecord part)
    {
        if (def == null || part == null || def.exclusionTags == null || def.exclusionTags.Count == 0) return string.Empty;

        if (pawn?.health?.hediffSet?.hediffs == null) return string.Empty;

        EnsureCache();

        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff h = hediffs[i];

            if (h?.def == null || h.Part == null) continue;

            if (!IsPartInSubtree(part, h.Part) && !IsPartInSubtree(h.Part, part)) continue;

            if (!_hediffToUpgrade.TryGetValue(h.def, out GRUpgradeDef owner)) continue;

            if (owner == null || owner == def || !def.ConflictsWith(owner)) continue;

            return $"替换 {owner.LabelCap}";
        }

        return string.Empty;
    }

    private bool TryStartPluginInstallJob(GRUpgradeDef def, BodyPartRecord part, out string reason)
    {
        reason = string.Empty;

        if (pawn?.Map == null)
        {
            reason = "不在地图上，无法安装插件";
            return false;
        }

        if (pawn.jobs == null)
        {
            reason = "该单位无法执行任务";
            return false;
        }

        if (pawn.Drafted)
        {
            reason = "单位处于征召状态";
            return false;
        }

        if (!TryBuildMaterialCollectionPlan(def, out _, out reason)) return false;

        List<BodyPartRecord> allParts = pawn.RaceProps?.body?.AllParts;
        int partIndex = allParts?.IndexOf(part) ?? -1;
        if (partIndex < 0)
        {
            reason = "无法定位目标部位";
            return false;
        }

        _pendingPluginUpgradeDefName = def.defName;
        _pendingPluginPartIndex = partIndex;

        Job job = JobMaker.MakeJob(GrayRaceDefOf.GR_InstallPluginUpgrade, pawn);
        job.playerForced = true;

        if (!pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc))
        {
            ClearPendingPluginInstall();
            reason = "无法创建安装任务";
            return false;
        }

        return true;
    }

    private void RemoveConflictingUpgrades(BodyPartRecord part, GRUpgradeDef incomingDef, bool refundPluginMaterials)
    {
        if (pawn?.health?.hediffSet == null || part == null) return;

        foreach (GRUpgradeDef upgrade in DefDatabase<GRUpgradeDef>.AllDefsListForReading)
        {
            if (!IsValidUpgradeDef(upgrade)) continue;
            if (!TryGetActiveUpgradeHediff(upgrade, part, out Hediff activeHediff)) continue;
            if (incomingDef != null && upgrade == incomingDef) continue;
            if (incomingDef != null && UpgradesCanCoexist(upgrade, incomingDef)) continue;

            RemoveUpgradeInstance(upgrade, activeHediff.Part, refundPluginMaterials);
        }
    }

    private void RemoveUpgradeInstance(GRUpgradeDef upgrade, BodyPartRecord part, bool refundPluginMaterials)
    {
        if (pawn?.health?.hediffSet == null || part == null || upgrade?.hediffToApply == null) return;

        Hediff existing = pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.def == upgrade.hediffToApply && h.Part == part);
        if (existing == null) return;

        if (existing is Hediff_AddedPart)
        {
            RemoveAddedPartWithoutResettingOtherUpgrades(existing);
        }
        else
        {
            pawn.health.RemoveHediff(existing);
        }

        if (refundPluginMaterials && upgrade.IsPlugin)
        {
            RefundPluginMaterialsToGround(upgrade);
        }
    }

    private bool TryGetActiveUpgradeHediff(GRUpgradeDef def, BodyPartRecord part, out Hediff activeHediff)
    {
        activeHediff = null;
        if (pawn?.health?.hediffSet == null || def?.hediffToApply == null || part == null) return false;

        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff hediff = hediffs[i];
            if (hediff == null || hediff.def != def.hediffToApply || hediff.Part == null) continue;

            if (IsPartInSubtree(part, hediff.Part) || IsPartInSubtree(hediff.Part, part))
            {
                activeHediff = hediff;
                return true;
            }
        }

        return false;
    }

    private void RemoveAddedPartWithoutResettingOtherUpgrades(Hediff addedPartHediff)
    {
        if (pawn?.health?.hediffSet == null || addedPartHediff?.Part == null) return;

        BodyPartRecord rootPart = addedPartHediff.Part;
        pawn.health.RemoveHediff(addedPartHediff);

        List<Hediff> childMissingParts = pawn.health.hediffSet.hediffs
            .OfType<Hediff_MissingPart>()
            .Where(h => h.Part != null && IsPartInSubtree(h.Part, rootPart))
            .Cast<Hediff>()
            .ToList();

        foreach (Hediff missing in childMissingParts)
        {
            pawn.health.RemoveHediff(missing);
        }
    }

    private static bool IsPartInSubtree(BodyPartRecord part, BodyPartRecord root)
    {
        BodyPartRecord current = part;
        while (current != null)
        {
            if (current == root) return true;
            current = current.parent;
        }

        return false;
    }

    private static bool UpgradesCanCoexist(GRUpgradeDef left, GRUpgradeDef right)
    {
        if (left == null || right == null || left == right) return true;
        if (!left.IsPlugin && !right.IsPlugin) return false;
        return !left.ConflictsWith(right);
    }

    private bool CheckSkillRequirement(GRUpgradeDef def, out string reason)
    {
        reason = string.Empty;

        if (def.requiredSkill == null) return true;
        if (pawn.skills == null)
        {
            reason = "该单位没有技能系统";
            return false;
        }

        int level = pawn.skills.GetSkill(def.requiredSkill)?.Level ?? 0;
        if (level < def.minSkillLevel)
        {
            reason = $"技能不足: {def.requiredSkill.LabelCap} {level}/{def.minSkillLevel}";
            return false;
        }

        return true;
    }

    private bool HasRequiredMaterials(GRUpgradeDef def, out string reason)
    {
        reason = string.Empty;

        if (def.requiredMaterials == null || def.requiredMaterials.Count == 0) return true;
        if (pawn.Map == null)
        {
            reason = "不在地图上，无法检查材料";
            return false;
        }

        List<string> missing = new List<string>();
        foreach (ThingDefCountClass material in def.requiredMaterials)
        {
            if (material?.thingDef == null || material.count <= 0) continue;

            int available = pawn.Map.listerThings.ThingsOfDef(material.thingDef)
                .Where(IsGatherableThing)
                .Sum(t => t.stackCount);
            if (available < material.count)
            {
                missing.Add($"{material.thingDef.LabelCap} {available}/{material.count}");
            }
        }

        if (missing.Count == 0) return true;

        reason = "材料不足: " + string.Join("、", missing);
        return false;
    }

    private bool TryConsumeMaterialPlan(List<MaterialPickupTask> materialPlan, out string reason)
    {
        reason = string.Empty;
        if (materialPlan == null || materialPlan.Count == 0) return true;

        for (int i = 0; i < materialPlan.Count; i++)
        {
            MaterialPickupTask task = materialPlan[i];
            if (task?.thing == null || task.thing.Destroyed || task.count <= 0)
            {
                reason = "收集材料数据失效";
                return false;
            }

            if (task.thing.stackCount < task.count)
            {
                reason = $"材料不足: {task.thingDef?.LabelCap ?? task.thing.LabelCap}";
                return false;
            }
        }

        for (int i = 0; i < materialPlan.Count; i++)
        {
            MaterialPickupTask task = materialPlan[i];
            if (task.count >= task.thing.stackCount)
            {
                task.thing.Destroy(DestroyMode.Vanish);
            }
            else
            {
                Thing split = task.thing.SplitOff(task.count);
                split.Destroy(DestroyMode.Vanish);
            }
        }

        return true;
    }

    private void RefundPluginMaterialsToGround(GRUpgradeDef def)
    {
        if (pawn?.Map == null || def?.requiredMaterials == null) return;

        foreach (ThingDefCountClass material in def.requiredMaterials)
        {
            if (material?.thingDef == null || material.count <= 0) continue;

            DropMaterialNearPawn(material.thingDef, material.count);
        }
    }

    private void DropMaterialNearPawn(ThingDef thingDef, int totalCount)
    {
        if (thingDef == null || totalCount <= 0 || pawn?.Map == null) return;

        int stackLimit = Math.Max(1, thingDef.stackLimit);
        int remaining = totalCount;

        while (remaining > 0)
        {
            int stackCount = Math.Min(stackLimit, remaining);
            Thing thing = ThingMaker.MakeThing(thingDef);
            thing.stackCount = stackCount;
            GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            remaining -= stackCount;
        }
    }

    private bool IsGatherableThing(Thing thing)
    {
        return thing != null
               && thing.Spawned
               && !thing.Destroyed
               && !thing.IsForbidden(pawn)
               && pawn.CanReach(thing, PathEndMode.Touch, Danger.Some);
    }

    private static bool IsValidUpgradeDef(GRUpgradeDef def)
    {
        return def != null
               && def.hediffToApply != null
               && UpgradeTargetMatcher.HasAnyTargetRule(def);
    }

    private static bool IsUpgradeVisible(GRUpgradeDef def)
    {
        if (def == null) return false;
        return def.AreResearchPrerequisitesMet(out _);
    }
}
