using System;
using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.Hediffs;
using SD.GrayRace.Utilities;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.ITabs
{
    public class ITab_GrayRaceOverlock : ITab
    {
        private Pawn _cachedPawn;

        private Dictionary<BodyPartRecord, float> _draftValues = new Dictionary<BodyPartRecord, float>();

        private List<BodyPartRecord> _overclockableParts = new List<BodyPartRecord>();
        private BodyPartRecord _selectedPart;

        private Vector2 _scrollPosition = Vector2.zero;

        private static readonly Vector2 s_winSize = new Vector2(600f, 480f);
        private const float PanelWidth = 630f;
        private const float PanelHeight = 480f;
        private const float LeftPanelWidth = 200f;
        private const float Margin = 10f;
        private const float RowHeight = 30f;

        public ITab_GrayRaceOverlock()
        {
            size = new Vector2(PanelWidth, PanelHeight);
            labelKey = "超频"; // "GR_TabOverclock";
        }

        protected override void FillTab()
        {
            Pawn selPawn = SelPawn;
            if (selPawn != _cachedPawn)
            {
                _cachedPawn = selPawn;
                ResetDraftData(selPawn);
            }
            // 2. 准备绘图区域 (ITab 的坐标原点 (0,0) 在左上角)
            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(Margin);

            // 3. 绘制标题
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(rect.x, rect.y, rect.width, 35f);
            Widgets.Label(titleRect, "超频控制面板"); // "Gray_Overclock_Title".Translate()
            Text.Font = GameFont.Small;

            // 4. 布局分割
            // 顶部留出标题高度
            float topOffset = 40f;
            // 底部留出按钮高度
            float bottomHeight = 40f;

            Rect contentRect = new Rect(rect.x, rect.y + topOffset, rect.width, rect.height - topOffset);

            // 左侧面板区域
            Rect leftRect = new Rect(contentRect.x, contentRect.y, LeftPanelWidth, contentRect.height - bottomHeight);

            // 右侧面板区域
            Rect rightRect = new Rect(leftRect.xMax + Margin, contentRect.y, contentRect.width - LeftPanelWidth - Margin, contentRect.height - bottomHeight);

            // 绘制中间分割线
            Widgets.DrawLineVertical(leftRect.xMax + 5f, leftRect.y, leftRect.height);

            // 5. 执行具体绘制
            DrawPartList(leftRect, selPawn);
            DrawControlPanel(rightRect, selPawn);

        }

        public override bool IsVisible => base.IsVisible && SelPawn.IsGrayRace() && !SelPawn.Dead;

        private void ResetDraftData(Pawn pawn)
        {
            // Log.Message($"ResetDraftData - BodyPart Count:{pawn.RaceProps.body.AllParts.Count}");
            _draftValues.Clear();
            _overclockableParts.Clear();
            _selectedPart = null;

            CompOverclock comp = pawn.GetComp<CompOverclock>();
            if (comp == null) return;

            // 扫描所有部位
            foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
            {
                // Log.Message($"ResetDraftData - Find bodyPart: {part.LabelCap} Is Overclockable? {OverclockUtility.IsOverclockable(part)}");
                if (OverclockUtility.IsOverclockable(part))
                {
                    _overclockableParts.Add(part);
                    // 读取当前的真实超频值
                    float currentLevel = comp.GetOverclockLevel(part);
                    if (currentLevel > 0f) _draftValues[part] = currentLevel;
                }
            }

            // 默认选中第一个
            if (_overclockableParts.Any())
                _selectedPart = _overclockableParts[0];
        }

        private void ApplyChanges(Pawn pawn)
        {
            CompOverclock comp = pawn.GetComp<CompOverclock>();
            if (comp == null) return;

            // 会更新所有部件
            foreach (var part in _overclockableParts)
            {
                float val = _draftValues.TryGetValue(part, out float v) ? v : 0f;
                comp.SetOverclockLevel(part, val);
            }

            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        // 主绘制逻辑
        private void DrawPartList(Rect rect, Pawn pawn)
        {
            Widgets.DrawMenuSection(rect);

            // 计算滚动视图内容高度
            Rect viewRect = new Rect(0, 0, rect.width - 16f, _overclockableParts.Count * RowHeight);
            Widgets.BeginScrollView(rect, ref _scrollPosition, viewRect);

            float y = 0f;
            foreach (var part in _overclockableParts)
            {
                Rect rowRect = new Rect(0, y, viewRect.width, RowHeight - 2f);

                float level = _draftValues.TryGetValue(part, out float val) ? val : 0f;
                bool isCore = OverclockUtility.IsCorePart(part);

                // 鼠标移动高亮
                if (_selectedPart == part) Widgets.DrawHighlightSelected(rowRect);
                else if (Mouse.IsOver(rowRect)) Widgets.DrawHighlight(rowRect);

                // 左侧的按钮点击
                if (Widgets.ButtonInvisible(rowRect))
                {
                    _selectedPart = part;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }

                // 准备显示文本
                string prefix = level > 0f ? "●" : "○";
                string suffix = isCore ? "⚡" : "";
                string label = $"{prefix} {part.LabelCap} {level:P0} {suffix}";

                Color labelColor = Color.white;
                if (isCore) labelColor = Color.yellow; // 核心是黄色
                if (level > 0f) labelColor = Color.cyan; // 已超频是青色

                Rect textRect = rowRect;
                textRect.xMin += 5f;

                GUI.color = labelColor;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(textRect, label);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;

                y += RowHeight;
            }

            Widgets.EndScrollView();
        }

        private void DrawControlPanel(Rect rect, Pawn pawn)
        {
            if (_selectedPart == null) return;

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            // 1. 选中部位信息
            listing.Label($"<b>当前部件:</b> {_selectedPart.LabelCap}"); // "Gray_Overclock_SelectedPart".Translate()
            listing.Label($"<b>效果:</b> {OverclockUtility.GetAffectedStatLabel(_selectedPart)}"); // {"Gray_Overclock_AffectedStat".Translate()}
            listing.GapLine();

            float currentVal = _draftValues.TryGetValue(_selectedPart, out float v) ? v : 0f;
            listing.Label($"超频级别: {currentVal:P0}"); // {"Gray_Overclock_Level".Translate()}

            Rect barRect = listing.GetRect(24f);
            // 绘制一个深色底，再填充
            Widgets.DrawBoxSolid(barRect, new Color(0.1f, 0.1f, 0.1f));
            Widgets.FillableBar(barRect, currentVal, SolidColorMaterials.NewSolidColorTexture(new Color(0.3f, 0.6f, 1f)));

            float newVal = Widgets.HorizontalSlider(barRect, currentVal, 0f, 1f, true);
            if (Mathf.Abs(newVal - currentVal) > 0.001f)
            {
                _draftValues[_selectedPart] = newVal;
            }
            listing.Gap(5f);

            // 3. 那堆 xx% 的方便按钮
            Rect btnRect = listing.GetRect(30f);
            float btnWidth = btnRect.width / 5f;
            for (int i = 0; i <= 4; i++)
            {
                float pct = i * 0.25f;
                Rect bRect = new Rect(btnRect.x + i * btnWidth, btnRect.y, btnWidth - 4f, 30f);
                if (Widgets.ButtonText(bRect, $"{pct:P0}"))
                {
                    _draftValues[_selectedPart] = pct;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }
            listing.GapLine();

            // 4. 效果预览
            listing.Label("效果预览"); // "Gray_Overclock_Preview".Translate()

            float statBonus = OverclockUtility.GetStatBonus(_selectedPart, newVal);
            float energyCost = OverclockUtility.GetEnergyCost(_selectedPart, newVal);
            string affectedStat = OverclockUtility.GetAffectedStatLabel(_selectedPart);

            string energyText = $" • 能量消耗: +{energyCost:P0}"; // {"Gray_Overclock_EnergyCost".Translate()}
            if (OverclockUtility.IsCorePart(_selectedPart))
                energyText += $" (核心部件消耗翻倍)"; // {"Gray_Overclock_CorePartDouble".Translate()}

            listing.Label($" • {affectedStat}: +{statBonus:P0}".Colorize(ColorLibrary.Green));
            listing.Label(energyText.Colorize(ColorLibrary.Orange));

            listing.GapLine();

            // 5. 总
            listing.Label("总体状态"); // "Gray_Overclock_TotalStatus".Translate()
            float totalEnergy = 0f;
            float regenPenalty = 0f;

            foreach (var kvp in _draftValues)
            {
                totalEnergy += OverclockUtility.GetEnergyCost(kvp.Key, kvp.Value);
                regenPenalty += OverclockUtility.GetEnergyCost(kvp.Key, kvp.Value);
            }

            listing.Label($" • 总能量消耗: +{totalEnergy:P0}".Colorize(ColorLibrary.Orange)); // {"Gray_Overclock_TotalEnergyRate".Translate()}
            listing.Label($" • 纳米机械: -{regenPenalty:P0}".Colorize(ColorLibrary.RedReadable)); // {"Gray_Overclock_NaniteRegen".Translate()}

            listing.Gap(15f);
            Rect actionRect = listing.GetRect(40f);

            if (Widgets.ButtonText(new Rect(actionRect.x, actionRect.y, 140f, 40f), "更新应用")) // "Gray_Overclock_Apply".Translate()
            {
                ApplyChanges(pawn);
            }

            if (Widgets.ButtonText(new Rect(actionRect.x + 150f, actionRect.y, 100f, 40f), "重置")) // "Gray_Overclock_Reset".Translate()
            {
                ResetDraftData(pawn);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            listing.End();
        }
    }
}
