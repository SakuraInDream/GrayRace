using RimWorld;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Abilities;

/// <summary>
/// 能力开关按钮：与原版 Command_Toggle 一致，会在图标右上角画勾/叉表示开关状态，
/// 而不是额外生成一个关闭用的 gizmo。
/// </summary>
public class Command_NaniteToggle : Command_Toggle
{
    private readonly Ability_NaniteToggle ability;

    public Command_NaniteToggle(Ability_NaniteToggle ability)
    {
        this.ability = ability;

        defaultLabel = ability.def.LabelCap;
        defaultDesc = ability.def.description;
        icon = ability.def.uiIcon;
        hotKey = ability.def.hotKey;
        shrinkable = true;
        groupable = false;
        Order = 5f + (((float?)ability.def.category?.displayOrder) ?? 0f) / 100f
                   + ability.def.displayOrder / 1000f
                   + ability.def.level / 10000f;

        isActive = () => ability.EffectActive;
        toggleAction = ability.Toggle;
    }

    protected override GizmoResult GizmoOnGUIInt(Rect butRect, GizmoRenderParms parms)
    {
        disabled = ability.GizmoDisabled(out string reason);
        disabledReason = reason;
        return base.GizmoOnGUIInt(butRect, parms);
    }
}
