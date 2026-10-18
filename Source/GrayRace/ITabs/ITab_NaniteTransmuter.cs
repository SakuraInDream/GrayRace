using System;
using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.ThingClasses;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.ITabs
{
    /// <summary>
    /// 纳米变形器 UI：
    /// 左侧选产物，中间预览 + 数量计划，右侧任务队列，顶部装填灰潮源质。
    /// 原版 ITab_Bills 仍然保留，手动 Bill 不受影响。
    /// </summary>
    public class ITab_NaniteTransmuter : ITab
    {
        private static readonly Vector2 TabSize = new Vector2(1000f, 660f);
        private const float Margin = 8f;
        private const float TopBarHeight = 70f;
        private const float ColumnGap = 10f;
        private const float SideColumnWidth = 290f;
        private const float PreviewHeight = 300f;

        private static List<ThingDef> allProducts;

        private Building_NaniteTransmuter lastBuilding;
        private ThingDef selectedProduct;
        private int selectedCount = Building_NaniteTransmuter.MinTaskCount;
        private string countBuffer = Building_NaniteTransmuter.MinTaskCount.ToString();
        private string searchQuery = string.Empty;
        private string loadAmountBuffer = "1000";
        private ThingDef animatedPreviewDef;
        private float animatedPreviewStart;

        private Vector2 productScroll;
        private float productScrollHeight;
        private Vector2 queueScroll;
        private float queueScrollHeight;

        public ITab_NaniteTransmuter()
        {
            size = TabSize;
            labelKey = "GR_NaniteTransmuterTab";
        }

        private Building_NaniteTransmuter SelBuilding => SelThing as Building_NaniteTransmuter;

        public override bool IsVisible => base.IsVisible && SelBuilding != null;

        protected override void FillTab()
        {
            Building_NaniteTransmuter building = SelBuilding;
            if (building == null)
            {
                return;
            }

            if (building != lastBuilding)
            {
                lastBuilding = building;
                selectedProduct = null;
                selectedCount = Building_NaniteTransmuter.MinTaskCount;
                countBuffer = selectedCount.ToString();
                loadAmountBuffer = "1000";
                productScroll = Vector2.zero;
                queueScroll = Vector2.zero;
                animatedPreviewDef = null;
                animatedPreviewStart = 0f;
            }

            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(Margin);
            DrawStorageBar(new Rect(rect.x, rect.y, rect.width, TopBarHeight), building);

            Rect content = new Rect(
                rect.x,
                rect.y + TopBarHeight + ColumnGap,
                rect.width,
                rect.height - TopBarHeight - ColumnGap);

            Rect leftRect = new Rect(content.x, content.y, SideColumnWidth, content.height);
            Rect rightRect = new Rect(content.xMax - SideColumnWidth, content.y, SideColumnWidth, content.height);
            Rect middleRect = new Rect(
                leftRect.xMax + ColumnGap,
                content.y,
                rightRect.x - leftRect.xMax - ColumnGap * 2f,
                content.height);

            DrawProductColumn(leftRect, building);
            DrawPlanningColumn(middleRect, building);
            DrawQueueColumn(rightRect, building);
        }

        private void DrawStorageBar(Rect rect, Building_NaniteTransmuter building)
        {
            Widgets.DrawMenuSection(rect);

            Widgets.Label(new Rect(rect.x + 8f, rect.y + 8f, 110f, 24f),
                "GR_NaniteTransmuter_StorageTitle".Translate().ToString());
            Widgets.Label(new Rect(rect.x + 118f, rect.y + 8f, 70f, 24f),
                "GR_NaniteTransmuter_LoadAmount".Translate().ToString());

            loadAmountBuffer = Widgets.TextField(new Rect(rect.x + 190f, rect.y + 6f, 110f, 26f), loadAmountBuffer);

            if (Widgets.ButtonText(new Rect(rect.x + 306f, rect.y + 6f, 70f, 26f),
                    "GR_NaniteTransmuter_Load".Translate().ToString()))
            {
                if (!int.TryParse(loadAmountBuffer, out int amount))
                {
                    Messages.Message("GR_NaniteTransmuter_InvalidAmount".Translate().ToString(),
                        MessageTypeDefOf.RejectInput, false);
                }
                else if (building.TryLoadNanites(amount, out string reason))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
                else
                {
                    Messages.Message(reason.Translate().ToString(), MessageTypeDefOf.RejectInput, false);
                }
            }

            Widgets.Label(new Rect(rect.x + 392f, rect.y + 8f, 260f, 24f),
                "GR_NaniteTransmuter_AvailableOnMap".Translate(building.NanitesOnMap).ToString());

            Widgets.Label(new Rect(rect.x + 8f, rect.y + 38f, 520f, 24f),
                "GR_NaniteTransmuter_StoredOfMax".Translate(
                    building.StoredNanites.ToStringDecimalIfSmall(),
                    Building_NaniteTransmuter.MaxStoredNanites.ToString("0")).ToString());

            if (DebugSettings.godMode)
            {
                DrawDebugButtons(rect, building);
            }
        }

        /// <summary>
        /// 仅在开发者 God mode 下显示的调试按钮，放在顶部第一行的空白区域。
        /// </summary>
        private static void DrawDebugButtons(Rect rect, Building_NaniteTransmuter building)
        {
            const float buttonHeight = 26f;
            const float gap = 4f;
            const float fillWidth = 54f;
            const float clearWidth = 54f;
            const float completeWidth = 80f;
            const float totalWidth = fillWidth + clearWidth + completeWidth * 2f + gap * 3f;
            float x = rect.xMax - 4f - totalWidth;
            float y = rect.y + 6f;

            Rect fillRect = new Rect(x, y, fillWidth, buttonHeight);
            x = fillRect.xMax + gap;
            Rect clearRect = new Rect(x, y, clearWidth, buttonHeight);
            x = clearRect.xMax + gap;
            Rect completeCurrentRect = new Rect(x, y, completeWidth, buttonHeight);
            x = completeCurrentRect.xMax + gap;
            Rect completeAllRect = new Rect(x, y, completeWidth, buttonHeight);

            bool hasTasks = building.Tasks.Count > 0;
            string completeCurrentTip = hasTasks
                ? "GR_NaniteTransmuter_DebugCompleteCurrentTip".Translate().ToString()
                : "GR_NaniteTransmuter_NoTask".Translate().ToString();
            string completeAllTip = hasTasks
                ? "GR_NaniteTransmuter_DebugCompleteAllTip".Translate().ToString()
                : "GR_NaniteTransmuter_NoTask".Translate().ToString();

            TooltipHandler.TipRegion(fillRect,
                "GR_NaniteTransmuter_DebugFillTip".Translate().ToString());
            TooltipHandler.TipRegion(clearRect,
                "GR_NaniteTransmuter_DebugClearTip".Translate().ToString());
            TooltipHandler.TipRegion(completeCurrentRect, completeCurrentTip);
            TooltipHandler.TipRegion(completeAllRect, completeAllTip);

            if (Widgets.ButtonText(fillRect, "GR_NaniteTransmuter_DebugFillButton".Translate().ToString()))
            {
                building.DebugFillNanites();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            if (Widgets.ButtonText(clearRect, "GR_NaniteTransmuter_DebugClearButton".Translate().ToString()))
            {
                building.DebugClearNanites();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            Color oldColor = GUI.color;
            if (!hasTasks)
            {
                GUI.color = Color.gray;
            }

            if (Widgets.ButtonText(completeCurrentRect,
                    "GR_NaniteTransmuter_DebugCompleteCurrentButton".Translate().ToString())
                && hasTasks)
            {
                building.DebugCompleteCurrentTask();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            if (Widgets.ButtonText(completeAllRect,
                    "GR_NaniteTransmuter_DebugCompleteAllButton".Translate().ToString())
                && hasTasks)
            {
                building.DebugCompleteAllTasks();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            GUI.color = oldColor;
        }

        private void DrawProductColumn(Rect rect, Building_NaniteTransmuter building)
        {
            Widgets.DrawMenuSection(rect);

            Widgets.Label(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 140f, 24f),
                "GR_NaniteTransmuter_ProductList".Translate().ToString());

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(rect.x + rect.width - 132f, rect.y + 6f, 126f, 24f),
                "GR_NaniteTransmuter_CostPer100".Translate().ToString());
            Text.Anchor = TextAnchor.UpperLeft;

            searchQuery = Widgets.TextField(
                new Rect(rect.x + 6f, rect.y + 32f, rect.width - 12f, 26f),
                searchQuery ?? string.Empty);

            Rect scrollOuter = new Rect(rect.x + 4f, rect.y + 64f, rect.width - 8f, rect.height - 68f);
            Rect viewRect = new Rect(0f, 0f, scrollOuter.width - 16f, productScrollHeight);
            Widgets.BeginScrollView(scrollOuter, ref productScroll, viewRect);

            float curY = 0f;
            string query = searchQuery?.Trim().ToLowerInvariant();
            List<ThingDef> products = AllProducts;
            for (int i = 0; i < products.Count; i++)
            {
                ThingDef thingDef = products[i];
                if (!building.CanTransmute(thingDef))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(query))
                {
                    string label = (thingDef.label ?? thingDef.defName).ToLowerInvariant();
                    if (!label.Contains(query))
                    {
                        continue;
                    }
                }

                DrawProductRow(ref curY, viewRect.width, building, thingDef);
            }

            if (Event.current.type == EventType.Layout)
            {
                productScrollHeight = curY + 10f;
            }

            Widgets.EndScrollView();
        }

        private void DrawProductRow(ref float y, float width, Building_NaniteTransmuter building, ThingDef thingDef)
        {
            Rect row = new Rect(0f, y, width, 28f);
            if (thingDef == selectedProduct)
            {
                Widgets.DrawHighlightSelected(row);
            }
            else
            {
                Widgets.DrawHighlightIfMouseover(row);
            }

            if (thingDef.DrawMatSingle?.mainTexture != null)
            {
                Widgets.ThingIcon(new Rect(4f, y + 2f, 24f, 24f), thingDef);
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            Widgets.Label(new Rect(32f, y, width - 108f, 28f), thingDef.LabelCap.Truncate(width - 108f));
            Text.WordWrap = true;

            float costPer100 = building.GetCostPerItem(thingDef) * 100f;

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(
                new Rect(width - 74f, y, 70f, 28f),
                costPer100.ToStringDecimalIfSmall());
            Text.Anchor = TextAnchor.UpperLeft;

            string tip = thingDef.description;
            string costTip = "GR_NaniteTransmuter_CostPer100Tip"
                .Translate(costPer100.ToStringDecimalIfSmall())
                .ToString();
            tip = string.IsNullOrEmpty(tip) ? costTip : tip + "\n\n" + costTip;
            TooltipHandler.TipRegion(row, tip);

            if (GUI.Button(row, string.Empty, Widgets.EmptyStyle))
            {
                selectedProduct = thingDef;
                selectedCount = Building_NaniteTransmuter.MinTaskCount;
                countBuffer = selectedCount.ToString();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            y += 28f;
        }

        private void DrawPlanningColumn(Rect rect, Building_NaniteTransmuter building)
        {
            Rect previewRect = new Rect(rect.x, rect.y, rect.width, PreviewHeight);
            ThingDef previewDef = selectedProduct ?? building.ActiveTask?.product;
            DrawPreview(previewRect, previewDef);

            Rect planRect = new Rect(
                rect.x,
                previewRect.yMax + ColumnGap,
                rect.width,
                rect.yMax - previewRect.yMax - ColumnGap);
            Widgets.DrawMenuSection(planRect);

            if (selectedProduct == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(planRect, "GR_NaniteTransmuter_PreviewHint".Translate().ToString());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            float x = planRect.x + 10f;
            float y = planRect.y + 8f;
            float width = planRect.width - 20f;

            Widgets.Label(new Rect(x, y, width, 24f),
                "GR_NaniteTransmuter_SelectedProduct".Translate(selectedProduct.LabelCap).ToString());
            y += 28f;

            Widgets.Label(new Rect(x, y, width, 22f),
                "GR_NaniteTransmuter_Quantity".Translate().ToString());
            y += 24f;

            if (Widgets.ButtonText(new Rect(x, y, 34f, 28f), "-"))
            {
                selectedCount = AdjustCount(selectedCount, -100 * GenUI.CurrentAdjustmentMultiplier());
                countBuffer = selectedCount.ToString();
            }

            countBuffer = Widgets.TextField(new Rect(x + 38f, y, 110f, 28f), countBuffer);
            if (int.TryParse(countBuffer, out int parsed))
            {
                selectedCount = Mathf.Max(Building_NaniteTransmuter.MinTaskCount, parsed);
            }

            if (Widgets.ButtonText(new Rect(x + 152f, y, 34f, 28f), "+"))
            {
                selectedCount = AdjustCount(selectedCount, 100 * GenUI.CurrentAdjustmentMultiplier());
                countBuffer = selectedCount.ToString();
            }

            Widgets.Label(new Rect(x + 194f, y + 4f, width - 194f, 24f),
                "GR_NaniteTransmuter_StepHint".Translate().ToString());
            y += 38f;

            float cost = building.GetTaskCost(selectedProduct, selectedCount);
            long ticks = building.GetTaskWorkTicks(selectedProduct, selectedCount);
            bool enough = building.StoredNanites + 0.0001f >= cost;

            Widgets.Label(new Rect(x, y, width, 24f),
                "GR_NaniteTransmuter_EstimatedCost".Translate(cost.ToStringDecimalIfSmall()).ToString());
            y += 24f;
            Widgets.Label(new Rect(x, y, width, 24f),
                "GR_NaniteTransmuter_EstimatedTime".Translate(FormatTicks(ticks)).ToString());
            y += 24f;
            Widgets.Label(new Rect(x, y, width, 24f),
                "GR_NaniteTransmuter_MachineStock".Translate(building.StoredNanites.ToStringDecimalIfSmall()).ToString());
            y += 24f;

            if (!enough)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 0.45f, 0.45f);
                Widgets.Label(new Rect(x, y, width, 24f),
                    "GR_NaniteTransmuter_NotEnoughNanites".Translate().ToString());
                GUI.color = old;
            }
            y += 28f;

            Color oldColor = GUI.color;
            if (!enough)
            {
                GUI.color = Color.gray;
            }

            if (Widgets.ButtonText(new Rect(x, y, width, 32f),
                    "GR_NaniteTransmuter_AddToQueue".Translate().ToString()))
            {
                if (building.TryEnqueueTask(selectedProduct, selectedCount, out string reason))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
                else
                {
                    Messages.Message(reason.Translate().ToString(), MessageTypeDefOf.RejectInput, false);
                }
            }

            GUI.color = oldColor;
        }

        private void DrawQueueColumn(Rect rect, Building_NaniteTransmuter building)
        {
            Widgets.DrawMenuSection(rect);

            Widgets.Label(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, 24f),
                "GR_NaniteTransmuter_Queue".Translate().ToString());

            Rect scrollOuter = new Rect(rect.x + 4f, rect.y + 34f, rect.width - 8f, rect.height - 38f);
            Rect viewRect = new Rect(0f, 0f, scrollOuter.width - 16f, queueScrollHeight);
            Widgets.BeginScrollView(scrollOuter, ref queueScroll, viewRect);

            float curY = 0f;
            NaniteTransmuterTask taskToCancel = null;

            if (building.Tasks.Count == 0)
            {
                Widgets.Label(new Rect(0f, 0f, viewRect.width, 24f),
                    "GR_NaniteTransmuter_NoTask".Translate().ToString());
            }
            else
            {
                for (int i = 0; i < building.Tasks.Count; i++)
                {
                    NaniteTransmuterTask task = building.Tasks[i];
                    if (i == 0)
                    {
                        DrawActiveTaskRow(ref curY, viewRect.width, task, ref taskToCancel);
                    }
                    else
                    {
                        DrawQueuedTaskRow(ref curY, viewRect.width, task, i, ref taskToCancel);
                    }
                }
            }

            if (Event.current.type == EventType.Layout)
            {
                queueScrollHeight = curY + 10f;
            }

            Widgets.EndScrollView();

            if (taskToCancel != null)
            {
                building.CancelTask(taskToCancel);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }

        private void DrawActiveTaskRow(
            ref float y,
            float width,
            NaniteTransmuterTask task,
            ref NaniteTransmuterTask taskToCancel)
        {
            Rect row = new Rect(0f, y, width, 84f);
            Widgets.DrawHighlightSelected(row);

            if (task.product != null && task.product.DrawMatSingle?.mainTexture != null)
            {
                Widgets.ThingIcon(new Rect(4f, y + 4f, 32f, 32f), task.product);
            }

            string productLabel = task.product != null
                ? task.product.LabelCap.ToString()
                : "GR_NaniteTransmuter_None".Translate().ToString();
            Widgets.Label(
                new Rect(42f, y + 4f, width - 100f, 24f),
                "GR_NaniteTransmuter_ActiveTask".Translate(productLabel, task.count)
                    .ToString()
                    .Truncate(width - 100f));

            Widgets.Label(
                new Rect(42f, y + 26f, width - 100f, 20f),
                "GR_NaniteTransmuter_Cost".Translate(task.cost.ToStringDecimalIfSmall()).ToString());

            Rect bar = new Rect(4f, y + 50f, width - 62f, 12f);
            Widgets.DrawBoxSolid(bar, new Color(0.14f, 0.14f, 0.14f, 1f));
            Widgets.DrawBoxSolid(
                new Rect(bar.x, bar.y, bar.width * task.ProgressPercent, bar.height),
                new Color(0.3f, 0.72f, 0.3f, 1f));

            Widgets.Label(
                new Rect(4f, y + 62f, width - 62f, 20f),
                task.ProgressPercent.ToStringPercent() + " (" + FormatTicks(task.RemainingTicks) + ")");

            if (Widgets.ButtonText(new Rect(width - 54f, y + 4f, 50f, 24f),
                    "GR_NaniteTransmuter_Cancel".Translate().ToString()))
            {
                taskToCancel = task;
            }

            y += 84f;
        }

        private void DrawQueuedTaskRow(
            ref float y,
            float width,
            NaniteTransmuterTask task,
            int index,
            ref NaniteTransmuterTask taskToCancel)
        {
            Rect row = new Rect(0f, y, width, 42f);
            Widgets.DrawHighlightIfMouseover(row);

            if (task.product != null && task.product.DrawMatSingle?.mainTexture != null)
            {
                Widgets.ThingIcon(new Rect(4f, y + 5f, 28f, 28f), task.product);
            }

            Widgets.Label(
                new Rect(36f, y + 3f, width - 92f, 20f),
                (index + ". " + (task.product?.LabelCap.ToString() ?? "?") + " × " + task.count)
                    .Truncate(width - 92f));

            Widgets.Label(
                new Rect(36f, y + 21f, width - 92f, 18f),
                "GR_NaniteTransmuter_Cost".Translate(task.cost.ToStringDecimalIfSmall()).ToString());

            if (Widgets.ButtonText(new Rect(width - 54f, y + 8f, 50f, 24f),
                    "GR_NaniteTransmuter_Cancel".Translate().ToString()))
            {
                taskToCancel = task;
            }

            y += 42f;
        }

        private void DrawPreview(Rect rect, ThingDef def)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.05f, 0.06f, 0.07f, 0.94f));
            Widgets.DrawBox(rect, 1);

            if (def == null)
            {
                animatedPreviewDef = null;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "GR_NaniteTransmuter_PreviewHint".Translate().ToString());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            Texture icon = def.uiIcon;
            if (icon == null && def.DrawMatSingle != null)
            {
                icon = def.DrawMatSingle.mainTexture;
            }

            const float Cycle = 6f;
            float now = Time.realtimeSinceStartup;
            if (def != animatedPreviewDef)
            {
                animatedPreviewDef = def;
                animatedPreviewStart = now;
            }

            float time = now - animatedPreviewStart;
            float t = (time % Cycle) / Cycle;

            // 0.00 - 0.58：灰潮粒子从外围螺旋向中心汇聚
            // 0.50 - 0.80：产物在粒子核心中逐渐显现
            // 0.92 - 1.00：产物淡出，重新开始下一轮
            float gather = Mathf.Clamp01(t / 0.58f);
            float gatherEase = 1f - Mathf.Pow(1f - gather, 3f);
            float reveal = Mathf.Clamp01((t - 0.50f) / 0.30f);
            float fadeOut = Mathf.Clamp01((t - 0.92f) / 0.08f);
            float iconAlpha = reveal * (1f - fadeOut);
            float appear = Mathf.Clamp01(t / 0.08f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 2.2f);

            Vector2 center = rect.center;
            float maxRadius = Mathf.Min(rect.width, rect.height) * 0.42f;

            for (int i = 0; i < 40; i++)
            {
                float seed = i * 2.399963f;
                float wobble = 0.70f + 0.30f * Mathf.Sin(i * 1.3f + time * 3.1f);
                float radius = maxRadius * (1f - gatherEase) * wobble;
                float angle = seed + gatherEase * 4.2f;
                float px = center.x + Mathf.Cos(angle) * radius;
                float py = center.y + Mathf.Sin(angle) * radius;
                float twinkle = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(i * 1.7f + time * 9f));
                float alpha = Mathf.Clamp01(1f - gatherEase) * twinkle * appear;
                float size = Mathf.Lerp(6.5f, 1.2f, gatherEase);
                Color color = i % 6 == 0
                    ? new Color(0.78f, 0.96f, 1f, alpha * 0.9f)
                    : new Color(0.55f, 0.63f, 0.66f, alpha * 0.75f);
                Widgets.DrawBoxSolid(new Rect(px, py, size, size), color);
            }

            // 汇聚形成的灰潮核心
            if (t < 0.75f)
            {
                float coreAlpha = Mathf.Clamp01(1f - t / 0.75f) * 0.55f;
                float coreSize = Mathf.Lerp(3f, 16f, gatherEase) * (0.85f + 0.15f * pulse);
                Widgets.DrawBoxSolid(
                    new Rect(center.x - coreSize * 0.5f, center.y - coreSize * 0.5f, coreSize, coreSize),
                    new Color(0.72f, 0.95f, 1f, coreAlpha));
            }

            if (icon != null)
            {
                float iconSize = Mathf.Min(rect.width, rect.height) * Mathf.Lerp(0.46f, 0.56f, reveal);
                Rect iconRect = new Rect(
                    center.x - iconSize * 0.5f,
                    center.y - iconSize * 0.5f,
                    iconSize,
                    iconSize);

                // 产物外围残留的灰潮粒子
                if (reveal > 0f)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        float angle = i * (Mathf.PI * 2f / 16f) + time * 0.7f;
                        float radius = iconSize * (0.52f + 0.04f * Mathf.Sin(i * 2.1f + time * 4f));
                        float px = center.x + Mathf.Cos(angle) * radius;
                        float py = center.y + Mathf.Sin(angle) * radius;
                        float alpha = iconAlpha * (0.25f + 0.35f * Mathf.Abs(Mathf.Sin(i * 1.9f + time * 6f)));
                        Widgets.DrawBoxSolid(new Rect(px, py, 2f, 2f),
                            new Color(0.72f, 0.92f, 1f, alpha));
                    }
                }

                // 产物从核心中逐渐显现，而不是直接凭空出现
                // 显现初期带一点灰潮的冷灰色，随扫描线推进恢复产物本色
                Color old = GUI.color;
                GUI.color = Color.Lerp(
                    new Color(0.55f, 0.62f, 0.64f, iconAlpha),
                    new Color(1f, 1f, 1f, iconAlpha),
                    reveal);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                GUI.color = old;

                // 材质化扫描线：随显现进度从下往上扫过产物
                if (reveal > 0f && reveal < 1f)
                {
                    float lineY = iconRect.y + iconRect.height * (1f - reveal);
                    Widgets.DrawBoxSolid(
                        new Rect(iconRect.x, lineY - 1f, iconRect.width, 2f),
                        new Color(0.72f, 0.95f, 1f, 0.75f * (1f - reveal)));
                }
            }

            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, 24f), def.LabelCap.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static int AdjustCount(int current, int delta)
        {
            long next = (long)current + delta;
            if (next < Building_NaniteTransmuter.MinTaskCount)
            {
                return Building_NaniteTransmuter.MinTaskCount;
            }

            return next > int.MaxValue ? int.MaxValue : (int)next;
        }

        private static List<ThingDef> AllProducts
        {
            get
            {
                if (allProducts != null)
                {
                    return allProducts;
                }

                allProducts = new List<ThingDef>();
                foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs)
                {
                    if (Building_NaniteTransmuter.HasMetalOrStoneStuffCategory(thingDef))
                    {
                        allProducts.Add(thingDef);
                    }
                }

                allProducts.Sort((left, right) =>
                    string.Compare(left.LabelCap.RawText, right.LabelCap.RawText, StringComparison.OrdinalIgnoreCase));
                return allProducts;
            }
        }

        private static string FormatTicks(long ticks)
        {
            if (ticks <= 0L)
            {
                return "0";
            }

            int clamped = (int)Math.Min((long)int.MaxValue, ticks);
            return clamped.ToStringTicksToPeriod();
        }
    }
}
