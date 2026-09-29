using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.JobGivers;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace SD.GrayRace.Needs
{
    public class Need_GrayRaceEnergy: Need
    {
        public bool stopSeekingMetal = true;
        public Need_GrayRaceEnergy(Pawn pawn) : base(pawn)
        {
        }

        // 调一下能量上限，避免后期超频太高天天吃饭不停下
        public override float MaxLevel => 25f;

        public override void NeedInterval()
        {
            if (!IsFrozen)
            {
                float fallPerTick = def.fallPerDay / 60000f * 150f;
                var overclockComp = pawn.GetManager().overclockModule; // pawn.GetComp<CompOverclock>();
                if (overclockComp != null)
                {
                    // 假设 CachedEnergyConsumptionFactor 是 0.5 (代表增加 50% 消耗)
                    // 最终消耗 = 基础 * (1 + 额外系数)
                    fallPerTick *= (1f + overclockComp.CachedEnergyConsumptionFactor);
                }
                CurLevel -= fallPerTick;
            }
        }

        public override string GetTipString()
        {
            StringBuilder sb = new StringBuilder(base.GetTipString());
            sb.AppendInNewLine($"{CurLevel:F2}/{MaxLevel:F2}");
            float fallPerDay = def.fallPerDay;
            float extraFactor = 0f;
            var overclockComp = pawn.GetManager().overclockModule; // pawn.GetComp<CompOverclock>();
            if (overclockComp != null)
            {
                extraFactor = overclockComp.CachedEnergyConsumptionFactor;
                fallPerDay *= (1f + extraFactor);
            }

            if (fallPerDay > 0.01f)
            {
                sb.AppendInNewLine($"能量消耗 {-fallPerDay:F2}/天 | {(fallPerDay/60000):F4}/tick (超频: {extraFactor:P0})".Colorize(ColorLibrary.RedReadable));
            }

            sb.AppendInNewLine($"自动寻找金属:{stopSeekingMetal}");

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
            float buttonSize = 24f; // rect.height / 2f;
            Rect buttonRect = new Rect(rect.xMax - buttonSize, rect.center.y, buttonSize, buttonSize);
            if(Widgets.ButtonImage(buttonRect, ThingDefOf.Steel.uiIcon))
            {
                stopSeekingMetal = !stopSeekingMetal;
                if (stopSeekingMetal)
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                }
                else
                {
                    SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                }
            }
            GUI.DrawTexture(new Rect(buttonRect.center.x, buttonRect.y, buttonRect.width/2f, buttonRect.height/2f), stopSeekingMetal ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);

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
