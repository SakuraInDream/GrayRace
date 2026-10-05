using System;
using System.Globalization;
using RimWorld;
using SD.GrayRace.Comps;
using SD.GrayRace.Mechs;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Dialogs;

public class Dialog_HardpointAnchorTuner : Window
{
    private const float TitleHeight = 34f;
    private const float PaneGap = 12f;
    private const float LeftPaneWidth = 260f;
    private const float ListRowHeight = 38f;
    private const float FieldLabelWidth = 126f;
    private const float FieldHeight = 28f;
    private const float AxisLabelWidth = 20f;
    private const float AxisButtonSize = 24f;
    private const float AxisButtonGap = 4f;
    private const float AxisFieldWidth = 92f;
    private const float StepButtonWidth = 64f;
    private const float StepButtonGap = 6f;
    private const float MinAnchor = -10f;
    private const float MaxAnchor = 10f;

    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
    private static readonly float[] StepValues = { 0.01f, 0.05f, 0.1f };
    private static readonly string[] StepLabels = { "0.01", "0.05", "0.1" };
    private const string XDecreaseTip = "X 减少一步";
    private const string XIncreaseTip = "X 增加一步";
    private const string YDecreaseTip = "Y 减少一步";
    private const string YIncreaseTip = "Y 增加一步";

    private readonly CompMultiTurretGun comp;
    private readonly string title;
    private string[] hardpointLabels = Array.Empty<string>();
    private Vector2 listScrollPosition;
    private int observedRevision = -1;
    private int selectedIndex = -1;
    private int selectedStepIndex = 1;
    private int lastLiveRefreshTick = -1;

    private float editX;
    private float editY;
    private string editXBuffer = "0";
    private string editYBuffer = "0";

    private string cachedBaseAnchor = "-";
    private string cachedEffectiveAnchor = "-";
    private string cachedLocalPosition = "-";
    private string cachedWorldPosition = "-";
    private string cachedDeployment = "-";
    private string cachedState = "-";
    private string cachedOverride = "-";
    private string cachedXml = "-";

    public override Vector2 InitialSize => new(760f, 520f);

    internal Dialog_HardpointAnchorTuner(CompMultiTurretGun comp)
    {
        this.comp = comp;
        title = "Hardpoint 锚点调试 - " + (comp?.DebugPawnLabel ?? "Unknown mech");

        layer = WindowLayer.GameUI;
        forcePause = false;
        absorbInputAroundWindow = false;
        preventCameraMotion = false;
        closeOnClickedOutside = false;
        closeOnAccept = false;
        closeOnCancel = true;
        doCloseX = true;
        doWindowBackground = true;
        draggable = true;
        onlyDrawInDevMode = true;

        RebuildHardpointCache();
    }

    public override void WindowUpdate()
    {
        base.WindowUpdate();

        if (!Prefs.DevMode || comp == null || !comp.DebugTargetValid)
        {
            Close(false);
            return;
        }

        if (observedRevision != comp.DebugHardpointRevision)
        {
            RebuildHardpointCache();
        }

        int ticksGame = Find.TickManager?.TicksGame ?? -1;
        if (ticksGame != lastLiveRefreshTick)
        {
            RefreshLiveCache(ticksGame);
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        Color oldColor = GUI.color;

        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(new Rect(0f, 0f, inRect.width, TitleHeight), title);

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        Rect contentRect = new(0f, TitleHeight + 8f, inRect.width, inRect.height - TitleHeight - 8f);
        Rect leftRect = new(contentRect.x, contentRect.y, LeftPaneWidth, contentRect.height);
        Rect rightRect = new(leftRect.xMax + PaneGap, contentRect.y, contentRect.width - LeftPaneWidth - PaneGap, contentRect.height);

        DrawHardpointList(leftRect);
        DrawEditor(rightRect);

        Text.Font = oldFont;
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;
    }

    private void DrawHardpointList(Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Rect inner = rect.ContractedBy(6f);

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(inner.x + 4f, inner.y, inner.width - 8f, 26f), "炮位");

        Rect scrollRect = new(inner.x, inner.y + 28f, inner.width, inner.height - 28f);
        float viewHeight = Mathf.Max(scrollRect.height, hardpointLabels.Length * ListRowHeight);
        Rect viewRect = new(0f, 0f, scrollRect.width - 16f, viewHeight);
        Widgets.BeginScrollView(scrollRect, ref listScrollPosition, viewRect);

        for (int i = 0; i < hardpointLabels.Length; i++)
        {
            Rect rowRect = new(0f, i * ListRowHeight, viewRect.width, ListRowHeight - 2f);
            if (i == selectedIndex)
            {
                Widgets.DrawHighlightSelected(rowRect);
            }
            else if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawHighlight(rowRect);
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rowRect.ContractedBy(6f, 0f), hardpointLabels[i]);
            if (Widgets.ButtonInvisible(rowRect))
            {
                SelectHardpoint(i);
            }
        }

