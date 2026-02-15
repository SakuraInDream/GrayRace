using System;
using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.Defs;
using SD.GrayRace.Hediffs;
using SD.GrayRace.Utilities;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.ITabs
{
    public class ITab_GrayRaceOverlock : ITab
    {
        // 切换为超频/升级模式
        private enum Mode
        {
            Overclock,
            Upgrade
        }
        private Mode _currentMode = Mode.Overclock;
        private Pawn _cachedPawn;

        private List<BodyPartRecord> _overclockableParts = new List<BodyPartRecord>();
        private Dictionary<BodyPartRecord, float> _draftValues = new Dictionary<BodyPartRecord, float>();

        private List<BodyPartRecord> _upgradeableParts = new List<BodyPartRecord>();
        private List<GRUpgradeDef> _availableUpgradesForSelectedPart = new List<GRUpgradeDef>();

        private BodyPartRecord _selectedPart;
        private Vector2 _scrollPosition = Vector2.zero;

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

            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(Margin);

            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            DrawModeSwitcher(headerRect);


            float topOffset = 40f;
            Rect contentRect = new Rect(rect.x, rect.y + topOffset, rect.width, rect.height - topOffset);

            // 左侧面板区域
            Rect leftRect = new Rect(contentRect.x, contentRect.y, LeftPanelWidth, contentRect.height);
            // 右侧详情
            Rect rightRect = new Rect(leftRect.xMax + Margin, contentRect.y, contentRect.width - LeftPanelWidth - Margin, contentRect.height);

            // 分割线
            Widgets.DrawLineVertical(leftRect.xMax + 5f, leftRect.y, leftRect.height);

            // 3. 绘制具体内容
            DrawPartList(leftRect, selPawn); // 左侧列表

            // 右侧根据模式切换
            if (_currentMode == Mode.Overclock)
            {
                DrawControlPanel(rightRect, selPawn);
            }
            else
            {
                DrawUpgradePanel(rightRect, selPawn);
            }

        }

        public override bool IsVisible => base.IsVisible && SelPawn.IsGrayRace() && !SelPawn.Dead;

        private void ResetDraftData(Pawn pawn)
        {
            _draftValues.Clear();
            _overclockableParts.Clear();
            _upgradeableParts.Clear();
            _selectedPart = null;

            CompOverclock comp = pawn.GetComp<CompOverclock>();
            if (comp == null) return;

            // 遍历所有部位进行分类
            foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
            {
                // 1. 填充超频列表 (逻辑不变)
                if (OverclockUtility.IsOverclockable(part))
                {
                    _overclockableParts.Add(part);
                    float currentLevel = comp.GetOverclockLevel(part);
                    if (currentLevel > 0f) _draftValues[part] = currentLevel;
                }

                // 2. 填充升级列表 (新逻辑：过滤手指脚趾)
                if (IsPartDisplayableForUpgrade(part))
                {
                    _upgradeableParts.Add(part);
                }
            }

            // 初始化选中项
            SetDefaultSelection();
        }

        private bool IsPartDisplayableForUpgrade(BodyPartRecord part)
        {
            // 排除手指
            if (part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbDigit)) return false;
            // 排除脚趾
            if (part.def.tags.Contains(BodyPartTagDefOf.MovingLimbDigit)) return false;

            // 排除其他过于琐碎的部件 (如果需要)
            // 比如只显示有一定血量的部位，或者是核心器官/外部肢体
            if (part.def.defName.ToLower().Contains("rib")) return false; // 排除肋骨
            if (part.def.defName.ToLower().Contains("clavicle")) return false; // 排除锁骨

            // 必须保留的内容：
            // 手 (Hand), 脚 (Foot), 舌头 (Tongue), 眼 (Eye), 耳 (Ear), 鼻 (Nose)
            // 以及原本的大脑、心脏、手臂、腿等

            // 简单规则：不显示“内部”且“覆盖率极低”的部件，除非它是核心
            // 但为了保险起见，只要排除了手指脚趾，剩下的通常都是值得升级的
            return true;
        }

        private void SetDefaultSelection()
        {
            var list = _currentMode == Mode.Overclock ? _overclockableParts : _upgradeableParts;

            // 如果当前选中的部件不在新列表里，重置选中
            if (_selectedPart == null || !list.Contains(_selectedPart))
            {
                if (list.Any())
                {
                    _selectedPart = list[0];
                    RefreshAvailableUpgrades();
                }
                else
                {
                    _selectedPart = null;
                }
            }
        }

        private void RefreshAvailableUpgrades()
        {
            _availableUpgradesForSelectedPart.Clear();
            if (_selectedPart == null) return;

            var allUpgrades = DefDatabase<GRUpgradeDef>.AllDefsListForReading;
            foreach (var def in allUpgrades)
            {
                if (def.targetBodyPart == _selectedPart.def)
                {
                    _availableUpgradesForSelectedPart.Add(def);
                }
            }
        }

        private void DrawModeSwitcher(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float btnWidth = 120f;
            Rect btn1 = new Rect(0, 0, btnWidth, 28f);
            Rect btn2 = new Rect(btnWidth + 10f, 0, btnWidth, 28f);

            if (_currentMode == Mode.Overclock) GUI.color = Color.white; else GUI.color = Color.gray;
            if (Widgets.ButtonText(btn1, "超频控制"))
            {
                _currentMode = Mode.Overclock;
                SetDefaultSelection(); // 切换模式时重置选中
                SoundDefOf.Click.PlayOneShotOnCamera();
                labelKey = "超频";
            }

            if (_currentMode == Mode.Upgrade) GUI.color = Color.white; else GUI.color = Color.gray;
            if (Widgets.ButtonText(btn2, "机体升级"))
            {
                _currentMode = Mode.Upgrade;
                SetDefaultSelection(); // 切换模式时重置选中
                RefreshAvailableUpgrades();
                SoundDefOf.Click.PlayOneShotOnCamera();
                labelKey = "升级";
            }

            GUI.color = Color.white;
            Widgets.EndGroup();
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
        private void DrawPartList_old(Rect rect)
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
         private void DrawPartList(Rect rect, Pawn pawn)
        {
            Widgets.DrawMenuSection(rect);

            // 根据模式选择要显示的列表
            List<BodyPartRecord> listToShow = _currentMode == Mode.Overclock ? _overclockableParts : _upgradeableParts;

            Rect viewRect = new Rect(0, 0, rect.width - 16f, listToShow.Count * RowHeight);
            Widgets.BeginScrollView(rect, ref _scrollPosition, viewRect);

            var upgradeComp = pawn.GetComp<CompUpgrades>();

            float y = 0f;
            foreach (var part in listToShow)
            {
                Rect rowRect = new Rect(0, y, viewRect.width, RowHeight - 2f);

                if (_selectedPart == part) Widgets.DrawHighlightSelected(rowRect);
                else if (Mouse.IsOver(rowRect)) Widgets.DrawHighlight(rowRect);

                if (Widgets.ButtonInvisible(rowRect))
                {
                    _selectedPart = part;
                    RefreshAvailableUpgrades();
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }

                string label = "";
                Color labelColor = Color.white;

                if (_currentMode == Mode.Overclock)
                {
                    float level = _draftValues.TryGetValue(part, out float val) ? val : 0f;
                    bool isCore = OverclockUtility.IsCorePart(part);
                    string prefix = level > 0f ? "●" : "○";
                    string suffix = isCore ? "⚡" : "";
                    label = $"{prefix} {part.LabelCap} {level:P0} {suffix}";

                    if (isCore) labelColor = Color.yellow;
                    if (level > 0f) labelColor = Color.cyan;
                }
                else if (_currentMode == Mode.Upgrade)
                {
                    bool hasActiveUpgrade = false;
                    if (upgradeComp != null)
                    {
                        foreach (var def in DefDatabase<GRUpgradeDef>.AllDefsListForReading)
                        {
                            if (def.targetBodyPart == part.def && upgradeComp.IsUpgradeActive(def, part))
                            {
                                hasActiveUpgrade = true;
                                break;
                            }
                        }
                    }

                    string prefix = hasActiveUpgrade ? "★" : "○";
                    label = $"{prefix} {part.LabelCap}";
                    if (hasActiveUpgrade) labelColor = Color.green;
                }

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

        private void DrawControlPanel_Old(Rect rect, Pawn pawn)
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
            // float regenPenalty = 0f;

            foreach (var kvp in _draftValues)
            {
                totalEnergy += OverclockUtility.GetEnergyCost(kvp.Key, kvp.Value);
                // regenPenalty += OverclockUtility.GetEnergyCost(kvp.Key, kvp.Value);
            }

            listing.Label($" • 总能量消耗: +{totalEnergy:P0}".Colorize(ColorLibrary.Orange)); // {"Gray_Overclock_TotalEnergyRate".Translate()}
            // listing.Label($" • 纳米机械: -{regenPenalty:P0}".Colorize(ColorLibrary.RedReadable)); // {"Gray_Overclock_NaniteRegen".Translate()}

            listing.Gap(15f);
            Rect actionRect = listing.GetRect(40f);

            if (Widgets.ButtonText(new Rect(actionRect.x, actionRect.y, 100f, 40f), "更新应用")) // "Gray_Overclock_Apply".Translate()
            {
                ApplyChanges(pawn);
            }

            if (Widgets.ButtonText(new Rect(actionRect.x + 100f + 10f, actionRect.y, 100f, 40f), "放弃所有更改")) // "Gray_Overclock_Reset".Translate()
            {
                ResetDraftData(pawn);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            if (Widgets.ButtonText(new Rect(actionRect.x + (100f + 10f) * 2, actionRect.y, 100f, 40f), "全部重置"))
            {
                foreach (BodyPartRecord part in _overclockableParts)
                {
                    _draftValues[part] = 0f;
                }
                SoundDefOf.Click.PlayOneShotOnCamera();
            }


            listing.End();
        }
        private void DrawControlPanel(Rect rect, Pawn pawn)
        {
            if (_selectedPart == null) return;

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label($"<b>当前部件:</b> {_selectedPart.LabelCap}");
            listing.Label($"<b>效果:</b> {OverclockUtility.GetAffectedStatLabel(_selectedPart)}");
            listing.GapLine();

            float currentVal = _draftValues.TryGetValue(_selectedPart, out float v) ? v : 0f;
            listing.Label($"超频级别: {currentVal:P0}");

            Rect barRect = listing.GetRect(24f);
            Widgets.DrawBoxSolid(barRect, new Color(0.1f, 0.1f, 0.1f));
            Widgets.FillableBar(barRect, currentVal, SolidColorMaterials.NewSolidColorTexture(new Color(0.3f, 0.6f, 1f)));

            float newVal = Widgets.HorizontalSlider(barRect, currentVal, 0f, 1f, true);
            if (Mathf.Abs(newVal - currentVal) > 0.001f)
            {
                _draftValues[_selectedPart] = newVal;
            }
            listing.Gap(5f);

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

            // 预览区域
            listing.Label("效果预览");
            float statBonus = OverclockUtility.GetStatBonus(_selectedPart, newVal);
            string affectedStat = OverclockUtility.GetAffectedStatLabel(_selectedPart);


            listing.Label($" • {affectedStat}: +{statBonus:P0}".Colorize(ColorLibrary.Green));

            listing.GapLine();
            listing.Gap(15f);

            // 底部操作按钮
            Rect actionRect = listing.GetRect(40f);
            float buttonWidth = 100f;
            float spacing = 10f;

            if (Widgets.ButtonText(new Rect(actionRect.x, actionRect.y, buttonWidth, 40f), "应用"))
            {
                ApplyChanges(pawn);
            }
            if (Widgets.ButtonText(new Rect(actionRect.x + buttonWidth + spacing, actionRect.y, buttonWidth, 40f), "放弃当前更改"))
            {
                ResetDraftData(pawn);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            if (Widgets.ButtonText(new Rect(actionRect.x + (buttonWidth + spacing) * 2, actionRect.y, buttonWidth, 40f), "重置"))
            {
                foreach (BodyPartRecord part in _overclockableParts) _draftValues[part] = 0f;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            listing.End();
        }

        private void DrawUpgradePanel(Rect rect, Pawn pawn)
        {
            if (_selectedPart == null) return;

            CompUpgrades upgradeComp = pawn.GetComp<CompUpgrades>();
            if (upgradeComp == null) return;

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label($"<b>当前部位:</b> {_selectedPart.LabelCap}");
            listing.GapLine();

            if (_availableUpgradesForSelectedPart.Count == 0)
            {
                listing.Label("该部位无可用的变形或插件方案。".Colorize(Color.gray));
            }
            else
            {
                Rect scrollOutRect = new Rect(0, listing.CurHeight, rect.width, rect.height - listing.CurHeight);
                Rect scrollViewRect = new Rect(0, 0, rect.width - 16f, _availableUpgradesForSelectedPart.Count * 60f);

                foreach (var upgradeDef in _availableUpgradesForSelectedPart)
                {
                    DrawUpgradeRow(listing, upgradeComp, upgradeDef);
                }
            }

            listing.End();
        }

        private void DrawUpgradeRow(Listing_Standard listing, CompUpgrades comp, GRUpgradeDef def)
        {
            // 没想好
        }
    }
}
