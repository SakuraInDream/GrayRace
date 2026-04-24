using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.ITabs;

internal static class GrayMechDrydockPanelWidgets
{
    internal static bool DrawButton(Rect rect, string label, bool enabled = true, bool highlighted = false)
    {
        Color oldColor = GUI.color;
        if (!enabled)
        {
            GUI.color = Color.gray;
        }
        else if (highlighted)
        {
            GUI.color = new Color(GrayMechDrydockTabStyle.ReadyColor.r, GrayMechDrydockTabStyle.ReadyColor.g, GrayMechDrydockTabStyle.ReadyColor.b, 0.92f);
        }

        bool pressed = Widgets.ButtonText(rect, label);
        GUI.color = oldColor;
        if (!pressed)
        {
            return false;
        }

        if (enabled)
        {
            return true;
        }

        SoundDefOf.ClickReject.PlayOneShotOnCamera();
        return false;
    }

    internal static void DrawPill(Rect rect, string label, Color color)
    {
        Widgets.DrawBoxSolidWithOutline(
            rect,
            new Color(color.r, color.g, color.b, 0.18f),
            new Color(color.r, color.g, color.b, 0.92f));

        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        bool oldWrap = Text.WordWrap;
        Text.Anchor = TextAnchor.UpperCenter;
        Text.Font = GameFont.Tiny;
        Text.WordWrap = true;
        float labelHeight = GrayMechDrydockTabText.MeasureWrappedTextHeight(label, rect.width - 8f, GameFont.Tiny);
        Rect labelRect = new Rect(rect.x + 4f, rect.y + (rect.height - labelHeight) * 0.5f, rect.width - 8f, labelHeight);
        Widgets.Label(labelRect, label);
        Text.WordWrap = oldWrap;
        Text.Anchor = oldAnchor;
        Text.Font = oldFont;
    }
}
