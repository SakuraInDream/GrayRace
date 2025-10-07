using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using SD.GrayRace.JobGivers;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SD.GrayRace.Needs
{
    public class Need_GrayRaceEnergy: Need
    {
        public bool StopSeekingMetal = false;
        public Need_GrayRaceEnergy(Pawn pawn) : base(pawn)
        {
        }

        public override void NeedInterval()
        {
            if (!IsFrozen)
            {
                CurLevel -= 0.002f;
            }
        }

        public override string GetTipString()
        {
            StringBuilder sb = new StringBuilder(base.GetTipString());
            sb.AppendInNewLine($"{CurLevel:F2}/{MaxLevel:F2}");
            sb.AppendInNewLine($"自动寻找金属:{StopSeekingMetal}");

            return sb.ToString();
        }

        public override void DrawOnGUI(Rect rect, int maxThresholdMarkers = 2147483647, float customMargin = -1, bool drawArrows = true, bool doTooltip = true, Rect? rectForTooltip = null, bool drawLabel = true)
        {
            threshPercents ??= new List<float>();

            threshPercents.Clear();
            threshPercents.Add(0.8f);
            threshPercents.Add(0.5f);
            threshPercents.Add(0.2f);
            base.DrawOnGUI(rect, maxThresholdMarkers, customMargin, drawArrows, doTooltip, rectForTooltip, drawLabel);

            // 在需求条右上角添加一个小按钮，控制当缺乏纳米机械时是否自动搜寻金属物品
            // 实验性功能 随时弃用
            float buttonSize = rect.height / 2f;
            Rect buttonRect = new Rect(rect.xMax - buttonSize, rect.y, buttonSize, buttonSize);
            if(Widgets.ButtonImage(buttonRect, ContentFinder<Texture2D>.Get("Things/Item/Resource/Steel")))
            {
                StopSeekingMetal = !StopSeekingMetal;
            }

            if (Mouse.IsOver(buttonRect))
            {
                Widgets.DrawHighlight(buttonRect);
            }
        }

        public override int GUIChangeArrow
        {
            get
            {
                if (IsFrozen) return 0;

                return -1;
            }
        }
    }
}
