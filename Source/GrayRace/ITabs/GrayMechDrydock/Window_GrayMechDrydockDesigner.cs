using System.Collections.Generic;
using SD.GrayRace.Defs;
using SD.GrayRace.ThingClasses;
using UnityEngine;
using Verse;

namespace SD.GrayRace.ITabs;

public class Window_GrayMechDrydockDesigner : Window
{
    /// <summary>
    /// 原始设计尺寸（设计器画布尺寸的单一出处，曾住在 GrayMechDrydockTabStyle，按“尺寸归窗口自己”搬回）。
    /// 三栏比例就是按这个尺寸推出来的；画布本身会跟随窗口伸缩。
    /// </summary>
    private static readonly Vector2 DesignCanvasSize = new(1780f, 820f);

    /// <summary>
    /// 画布最多能缩到设计尺寸的多少倍。
    /// </summary>
    private const float MinCanvasScale = 0.5f;

    /// <summary>画布最小尺寸。视口小于它时才启用平移滚动条。</summary>
    private static readonly Vector2 MinCanvasSize = DesignCanvasSize * MinCanvasScale;

    /// <summary>
    /// 三栏可分配的主体宽度（原始设计下算出来）：
    /// 设计宽 - 左右外边距 - 关闭按钮预留 - 2 个栏间距 = 1780 - 10*2 - 22 - 12*2 = 1714
    /// </summary>
    private static readonly float BaseBodyWidth = DesignCanvasSize.x
        - GrayMechDrydockTabStyle.Margin * 2f
        - GrayMechDrydockTabStyle.CloseButtonReserveRight
        - GrayMechDrydockTabStyle.PanelGap * 2f;

    /// <summary>左栏比例 = 360 / 1714 ≈ 21.0%，取自原始设计的左栏宽度。</summary>
    private static readonly float LeftColumnRatio = GrayMechDrydockTabStyle.LeftPanelWidth / BaseBodyWidth;

    /// <summary>右栏比例 = 360 / 1714 ≈ 21.0%，取自原始设计的右栏宽度。</summary>
    private static readonly float RightColumnRatio = GrayMechDrydockTabStyle.RightPanelWidth / BaseBodyWidth;

    /// <summary>底部主按钮栏高度。与 MainTabWindow 一致，避免窗口压住它。</summary>
    private const float BottomBarReserve = 35f;

    private const float PanBarSize = 16f;
    private const float CursorOffset = 14f;
    private const float CursorIconSize = 32f;

    private readonly Building_GR_Drydock dock;
    private readonly GrayMechDrydockViewState viewState = new();
    private readonly GrayMechDrydockPresenter state = new();
    private readonly GrayMechDrydockTabController controller;
    private readonly GrayMechDrydockDesignerPanel designerPanel = new();
    private readonly GrayMechDrydockFocusPanel focusPanel = new();
    private readonly GrayMechDrydockBottomBarPanel bottomBarPanel = new();

    private Vector2 canvasPan = Vector2.zero;

    // 大肥鱼这两处代码太弱智了，后面再人工强制改一次
    private Rect rememberedRect;
    private bool geometrySavePending;

    public Window_GrayMechDrydockDesigner(Building_GR_Drydock dock)
    {
        this.dock = dock;
        controller = new GrayMechDrydockTabController(state);

        doWindowBackground = true;
        doCloseX = true;
        doCloseButton = false;
        draggable = true;
        resizeable = true;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = false;
        preventCameraMotion = false;
    }

    public override Vector2 InitialSize
    {
        get
        {
            float width = dock?.DesignerWindowWidth ?? Building_GR_Drydock.DefaultDesignerWindowWidth;
            float height = dock?.DesignerWindowHeight ?? Building_GR_Drydock.DefaultDesignerWindowHeight;
            Vector2 desired = new(width, height);

            desired.x = Mathf.Clamp(desired.x, MinResizeSize.x, UI.screenWidth);
            desired.y = Mathf.Clamp(desired.y, MinResizeSize.y, UI.screenHeight - BottomBarReserve);
            return desired;
        }
    }

    private static readonly Vector2 MinResizeSize = new(150f, 150f);

    protected override void SetInitialSizeAndPosition()
    {
        if (dock != null && dock.HasDesignerWindowPosition)
        {
            Vector2 size = InitialSize;
            float x = Mathf.Clamp(dock.DesignerWindowX, 0f, Mathf.Max(0f, UI.screenWidth - size.x));
            float y = Mathf.Clamp(dock.DesignerWindowY, 0f, Mathf.Max(0f, (float)UI.screenHeight - size.y));
            windowRect = new Rect(x, y, size.x, size.y).Rounded();
        }
        else
        {
            // 首次打开（或重置过位置）：走原版基类逻辑——InitialSize + 屏幕居中。
            base.SetInitialSizeAndPosition();
        }

        rememberedRect = windowRect;
    }