        Widgets.EndScrollView();
    }

    private void DrawEditor(Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Rect inner = rect.ContractedBy(10f);

        if (selectedIndex < 0)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(inner, "当前没有可调试的 Hardpoint");
            return;
        }

        float y = inner.y;
        DrawValueRow(inner, ref y, "XML 基础锚点", cachedBaseAnchor);
        DrawValueRow(inner, ref y, "当前有效锚点", cachedEffectiveAnchor);
        DrawValueRow(inner, ref y, "实时局部位置", cachedLocalPosition);
        DrawValueRow(inner, ref y, "实时世界 X/Z", cachedWorldPosition);
        DrawValueRow(inner, ref y, "展开进度", cachedDeployment);
        DrawValueRow(inner, ref y, "武器状态", cachedState);
        DrawValueRow(inner, ref y, "临时覆盖", cachedOverride);

        y += 8f;
        Widgets.DrawLineHorizontal(inner.x, y, inner.width);
        y += 10f;

        DrawAxisEditor(inner, ref y, "X", ref editX, ref editXBuffer, true);
        DrawAxisEditor(inner, ref y, "Y", ref editY, ref editYBuffer, false);

        y += 4f;
        DrawStepSelector(inner, ref y);

        y += 8f;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(inner.x, y, inner.width, FieldHeight), cachedXml);
        y += FieldHeight + 4f;

        DrawActions(inner, y);
    }

    private static void DrawValueRow(Rect rect, ref float y, string label, string value)
    {
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(rect.x, y, FieldLabelWidth, FieldHeight), label);
        Widgets.Label(new Rect(rect.x + FieldLabelWidth, y, rect.width - FieldLabelWidth, FieldHeight), value);
        y += FieldHeight;
    }

    private void DrawAxisEditor(Rect rect, ref float y, string axisLabel, ref float value, ref string buffer, bool isXAxis)
    {
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(rect.x, y, AxisLabelWidth, FieldHeight), axisLabel);

        float x = rect.x + AxisLabelWidth;
        Rect minusRect = new(x, y + 2f, AxisButtonSize, AxisButtonSize);
        if (Widgets.ButtonImage(minusRect, TexButton.Minus))
        {
            ApplyStep(isXAxis, -StepValues[selectedStepIndex]);
        }
        TooltipHandler.TipRegion(minusRect, isXAxis ? XDecreaseTip : YDecreaseTip);

        x = minusRect.xMax + AxisButtonGap;
        float previousValue = value;
        Widgets.TextFieldNumeric(new Rect(x, y, AxisFieldWidth, FieldHeight), ref value, ref buffer, MinAnchor, MaxAnchor);
        if (!Mathf.Approximately(previousValue, value))
        {
            CommitEditedAnchor();
        }

        x += AxisFieldWidth + AxisButtonGap;
        Rect plusRect = new(x, y + 2f, AxisButtonSize, AxisButtonSize);
        if (Widgets.ButtonImage(plusRect, TexButton.Plus))
        {
            ApplyStep(isXAxis, StepValues[selectedStepIndex]);
        }
        TooltipHandler.TipRegion(plusRect, isXAxis ? XIncreaseTip : YIncreaseTip);

        y += FieldHeight + 4f;
    }

    private void DrawStepSelector(Rect rect, ref float y)
    {
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(rect.x, y, FieldLabelWidth, FieldHeight), "微调步长");

        float x = rect.x + FieldLabelWidth;
        for (int i = 0; i < StepLabels.Length; i++)
        {
            Rect buttonRect = new(x, y, StepButtonWidth, FieldHeight);
            if (Widgets.ButtonText(buttonRect, StepLabels[i], active: i != selectedStepIndex))
            {
                selectedStepIndex = i;
            }

            if (i == selectedStepIndex)
            {
                Widgets.DrawHighlightSelected(buttonRect);
            }

            x += StepButtonWidth + StepButtonGap;
        }

        y += FieldHeight;
    }

    private void DrawActions(Rect rect, float y)
    {
        const float actionGap = 6f;
        float actionWidth = (rect.width - actionGap * 2f) / 3f;

        Rect copyRect = new(rect.x, y, actionWidth, 32f);
        if (Widgets.ButtonText(copyRect, "复制 XML"))
        {
            GUIUtility.systemCopyBuffer = FormatXml(editX, editY);
            Messages.Message("已复制 Hardpoint 锚点 XML", MessageTypeDefOf.TaskCompletion, false);
        }

        Rect resetRect = new(copyRect.xMax + actionGap, y, actionWidth, 32f);
        if (Widgets.ButtonText(resetRect, "重置当前"))
        {
            ResetSelected();
        }

        Rect resetAllRect = new(resetRect.xMax + actionGap, y, actionWidth, 32f);
        if (Widgets.ButtonText(resetAllRect, "重置全部"))
        {
            comp.DebugResetAllHardpointAnchors();
            SyncEditorFromSelected();
            InvalidateLiveCache();
        }
    }

    private void ApplyStep(bool isXAxis, float delta)
    {
        if (isXAxis)
        {
            editX = RoundAnchor(Mathf.Clamp(editX + delta, MinAnchor, MaxAnchor));
            editXBuffer = FormatNumber(editX);
        }
        else
        {
            editY = RoundAnchor(Mathf.Clamp(editY + delta, MinAnchor, MaxAnchor));
            editYBuffer = FormatNumber(editY);
        }

        CommitEditedAnchor();
    }

    private void CommitEditedAnchor()
    {
        editX = Mathf.Clamp(editX, MinAnchor, MaxAnchor);
        editY = Mathf.Clamp(editY, MinAnchor, MaxAnchor);
        if (comp.DebugSetHardpointAnchor(selectedIndex, new Vector2(editX, editY)))
        {
            InvalidateLiveCache();
        }
    }

    private void ResetSelected()
    {
        if (comp.DebugResetHardpointAnchor(selectedIndex))
        {
            SyncEditorFromSelected();
            InvalidateLiveCache();
        }
    }

    private void RebuildHardpointCache()
    {
        observedRevision = comp?.DebugHardpointRevision ?? -1;
        int count = comp?.DebugHardpointCount ?? 0;
        string[] labels = new string[count];

        for (int i = 0; i < count; i++)
        {
            if (comp.TryGetHardpointDebugData(i, out CompMultiTurretGun.HardpointDebugData data))
            {
                labels[i] = BuildHardpointLabel(data);
            }
            else
            {
                labels[i] = "Hardpoint " + (i + 1).ToString(InvariantCulture);
            }
        }

        hardpointLabels = labels;
        if (count == 0)
        {
            selectedIndex = -1;
            ClearCachedValues();
            return;
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, count - 1);
        SyncEditorFromSelected();
        InvalidateLiveCache();
    }

    private void SelectHardpoint(int index)
    {
        if (index == selectedIndex || index < 0 || index >= hardpointLabels.Length)
        {
            return;
        }

        selectedIndex = index;
        SyncEditorFromSelected();
        InvalidateLiveCache();
    }

    private void SyncEditorFromSelected()
    {
        if (!comp.TryGetHardpointDebugData(selectedIndex, out CompMultiTurretGun.HardpointDebugData data))
        {
            selectedIndex = -1;
            ClearCachedValues();
            return;
        }

        editX = data.EffectiveAnchor.x;
        editY = data.EffectiveAnchor.y;
        editXBuffer = FormatNumber(editX);
        editYBuffer = FormatNumber(editY);
    }

    private void RefreshLiveCache(int ticksGame)
    {
        lastLiveRefreshTick = ticksGame;
        if (!comp.TryGetHardpointDebugData(selectedIndex, out CompMultiTurretGun.HardpointDebugData data))
        {
            ClearCachedValues();
            return;
        }

        cachedBaseAnchor = FormatVector(data.BaseAnchor);
        cachedEffectiveAnchor = FormatVector(data.EffectiveAnchor);
        cachedLocalPosition = FormatVector(data.LocalPosition);
        cachedWorldPosition = FormatVector(data.WorldPosition);
        cachedDeployment = data.DeploymentProgress.ToString("0.000", InvariantCulture);
        cachedState = FormatState(data.State);
        cachedOverride = data.HasAnchorOverride ? "已启用" : "未启用";
        cachedXml = FormatXml(data.EffectiveAnchor.x, data.EffectiveAnchor.y);
    }

    private void InvalidateLiveCache()
    {
        lastLiveRefreshTick = int.MinValue;
    }

    private void ClearCachedValues()
    {
        cachedBaseAnchor = "-";
        cachedEffectiveAnchor = "-";
        cachedLocalPosition = "-";
        cachedWorldPosition = "-";
        cachedDeployment = "-";
        cachedState = "-";
        cachedOverride = "-";
        cachedXml = "-";
        InvalidateLiveCache();
    }

    private static string BuildHardpointLabel(CompMultiTurretGun.HardpointDebugData data)
    {
        string section = data.SectionSlotId.NullOrEmpty() ? "?" : data.SectionSlotId;
        string slot = data.SlotKey.NullOrEmpty() ? "?" : data.SlotKey;
        string module = data.ModuleLabel.NullOrEmpty() ? "未命名武器" : data.ModuleLabel;
        return section + " / " + slot + "\n" + module;
    }

    private static string FormatState(MechHardpoint.State state)
    {
        return state switch
        {
            MechHardpoint.State.Idle => "待机",
            MechHardpoint.State.WarmingUp => "瞄准/预热",
            MechHardpoint.State.Firing => "开火",
            MechHardpoint.State.Cooling => "冷却",
            _ => "未知",
        };
    }

    private static float RoundAnchor(float value)
    {
        return Mathf.Round(value * 1000f) * 0.001f;
    }

    private static string FormatNumber(float value)
    {
        return value.ToString("0.###", InvariantCulture);
    }

    private static string FormatVector(Vector2 value)
    {
        return "(" + FormatNumber(value.x) + ", " + FormatNumber(value.y) + ")";
    }

    private static string FormatXml(float x, float y)
    {
        return "<hardpointAnchor>(" + FormatNumber(x) + "," + FormatNumber(y) + ")</hardpointAnchor>";
    }
}
