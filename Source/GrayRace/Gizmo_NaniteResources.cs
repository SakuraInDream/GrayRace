using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace SD.GrayRace
{
    [StaticConstructorOnStartup]
    public class Gizmo_NaniteResources: Gizmo_Slider
    {
        // 仿造 Gene_Resource
        protected CompResource_Nanites resource;

        // 正在移动滑条
        private static bool draggingBar;
        
        private static readonly Texture2D NaniteCostTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.2f));

        public Gizmo_NaniteResources(CompResource_Nanites resource)
        {
            this.resource = resource;
        }

        protected override Color BarColor => new ColorInt(120, 150, 130).ToColor;
        
        protected override Color BarHighlightColor => new ColorInt(160, 190, 170).ToColor;
        
        // 资源状态显示
        protected override string BarLabel => $"{resource.ValueForDisplay}/{resource.MaxForDisplay}";
        
        // 是否可移动滑条
        protected override bool IsDraggable => resource.Pawn.IsColonistPlayerControlled || resource.Pawn.IsPrisonerOfColony;

        protected override int Increments => resource.MaxForDisplay / 10;

        protected override float ValuePercent => resource.ValuePercent;

        protected override FloatRange DragRange => new FloatRange(0f, 1f);
        
        protected override float Target
        {
            get => resource.TargetValue / resource.Max;
            set => resource.TargetValue = value * resource.Props.maxResource;
        }

        protected override string Title
        {
            get
            {
                StringBuilder text = new StringBuilder(resource.ResourceLabel.CapitalizeFirst());
                
                if (Find.Selector.SelectedPawns.Count != 1)
                    text.Append($" ({resource.Pawn.LabelShort})");
                
                return text.ToString();
            }
        }

        protected override bool DraggingBar
        {
            get => draggingBar;
            set => draggingBar = value;
        }
        protected override string GetTooltip()
        {
            return $"{resource.ResourceLabel.Colorize(ColoredText.TipSectionTitleColor)}: {resource.ValueForDisplay} / {resource.MaxForDisplay}";
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            GizmoResult gizmoResult = base.GizmoOnGUI(topLeft, maxWidth, parms);
            Rect baserect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            
            var num = Mathf.Repeat(Time.time, 0.85f);
            var num2 = 1f;
            if (num < 0.1f)
            {
                num2 = num / 0.1f;
            }
            else if (num >= 0.25f)
            {
                num2 = 1f - (num - 0.25f) / 0.6f;
            }
            
            // 预览消耗多少资源
            if (MapGizmoUtility.LastMouseOverGizmo is Command_Ability command_Ability && resource.Max >= 0f)
            {
                foreach (var effectComp in command_Ability.Ability.EffectComps)
                {
                    if (!(effectComp is CompAbilityEffect_NanitesCost compAbilityEffectNanitesCost))
                    {
                        continue;
                    }
                    
                    var props = compAbilityEffectNanitesCost.Props;
                    
                    if(props.nanitesCost < float.Epsilon) continue;
            
                    var rect = barRect.ContractedBy(3f);
                    var width = rect.width;
                    var num3 = resource.Value / resource.Max;
                    rect.xMax = rect.xMin + width * num3;
                    
                    var num4 = Mathf.Min(props.nanitesCost / resource.Max, 1f);
                    rect.xMin = Mathf.Max(rect.xMin, rect.xMax - width * num4);
                    
                    GUI.color = new Color(1f, 1f, 1f, num2 * 0.7f);
                    GenUI.DrawTextureWithMaterial(rect, NaniteCostTex, null);
                    GUI.color = Color.white;
                    return gizmoResult;
                }
            }
            
            // 悬浮提示
            if (Mouse.IsOver(baserect))
            {
                TooltipHandler.TipRegion(baserect, GetTooltip());
            }
            return gizmoResult;
        }

        protected override IEnumerable<float> GetBarThresholds()
        {
            for (int i = 0; i < resource.Props.resourceGizmoThresholds.Count; i++)
            {
                yield return resource.Props.resourceGizmoThresholds[i];
            }
        }
    }
}
