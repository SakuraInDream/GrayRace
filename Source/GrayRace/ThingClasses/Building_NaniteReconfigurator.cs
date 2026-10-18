using System;
using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.DefModExtensions;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ThingClasses
{
    /// <summary>
    /// 纳米重塑台：
    /// - 玩家从地图上的金属材料中选择输入，加入队列时立即从地图扣除。
    /// - 任务完成后在输出格生成灰潮源质。
    /// - 产出只看质量：每 0.1kg 产出 1 灰潮源质，不考虑市场价值和耐久度倍率。
    /// - 速度固定：100 个材料耗时 600 tick。
    /// - 取消任务或拆除机器会退还尚未完成的输入材料。
    /// </summary>
    public class Building_NaniteReconfigurator : Building_WorkTable
    {
        public const int MinTaskCount = 100;

        /// <summary>每 100 个材料消耗的 tick。</summary>
        public const float TicksPerHundredItems = 600f;

        /// <summary>每 1kg 材料产出的灰潮源质。</summary>
        public const float NanitesPerKilogram = 10f;

        public const float IdlePowerConsumption = 1000f;
        public const float WorkingPowerConsumption = 10000f;

        private List<NaniteReconfiguratorTask> tasks = new List<NaniteReconfiguratorTask>();

        [Unsaved]
        private CompPowerTrader powerComp;

        [Unsaved]
        private CompBreakdownable breakdownComp;

        [Unsaved]
        private int lastManualWorkTick = -2;

        [Unsaved]
        private int cachedMaterialCountFrame = -1;

        [Unsaved]
        private readonly Dictionary<ThingDef, int> cachedMaterialCounts = new Dictionary<ThingDef, int>();

        public List<NaniteReconfiguratorTask> Tasks => tasks;

        public NaniteReconfiguratorTask ActiveTask => tasks.Count > 0 ? tasks[0] : null;

        public bool IsAutoWorking => tasks.Count > 0
            && powerComp != null
            && powerComp.PowerOn
            && (breakdownComp == null || !breakdownComp.BrokenDown);

        public int QueuedOutput
        {
            get
            {
                long total = 0L;
                for (int i = 0; i < tasks.Count; i++)
                {
                    NaniteReconfiguratorTask task = tasks[i];
                    if (task == null)
                    {
                        continue;
                    }

                    total += task.outputNanites;
                    if (total >= int.MaxValue)
                    {
                        return int.MaxValue;
                    }
                }

                return (int)total;
            }
        }

        /// <summary>
        /// 产物输出位置：由 DefModExtension 配置，随建筑旋转，便于贴合四向贴图的传送带。
        /// </summary>
        public IntVec3 OutputCell
        {
            get
            {
                if (!Spawned || Map == null)
                {
                    return IntVec3.Invalid;
                }

                DefModExtension_NaniteReconfiguratorOutput output =
                    def.GetModExtension<DefModExtension_NaniteReconfiguratorOutput>();
                IntVec3 offset = output != null
                    ? output.outputOffset.RotatedBy(Rotation)
                    : IntVec3.Zero;

                IntVec3 cell = Position + offset;
                return cell.InBounds(Map) ? cell : Position;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
            breakdownComp = GetComp<CompBreakdownable>();
        }

        public override void UsedThisTick()
        {
            base.UsedThisTick();
            lastManualWorkTick = Find.TickManager.TicksGame;
        }

        protected override void Tick()
        {
            base.Tick();

            if (!Spawned)
            {
                return;
            }

            bool autoWorking = false;
            if (powerComp != null
                && powerComp.PowerOn
                && (breakdownComp == null || !breakdownComp.BrokenDown)
                && tasks.Count > 0)
            {
                NaniteReconfiguratorTask task = tasks[0];
                if (task == null || task.material == null)
                {
                    tasks.RemoveAt(0);
                }
                else
                {
                    if (task.totalWork <= 0L)
                    {
                        task.totalWork = 1L;
                    }

                    task.progressTicks++;
                    autoWorking = true;

                    if (task.progressTicks >= task.totalWork)
                    {
                        tasks.RemoveAt(0);
                        SpawnNanites(task.outputNanites, true);
                    }
                }
            }

            bool manualWorking = (breakdownComp == null || !breakdownComp.BrokenDown)
                && Find.TickManager.TicksGame - lastManualWorkTick <= 1;

            if (powerComp != null)
            {
                powerComp.PowerOutput = !powerComp.PowerOn
                    ? 0f
                    : (autoWorking || manualWorking ? -WorkingPowerConsumption : -IdlePowerConsumption);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref tasks, "GR_NaniteReconfigurator_Tasks", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                tasks ??= new List<NaniteReconfiguratorTask>();

                for (int i = tasks.Count - 1; i >= 0; i--)
                {
                    NaniteReconfiguratorTask task = tasks[i];
                    if (task == null || task.material == null || task.count < MinTaskCount)
                    {
                        if (task != null && task.material != null && task.count > 0)
                        {
                            RefundMaterial(task.material, task.count);
                        }

                        tasks.RemoveAt(i);
                        continue;
                    }

                    if (task.outputNanites <= 0)
                    {
                        task.outputNanites = GetOutputNanites(task.material, task.count);
                    }

                    if (task.totalWork <= 0L)
                    {
                        task.totalWork = GetTaskWorkTicks(task.count);
                    }
                }
            }
        }

        public static bool HasMetallicStuffCategory(ThingDef def)
        {
            if (def?.stuffProps?.categories == null)
            {
                return false;
            }

            List<StuffCategoryDef> categories = def.stuffProps.categories;
            for (int i = 0; i < categories.Count; i++)
            {
                if (categories[i] == StuffCategoryDefOf.Metallic)
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanDecompose(ThingDef def)
        {
            return HasMetallicStuffCategory(def)
                && def.GetStatValueAbstract(StatDefOf.Mass) > 0f;
        }

        /// <summary>只看质量：总质量 / 0.1kg，四舍五入为整数个灰潮源质。</summary>
        public int GetOutputNanites(ThingDef def, int count)
        {
            if (def == null || count <= 0)
            {
                return 0;
            }

            float mass = Mathf.Max(0f, def.GetStatValueAbstract(StatDefOf.Mass));
            double output = mass * count * (double)NanitesPerKilogram;
            if (output <= 0.0)
            {
                return 0;
            }

            if (output >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return Mathf.Max(1, (int)Math.Round(output, MidpointRounding.AwayFromZero));
        }

        /// <summary>固定速度：100 个材料 / 600 tick。</summary>
        public long GetTaskWorkTicks(int count)
        {
            double ticks = Math.Max(0, count) * (double)TicksPerHundredItems / 100.0;
            if (ticks <= 0.0)
            {
                return 1L;
            }

            if (ticks >= long.MaxValue)
            {
                return long.MaxValue;
            }

            return (long)Math.Ceiling(ticks);
        }

        /// <summary>地图上可用（未禁用）的材料数量，与扣除逻辑保持一致。</summary>
        public int CountMaterialOnMap(ThingDef def)
        {
            if (Map == null || def == null)
            {
                return 0;
            }

            int frame = Time.frameCount;
            if (frame != cachedMaterialCountFrame)
            {
                cachedMaterialCounts.Clear();
                cachedMaterialCountFrame = frame;
            }

            if (!cachedMaterialCounts.TryGetValue(def, out int count))
            {
                count = CountMaterialOnMapRaw(def);
                cachedMaterialCounts[def] = count;
            }

            return count;
        }

        private int CountMaterialOnMapRaw(ThingDef def)
        {
            if (Map == null || def == null)
            {
                return 0;
            }

            List<Thing> things = Map.listerThings.ThingsOfDef(def);
            Faction player = Faction.OfPlayer;
            int count = 0;
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (!thing.Spawned || thing.Destroyed)
                {
                    continue;
                }

                if (player != null && thing.IsForbidden(player))
                {
                    continue;
                }

                count += thing.stackCount;
            }

            return count;
        }

        public bool TryEnqueueTask(ThingDef material, int count, out string failReasonKey)
        {
            failReasonKey = null;

            if (material == null)
            {
                failReasonKey = "GR_NaniteReconfigurator_NoMaterial";
                return false;
            }

            if (!CanDecompose(material))
            {
                failReasonKey = "GR_NaniteReconfigurator_InvalidMaterial";
                return false;
            }

            if (count < MinTaskCount)
            {
                failReasonKey = "GR_NaniteReconfigurator_MinCount";
                return false;
            }

            if (CountMaterialOnMap(material) < count)
            {
                failReasonKey = "GR_NaniteReconfigurator_NotEnoughMaterial";
                return false;
            }

            int removed = RemoveMaterialFromMap(material, count);
            InvalidateMaterialCountCache();
            if (removed < count)
            {
                RefundMaterial(material, removed);
                failReasonKey = "GR_NaniteReconfigurator_NotEnoughMaterial";
                return false;
            }

            tasks.Add(new NaniteReconfiguratorTask
            {
                material = material,
                count = count,
                outputNanites = GetOutputNanites(material, count),
                totalWork = GetTaskWorkTicks(count),
                progressTicks = 0L
            });

            return true;
        }

        private int RemoveMaterialFromMap(ThingDef def, int amount)
        {
            if (def == null || amount <= 0 || Map == null)
            {
                return 0;
            }

            List<Thing> things = new List<Thing>(Map.listerThings.ThingsOfDef(def));
            Faction player = Faction.OfPlayer;
            int remaining = amount;

            for (int i = things.Count - 1; i >= 0 && remaining > 0; i--)
            {
                Thing thing = things[i];
                if (!thing.Spawned || thing.Destroyed)
                {
                    continue;
                }

                if (player != null && thing.IsForbidden(player))
                {
                    continue;
                }

                int take = Mathf.Min(remaining, thing.stackCount);
                if (take <= 0)
                {
                    continue;
                }

                Thing split = thing.SplitOff(take);
                if (split != null)
                {
                    split.Destroy(DestroyMode.Vanish);
                }

                remaining -= take;
            }

            return amount - remaining;
        }

        public void CancelTask(NaniteReconfiguratorTask task)
        {
            if (task != null && tasks.Remove(task))
            {
                RefundMaterial(task.material, task.count);
            }
        }

        /// <summary>开发者命令：立即完成队首配方。仅用于调试。</summary>
        public bool DebugCompleteCurrentTask()
        {
            if (tasks.Count == 0)
            {
                return false;
            }

            NaniteReconfiguratorTask task = tasks[0];
            tasks.RemoveAt(0);
            if (task == null || task.material == null)
            {
                return true;
            }

            if (task.outputNanites <= 0)
            {
                task.outputNanites = GetOutputNanites(task.material, task.count);
            }

            SpawnNanites(task.outputNanites, true);
            return true;
        }

        /// <summary>开发者命令：立即完成队列中的全部配方。仅用于调试。</summary>
        public int DebugCompleteAllTasks()
        {
            int completed = 0;
            while (tasks.Count > 0)
            {
                DebugCompleteCurrentTask();
                completed++;
            }

            return completed;
        }

        private void RefundMaterial(ThingDef def, int count)
        {
            if (def != null && count > 0)
            {
                SpawnStacks(def, count, false);
            }

            InvalidateMaterialCountCache();
        }

        private void SpawnNanites(int amount, bool playEffect)
        {
            SpawnStacks(GrayRaceDefOf.GR_Nanites, amount, playEffect);
        }

        private void SpawnStacks(ThingDef def, int totalItems, bool playEffect)
        {
            if (def == null || totalItems <= 0 || !Spawned)
            {
                return;
            }

            IntVec3 outputCell = OutputCell;
            if (!outputCell.IsValid)
            {
                outputCell = Position;
            }

            if (playEffect)
            {
                FleckMaker.ThrowMicroSparks(outputCell.ToVector3Shifted(), Map);
                FleckMaker.ThrowDustPuff(outputCell, Map, 1.5f);
            }

            int remaining = totalItems;
            while (remaining > 0)
            {
                int stackCount = Mathf.Min(remaining, Mathf.Max(1, def.stackLimit));
                Thing thing = ThingMaker.MakeThing(def);
                thing.stackCount = stackCount;

                if (!GenPlace.TryPlaceThing(thing, outputCell, Map, ThingPlaceMode.Near)
                    && !GenPlace.TryPlaceThing(thing, Position, Map, ThingPlaceMode.Near))
                {
                    Log.Warning($"GR_NaniteReconfigurator: 无法在输出格 {outputCell} 放置 {def.defName}，已销毁 {stackCount} 个。");
                    thing.Destroy();
                }

                remaining -= stackCount;
            }
        }

        private void InvalidateMaterialCountCache()
        {
            cachedMaterialCountFrame = -1;
            cachedMaterialCounts.Clear();
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (Spawned && Map != null && (mode == DestroyMode.Deconstruct || mode == DestroyMode.KillFinalize))
            {
                for (int i = 0; i < tasks.Count; i++)
                {
                    NaniteReconfiguratorTask task = tasks[i];
                    if (task != null && task.material != null && task.count > 0)
                    {
                        SpawnStacks(task.material, task.count, false);
                    }
                }

                tasks.Clear();
            }

            base.Destroy(mode);
        }
    }
}