    public override void PostOpen()
    {
        base.PostOpen();
        rememberedRect = windowRect;
    }

    public override void PostClose()
    {
        RememberGeometryIfChanged();
        base.PostClose();
    }

    protected override void LateWindowOnGUI(Rect inRect)
    {
        if (geometrySavePending)
        {
            geometrySavePending = false;
            RememberGeometryIfChanged();
            return;
        }

        if (Event.current.type == EventType.MouseUp)
        {
            geometrySavePending = true;
        }
    }

    private void RememberGeometryIfChanged()
    {
        if (Mathf.Abs(windowRect.x - rememberedRect.x) < 1f
            && Mathf.Abs(windowRect.y - rememberedRect.y) < 1f
            && Mathf.Abs(windowRect.width - rememberedRect.width) < 1f
            && Mathf.Abs(windowRect.height - rememberedRect.height) < 1f)
        {
            return;
        }

        if (dock == null)
        {
            return;
        }

        rememberedRect = windowRect;
        // 几何随建筑保存在存档内：换存档不串，也无需 ModSettings.Write()。
        dock.RememberDesignerWindowGeometry(windowRect.x, windowRect.y, windowRect.width, windowRect.height);
    }

    /// <summary>重置指定干船坞的设计器窗口几何（右键菜单用）。</summary>
    internal static void ResetGeometry(Building_GR_Drydock dock)
    {
        dock?.ResetDesignerWindowGeometry();

        IList<Window> openWindows = Find.WindowStack.Windows;
        for (int i = 0; i < openWindows.Count; i++)
        {
            if (openWindows[i] is Window_GrayMechDrydockDesigner designer && designer.dock == dock)
            {
                // SetInitialSizeAndPosition 末尾会同步 rememberedRect，所以不会又被写回去。
                designer.SetInitialSizeAndPosition();
            }
        }
    }

    /// <summary>设计器打开入口。同一个船坞已经开着时不重复打开。</summary>
    internal static void OpenFor(Building_GR_Drydock dock)
    {
        if (dock == null)
        {
            return;
        }

        IList<Window> openWindows = Find.WindowStack.Windows;
        for (int i = 0; i < openWindows.Count; i++)
        {
            if (openWindows[i] is Window_GrayMechDrydockDesigner existing && existing.dock == dock)
            {
                return;
            }
        }

        // WindowStack.Add 会先移除同类型窗口，所以切换船坞时旧窗口会被替换掉。
        Find.WindowStack.Add(new Window_GrayMechDrydockDesigner(dock));
    }

    internal static void ToggleFor(Building_GR_Drydock dock)
    {
        if (dock == null)
        {
            return;
        }

        IList<Window> openWindows = Find.WindowStack.Windows;
        for (int i = 0; i < openWindows.Count; i++)
        {
            if (openWindows[i] is Window_GrayMechDrydockDesigner existing && existing.dock == dock)
            {
                // Close 就是 WindowStack.TryRemove，gizmo action 里调用是安全的。
                existing.Close(true);
                return;
            }
        }

        OpenFor(dock);
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (dock == null || dock.Destroyed || !dock.Spawned)
        {
            Close();
            return;
        }

        state.EnsureCaches(dock, viewState);
        GrayMechDrydockTabContext context = new(dock, state, viewState, controller);

        ResolveViewport(inRect, out Rect viewport, out bool panX, out bool panY);
        float maxPanX = Mathf.Max(0f, MinCanvasSize.x - viewport.width);
        float maxPanY = Mathf.Max(0f, MinCanvasSize.y - viewport.height);
        canvasPan.x = panX ? Mathf.Clamp(canvasPan.x, 0f, maxPanX) : 0f;
        canvasPan.y = panY ? Mathf.Clamp(canvasPan.y, 0f, maxPanY) : 0f;

        Vector2 canvasSize = new(
            panX ? MinCanvasSize.x : viewport.width,
            panY ? MinCanvasSize.y : viewport.height);
        Rect canvasRect = new(
            panX ? -canvasPan.x : 0f,
            panY ? -canvasPan.y : 0f,
            canvasSize.x,
            canvasSize.y);
        Widgets.BeginGroup(viewport);
        DrawCanvas(context, canvasRect);
        Widgets.EndGroup();

        if (panX)
        {
            // 只有画布停在下限尺寸时才会走到这里，所以滚动范围就是下限宽。
            canvasPan.x = GUI.HorizontalScrollbar(
                new Rect(inRect.x, viewport.yMax, viewport.width, PanBarSize),
                canvasPan.x,
                viewport.width,
                0f,
                MinCanvasSize.x);
        }

        if (panY)
        {
            canvasPan.y = GUI.VerticalScrollbar(
                new Rect(viewport.xMax, inRect.y, PanBarSize, viewport.height),
                canvasPan.y,
                viewport.height,
                0f,
                MinCanvasSize.y);
        }

        DrawArmedModuleCursor(viewport);
    }
    private static void ResolveViewport(Rect inRect, out Rect viewport, out bool panX, out bool panY)
    {
        float availWidth = inRect.width;
        float availHeight = inRect.height;
        panX = false;
        panY = false;
        for (int i = 0; i < 2; i++)
        {
            panX = MinCanvasSize.x > availWidth;
            panY = MinCanvasSize.y > availHeight;
            availWidth = inRect.width - (panY ? PanBarSize : 0f);
            availHeight = inRect.height - (panX ? PanBarSize : 0f);
        }

        viewport = new Rect(inRect.x, inRect.y, Mathf.Max(1f, availWidth), Mathf.Max(1f, availHeight));
    }

