using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Text;
using RimWorld;
using SD.GrayRace.Comps;
using UnityEngine;
using Verse;

namespace SD.GrayRace.UISet.Gizmos
{
    [StaticConstructorOnStartup]
    public class Gizmo_NaniteResourcesNew : Gizmo_Slider
    {
        protected CompResource_NanitesNew resource;
        protected override float Width => 300f;
        protected override float Target { get; set; }

        private static readonly Texture2D s_naniteCostTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.2f));
        private static readonly Texture2D s_barTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.34f, 0.42f, 0.43f));
        private static readonly Texture2D s_emptyBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.03f, 0.035f, 0.05f));

        private static readonly Texture2D s_hover = ContentFinder<Texture2D>.Get("UI/Gizmo/Hover");
        private static readonly Texture2D s_normal = ContentFinder<Texture2D>.Get("UI/Gizmo/Normal");

        public Gizmo_NaniteResourcesNew(CompResource_NanitesNew resource)
        {
            this.resource = resource;
        }

        protected override Color BarColor => new ColorInt(120, 150, 130).ToColor;
        protected override Color BarHighlightColor => new ColorInt(160, 190, 170).ToColor;

        protected override string BarLabel => $"{resource.CurrentNanites:F0} / {resource.Max:F0}";

        protected override bool IsDraggable => false;

        protected override float ValuePercent => resource.CurrentNanitesPercent;

        protected override string Title
        {
            get
            {
                string label = resource.Props.resourceLabel;
                StringBuilder text = new StringBuilder(label.CapitalizeFirst());

                if (Find.Selector.SelectedPawns.Count != 1)
                    text.Append($" ({resource.Pawn.LabelShort})");

                return text.ToString();
            }
        }

        protected override bool DraggingBar { get; set; }

        protected override string GetTooltip()
        {
            StringBuilder sb = new StringBuilder();

            string label = resource.Props.resourceLabel;
            sb.Append($"{label.Colorize(ColoredText.TipSectionTitleColor)}: {resource.CurrentNanites:F1} / {resource.Max:F0}\n");

            float regenRate = resource.Pawn.GetStatValue(GrayRaceDefOf.GRStat_NaniteRegenRate);

            if (!resource.CanRegenNanites)
            {
                if (resource.CurrentNanites >= resource.Max)
                    sb.Append("达到上限".Translate().Colorize(Color.gray));
                else
                    sb.Append("能量归零".Translate().Colorize(ColorLibrary.RedReadable));
            }
            else if (regenRate > 0f)
            {
                sb.Append($"每秒回复: +{regenRate:F1}/s".Colorize(ColorLibrary.Green));
            }
            else if (regenRate <= 0f)
            {
                sb.Append($"每秒消耗: -{regenRate:F1}/s".Colorize(ColorLibrary.RedReadable));
            }

            return sb.ToString();
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            GizmoResult gizmoResult = new GizmoResult(GizmoState.Clear);
            bool mouseOverElement = false;
            Rect baseRect = new Rect(topLeft.x, topLeft.y, Width, 75f);
            Rect innerRect = baseRect.ContractedBy(2f);
            if (Mouse.IsOver(baseRect))
                GrayRaceUtilities.DrawWindowBackgroundWithTexture(baseRect, s_hover);
            else
                GrayRaceUtilities.DrawWindowBackgroundWithTexture(baseRect, s_normal);

            Text.Font = GameFont.Small;
            Rect textRect = innerRect;

            textRect.height = Text.LineHeight;
            DrawHeader(textRect, ref mouseOverElement);

            barRect = innerRect.ContractedBy(22f);

            Widgets.FillableBar(barRect, ValuePercent, s_barTex, s_emptyBarTex, true);

            foreach (float barThreshold in GetBarThresholds())
            {
                GUI.DrawTexture(
                    new Rect
                    {
                        x = barRect.x + 3f + (barRect.width - 6f) * barThreshold,
                        y = barRect.y + barRect.height - 5f,
                        width = 2f,
                        height = 4f
                    },
                    (ValuePercent < barThreshold) ? BaseContent.GreyTex : BaseContent.BlackTex
                );
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(barRect, BarLabel);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            float timePulse = Mathf.Repeat(Time.time, 0.85f);
            float alphaMultiplier = 1f;

            if (timePulse < 0.1f)
            {
                alphaMultiplier = timePulse / 0.1f;
            }
            else if (timePulse >= 0.25f)
            {
                alphaMultiplier = 1f - (timePulse - 0.25f) / 0.6f;
            }

            if (MapGizmoUtility.LastMouseOverGizmo is Command_Ability commandAbility && resource.Max > 0f)
            {
                foreach (var effectComp in commandAbility.Ability.EffectComps)
                {
                    if (effectComp is not CompAbilityEffect_NanitesCost compAbilityEffectNanitesCost)
                    {
                        continue;
                    }

                    var props = compAbilityEffectNanitesCost.Props;

                    if (props.nanitesCost < float.Epsilon) continue;

                    var rect = barRect.ContractedBy(3f);
                    float barWidth = rect.width;
                    float currentPercent = resource.CurrentNanites / resource.Max;

                    rect.xMax = rect.xMin + barWidth * currentPercent;

                    float costPercent = Mathf.Min(props.nanitesCost / resource.Max, 1f);
                    rect.xMin = Mathf.Max(rect.xMin, rect.xMax - barWidth * costPercent);

                    GUI.color = new Color(1f, 1f, 1f, alphaMultiplier * 0.7f);
                    GenUI.DrawTextureWithMaterial(rect, s_naniteCostTex, null);
                    GUI.color = Color.white;
                    return gizmoResult;
                }
            }

            if (Mouse.IsOver(barRect))
            {
                Widgets.DrawHighlight(barRect);
                TooltipHandler.TipRegion(barRect, GetTooltip, 828267373);
            }

            return gizmoResult;
        }

        protected override void DrawHeader(Rect headerRect, ref bool mouseOverElement)
        {
            string text = Title;
            text = text.Truncate(headerRect.width);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(headerRect, text);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        protected override IEnumerable<float> GetBarThresholds()
        {
            if (resource.Props.resourceGizmoThresholds != null)
            {
                foreach (float thresholds in resource.Props.resourceGizmoThresholds)
                {
                    yield return thresholds;
                }
            }
        }
    }
}
