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
    /// 纳米重塑台 UI：
    /// 左侧选择地图上的金属材料，中间输入数量和预估产出，右侧任务队列。
    /// </summary>
    public class ITab_NaniteReconfigurator : ITab
    {
        private static readonly Vector2 TabSize = new Vector2(1000f, 660f);
        private const float Margin = 8f;
        private const float TopBarHeight = 70f;
        private const float ColumnGap = 10f;
        private const float SideColumnWidth = 290f;
        private const float PreviewHeight = 300f;

        private static List<ThingDef> allMaterials;

        private Building_NaniteReconfigurator lastBuilding;
        private ThingDef selectedMaterial;
        private int selectedCount = Building_NaniteReconfigurator.MinTaskCount;
        private string countBuffer = Building_NaniteReconfigurator.MinTaskCount.ToString();
        private string searchQuery = string.Empty;

        private Vector2 materialScroll;
        private float materialScrollHeight;
        private Vector2 queueScroll;
        private float queueScrollHeight;

        private ThingDef animatedPreviewMaterial;
        private float animatedPreviewStart;

        public ITab_NaniteReconfigurator()
        {
            size = TabSize;
            labelKey = "GR_NaniteReconfiguratorTab";
        }

        private Building_NaniteReconfigurator SelBuilding => SelThing as Building_NaniteReconfigurator;

        public override bool IsVisible => base.IsVisible && SelBuilding != null;

        protected override void FillTab()
        {
            Building_NaniteReconfigurator building = SelBuilding;
            if (building == null)
            {
                return;
            }

            if (building != lastBuilding)
            {
                lastBuilding = building;
                selectedMaterial = null;
                selectedCount = Building_NaniteReconfigurator.MinTaskCount;
                countBuffer = selectedCount.ToString();
                searchQuery = string.Empty;
                materialScroll = Vector2.zero;
                queueScroll = Vector2.zero;
                animatedPreviewMaterial = null;
                animatedPreviewStart = 0f;
            }

            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(Margin);
            DrawTopBar(new Rect(rect.x, rect.y, rect.width, TopBarHeight), building);

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

            DrawMaterialColumn(leftRect, building);
            DrawPlanningColumn(middleRect, building);
            DrawQueueColumn(rightRect, building);
        }

        private void DrawTopBar(Rect rect, Building_NaniteReconfigurator building)
        {
            Widgets.DrawMenuSection(rect);

            Widgets.Label(new Rect(rect.x + 8f, rect.y + 8f, 220f, 24f),
                "GR_NaniteReconfigurator_TopTitle".Translate().ToString());
            Widgets.Label(new Rect(rect.x + 240f, rect.y + 8f, 150f, 24f),
                "GR_NaniteReconfigurator_QueuedTasks".Translate(building.Tasks.Count).ToString());
            Widgets.Label(new Rect(rect.x + 400f, rect.y + 8f, 300f, 24f),
                "GR_NaniteReconfigurator_QueuedOutput".Translate(building.QueuedOutput).ToString());
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 38f, 620f, 24f),
                "GR_NaniteReconfigurator_RateHint".Translate().ToString());

            if (DebugSettings.godMode)
            {
                DrawDebugButtons(rect, building);
            }
        }

        /// <summary>仅在 God mode 下显示的调试按钮。</summary>
        private static void DrawDebugButtons(Rect rect, Building_NaniteReconfigurator building)
        {
            const float buttonHeight = 26f;
            const float gap = 4f;
            const float buttonWidth = 80f;
            float x = rect.xMax - 4f - (buttonWidth * 2f + gap);
            float y = rect.y + 6f;

            Rect completeCurrentRect = new Rect(x, y, buttonWidth, buttonHeight);
            Rect completeAllRect = new Rect(x + buttonWidth + gap, y, buttonWidth, buttonHeight);

            bool hasTasks = building.Tasks.Count > 0;
            string completeCurrentTip = hasTasks
                ? "GR_NaniteReconfigurator_DebugCompleteCurrentTip".Translate().ToString()
                : "GR_NaniteReconfigurator_NoTask".Translate().ToString();
            string completeAllTip = hasTasks
                ? "GR_NaniteReconfigurator_DebugCompleteAllTip".Translate().ToString()
                : "GR_NaniteReconfigurator_NoTask".Translate().ToString();

            TooltipHandler.TipRegion(completeCurrentRect, completeCurrentTip);
            TooltipHandler.TipRegion(completeAllRect, completeAllTip);

            Color oldColor = GUI.color;
            if (!hasTasks)
            {
                GUI.color = Color.gray;
            }

            if (Widgets.ButtonText(completeCurrentRect,
                    "GR_NaniteReconfigurator_DebugCompleteCurrentButton".Translate().ToString())
                && hasTasks)
            {
                building.DebugCompleteCurrentTask();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            if (Widgets.ButtonText(completeAllRect,
                    "GR_NaniteReconfigurator_DebugCompleteAllButton".Translate().ToString())
                && hasTasks)
            {
                building.DebugCompleteAllTasks();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            GUI.color = oldColor;
        }

        private void DrawMaterialColumn(Rect rect, Building_NaniteReconfigurator building)
        {
            Widgets.DrawMenuSection(rect);

            Widgets.Label(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 112f, 24f),
                "GR_NaniteReconfigurator_MaterialList".Translate().ToString());

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(rect.x + rect.width - 106f, rect.y + 6f, 100f, 24f),
                "GR_NaniteReconfigurator_MapCountHeader".Translate().ToString());
            Text.Anchor = TextAnchor.UpperLeft;

            searchQuery = Widgets.TextField(
                new Rect(rect.x + 6f, rect.y + 32f, rect.width - 12f, 26f),
                searchQuery ?? string.Empty);

            Rect scrollOuter = new Rect(rect.x + 4f, rect.y + 64f, rect.width - 8f, rect.height - 68f);
            Rect viewRect = new Rect(0f, 0f, scrollOuter.width - 16f, materialScrollHeight);
            Widgets.BeginScrollView(scrollOuter, ref materialScroll, viewRect);

            float curY = 0f;
            string query = searchQuery?.Trim().ToLowerInvariant();
            List<ThingDef> materials = AllMaterials;
            for (int i = 0; i < materials.Count; i++)
            {
                ThingDef thingDef = materials[i];
                if (!building.CanDecompose(thingDef))
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

                DrawMaterialRow(ref curY, viewRect.width, building, thingDef);
            }

            if (Event.current.type == EventType.Layout)
            {
                materialScrollHeight = curY + 10f;
            }

            Widgets.EndScrollView();
        }

        private void DrawMaterialRow(
            ref float y,
            float width,
            Building_NaniteReconfigurator building,
            ThingDef thingDef)
        {
            Rect row = new Rect(0f, y, width, 28f);
            if (thingDef == selectedMaterial)
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
            Widgets.Label(new Rect(32f, y, width - 112f, 28f), thingDef.LabelCap.Truncate(width - 112f));
            Text.WordWrap = true;

            int mapCount = building.CountMaterialOnMap(thingDef);
            Color oldColor = GUI.color;
            if (mapCount < Building_NaniteReconfigurator.MinTaskCount)
            {
                GUI.color = Color.gray;
            }

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(width - 78f, y, 72f, 28f), mapCount.ToString());
            GUI.color = oldColor;
            Text.Anchor = TextAnchor.UpperLeft;

            string tip = thingDef.description;
            string countTip = "GR_NaniteReconfigurator_MapCountTip"
                .Translate(mapCount)
                .ToString();
            tip = string.IsNullOrEmpty(tip) ? countTip : tip + "\n\n" + countTip;
            TooltipHandler.TipRegion(row, tip);

            if (GUI.Button(row, string.Empty, Widgets.EmptyStyle))
            {
                selectedMaterial = thingDef;
                selectedCount = Building_NaniteReconfigurator.MinTaskCount;
                countBuffer = selectedCount.ToString();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            y += 28f;
        }

        private void DrawPlanningColumn(Rect rect, Building_NaniteReconfigurator building)
        {
            Rect previewRect = new Rect(rect.x, rect.y, rect.width, PreviewHeight);
            DrawPreview(previewRect, selectedMaterial);

            Rect planRect = new Rect(
                rect.x,
                previewRect.yMax + ColumnGap,
                rect.width,
                rect.yMax - previewRect.yMax - ColumnGap);
            Widgets.DrawMenuSection(planRect);

            if (selectedMaterial == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(planRect, "GR_NaniteReconfigurator_PreviewHint".Translate().ToString());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            float x = planRect.x + 10f;
            float y = planRect.y + 8f;
            float width = planRect.width - 20f;

            Widgets.Label(new Rect(x, y, width, 24f),
                "GR_NaniteReconfigurator_SelectedMaterial".Translate(selectedMaterial.LabelCap).ToString());
            y += 28f;

            Widgets.Label(new Rect(x, y, width, 22f),
                "GR_NaniteReconfigurator_Quantity".Translate().ToString());
            y += 24f;

            if (Widgets.ButtonText(new Rect(x, y, 34f, 28f), "-"))
            {
                selectedCount = AdjustCount(selectedCount, -100 * GenUI.CurrentAdjustmentMultiplier());
                countBuffer = selectedCount.ToString();
            }

            countBuffer = Widgets.TextField(new Rect(x + 38f, y, 110f, 28f), countBuffer);
            if (int.TryParse(countBuffer, out int parsed))
            {
                selectedCount = Mathf.Max(Building_NaniteReconfigurator.MinTaskCount, parsed);
            }

            if (Widgets.ButtonText(new Rect(x + 152f, y, 34f, 28f), "+"))
            {
                selectedCount = AdjustCount(selectedCount, 100 * GenUI.CurrentAdjustmentMultiplier());
                countBuffer = selectedCount.ToString();
            }

            Widgets.Label(new Rect(x + 194f, y + 4f, width - 194f, 24f),
                "GR_NaniteReconfigurator_StepHint".Translate().ToString());
            y += 38f;

            long ticks = building.GetTaskWorkTicks(selectedCount);
            int output = building.GetOutputNanites(selectedMaterial, selectedCount);
            int available = building.CountMaterialOnMap(selectedMaterial);
            bool enough = available >= selectedCount;

            Widgets.Label(new Rect(x, y, width, 24f),
                "GR_NaniteReconfigurator_EstimatedTime".Translate(FormatTicks(ticks)).ToString());
            y += 24f;
            Widgets.Label(new Rect(x, y, width, 24f),
                "GR_NaniteReconfigurator_EstimatedYield".Translate(output).ToString());
            y += 24f;
            Widgets.Label(new Rect(x, y, width, 24f),
                "GR_NaniteReconfigurator_MapAvailable".Translate(available).ToString());
            y += 24f;

            if (!enough)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 0.45f, 0.45f);
                Widgets.Label(new Rect(x, y, width, 24f),
                    "GR_NaniteReconfigurator_NotEnoughMaterial".Translate().ToString());
                GUI.color = old;
            }
            y += 28f;

            Color oldColor = GUI.color;
            if (!enough)
            {
                GUI.color = Color.gray;
            }

            if (Widgets.ButtonText(new Rect(x, y, width, 32f),
                    "GR_NaniteReconfigurator_AddToQueue".Translate().ToString()))
            {
                if (building.TryEnqueueTask(selectedMaterial, selectedCount, out string reason))
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

        private void DrawQueueColumn(Rect rect, Building_NaniteReconfigurator building)
        {
            Widgets.DrawMenuSection(rect);

            Widgets.Label(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, 24f),
                "GR_NaniteReconfigurator_Queue".Translate().ToString());

            Rect scrollOuter = new Rect(rect.x + 4f, rect.y + 34f, rect.width - 8f, rect.height - 38f);
            Rect viewRect = new Rect(0f, 0f, scrollOuter.width - 16f, queueScrollHeight);
            Widgets.BeginScrollView(scrollOuter, ref queueScroll, viewRect);

            float curY = 0f;
            NaniteReconfiguratorTask taskToCancel = null;

            if (building.Tasks.Count == 0)
            {
                Widgets.Label(new Rect(0f, 0f, viewRect.width, 24f),
                    "GR_NaniteReconfigurator_NoTask".Translate().ToString());
            }
            else
            {
                for (int i = 0; i < building.Tasks.Count; i++)
                {
                    NaniteReconfiguratorTask task = building.Tasks[i];
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
            NaniteReconfiguratorTask task,
            ref NaniteReconfiguratorTask taskToCancel)
        {
            Rect row = new Rect(0f, y, width, 84f);
            Widgets.DrawHighlightSelected(row);

            if (task.material != null && task.material.DrawMatSingle?.mainTexture != null)
            {
                Widgets.ThingIcon(new Rect(4f, y + 4f, 32f, 32f), task.material);
            }

            string materialLabel = task.material != null
                ? task.material.LabelCap.ToString()
                : "GR_NaniteReconfigurator_None".Translate().ToString();
            Widgets.Label(
                new Rect(42f, y + 4f, width - 100f, 24f),
                "GR_NaniteReconfigurator_ActiveTask".Translate(materialLabel, task.count, task.outputNanites)
                    .ToString()
                    .Truncate(width - 100f));

            Widgets.Label(
                new Rect(42f, y + 26f, width - 100f, 20f),
                "GR_NaniteReconfigurator_Output".Translate(task.outputNanites).ToString());

            Rect bar = new Rect(4f, y + 50f, width - 62f, 12f);
            Widgets.DrawBoxSolid(bar, new Color(0.14f, 0.14f, 0.14f, 1f));
            Widgets.DrawBoxSolid(
                new Rect(bar.x, bar.y, bar.width * task.ProgressPercent, bar.height),
                new Color(0.3f, 0.72f, 0.3f, 1f));

            Widgets.Label(
                new Rect(4f, y + 62f, width - 62f, 20f),
                task.ProgressPercent.ToStringPercent() + " (" + FormatTicks(task.RemainingTicks) + ")");

            if (Widgets.ButtonText(new Rect(width - 54f, y + 4f, 50f, 24f),
                    "GR_NaniteReconfigurator_Cancel".Translate().ToString()))
            {
                taskToCancel = task;
            }

            y += 84f;
        }

        private void DrawQueuedTaskRow(
            ref float y,
            float width,
            NaniteReconfiguratorTask task,
            int index,
            ref NaniteReconfiguratorTask taskToCancel)
        {
            Rect row = new Rect(0f, y, width, 42f);
            Widgets.DrawHighlightIfMouseover(row);

            if (task.material != null && task.material.DrawMatSingle?.mainTexture != null)
            {
                Widgets.ThingIcon(new Rect(4f, y + 5f, 28f, 28f), task.material);
            }

            Widgets.Label(
                new Rect(36f, y + 3f, width - 92f, 20f),
                "GR_NaniteReconfigurator_QueuedTask"
                    .Translate(index, task.material?.LabelCap.ToString() ?? "?", task.count, task.outputNanites)
                    .ToString()
                    .Truncate(width - 92f));

            if (Widgets.ButtonText(new Rect(width - 54f, y + 8f, 50f, 24f),
                    "GR_NaniteReconfigurator_Cancel".Translate().ToString()))
            {
                taskToCancel = task;
            }

            y += 42f;
        }

        private void DrawPreview(Rect rect, ThingDef material)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.05f, 0.06f, 0.07f, 0.94f));
            Widgets.DrawBox(rect, 1);

            if (material == null)
            {
                animatedPreviewMaterial = null;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "GR_NaniteReconfigurator_PreviewHint".Translate().ToString());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            Texture materialIcon = material.uiIcon;
            if (materialIcon == null && material.DrawMatSingle != null)
            {
                materialIcon = material.DrawMatSingle.mainTexture;
            }

            ThingDef naniteDef = GrayRaceDefOf.GR_Nanites;
            Texture naniteIcon = naniteDef?.uiIcon;
            if (naniteIcon == null && naniteDef?.DrawMatSingle != null)
            {
                naniteIcon = naniteDef.DrawMatSingle.mainTexture;
            }

            const float Cycle = 6f;
            float now = Time.realtimeSinceStartup;
            if (material != animatedPreviewMaterial)
            {
                animatedPreviewMaterial = material;
                animatedPreviewStart = now;
            }

            float time = now - animatedPreviewStart;
            float t = (time % Cycle) / Cycle;
            float fadeOut = Mathf.Clamp01((t - 0.92f) / 0.08f);
            float materialAlpha = Mathf.Clamp01(1f - t / 0.55f) * (1f - fadeOut);
            float naniteAlpha = Mathf.Clamp01((t - 0.42f) / 0.38f) * (1f - fadeOut);
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 2.2f);

            Vector2 center = rect.center;
            float iconSize = Mathf.Min(rect.width * 0.24f, rect.height * 0.40f);
            Vector2 leftCenter = new Vector2(rect.x + rect.width * 0.30f, center.y + 6f);
            Vector2 rightCenter = new Vector2(rect.x + rect.width * 0.70f, center.y + 6f);
            Rect leftRect = new Rect(
                leftCenter.x - iconSize * 0.5f,
                leftCenter.y - iconSize * 0.5f,
                iconSize,
                iconSize);
            Rect rightRect = new Rect(
                rightCenter.x - iconSize * 0.5f,
                rightCenter.y - iconSize * 0.5f,
                iconSize,
                iconSize);

            Widgets.DrawLine(leftCenter, rightCenter,
                new Color(0.3f, 0.7f, 0.72f, 0.18f), 2f);

            for (int i = 0; i < 36; i++)
            {
                float p = Mathf.Repeat(t * 1.6f + i / 36f, 1f);
                float x = Mathf.Lerp(leftCenter.x, rightCenter.x, p);
                float y = Mathf.Lerp(leftCenter.y, rightCenter.y, p)
                    + Mathf.Sin(p * Mathf.PI * 2f + i * 1.7f) * 16f * Mathf.Sin(p * Mathf.PI);
                float alpha = Mathf.Sin(p * Mathf.PI)
                    * (0.25f + 0.55f * Mathf.Abs(Mathf.Sin(i * 1.9f + time * 7f)));
                float size = Mathf.Lerp(5.5f, 1.5f, p);
                Color color = i % 5 == 0
                    ? new Color(0.75f, 0.95f, 1f, alpha)
                    : new Color(0.55f, 0.63f, 0.66f, alpha * 0.8f);
                Widgets.DrawBoxSolid(new Rect(x, y, size, size), color);
            }

            if (t < 0.70f)
            {
                float coreAlpha = Mathf.Clamp01(1f - t / 0.70f) * 0.45f;
                float coreSize = Mathf.Lerp(4f, 18f, Mathf.Clamp01(t / 0.55f))
                    * (0.85f + 0.15f * pulse);
                Widgets.DrawBoxSolid(
                    new Rect(center.x - coreSize * 0.5f, center.y + 6f - coreSize * 0.5f, coreSize, coreSize),
                    new Color(0.7f, 0.95f, 1f, coreAlpha));
            }

            if (materialIcon != null && materialAlpha > 0.01f)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, materialAlpha);
                GUI.DrawTexture(leftRect, materialIcon, ScaleMode.ScaleToFit, true);
                GUI.color = old;
            }

            if (naniteIcon != null && naniteAlpha > 0.01f)
            {
                Color old = GUI.color;
                GUI.color = Color.Lerp(
                    new Color(0.55f, 0.62f, 0.64f, naniteAlpha),
                    new Color(1f, 1f, 1f, naniteAlpha),
                    naniteAlpha);
                GUI.DrawTexture(rightRect, naniteIcon, ScaleMode.ScaleToFit, true);
                GUI.color = old;

                if (naniteAlpha < 1f)
                {
                    float lineY = rightRect.y + rightRect.height * (1f - naniteAlpha);
                    Widgets.DrawBoxSolid(
                        new Rect(rightRect.x, lineY - 1f, rightRect.width, 2f),
                        new Color(0.72f, 0.95f, 1f, 0.7f * (1f - naniteAlpha)));
                }
            }

            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width * 0.5f - 16f, 24f),
                material.LabelCap.ToString());

            string outputLabel = naniteDef != null
                ? naniteDef.LabelCap.ToString()
                : "GR_NaniteReconfigurator_None".Translate().ToString();
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(rect.center.x, rect.y + 6f, rect.width * 0.5f - 8f, 24f), outputLabel);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static int AdjustCount(int current, int delta)
        {
            long next = (long)current + delta;
            if (next < Building_NaniteReconfigurator.MinTaskCount)
            {
                return Building_NaniteReconfigurator.MinTaskCount;
            }

            return next > int.MaxValue ? int.MaxValue : (int)next;
        }

        private static List<ThingDef> AllMaterials
        {
            get
            {
                if (allMaterials != null)
                {
                    return allMaterials;
                }

                allMaterials = new List<ThingDef>();
                foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs)
                {
                    if (Building_NaniteReconfigurator.HasMetallicStuffCategory(thingDef))
                    {
                        allMaterials.Add(thingDef);
                    }
                }

                allMaterials.Sort((left, right) =>
                    string.Compare(left.LabelCap.RawText, right.LabelCap.RawText, StringComparison.OrdinalIgnoreCase));
                return allMaterials;
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
