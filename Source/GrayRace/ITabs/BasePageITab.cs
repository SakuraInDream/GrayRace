using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;


// 此处代码托管 AI 生成
public abstract class BasePageITab
{
    public abstract string PageLabel { get; }
    protected Pawn pawn;
    protected Vector2 scrollPosition = Vector2.zero;

    public const float LeftPanelWidth = 200f;
    public const float Margin = 10f;
    public const float RowHeight = 30f;

    public virtual void Initialize(Pawn vPawn)
    {
        this.pawn = vPawn;
        OnPawnChanged();
    }

    /// <summary>
    /// 重置页面数据，在切换角色时调用
    /// </summary>
    public abstract void OnPawnChanged();

    /// <summary>
    /// 绘制左侧部位列表
    /// </summary>
    public abstract void DrawPartList(Rect rect);

    /// <summary>
    /// 绘制右侧详情面板
    /// </summary>
    public abstract void DrawDetailPanel(Rect rect);

    /// <summary>
    /// 获取页面标题
    /// </summary>
    public abstract string GetPageTitle();

    /// <summary>
    /// 绘制通用的部位列表UI
    /// </summary>
    protected void DrawPartListGeneric(Rect rect, List<BodyPartRecord> parts, BodyPartRecord selectedPart, Action<BodyPartRecord> onSelectPart, Func<BodyPartRecord, string> getLabel, Func<BodyPartRecord, Color> getLabelColor)
    {
        Widgets.DrawMenuSection(rect);

        Rect viewRect = new Rect(0, 0, rect.width - 16f, parts.Count * RowHeight);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

        float y = 0f;
        foreach (var part in parts)
        {
            Rect rowRect = new Rect(0, y, viewRect.width, RowHeight - 2f);

            if (selectedPart == part) Widgets.DrawHighlightSelected(rowRect);
            else if (Mouse.IsOver(rowRect)) Widgets.DrawHighlight(rowRect);

            if (Widgets.ButtonInvisible(rowRect))
            {
                onSelectPart?.Invoke(part);
            }

            string label = getLabel?.Invoke(part) ?? part.LabelCap;
            Color labelColor = getLabelColor?.Invoke(part) ?? Color.white;

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

    /// <summary>
    /// 获取可显示的身体部位列表（过滤掉手指脚趾等琐碎部位）
    /// </summary>
    protected List<BodyPartRecord> GetDisplayableBodyParts()
    {
        List<BodyPartRecord> result = new List<BodyPartRecord>();

        foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
        {
            if (IsPartDisplayable(part))
            {
                result.Add(part);
            }
        }

        return result;
    }

    /// <summary>
    /// 判断部位是否应该在UI中显示
    /// </summary>
    private bool IsPartDisplayable(BodyPartRecord part)
    {
        // 排除手指
        if (part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbDigit)) return false;
        // 脚趾
        if (part.def.tags.Contains(BodyPartTagDefOf.MovingLimbDigit)) return false;
        // 肋骨
        if (part.def.defName.ToLower().Contains("rib")) return false;
        // 锁骨
        if (part.def.defName.ToLower().Contains("clavicle")) return false;

        return true;
    }
}
