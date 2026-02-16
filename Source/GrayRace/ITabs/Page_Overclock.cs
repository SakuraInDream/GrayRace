using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.Modules;
using SD.GrayRace.Utilities;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.ITabs;

// 超频页面
public class Page_Overclock : BasePageITab
{
    private List<BodyPartRecord>  _overclockableParts = new List<BodyPartRecord>();
    private Dictionary<BodyPartRecord, float> _draftValues = new Dictionary<BodyPartRecord, float>();
    private BodyPartRecord _selectedPart;

    private OverclockModule Comp => pawn.GetManager().overclockModule;

    public override string PageLabel => "超频".Translate(); // 待本地化

    public override void OnPawnChanged()
    {
        _draftValues.Clear();
        _overclockableParts.Clear();
        _selectedPart = null;

        if (Comp == null) return;

        foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
        {
            if (OverclockUtility.IsOverclockable(part))
            {
                _overclockableParts.Add(part);
                float currentLevel = Comp.GetOverclockLevel(part);
                if (currentLevel > 0f) _draftValues[part] = currentLevel;
            }
        }

        SetDefaultSelection();
    }

    public override void DrawPartList(Rect rect)
    {
        DrawPartListGeneric(
            rect,
            _overclockableParts,
            _selectedPart,
            (part) => { _selectedPart = part; },
            GetPartLabel,
            GetPartLabelColor
        );
    }

    public override void DrawDetailPanel(Rect rect)
    {
        if (_selectedPart == null) return;

        Listing_Standard listing = new Listing_Standard();
        listing.Begin(rect);

        // 部位信息
        listing.Label($"<b>当前部件:</b> {_selectedPart.LabelCap}");
        listing.Label($"<b>效果:</b> {OverclockUtility.GetAffectedStatLabel(_selectedPart)}");
        listing.GapLine();

        // 超频级别控制
        float maxLevel = Comp.GetMaxOverclockLevel(_selectedPart);
        float currentVal = _draftValues.TryGetValue(_selectedPart, out float v) ? Mathf.Clamp(v, 0f, maxLevel) : 0f;
        _draftValues[_selectedPart] = currentVal;

        bool capBoosted = maxLevel > Comp.BaseOverclockCap + 0.001f;
        string capText = capBoosted ? "已由属性修正提升上限" : "当前无上限增幅";

        listing.Label($"超频级别: {currentVal:P0} / {maxLevel:P0}");
        listing.Label($"上限状态: {capText}（基础: {Comp.BaseOverclockCap:P0}）");

        Rect barRect = listing.GetRect(24f);
        Widgets.DrawBoxSolid(barRect, new Color(0.1f, 0.1f, 0.1f));
        float barPercent = maxLevel > 0f ? currentVal / maxLevel : 0f;
        Widgets.FillableBar(barRect, barPercent, SolidColorMaterials.NewSolidColorTexture(new Color(0.3f, 0.6f, 1f)));

        float newVal = Widgets.HorizontalSlider(barRect, currentVal, 0f, maxLevel, true);
        if (Mathf.Abs(newVal - currentVal) > 0.001f)
        {
            _draftValues[_selectedPart] = newVal;
        }
        listing.Gap(5f);

        // 快速设置按钮
        Rect btnRect = listing.GetRect(30f);
        float btnWidth = btnRect.width / 5f;
        for (int i = 0; i <= 4; i++)
        {
            float pct = maxLevel * (i * 0.25f);
            Rect bRect = new Rect(btnRect.x + i * btnWidth, btnRect.y, btnWidth - 4f, 30f);
            if (Widgets.ButtonText(bRect, $"{pct:P0}"))
            {
                _draftValues[_selectedPart] = pct;
            }
        }
        listing.GapLine();

        // 效果预览
        listing.Label("效果预览");
        float statBonus = OverclockUtility.GetStatBonus(_selectedPart, newVal);
        string affectedStat = OverclockUtility.GetAffectedStatLabel(_selectedPart);

        listing.Label($" • {affectedStat}: +{statBonus:P0}".Colorize(ColorLibrary.Green));
        listing.GapLine();

        // 总体状态
        listing.Label("总体状态");
        float totalEnergy = 0f;
        foreach (var kvp in _draftValues)
        {
            float clamped = Mathf.Clamp(kvp.Value, 0f, Comp.GetMaxOverclockLevel(kvp.Key));
            totalEnergy += OverclockUtility.GetEnergyCost(kvp.Key, clamped);
        }
        listing.Label($" • 总能量消耗: +{totalEnergy:P0}".Colorize(ColorLibrary.Orange));

        listing.Gap(15f);

        // 操作按钮
        Rect actionRect = listing.GetRect(40f);
        float buttonWidth = 100f;
        float spacing = 10f;

        if (Widgets.ButtonText(new Rect(actionRect.x, actionRect.y, buttonWidth, 40f), "应用"))
        {
            ApplyChanges();
            SoundDefOf.Click.PlayOneShotOnCamera();
        }
        if (Widgets.ButtonText(new Rect(actionRect.x + buttonWidth + spacing, actionRect.y, buttonWidth, 40f), "放弃当前更改"))
        {
            OnPawnChanged();
            SoundDefOf.Click.PlayOneShotOnCamera();
        }
        if (Widgets.ButtonText(new Rect(actionRect.x + (buttonWidth + spacing) * 2, actionRect.y, buttonWidth, 40f), "重置"))
        {
            foreach (BodyPartRecord part in _overclockableParts)
            {
                _draftValues[part] = 0f;
            }
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        listing.End();
    }

    public override string GetPageTitle()
    {
        return "超频控制";
    }

    private void SetDefaultSelection()
    {
        if (_selectedPart == null || !_overclockableParts.Contains(_selectedPart))
        {
            _selectedPart = _overclockableParts.FirstOrDefault();
        }
    }

    private string GetPartLabel(BodyPartRecord part)
    {
        float level = _draftValues.TryGetValue(part, out float val) ? val : 0f;
        bool isCore = OverclockUtility.IsCorePart(part);
        string prefix = level > 0f ? "●" : "○";
        string suffix = isCore ? "⚡" : "";
        return $"{prefix} {part.LabelCap} {level:P0} {suffix}";
    }

    private Color GetPartLabelColor(BodyPartRecord part)
    {
        float level = _draftValues.TryGetValue(part, out float val) ? val : 0f;
        bool isCore = OverclockUtility.IsCorePart(part);

        if (isCore) return Color.yellow;
        if (level > 0f) return Color.cyan;
        return Color.white;
    }

    private void ApplyChanges()
    {
        if (Comp == null) return;

        foreach (var part in _overclockableParts)
        {
            float maxLevel = Comp.GetMaxOverclockLevel(part);
            float val = _draftValues.TryGetValue(part, out float v) ? Mathf.Clamp(v, 0f, maxLevel) : 0f;
            Comp.SetOverclockLevel(part, val);
        }
    }
}
