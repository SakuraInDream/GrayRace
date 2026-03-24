using RimWorld;
using SD.GrayRace.Comps;
using UnityEngine;
using Verse;

namespace SD.GrayRace.UISet.Gizmos;

public class Gizmo_GrayMechShieldStatus : Gizmo
{
    public CompGrayMechShield shield;

    private static readonly Texture2D FullShieldBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.24f));
    private static readonly Texture2D EmptyShieldBarTex = SolidColorMaterials.NewSolidColorTexture(Color.clear);

    public Gizmo_GrayMechShieldStatus()
    {
        Order = -100f;
    }

    public override float GetWidth(float maxWidth)
    {
        return 140f;
    }

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        Rect rect = new(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
        Rect inner = rect.ContractedBy(6f);
        Widgets.DrawWindowBackground(rect);

        Rect titleRect = inner;
        titleRect.height = rect.height / 2f;
        Text.Font = GameFont.Tiny;
        Widgets.Label(titleRect, "ShieldInbuilt".Translate().Resolve());

        Rect barRect = inner;
        barRect.yMin = inner.y + inner.height / 2f;
        float maxEnergy = Mathf.Max(0.001f, shield?.EnergyMax ?? 0.001f);
        float fillPercent = Mathf.Clamp01((shield?.Energy ?? 0f) / maxEnergy);
        Widgets.FillableBar(barRect, fillPercent, FullShieldBarTex, EmptyShieldBarTex, doBorder: false);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(barRect, ((shield?.Energy ?? 0f) * 100f).ToString("F0") + " / " + (maxEnergy * 100f).ToString("F0"));
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(inner, "ShieldPersonalTip".Translate());
        return new GizmoResult(GizmoState.Clear);
    }
}
