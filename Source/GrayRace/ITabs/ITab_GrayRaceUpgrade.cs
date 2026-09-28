using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SD.GrayRace.Utilities;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.ITabs;

public class ITab_GrayRaceUpgrade : ITab
{
    private Pawn _cachedPawn;
    private List<BasePageITab> _pages = new List<BasePageITab>()
    {
        new Page_Overclock(),
        new Page_Upgrade()
    };
    private int _currentPageIndex = 0;

    private const float PanelWidth = 630f;
    private const float PanelHeight = 480f;
    private const float Margin = 10f;

    public ITab_GrayRaceUpgrade()
    {
        size = new Vector2(PanelWidth, PanelHeight);
        labelKey = _pages.FirstOrDefault()?.PageLabel;
    }

    protected override void FillTab()
    {
        if (SelPawn != _cachedPawn)
        {
            _cachedPawn = SelPawn;
            foreach (var page in _pages)
            {
                page.Initialize(SelPawn);
            }
        }

        Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(Margin);

        Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
        DrawModeSwitcher(headerRect);

        float topOffset = 40f;
        Rect contentRect = new Rect(rect.x, rect.y + topOffset, rect.width, rect.height - topOffset);

        Rect leftRect = new Rect(contentRect.x, contentRect.y, BasePageITab.LeftPanelWidth, contentRect.height);
        Rect rightRect = new Rect(leftRect.xMax + Margin, contentRect.y, contentRect.width - BasePageITab.LeftPanelWidth - Margin, contentRect.height);

        Widgets.DrawLineVertical(leftRect.xMax + 5f, leftRect.y, leftRect.height);

        // 绘制当前页面内容
        _pages[_currentPageIndex].DrawPartList(leftRect);
        _pages[_currentPageIndex].DrawDetailPanel(rightRect);
    }

    public override bool IsVisible => base.IsVisible && SelPawn.IsGrayRace() && !SelPawn.Dead && SelPawn.IsPlayerControlled;

    private void DrawModeSwitcher(Rect rect)
    {
        Widgets.BeginGroup(rect);

        for (int i = 0; i < _pages.Count; i++)
        {
            Rect btnRect = new Rect(i * (120f + 10f), 0, 120f, 28f);
            BasePageITab page = _pages[i];

            if (i == _currentPageIndex)
            {
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = Color.gray;
            }

            if (Widgets.ButtonText(btnRect, page.PageLabel))
            {
                SwitchToPage(i);
            }
        }

        GUI.color = Color.white;
        Widgets.EndGroup();
    }

    private void SwitchToPage(int index)
    {
        if (_currentPageIndex == index) return;

        _currentPageIndex = index;

        labelKey = _pages[_currentPageIndex].PageLabel;
        SoundDefOf.Click.PlayOneShotOnCamera();
    }
}