    private void DrawCanvas(GrayMechDrydockTabContext context, Rect canvas)
    {
        Rect root = new(
            canvas.x + GrayMechDrydockTabStyle.Margin,
            canvas.y + GrayMechDrydockTabStyle.Margin + GrayMechDrydockTabStyle.CloseButtonReserveTop,
            canvas.width - GrayMechDrydockTabStyle.Margin * 2f - GrayMechDrydockTabStyle.CloseButtonReserveRight,
            canvas.height - GrayMechDrydockTabStyle.Margin * 2f - GrayMechDrydockTabStyle.CloseButtonReserveTop);

        float bodyWidth = Mathf.Max(0f, root.width - GrayMechDrydockTabStyle.PanelGap * 2f);
        float leftPanelWidth = bodyWidth * LeftColumnRatio;
        float rightPanelWidth = bodyWidth * RightColumnRatio;
        float centerPanelWidth = Mathf.Max(0f, bodyWidth - leftPanelWidth - rightPanelWidth);
        float mainBodyWidth = leftPanelWidth + GrayMechDrydockTabStyle.PanelGap + centerPanelWidth;

        Rect mainBodyRect = new(root.x, root.y, mainBodyWidth, root.height);
        Rect summaryRect = new(
            mainBodyRect.xMax + GrayMechDrydockTabStyle.PanelGap,
            root.y,
            rightPanelWidth,
            root.height);

        float bottomBarHeight = state.GetBottomBarHeight(mainBodyWidth);
        float centerHeight = Mathf.Max(120f, mainBodyRect.height - bottomBarHeight - GrayMechDrydockTabStyle.PanelGap);
        Rect upperRect = new(mainBodyRect.x, mainBodyRect.y, mainBodyWidth, centerHeight);
        Rect leftRect = new(upperRect.x, upperRect.y, leftPanelWidth, upperRect.height);
        Rect centerRect = new(
            leftRect.xMax + GrayMechDrydockTabStyle.PanelGap,
            upperRect.y,
            centerPanelWidth,
            upperRect.height);
        Rect bottomRect = new(
            mainBodyRect.x,
            upperRect.yMax + GrayMechDrydockTabStyle.PanelGap,
            mainBodyWidth,
            bottomBarHeight);

        designerPanel.Draw(context, centerRect);
        focusPanel.DrawSelection(context, leftRect);
        focusPanel.DrawSummary(context, summaryRect);
        bottomBarPanel.Draw(context, bottomRect);
    }

    private void DrawArmedModuleCursor(Rect visibleRect)
    {
        GRMechModuleDef armedModule = state.ArmedModule;
        if (armedModule == null)
        {
            return;
        }

        Vector2 mousePosition = Event.current.mousePosition;
        if (!visibleRect.Contains(mousePosition))
        {
            return;
        }

        Rect iconRect = new(mousePosition.x + CursorOffset, mousePosition.y + CursorOffset, CursorIconSize, CursorIconSize);
        iconRect.x = Mathf.Min(iconRect.x, visibleRect.xMax - iconRect.width - 4f);
        iconRect.y = Mathf.Min(iconRect.y, visibleRect.yMax - iconRect.height - 4f);

        Texture2D icon = GrayMechDrydockTabStyle.GetModuleIcon(armedModule);
        if (icon != null)
        {
            Widgets.DrawTextureFitted(iconRect, icon, 1f);
        }
        else if (armedModule.equipmentDef != null)
        {
            Widgets.ThingIcon(iconRect, armedModule.equipmentDef, armedModule.equipmentStuff);
        }
    }
}
