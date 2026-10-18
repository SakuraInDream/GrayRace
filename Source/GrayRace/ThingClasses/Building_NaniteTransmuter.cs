using System;
using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.DefModExtensions;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ThingClasses
{
    /// <summary>
    /// 纳米变形器：
    /// - 机器内部存储灰潮源质，由玩家在 UI 中手动装填。
    /// - 玩家选择产物和数量后加入任务队列，加入时立即扣除灰潮源质。
    /// - 任务按队列顺序执行，完整完成后才一次性输出产物。
    /// - 取消任务（包括正在执行的任务）会全额返还已扣除的灰潮源质。
    ///
    /// 成本：每个产物 = Mass / 0.1kg，再乘市场价值倍率。
    /// 时间：总耗材 × 2 tick，再乘 stuffProps.statFactors.MaxHitPoints 时间倍率。
    /// </summary>
    public class Building_NaniteTransmuter : Building_WorkTable
    {
        public const int MinTaskCount = 100;
        public const float MaxStoredNanites = 5000000f;

        /// <summary>每 1 点灰潮源质耗材对应的工作 tick。</summary>
        public const float WorkTicksPerNanite = 1f;

        public const float IdlePowerConsumption = 1000f;
        public const float WorkingPowerConsumption = 10000f;

        private float storedNanites;
        private List<NaniteTransmuterTask> tasks = new List<NaniteTransmuterTask>();

        [Unsaved]
        private int lastManualWorkTick = -2;

        [Unsaved]
        private CompPowerTrader powerComp;

        [Unsaved]
        private CompBreakdownable breakdownComp;

        public float StoredNanites => storedNanites;

        public List<NaniteTransmuterTask> Tasks => tasks;

        public NaniteTransmuterTask ActiveTask => tasks.Count > 0 ? tasks[0] : null;

        public bool IsAutoWorking => tasks.Count > 0
            && powerComp != null
            && powerComp.PowerOn
            && (breakdownComp == null || !breakdownComp.BrokenDown);

        /// <summary>
        /// 产物输出位置：由 DefModExtension 配置固定偏移，不随建筑旋转。
        /// 这样可以直接按机器贴图上的传送带位置调整。
        /// </summary>
        public IntVec3 OutputCell
        {
            get
            {
                if (!Spawned || Map == null)
                {
                    return IntVec3.Invalid;
                }

                DefModExtension_NaniteTransmuterOutput output =
                    def.GetModExtension<DefModExtension_NaniteTransmuterOutput>();
                IntVec3 offset = output != null ? output.outputOffset : IntVec3.Zero;

                IntVec3 cell = Position + offset;
                return cell.InBounds(Map) ? cell : Position;
            }
        }

        public int NanitesOnMap
        {
            get
            {
                if (Map == null)
                {
                    return 0;
                }

                ThingDef naniteDef = GrayRaceDefOf.GR_Nanites;
                if (naniteDef == null)
                {
                    return 0;
                }

                int count = 0;
                List<Thing> things = Map.listerThings.ThingsOfDef(naniteDef);
                Faction player = Faction.OfPlayer;
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
                NaniteTransmuterTask task = tasks[0];
                if (task.product == null)
                {
                    tasks.RemoveAt(0);
                    RefundNanites(task.cost);
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
                        SpawnProducts(task.product, task.count, true);
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

            Scribe_Values.Look(ref storedNanites, "GR_NaniteTransmuter_StoredNanites", 0f);
            Scribe_Collections.Look(ref tasks, "GR_NaniteTransmuter_Tasks", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                tasks ??= new List<NaniteTransmuterTask>();
                storedNanites = Mathf.Clamp(storedNanites, 0f, MaxStoredNanites);

                for (int i = tasks.Count - 1; i >= 0; i--)
                {
                    NaniteTransmuterTask task = tasks[i];
                    if (task != null && task.product != null && task.count >= MinTaskCount && task.totalWork > 0L)
                    {
                        continue;
                    }

                    if (task != null)
                    {
                        storedNanites = Mathf.Min(MaxStoredNanites, storedNanites + task.cost);
                    }

                    tasks.RemoveAt(i);
                }
            }
        }

        /// <summary>每个产物消耗的灰潮源质（允许小数）。</summary>
        public float GetCostPerItem(ThingDef def)
        {
            if (def == null)
            {
                return 0f;
            }

            float mass = Mathf.Max(0f, def.GetStatValueAbstract(StatDefOf.Mass));
            float costPerItem = mass / 0.1f;

            float marketValue = Mathf.Max(0f, def.GetStatValueAbstract(StatDefOf.MarketValue));
            float marketValueMultiplier = 1f;
            if (marketValue > 10f)
            {
                marketValueMultiplier += 0.1f * Mathf.CeilToInt(marketValue - 10f);
            }

            return Mathf.Max(0.01f, costPerItem * marketValueMultiplier);
        }

        public float GetTaskCost(ThingDef def, int count)
        {
            return GetCostPerItem(def) * Mathf.Max(0, count);
        }

        /// <summary>整个任务需要的工作 tick。</summary>
        public long GetTaskWorkTicks(ThingDef def, int count)
        {
            double ticks = GetTaskCost(def, count) * WorkTicksPerNanite * GetTimeMultiplier(def);
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

        /// <summary>
        /// 读取 stuffProps.statFactors 中的 MaxHitPoints。
        /// > 10 时每超出 1 点（向上取整）增加 10% 时间。
        /// </summary>
        public float GetTimeMultiplier(ThingDef def)
        {
            if (def?.stuffProps?.statFactors == null)
            {
                return 1f;
            }

            List<StatModifier> factors = def.stuffProps.statFactors;
            for (int i = 0; i < factors.Count; i++)
            {
                StatModifier modifier = factors[i];
                if (modifier.stat == StatDefOf.MaxHitPoints && modifier.value > 10f)
                {
                    return 1f + 0.1f * Mathf.CeilToInt(modifier.value - 10f);
                }
            }

            return 1f;
        }

        public static bool HasMetalOrStoneStuffCategory(ThingDef def)
        {
            if (def?.stuffProps?.categories == null)
            {
                return false;
            }

            List<StuffCategoryDef> categories = def.stuffProps.categories;
            for (int i = 0; i < categories.Count; i++)
            {
                StuffCategoryDef category = categories[i];
                if (category == StuffCategoryDefOf.Metallic || category == StuffCategoryDefOf.Stony)
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanTransmute(ThingDef def)
        {
            if (!HasMetalOrStoneStuffCategory(def))
            {
                return false;
            }

            DefModExtension_NaniteTransmuterBlacklist blacklist =
                this.def.GetModExtension<DefModExtension_NaniteTransmuterBlacklist>();
            return blacklist?.blacklistedDefs == null || !blacklist.blacklistedDefs.Contains(def);
        }

        public bool TryEnqueueTask(ThingDef product, int count, out string failReasonKey)
        {
            failReasonKey = null;

            if (product == null)
            {
                failReasonKey = "GR_NaniteTransmuter_NoProduct";
                return false;
            }

            if (count < MinTaskCount)
            {
                failReasonKey = "GR_NaniteTransmuter_MinCount";
                return false;
            }

            float cost = GetTaskCost(product, count);
            if (storedNanites + 0.0001f < cost)
            {
                failReasonKey = "GR_NaniteTransmuter_NotEnoughNanites";
                return false;
            }

            long work = GetTaskWorkTicks(product, count);
            if (work <= 0L)
            {
                work = 1L;
            }

            storedNanites = Mathf.Max(0f, storedNanites - cost);
            tasks.Add(new NaniteTransmuterTask
            {
                product = product,
                count = count,
                cost = cost,
                totalWork = work,
                progressTicks = 0L
            });
            return true;
        }

        public bool TryLoadNanites(int amount, out string failReasonKey)
        {
            failReasonKey = null;

            if (amount <= 0)
            {
                failReasonKey = "GR_NaniteTransmuter_InvalidAmount";
                return false;
            }

            float space = MaxStoredNanites - storedNanites;
            if (space < 1f)
            {
                failReasonKey = "GR_NaniteTransmuter_StorageFull";
                return false;
            }

            int toLoad = Mathf.Min(amount, Mathf.FloorToInt(space));
            int loaded = RemoveNanitesFromMap(toLoad);
            if (loaded <= 0)
            {
                failReasonKey = "GR_NaniteTransmuter_NoNanitesOnMap";
                return false;
            }

            storedNanites = Mathf.Min(MaxStoredNanites, storedNanites + loaded);
            return true;
        }

        public void CancelTask(NaniteTransmuterTask task)
        {
            if (task != null && tasks.Remove(task))
            {
                RefundNanites(task.cost);
            }
        }

        /// <summary>开发者命令：填满内部灰潮源质存储。</summary>
        public void DebugFillNanites()
        {
            storedNanites = MaxStoredNanites;
        }

        /// <summary>开发者命令：清空内部灰潮源质存储。</summary>
        public void DebugClearNanites()
        {
            storedNanites = 0f;
        }

        /// <summary>开发者命令：立即完成队首配方。</summary>
        public bool DebugCompleteCurrentTask()
        {
            if (tasks.Count == 0)
            {
                return false;
            }

            NaniteTransmuterTask task = tasks[0];
            tasks.RemoveAt(0);
            if (task == null)
            {
                return true;
            }

            if (task.product != null && task.count > 0)
            {
                SpawnProducts(task.product, task.count, true);
            }

            return true;
        }

        /// <summary>开发者命令：立即完成队列中的全部配方。</summary>
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

        private int RemoveNanitesFromMap(int amount)
        {
            if (amount <= 0 || Map == null)
            {
                return 0;
            }

            ThingDef naniteDef = GrayRaceDefOf.GR_Nanites;
            if (naniteDef == null)
            {
                return 0;
            }

            List<Thing> things = new List<Thing>(Map.listerThings.ThingsOfDef(naniteDef));
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

        private void RefundNanites(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            float space = Mathf.Max(0f, MaxStoredNanites - storedNanites);
            float toStore = Mathf.Min(amount, space);
            storedNanites += toStore;

            float overflow = amount - toStore;
            if (overflow > 0.001f && Spawned)
            {
                SpawnProducts(GrayRaceDefOf.GR_Nanites, Mathf.CeilToInt(overflow));
            }
        }

        private void SpawnProducts(ThingDef def, int totalItems, bool playEffect = false)
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

                if (!GenPlace.TryPlaceThing(thing, outputCell, Map, ThingPlaceMode.Near))
                {
                    Log.Warning($"GR_NaniteTransmuter: 无法在交互格 {outputCell} 放置产物 {def.defName}，已销毁 {stackCount} 个。");
                    thing.Destroy();
                }

                remaining -= stackCount;
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (Spawned && Map != null && (mode == DestroyMode.Deconstruct || mode == DestroyMode.KillFinalize))
            {
                float refund = storedNanites;
                for (int i = 0; i < tasks.Count; i++)
                {
                    refund += tasks[i].cost;
                }

                storedNanites = 0f;
                tasks.Clear();

                if (refund > 0.001f)
                {
                    SpawnProducts(GrayRaceDefOf.GR_Nanites, Mathf.CeilToInt(refund));
                }
            }

            base.Destroy(mode);
        }
    }
}
