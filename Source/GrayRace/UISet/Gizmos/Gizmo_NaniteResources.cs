// using System.Collections.Generic;
// using System.Text;
// using RimWorld;
// using SD.GrayRace.Comps;
// using UnityEngine;
// using Verse;
//
// namespace SD.GrayRace.UISet.Gizmos
// {
//     [StaticConstructorOnStartup]
//     public class Gizmo_NaniteResources: Gizmo_Slider
//     {
//         // 仿造 Gene_Resource
//         protected CompResource_Nanites resource;
//
//         protected override float Width => 300f;
//
//         // 正在移动滑条
//         private static bool s_draggingBar;
//
//         private static readonly Texture2D s_naniteCostTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.2f));
//         private static readonly Texture2D s_barTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.34f, 0.42f, 0.43f));
//         private static readonly Texture2D s_barHighlightTex =  SolidColorMaterials.NewSolidColorTexture(new Color(0.43f, 0.54f, 0.55f));
//         private static readonly Texture2D s_emptyBarTex =  SolidColorMaterials.NewSolidColorTexture(new Color(0.03f, 0.035f, 0.05f));
//         // private static readonly Texture2D DragBarTex =  SolidColorMaterials.NewSolidColorTexture(new Color(0.74f, 0.97f, 0.8f));
//
//         private static readonly Texture2D s_click = ContentFinder<Texture2D>.Get("UI/Gizmo/Click", true);
//         private static readonly Texture2D s_hover = ContentFinder<Texture2D>.Get("UI/Gizmo/Hover", true);
//         private static readonly Texture2D s_normal = ContentFinder<Texture2D>.Get("UI/Gizmo/Normal", true);
//
//         public Gizmo_NaniteResources(CompResource_Nanites resource)
//         {
//             this.resource = resource;
//         }
//
//         protected override Color BarColor => new ColorInt(120, 150, 130).ToColor;
//
//         protected override Color BarHighlightColor => new ColorInt(160, 190, 170).ToColor;
//
//         // 资源状态显示
//         protected override string BarLabel => $"{resource.ValueForDisplay}/{resource.MaxForDisplay}";
//
//         // 是否可移动滑条
//         protected override bool IsDraggable => false; // resource.Pawn.IsColonistPlayerControlled || resource.Pawn.IsPrisonerOfColony;
//
//         protected override int Increments => resource.MaxForDisplay / 10;
//
//         protected override float ValuePercent => resource.ValuePercent;
//
//         protected override FloatRange DragRange => new FloatRange(0f, 1f);
//
//         // 无用
//         protected override float Target
//         {
//             get; // => resource.TargetValue / resource.Max;
//             set;// => resource.TargetValue = value * resource.Props.maxResource;
//         }
//
//         protected override string Title
//         {
//             get
//             {
//                 StringBuilder text = new StringBuilder(resource.ResourceLabel.CapitalizeFirst());
//
//                 if (Find.Selector.SelectedPawns.Count != 1)
//                     text.Append($" ({resource.Pawn.LabelShort})");
//
//                 return text.ToString();
//             }
//         }
//
//         protected override bool DraggingBar
//         {
//             get => s_draggingBar;
//             set => s_draggingBar = value;
//         }
//         protected override string GetTooltip()
//         {
//             StringBuilder sb = new StringBuilder();
//             sb.Append($"{resource.ResourceLabel.Colorize(ColoredText.TipSectionTitleColor)}: {resource.ValueForDisplay} / {resource.MaxForDisplay}");
//             if (resource.RegenPerSecond > 0f)
//             {
//                 sb.Append($"(+{resource.RegenPerSecond * 100}/s)");
//             }
//             return sb.ToString();
//         }
//
//         public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
//         {
//             GizmoResult gizmoResult = new GizmoResult(GizmoState.Clear);
//             bool mouseOverElement = false;
//             Rect baseRect = new Rect(topLeft.x, topLeft.y, Width, 75f);
//             Rect innerRect = baseRect.ContractedBy(2f);
//             if (Mouse.IsOver(baseRect))
//             {
//                 GrayRaceUtilities.DrawWindowBackgroundWithTexture(baseRect, s_hover);
//             }
//             else
//             {
//                 GrayRaceUtilities.DrawWindowBackgroundWithTexture(baseRect, s_normal);
//             }
//
//             Text.Font = GameFont.Small;
//             Rect textRect = innerRect;
//
//             textRect.height = Text.LineHeight;
//             DrawHeader(textRect, ref mouseOverElement);
//
//             barRect = innerRect.ContractedBy(22f);
//
//             Widgets.FillableBar(barRect, ValuePercent, s_barTex, s_emptyBarTex, true);
//
//             foreach (float barThreshold in GetBarThresholds())
//             {
//                 GUI.DrawTexture(
//                     new Rect
//                     {
//                         x = barRect.x + 3f + (barRect.width - 6f) * barThreshold,
//                         y = barRect.y + barRect.height - 5f,
//                         width = 2f,
//                         height = 4f
//                     },
//                     (ValuePercent < barThreshold) ? BaseContent.GreyTex : BaseContent.BlackTex
//                 );
//             }
//             Text.Anchor = TextAnchor.MiddleCenter;
//             Text.Font = GameFont.Tiny;
//             Widgets.Label(barRect, BarLabel);
//             Text.Anchor = TextAnchor.UpperLeft;
//             Text.Font = GameFont.Small;
//             var num = Mathf.Repeat(Time.time, 0.85f);
//             var num2 = 1f;
//             if (num < 0.1f)
//             {
//                 num2 = num / 0.1f;
//             }
//             else if (num >= 0.25f)
//             {
//                 num2 = 1f - (num - 0.25f) / 0.6f;
//             }
//
//             // 预览消耗多少资源
//             if (MapGizmoUtility.LastMouseOverGizmo is Command_Ability commandAbility && resource.Max > 0f)
//             {
//                 foreach (var effectComp in commandAbility.Ability.EffectComps)
//                 {
//                     if (!(effectComp is CompAbilityEffect_NanitesCost compAbilityEffectNanitesCost))
//                     {
//                         continue;
//                     }
//
//                     var props = compAbilityEffectNanitesCost.Props;
//
//                     if(props.nanitesCost < float.Epsilon) continue;
//
//                     var rect = barRect.ContractedBy(3f);
//                     var width = rect.width;
//                     var num3 = resource.Value / resource.Max;
//                     rect.xMax = rect.xMin + width * num3;
//
//                     var num4 = Mathf.Min(props.nanitesCost / resource.Max, 1f);
//                     rect.xMin = Mathf.Max(rect.xMin, rect.xMax - width * num4);
//
//                     GUI.color = new Color(1f, 1f, 1f, num2 * 0.7f);
//                     GenUI.DrawTextureWithMaterial(rect, s_naniteCostTex, null);
//                     GUI.color = Color.white;
//                     return gizmoResult;
//                 }
//             }
//
//             // 悬浮提示
//             if (Mouse.IsOver(barRect))
//             {
//                 Widgets.DrawHighlight(barRect);
//                 // 意思是这个 uniqueid 必须是已经存在的，否则不显示，或者显示会闪烁
//                 TooltipHandler.TipRegion(barRect, GetTooltip,828267373);
//             }
//             return gizmoResult;
//         }
//
//         protected override void DrawHeader(Rect headerRect, ref bool mouseOverElement)
//         {
//             string text = Title;
//             text = text.Truncate(headerRect.width);
//             Text.Anchor = TextAnchor.MiddleCenter;
//             Widgets.Label(headerRect, text);
//             Text.Anchor = TextAnchor.UpperLeft;
//         }
//
//         protected override IEnumerable<float> GetBarThresholds()
//         {
//             for (int i = 0; i < resource.Props.resourceGizmoThresholds.Count; i++)
//             {
//                 yield return resource.Props.resourceGizmoThresholds[i];
//             }
//         }
//     }
// }
