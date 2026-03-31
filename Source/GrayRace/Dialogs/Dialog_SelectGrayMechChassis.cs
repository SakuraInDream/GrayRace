using System;
using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.ITabs;
using SD.GrayRace.ThingClasses;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Dialogs;

public class Dialog_SelectGrayMechChassis : Window
{
    private const int ColumnCount = 3;
    private const float TitleHeight = 34f;
    private const float CardHeight = 104f;
    private const float CardGap = 6f;

    private readonly Building_GR_Drydock dock;
    private readonly List<GRMechChassisDef> chassisOptions;
    private readonly Action<Building_GR_Drydock, GRMechChassisDef> onSelected;
    private Vector2 scrollPosition = Vector2.zero;

    public override Vector2 InitialSize => new(860f, 520f);

    public Dialog_SelectGrayMechChassis(
        Building_GR_Drydock dock,
        List<GRMechChassisDef> chassisOptions,
        Action<Building_GR_Drydock, GRMechChassisDef> onSelected)
    {
        this.dock = dock;
        this.onSelected = onSelected;
        this.chassisOptions = new List<GRMechChassisDef>(chassisOptions.Count);
        for (int i = 0; i < chassisOptions.Count; i++)
        {
            GRMechChassisDef chassis = chassisOptions[i];
            if (chassis != null)
            {
                this.chassisOptions.Add(chassis);
            }
        }

        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
        doCloseX = true;
        doWindowBackground = true;
        draggable = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Rect titleRect = new Rect(0f, 0f, inRect.width, TitleHeight);
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Medium;
        Widgets.Label(titleRect, "选择船舰型级");
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        Rect contentRect = new Rect(0f, titleRect.yMax + 6f, inRect.width, inRect.height - titleRect.height - 6f);
        float cardWidth = (contentRect.width - CardGap * (ColumnCount - 1)) / ColumnCount;
        int rowCount = Mathf.CeilToInt(chassisOptions.Count / (float)ColumnCount);
        float viewHeight = rowCount * CardHeight + Mathf.Max(0, rowCount - 1) * CardGap;
        Rect viewRect = new Rect(0f, 0f, contentRect.width - 16f, Mathf.Max(contentRect.height, viewHeight));
        Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);

        for (int i = 0; i < chassisOptions.Count; i++)
        {
            GRMechChassisDef chassis = chassisOptions[i];
            int row = i / ColumnCount;
            int column = i % ColumnCount;
            float x = column * (cardWidth + CardGap);
            float y = row * (CardHeight + CardGap);
            DrawChassisCard(new Rect(x, y, cardWidth, CardHeight), chassis);
        }

        Widgets.EndScrollView();
    }

    private void DrawChassisCard(Rect rect, GRMechChassisDef chassis)
    {
        string restrictionReason = string.Empty;
        bool buildableHere = false;
        if (dock != null)
        {
            buildableHere = dock.CanBuildChassis(chassis, out restrictionReason);
        }

        Color accent = buildableHere ? GrayMechDrydockTabStyle.UtilitySlotColor : GrayMechDrydockTabStyle.LockedColor;
        Widgets.DrawBoxSolidWithOutline(rect, GrayMechDrydockTabStyle.CardFill, accent);
        Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 4f), accent);
        if (Mouse.IsOver(rect))
        {
            Widgets.DrawHighlight(rect);
        }

        if (Widgets.ButtonInvisible(rect) && buildableHere)
        {
            onSelected?.Invoke(dock, chassis);
            Close();
            return;
        }

        Rect titleRect = new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 18f);
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Small;
        GUI.color = buildableHere ? new Color(1f, 0.8f, 0.35f) : Color.white;
        Widgets.Label(titleRect, chassis?.LabelCap.ToString() ?? "Unnamed");
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;

        Rect previewRect = new Rect(rect.x + 3f, rect.y + 24f, rect.width - 6f, rect.height - 30f);
        Widgets.DrawBoxSolid(previewRect, GrayMechDrydockTabStyle.BgDark);
        Texture2D preview = GrayMechDrydockTabStyle.GetChassisPreview(chassis);
        if (preview != null)
        {
            Widgets.DrawTextureFitted(previewRect, preview, 1f);
        }
        else if (chassis?.ProducedRace != null)
        {
            Widgets.ThingIcon(previewRect, chassis.ProducedRace);
        }

        string tooltip = chassis?.description ?? string.Empty;
        if (!buildableHere && !restrictionReason.NullOrEmpty())
        {
            tooltip = tooltip.NullOrEmpty() ? restrictionReason : tooltip + "\n\n" + restrictionReason;
        }

        TooltipHandler.TipRegion(rect, tooltip);
    }
}
